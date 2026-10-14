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
        private bool _isInitialized;
        
        private TrackAdImpressionDelegate _trackAdImpressionDelegate;

        /// <summary>
        /// Delegate for tracking ad impressions
        /// </summary>
        public delegate void TrackAdImpressionDelegate(
            string adNetworkName, 
            string adFormat, 
            double revenue,
            string adUnitId, 
            string placement, 
            string creativeId
        );

        /// <summary>
        /// Initialize the ad monetization service
        /// </summary>
        public void Initialize(TrackAdImpressionDelegate trackAdImpressionDelegate)
        {
            _trackAdImpressionDelegate = trackAdImpressionDelegate;
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
            TrackAdImpressionCore(
                adNetworkName: "MAX",
                adFormat: format,
                adUnitId: adUnitId,
                placement: null,
                creativeId: creativeId,
                revenue: revenue,
                maxPlacement: placement
            );
        }

        /// <summary>
        /// Generic method to track ad impression from any network
        /// </summary>
        public void TrackAdImpression(string adNetworkName, string adFormat, string adUnitId,
            string placement = "", string creativeId = "", double revenue = 0.0,
            string country = "", string adUnitName = "", string customData = "")
        {
            TrackAdImpressionCore(adNetworkName, adFormat, adUnitId, placement, creativeId, revenue);
        }

        private void TrackAdImpressionCore(string adNetworkName, string adFormat, string adUnitId,
            string placement, string creativeId, double revenue, string maxPlacement = null)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[LvlUp] AdMonetizationService not initialized. Call Initialize() first.");
                return;
            }

            string resolvedPlacement = AdPlacementContext.Shared.Resolve(adFormat, placement, maxPlacement);

            // The delegate receives a resolved placement; it must not consume the context again.
            // LvlUpManager will handle context population.
            _trackAdImpressionDelegate?.Invoke(
                adNetworkName, 
                adFormat, 
                revenue,
                adUnitId, 
                resolvedPlacement,
                creativeId
            );
            
            if (revenue > 0)
            {
                Debug.Log($"[LvlUp] Ad Impression tracked: {adNetworkName} - {adFormat} - ${revenue:F4}");
            }
        }
    }
}
