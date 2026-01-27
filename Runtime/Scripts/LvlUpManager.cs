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
        private RemoteConfigService _remoteConfigService;
        private string _apiKey;
        private string _baseUrl;

        // Session tracking
        private SessionData _currentSession;
        private string _currentUserId;
        private UserMetadata _currentUserMetadata;
        private int _sessionNumber = 0;
        
        // Offline session tracking - support multiple offline sessions
        private List<SessionStartRequest> _pendingSessionStarts = new List<SessionStartRequest>();
        private List<SessionEndRequest> _pendingSessionEnds = new List<SessionEndRequest>();
        private bool _hasOfflineSession = false;
        
        // Heartbeat tracking
        private Coroutine _heartbeatCoroutine;
        private float _lastHeartbeatTime;
        private const float HEARTBEAT_INTERVAL = 30f; // Send heartbeat every 30 seconds
        
        // Server limits
        private const int MAX_BATCH_SIZE = 100; // Maximum events per batch allowed by server
        
        // Cached geo data
        private GeoData _cachedGeoData;

        // Event queue for offline support
        private Queue<LvlUpEvent> _eventQueue = new Queue<LvlUpEvent>();
        private List<LvlUpEvent> _eventBatch = new List<LvlUpEvent>();
        private float _lastFlushTime;
        private bool _hasLoadedPersistedEvents = false;

        // State
        private bool _isInitialized = false;
        private bool _isSendingEvents = false;
        
        // PlayerPrefs keys for persistence
        private const string PREF_SESSION_NUMBER = "LvlUp_SessionNumber";
        private const string PREF_OFFLINE_EVENTS = "LvlUp_OfflineEvents";
        private const string PREF_OFFLINE_EVENT_COUNT = "LvlUp_OfflineEventCount";
        private const string PREF_PENDING_SESSION_STARTS = "LvlUp_PendingSessionStarts";
        private const string PREF_PENDING_SESSION_START_COUNT = "LvlUp_PendingSessionStartCount";
        private const string PREF_PENDING_SESSION_ENDS = "LvlUp_PendingSessionEnds";
        private const string PREF_PENDING_SESSION_END_COUNT = "LvlUp_PendingSessionEndCount";

        #region Initialization

        /// <summary>
        /// Initialize the LvlUp SDK by automatically loading config from Resources
        /// Loads LvlUpConfig.asset from Assets/lvlup-unity-sdk/Resources/
        /// </summary>
        /// <param name="onComplete">Callback when initialization completes</param>
        public static void Initialize(Action<bool, string> onComplete = null)
        {
            LvlUpConfigScriptable configScriptable = Resources.Load<LvlUpConfigScriptable>("LvlUpConfig");
            
            if (configScriptable == null)
            {
                string error = "[LvlUp] LvlUpConfig scriptable asset not found in Resources/. " +
                    "Please create one using: Assets > LvlUp > Create Configuration";
                Debug.LogError(error);
                onComplete?.Invoke(false, error);
                return;
            }

            if (!configScriptable.IsValid())
            {
                string error = "[LvlUp] LvlUpConfigScriptable is not valid. API Key and Base URL must be configured.";
                Debug.LogError(error);
                onComplete?.Invoke(false, error);
                return;
            }

            LvlUpConfig config = configScriptable.ToLvlUpConfig();
            Instance._Initialize(configScriptable.GetApiKey(), configScriptable.GetBaseUrl(), config, configScriptable.remoteConfigEnvironment, onComplete);
        }

        /// <summary>
        /// Initialize the LvlUp SDK
        /// </summary>
        /// <param name="apiKey">Your LvlUp API key</param>
        /// <param name="baseUrl">Backend API URL</param>
        /// <param name="config">Optional configuration</param>
        /// <param name="onComplete">Callback when initialization completes</param>
        public static void Initialize(string apiKey, string baseUrl, LvlUpConfig config = null, Action<bool, string> onComplete = null)
        {
            Instance._Initialize(apiKey, baseUrl, config, "production", onComplete);
        }

        private void _Initialize(string apiKey, string baseUrl, LvlUpConfig config = null, string remoteConfigEnvironment = "production", Action<bool, string> onComplete = null)
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
            _crashReporter = new CrashReporter(_httpClient, this, _apiKey, GetCachedGeoData, null, null);
            _remoteConfigService = new RemoteConfigService();
            
            // Initialize Remote Config Service with environment from config
            _remoteConfigService.Initialize(_httpClient, remoteConfigEnvironment, _config.enableDebugLogs);
            
            if (_config.enableDebugLogs)
                Debug.Log($"[LvlUp] Remote Config initialized with environment: {remoteConfigEnvironment}");
            
            // Enable crash reporting by default
            if (_config.enableCrashReporting)
            {
                _crashReporter.SetEnabled(true);
            }
            
            _isInitialized = true;
            _lastFlushTime = Time.time;

            // Load persisted offline events from previous session
            LoadPersistedEvents();
            
            // Load pending sessions from previous session
            LoadPendingSessions();

            if (_config.enableDebugLogs)
                Debug.Log($"[LvlUp] SDK Initialized - Base URL: {_baseUrl}");
            
            // Optionally fetch geo data at initialization (async, non-blocking)
            if (_config.enableGeoTracking)
                StartCoroutine(FetchGeoLocationAsync());

            // Start automatic flush coroutine
            if (!_config.sendImmediately)
                StartCoroutine(AutoFlushCoroutine());

            // Auto-start session if enabled
            if (_config.autoTrackSessions)
            {
                // Generate or retrieve user ID
                string autoUserId = GetOrCreateAutoUserId();
                
                // If geo tracking is enabled, wait briefly for geo data before starting session
                // This ensures countryCode is available from the start rather than being NULL
                // and getting updated later by heartbeat
                if (_config.enableGeoTracking && _cachedGeoData == null)
                {
                    StartCoroutine(WaitForGeoThenStartSession(autoUserId, onComplete));
                }
                else
                {
                    // Geo disabled or already cached - start immediately
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

        private void OnDestroy()
        {
            // Clean up singleton reference when destroyed
            if (_instance == this)
            {
                _instance = null;
            }
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
                
                // Persist any pending events before going to background
                PersistEvents();
                
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
                
                // Persist events before quitting
                PersistEvents();
                
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

        #region Remote Config Service

        /// <summary>
        /// Access Remote Config Service for config operations
        /// Automatically initialized after session starts
        /// </summary>
        public static RemoteConfigService RemoteConfig => _instance?._remoteConfigService;

        /// <summary>
        /// Fetch remote configs from backend (called automatically after geo data fetch)
        /// </summary>
        private void FetchRemoteConfigs()
        {
            if (_remoteConfigService == null || !_remoteConfigService.IsInitialized)
            {
                if (_config.enableDebugLogs)
                    Debug.LogWarning("[LvlUp] Remote Config Service not initialized.");
                return;
            }

            _remoteConfigService.FetchAsync(this, success =>
            {
                if (_config.enableDebugLogs)
                    Debug.Log($"[LvlUp] Remote configs fetched: {success}");
            });
        }

        /// <summary>
        /// Set context for Remote Config rule evaluation
        /// </summary>
        public static void SetRemoteConfigContext(string platform = null, string version = null, string country = null, string segment = null)
        {
            if (_instance?._remoteConfigService != null && _instance._remoteConfigService.IsInitialized)
            {
                _instance._remoteConfigService.SetContext(platform, version, country, segment);
            }
        }

        /// <summary>
        /// Get current Remote Config environment
        /// </summary>
        public static string GetRemoteConfigEnvironment()
        {
            return _instance?._config?.remoteConfigEnvironment ?? "production";
        }

        /// <summary>
        /// Get the actual environment that will be used (always production in builds)
        /// </summary>
        public static string GetEffectiveRemoteConfigEnvironment()
        {
            #if UNITY_EDITOR
            return _instance?._config?.remoteConfigEnvironment ?? "production";
            #else
            return "production"; // Always production in builds
            #endif
        }

        /// <summary>
        /// Auto-set RemoteConfig context from current session data
        /// Called automatically when initializing remote config
        /// </summary>
        private void AutoSetRemoteConfigContext()
        {
            if (_remoteConfigService == null || !_remoteConfigService.IsInitialized)
                return;

            string platform = GetPlatform();
            string version = Application.version;
            string country = _cachedGeoData?.countryCode;
            
            _remoteConfigService.SetContext(platform, version, country);
            
            if (_config.enableDebugLogs)
                Debug.Log($"[LvlUp] RemoteConfig context set: platform={platform}, version={version}, country={country}");
        }

        /// <summary>
        /// Initialize and fetch remote configs (called after geo data is fetched)
        /// Independent from session lifecycle
        /// </summary>
        private void InitializeAndFetchRemoteConfig()
        {
            if (!_isInitialized)
                return;

            try
            {
                // Force production environment in builds
                #if !UNITY_EDITOR
                _config.remoteConfigEnvironment = "production";
                #endif

                // Initialize RemoteConfig service
                _remoteConfigService.Initialize(
                    _httpClient,
                    _config.remoteConfigEnvironment,
                    _config.enableDebugLogs
                );

                // Set context with current platform, version, and country
                AutoSetRemoteConfigContext();

                // Fetch remote configs from backend
                FetchRemoteConfigs();

                if (_config.enableDebugLogs)
                    Debug.Log($"[LvlUp] RemoteConfig initialized (environment: {_config.remoteConfigEnvironment}) and fetching configs...");
            }
            catch (Exception e)
            {
                if (_config.enableDebugLogs)
                    Debug.LogError($"[LvlUp] Failed to initialize RemoteConfig: {e.Message}");
            }
        }

        /// <summary>
        /// Get current platform as string
        /// </summary>
        private string GetPlatform()
        {
            #if UNITY_IOS
                return "iOS";
            #elif UNITY_ANDROID
                return "Android";
            #elif UNITY_WEBGL
                return "WebGL";
            #elif UNITY_STANDALONE_WIN
                return "Windows";
            #elif UNITY_STANDALONE_OSX
                return "macOS";
            #elif UNITY_STANDALONE_LINUX
                return "Linux";
            #else
                return "Unknown";
            #endif
        }

        // RemoteConfig convenience methods removed - use LvlUpSDK.Config.* instead

        #endregion

        #region Utility Methods

        /// <summary>
        /// Wait for geo data to be fetched (with timeout) then start session
        /// This ensures sessions have countryCode from the start when geo tracking is enabled
        /// </summary>
        private IEnumerator WaitForGeoThenStartSession(string userId, Action<bool, string> onComplete)
        {
            const float GEO_WAIT_TIMEOUT = 3f; // Wait up to 3 seconds for geo data
            float waitStartTime = Time.time;
            
            if (_config.enableDebugLogs)
                Debug.Log("[LvlUp] Waiting for geo data before starting session...");
            
            // Wait for geo data or timeout
            while (_cachedGeoData == null && (Time.time - waitStartTime) < GEO_WAIT_TIMEOUT)
            {
                yield return new WaitForSeconds(0.1f);
            }
            
            if (_cachedGeoData != null)
            {
                if (_config.enableDebugLogs)
                    Debug.Log($"[LvlUp] Geo data received ({_cachedGeoData.countryCode}), starting session with countryCode");
            }
            else
            {
                if (_config.enableDebugLogs)
                    Debug.LogWarning("[LvlUp] Geo data timeout, starting session without countryCode (will be updated by heartbeat if geo arrives later)");
            }
            
            // Start session with or without geo data
            StartSession(userId, null, response =>
            {
                if (_config.enableDebugLogs)
                {
                    if (response.success)
                        Debug.Log($"[LvlUp] Auto-started session for user: {userId}");
                    else
                        Debug.LogWarning($"[LvlUp] Failed to auto-start session: {response.error}");
                }
                
                // Invoke initialization complete callback
                if (response.success)
                    onComplete?.Invoke(true, $"Initialized and session started for user: {userId}");
                else
                    onComplete?.Invoke(false, $"Initialized but session failed: {response.error}");
            });
        }

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
            
            _crashReporter?.SetUserId(_currentUserId);
            
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
                    _hasOfflineSession = false;
                    
                    _crashReporter.SetSessionId(_currentSession.sessionId);
                    
                    // Start heartbeat coroutine
                    StartHeartbeat();
                    
                    if (_config.enableDebugLogs)
                        Debug.Log($"[LvlUp] Session started: {_currentSession.sessionId}");
                }
                else
                {
                    // Failed to start session (likely offline)
                    // Add to list of pending session starts
                    _pendingSessionStarts.Add(request);
                    _hasOfflineSession = true;
                    
                    // Persist all pending sessions
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

            // SessionEndRequest auto-populates all metadata via LvlUpEvent constructor
            var request = new SessionEndRequest
            {
                sessionId = _currentSession?.sessionId,
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

            // If we have an offline session, just store the end request
            if (_hasOfflineSession && _currentSession == null)
            {
                _pendingSessionEnds.Add(request);
                
                // Persist all pending sessions
                PersistPendingSessions();
                
                if (_config.enableDebugLogs)
                    Debug.Log($"[LvlUp] Offline session end queued. Total pending ends: {_pendingSessionEnds.Count}");
                
                callback?.Invoke(new ApiResponse<SessionData> { success = true, message = "Offline session end queued" });
                return;
            }

            // Double-check _currentSession is not null before capturing sessionId
            if (_currentSession == null)
            {
                callback?.Invoke(new ApiResponse<SessionData> { success = false, error = "No active session" });
                return;
            }

            // Capture session ID before async call to avoid race condition
            string sessionId = _currentSession.sessionId;

            StartCoroutine(_httpClient.Put<SessionData>($"analytics/sessions/{sessionId}", request, response =>
            {
                if (response.success)
                {
                    // Stop heartbeat coroutine
                    StopHeartbeat();
                    
                    if (_config.enableDebugLogs)
                        Debug.Log($"[LvlUp] Session ended: {sessionId}");
                    _currentSession = null;
                }
                else
                {
                    // Failed to end session - add to pending list
                    _pendingSessionEnds.Add(request);
                    
                    // Persist all pending sessions
                    PersistPendingSessions();
                    
                    if (_config.enableDebugLogs)
                        Debug.LogWarning($"[LvlUp] Failed to end session. Queued for retry. Total pending ends: {_pendingSessionEnds.Count}");
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
        /// Send a single heartbeat to keep the session alive and update geo-location if available
        /// </summary>
        private void SendHeartbeat()
        {
            if (_currentSession == null)
                return;

            // Capture session ID before async call to avoid race condition
            // (_currentSession could become null while the request is in flight)
            string sessionId = _currentSession.sessionId;
            string endpoint = $"analytics/sessions/{sessionId}/heartbeat";
            
            // Include countryCode if available from geo-location tracking
            // This updates sessions that started before geo data was resolved
            var heartbeatData = new
            {
                countryCode = (_config.enableGeoTracking && _cachedGeoData != null) 
                    ? _cachedGeoData.countryCode 
                    : (string)null
            };
            
            StartCoroutine(_httpClient.Post<object>(endpoint, heartbeatData, response =>
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

        #endregion

        #region Geographic Location

        /// <summary>
        /// Fetch geographic location data asynchronously
        /// After successful geo fetch, initialize and fetch remote configs
        /// </summary>
        private IEnumerator FetchGeoLocationAsync()
        {
            yield return _geoService.FetchGeoLocation(
                onSuccess: (geoData) =>
                {
                    _cachedGeoData = geoData;
                    if (_config.enableDebugLogs)
                        Debug.Log($"[LvlUp] Geo location fetched: {geoData.city}, {geoData.region}, {geoData.country}");
                    
                    // After geo data is fetched, initialize and fetch remote configs
                    InitializeAndFetchRemoteConfig();
                },
                onError: (error) =>
                {
                    if (_config.enableDebugLogs)
                        Debug.LogWarning($"[LvlUp] Failed to fetch geo location: {error}");
                    
                    // Still initialize remote config even if geo fetch failed
                    InitializeAndFetchRemoteConfig();
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
            // First, try to send any pending sessions
            RetryPendingSessions();
            
            if (_eventBatch.Count == 0 || _isSendingEvents)
            {
                callback?.Invoke(new ApiResponse { success = true, message = "No events to flush" });
                return;
            }

            // Split into batches if we have more than MAX_BATCH_SIZE events
            // This prevents "Batch size exceeds maximum limit" errors from server
            if (_eventBatch.Count > MAX_BATCH_SIZE)
            {
                if (_config.enableDebugLogs)
                    Debug.Log($"[LvlUp] Splitting {_eventBatch.Count} events into multiple batches of {MAX_BATCH_SIZE}");
                
                StartCoroutine(FlushInBatchesCoroutine(callback));
                return;
            }

            var eventsToSend = new List<LvlUpEvent>(_eventBatch);
            // Don't clear yet - wait for confirmation
            _isSendingEvents = true;

            TrackEventsBatch(eventsToSend, response =>
            {
                _isSendingEvents = false;
                _lastFlushTime = Time.time;
                
                if (response.success)
                {
                    // Success - remove sent events from batch
                    // Remove only the events we just sent (in case new ones were added)
                    for (int i = 0; i < eventsToSend.Count && _eventBatch.Count > 0; i++)
                    {
                        _eventBatch.RemoveAt(0);
                    }
                    
                    if (_config.enableDebugLogs)
                        Debug.Log($"[LvlUp] Successfully sent {eventsToSend.Count} events");
                }
                else
                {
                    // Failed - events stay in queue for retry
                    // Persist them in case app quits
                    PersistEvents();
                    
                    if (_config.enableDebugLogs)
                        Debug.LogWarning($"[LvlUp] Failed to send events: {response.error}. Will retry later.");
                }
                
                callback?.Invoke(response);
            });
        }

        /// <summary>
        /// Flush events in multiple batches to respect server limits
        /// </summary>
        private IEnumerator FlushInBatchesCoroutine(Action<ApiResponse> callback)
        {
            _isSendingEvents = true;
            int totalSent = 0;
            bool allSucceeded = true;
            string lastError = null;

            while (_eventBatch.Count > 0 && allSucceeded)
            {
                // Take up to MAX_BATCH_SIZE events
                int batchSize = Mathf.Min(_eventBatch.Count, MAX_BATCH_SIZE);
                var eventsToSend = _eventBatch.GetRange(0, batchSize);
                
                bool batchComplete = false;
                bool batchSuccess = false;

                TrackEventsBatch(eventsToSend, response =>
                {
                    batchSuccess = response.success;
                    lastError = response.error;
                    batchComplete = true;
                    
                    if (response.success)
                    {
                        // Remove sent events
                        for (int i = 0; i < eventsToSend.Count && _eventBatch.Count > 0; i++)
                        {
                            _eventBatch.RemoveAt(0);
                        }
                        totalSent += eventsToSend.Count;
                        
                        if (_config.enableDebugLogs)
                            Debug.Log($"[LvlUp] Batch sent: {eventsToSend.Count} events ({totalSent} total)");
                    }
                });

                // Wait for batch to complete
                yield return new WaitUntil(() => batchComplete);

                if (!batchSuccess)
                {
                    allSucceeded = false;
                    if (_config.enableDebugLogs)
                        Debug.LogWarning($"[LvlUp] Batch failed: {lastError}. Stopping flush.");
                    
                    // Persist remaining events
                    PersistEvents();
                }

                // Small delay between batches to avoid overwhelming server
                if (_eventBatch.Count > 0 && allSucceeded)
                {
                    yield return new WaitForSeconds(0.5f);
                }
            }

            _isSendingEvents = false;
            _lastFlushTime = Time.time;

            if (allSucceeded)
            {
                if (_config.enableDebugLogs)
                    Debug.Log($"[LvlUp] Successfully sent all {totalSent} events in multiple batches");
                
                callback?.Invoke(new ApiResponse { success = true, message = $"Sent {totalSent} events" });
            }
            else
            {
                callback?.Invoke(new ApiResponse { success = false, error = lastError });
            }
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

        /// <summary>
        /// Get API Key (for debug purposes)
        /// </summary>
        public string GetApiKey()
        {
            return _apiKey;
        }

        /// <summary>
        /// Get Base URL (for debug purposes)
        /// </summary>
        public string GetBaseUrl()
        {
            return _baseUrl;
        }

        /// <summary>
        /// Get Remote Config Service (for debug purposes)
        /// </summary>
        public RemoteConfigService GetRemoteConfigService()
        {
            return _remoteConfigService;
        }

        /// <summary>
        /// Get current config (for debug purposes)
        /// </summary>
        public LvlUpConfig GetConfig()
        {
            return _config;
        }

        /// <summary>
        /// Get crash reporter (for internal SDK use)
        /// </summary>
        internal CrashReporter GetCrashReporter()
        {
            return _crashReporter;
        }


        #endregion

        #region Offline Event Persistence

        /// <summary>
        /// Persist offline events to PlayerPrefs
        /// </summary>
        private void PersistEvents()
        {
            if (_eventBatch.Count == 0)
            {
                // Clear persisted events if none to save
                PlayerPrefs.DeleteKey(PREF_OFFLINE_EVENTS);
                PlayerPrefs.DeleteKey(PREF_OFFLINE_EVENT_COUNT);
                PlayerPrefs.Save();
                return;
            }

            try
            {
                // Convert events to JSON array
                var eventsJson = new List<string>();
                foreach (var evt in _eventBatch)
                {
                    string json = JsonUtility.ToJson(evt);
                    eventsJson.Add(json);
                }

                // Store each event with an index (PlayerPrefs has size limits)
                PlayerPrefs.SetInt(PREF_OFFLINE_EVENT_COUNT, eventsJson.Count);
                for (int i = 0; i < eventsJson.Count; i++)
                {
                    PlayerPrefs.SetString($"{PREF_OFFLINE_EVENTS}_{i}", eventsJson[i]);
                }
                PlayerPrefs.Save();

                if (_config.enableDebugLogs)
                    Debug.Log($"[LvlUp] Persisted {eventsJson.Count} offline events");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LvlUp] Failed to persist events: {ex.Message}");
            }
        }

        /// <summary>
        /// Load persisted offline events from PlayerPrefs
        /// </summary>
        private void LoadPersistedEvents()
        {
            if (_hasLoadedPersistedEvents)
                return;

            _hasLoadedPersistedEvents = true;

            try
            {
                int count = PlayerPrefs.GetInt(PREF_OFFLINE_EVENT_COUNT, 0);
                if (count == 0)
                    return;

                int loadedCount = 0;
                for (int i = 0; i < count; i++)
                {
                    string key = $"{PREF_OFFLINE_EVENTS}_{i}";
                    if (PlayerPrefs.HasKey(key))
                    {
                        string json = PlayerPrefs.GetString(key);
                        try
                        {
                            var evt = JsonUtility.FromJson<LvlUpEvent>(json);
                            if (evt != null)
                            {
                                _eventBatch.Add(evt);
                                loadedCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[LvlUp] Failed to deserialize event {i}: {ex.Message}");
                        }
                        
                        // Clean up this entry
                        PlayerPrefs.DeleteKey(key);
                    }
                }

                // Clean up count
                PlayerPrefs.DeleteKey(PREF_OFFLINE_EVENT_COUNT);
                PlayerPrefs.Save();

                if (loadedCount > 0 && _config.enableDebugLogs)
                    Debug.Log($"[LvlUp] Loaded {loadedCount} persisted offline events");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LvlUp] Failed to load persisted events: {ex.Message}");
            }
        }

        #endregion

        #region Offline Session Persistence

        /// <summary>
        /// Persist pending session requests to PlayerPrefs
        /// </summary>
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

        /// <summary>
        /// Load pending session requests from PlayerPrefs
        /// </summary>
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
                        
                        // Clean up
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
                        
                        // Clean up
                        PlayerPrefs.DeleteKey(key);
                    }
                }

                // Clean up counts
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

        /// <summary>
        /// Retry sending pending session start/end requests
        /// Called when network comes back or events are flushed
        /// Processes sessions in order to maintain temporal sequence
        /// </summary>
        private void RetryPendingSessions()
        {
            // Process all pending session starts first (in order)
            if (_pendingSessionStarts.Count > 0)
            {
                if (_config.enableDebugLogs)
                    Debug.Log($"[LvlUp] Retrying {_pendingSessionStarts.Count} pending session starts...");

                // Process sessions one at a time to maintain order
                StartCoroutine(ProcessPendingSessionStarts());
            }
        }

        /// <summary>
        /// Process pending session starts sequentially
        /// </summary>
        private IEnumerator ProcessPendingSessionStarts()
        {
            while (_pendingSessionStarts.Count > 0)
            {
                var request = _pendingSessionStarts[0];
                bool completed = false;
                bool success = false;

                StartCoroutine(_httpClient.Post<SessionData>("analytics/sessions", request, response =>
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

                // Wait for completion
                yield return new WaitUntil(() => completed);

                if (success)
                {
                    // Remove from pending list
                    _pendingSessionStarts.RemoveAt(0);
                    
                    // Update persistence
                    PersistPendingSessions();
                }
                else
                {
                    // Stop retrying if still failing (still offline)
                    if (_config.enableDebugLogs)
                        Debug.LogWarning($"[LvlUp] Stopping session retry - still offline. {_pendingSessionStarts.Count} sessions remain queued.");
                    yield break;
                }

                // Small delay between requests
                yield return new WaitForSeconds(0.5f);
            }

            // All session starts processed, now process ends
            if (_pendingSessionEnds.Count > 0)
            {
                if (_config.enableDebugLogs)
                    Debug.Log($"[LvlUp] Retrying {_pendingSessionEnds.Count} pending session ends...");

                StartCoroutine(ProcessPendingSessionEnds());
            }

            // Update offline flag
            if (_pendingSessionStarts.Count == 0 && _pendingSessionEnds.Count == 0)
            {
                _hasOfflineSession = false;
            }
        }

        /// <summary>
        /// Process pending session ends sequentially
        /// </summary>
        private IEnumerator ProcessPendingSessionEnds()
        {
            while (_pendingSessionEnds.Count > 0)
            {
                var request = _pendingSessionEnds[0];
                
                // Session ends might not have sessionId if they were offline
                // In that case, we can't send them - just remove
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

                StartCoroutine(_httpClient.Put<SessionData>($"analytics/sessions/{request.sessionId}", request, response =>
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

                // Wait for completion
                yield return new WaitUntil(() => completed);

                if (success)
                {
                    // Remove from pending list
                    _pendingSessionEnds.RemoveAt(0);
                    
                    // Update persistence
                    PersistPendingSessions();
                }
                else
                {
                    // Stop retrying if still failing
                    if (_config.enableDebugLogs)
                        Debug.LogWarning($"[LvlUp] Stopping session end retry. {_pendingSessionEnds.Count} session ends remain queued.");
                    yield break;
                }

                // Small delay between requests
                yield return new WaitForSeconds(0.5f);
            }

            // Update offline flag
            if (_pendingSessionStarts.Count == 0 && _pendingSessionEnds.Count == 0)
            {
                _hasOfflineSession = false;
            }
        }

        #endregion
    }
}