using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LvlUp.Models;
using LvlUp.Services;

namespace LvlUp
{
    /// <summary>
    /// Main manager class for LvlUp SDK
    /// Singleton pattern for easy access throughout the application
    /// </summary>
    public class LvlUpManager : MonoBehaviour
    {
        private static LvlUpManager _instance;
        public static LvlUpManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("LvlUpManager");
                    _instance = go.AddComponent<LvlUpManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // Configuration
        private LvlUpConfig _config;
        private LvlUpHttpClient _httpClient;
        private GeoLocationService _geoService;
        private CrashReporter _crashReporter;
        private string _apiKey;
        private string _baseUrl;

        // Session tracking
        private SessionData _currentSession;
        private string _currentUserId;
        private UserMetadata _currentUserMetadata;
        private int _sessionNumber = 0;
        
        // Heartbeat tracking
        private Coroutine _heartbeatCoroutine;
        private float _lastHeartbeatTime;
        private const float HEARTBEAT_INTERVAL = 30f; // Send heartbeat every 30 seconds
        
        // Cached geo data
        private GeoData _cachedGeoData;

        // Event queue for offline support
        private Queue<LvlUpEvent> _eventQueue = new Queue<LvlUpEvent>();
        private List<LvlUpEvent> _eventBatch = new List<LvlUpEvent>();
        private float _lastFlushTime;

        // State
        private bool _isInitialized = false;
        private bool _isSendingEvents = false;
        
        // PlayerPrefs keys for persistence
        private const string PREF_SESSION_NUMBER = "LvlUp_SessionNumber";

        #region Initialization

        /// <summary>
        /// Initialize the LvlUp SDK
        /// </summary>
        /// <param name="apiKey">Your LvlUp API key</param>
        /// <param name="baseUrl">Backend API URL</param>
        /// <param name="config">Optional configuration</param>
        /// <param name="onComplete">Callback when initialization completes</param>
        public static void Initialize(string apiKey, string baseUrl, LvlUpConfig config = null, Action<bool, string> onComplete = null)
        {
            Instance._Initialize(apiKey, baseUrl, config, onComplete);
        }

        private void _Initialize(string apiKey, string baseUrl, LvlUpConfig config = null, Action<bool, string> onComplete = null)
        {
            if (_isInitialized)
            {
                Debug.LogWarning("[LvlUp] SDK already initialized");
                onComplete?.Invoke(false, "SDK already initialized");
                return;
            }

            _apiKey = apiKey;
            _baseUrl = baseUrl;
            _config = config ?? new LvlUpConfig();

            _httpClient = new LvlUpHttpClient(_baseUrl, _apiKey, _config.timeout, _config.enableDebugLogs);
            _geoService = new GeoLocationService();
            _crashReporter = new CrashReporter(_httpClient, _apiKey, null, null);
            
            // Enable crash reporting by default
            if (_config.enableCrashReporting)
            {
                _crashReporter.SetEnabled(true);
                _crashReporter.SetAutoCapture(true);
            }
            
            _isInitialized = true;
            _lastFlushTime = Time.time;

            if (_config.enableDebugLogs)
                Debug.Log($"[LvlUp] SDK Initialized - Base URL: {_baseUrl}");
            
            // Optionally fetch geo data at initialization (async, non-blocking)
            if (_config.enableGeoTracking)
            {
                StartCoroutine(FetchGeoLocationAsync());
            }

            // Start automatic flush coroutine
            if (!_config.sendImmediately)
                StartCoroutine(AutoFlushCoroutine());

            // Auto-start session if enabled
            if (_config.autoTrackSessions)
            {
                // Generate or retrieve user ID
                string autoUserId = GetOrCreateAutoUserId();
                StartSession(autoUserId, null, response =>
                {
                    if (_config.enableDebugLogs)
                    {
                        if (response.success)
                            Debug.Log($"[LvlUp] Auto-started session for user: {autoUserId}");
                        else
                            Debug.LogWarning($"[LvlUp] Failed to auto-start session: {response.error}");
                    }
                    
                    // Invoke initialization complete callback
                    if (response.success)
                        onComplete?.Invoke(true, $"Initialized and session started for user: {autoUserId}");
                    else
                        onComplete?.Invoke(false, $"Initialized but session failed: {response.error}");
                });
            }
            else
            {
                // No auto session - initialization is complete immediately
                onComplete?.Invoke(true, "Initialized successfully (manual session mode)");
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (!_isInitialized || !_config.autoTrackSessions)
                return;

            if (pauseStatus)
            {
                // App going to background - stop heartbeat
                // Backend will auto-close session after 3 minutes of inactivity
                StopHeartbeat();
                
                if (_config.autoTrackAppLifecycle)
                    TrackEvent("app_paused", null);
                
                FlushEventQueue();
                
                // Clear current session - it will be closed by backend timeout
                // We'll start a new session when app resumes
                _currentSession = null;
            }
            else
            {
                // App resumed from background - start a NEW session
                // Don't resume old session because:
                // 1. Backend may have already closed it (after 3 min timeout)
                // 2. Background time shouldn't count as playtime
                // 3. Prevents data inconsistency with endTime
                
                if (_config.autoTrackAppLifecycle)
                    TrackEvent("app_resumed", null);
                
                // Start new session if we have a user
                if (!string.IsNullOrEmpty(_currentUserId))
                {
                    _sessionNumber++; // Increment session number
                    StartSession(_currentUserId, null);
                }
            }
        }

        private void Update()
        {
            // Safety check: Restart heartbeat if session exists but coroutine stopped
            if (_currentSession != null && _heartbeatCoroutine == null && _isInitialized)
            {
                // Check if enough time has passed since last heartbeat
                if (Time.realtimeSinceStartup - _lastHeartbeatTime > HEARTBEAT_INTERVAL * 2)
                {
                    if (_config.enableDebugLogs)
                        Debug.LogWarning("[LvlUp] Heartbeat coroutine stopped unexpectedly, restarting...");
                    
                    StartHeartbeat();
                }
            }
        }

        private void OnApplicationQuit()
        {
            if (_isInitialized && _currentSession != null)
            {
                if (_config.autoTrackAppLifecycle)
                    TrackEvent("app_quit", null);
                
                FlushEventQueue();
                
                // Don't explicitly end session here - let heartbeat timeout handle it
                // This prevents race conditions where:
                // 1. EndSession is called
                // 2. But heartbeats continue for a few more seconds
                // 3. Creating inconsistent data (endTime set but lastHeartbeat continues)
                //
                // Instead:
                // - Heartbeats stop naturally when app quits
                // - Backend auto-closes session after 3 min timeout
                // - Duration is calculated from lastHeartbeat
                
                StopHeartbeat();
            }
        }

        #endregion


        #region Utility Methods

        /// <summary>
        /// Start a new session for a user
        /// </summary>
        public void StartSession(string userId, UserMetadata metadata = null, Action<ApiResponse<SessionData>> callback = null)
        {
            if (!_isInitialized)
            {
                Debug.LogError("[LvlUp] SDK not initialized. Call Initialize() first.");
                callback?.Invoke(new ApiResponse<SessionData> { success = false, error = "SDK not initialized" });
                return;
            }

            _currentUserId = userId;
            _currentUserMetadata = metadata ?? new UserMetadata();
            
            // Increment session number
            _sessionNumber = PlayerPrefs.GetInt(PREF_SESSION_NUMBER, 0) + 1;
            PlayerPrefs.SetInt(PREF_SESSION_NUMBER, _sessionNumber);
            PlayerPrefs.Save();

            // SessionStartRequest auto-populates all metadata via LvlUpEvent constructor
            var request = new SessionStartRequest
            {
                userId = userId,
                sessionNum = _sessionNumber
            };
            
            // Apply cached geo data if available
            if (_config.enableGeoTracking && _cachedGeoData != null)
            {
                request.SetGeoLocation(
                    _cachedGeoData.country,
                    _cachedGeoData.countryCode,
                    _cachedGeoData.region,
                    _cachedGeoData.city,
                    _cachedGeoData.latitude,
                    _cachedGeoData.longitude,
                    _cachedGeoData.timezone
                );
            }

            StartCoroutine(_httpClient.Post<SessionData>("analytics/sessions", request, response =>
            {
                if (response.success)
                {
                    _currentSession = response.data;
                    
                    // Start heartbeat coroutine
                    StartHeartbeat();
                    
                    if (_config.enableDebugLogs)
                        Debug.Log($"[LvlUp] Session started: {_currentSession.sessionId}");
                }
                callback?.Invoke(response);
            }));
        }

        /// <summary>
        /// End the current session
        /// </summary>
        public void EndSession(Action<ApiResponse<SessionData>> callback = null)
        {
            if (_currentSession == null)
            {
                callback?.Invoke(new ApiResponse<SessionData> { success = false, error = "No active session" });
                return;
            }

            // SessionEndRequest auto-populates all metadata via LvlUpEvent constructor
            var request = new SessionEndRequest
            {
                sessionId = _currentSession.sessionId,
                sessionNum = _sessionNumber
            };
            
            // Apply cached geo data if available (may have changed during session)
            if (_config.enableGeoTracking && _cachedGeoData != null)
            {
                request.SetGeoLocation(
                    _cachedGeoData.country,
                    _cachedGeoData.countryCode,
                    _cachedGeoData.region,
                    _cachedGeoData.city,
                    _cachedGeoData.latitude,
                    _cachedGeoData.longitude,
                    _cachedGeoData.timezone
                );
            }


            StartCoroutine(_httpClient.Put<SessionData>($"analytics/sessions/{_currentSession.sessionId}", request, response =>
            {
                if (response.success)
                {
                    // Stop heartbeat coroutine
                    StopHeartbeat();
                    
                    if (_config.enableDebugLogs)
                        Debug.Log($"[LvlUp] Session ended: {_currentSession.sessionId}");
                    _currentSession = null;
                }
                callback?.Invoke(response);
            }));
        }

        private IEnumerator EndSessionCoroutine()
        {
            bool completed = false;
            EndSession(response => completed = true);
            
            float startTime = Time.time;
            while (!completed && Time.time - startTime < 5f)
            {
                yield return null;
            }
        }

        #endregion

        #region Heartbeat

        /// <summary>
        /// Start sending session heartbeats
        /// </summary>
        private void StartHeartbeat()
        {
            // Stop any existing heartbeat coroutine
            StopHeartbeat();
            
            if (_currentSession != null)
            {
                _lastHeartbeatTime = Time.realtimeSinceStartup;
                _heartbeatCoroutine = StartCoroutine(HeartbeatCoroutine());
                
                if (_config.enableDebugLogs)
                    Debug.Log($"[LvlUp] Heartbeat started for session: {_currentSession.sessionId}");
            }
        }

        /// <summary>
        /// Stop sending session heartbeats
        /// </summary>
        private void StopHeartbeat()
        {
            if (_heartbeatCoroutine != null)
            {
                StopCoroutine(_heartbeatCoroutine);
                _heartbeatCoroutine = null;
                
                if (_config.enableDebugLogs)
                    Debug.Log("[LvlUp] Heartbeat stopped");
            }
        }

        /// <summary>
        /// Coroutine that sends heartbeats at regular intervals
        /// Uses WaitForSecondsRealtime to work even when Time.timeScale = 0
        /// </summary>
        private IEnumerator HeartbeatCoroutine()
        {
            while (_currentSession != null && _isInitialized)
            {
                // Wait for the heartbeat interval using realtime (unaffected by Time.timeScale)
                yield return new WaitForSecondsRealtime(HEARTBEAT_INTERVAL);
                
                // Double-check session still exists before sending
                if (_currentSession != null && _isInitialized)
                {
                    SendHeartbeat();
                }
            }
            
            if (_config.enableDebugLogs)
                Debug.Log("[LvlUp] Heartbeat coroutine ended");
        }

        /// <summary>
        /// Send a single heartbeat to keep the session alive
        /// </summary>
        private void SendHeartbeat()
        {
            if (_currentSession == null)
                return;

            string endpoint = $"analytics/sessions/{_currentSession.sessionId}/heartbeat";
            
            // Send empty object instead of null to avoid JSON parsing errors
            var emptyRequest = new { };
            
            StartCoroutine(_httpClient.Post<object>(endpoint, emptyRequest, response =>
            {
                if (response.success)
                {
                    _lastHeartbeatTime = Time.realtimeSinceStartup;
                    
                    if (_config.enableDebugLogs)
                        Debug.Log($"[LvlUp] Heartbeat sent for session: {_currentSession.sessionId}");
                }
                else
                {
                    if (_config.enableDebugLogs)
                        Debug.LogWarning($"[LvlUp] Heartbeat failed: {response.error}");
                }
            }));
        }

        #endregion

        #region Geographic Location

        /// <summary>
        /// Fetch geographic location data asynchronously
        /// </summary>
        private IEnumerator FetchGeoLocationAsync()
        {
            yield return _geoService.FetchGeoLocation(
                onSuccess: (geoData) =>
                {
                    _cachedGeoData = geoData;
                    if (_config.enableDebugLogs)
                        Debug.Log($"[LvlUp] Geo location fetched: {geoData.city}, {geoData.region}, {geoData.country}");
                },
                onError: (error) =>
                {
                    if (_config.enableDebugLogs)
                        Debug.LogWarning($"[LvlUp] Failed to fetch geo location: {error}");
                }
            );
        }

        /// <summary>
        /// Manually trigger geo location fetch (public API)
        /// </summary>
        public void RefreshGeoLocation(Action<GeoData> onSuccess = null, Action<string> onError = null)
        {
            StartCoroutine(_geoService.FetchGeoLocation(
                onSuccess: (geoData) =>
                {
                    _cachedGeoData = geoData;
                    onSuccess?.Invoke(geoData);
                },
                onError: onError
            ));
        }

        /// <summary>
        /// Get current cached geo data
        /// </summary>
        public GeoData GetCachedGeoData()
        {
            return _cachedGeoData;
        }

        /// <summary>
        /// Apply cached geo data to an event
        /// </summary>
        private void ApplyGeoDataToEvent(LvlUpEvent evt)
        {
            if (_cachedGeoData != null && _cachedGeoData.IsValid())
            {
                evt.SetGeoLocation(
                    _cachedGeoData.country,
                    _cachedGeoData.countryCode,
                    _cachedGeoData.region,
                    _cachedGeoData.city,
                    _cachedGeoData.latitude,
                    _cachedGeoData.longitude,
                    _cachedGeoData.timezone
                );
            }
        }

        #endregion

        #region Event Tracking

        /// <summary>
        /// Track a single event (convenience method - creates LvlUpEvent internally)
        /// </summary>
        public void TrackEvent(string eventName, Dictionary<string, object> properties, Action<ApiResponse> callback = null)
        {
            var lvlUpEvent = new LvlUpEvent(eventName, properties);
            TrackEvent(lvlUpEvent, callback);
        }

        /// <summary>
        /// Track an event (direct method - accepts any LvlUpEvent or subclass like LevelEvent)
        /// </summary>
        public void TrackEvent(LvlUpEvent lvlUpEvent, Action<ApiResponse> callback = null)
        {
            if (!_isInitialized)
            {
                Debug.LogError("[LvlUp] SDK not initialized. Call Initialize() first.");
                callback?.Invoke(new ApiResponse { success = false, error = "SDK not initialized" });
                return;
            }
            
            // Add session number if available
            if (_sessionNumber > 0)
                lvlUpEvent.sessionNum = _sessionNumber;
            
            // Apply cached geo data if available
            if (_config.enableGeoTracking)
                ApplyGeoDataToEvent(lvlUpEvent);

            if (_config.sendImmediately)
            {
                SendEventImmediately(lvlUpEvent, callback);
            }
            else
            {
                _eventBatch.Add(lvlUpEvent);

                if (_config.enableDebugLogs)
                    Debug.Log($"[LvlUp] Event queued: {lvlUpEvent.eventName} (Batch size: {_eventBatch.Count}/{_config.eventBatchSize})");

                // Check if we need to flush
                if (_eventBatch.Count >= _config.eventBatchSize)
                {
                    FlushEventQueue();
                }

                callback?.Invoke(new ApiResponse { success = true, message = "Event queued" });
            }
        }

        /// <summary>
        /// Set level funnel configuration after initialization
        /// Useful when fetching funnel assignment from backend (Remote Config or A/B Test)
        /// </summary>
        /// <param name="levelFunnel">Level funnel name (e.g., "live_v1", "test_hard")</param>
        /// <param name="levelFunnelVersion">Level funnel version number (e.g., 1, 2, 3)</param>
        public void SetLevelFunnel(string levelFunnel, int levelFunnelVersion)
        {
            _config.levelFunnel = levelFunnel;
            _config.levelFunnelVersion = levelFunnelVersion;
            
            if (_config.enableDebugLogs)
                Debug.Log($"[LvlUp] Level funnel updated: {levelFunnel} (v{levelFunnelVersion})");
        }

        /// <summary>
        /// Get current level funnel configuration
        /// </summary>
        public (string funnel, int version) GetLevelFunnel()
        {
            return (_config.levelFunnel, _config.levelFunnelVersion);
        }


        /// <summary>
        /// Track multiple events in a batch
        /// </summary>
        public void TrackEventsBatch(List<LvlUpEvent> events, Action<ApiResponse> callback = null)
        {
            if (!_isInitialized)
            {
                Debug.LogError("[LvlUp] SDK not initialized. Call Initialize() first.");
                callback?.Invoke(new ApiResponse { success = false, error = "SDK not initialized" });
                return;
            }

            if (events == null || events.Count == 0)
            {
                callback?.Invoke(new ApiResponse { success = false, error = "No events provided" });
                return;
            }

            var batchRequest = new BatchEventRequest
            {
                userId = _currentUserId,
                sessionId = _currentSession?.sessionId,
                events = new List<EventDataItem>(),
            };

            foreach (var evt in events)
            {
                // Use the conversion method - much cleaner!
                batchRequest.events.Add(evt.ToEventDataItem());
            }

            StartCoroutine(_httpClient.Post<object>("analytics/events/batch", batchRequest, response =>
            {
                if (_config.enableDebugLogs)
                    Debug.Log($"[LvlUp] Batch sent: {events.Count} events - Success: {response.success}");
                
                callback?.Invoke(new ApiResponse 
                { 
                    success = response.success, 
                    error = response.error,
                    message = response.message 
                });
            }));
        }

        private void SendEventImmediately(LvlUpEvent lvlUpEvent, Action<ApiResponse> callback)
        {
            // Send as a single-event batch to ensure all metadata fields are included
            var batchRequest = new BatchEventRequest
            {
                userId = _currentUserId,
                sessionId = _currentSession?.sessionId,
                events = new List<EventDataItem> { lvlUpEvent.ToEventDataItem() },
            };

            StartCoroutine(_httpClient.Post<object>("analytics/events/batch", batchRequest, response =>
            {
                if (_config.enableDebugLogs)
                    Debug.Log($"[LvlUp] Event sent: {lvlUpEvent.eventName} - Success: {response.success}");
                
                callback?.Invoke(new ApiResponse 
                { 
                    success = response.success, 
                    error = response.error,
                    message = response.message 
                });
            }));
        }

        /// <summary>
        /// Flush all queued events to the server
        /// </summary>
        public void FlushEventQueue(Action<ApiResponse> callback = null)
        {
            if (_eventBatch.Count == 0 || _isSendingEvents)
            {
                callback?.Invoke(new ApiResponse { success = true, message = "No events to flush" });
                return;
            }

            var eventsToSend = new List<LvlUpEvent>(_eventBatch);
            _eventBatch.Clear();
            _isSendingEvents = true;

            TrackEventsBatch(eventsToSend, response =>
            {
                _isSendingEvents = false;
                _lastFlushTime = Time.time;
                callback?.Invoke(response);
            });
        }

        private IEnumerator AutoFlushCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(_config.eventFlushInterval);

                if (_eventBatch.Count > 0 && !_isSendingEvents)
                {
                    if (_config.enableDebugLogs)
                        Debug.Log($"[LvlUp] Auto-flushing {_eventBatch.Count} events");
                    
                    FlushEventQueue();
                }
            }
        }

        /// <summary>
        /// Get the number of queued events
        /// </summary>
        public int GetQueuedEventCount()
        {
            return _eventBatch.Count;
        }

        #endregion

        #region Player Journey

        /// <summary>
        /// Create a new checkpoint
        /// </summary>
        public void CreateCheckpoint(string name, string description, string type, int order, string[] tags = null, Action<ApiResponse<Checkpoint>> callback = null)
        {
            if (!_isInitialized)
            {
                Debug.LogError("[LvlUp] SDK not initialized. Call Initialize() first.");
                callback?.Invoke(new ApiResponse<Checkpoint> { success = false, error = "SDK not initialized" });
                return;
            }

            var request = new CheckpointRequest
            {
                name = name,
                description = description,
                type = type,
                order = order,
                tags = tags ?? new string[0]
            };

            StartCoroutine(_httpClient.Post<Checkpoint>("analytics/journey/checkpoints", request, callback));
        }

        /// <summary>
        /// Record a checkpoint completion for the current user
        /// </summary>
        public void RecordCheckpoint(string checkpointId, Dictionary<string, object> metadata = null, Action<ApiResponse> callback = null)
        {
            if (!_isInitialized)
            {
                Debug.LogError("[LvlUp] SDK not initialized. Call Initialize() first.");
                callback?.Invoke(new ApiResponse { success = false, error = "SDK not initialized" });
                return;
            }

            if (string.IsNullOrEmpty(_currentUserId))
            {
                Debug.LogError("[LvlUp] No active user. Call StartSession() first.");
                callback?.Invoke(new ApiResponse { success = false, error = "No active user" });
                return;
            }

            // CheckpointRecordRequest auto-populates all metadata via LvlUpEvent constructor
            var request = new CheckpointRecordRequest
            {
                userId = _currentUserId,
                checkpointId = checkpointId,
                metadata = metadata ?? new Dictionary<string, object>(),
                sessionNum = _sessionNumber
            };
            
            // Apply cached geo data if available
            if (_config.enableGeoTracking && _cachedGeoData != null)
            {
                request.SetGeoLocation(
                    _cachedGeoData.country,
                    _cachedGeoData.countryCode,
                    _cachedGeoData.region,
                    _cachedGeoData.city,
                    _cachedGeoData.latitude,
                    _cachedGeoData.longitude,
                    _cachedGeoData.timezone
                );
            }

            StartCoroutine(_httpClient.Post<object>("analytics/journey/record", request, response =>
            {
                callback?.Invoke(new ApiResponse 
                { 
                    success = response.success, 
                    error = response.error,
                    message = response.message 
                });
            }));
        }

        /// <summary>
        /// Get player journey progress for current user
        /// </summary>
        public void GetPlayerJourneyProgress(Action<ApiResponse<PlayerJourneyProgress>> callback = null)
        {
            if (!_isInitialized)
            {
                Debug.LogError("[LvlUp] SDK not initialized. Call Initialize() first.");
                callback?.Invoke(new ApiResponse<PlayerJourneyProgress> { success = false, error = "SDK not initialized" });
                return;
            }

            if (string.IsNullOrEmpty(_currentUserId))
            {
                Debug.LogError("[LvlUp] No active user. Call StartSession() first.");
                callback?.Invoke(new ApiResponse<PlayerJourneyProgress> { success = false, error = "No active user" });
                return;
            }

            StartCoroutine(_httpClient.Get<PlayerJourneyProgress>($"analytics/journey/progress/{_currentUserId}", callback));
        }

        #endregion


        #region Utility Methods

        /// <summary>
        /// Check if SDK is initialized
        /// </summary>
        public bool IsInitialized()
        {
            return _isInitialized;
        }

        /// <summary>
        /// Get current session data
        /// </summary>
        public SessionData GetCurrentSession()
        {
            return _currentSession;
        }

        /// <summary>
        /// Get current user ID
        /// </summary>
        public string GetCurrentUserId()
        {
            return _currentUserId;
        }

        /// <summary>
        /// Get or create a persistent user ID for auto-tracking
        /// </summary>
        private string GetOrCreateAutoUserId()
        {
            const string USER_ID_KEY = "LvlUp_AutoUserId";
            
            string userId = PlayerPrefs.GetString(USER_ID_KEY, "");
            
            if (string.IsNullOrEmpty(userId))
            {
                // Generate a new unique user ID using device identifier
                userId = SystemInfo.deviceUniqueIdentifier;
                
                // If device identifier is not available, generate a GUID
                if (string.IsNullOrEmpty(userId) || userId == SystemInfo.unsupportedIdentifier)
                {
                    userId = $"auto_{System.Guid.NewGuid().ToString()}";
                }
                
                PlayerPrefs.SetString(USER_ID_KEY, userId);
                PlayerPrefs.Save();
            }
            
            return userId;
        }

        #endregion

        #region Crash Reporting

        /// <summary>
        /// Add a breadcrumb to track user actions
        /// </summary>
        public static void AddBreadcrumb(string message, BreadcrumbType type = BreadcrumbType.Navigation, Dictionary<string, object> data = null)
        {
            if (Instance._crashReporter != null)
            {
                Instance._crashReporter.AddBreadcrumb(message, type, data);
            }
        }

        /// <summary>
        /// Report an exception manually
        /// </summary>
        public static void ReportException(Exception exception, string context = null, Dictionary<string, object> customData = null)
        {
            if (Instance._crashReporter != null)
            {
                Instance._crashReporter.ReportException(exception, context, customData);
            }
        }

        /// <summary>
        /// Report an error manually
        /// </summary>
        public static void ReportError(string message, string stackTrace = null, Dictionary<string, object> customData = null)
        {
            if (Instance._crashReporter != null)
            {
                Instance._crashReporter.ReportError(message, stackTrace, customData);
            }
        }

        /// <summary>
        /// Enable or disable crash reporting
        /// </summary>
        public static void SetCrashReportingEnabled(bool enabled)
        {
            if (Instance._crashReporter != null)
            {
                Instance._crashReporter.SetEnabled(enabled);
            }
        }

        #endregion
    }
}

