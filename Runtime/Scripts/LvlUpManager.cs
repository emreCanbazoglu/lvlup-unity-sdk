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

        // Session tracking
        private SessionData _currentSession;
        private string _currentUserId;
        private UserMetadata _currentUserMetadata;

        // Event queue for offline support
        private Queue<LvlUpEvent> _eventQueue = new Queue<LvlUpEvent>();
        private List<LvlUpEvent> _eventBatch = new List<LvlUpEvent>();
        private float _lastFlushTime;

        // State
        private bool _isInitialized = false;
        private bool _isSendingEvents = false;

        #region Initialization

        /// <summary>
        /// Initialize the LvlUp SDK
        /// </summary>
        public static void Initialize(string apiKey, string baseUrl, LvlUpConfig config = null)
        {
            Instance._Initialize(apiKey, baseUrl, config);
        }

        private void _Initialize(string apiKey, string baseUrl, LvlUpConfig config = null)
        {
            if (_isInitialized)
            {
                Debug.LogWarning("[LvlUp] SDK already initialized");
                return;
            }

            _apiKey = apiKey;
            _baseUrl = baseUrl;
            _config = config ?? new LvlUpConfig();

            _httpClient = new LvlUpHttpClient(_baseUrl, _apiKey, _config.timeout, _config.enableDebugLogs);
            _isInitialized = true;
            _lastFlushTime = Time.time;

            if (_config.enableDebugLogs)
                Debug.Log($"[LvlUp] SDK Initialized - Base URL: {_baseUrl}");

            // Start automatic flush coroutine
            if (!_config.sendImmediately)
                StartCoroutine(AutoFlushCoroutine());
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
                // App paused - end session
                if (_config.autoTrackAppLifecycle)
                    TrackEvent("app_paused", null);
                
                FlushEventQueue();
            }
            else
            {
                // App resumed
                if (_config.autoTrackAppLifecycle)
                    TrackEvent("app_resumed", null);
            }
        }

        private void OnApplicationQuit()
        {
            if (_isInitialized && _currentSession != null)
            {
                if (_config.autoTrackAppLifecycle)
                    TrackEvent("app_quit", null);
                
                FlushEventQueue();
                
                // End session synchronously before quitting
                StartCoroutine(EndSessionCoroutine());
            }
        }

        #endregion

        #region Session Management

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

            var request = new SessionStartRequest
            {
                userId = userId,
                deviceId = _currentUserMetadata.deviceId ?? SystemInfo.deviceUniqueIdentifier,
                platform = _currentUserMetadata.platform ?? Application.platform.ToString(),
                version = _currentUserMetadata.version ?? Application.version,
                country = _currentUserMetadata.country ?? "Unknown",
                language = _currentUserMetadata.language ?? Application.systemLanguage.ToString()
            };

            StartCoroutine(_httpClient.Post<SessionData>("analytics/session/start", request, response =>
            {
                if (response.success)
                {
                    _currentSession = response.data;
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

            var request = new SessionEndRequest
            {
                sessionId = _currentSession.sessionId
            };

            StartCoroutine(_httpClient.Put<SessionData>("analytics/session/end", request, response =>
            {
                if (response.success)
                {
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

        #region Event Tracking

        /// <summary>
        /// Track a single event
        /// </summary>
        public void TrackEvent(string eventName, Dictionary<string, object> properties, Action<ApiResponse> callback = null)
        {
            if (!_isInitialized)
            {
                Debug.LogError("[LvlUp] SDK not initialized. Call Initialize() first.");
                callback?.Invoke(new ApiResponse { success = false, error = "SDK not initialized" });
                return;
            }

            var lvlUpEvent = new LvlUpEvent(eventName, properties);

            if (_config.sendImmediately)
            {
                SendEventImmediately(lvlUpEvent, callback);
            }
            else
            {
                _eventBatch.Add(lvlUpEvent);

                if (_config.enableDebugLogs)
                    Debug.Log($"[LvlUp] Event queued: {eventName} (Batch size: {_eventBatch.Count}/{_config.eventBatchSize})");

                // Check if we need to flush
                if (_eventBatch.Count >= _config.eventBatchSize)
                {
                    FlushEventQueue();
                }

                callback?.Invoke(new ApiResponse { success = true, message = "Event queued" });
            }
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
                deviceInfo = new DeviceInfo
                {
                    platform = _currentUserMetadata?.platform ?? Application.platform.ToString(),
                    version = _currentUserMetadata?.version ?? Application.version,
                    deviceId = _currentUserMetadata?.deviceId ?? SystemInfo.deviceUniqueIdentifier
                }
            };

            foreach (var evt in events)
            {
                batchRequest.events.Add(new EventDataItem
                {
                    eventName = evt.eventName,
                    properties = evt.properties,
                    timestamp = evt.timestamp
                });
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
            var request = new EventTrackRequest
            {
                userId = _currentUserId,
                sessionId = _currentSession?.sessionId,
                type = lvlUpEvent.eventName,
                data = lvlUpEvent.properties
            };

            StartCoroutine(_httpClient.Post<object>("analytics/events", request, response =>
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

            var request = new CheckpointRecordRequest
            {
                userId = _currentUserId,
                checkpointId = checkpointId,
                metadata = metadata ?? new Dictionary<string, object>()
            };

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

        #endregion
    }
}

