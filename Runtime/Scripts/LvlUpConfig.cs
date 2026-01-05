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
        /// Track Unity scenes automatically
        /// </summary>
        public bool autoTrackScenes = false;

        /// <summary>
        /// Persist event queue to disk for offline support
        /// </summary>
        public bool persistQueueToDisk = true;
    }
}

