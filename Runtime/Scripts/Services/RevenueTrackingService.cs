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

            // Populate context from current session and metadata
            PopulateRevenueContext(revenueData);

            // Add to batch
            _revenueBatch.Add(revenueData);

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
                adCreativeId = creativeId,
                adImpressionId = Guid.NewGuid().ToString()
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
        /// Persist revenue for offline support
        /// </summary>
        public void PersistRevenue(List<RevenueData> revenue)
        {
            try
            {
                int existingCount = PlayerPrefs.GetInt(PREF_OFFLINE_REVENUE_COUNT, 0);

                for (int i = 0; i < revenue.Count; i++)
                {
                    string json = JsonUtility.ToJson(revenue[i]);
                    string key = $"{PREF_OFFLINE_REVENUE}_{existingCount + i}";
                    PlayerPrefs.SetString(key, json);
                }

                PlayerPrefs.SetInt(PREF_OFFLINE_REVENUE_COUNT, existingCount + revenue.Count);
                PlayerPrefs.Save();

                if (_config.enableDebugLogs)
                    Debug.Log($"[LvlUp] Persisted {revenue.Count} revenue items for offline");
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
            if (_revenueBatch.Count > 0)
            {
                PersistRevenue(_revenueBatch);
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
                }
                else
                {
                    Debug.LogWarning($"[LvlUp] Failed to send revenue batch: {response.error}");

                    // Re-queue failed events for retry
                    _revenueBatch.InsertRange(0, batch);

                    // Persist to PlayerPrefs for offline support
                    PersistRevenue(batch);
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
            if (_hasLoadedPersistedRevenue)
                return;

            _hasLoadedPersistedRevenue = true;

            try
            {
                int count = PlayerPrefs.GetInt(PREF_OFFLINE_REVENUE_COUNT, 0);
                if (count == 0)
                    return;

                int loadedCount = 0;
                for (int i = 0; i < count; i++)
                {
                    string key = $"{PREF_OFFLINE_REVENUE}_{i}";
                    if (PlayerPrefs.HasKey(key))
                    {
                        string json = PlayerPrefs.GetString(key);
                        try
                        {
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
                        }

                        PlayerPrefs.DeleteKey(key);
                    }
                }

                PlayerPrefs.DeleteKey(PREF_OFFLINE_REVENUE_COUNT);
                PlayerPrefs.Save();

                if (loadedCount > 0 && _config.enableDebugLogs)
                    Debug.Log($"[LvlUp] Loaded {loadedCount} persisted offline revenue items");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LvlUp] Failed to load persisted revenue: {ex.Message}");
            }
        }
    }
}

