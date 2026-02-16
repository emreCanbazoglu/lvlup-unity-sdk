using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LvlUp.Models;

namespace LvlUp.Services
{
    /// <summary>
    /// Service responsible for managing user sessions
    /// Handles session start, end, heartbeats, and offline session persistence
    /// </summary>
    public class SessionManagementService
    {
        private readonly LvlUpHttpClient _httpClient;
        private readonly LvlUpConfig _config;
        private readonly MonoBehaviour _coroutineRunner;
        private readonly Func<GeoData> _getGeoData;
        private readonly Action<string> _onSessionIdChanged;
        private readonly Action<string> _onUserIdChanged;

        // Session tracking
        private SessionData _currentSession;
        private string _currentUserId;
        private UserMetadata _currentUserMetadata;
        private int _sessionNumber = 0;

        // Offline session tracking
        private List<SessionStartRequest> _pendingSessionStarts = new List<SessionStartRequest>();
        private List<SessionEndRequest> _pendingSessionEnds = new List<SessionEndRequest>();
        private bool _hasOfflineSession = false;

        // Heartbeat tracking
        private Coroutine _heartbeatCoroutine;
        private float _lastHeartbeatTime;
        private const float HEARTBEAT_INTERVAL = 30f;

        // PlayerPrefs keys
        private const string PREF_SESSION_NUMBER = "LvlUp_SessionNumber";
        private const string PREF_PENDING_SESSION_STARTS = "LvlUp_PendingSessionStarts";
        private const string PREF_PENDING_SESSION_START_COUNT = "LvlUp_PendingSessionStartCount";
        private const string PREF_PENDING_SESSION_ENDS = "LvlUp_PendingSessionEnds";
        private const string PREF_PENDING_SESSION_END_COUNT = "LvlUp_PendingSessionEndCount";
        private const int MAX_PENDING_SESSION_STARTS_HARD_CAP = 500;
        private const int MAX_PENDING_SESSION_ENDS_HARD_CAP = 500;

        public SessionManagementService(
            LvlUpHttpClient httpClient,
            LvlUpConfig config,
            MonoBehaviour coroutineRunner,
            Func<GeoData> getGeoData,
            Action<string> onSessionIdChanged,
            Action<string> onUserIdChanged)
        {
            _httpClient = httpClient;
            _config = config;
            _coroutineRunner = coroutineRunner;
            _getGeoData = getGeoData;
            _onSessionIdChanged = onSessionIdChanged;
            _onUserIdChanged = onUserIdChanged;
        }

        /// <summary>
        /// Initialize service - load pending sessions
        /// </summary>
        public void Initialize()
        {
            LoadPendingSessions();
        }

        /// <summary>
        /// Start a new session for a user
        /// </summary>
        public void StartSession(string userId, UserMetadata metadata = null, Action<ApiResponse<SessionData>> callback = null)
        {
            _currentUserId = userId;
            _currentUserMetadata = metadata ?? new UserMetadata();

            _onUserIdChanged?.Invoke(_currentUserId);

            // Increment session number
            if (_config.persistQueueToDisk)
            {
                _sessionNumber = PlayerPrefs.GetInt(PREF_SESSION_NUMBER, 0) + 1;
                PlayerPrefs.SetInt(PREF_SESSION_NUMBER, _sessionNumber);
                PlayerPrefs.Save();
            }
            else
            {
                _sessionNumber += 1;
            }

            var request = new SessionStartRequest
            {
                userId = userId,
                sessionNum = _sessionNumber
            };

            // Apply cached geo data if available
            var geoData = _getGeoData();
            if (_config.enableGeoTracking && geoData != null)
            {
                request.SetGeoLocation(
                    geoData.countryCode
                );
            }

            _coroutineRunner.StartCoroutine(_httpClient.Post<SessionData>("analytics/sessions", request, response =>
            {
                if (response.success)
                {
                    _currentSession = response.data;
                    _hasOfflineSession = false;

                    _onSessionIdChanged?.Invoke(_currentSession.sessionId);

                    // Start heartbeat
                    StartHeartbeat();
                    // Send one heartbeat immediately so short-lived sessions are observed by backend.
                    SendHeartbeat();

                    if (_config.enableDebugLogs)
                        Debug.Log($"[LvlUp] Session started: {_currentSession.sessionId}");
                }
                else
                {
                    // Failed to start session (likely offline)
                    EnqueuePendingSessionStart(request);
                    _hasOfflineSession = true;

                    PersistPendingSessions();

                    if (_config.enableDebugLogs)
                        Debug.LogWarning($"[LvlUp] Failed to start session (offline?). Queued for retry. Total pending: {_pendingSessionStarts.Count}");
                }
                callback?.Invoke(response);
            }));
        }

        /// <summary>
        /// End the current session
        /// </summary>
        public void EndSession(Action<ApiResponse<SessionData>> callback = null)
        {
            if (_currentSession == null && !_hasOfflineSession)
            {
                callback?.Invoke(new ApiResponse<SessionData> { success = false, error = "No active session" });
                return;
            }

            var request = new SessionEndRequest
            {
                sessionId = _currentSession?.sessionId,
                sessionNum = _sessionNumber
            };

            // Apply cached geo data if available
            var geoData = _getGeoData();
            if (_config.enableGeoTracking && geoData != null)
            {
                request.SetGeoLocation(
                    geoData.countryCode
                );
            }

            // If we have an offline session, just store the end request
            if (_hasOfflineSession && _currentSession == null)
            {
                EnqueuePendingSessionEnd(request);
                PersistPendingSessions();

                if (_config.enableDebugLogs)
                    Debug.Log($"[LvlUp] Offline session end queued. Total pending ends: {_pendingSessionEnds.Count}");

                callback?.Invoke(new ApiResponse<SessionData> { success = true, message = "Offline session end queued" });
                return;
            }

            if (_currentSession == null)
            {
                callback?.Invoke(new ApiResponse<SessionData> { success = false, error = "No active session" });
                return;
            }

            string sessionId = _currentSession.sessionId;

            _coroutineRunner.StartCoroutine(_httpClient.Put<SessionData>($"analytics/sessions/{sessionId}", request, response =>
            {
                if (response.success)
                {
                    StopHeartbeat();

                    if (_config.enableDebugLogs)
                        Debug.Log($"[LvlUp] Session ended: {sessionId}");
                    
                    _currentSession = null;
                }
                else
                {
                    // Failed to end session - add to pending list
                    EnqueuePendingSessionEnd(request);
                    PersistPendingSessions();
                    // Session close was requested; clear local session state to avoid overlap on next start.
                    StopHeartbeat();
                    _currentSession = null;

                    if (_config.enableDebugLogs)
                        Debug.LogWarning($"[LvlUp] Failed to end session. Queued for retry. Total pending ends: {_pendingSessionEnds.Count}");
                }
                callback?.Invoke(response);
            }));
        }

        /// <summary>
        /// Retry pending session starts/ends
        /// </summary>
        public void RetryPendingSessions()
        {
            if (_pendingSessionStarts.Count > 0)
            {
                if (_config.enableDebugLogs)
                    Debug.Log($"[LvlUp] Retrying {_pendingSessionStarts.Count} pending session starts...");

                _coroutineRunner.StartCoroutine(ProcessPendingSessionStarts());
            }
            else if (_pendingSessionEnds.Count > 0)
            {
                if (_config.enableDebugLogs)
                    Debug.Log($"[LvlUp] Retrying {_pendingSessionEnds.Count} pending session ends...");

                _coroutineRunner.StartCoroutine(ProcessPendingSessionEnds());
            }
        }

        /// <summary>
        /// Handle app pause/resume for session management
        /// </summary>
        public void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                // App going to background - stop heartbeat and attempt to close active session.
                if (_currentSession != null)
                {
                    StopHeartbeat();
                    EndSession(response =>
                    {
                        if (_config.enableDebugLogs && !response.success)
                            Debug.LogWarning($"[LvlUp] Pause-triggered EndSession failed: {response.error}");
                    });
                }
            }
            else
            {
                // App resumed - start new session if we have a user
                if (!string.IsNullOrEmpty(_currentUserId))
                {
                    StartSession(_currentUserId, null);
                }
            }
        }

        /// <summary>
        /// Get current session data
        /// </summary>
        public SessionData GetCurrentSession() => _currentSession;

        /// <summary>
        /// Get current user ID
        /// </summary>
        public string GetCurrentUserId() => _currentUserId;

        /// <summary>
        /// Get current session number
        /// </summary>
        public int GetSessionNumber() => _sessionNumber;

        /// <summary>
        /// Check if session exists but heartbeat stopped
        /// </summary>
        public bool ShouldRestartHeartbeat()
        {
            return _currentSession != null && _heartbeatCoroutine == null && 
                   Time.realtimeSinceStartup - _lastHeartbeatTime > HEARTBEAT_INTERVAL * 2;
        }

        /// <summary>
        /// Restart heartbeat if needed
        /// </summary>
        public void RestartHeartbeatIfNeeded()
        {
            if (ShouldRestartHeartbeat())
            {
                if (_config.enableDebugLogs)
                    Debug.LogWarning("[LvlUp] Heartbeat coroutine stopped unexpectedly, restarting...");

                StartHeartbeat();
            }
        }

        private void StartHeartbeat()
        {
            StopHeartbeat();

            if (_currentSession != null)
            {
                _lastHeartbeatTime = Time.realtimeSinceStartup;
                _heartbeatCoroutine = _coroutineRunner.StartCoroutine(HeartbeatCoroutine());

                if (_config.enableDebugLogs)
                    Debug.Log($"[LvlUp] Heartbeat started for session: {_currentSession.sessionId}");
            }
        }

        private void StopHeartbeat()
        {
            if (_heartbeatCoroutine != null)
            {
                _coroutineRunner.StopCoroutine(_heartbeatCoroutine);
                _heartbeatCoroutine = null;

                if (_config.enableDebugLogs)
                    Debug.Log("[LvlUp] Heartbeat stopped");
            }
        }

        private IEnumerator HeartbeatCoroutine()
        {
            while (_currentSession != null)
            {
                yield return new WaitForSecondsRealtime(HEARTBEAT_INTERVAL);

                if (_currentSession != null)
                {
                    SendHeartbeat();
                }
            }

            if (_config.enableDebugLogs)
                Debug.Log("[LvlUp] Heartbeat coroutine ended");
        }

        private void SendHeartbeat()
        {
            if (_currentSession == null)
                return;

            string sessionId = _currentSession.sessionId;
            string endpoint = $"analytics/sessions/{sessionId}/heartbeat";

            var geoData = _getGeoData();
            var heartbeatData = new
            {
                countryCode = (_config.enableGeoTracking && geoData != null)
                    ? geoData.countryCode
                    : (string)null
            };

            _coroutineRunner.StartCoroutine(_httpClient.Post<object>(endpoint, heartbeatData, response =>
            {
                if (response.success)
                {
                    _lastHeartbeatTime = Time.realtimeSinceStartup;

                    if (_config.enableDebugLogs)
                        Debug.Log($"[LvlUp] Heartbeat sent for session: {sessionId}");
                }
                else
                {
                    if (_config.enableDebugLogs)
                        Debug.LogWarning($"[LvlUp] Heartbeat failed: {response.error}");
                }
            }));
        }

        private void PersistPendingSessions()
        {
            if (!_config.persistQueueToDisk)
                return;

            try
            {
                EnforcePendingQueueCaps();

                int previousStartCount = PlayerPrefs.GetInt(PREF_PENDING_SESSION_START_COUNT, 0);
                int previousEndCount = PlayerPrefs.GetInt(PREF_PENDING_SESSION_END_COUNT, 0);

                // Persist session starts
                PlayerPrefs.SetInt(PREF_PENDING_SESSION_START_COUNT, _pendingSessionStarts.Count);
                for (int i = 0; i < _pendingSessionStarts.Count; i++)
                {
                    string json = JsonUtility.ToJson(_pendingSessionStarts[i]);
                    PlayerPrefs.SetString($"{PREF_PENDING_SESSION_STARTS}_{i}", json);
                }
                DeleteIndexedPrefRange(PREF_PENDING_SESSION_STARTS, _pendingSessionStarts.Count, previousStartCount);

                // Persist session ends
                PlayerPrefs.SetInt(PREF_PENDING_SESSION_END_COUNT, _pendingSessionEnds.Count);
                for (int i = 0; i < _pendingSessionEnds.Count; i++)
                {
                    string json = JsonUtility.ToJson(_pendingSessionEnds[i]);
                    PlayerPrefs.SetString($"{PREF_PENDING_SESSION_ENDS}_{i}", json);
                }
                DeleteIndexedPrefRange(PREF_PENDING_SESSION_ENDS, _pendingSessionEnds.Count, previousEndCount);

                PlayerPrefs.Save();

                if (_config.enableDebugLogs)
                    Debug.Log($"[LvlUp] Persisted {_pendingSessionStarts.Count} session starts and {_pendingSessionEnds.Count} session ends");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LvlUp] Failed to persist pending sessions: {ex.Message}");
            }
        }

        private void LoadPendingSessions()
        {
            if (!_config.persistQueueToDisk)
                return;

            try
            {
                // Load pending session starts
                int startCount = PlayerPrefs.GetInt(PREF_PENDING_SESSION_START_COUNT, 0);
                for (int i = 0; i < startCount; i++)
                {
                    string key = $"{PREF_PENDING_SESSION_STARTS}_{i}";
                    if (PlayerPrefs.HasKey(key))
                    {
                        string json = PlayerPrefs.GetString(key);
                        try
                        {
                            var request = JsonUtility.FromJson<SessionStartRequest>(json);
                            if (request != null)
                            {
                                EnqueuePendingSessionStart(request);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[LvlUp] Failed to deserialize session start {i}: {ex.Message}");
                        }

                        PlayerPrefs.DeleteKey(key);
                    }
                }

                // Load pending session ends
                int endCount = PlayerPrefs.GetInt(PREF_PENDING_SESSION_END_COUNT, 0);
                for (int i = 0; i < endCount; i++)
                {
                    string key = $"{PREF_PENDING_SESSION_ENDS}_{i}";
                    if (PlayerPrefs.HasKey(key))
                    {
                        string json = PlayerPrefs.GetString(key);
                        try
                        {
                            var request = JsonUtility.FromJson<SessionEndRequest>(json);
                            if (request != null)
                            {
                                EnqueuePendingSessionEnd(request);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[LvlUp] Failed to deserialize session end {i}: {ex.Message}");
                        }

                        PlayerPrefs.DeleteKey(key);
                    }
                }

                PlayerPrefs.DeleteKey(PREF_PENDING_SESSION_START_COUNT);
                PlayerPrefs.DeleteKey(PREF_PENDING_SESSION_END_COUNT);
                PlayerPrefs.Save();

                if (_pendingSessionStarts.Count > 0 || _pendingSessionEnds.Count > 0)
                {
                    _hasOfflineSession = true;

                    if (_config.enableDebugLogs)
                        Debug.Log($"[LvlUp] Loaded {_pendingSessionStarts.Count} pending session starts and {_pendingSessionEnds.Count} session ends from previous session");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LvlUp] Failed to load pending sessions: {ex.Message}");
            }
        }

        private int GetPendingSessionStartsCap()
        {
            return Mathf.Max(1, Mathf.Min(_config.maxQueueSize, MAX_PENDING_SESSION_STARTS_HARD_CAP));
        }

        private int GetPendingSessionEndsCap()
        {
            return Mathf.Max(1, Mathf.Min(_config.maxQueueSize, MAX_PENDING_SESSION_ENDS_HARD_CAP));
        }

        private void EnforcePendingQueueCaps()
        {
            TrimPendingStartsToCap();
            TrimPendingEndsToCap();
        }

        private void EnqueuePendingSessionStart(SessionStartRequest request)
        {
            _pendingSessionStarts.Add(request);
            TrimPendingStartsToCap();
        }

        private void EnqueuePendingSessionEnd(SessionEndRequest request)
        {
            _pendingSessionEnds.Add(request);
            TrimPendingEndsToCap();
        }

        private void TrimPendingStartsToCap()
        {
            int cap = GetPendingSessionStartsCap();
            if (_pendingSessionStarts.Count <= cap)
                return;

            int overflow = _pendingSessionStarts.Count - cap;
            _pendingSessionStarts.RemoveRange(0, overflow);

            if (_config.enableDebugLogs)
                Debug.LogWarning($"[LvlUp] Pending session start queue cap reached ({cap}). Dropped {overflow} oldest item(s).");
        }

        private void TrimPendingEndsToCap()
        {
            int cap = GetPendingSessionEndsCap();
            if (_pendingSessionEnds.Count <= cap)
                return;

            int overflow = _pendingSessionEnds.Count - cap;
            _pendingSessionEnds.RemoveRange(0, overflow);

            if (_config.enableDebugLogs)
                Debug.LogWarning($"[LvlUp] Pending session end queue cap reached ({cap}). Dropped {overflow} oldest item(s).");
        }

        private void DeleteIndexedPrefRange(string prefix, int startInclusive, int endExclusive)
        {
            if (endExclusive <= startInclusive)
                return;

            for (int i = startInclusive; i < endExclusive; i++)
            {
                PlayerPrefs.DeleteKey($"{prefix}_{i}");
            }
        }

        private IEnumerator ProcessPendingSessionStarts()
        {
            while (_pendingSessionStarts.Count > 0)
            {
                var request = _pendingSessionStarts[0];
                bool completed = false;
                bool success = false;

                _coroutineRunner.StartCoroutine(_httpClient.Post<SessionData>("analytics/sessions", request, response =>
                {
                    success = response.success;
                    completed = true;

                    if (response.success)
                    {
                        if (_config.enableDebugLogs)
                            Debug.Log($"[LvlUp] Successfully started offline session: {response.data.sessionId}");
                    }
                    else
                    {
                        if (_config.enableDebugLogs)
                            Debug.LogWarning($"[LvlUp] Still cannot start session (offline?): {response.error}");
                    }
                }));

                yield return new WaitUntil(() => completed);

                if (success)
                {
                    _pendingSessionStarts.RemoveAt(0);
                    PersistPendingSessions();
                }
                else
                {
                    if (_config.enableDebugLogs)
                        Debug.LogWarning($"[LvlUp] Stopping session retry - still offline. {_pendingSessionStarts.Count} sessions remain queued.");
                    yield break;
                }

                yield return new WaitForSeconds(0.5f);
            }

            // Process session ends after starts
            if (_pendingSessionEnds.Count > 0)
            {
                if (_config.enableDebugLogs)
                    Debug.Log($"[LvlUp] Retrying {_pendingSessionEnds.Count} pending session ends...");

                _coroutineRunner.StartCoroutine(ProcessPendingSessionEnds());
            }

            if (_pendingSessionStarts.Count == 0 && _pendingSessionEnds.Count == 0)
            {
                _hasOfflineSession = false;
            }
        }

        private IEnumerator ProcessPendingSessionEnds()
        {
            while (_pendingSessionEnds.Count > 0)
            {
                var request = _pendingSessionEnds[0];

                if (string.IsNullOrEmpty(request.sessionId))
                {
                    if (_config.enableDebugLogs)
                        Debug.LogWarning($"[LvlUp] Removing session end with no sessionId (offline session never started)");

                    _pendingSessionEnds.RemoveAt(0);
                    PersistPendingSessions();
                    continue;
                }

                bool completed = false;
                bool success = false;

                _coroutineRunner.StartCoroutine(_httpClient.Put<SessionData>($"analytics/sessions/{request.sessionId}", request, response =>
                {
                    success = response.success;
                    completed = true;

                    if (response.success)
                    {
                        if (_config.enableDebugLogs)
                            Debug.Log($"[LvlUp] Successfully ended offline session: {request.sessionId}");
                    }
                    else
                    {
                        if (_config.enableDebugLogs)
                            Debug.LogWarning($"[LvlUp] Failed to end session: {response.error}");
                    }
                }));

                yield return new WaitUntil(() => completed);

                if (success)
                {
                    _pendingSessionEnds.RemoveAt(0);
                    PersistPendingSessions();
                }
                else
                {
                    if (_config.enableDebugLogs)
                        Debug.LogWarning($"[LvlUp] Stopping session end retry. {_pendingSessionEnds.Count} session ends remain queued.");
                    yield break;
                }

                yield return new WaitForSeconds(0.5f);
            }

            if (_pendingSessionStarts.Count == 0 && _pendingSessionEnds.Count == 0)
            {
                _hasOfflineSession = false;
            }
        }
    }
}
