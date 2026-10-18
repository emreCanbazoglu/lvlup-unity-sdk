using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace LvlUp.Services
{
    [Serializable]
    internal sealed class AttributionData
    {
        public string provider;
        public string network;
        public string campaign;
        public string adgroup;
        public string creative;
        public string trackerName;
        public string trackerToken;
        public string clickLabel;

        internal AttributionData Normalize() => new AttributionData
        {
            provider = Clean(provider), network = Clean(network), campaign = Clean(campaign),
            adgroup = Clean(adgroup), creative = Clean(creative), trackerName = Clean(trackerName),
            trackerToken = Clean(trackerToken), clickLabel = Clean(clickLabel)
        };

        private static string Clean(string value) => value?.Trim() ?? "";
    }

    internal sealed class AttributionRequest
    {
        public string userId;
        public string deviceId;
        public AttributionData attribution;
        public string observedAt;
    }

    internal interface IAttributionStorage
    {
        string Read();
        void Write(string value);
    }

    internal sealed class PlayerPrefsAttributionStorage : IAttributionStorage
    {
        private const string Key = "lvlup_attribution_sent_v1";
        public string Read() => PlayerPrefs.GetString(Key, "");
        public void Write(string value)
        {
            PlayerPrefs.SetString(Key, value);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Tracks the latest install attribution without delaying SDK or session startup.</summary>
    internal sealed class AttributionTrackingService : IDisposable
    {
        private static readonly double[] RetryDelays = { 10, 30, 60, 120, 300 };
        private readonly LvlUpConfig _config;
        private readonly Func<string> _getUserId;
        private readonly Func<string> _getDeviceId;
        private readonly IAttributionStorage _storage;
        private readonly Func<AttributionRequest, Action<bool>, IEnumerator> _send;
        private readonly Action<IEnumerator> _startCoroutine;
        private readonly Func<double> _now;
        private readonly Action<Action<AttributionData>> _readAttribution;
        // SDK callbacks may run on worker threads. Only Tick accesses Unity or mutable state.
        private readonly ConcurrentQueue<Action> _mainThread = new ConcurrentQueue<Action>();
        private AttributionData _latest;
        private string _observedAt;
        private bool _sending;
        private volatile bool _disposed;
        private int _generation;
        private int _session;
        private int _retryIndex;
        private double _retryAt = double.PositiveInfinity;
        private string _failedFingerprint;
        private int _failedSession;

        internal AttributionTrackingService(LvlUpHttpClient client, LvlUpConfig config,
            MonoBehaviour runner, Func<string> getUserId)
            : this(config, getUserId, () => SystemInfo.deviceUniqueIdentifier,
                new PlayerPrefsAttributionStorage(),
                (request, complete) => client.Post<AttributionResponse>("analytics/attribution", request,
                    response => complete(response != null && response.success && response.data != null &&
                        response.data.success != false)),
                routine => runner.StartCoroutine(routine), () => Time.realtimeSinceStartup,
                ReadAdjustAttribution)
        {
        }

        internal AttributionTrackingService(LvlUpConfig config, Func<string> getUserId,
            Func<string> getDeviceId, IAttributionStorage storage,
            Func<AttributionRequest, Action<bool>, IEnumerator> send, Action<IEnumerator> startCoroutine,
            Func<double> now, Action<Action<AttributionData>> readAttribution)
        {
            _config = config;
            _getUserId = getUserId;
            _getDeviceId = getDeviceId;
            _storage = storage;
            _send = send;
            _startCoroutine = startCoroutine;
            _now = now;
            _readAttribution = readAttribution;
        }

        internal void Initialize() => Refresh();

        internal void OnSessionStarted() => Enqueue(() =>
        {
            _session++;
            TrySend();
            BeginRefresh();
        });

        internal void Set(string network, string campaign = null, string adgroup = null,
            string creative = null, string trackerName = null) => Enqueue(() =>
        {
            // Explicit manual input supersedes pending Adjust reads/retries.
            _generation++;
            _retryAt = double.PositiveInfinity;
            Observe(new AttributionData { provider = "manual", network = network, campaign = campaign,
                adgroup = adgroup, creative = creative, trackerName = trackerName });
        });

        internal void Refresh() => Enqueue(BeginRefresh);

        internal void Tick()
        {
            if (_disposed) return;
            while (_mainThread.TryDequeue(out var action))
            {
                try { action(); }
                catch (Exception ex) { Log(ex.Message); }
            }
            try
            {
                if (_now() >= _retryAt)
                {
                    _retryAt = double.PositiveInfinity;
                    RequestAttribution();
                }
            }
            catch (Exception ex) { Log(ex.Message); }
        }

        private void Enqueue(Action action)
        {
            if (!_disposed) _mainThread.Enqueue(action);
        }

        private void BeginRefresh()
        {
            _generation++;
            _retryIndex = 0;
            _retryAt = double.PositiveInfinity;
            RequestAttribution();
        }

        private void RequestAttribution()
        {
            int generation = _generation;
            try
            {
                _readAttribution?.Invoke(data => Enqueue(() =>
                {
                    if (generation != _generation) return;
                    var normalized = data?.Normalize();
                    if (normalized == null || (normalized.network.Length == 0 && normalized.trackerToken.Length == 0))
                    {
                        ScheduleRetry();
                        return;
                    }
                    _retryAt = double.PositiveInfinity;
                    Observe(normalized);
                }));
            }
            catch (Exception ex)
            {
                Log(ex.Message);
                ScheduleRetry();
            }
        }

        private void ScheduleRetry()
        {
            if (_retryIndex < RetryDelays.Length)
                _retryAt = _now() + RetryDelays[_retryIndex++];
        }

        private void Observe(AttributionData data)
        {
            _latest = data.Normalize();
            _observedAt = DateTime.UtcNow.ToString("O");
            TrySend();
        }

        private void TrySend()
        {
            if (_sending || _latest == null || _disposed) return;
            var userId = _getUserId();
            // Initialization can precede the first session's user identity.
            if (string.IsNullOrWhiteSpace(userId)) return;
            var request = new AttributionRequest { userId = userId, deviceId = _getDeviceId(),
                attribution = _latest, observedAt = _observedAt };
            // Observation time is deliberately excluded: time alone is not a change.
            string fingerprint = JsonConvert.SerializeObject(new { request.userId, request.deviceId, request.attribution });
            if (fingerprint == _storage.Read() ||
                (fingerprint == _failedFingerprint && _session == _failedSession)) return;
            _sending = true;
            int session = _session;
            bool completed = false;
            Action<bool> complete = success => Enqueue(() =>
            {
                if (completed) return;
                completed = true;
                _sending = false;
                if (success)
                {
                    try { _storage.Write(fingerprint); }
                    catch (Exception ex) { Log(ex.Message); }
                }
                else
                {
                    _failedFingerprint = fingerprint;
                    _failedSession = session;
                    Log("Send failed; attribution remains pending until the next session.");
                }
                // Serialize sends so an older response never overwrites a newer observation.
                if (session != _session || !ReferenceEquals(request.attribution, _latest))
                    TrySend();
            });
            try { _startCoroutine(GuardSend(_send(request, complete), complete)); }
            catch (Exception ex)
            {
                Log(ex.Message);
                complete(false);
            }
        }

        private IEnumerator GuardSend(IEnumerator routine, Action<bool> complete)
        {
            // Also contain exceptions thrown after a coroutine's first yield.
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            try
            {
                while (stack.Count > 0)
                {
                    object current = null;
                    bool moved;
                    try
                    {
                        moved = stack.Peek().MoveNext();
                        if (moved) current = stack.Peek().Current;
                    }
                    catch (Exception ex)
                    {
                        Log(ex.Message);
                        complete(false);
                        yield break;
                    }
                    if (!moved)
                    {
                        try { (stack.Pop() as IDisposable)?.Dispose(); }
                        catch (Exception ex)
                        {
                            Log(ex.Message);
                            complete(false);
                            yield break;
                        }
                        continue;
                    }
                    if (current is IEnumerator nested) stack.Push(nested);
                    else yield return current;
                }
            }
            finally
            {
                while (stack.Count > 0)
                {
                    try { (stack.Pop() as IDisposable)?.Dispose(); }
                    catch (Exception ex) { Log(ex.Message); }
                }
            }
        }

        private void Log(string message)
        {
            if (_config.enableDebugLogs) Debug.LogWarning($"[LvlUp] Attribution: {message}");
        }

        public void Dispose()
        {
            _disposed = true;
            while (_mainThread.TryDequeue(out _)) { }
        }

        private sealed class AttributionResponse
        {
            public bool? success { get; set; }
            public bool changed { get; set; }
        }

        private static void ReadAdjustAttribution(Action<AttributionData> callback)
        {
#if lvlup_adjust_enabled
            AdjustSdk.Adjust.GetAttribution(value => callback(value == null ? null : new AttributionData
            {
                provider = "adjust", network = value.Network, campaign = value.Campaign,
                adgroup = value.Adgroup, creative = value.Creative, trackerName = value.TrackerName,
                trackerToken = value.TrackerToken, clickLabel = value.ClickLabel
            }));
#endif
        }
    }
}
