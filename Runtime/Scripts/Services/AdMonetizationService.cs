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
        // Note: _httpClient reserved for future server transmission of ad events
        private EventMetadata _currentEventMetadata;
        private List<AdMonetizationEvent> _adEventQueue = new List<AdMonetizationEvent>();
        private bool _isInitialized;

        // Callbacks for various ad network events
        private Dictionary<string, Action<AdImpressionData>> _networkCallbacks = 
            new Dictionary<string, Action<AdImpressionData>>();

        /// <summary>
        /// Initialize the ad monetization service
        /// Also initializes MAX integration if lvlup_max_enabled is defined
        /// </summary>
        public void Initialize(LvlUpHttpClient httpClient, EventMetadata eventMetadata)
        {
            // Store httpClient for future server transmission of ad events
            // Currently tracking is local, but this allows for future server-side aggregation
            _currentEventMetadata = eventMetadata;
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
        /// Track an ad impression from AdMob
        /// </summary>
        public void TrackAdMobImpression(string format, string adUnitId, 
            string placement, double revenue, string country = "")
        {
            TrackAdImpression(
                adNetworkName: "AdMob",
                adFormat: format,
                adUnitId: adUnitId,
                placement: placement,
                revenue: revenue,
                country: country
            );
        }

        /// <summary>
        /// Track an ad impression from IronSource
        /// </summary>
        public void TrackIronSourceImpression(string format, string adUnitId, 
            string placement, double revenue, string country = "")
        {
            TrackAdImpression(
                adNetworkName: "IronSource",
                adFormat: format,
                adUnitId: adUnitId,
                placement: placement,
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
                country = string.IsNullOrEmpty(country) ? (_currentEventMetadata?.country ?? "") : country,
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

            // Add to queue
            _adEventQueue.Add(adEvent);

            // Invoke any registered callbacks
            string networkKey = adNetworkName.ToLower();
            if (_networkCallbacks.ContainsKey(networkKey))
            {
                _networkCallbacks[networkKey]?.Invoke(adData);
            }

            Debug.Log($"[LvlUp] Ad Impression Tracked: {adNetworkName} - {adFormat} - ${revenue}");
        }

        /// <summary>
        /// Register a callback for a specific ad network
        /// Callback is invoked whenever an ad from that network is tracked
        /// </summary>
        public void RegisterNetworkCallback(string networkName, Action<AdImpressionData> callback)
        {
            string networkKey = networkName.ToLower();
            _networkCallbacks[networkKey] = callback;
        }

        /// <summary>
        /// Unregister a callback for a specific ad network
        /// </summary>
        public void UnregisterNetworkCallback(string networkName)
        {
            string networkKey = networkName.ToLower();
            if (_networkCallbacks.ContainsKey(networkKey))
            {
                _networkCallbacks.Remove(networkKey);
            }
        }

        /// <summary>
        /// Get all pending ad events
        /// </summary>
        public List<AdMonetizationEvent> GetPendingAdEvents()
        {
            return new List<AdMonetizationEvent>(_adEventQueue);
        }

        /// <summary>
        /// Clear pending ad events after successful transmission
        /// </summary>
        public void ClearPendingAdEvents()
        {
            _adEventQueue.Clear();
        }

        /// <summary>
        /// Get the count of pending ad events
        /// </summary>
        public int GetPendingAdEventCount()
        {
            return _adEventQueue.Count;
        }

        /// <summary>
        /// Update the current event metadata
        /// Should be called when user or session context changes
        /// </summary>
        public void UpdateEventMetadata(EventMetadata eventMetadata)
        {
            _currentEventMetadata = eventMetadata;
        }

        /// <summary>
        /// Clone event metadata to ensure each event has its own metadata copy
        /// </summary>
        private EventMetadata CloneEventMetadata(EventMetadata source)
        {
            if (source == null) return new EventMetadata();

            EventMetadata clone = new EventMetadata
            {
                eventUuid = Guid.NewGuid().ToString(),
                clientTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                platform = source.platform,
                osVersion = source.osVersion,
                manufacturer = source.manufacturer,
                device = source.device,
                deviceId = source.deviceId,
                appVersion = source.appVersion,
                appBuild = source.appBuild,
                bundleId = source.bundleId,
                engineVersion = source.engineVersion,
                sdkVersion = source.sdkVersion,
                connectionType = source.connectionType,
                sessionNum = source.sessionNum,
                appSignature = source.appSignature,
                channelId = source.channelId,
                country = source.country,
                countryCode = source.countryCode,
                region = source.region,
                city = source.city,
                latitude = source.latitude,
                longitude = source.longitude,
                timezone = source.timezone
            };

            return clone;
        }

        /// <summary>
        /// Get total revenue from all tracked ad impressions
        /// </summary>
        public double GetTotalRevenue()
        {
            double total = 0;
            foreach (var adEvent in _adEventQueue)
            {
                total += adEvent.adData.revenue;
            }
            return total;
        }

        /// <summary>
        /// Get total revenue for a specific ad network
        /// </summary>
        public double GetNetworkRevenue(string networkName)
        {
            double total = 0;
            foreach (var adEvent in _adEventQueue)
            {
                if (adEvent.adData.adNetworkName.Equals(networkName, StringComparison.OrdinalIgnoreCase))
                {
                    total += adEvent.adData.revenue;
                }
            }
            return total;
        }

        /// <summary>
        /// Get total revenue for a specific ad format
        /// </summary>
        public double GetFormatRevenue(string adFormat)
        {
            double total = 0;
            foreach (var adEvent in _adEventQueue)
            {
                if (adEvent.adData.adFormat.Equals(adFormat, StringComparison.OrdinalIgnoreCase))
                {
                    total += adEvent.adData.revenue;
                }
            }
            return total;
        }
    }
}

