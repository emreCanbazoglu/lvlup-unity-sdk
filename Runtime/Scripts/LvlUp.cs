using System;
using System.Collections.Generic;
using LvlUp.Models;
using LvlUp.Services;

namespace LvlUp
{
    /// <summary>
    /// Main static API for LvlUp SDK
    /// Provides a clean, organized interface to all SDK functionality
    /// Use LvlUpSDK.Config, LvlUpSDK.Events, etc.
    /// </summary>
    public static class LvlUpSDK
    {
        /// <summary>
        /// Initialize the LvlUp SDK
        /// </summary>
        public static void Initialize(Action<bool, string> onComplete = null)
        {
            LvlUpManager.Initialize(onComplete);
        }

        /// <summary>
        /// Initialize the LvlUp SDK with custom configuration
        /// </summary>
        public static void Initialize(string apiKey, string baseUrl, LvlUpConfig config = null, Action<bool, string> onComplete = null)
        {
            LvlUpManager.Initialize(apiKey, baseUrl, config, onComplete);
        }

        /// <summary>
        /// Check if SDK is initialized
        /// </summary>
        public static bool IsInitialized => LvlUpManager.Instance?.IsInitialized() ?? false;

        /// <summary>
        /// Get the SDK manager instance
        /// </summary>
        public static LvlUpManager Manager => LvlUpManager.Instance;

        #region Session Management

        /// <summary>
        /// Session management API
        /// </summary>
        public static class Session
        {
            /// <summary>
            /// Start a new session for a user
            /// </summary>
            public static void Start(string userId, UserMetadata metadata = null, Action<ApiResponse<SessionData>> callback = null)
            {
                LvlUpManager.Instance?.StartSession(userId, metadata, callback);
            }

            /// <summary>
            /// End the current session
            /// </summary>
            public static void End(Action<ApiResponse<SessionData>> callback = null)
            {
                LvlUpManager.Instance?.EndSession(callback);
            }

            /// <summary>
            /// Get current session data
            /// </summary>
            public static SessionData Current => LvlUpManager.Instance?.GetCurrentSession();

            /// <summary>
            /// Get current user ID
            /// </summary>
            public static string CurrentUserId => LvlUpManager.Instance?.GetCurrentUserId();
        }

        #endregion

        #region Event Tracking

        /// <summary>
        /// Event tracking API
        /// </summary>
        public static class Events
        {
            /// <summary>
            /// Track a single event
            /// </summary>
            public static void Track(string eventName, Dictionary<string, object> properties = null, Action<ApiResponse> callback = null)
            {
                LvlUpManager.Instance?.TrackEvent(eventName, properties, callback);
            }

            /// <summary>
            /// Track a custom event object
            /// </summary>
            public static void Track(LvlUpEvent lvlUpEvent, Action<ApiResponse> callback = null)
            {
                LvlUpManager.Instance?.TrackEvent(lvlUpEvent, callback);
            }

            /// <summary>
            /// Track multiple events in a batch
            /// </summary>
            public static void TrackBatch(List<LvlUpEvent> events, Action<ApiResponse> callback = null)
            {
                LvlUpManager.Instance?.TrackEventsBatch(events, callback);
            }

            /// <summary>
            /// Flush queued events immediately
            /// </summary>
            public static void Flush(Action<ApiResponse> callback = null)
            {
                LvlUpManager.Instance?.FlushEventQueue(callback);
            }

            /// <summary>
            /// Get number of queued events
            /// </summary>
            public static int QueuedCount => LvlUpManager.Instance?.GetQueuedEventCount() ?? 0;
        }

        #endregion

        #region Remote Config

        /// <summary>
        /// Remote Config API
        /// </summary>
        public static class Config
        {
            /// <summary>
            /// Check if Remote Config is ready to use
            /// </summary>
            public static bool IsReady => RemoteConfig != null && RemoteConfig.IsInitialized;

            /// <summary>
            /// Try to get an integer config value
            /// </summary>
            public static bool TryGetInt(string key, out int value)
            {
                value = 0;
                
                if (!IsReady) return false;
                if (!RemoteConfig.HasKey(key)) return false;
                
                value = RemoteConfig.GetInt(key, 0);
                return true;
            }

            /// <summary>
            /// Try to get a string config value
            /// </summary>
            public static bool TryGetString(string key, out string value)
            {
                value = null;
                
                if (!IsReady) return false;
                if (!RemoteConfig.HasKey(key)) return false;
                
                value = RemoteConfig.GetString(key, "");
                return true;
            }

            /// <summary>
            /// Try to get a boolean config value
            /// </summary>
            public static bool TryGetBool(string key, out bool value)
            {
                value = false;
                
                if (!IsReady) return false;
                if (!RemoteConfig.HasKey(key)) return false;
                
                value = RemoteConfig.GetBool(key, false);
                return true;
            }

            /// <summary>
            /// Try to get a float config value
            /// </summary>
            public static bool TryGetFloat(string key, out float value)
            {
                value = 0f;
                
                if (!IsReady) return false;
                if (!RemoteConfig.HasKey(key)) return false;
                
                value = RemoteConfig.GetFloat(key, 0f);
                return true;
            }

            /// <summary>
            /// Try to get a JSON config value and deserialize to type T
            /// </summary>
            public static bool TryGetJson<T>(string key, out T value) where T : class
            {
                value = null;
                
                if (!IsReady) return false;
                if (!RemoteConfig.HasKey(key)) return false;
                
                value = RemoteConfig.GetJson<T>(key, null);
                return value != null;
            }

            /// <summary>
            /// Check if a config key exists
            /// </summary>
            public static bool HasKey(string key)
            {
                if (!IsReady) return false;
                return RemoteConfig.HasKey(key);
            }

            /// <summary>
            /// Refresh remote configs from server
            /// </summary>
            public static void Refresh(Action<bool> onComplete = null)
            {
                if (!IsReady)
                {
                    onComplete?.Invoke(false);
                    return;
                }
                
                RemoteConfig.FetchAsync(Manager, onComplete);
            }

            /// <summary>
            /// Set context for Remote Config rule evaluation
            /// </summary>
            public static void SetContext(string platform = null, string version = null, string country = null, string segment = null)
            {
                if (RemoteConfig != null && RemoteConfig.IsInitialized)
                {
                    RemoteConfig.SetContext(platform, version, country, segment);
                }
            }

            /// <summary>
            /// Get current environment
            /// </summary>
            public static string CurrentEnvironment => LvlUpManager.GetRemoteConfigEnvironment();

            /// <summary>
            /// Get effective environment (always production in builds)
            /// </summary>
            public static string EffectiveEnvironment => LvlUpManager.GetEffectiveRemoteConfigEnvironment();

            /// <summary>
            /// Access the RemoteConfig service directly for advanced usage
            /// </summary>
            public static RemoteConfigService Service => RemoteConfig;
            
            /// <summary>
            /// Internal helper to get RemoteConfig service
            /// </summary>
            private static RemoteConfigService RemoteConfig => LvlUpManager.RemoteConfig;
        }

        #endregion

        #region Crash Reporting

        /// <summary>
        /// Crash and error reporting API
        /// </summary>
        public static class Crash
        {
            /// <summary>
            /// Add a breadcrumb to track user actions
            /// </summary>
            public static void AddBreadcrumb(string message, BreadcrumbType type = BreadcrumbType.Navigation, Dictionary<string, object> data = null)
            {
                Manager?.GetCrashReporter()?.AddBreadcrumb(message, type, data);
            }

            /// <summary>
            /// Report an exception manually
            /// </summary>
            public static void ReportException(Exception exception, string context = null, Dictionary<string, object> customData = null)
            {
                Manager?.GetCrashReporter()?.ReportException(exception, context, customData);
            }

            /// <summary>
            /// Report an error manually
            /// </summary>
            public static void ReportError(string message, string stackTrace = null, Dictionary<string, object> customData = null)
            {
                Manager?.GetCrashReporter()?.ReportError(message, stackTrace, customData);
            }

            /// <summary>
            /// Enable or disable crash reporting
            /// </summary>
            public static void SetEnabled(bool enabled)
            {
                Manager?.GetCrashReporter()?.SetEnabled(enabled);
            }
        }

        #endregion

        #region Geo Location

        /// <summary>
        /// Geographic location API
        /// </summary>
        public static class Geo
        {
            /// <summary>
            /// Refresh geo location data
            /// </summary>
            public static void Refresh(Action<GeoData> onSuccess = null, Action<string> onError = null)
            {
                LvlUpManager.Instance?.RefreshGeoLocation(onSuccess, onError);
            }

            /// <summary>
            /// Get cached geo data
            /// </summary>
            public static GeoData Cached => LvlUpManager.Instance?.GetCachedGeoData();
        }

        #endregion

        #region Player Journey

        /// <summary>
        /// Player Journey / Checkpoint API
        /// </summary>
        public static class Journey
        {
            /// <summary>
            /// Create a new checkpoint
            /// </summary>
            public static void CreateCheckpoint(string name, string description, string type, int order, string[] tags = null, Action<ApiResponse<Checkpoint>> callback = null)
            {
                LvlUpManager.Instance?.CreateCheckpoint(name, description, type, order, tags, callback);
            }

            /// <summary>
            /// Record a checkpoint completion
            /// </summary>
            public static void RecordCheckpoint(string checkpointId, Dictionary<string, object> metadata = null, Action<ApiResponse> callback = null)
            {
                LvlUpManager.Instance?.RecordCheckpoint(checkpointId, metadata, callback);
            }

            /// <summary>
            /// Get player journey progress
            /// </summary>
            public static void GetProgress(Action<ApiResponse<PlayerJourneyProgress>> callback = null)
            {
                LvlUpManager.Instance?.GetPlayerJourneyProgress(callback);
            }
        }

        #endregion

        #region Level Funnel

        /// <summary>
        /// Level funnel configuration API
        /// </summary>
        public static class LevelFunnel
        {
            /// <summary>
            /// Set level funnel configuration
            /// </summary>
            public static void Set(string levelFunnel, int levelFunnelVersion)
            {
                LvlUpManager.Instance?.SetLevelFunnel(levelFunnel, levelFunnelVersion);
            }

            /// <summary>
            /// Get current level funnel configuration
            /// </summary>
            public static (string funnel, int version) Get()
            {
                return LvlUpManager.Instance?.GetLevelFunnel() ?? (null, 0);
            }
        }

        #endregion

        #region Revenue Tracking

        /// <summary>
        /// Revenue tracking API for ad impressions and in-app purchases
        /// </summary>
        public static class Revenue
        {
            /// <summary>
            /// Track revenue data (ad impression or in-app purchase)
            /// </summary>
            public static void Track(RevenueData revenueData, Action<ApiResponse> callback = null)
            {
                Manager?.TrackRevenue(revenueData, callback);
            }

            /// <summary>
            /// Track an ad impression
            /// </summary>
            public static void TrackAdImpression(string adNetworkName, string adFormat, double revenue,
                string adUnitId = null, string placement = null, string creativeId = null, string country = null)
            {
                Manager?.TrackAdImpression(adNetworkName, adFormat, revenue, adUnitId, placement, creativeId);
            }

            /// <summary>
            /// Track an in-app purchase
            /// </summary>
            public static void TrackInAppPurchase(string productId, double revenue, string transactionId,
                string store = null, string productName = null, int quantity = 1, bool isVerified = false)
            {
                Manager?.TrackInAppPurchase(productId, revenue, transactionId, store, productName, quantity, isVerified);
            }

            /// <summary>
            /// Flush queued revenue immediately
            /// </summary>
            public static void Flush()
            {
                Manager?.FlushRevenueQueue();
            }

            /// <summary>
            /// Get number of queued revenue items
            /// </summary>
            public static int QueuedCount => Manager?.GetRevenueTrackingService()?.GetQueuedRevenueCount() ?? 0;
        }

        #endregion
    }
}

