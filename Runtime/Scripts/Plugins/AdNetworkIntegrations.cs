using System;
using UnityEngine;
using LvlUp;
using LvlUp.Models;

namespace LvlUp.AdIntegration
{
    /// <summary>
    /// Example integration for MAX (AppLovin MAX) ad network
    /// Shows how to listen to MAX ad impressions and track them with LvlUp
    /// Inspired by GameAnalytics MAX integration pattern
    /// </summary>
    public class MaxAdIntegration
    {
#if UNITY_ANDROID || UNITY_IOS
        private static bool _initialized = false;

        /// <summary>
        /// Initialize MAX ad impression tracking with LvlUp
        /// Call this after MAX SDK is initialized
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
            {
                Debug.Log("[LvlUp] MAX integration already initialized");
                return;
            }

            // Get the ad monetization service from LvlUpManager
            var adMonetizationService = LvlUpManager.Instance.GetAdMonetizationService();
            if (adMonetizationService == null)
            {
                Debug.LogError("[LvlUp] AdMonetizationService not available. Ensure LvlUpManager is initialized.");
                return;
            }

            // Subscribe to MAX ad revenue events
            SubscribeToMaxEvents(adMonetizationService);
            _initialized = true;
            Debug.Log("[LvlUp] MAX ad integration initialized");
        }

        /// <summary>
        /// Subscribe to MAX SDK callbacks for different ad formats
        /// </summary>
        private static void SubscribeToMaxEvents(AdMonetizationService adMonetizationService)
        {
#if lvlup_max_enabled
            // Interstitial ads
            MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += (adUnitId, adInfo) =>
            {
                TrackMaxAdRevenue("INTER", adInfo, adMonetizationService);
            };

            // Banner ads
            MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent += (adUnitId, adInfo) =>
            {
                TrackMaxAdRevenue("BANNER", adInfo, adMonetizationService);
            };

            // Rewarded ads
            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += (adUnitId, adInfo) =>
            {
                TrackMaxAdRevenue("REWARDED", adInfo, adMonetizationService);
            };

            // Medium Rectangle (MREC) ads
            MaxSdkCallbacks.MRec.OnAdRevenuePaidEvent += (adUnitId, adInfo) =>
            {
                TrackMaxAdRevenue("MREC", adInfo, adMonetizationService);
            };

            // App Open ads
            MaxSdkCallbacks.AppOpen.OnAdRevenuePaidEvent += (adUnitId, adInfo) =>
            {
                TrackMaxAdRevenue("APPOPEN", adInfo, adMonetizationService);
            };
#endif
        }

        /// <summary>
        /// Track MAX ad impression with LvlUp
        /// Extracts MAX AdInfo and forwards to LvlUp AdMonetizationService
        /// </summary>
        private static void TrackMaxAdRevenue(string format, object adInfo, AdMonetizationService adMonetizationService)
        {
#if lvlup_max_enabled
            // Extract data from MAX AdInfo (MaxSdkBase.AdInfo structure)
            string adUnitId = GetAdInfoProperty(adInfo, "AdUnitIdentifier");
            string networkName = GetAdInfoProperty(adInfo, "NetworkName");
            string placement = GetAdInfoProperty(adInfo, "Placement");
            string creativeId = GetAdInfoProperty(adInfo, "CreativeIdentifier");
            double revenue = GetAdInfoDoubleProperty(adInfo, "Revenue");
            
            // Get country code from MAX SDK configuration if available
            string country = GetMaxCountryCode();
            
            // Track with LvlUp
            adMonetizationService.TrackMaxAdImpression(
                format: format,
                adUnitId: adUnitId,
                networkName: networkName,
                placement: placement,
                creativeId: creativeId,
                revenue: revenue,
                country: country
            );
#endif
        }

        /// <summary>
        /// Helper to extract string properties from AdInfo object
        /// Safely handles reflection errors
        /// </summary>
        private static string GetAdInfoProperty(object adInfo, string propertyName)
        {
            try
            {
                var property = adInfo.GetType().GetProperty(propertyName);
                return property?.GetValue(adInfo)?.ToString() ?? "";
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LvlUp] Failed to extract {propertyName} from MAX AdInfo: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// Helper to extract double properties from AdInfo object
        /// Handles double, float, and string conversions
        /// </summary>
        private static double GetAdInfoDoubleProperty(object adInfo, string propertyName)
        {
            try
            {
                var property = adInfo.GetType().GetProperty(propertyName);
                var value = property?.GetValue(adInfo);
                
                if (value is double d) return d;
                if (value is float f) return f;
                if (value is int i) return i;
                if (double.TryParse(value?.ToString() ?? "0", out double result)) return result;
                
                return 0;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LvlUp] Failed to extract {propertyName} from MAX AdInfo: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Get country code from MAX SDK configuration
        /// Returns empty string if not available
        /// </summary>
        private static string GetMaxCountryCode()
        {
            try
            {
#if lvlup_max_enabled
                // Try to get country code from MAX SDK configuration
                var sdkConfig = MaxSdk.GetSdkConfiguration();
                if (sdkConfig != null)
                {
                    var countryCodeProperty = sdkConfig.GetType().GetProperty("CountryCode");
                    if (countryCodeProperty != null)
                    {
                        return countryCodeProperty.GetValue(sdkConfig)?.ToString() ?? "";
                    }
                }
#endif
                return "";
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LvlUp] Failed to get country code from MAX: {ex.Message}");
                return "";
            }
        }
#endif
    }
}

