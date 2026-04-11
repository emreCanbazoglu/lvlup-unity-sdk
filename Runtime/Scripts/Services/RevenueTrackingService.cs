using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LvlUp.Models;
using LvlUp.Utils;

namespace LvlUp.Services
{
    /// <summary>
    /// Service responsible for tracking revenue (ads and IAP)
    /// Handles revenue queueing, batching, flushing, and offline persistence
    /// </summary>
    public class RevenueTrackingService
    {
        private readonly LvlUpHttpClient _httpClient;
        private readonly LvlUpConfig _config;
        private readonly MonoBehaviour _coroutineRunner;
        private readonly Func<string> _getUserId;
        private readonly Func<string> _getSessionId;
        private readonly Func<int> _getSessionNum;
        private readonly Func<GeoData> _getGeoData;
        private readonly Func<string> _getPlatform;
        private readonly Func<string> _getManufacturer;

        // Revenue queue for offline support
        private List<RevenueData> _revenueBatch = new List<RevenueData>();
        private bool _hasLoadedPersistedRevenue = false;
        private bool _isSendingRevenue = false;

        // PlayerPrefs keys for persistence
        private const string PREF_OFFLINE_REVENUE = "LvlUp_OfflineRevenue";
        private const string PREF_OFFLINE_REVENUE_COUNT = "LvlUp_OfflineRevenueCount";
        
        // Memory and storage guards
        private const int MAX_REVENUE_QUEUE_HARD_CAP = 500; // Hard cap for queued revenue items
        private const long MAX_MEMORY_FREE_THRESHOLD = 100 * 1024 * 1024; // 10MB - minimum free memory required to persist

        // Transaction deduplication — prevents double-tracking when both
        // TrackPurchase() and manual TrackInAppPurchase() are called for the same purchase.
        private readonly HashSet<string> _recentTransactionIds = new HashSet<string>();
        private const int MAX_DEDUP_ENTRIES = 200;

        public RevenueTrackingService(
            LvlUpHttpClient httpClient,
            LvlUpConfig config,
            MonoBehaviour coroutineRunner,
            Func<string> getUserId,
            Func<string> getSessionId,
            Func<int> getSessionNum,
            Func<GeoData> getGeoData,
            Func<string> getPlatform,
            Func<string> getManufacturer)
        {
            _httpClient = httpClient;
            _config = config;
            _coroutineRunner = coroutineRunner;
            _getUserId = getUserId;
            _getSessionId = getSessionId;
            _getSessionNum = getSessionNum;
            _getGeoData = getGeoData;
            _getPlatform = getPlatform;
            _getManufacturer = getManufacturer;
        }

        /// <summary>
        /// Initialize service - load persisted revenue
        /// </summary>
        public void Initialize()
        {
            LoadPersistedRevenue();
        }

        /// <summary>
        /// Track revenue (Ad Impression or In-App Purchase)
        /// </summary>
        public void TrackRevenue(RevenueData revenueData, Action<ApiResponse> callback = null)
        {
            if (revenueData == null)
            {
                Debug.LogError("[LvlUp] RevenueData cannot be null");
                callback?.Invoke(new ApiResponse { success = false, error = "RevenueData cannot be null" });
                return;
            }

            // Deduplicate IAP by transactionId — prevents double-tracking when both
            // TrackPurchase() and manual TrackInAppPurchase() are called for the same purchase.
            if (revenueData.revenueType == "IN_APP_PURCHASE" && !string.IsNullOrEmpty(revenueData.transactionId))
            {
                if (_recentTransactionIds.Contains(revenueData.transactionId))
                {
                    if (_config.enableDebugLogs)
                        Debug.Log($"[LvlUp] Duplicate IAP skipped (transactionId: {revenueData.transactionId})");
                    callback?.Invoke(new ApiResponse { success = true, message = "Duplicate skipped" });
                    return;
                }

                _recentTransactionIds.Add(revenueData.transactionId);

                // Evict oldest entries when the set gets too large
                if (_recentTransactionIds.Count > MAX_DEDUP_ENTRIES)
                {
                    _recentTransactionIds.Clear();
                }
            }

            // Populate context from current session and metadata
            PopulateRevenueContext(revenueData);

            // Add to batch
            _revenueBatch.Add(revenueData);
            EnforceRevenueQueueCap();

            if (_config.enableDebugLogs)
            {
                string type = revenueData.revenueType == "AD_IMPRESSION" ? "Ad" : "IAP";
                Debug.Log($"[LvlUp] Revenue queued: {type} ${revenueData.revenue:F4} (Batch: {_revenueBatch.Count})");
            }

            // Check if we need to flush
            if (_revenueBatch.Count >= _config.eventBatchSize)
            {
                FlushRevenueQueue();
            }

            callback?.Invoke(new ApiResponse { success = true, message = "Revenue queued" });
        }

        /// <summary>
        /// Track an ad impression (convenience method)
        /// </summary>
        public void TrackAdImpression(string adNetworkName, string adFormat, double revenue,
            string adUnitId = null, string placement = null, string creativeId = null)
        {
            var revenueData = new RevenueData
            {
                revenueType = "AD_IMPRESSION",
                revenue = revenue,
                currency = "USD",
                adNetworkName = adNetworkName,
                adFormat = adFormat,
                adUnitId = adUnitId,
                adPlacement = placement,
                adCreativeId = creativeId
            };

            TrackRevenue(revenueData);
        }

        /// <summary>
        /// Track an in-app purchase (convenience method)
        /// </summary>
        public void TrackInAppPurchase(string productId, double revenue, string currency, double revenueUSD, string transactionId,
            string store = null, string productName = null, string productType = null, int quantity = 1, bool isVerified = false)
        {
            var revenueData = new RevenueData
            {
                revenueType = "IN_APP_PURCHASE",
                revenue = revenue,
                currency = currency,
                revenueUSD = revenueUSD,
                productId = productId,
                productName = productName,
                productType = productType,
                transactionId = transactionId,
                store = store,
                quantity = quantity,
                isVerified = isVerified
            };

            TrackRevenue(revenueData);
        }

        /// <summary>
        /// Flush the revenue queue and send to server
        /// </summary>
        public void FlushRevenueQueue()
        {
            if (_revenueBatch.Count == 0 || _isSendingRevenue)
                return;

            _isSendingRevenue = true;

            var batchToSend = new List<RevenueData>(_revenueBatch);
            _revenueBatch.Clear();

            if (_config.enableDebugLogs)
                Debug.Log($"[LvlUp] Flushing revenue batch: {batchToSend.Count} items");

            _coroutineRunner.StartCoroutine(SendRevenueBatch(batchToSend));
        }

        /// <summary>
        /// Get the number of queued revenue items
        /// </summary>
        public int GetQueuedRevenueCount()
        {
            return _revenueBatch.Count;
        }

        /// <summary>
        /// Check if we have enough memory and storage to persist revenue
        /// </summary>
        private bool CanPersistRevenue()
        {
            if (!_config.persistQueueToDisk)
            {
                return false;
            }

            // Check free memory
            long freeMemory = SystemInfo.systemMemorySize * 1024L * 1024L - UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            if (freeMemory < MAX_MEMORY_FREE_THRESHOLD)
            {
                Debug.LogWarning($"[LvlUp] Insufficient memory to persist revenue. Free memory: {freeMemory / (1024 * 1024)}MB, Required: {MAX_MEMORY_FREE_THRESHOLD / (1024 * 1024)}MB");
                return false;
            }

            // Check current persisted revenue count
            int currentPersistedCount = PlayerPrefs.GetInt(PREF_OFFLINE_REVENUE_COUNT, 0);
            if (currentPersistedCount >= GetMaxRevenueQueueSize())
            {
                Debug.LogWarning($"[LvlUp] Max persisted revenue limit reached: {currentPersistedCount}/{GetMaxRevenueQueueSize()}. Clearing old revenue data.");
                ClearPersistedRevenue();
                return true; // Continue with fresh slate
            }

            return true;
        }

        /// <summary>
        /// Persist revenue (Ad Impression or In-App Purchase) for offline support with guards
        /// </summary>
        public void PersistRevenue(List<RevenueData> revenue)
        {
            if (!_config.persistQueueToDisk)
                return;

            if (revenue == null || revenue.Count == 0)
            {
                int previousCount = PlayerPrefs.GetInt(PREF_OFFLINE_REVENUE_COUNT, 0);
                DeleteIndexedPrefRange(PREF_OFFLINE_REVENUE, 0, previousCount);
                PlayerPrefs.DeleteKey(PREF_OFFLINE_REVENUE_COUNT);
                PlayerPrefs.Save();
                return;
            }

            // Check if we can persist (memory and storage guards)
            if (!CanPersistRevenue())
            {
                Debug.LogWarning("[LvlUp] Cannot persist revenue due to memory/storage constraints. Dropping revenue data.");
                return;
            }

            try
            {
                int maxQueueSize = GetMaxRevenueQueueSize();
                int itemsToStore = Mathf.Min(revenue.Count, maxQueueSize);
                if (itemsToStore < revenue.Count)
                {
                    Debug.LogWarning($"[LvlUp] Limiting persisted revenue from {revenue.Count} to {itemsToStore} to keep queue bounded");
                }

                int startIndex = Mathf.Max(0, revenue.Count - itemsToStore);
                int previousCount = PlayerPrefs.GetInt(PREF_OFFLINE_REVENUE_COUNT, 0);

                for (int i = 0; i < itemsToStore; i++)
                {
                    string json = JsonUtility.ToJson(revenue[startIndex + i]);
                    string key = $"{PREF_OFFLINE_REVENUE}_{i}";
                    PlayerPrefs.SetString(key, json);
                }

                PlayerPrefs.SetInt(PREF_OFFLINE_REVENUE_COUNT, itemsToStore);
                DeleteIndexedPrefRange(PREF_OFFLINE_REVENUE, itemsToStore, previousCount);
                PlayerPrefs.Save();

                if (_config.enableDebugLogs)
                    Debug.Log($"[LvlUp] Persisted {itemsToStore} revenue items for offline");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LvlUp] Failed to persist revenue: {ex.Message}");
            }
        }

        /// <summary>
        /// Persist current revenue batch for offline support
        /// </summary>
        public void PersistRevenue()
        {
            if (!_config.persistQueueToDisk)
                return;

            EnforceRevenueQueueCap();

            if (_revenueBatch.Count > 0)
            {
                PersistRevenue(_revenueBatch);
            }
        }

        /// <summary>
        /// Clear all persisted offline revenue (call after successful send)
        /// </summary>
        private void ClearPersistedRevenue()
        {
            if (!_config.persistQueueToDisk)
                return;

            try
            {
                int count = PlayerPrefs.GetInt(PREF_OFFLINE_REVENUE_COUNT, 0);
                DeleteIndexedPrefRange(PREF_OFFLINE_REVENUE, 0, count);
                
                // Delete count key
                PlayerPrefs.DeleteKey(PREF_OFFLINE_REVENUE_COUNT);
                PlayerPrefs.Save();
                
                if (_config.enableDebugLogs && count > 0)
                    Debug.Log($"[LvlUp] Cleared {count} persisted offline revenue items");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LvlUp] Failed to clear persisted revenue: {ex.Message}");
            }
        }

        private void PopulateRevenueContext(RevenueData revenueData)
        {
            // Apply device metadata
            revenueData.platform = _getPlatform();
            revenueData.deviceId = SystemInfo.deviceUniqueIdentifier;
            revenueData.appVersion = Application.version;
            revenueData.appBuild = DeviceInfo.GetAppBuild();

            // Apply geo data if available (only countryCode)
            var geoData = _getGeoData();
            if (_config.enableGeoTracking && geoData != null)
            {
                revenueData.countryCode = geoData.countryCode;
            }
        }

        private IEnumerator SendRevenueBatch(List<RevenueData> batch)
        {
            var request = new
            {
                userId = _getUserId(),
                sessionId = _getSessionId(),
                revenueData = batch
            };

            bool requestComplete = false;
            bool requestSuccess = false;

            yield return _httpClient.Post<object>("analytics/revenue", request, response =>
            {
                requestComplete = true;
                requestSuccess = response.success;

                if (response.success)
                {
                    if (_config.enableDebugLogs)
                        Debug.Log($"[LvlUp] Revenue batch sent successfully: {batch.Count} items");
                    
                    // Clear offline storage for successfully sent items
                    ClearPersistedRevenue();
                }
                else
                {
                    Debug.LogWarning($"[LvlUp] Failed to send revenue batch: {response.error}");

                    // Re-queue failed events for retry
                    _revenueBatch.InsertRange(0, batch);
                    EnforceRevenueQueueCap();

                    // Persist to PlayerPrefs for offline support (only on failure)
                    PersistRevenue(_revenueBatch);
                }
            });

            yield return new WaitUntil(() => requestComplete);

            _isSendingRevenue = false;

            // If there are more items in the batch, flush again
            if (_revenueBatch.Count >= _config.eventBatchSize)
            {
                FlushRevenueQueue();
            }
        }

        private void LoadPersistedRevenue()
        {
            if (!_config.persistQueueToDisk)
                return;

            if (_hasLoadedPersistedRevenue)
                return;

            _hasLoadedPersistedRevenue = true;

            try
            {
                int count = PlayerPrefs.GetInt(PREF_OFFLINE_REVENUE_COUNT, 0);
                if (count == 0)
                    return;

                // Safety check: if persisted count is suspiciously high, it might be corrupted
                if (count > GetMaxRevenueQueueSize() * 2)
                {
                    Debug.LogWarning($"[LvlUp] Persisted revenue count ({count}) exceeds safety threshold ({GetMaxRevenueQueueSize() * 2}). Data may be corrupted. Clearing.");
                    ClearPersistedRevenue();
                    return;
                }

                // Check free memory before loading
                long freeMemory = SystemInfo.systemMemorySize * 1024L * 1024L - UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                if (freeMemory < MAX_MEMORY_FREE_THRESHOLD)
                {
                    Debug.LogWarning($"[LvlUp] Insufficient free memory ({freeMemory / (1024 * 1024)}MB) to load persisted revenue. Skipping load to prevent crash.");
                    ClearPersistedRevenue();
                    return;
                }

                int loadedCount = 0;
                int skippedCount = 0;
                
                for (int i = 0; i < count; i++)
                {
                    string key = $"{PREF_OFFLINE_REVENUE}_{i}";
                    if (PlayerPrefs.HasKey(key))
                    {
                        try
                        {
                            string json = PlayerPrefs.GetString(key);
                            
                            // Sanity check: JSON string shouldn't be unreasonably large
                            if (json.Length > 100000) // 100KB per revenue item is unreasonable
                            {
                                Debug.LogWarning($"[LvlUp] Revenue {i} data suspiciously large ({json.Length} bytes). Skipping to prevent memory issues.");
                                skippedCount++;
                                PlayerPrefs.DeleteKey(key);
                                continue;
                            }
                            
                            var revenue = JsonUtility.FromJson<RevenueData>(json);
                            if (revenue != null)
                            {
                                _revenueBatch.Add(revenue);
                                loadedCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[LvlUp] Failed to deserialize revenue {i}: {ex.Message}");
                            skippedCount++;
                        }

                        PlayerPrefs.DeleteKey(key);
                    }
                }

                PlayerPrefs.DeleteKey(PREF_OFFLINE_REVENUE_COUNT);
                PlayerPrefs.Save();

                if (loadedCount > 0 && _config.enableDebugLogs)
                    Debug.Log($"[LvlUp] Loaded {loadedCount} persisted offline revenue items" + (skippedCount > 0 ? $" (skipped {skippedCount} corrupted)" : ""));

                EnforceRevenueQueueCap();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LvlUp] Failed to load persisted revenue: {ex.Message}");
                // Clear potentially corrupted data
                try
                {
                    ClearPersistedRevenue();
                }
                catch (Exception clearEx)
                {
                    Debug.LogWarning($"[LvlUp] Failed to clear corrupted revenue: {clearEx.Message}");
                }
            }
        }

        private int GetMaxRevenueQueueSize()
        {
            return Mathf.Max(1, Mathf.Min(_config.maxQueueSize, MAX_REVENUE_QUEUE_HARD_CAP));
        }

        private void EnforceRevenueQueueCap()
        {
            int maxQueueSize = GetMaxRevenueQueueSize();
            if (_revenueBatch.Count <= maxQueueSize)
                return;

            int overflow = _revenueBatch.Count - maxQueueSize;
            _revenueBatch.RemoveRange(0, overflow);

            if (_config.enableDebugLogs)
                Debug.LogWarning($"[LvlUp] Revenue queue cap reached ({maxQueueSize}). Dropped {overflow} oldest revenue item(s).");
        }

        private void DeleteIndexedPrefRange(string prefix, int startInclusive, int endExclusive)
        {
            if (endExclusive <= startInclusive)
                return;

            for (int i = startInclusive; i < endExclusive; i++)
            {
                PlayerPrefs.DeleteKey($"{prefix}_{i}");
            }
        }
    }
}
