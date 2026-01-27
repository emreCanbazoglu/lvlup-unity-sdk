namespace LvlUp
{
    /// <summary>
    /// Configuration options for LvlUp SDK
    /// </summary>
    public class LvlUpConfig
    {
        /// <summary>
        /// Enable debug logging
        /// </summary>
        public bool enableDebugLogs = false;

        /// <summary>
        /// Automatically track sessions on app lifecycle events
        /// </summary>
        public bool autoTrackSessions = true;

        /// <summary>
        /// Number of events to batch before sending
        /// </summary>
        public int eventBatchSize = 50;

        /// <summary>
        /// Time interval (in seconds) to flush events automatically
        /// </summary>
        public float eventFlushInterval = 30f;

        /// <summary>
        /// Maximum number of events to queue offline
        /// </summary>
        public int maxQueueSize = 1000;

        /// <summary>
        /// Number of retry attempts for failed requests
        /// </summary>
        public int retryAttempts = 3;

        /// <summary>
        /// Request timeout in seconds
        /// </summary>
        public float timeout = 30f;

        /// <summary>
        /// Send events immediately instead of batching
        /// </summary>
        public bool sendImmediately = false;

        /// <summary>
        /// Automatically track application pause/resume events
        /// </summary>
        public bool autoTrackAppLifecycle = true;

        /// <summary>
        /// Enable automatic geographic location tracking using IP geolocation
        /// Fetches location data on initialization and caches for 1 hour
        /// </summary>
        public bool enableGeoTracking = false;

        /// <summary>
        /// Track Unity scenes automatically
        /// </summary>
        public bool autoTrackScenes = false;

        /// <summary>
        /// Persist event queue to disk for offline support
        /// </summary>
        public bool persistQueueToDisk = true;

        /// <summary>
        /// Enable automatic crash and exception reporting
        /// </summary>
        public bool enableCrashReporting = true;

        /// <summary>
        /// Level funnel name for tracking different level design iterations
        /// Example: "live_v1", "live_v2", "test_hard"
        /// This helps compare performance between different level layouts/designs
        /// </summary>
        public string levelFunnel = null;

        /// <summary>
        /// Level funnel version number (incremental)
        /// Example: 1, 2, 3, etc.
        /// Incremented when changes are made to the level funnel
        /// </summary>
        public int levelFunnelVersion = 1;
        
        public string remoteConfigEnvironment = "production";
    }
}

