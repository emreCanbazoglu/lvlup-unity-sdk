using System;
using System.Collections.Generic;
using UnityEngine;
using LvlUp.Models;

namespace LvlUp.Services
{
    /// <summary>
    /// Service for tracking ad monetization and impressions
    /// Supports multiple ad networks (MAX, AdMob, IronSource, etc.)
    /// </summary>
    public class AdMonetizationService
    {
        private LvlUpHttpClient _httpClient;
        private MonoBehaviour _coroutineRunner;
        private bool _isInitialized;


        /// <summary>
        /// Initialize the ad monetization service
        /// Also initializes MAX integration if lvlup_max_enabled is defined
        /// </summary>
        public void Initialize(LvlUpHttpClient httpClient, MonoBehaviour coroutineRunner)
        {
            _httpClient = httpClient;
            _coroutineRunner = coroutineRunner;
            _isInitialized = true;
            Debug.Log("[LvlUp] AdMonetizationService initialized");

            // Initialize MAX integration if enabled
#if lvlup_max_enabled
            InitializeMaxIntegration();
#endif
        }

        /// <summary>
        /// Initialize MAX ad network integration
        /// </summary>
        private void InitializeMaxIntegration()
        {
#if lvlup_max_enabled && (UNITY_ANDROID || UNITY_IOS)
            try
            {
                LvlUp.AdIntegration.MaxAdIntegration.Initialize();
                Debug.Log("[LvlUp] MAX ad integration initialized automatically");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LvlUp] Failed to initialize MAX integration: {ex.Message}");
            }
#endif
        }

        /// <summary>
        /// Check if the service is initialized
        /// </summary>
        public bool IsInitialized()
        {
            return _isInitialized;
        }

        /// <summary>
        /// Track an ad impression from MAX (AppLovin MAX)
        /// </summary>
        public void TrackMaxAdImpression(string format, string adUnitId, string networkName, 
            string placement, string creativeId, double revenue, string country = "")
        {
            TrackAdImpression(
                adNetworkName: "MAX",
                adFormat: format,
                adUnitId: adUnitId,
                placement: placement,
                creativeId: creativeId,
                revenue: revenue,
                country: country
            );
        }

        /// <summary>
        /// Generic method to track ad impression from any network
        /// </summary>
        public void TrackAdImpression(string adNetworkName, string adFormat, string adUnitId,
            string placement = "", string creativeId = "", double revenue = 0.0,
            string country = "", string adUnitName = "", string customData = "")
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[LvlUp] AdMonetizationService not initialized. Call Initialize() first.");
                return;
            }

            // Create ad impression data
            AdImpressionData adData = new AdImpressionData
            {
                adNetworkName = adNetworkName,
                adFormat = adFormat,
                adUnitId = adUnitId,
                adUnitName = adUnitName,
                placement = placement,
                creativeId = creativeId,
                revenue = revenue,
                revenueCurrency = "USD",
                country = country,
                impressionId = Guid.NewGuid().ToString(),
                impressionTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                customData = customData
            };

            // Create ad monetization event
            long currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            AdMonetizationEvent adEvent = new AdMonetizationEvent
            {
                eventType = "ad_impression",
                adData = adData,
                metadata = CloneEventMetadata(_currentEventMetadata),
                timestamp = currentTimestamp
            };

            // Send event to backend
            if (_httpClient != null && _coroutineRunner != null)
            {
                _coroutineRunner.StartCoroutine(_httpClient.Post<object>("/ad-impressions", adEvent, (response) =>
                {
                    if (!response.success)
                    {
                        Debug.LogWarning($"[LvlUp] Failed to send ad impression event: {response.error}");
                    }
                    else
                    {
                        Debug.Log("[LvlUp] Ad impression event sent successfully");
                    }
                }));
            }

            Debug.Log($"[LvlUp] Ad Impression Tracked: {adNetworkName} - {adFormat} - ${revenue}");
        }
    }
}

