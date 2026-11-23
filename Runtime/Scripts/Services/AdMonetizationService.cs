using System;
using System.IO;
using System.Threading;
using UnityEngine;

namespace LvlUp.Services
{
    /// <summary>
    /// Service for tracking ad monetization and impressions
    /// Supports multiple ad networks (MAX, AdMob, IronSource, etc.)
    /// </summary>
    public class AdMonetizationService
    {
        private const string JOURNAL_FILE_NAME = "lvlup_pending_ad_impressions.jsonl";

        private bool _isInitialized;
        private int _mainThreadId;
        private AdImpressionJournal _journal;

        private TrackAdImpressionDelegate _trackAdImpressionDelegate;

        /// <summary>
        /// Delegate for tracking ad impressions. Always invoked on Unity's main thread.
        /// </summary>
        public delegate void TrackAdImpressionDelegate(
            string adNetworkName,
            string adFormat,
            double revenue,
            string adUnitId,
            string placement,
            string creativeId,
            string adImpressionId,
            long timestamp
        );

        /// <summary>
        /// Initialize the ad monetization service. Must be called on Unity's main thread.
        /// </summary>
        public void Initialize(TrackAdImpressionDelegate trackAdImpressionDelegate)
        {
            Initialize(trackAdImpressionDelegate, Path.Combine(Application.persistentDataPath, JOURNAL_FILE_NAME));
        }

        internal void Initialize(TrackAdImpressionDelegate trackAdImpressionDelegate, string journalPath)
        {
            _trackAdImpressionDelegate = trackAdImpressionDelegate;
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            _journal = new AdImpressionJournal(journalPath);
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
        /// Track an ad impression from MAX (AppLovin MAX). Safe to call from any thread.
        /// </summary>
        public void TrackMaxAdImpression(string format, string adUnitId, string networkName,
            string placement, string creativeId, double revenue, string country = "")
        {
            // Report the network that actually served the ad (e.g. "AppLovin", "Google AdMob");
            // fall back to the mediation name only when MAX doesn't provide one.
            TrackAdImpressionCore(
                adNetworkName: string.IsNullOrWhiteSpace(networkName) ? "MAX" : networkName,
                adFormat: format,
                adUnitId: adUnitId,
                placement: null,
                creativeId: creativeId,
                revenue: revenue,
                maxPlacement: placement
            );
        }

        /// <summary>
        /// Generic method to track ad impression from any network. Safe to call from any thread.
        /// </summary>
        public void TrackAdImpression(string adNetworkName, string adFormat, string adUnitId,
            string placement = "", string creativeId = "", double revenue = 0.0,
            string country = "", string adUnitName = "", string customData = "")
        {
            TrackAdImpressionCore(adNetworkName, adFormat, adUnitId, placement, creativeId, revenue);
        }

        /// <summary>
        /// Hands journaled impressions to the revenue queue. Runs only on Unity's main thread;
        /// LvlUpManager calls it every frame.
        /// </summary>
        public void DispatchPending()
        {
            if (!_isInitialized || !IsMainThread())
                return;

            var pending = _journal.TakeSnapshot();
            if (pending.Count == 0)
                return;

            foreach (var impression in pending)
            {
                try
                {
                    _trackAdImpressionDelegate?.Invoke(
                        impression.adNetworkName,
                        impression.adFormat,
                        impression.revenue,
                        impression.adUnitId,
                        impression.placement,
                        impression.creativeId,
                        impression.adImpressionId,
                        impression.timestamp
                    );
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[LvlUp] Failed to queue ad impression {impression.adImpressionId}: {ex}");
                }
            }

            _journal.Acknowledge(pending);
        }

        internal int PendingCount => _journal?.PendingCount ?? 0;

        private void TrackAdImpressionCore(string adNetworkName, string adFormat, string adUnitId,
            string placement, string creativeId, double revenue, string maxPlacement = null)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[LvlUp] AdMonetizationService not initialized. Call Initialize() first.");
                return;
            }

            // Resolved here, on the reporting thread, so the placement set for this ad is the one consumed.
            string resolvedPlacement = AdPlacementContext.Shared.Resolve(adFormat, placement, maxPlacement);

            _journal.Append(new PendingAdImpression
            {
                adImpressionId = Guid.NewGuid().ToString("N"),
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                adNetworkName = adNetworkName,
                adFormat = adFormat,
                revenue = revenue,
                adUnitId = adUnitId,
                placement = resolvedPlacement,
                creativeId = creativeId
            });

            if (revenue > 0)
            {
                Debug.Log($"[LvlUp] Ad Impression tracked: {adNetworkName} - {adFormat} - ${revenue:F4}");
            }

            DispatchPending();
        }

        private bool IsMainThread()
        {
            return Thread.CurrentThread.ManagedThreadId == _mainThreadId;
        }
    }
}
