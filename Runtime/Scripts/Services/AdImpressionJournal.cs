using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace LvlUp.Services
{
    internal class PendingAdImpression
    {
        public string adImpressionId;
        public long timestamp;
        public string adNetworkName;
        public string adFormat;
        public double revenue;
        public string adUnitId;
        public string placement;
        public string creativeId;
    }

    /// <summary>
    /// Writes each ad impression to disk on the thread that reports it, before the revenue queue sees it.
    /// MAX reports full-screen ad revenue on a background thread while the ad is still showing, when Unity's
    /// main thread (and PlayerPrefs) is paused, so a process killed during the ad would otherwise lose it.
    /// Entries stay on disk until the revenue queue has stored them, and are replayed on the next launch.
    /// </summary>
    internal class AdImpressionJournal
    {
        private const int MAX_PENDING = 500;

        private readonly object _gate = new object();
        private readonly string _path;
        private readonly List<PendingAdImpression> _pending = new List<PendingAdImpression>();

        internal AdImpressionJournal(string path)
        {
            _path = path;
            LoadFromDisk();
        }

        internal int PendingCount
        {
            get
            {
                lock (_gate)
                {
                    return _pending.Count;
                }
            }
        }

        internal void Append(PendingAdImpression impression)
        {
            lock (_gate)
            {
                if (_pending.Count >= MAX_PENDING)
                {
                    _pending.RemoveAt(0);
                    _pending.Add(impression);
                    RewriteFile();
                    return;
                }

                _pending.Add(impression);

                try
                {
                    File.AppendAllText(_path, JsonConvert.SerializeObject(impression) + "\n");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LvlUp] Failed to journal ad impression: {ex.Message}");
                }
            }
        }

        internal List<PendingAdImpression> TakeSnapshot()
        {
            lock (_gate)
            {
                return new List<PendingAdImpression>(_pending);
            }
        }

        internal void Acknowledge(List<PendingAdImpression> handled)
        {
            if (handled == null || handled.Count == 0)
                return;

            lock (_gate)
            {
                var handledSet = new HashSet<PendingAdImpression>(handled);
                _pending.RemoveAll(handledSet.Contains);
                RewriteFile();
            }
        }

        private void LoadFromDisk()
        {
            try
            {
                if (!File.Exists(_path))
                    return;

                foreach (string line in File.ReadAllLines(_path))
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    try
                    {
                        var impression = JsonConvert.DeserializeObject<PendingAdImpression>(line);
                        if (impression != null && !string.IsNullOrEmpty(impression.adImpressionId))
                            _pending.Add(impression);
                    }
                    catch (Exception)
                    {
                        // A line cut short by a kill mid-write is skipped; the rest still replay.
                    }
                }

                if (_pending.Count > MAX_PENDING)
                    _pending.RemoveRange(0, _pending.Count - MAX_PENDING);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LvlUp] Failed to read ad impression journal: {ex.Message}");
            }
        }

        private void RewriteFile()
        {
            try
            {
                if (_pending.Count == 0)
                {
                    if (File.Exists(_path))
                        File.Delete(_path);
                    return;
                }

                var lines = new List<string>(_pending.Count);
                foreach (var impression in _pending)
                    lines.Add(JsonConvert.SerializeObject(impression));

                File.WriteAllText(_path, string.Join("\n", lines) + "\n");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LvlUp] Failed to rewrite ad impression journal: {ex.Message}");
            }
        }
    }
}
