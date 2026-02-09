using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LvlUp.Models;

namespace LvlUp.Services
{
    /// <summary>
    /// Service responsible for tracking and batching events
    /// Handles event queueing, batching, flushing, and offline persistence
    /// </summary>
    public class EventTrackingService
    {
        private readonly LvlUpHttpClient _httpClient;
        private readonly LvlUpConfig _config;
        private readonly MonoBehaviour _coroutineRunner;
        private readonly Func<string> _getUserId;
        private readonly Func<string> _getSessionId;
        private readonly Func<int> _getSessionNum;
        private readonly Action<LvlUpEvent> _applyGeoData;

        // Event queue for offline support
        private List<LvlUpEvent> _eventBatch = new List<LvlUpEvent>();
        private bool _hasLoadedPersistedEvents = false;
        private bool _isSendingEvents = false;
        private float _lastFlushTime;

        // Server limits
        private const int MAX_BATCH_SIZE = 100; // Maximum events per batch allowed by server
        
        // Memory and storage guards
        private const int MAX_PERSISTED_EVENTS = 1000; // Absolute max events to persist (guard against unbounded growth)
        private const long MAX_MEMORY_FREE_THRESHOLD = 100 * 1024 * 1024; // 10MB - minimum free memory required to persist

        // PlayerPrefs keys for persistence
        private const string PREF_OFFLINE_EVENTS = "LvlUp_OfflineEvents";
        private const string PREF_OFFLINE_EVENT_COUNT = "LvlUp_OfflineEventCount";

        public EventTrackingService(
            LvlUpHttpClient httpClient,
            LvlUpConfig config,
            MonoBehaviour coroutineRunner,
            Func<string> getUserId,
            Func<string> getSessionId,
            Func<int> getSessionNum,
            Action<LvlUpEvent> applyGeoData)
        {
            _httpClient = httpClient;
            _config = config;
            _coroutineRunner = coroutineRunner;
            _getUserId = getUserId;
            _getSessionId = getSessionId;
            _getSessionNum = getSessionNum;
            _applyGeoData = applyGeoData;
        }

        /// <summary>
        /// Initialize service - load persisted events
        /// </summary>
        public void Initialize()
        {
            LoadPersistedEvents();
            _lastFlushTime = Time.time;
        }

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
            // Add session number if available
            int sessionNum = _getSessionNum();
            if (sessionNum > 0)
                lvlUpEvent.sessionNum = sessionNum;

            // Apply cached geo data if available
            _applyGeoData?.Invoke(lvlUpEvent);

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
        /// Track multiple events in a batch
        /// </summary>
        public void TrackEventsBatch(List<LvlUpEvent> events, Action<ApiResponse> callback = null)
        {
            if (events == null || events.Count == 0)
            {
                callback?.Invoke(new ApiResponse { success = false, error = "No events provided" });
                return;
            }

            var batchRequest = new BatchEventRequest
            {
                userId = _getUserId(),
                sessionId = _getSessionId(),
                events = new List<EventDataItem>(),
            };

            foreach (var evt in events)
            {
                batchRequest.events.Add(evt.ToEventDataItem());
            }

            _coroutineRunner.StartCoroutine(_httpClient.Post<object>("analytics/events/batch", batchRequest, response =>
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

            // Split into batches if we have more than MAX_BATCH_SIZE events
            if (_eventBatch.Count > MAX_BATCH_SIZE)
            {
                if (_config.enableDebugLogs)
                    Debug.Log($"[LvlUp] Splitting {_eventBatch.Count} events into multiple batches of {MAX_BATCH_SIZE}");

                _coroutineRunner.StartCoroutine(FlushInBatchesCoroutine(callback));
                return;
            }

            var eventsToSend = new List<LvlUpEvent>(_eventBatch);
            _isSendingEvents = true;

            TrackEventsBatch(eventsToSend, response =>
            {
                _isSendingEvents = false;
                _lastFlushTime = Time.time;

                if (response.success)
                {
                    // Success - remove sent events from batch
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
                    PersistEvents();

                    if (_config.enableDebugLogs)
                        Debug.LogWarning($"[LvlUp] Failed to send events: {response.error}. Will retry later.");
                }

                callback?.Invoke(response);
            });
        }

        /// <summary>
        /// Get the number of queued events
        /// </summary>
        public int GetQueuedEventCount()
        {
            return _eventBatch.Count;
        }

        /// <summary>
        /// Check if we have enough memory and storage to persist events
        /// </summary>
        private bool CanPersistEvents()
        {
            // Check free memory
            long freeMemory = SystemInfo.systemMemorySize * 1024L * 1024L - UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            if (freeMemory < MAX_MEMORY_FREE_THRESHOLD)
            {
                Debug.LogWarning($"[LvlUp] Insufficient memory to persist events. Free memory: {freeMemory / (1024 * 1024)}MB, Required: {MAX_MEMORY_FREE_THRESHOLD / (1024 * 1024)}MB");
                return false;
            }

            // Check current persisted event count
            int currentPersistedCount = PlayerPrefs.GetInt(PREF_OFFLINE_EVENT_COUNT, 0);
            if (currentPersistedCount >= MAX_PERSISTED_EVENTS)
            {
                Debug.LogWarning($"[LvlUp] Max persisted events limit reached: {currentPersistedCount}/{MAX_PERSISTED_EVENTS}. Clearing old events.");
                ClearPersistedEvents();
                return true; // Continue with fresh slate
            }

            return true;
        }

        /// <summary>
        /// Clear persisted offline events (called when limit is reached)
        /// </summary>
        private void ClearPersistedEvents()
        {
            try
            {
                int count = PlayerPrefs.GetInt(PREF_OFFLINE_EVENT_COUNT, 0);
                for (int i = 0; i < count; i++)
                {
                    string key = $"{PREF_OFFLINE_EVENTS}_{i}";
                    if (PlayerPrefs.HasKey(key))
                    {
                        PlayerPrefs.DeleteKey(key);
                    }
                }
                PlayerPrefs.DeleteKey(PREF_OFFLINE_EVENT_COUNT);
                PlayerPrefs.Save();

                if (_config.enableDebugLogs)
                    Debug.Log($"[LvlUp] Cleared {count} stale offline events to prevent memory bloat");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LvlUp] Failed to clear persisted events: {ex.Message}");
            }
        }

        /// <summary>
        /// Persist events for offline support with memory guards
        /// </summary>
        public void PersistEvents()
        {
            if (_eventBatch.Count == 0)
            {
                PlayerPrefs.DeleteKey(PREF_OFFLINE_EVENTS);
                PlayerPrefs.DeleteKey(PREF_OFFLINE_EVENT_COUNT);
                PlayerPrefs.Save();
                return;
            }

            // Check if we can persist (memory and storage guards)
            if (!CanPersistEvents())
            {
                Debug.LogWarning("[LvlUp] Cannot persist events due to memory/storage constraints. Clearing batch.");
                _eventBatch.Clear();
                return;
            }

            try
            {
                var eventsJson = new List<string>();
                foreach (var evt in _eventBatch)
                {
                    string json = JsonUtility.ToJson(evt);
                    eventsJson.Add(json);
                }

                // Guard: limit persisted events to prevent unbounded growth
                int maxEventsToStore = Mathf.Min(eventsJson.Count, MAX_PERSISTED_EVENTS);
                if (maxEventsToStore < eventsJson.Count)
                {
                    Debug.LogWarning($"[LvlUp] Limiting persisted events from {eventsJson.Count} to {maxEventsToStore} to prevent memory issues");
                    eventsJson = eventsJson.GetRange(0, maxEventsToStore);
                }

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
                // Don't lose events - keep them in memory batch even if persistence fails
            }
        }

        private void SendEventImmediately(LvlUpEvent lvlUpEvent, Action<ApiResponse> callback)
        {
            var batchRequest = new BatchEventRequest
            {
                userId = _getUserId(),
                sessionId = _getSessionId(),
                events = new List<EventDataItem> { lvlUpEvent.ToEventDataItem() },
            };

            _coroutineRunner.StartCoroutine(_httpClient.Post<object>("analytics/events/batch", batchRequest, response =>
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

        private IEnumerator FlushInBatchesCoroutine(Action<ApiResponse> callback)
        {
            _isSendingEvents = true;
            int totalSent = 0;
            bool allSucceeded = true;
            string lastError = null;

            while (_eventBatch.Count > 0 && allSucceeded)
            {
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
                        for (int i = 0; i < eventsToSend.Count && _eventBatch.Count > 0; i++)
                        {
                            _eventBatch.RemoveAt(0);
                        }
                        totalSent += eventsToSend.Count;

                        if (_config.enableDebugLogs)
                            Debug.Log($"[LvlUp] Batch sent: {eventsToSend.Count} events ({totalSent} total)");
                    }
                });

                yield return new WaitUntil(() => batchComplete);

                if (!batchSuccess)
                {
                    allSucceeded = false;
                    if (_config.enableDebugLogs)
                        Debug.LogWarning($"[LvlUp] Batch failed: {lastError}. Stopping flush.");

                    PersistEvents();
                }

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

                // Safety check: if persisted count is suspiciously high, it might be corrupted
                if (count > MAX_PERSISTED_EVENTS * 2)
                {
                    Debug.LogWarning($"[LvlUp] Persisted event count ({count}) exceeds safety threshold ({MAX_PERSISTED_EVENTS * 2}). Data may be corrupted. Clearing.");
                    ClearPersistedEvents();
                    return;
                }

                // Check free memory before loading
                long freeMemory = SystemInfo.systemMemorySize * 1024L * 1024L - UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                if (freeMemory < MAX_MEMORY_FREE_THRESHOLD)
                {
                    Debug.LogWarning($"[LvlUp] Insufficient free memory ({freeMemory / (1024 * 1024)}MB) to load persisted events. Skipping load to prevent crash.");
                    ClearPersistedEvents();
                    return;
                }

                int loadedCount = 0;
                int skippedCount = 0;
                
                for (int i = 0; i < count; i++)
                {
                    string key = $"{PREF_OFFLINE_EVENTS}_{i}";
                    if (PlayerPrefs.HasKey(key))
                    {
                        try
                        {
                            string json = PlayerPrefs.GetString(key);
                            
                            // Sanity check: JSON string shouldn't be unreasonably large
                            if (json.Length > 50000) // 50KB per event is unreasonable
                            {
                                Debug.LogWarning($"[LvlUp] Event {i} data suspiciously large ({json.Length} bytes). Skipping to prevent memory issues.");
                                skippedCount++;
                                PlayerPrefs.DeleteKey(key);
                                continue;
                            }
                            
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
                            skippedCount++;
                        }

                        PlayerPrefs.DeleteKey(key);
                    }
                }

                PlayerPrefs.DeleteKey(PREF_OFFLINE_EVENT_COUNT);
                PlayerPrefs.Save();

                if (loadedCount > 0 && _config.enableDebugLogs)
                    Debug.Log($"[LvlUp] Loaded {loadedCount} persisted offline events" + (skippedCount > 0 ? $" (skipped {skippedCount} corrupted)" : ""));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LvlUp] Failed to load persisted events: {ex.Message}");
                // Clear potentially corrupted data
                try
                {
                    ClearPersistedEvents();
                }
                catch (Exception clearEx)
                {
                    Debug.LogWarning($"[LvlUp] Failed to clear corrupted events: {clearEx.Message}");
                }
            }
        }
    }
}

