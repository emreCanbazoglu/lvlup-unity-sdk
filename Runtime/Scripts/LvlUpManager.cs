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
        private string _apiKey;
        private string _baseUrl;

        // Services
        private GeoLocationService _geoService;
        private CrashReporter _crashReporter;
        private RemoteConfigService _remoteConfigService;
        private AdMonetizationService _adMonetizationService;
        private EventTrackingService _eventTrackingService;
        private RevenueTrackingService _revenueTrackingService;
        private SessionManagementService _sessionManagementService;
        
        // Cached geo data
        private GeoData _cachedGeoData;

        // State
        private bool _isInitialized = false;

        #region Initialization

        /// <summary>
        /// Initialize the LvlUp SDK by automatically loading config from Resources
        /// Loads LvlUpConfig.asset from Assets/Resources/
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
            
            CreateServices();

            InitializeServices(remoteConfigEnvironment);

            if (_config.enableDebugLogs)
                Debug.Log($"[LvlUp] Remote Config initialized with environment: {remoteConfigEnvironment}");
            
            // Enable crash reporting by default
            if (_config.enableCrashReporting)
            {
                _crashReporter.SetEnabled(true);
            }
            
            _isInitialized = true;

            if (_config.enableDebugLogs)
                Debug.Log($"[LvlUp] SDK Initialized - Base URL: {_baseUrl}");
            
            StartCoroutine(InitializeAsync());
            
        }

        private void InitializeServices(string remoteConfigEnvironment)
        {
            // Initialize Remote Config Service with environment from config
            _remoteConfigService.Initialize(_httpClient, remoteConfigEnvironment, _config.enableDebugLogs);
            _adMonetizationService.Initialize(_revenueTrackingService.TrackAdImpression);
            
            // Initialize other services
            _sessionManagementService.Initialize();
            _eventTrackingService.Initialize();
            _revenueTrackingService.Initialize();
        }

        private void CreateServices()
        {
            _geoService = new GeoLocationService();
            _crashReporter = new CrashReporter(_httpClient, this, _apiKey, GetCachedGeoData, null, null);
            _remoteConfigService = new RemoteConfigService();
            _adMonetizationService = new AdMonetizationService();
            
            CreateSessionManagementService();
            CreateEventTrackingService();
            CreateRevenueTrackingService();
        }

        private void CreateRevenueTrackingService()
        {
            _revenueTrackingService = new RevenueTrackingService(
                _httpClient,
                _config,
                this,
                () => _sessionManagementService.GetCurrentUserId(),
                () => _sessionManagementService.GetCurrentSession()?.sessionId,
                () => _sessionManagementService.GetSessionNumber(),
                GetCachedGeoData,
                GetPlatform,
                GetManufacturer
            );
        }

        private void CreateEventTrackingService()
        {
            _eventTrackingService = new EventTrackingService(
                _httpClient,
                _config,
                this,
                () => _sessionManagementService.GetCurrentUserId(),
                () => _sessionManagementService.GetCurrentSession()?.sessionId,
                () => _sessionManagementService.GetSessionNumber(),
                ApplyGeoDataToEvent
            );
        }

        private void CreateSessionManagementService()
        {
            _sessionManagementService = new SessionManagementService(
                _httpClient,
                _config,
                this,
                GetCachedGeoData,
                sessionId => _crashReporter.SetSessionId(sessionId),
                userId => _crashReporter?.SetUserId(userId)
            );
        }

        private IEnumerator InitializeAsync(Action<bool, string> onComplete = null)
        {
            // Fetch remote configs - wait for geo if enabled, otherwise start immediately
            if (_config.enableGeoTracking)
            {
                // Geo tracking enabled - start geo fetch and remote config will be fetched after
                yield return StartCoroutine(FetchGeoLocationAsync());
            }
            
            // Fetch remote configs
            StartCoroutine(FetchRemoteConfigsAsync());

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
                if (_config.autoTrackAppLifecycle)
                    TrackEvent("app_paused", null);
                
                // Persist any pending events and revenue before going to background
                _eventTrackingService?.PersistEvents();
                _revenueTrackingService?.PersistRevenue();
                
                FlushEventQueue();
                FlushRevenueQueue();
            }
            else
            {
                if (_config.autoTrackAppLifecycle)
                    TrackEvent("app_resumed", null);
            }

            // Delegate session management to service
            _sessionManagementService?.OnApplicationPause(pauseStatus);
        }

        private void Update()
        {
            // Safety check: Restart heartbeat if session exists but coroutine stopped
            if (_isInitialized)
            {
                _sessionManagementService?.RestartHeartbeatIfNeeded();
            }
        }

        private void OnApplicationQuit()
        {
            if (_isInitialized && _sessionManagementService?.GetCurrentSession() != null)
            {
                if (_config.autoTrackAppLifecycle)
                    TrackEvent("app_quit", null);
                
                // Persist events and revenue before quitting
                _eventTrackingService?.PersistEvents();
                
                FlushEventQueue();
                FlushRevenueQueue();
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
        /// Fetch remote configs asynchronously
        /// Sets context with geo data if available
        /// </summary>
        private IEnumerator FetchRemoteConfigsAsync()
        {
            if (!_isInitialized)
                yield break;

            // Force production environment in builds
            #if !UNITY_EDITOR
            _config.remoteConfigEnvironment = "production";
            #endif

            // Set platform context (always available)
            string platform = GetPlatform();
            string version = Application.version;
            string country = _cachedGeoData?.countryCode; // Will be null if geo tracking is disabled or not yet fetched

            _remoteConfigService.SetContext(
                platform: platform,
                version: version,
                country: country
            );

            // Fetch remote configs from backend
            bool fetchComplete = false;
            _remoteConfigService.FetchAsync(this, success =>
            {
                fetchComplete = true;
                if (_config.enableDebugLogs)
                {
                    if (success)
                        Debug.Log($"[LvlUp] Remote configs fetched successfully");
                    else
                        Debug.LogWarning($"[LvlUp] Failed to fetch remote configs");
                }
            });

            // Wait for fetch to complete (with timeout)
            float timeout = 10f;
            float startTime = Time.time;
            while (!fetchComplete && (Time.time - startTime) < timeout)
            {
                yield return null;
            }

            if (_config.enableDebugLogs)
                Debug.Log($"[LvlUp] RemoteConfig service ready (environment: {_config.remoteConfigEnvironment}, country: {country ?? "not set"})");
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

        /// <summary>
        /// Get current platform as string (alias for revenue tracking)
        /// </summary>
        private string GetPlatformString()
        {
            return GetPlatform();
        }

        /// <summary>
        /// Get device manufacturer
        /// </summary>
        private string GetManufacturer()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaClass buildClass = new AndroidJavaClass("android.os.Build"))
                {
                    return buildClass.GetStatic<string>("MANUFACTURER");
                }
            }
            catch
            {
                return "Unknown";
            }
#elif UNITY_IOS && !UNITY_EDITOR
            return "Apple";
#elif UNITY_EDITOR
            return "Editor";
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
            
            // Start session with or without geo data using SessionManagementService
            _sessionManagementService.StartSession(userId, null, response =>
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

            _sessionManagementService.StartSession(userId, metadata, callback);
        }

        /// <summary>
        /// End the current session
        /// </summary>
        public void EndSession(Action<ApiResponse<SessionData>> callback = null)
        {
            _sessionManagementService?.EndSession(callback);
        }


        #endregion

        #region Geographic Location

        /// <summary>
        /// Fetch geographic location data asynchronously
        /// After successful geo fetch, fetch remote configs with geo context
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
                    _cachedGeoData.countryCode
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
            if (!_isInitialized)
            {
                Debug.LogError("[LvlUp] SDK not initialized. Call Initialize() first.");
                callback?.Invoke(new ApiResponse { success = false, error = "SDK not initialized" });
                return;
            }

            _eventTrackingService.TrackEvent(eventName, properties, callback);
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

            _eventTrackingService.TrackEvent(lvlUpEvent, callback);
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

            _eventTrackingService.TrackEventsBatch(events, callback);
        }

        #endregion

        #region Revenue Tracking

        /// <summary>
        /// Track revenue (Ad Impression or In-App Purchase)
        /// Supports offline tracking with automatic persistence
        /// </summary>
        public void TrackRevenue(RevenueData revenueData, Action<ApiResponse> callback = null)
        {
            if (!_isInitialized)
            {
                Debug.LogError("[LvlUp] SDK not initialized. Call Initialize() first.");
                callback?.Invoke(new ApiResponse { success = false, error = "SDK not initialized" });
                return;
            }

            _revenueTrackingService.TrackRevenue(revenueData, callback);
        }

        /// <summary>
        /// Track an ad impression (convenience method)
        /// </summary>
        public void TrackAdImpression(string adNetworkName, string adFormat, double revenue,
            string adUnitId = null, string placement = null, string creativeId = null)
        {
            _revenueTrackingService.TrackAdImpression(adNetworkName, adFormat, revenue, adUnitId, placement, creativeId);
        }

        /// <summary>
        /// Track an in-app purchase (convenience method)
        /// </summary>
        public void TrackInAppPurchase(string productId, double revenue, string currency, string transactionId,
            string store = null, string productName = null, string productType = null, int quantity = 1, bool isVerified = false)
        {
            _revenueTrackingService.TrackInAppPurchase(productId, revenue, currency, transactionId, store, productName, productType, quantity, isVerified);
        }

        #endregion

        /// <summary>
        /// Flush all queued events to the server
        /// </summary>
        public void FlushEventQueue(Action<ApiResponse> callback = null)
        {
            // First, try to send any pending sessions
            _sessionManagementService?.RetryPendingSessions();
            
            _eventTrackingService?.FlushEventQueue(callback);
        }

        /// <summary>
        /// Flush the revenue queue and send to server
        /// </summary>
        public void FlushRevenueQueue()
        {
            _revenueTrackingService?.FlushRevenueQueue();
        }

        private IEnumerator AutoFlushCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(_config.eventFlushInterval);

                if (_eventTrackingService != null && _eventTrackingService.GetQueuedEventCount() > 0)
                {
                    if (_config.enableDebugLogs)
                        Debug.Log($"[LvlUp] Auto-flushing {_eventTrackingService.GetQueuedEventCount()} events");
                    
                    FlushEventQueue();
                }

                // Also flush revenue batch
                if (_revenueTrackingService != null && _revenueTrackingService.GetQueuedRevenueCount() > 0)
                {
                    if (_config.enableDebugLogs)
                        Debug.Log($"[LvlUp] Auto-flushing {_revenueTrackingService.GetQueuedRevenueCount()} revenue items");
                    
                    FlushRevenueQueue();
                }
            }
        }

        /// <summary>
        /// Get the number of queued events
        /// </summary>
        public int GetQueuedEventCount()
        {
            return _eventTrackingService?.GetQueuedEventCount() ?? 0;
        }

        /// <summary>
        /// Get the number of queued revenue items
        /// </summary>
        public int GetQueuedRevenueCount()
        {
            return _revenueTrackingService?.GetQueuedRevenueCount() ?? 0;
        }

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

            string currentUserId = _sessionManagementService?.GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                Debug.LogError("[LvlUp] No active user. Call StartSession() first.");
                callback?.Invoke(new ApiResponse { success = false, error = "No active user" });
                return;
            }

            // CheckpointRecordRequest auto-populates all metadata via LvlUpEvent constructor
            var request = new CheckpointRecordRequest
            {
                userId = currentUserId,
                checkpointId = checkpointId,
                metadata = metadata ?? new Dictionary<string, object>(),
                sessionNum = _sessionManagementService?.GetSessionNumber() ?? 0
            };
            
            // Apply cached geo data if available
            if (_config.enableGeoTracking && _cachedGeoData != null)
            {
                request.SetGeoLocation(
                    _cachedGeoData.countryCode
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

            string currentUserId = _sessionManagementService?.GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                Debug.LogError("[LvlUp] No active user. Call StartSession() first.");
                callback?.Invoke(new ApiResponse<PlayerJourneyProgress> { success = false, error = "No active user" });
                return;
            }

            StartCoroutine(_httpClient.Get<PlayerJourneyProgress>($"analytics/journey/progress/{currentUserId}", callback));
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
            return _sessionManagementService?.GetCurrentSession();
        }

        /// <summary>
        /// Get current user ID
        /// </summary>
        public string GetCurrentUserId()
        {
            return _sessionManagementService?.GetCurrentUserId();
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


        /// <summary>
        /// Get the AdMonetizationService instance
        /// </summary>
        public AdMonetizationService GetAdMonetizationService()
        {
            return _adMonetizationService;
        }

        /// <summary>
        /// Get the RevenueTrackingService instance
        /// </summary>
        public RevenueTrackingService GetRevenueTrackingService()
        {
            return _revenueTrackingService;
        }
    }
}

