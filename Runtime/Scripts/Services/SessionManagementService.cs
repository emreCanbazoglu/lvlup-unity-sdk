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
        private bool _isProcessingSessionStarts = false;
        private bool _isProcessingSessionEnds = false;

        // Heartbeat tracking
        private Coroutine _heartbeatCoroutine;
        private float _lastHeartbeatTime;
        private bool _heartbeatRequestInFlight;
        private int _consecutiveHeartbeatFailures;
        private DateTime? _pausedAtUtc;
        private static readonly float[] HEARTBEAT_RETRY_DELAYS = new float[] { 2f, 5f };
        private const float HEARTBEAT_INTERVAL = 30f;
        private const float RESUME_EXISTING_SESSION_GRACE_SECONDS = 150f;

        // PlayerPrefs keys
        private const string PREF_SESSION_NUMBER = "LvlUp_SessionNumber";
        private const string PREF_PENDING_SESSION_STARTS = "LvlUp_PendingSessionStarts";
        private const string PREF_PENDING_SESSION_START_COUNT = "LvlUp_PendingSessionStartCount";
        private const string PREF_PENDING_SESSION_ENDS = "LvlUp_PendingSessionEnds";
        private const string PREF_PENDING_SESSION_END_COUNT = "LvlUp_PendingSessionEndCount";

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
            _sessionNumber = PlayerPrefs.GetInt(PREF_SESSION_NUMBER, 0) + 1;
            PlayerPrefs.SetInt(PREF_SESSION_NUMBER, _sessionNumber);
            PlayerPrefs.Save();

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

                    // Start heartbeat and send an immediate ping to reduce the first-gap window
                    StartHeartbeat(sendImmediately: true);

                    // Retry any pending ends from previous sessions (e.g. D0 end queued from HandleApplicationQuit)
                    RetryPendingSessions();

                    if (_config.enableDebugLogs)
                        Debug.Log($"[LvlUp] Session started: {_currentSession.sessionId}");
                }
                else
                {
                    // Failed to start session (likely offline)
                    _pendingSessionStarts.Add(request);
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
                _pendingSessionEnds.Add(request);
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
                    _pendingSessionEnds.Add(request);
                    PersistPendingSessions();

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
                if (_isProcessingSessionStarts)
                {
                    if (_config.enableDebugLogs)
                        Debug.Log("[LvlUp] Session start retry already in progress, skipping.");
                    return;
                }

                if (_config.enableDebugLogs)
                    Debug.Log($"[LvlUp] Retrying {_pendingSessionStarts.Count} pending session starts...");

                _coroutineRunner.StartCoroutine(ProcessPendingSessionStarts());
            }
            else if (_pendingSessionEnds.Count > 0)
            {
                if (_isProcessingSessionEnds)
                {
                    if (_config.enableDebugLogs)
                        Debug.Log("[LvlUp] Session end retry already in progress, skipping.");
                    return;
                }

                // No pending starts but there are pending ends (e.g. previous session end queued from HandleApplicationQuit)
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
                // App going to background - stop heartbeat but keep session metadata.
                // This allows short pauses/resumes without forcing a new session.
                _pausedAtUtc = DateTime.UtcNow;
                StopHeartbeat();
            }
            else
            {
                var pausedForSeconds = _pausedAtUtc.HasValue
                    ? (float)(DateTime.UtcNow - _pausedAtUtc.Value).TotalSeconds
                    : 0f;
                _pausedAtUtc = null;

                // Short pause: resume same session and send an immediate heartbeat.
                if (_currentSession != null && pausedForSeconds <= RESUME_EXISTING_SESSION_GRACE_SECONDS)
                {
                    StartHeartbeat(sendImmediately: true);
                    return;
                }

                // Long pause: let backend timeout the old session and create a new one on resume.
                if (!string.IsNullOrEmpty(_currentUserId))
                {
                    _currentSession = null;
                    StartSession(_currentUserId, _currentUserMetadata);
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

        private void StartHeartbeat(bool sendImmediately = false)
        {
            StopHeartbeat();

            if (_currentSession != null)
            {
                _lastHeartbeatTime = Time.realtimeSinceStartup;
                _heartbeatCoroutine = _coroutineRunner.StartCoroutine(HeartbeatCoroutine());

                if (_config.enableDebugLogs)
                    Debug.Log($"[LvlUp] Heartbeat started for session: {_currentSession.sessionId}");

                if (sendImmediately)
                {
                    SendHeartbeat();
                }
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

            if (_heartbeatRequestInFlight)
            {
                if (_config.enableDebugLogs)
                    Debug.LogWarning("[LvlUp] Skipping heartbeat tick because a previous heartbeat request is still in flight");
                return;
            }

            string sessionId = _currentSession.sessionId;
            string endpoint = $"analytics/sessions/{sessionId}/heartbeat";

            var geoData = _getGeoData();
            var heartbeatData = new
            {
                countryCode = (_config.enableGeoTracking && geoData != null)
                    ? geoData.countryCode
                    : (string)null
            };

            _coroutineRunner.StartCoroutine(SendHeartbeatWithRetryCoroutine(sessionId, endpoint, heartbeatData));
        }

        private IEnumerator SendHeartbeatWithRetryCoroutine(string sessionId, string endpoint, object heartbeatData)
        {
            _heartbeatRequestInFlight = true;

            try
            {
                for (int attempt = 0; attempt <= HEARTBEAT_RETRY_DELAYS.Length; attempt++)
                {
                    ApiResponse<object> heartbeatResponse = null;

                    yield return _httpClient.Post<object>(endpoint, heartbeatData, response =>
                    {
                        heartbeatResponse = response;
                    });

                    // Session changed while request was in flight; ignore stale heartbeat result.
                    if (_currentSession == null || _currentSession.sessionId != sessionId)
                    {
                        yield break;
                    }

                    if (heartbeatResponse != null && heartbeatResponse.success)
                    {
                        _lastHeartbeatTime = Time.realtimeSinceStartup;
                        _consecutiveHeartbeatFailures = 0;

                        if (_config.enableDebugLogs)
                            Debug.Log($"[LvlUp] Heartbeat sent for session: {sessionId}");

                        yield break;
                    }

                    _consecutiveHeartbeatFailures++;

                    if (_config.enableDebugLogs)
                        Debug.LogWarning($"[LvlUp] Heartbeat failed (attempt {attempt + 1}/{HEARTBEAT_RETRY_DELAYS.Length + 1}): {heartbeatResponse?.error}");

                    if (attempt < HEARTBEAT_RETRY_DELAYS.Length)
                    {
                        yield return new WaitForSecondsRealtime(HEARTBEAT_RETRY_DELAYS[attempt]);
                    }
                }
            }
            finally
            {
                _heartbeatRequestInFlight = false;
            }
        }

        public void HandleApplicationQuit()
        {
            StopHeartbeat();

            if (_currentSession == null && !_hasOfflineSession)
                return;

            var request = new SessionEndRequest
            {
                sessionId = _currentSession?.sessionId,
                sessionNum = _sessionNumber
            };

            var geoData = _getGeoData();
            if (_config.enableGeoTracking && geoData != null)
            {
                request.SetGeoLocation(geoData.countryCode);
            }

            _pendingSessionEnds.Add(request);
            PersistPendingSessions();

            if (_config.enableDebugLogs)
                Debug.Log($"[LvlUp] Queued session end on quit for session: {request.sessionId}");
        }

        private void PersistPendingSessions()
        {
            try
            {
                // Persist session starts
                PlayerPrefs.SetInt(PREF_PENDING_SESSION_START_COUNT, _pendingSessionStarts.Count);
                for (int i = 0; i < _pendingSessionStarts.Count; i++)
                {
                    string json = JsonUtility.ToJson(_pendingSessionStarts[i]);
                    PlayerPrefs.SetString($"{PREF_PENDING_SESSION_STARTS}_{i}", json);
                }

                // Persist session ends
                PlayerPrefs.SetInt(PREF_PENDING_SESSION_END_COUNT, _pendingSessionEnds.Count);
                for (int i = 0; i < _pendingSessionEnds.Count; i++)
                {
                    string json = JsonUtility.ToJson(_pendingSessionEnds[i]);
                    PlayerPrefs.SetString($"{PREF_PENDING_SESSION_ENDS}_{i}", json);
                }

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
                                _pendingSessionStarts.Add(request);
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
                                _pendingSessionEnds.Add(request);
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

        private IEnumerator ProcessPendingSessionStarts()
        {
            _isProcessingSessionStarts = true;

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
                    if (_pendingSessionStarts.Count > 0)
                    {
                        _pendingSessionStarts.RemoveAt(0);
                        PersistPendingSessions();
                    }
                }
                else
                {
                    if (_config.enableDebugLogs)
                        Debug.LogWarning($"[LvlUp] Stopping session retry - still offline. {_pendingSessionStarts.Count} sessions remain queued.");
                    _isProcessingSessionStarts = false;
                    yield break;
                }

                yield return new WaitForSeconds(0.5f);
            }

            _isProcessingSessionStarts = false;

            // Process session ends after starts
            if (_pendingSessionEnds.Count > 0)
            {
                if (_isProcessingSessionEnds)
                {
                    if (_config.enableDebugLogs)
                        Debug.Log("[LvlUp] Session end retry already in progress, skipping chained start.");
                }
                else
                {
                    if (_config.enableDebugLogs)
                        Debug.Log($"[LvlUp] Retrying {_pendingSessionEnds.Count} pending session ends...");

                    _coroutineRunner.StartCoroutine(ProcessPendingSessionEnds());
                }
            }

            if (_pendingSessionStarts.Count == 0 && _pendingSessionEnds.Count == 0)
            {
                _hasOfflineSession = false;
            }
        }

        private IEnumerator ProcessPendingSessionEnds()
        {
            _isProcessingSessionEnds = true;

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
                    if (_pendingSessionEnds.Count > 0)
                    {
                        _pendingSessionEnds.RemoveAt(0);
                        PersistPendingSessions();
                    }
                }
                else
                {
                    if (_config.enableDebugLogs)
                        Debug.LogWarning($"[LvlUp] Stopping session end retry. {_pendingSessionEnds.Count} session ends remain queued.");
                    _isProcessingSessionEnds = false;
                    yield break;
                }

                yield return new WaitForSeconds(0.5f);
            }

            _isProcessingSessionEnds = false;

            if (_pendingSessionStarts.Count == 0 && _pendingSessionEnds.Count == 0)
            {
                _hasOfflineSession = false;
            }
        }
    }
}

