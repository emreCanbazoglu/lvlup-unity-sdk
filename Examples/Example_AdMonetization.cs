using LvlUp.AdIntegration;
using UnityEngine;
using LvlUp.Services;

namespace LvlUp.Examples
{
    /// <summary>
    /// Example: Basic Ad Monetization Tracking
    /// Demonstrates how to track ad impressions from different networks
    /// </summary>
    public class Example_AdMonetizationTracking : MonoBehaviour
    {
        private AdMonetizationService _adService;

        void Start()
        {
            // Get the ad monetization service
            _adService = LvlUpManager.Instance.GetAdMonetizationService();

            if (_adService != null && _adService.IsInitialized())
            {
                Debug.Log("AdMonetizationService is ready");
            }
        }

        /// <summary>
        /// Simulate a banner ad impression
        /// </summary>
        public void SimulateBannerAd()
        {
            if (_adService == null) return;

            _adService.TrackAdImpression(
                adNetworkName: "MAX",
                adFormat: "BANNER",
                adUnitId: "banner_bottom",
                placement: "gameplay",
                revenue: 0.0045,
                country: "US"
            );

            Debug.Log($"Banner ad tracked - Total Revenue: ${_adService.GetTotalRevenue()}");
        }

        /// <summary>
        /// Simulate an interstitial ad impression
        /// </summary>
        public void SimulateInterstitialAd()
        {
            if (_adService == null) return;

            _adService.TrackAdImpression(
                adNetworkName: "MAX",
                adFormat: "INTER",
                adUnitId: "inter_levelup",
                placement: "level_complete",
                revenue: 0.0123,
                country: "US"
            );

            Debug.Log($"Interstitial ad tracked - Total Revenue: ${_adService.GetTotalRevenue()}");
        }

        /// <summary>
        /// Simulate a rewarded video ad
        /// </summary>
        public void SimulateRewardedAd()
        {
            if (_adService == null) return;

            _adService.TrackAdImpression(
                adNetworkName: "MAX",
                adFormat: "REWARDED",
                adUnitId: "rewarded_coins",
                placement: "bonus_coins",
                revenue: 0.0456,
                country: "US"
            );

            Debug.Log($"Rewarded ad tracked - Total Revenue: ${_adService.GetTotalRevenue()}");
        }

        /// <summary>
        /// Log current revenue stats
        /// </summary>
        public void LogRevenueStats()
        {
            if (_adService == null) return;

            double totalRevenue = _adService.GetTotalRevenue();
            double bannerRevenue = _adService.GetFormatRevenue("BANNER");
            double interRevenue = _adService.GetFormatRevenue("INTER");
            double rewardedRevenue = _adService.GetFormatRevenue("REWARDED");
            double maxRevenue = _adService.GetNetworkRevenue("MAX");
            int pendingCount = _adService.GetPendingAdEventCount();

            Debug.Log($"=== Ad Revenue Report ===");
            Debug.Log($"Total Revenue: ${totalRevenue}");
            Debug.Log($"Banner Revenue: ${bannerRevenue}");
            Debug.Log($"Interstitial Revenue: ${interRevenue}");
            Debug.Log($"Rewarded Revenue: ${rewardedRevenue}");
            Debug.Log($"MAX Network Revenue: ${maxRevenue}");
            Debug.Log($"Pending Impressions: {pendingCount}");
        }
    }

    /// <summary>
    /// Example: MAX Ad Integration with LvlUp
    /// Shows how to automatically track MAX ad impressions
    /// </summary>
    public class Example_MaxAdIntegration : MonoBehaviour
    {
#if UNITY_ANDROID || UNITY_IOS
        void Start()
        {
            // Initialize MAX ad impression tracking
            // This automatically listens to all MAX ad events and tracks them with LvlUp
            MaxAdIntegration.Initialize();

            Debug.Log("MAX ad tracking initialized");
        }
#endif
    }

    /// <summary>
    /// Example: Network Callbacks
    /// Demonstrates how to register callbacks for specific ad networks
    /// </summary>
    public class Example_NetworkCallbacks : MonoBehaviour
    {
        void Start()
        {
            var adService = LvlUpManager.Instance.GetAdMonetizationService();
            if (adService == null) return;

            // Register callback for MAX network
            adService.RegisterNetworkCallback("MAX", (impression) =>
            {
                Debug.Log($"[MAX] Format: {impression.adFormat}, Revenue: ${impression.revenue}");
                
                // Your custom handling:
                // - Update UI with latest revenue
                // - Store in local database
                // - Send to analytics service
                // - Update achievement/milestone progress
                OnMaxAdImpression(impression);
            });

            // Register callback for AdMob network
            adService.RegisterNetworkCallback("AdMob", (impression) =>
            {
                Debug.Log($"[AdMob] Format: {impression.adFormat}, Revenue: ${impression.revenue}");
                OnAdMobImpression(impression);
            });
        }

        private void OnMaxAdImpression(LvlUp.Models.AdImpressionData impression)
        {
            // Handle MAX ad impression
            // Example: Update player rewards
            if (impression.adFormat == "REWARDED")
            {
                // Give player bonus coins
                // PlayerManager.Instance.AddCoins(100);
            }
        }

        private void OnAdMobImpression(LvlUp.Models.AdImpressionData impression)
        {
            // Handle AdMob ad impression
        }
    }

    /// <summary>
    /// Example: Revenue Monitoring
    /// Shows how to monitor and log revenue metrics
    /// </summary>
    public class Example_RevenueMonitoring : MonoBehaviour
    {
        private AdMonetizationService _adService;
        private float _updateInterval = 10f;
        private float _lastUpdateTime;

        void Start()
        {
            _adService = LvlUpManager.Instance.GetAdMonetizationService();
        }

        void Update()
        {
            if (_adService == null || !_adService.IsInitialized())
                return;

            // Log revenue stats periodically
            if (Time.time - _lastUpdateTime >= _updateInterval)
            {
                LogRevenueStats();
                _lastUpdateTime = Time.time;
            }
        }

        private void LogRevenueStats()
        {
            double totalRevenue = _adService.GetTotalRevenue();
            double maxRevenue = _adService.GetNetworkRevenue("MAX");
            double admobRevenue = _adService.GetNetworkRevenue("AdMob");
            double bannerRevenue = _adService.GetFormatRevenue("BANNER");
            double rewardedRevenue = _adService.GetFormatRevenue("REWARDED");

            Debug.Log($"[Revenue] Total: ${totalRevenue:F4} | MAX: ${maxRevenue:F4} | AdMob: ${admobRevenue:F4}");
            Debug.Log($"[Format] Banner: ${bannerRevenue:F4} | Rewarded: ${rewardedRevenue:F4}");
        }
    }

    /// <summary>
    /// Example: Custom Ad Tracking
    /// Shows how to track ads from custom sources with additional metadata
    /// </summary>
    public class Example_CustomAdTracking : MonoBehaviour
    {
        private AdMonetizationService _adService;

        void Start()
        {
            _adService = LvlUpManager.Instance.GetAdMonetizationService();
        }

        /// <summary>
        /// Track an ad with custom metadata
        /// </summary>
        public void TrackAdWithCustomData(string networkName, string adFormat, string adUnitId, double revenue, string gameLevel)
        {
            if (_adService == null) return;

            // Create custom data as JSON
            string customData = JsonUtility.ToJson(new { gameLevel, difficulty = "normal", userLevel = 5 });

            _adService.TrackAdImpression(
                adNetworkName: networkName,
                adFormat: adFormat,
                adUnitId: adUnitId,
                placement: $"level_{gameLevel}",
                revenue: revenue,
                adUnitName: $"{adFormat} Ad - Level {gameLevel}",
                customData: customData
            );

            Debug.Log($"Ad tracked with custom data: {customData}");
        }

        [System.Serializable]
        public class AdCustomData
        {
            public string gameLevel;
            public string difficulty;
            public int userLevel;
        }
    }

    /// <summary>
    /// Example: Ad Impression Analytics
    /// Shows how to query and analyze ad impression data
    /// </summary>
    public class Example_AdImpressionAnalytics : MonoBehaviour
    {
        private AdMonetizationService _adService;

        void Start()
        {
            _adService = LvlUpManager.Instance.GetAdMonetizationService();
        }

        /// <summary>
        /// Get detailed analytics about current impressions
        /// </summary>
        public void PrintAnalytics()
        {
            if (_adService == null) return;

            var allImpressions = _adService.GetPendingAdEvents();

            Debug.Log("=== Ad Impression Analytics ===");
            Debug.Log($"Total Impressions: {allImpressions.Count}");
            Debug.Log($"Total Revenue: ${_adService.GetTotalRevenue():F4}");

            // Count by network
            var networkCounts = new System.Collections.Generic.Dictionary<string, int>();
            var networkRevenue = new System.Collections.Generic.Dictionary<string, double>();

            foreach (var impression in allImpressions)
            {
                string network = impression.adData.adNetworkName;
                
                if (!networkCounts.ContainsKey(network))
                {
                    networkCounts[network] = 0;
                    networkRevenue[network] = 0;
                }

                networkCounts[network]++;
                networkRevenue[network] += impression.adData.revenue;
            }

            Debug.Log("=== By Network ===");
            foreach (var network in networkCounts.Keys)
            {
                Debug.Log($"{network}: {networkCounts[network]} impressions, ${networkRevenue[network]:F4} revenue");
            }

            // Count by format
            var formatCounts = new System.Collections.Generic.Dictionary<string, int>();
            var formatRevenue = new System.Collections.Generic.Dictionary<string, double>();

            foreach (var impression in allImpressions)
            {
                string format = impression.adData.adFormat;
                
                if (!formatCounts.ContainsKey(format))
                {
                    formatCounts[format] = 0;
                    formatRevenue[format] = 0;
                }

                formatCounts[format]++;
                formatRevenue[format] += impression.adData.revenue;
            }

            Debug.Log("=== By Format ===");
            foreach (var format in formatCounts.Keys)
            {
                Debug.Log($"{format}: {formatCounts[format]} impressions, ${formatRevenue[format]:F4} revenue");
            }
        }
    }
}

