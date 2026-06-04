using System;
using System.Collections.Generic;

namespace LvlUp.Models
{
    /// <summary>
    /// Revenue data for tracking monetization (Ad Impressions + In-App Purchases)
    /// </summary>
    [Serializable]
    public class RevenueData
    {
        // Core fields
        public string revenueType;        // "AD_IMPRESSION" or "IN_APP_PURCHASE"
        public double revenue;            // Revenue amount
        public double revenueUSD;         // Revenue amount in USD
        public string currency;           // Currency code (default: "USD")
        
        // Timing
        public long timestamp;            // Unix timestamp in milliseconds
        public long? transactionTimestamp; // Transaction timestamp from store/network
        
        // Ad Impression fields (populated when revenueType = "AD_IMPRESSION")
        public string adNetworkName;      // e.g., "MAX", "AdMob", "IronSource"
        public string adFormat;           // e.g., "BANNER", "INTER", "REWARDED"
        public string adUnitId;
        public string adUnitName;
        public string adPlacement;
        public string adCreativeId;
        public string adImpressionId;     // Unique ID for deduplication
        public string adNetworkPlacement;
        
        // In-App Purchase fields (populated when revenueType = "IN_APP_PURCHASE")
        public string productId;          // e.g., "com.game.coins_100"
        public string productName;
        public string productType;        // e.g., "CONSUMABLE", "NON_CONSUMABLE", "SUBSCRIPTION"
        public string transactionId;      // Unique ID for deduplication
        public string store;              // e.g., "APPLE_APP_STORE", "GOOGLE_PLAY"
        public bool isVerified;
        public int quantity;
        public bool isRestored;

        // Player context (IAP only) — auto-injected by RevenueTrackingService from
        // LvlUpManager.GetCurrentLevel() when not explicitly set. Enables ARPU-by-level
        // analytics on the dashboard. HTTP payload uses Newtonsoft which serializes null
        // correctly; Unity's JsonUtility drops nullable value types on PlayerPrefs
        // persistence (same limitation as transactionTimestamp).
        public int? currentLevelOrder;
        
        // Context (from EventMetadata)
        public string platform;
        public string deviceId;
        public string appVersion;
        public string appBuild;
        public string countryCode;
        
        // Additional data
        public string customData;         // JSON string for additional data
        public Dictionary<string, string> abTests;
        
        public RevenueData()
        {
            currency = "USD";
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            quantity = 1;
        }
    }
}
