using UnityEngine;

namespace LvlUp
{
    /// <summary>
    /// Environment enum for API endpoint selection
    /// </summary>
    public enum ApiEnvironment
    {
        Production,
        Development
    }

    /// <summary>
    /// Scriptable Object wrapper for LvlUpConfig
    /// Allows configuration to be set in the Inspector
    /// </summary>
    public class LvlUpConfigScriptable : ScriptableObject
    {
        // API endpoint URLs for each environment
        private const string PRODUCTION_URL = "https://lvlup-backend-production.up.railway.app/api";
        private const string DEVELOPMENT_URL = "http://localhost:3000/api";

        [Header("API Configuration")]
        [Tooltip("Your LvlUp API Key")]
        public string apiKey = "";

        [Tooltip("Select the API environment (Production or Development)")]
        public ApiEnvironment apiEnvironment = ApiEnvironment.Production;

        [Header("Debug & Logging")]
        public bool enableDebugLogs = false;

        [Header("Session Tracking")]
        public bool autoTrackSessions = true;
        public bool autoTrackAppLifecycle = true;

        [Header("Event Configuration")]
        [Tooltip("Number of events to batch before sending")]
        public int eventBatchSize = 50;

        [Tooltip("Time interval (in seconds) to flush events automatically")]
        public float eventFlushInterval = 30f;

        [Tooltip("Maximum number of events to queue offline")]
        public int maxQueueSize = 1000;

        [Tooltip("Send events immediately instead of batching")]
        public bool sendImmediately = false;

        [Header("Persistence")]
        [Tooltip("Persist event queue to disk for offline support")]
        public bool persistQueueToDisk = true;

        [Header("Network Configuration")]
        [Tooltip("Number of retry attempts for failed requests")]
        public int retryAttempts = 3;

        [Tooltip("Request timeout in seconds")]
        public float timeout = 30f;

        [Header("Features")]
        [Tooltip("Enable automatic geographic location tracking using IP geolocation")]
        public bool enableGeoTracking = false;

        [Tooltip("Track Unity scenes automatically")]
        public bool autoTrackScenes = false;

        [Tooltip("Enable automatic crash and exception reporting")]
        public bool enableCrashReporting = true;

        [Header("Remote Config")]
        [Tooltip("Environment for remote config (production, staging, development)")]
        public string remoteConfigEnvironment = "production";

        /// <summary>
        /// Convert this scriptable object to LvlUpConfig
        /// </summary>
        public LvlUpConfig ToLvlUpConfig()
        {
            return new LvlUpConfig
            {
                enableDebugLogs = this.enableDebugLogs,
                autoTrackSessions = this.autoTrackSessions,
                eventBatchSize = this.eventBatchSize,
                eventFlushInterval = this.eventFlushInterval,
                maxQueueSize = this.maxQueueSize,
                retryAttempts = this.retryAttempts,
                timeout = this.timeout,
                sendImmediately = this.sendImmediately,
                autoTrackAppLifecycle = this.autoTrackAppLifecycle,
                enableGeoTracking = this.enableGeoTracking,
                autoTrackScenes = this.autoTrackScenes,
                persistQueueToDisk = this.persistQueueToDisk,
                enableCrashReporting = this.enableCrashReporting,
            };
        }

        /// <summary>
        /// Get API Key from config
        /// </summary>
        public string GetApiKey()
        {
            return apiKey;
        }

        /// <summary>
        /// Get Base URL from config based on selected environment
        /// </summary>
        public string GetBaseUrl()
        {
            return apiEnvironment == ApiEnvironment.Production ? PRODUCTION_URL : DEVELOPMENT_URL;
        }

        /// <summary>
        /// Validate that required fields are configured
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(apiKey);
        }
    }
}
