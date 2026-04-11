using System;
using UnityEngine;

namespace LvlUp.IAPIntegration
{
    /// <summary>
    /// Unity IAP auto-capture integration for LvlUp Analytics.
    /// Automatically extracts all IAP fields from Unity's Product object
    /// and forwards to RevenueTrackingService.
    ///
    /// Usage in IStoreListener.ProcessPurchase():
    ///   LvlUpSDK.Revenue.TrackPurchase(args.purchasedProduct);
    ///
    /// Requires com.unity.purchasing package. All code is conditionally compiled
    /// behind the lvlup_iap_enabled define (auto-set via asmdef versionDefines).
    /// </summary>
    public static class UnityIAPIntegration
    {
#if lvlup_iap_enabled
        private static Action<string, double, string, double, string, string, string, string, int, bool> _trackIAPDelegate;
        private static bool _initialized = false;

        /// <summary>
        /// Initialize the IAP integration with the revenue tracking delegate.
        /// Called from LvlUpManager.InitializeServices().
        /// </summary>
        public static void Initialize(
            Action<string, double, string, double, string, string, string, string, int, bool> trackInAppPurchaseDelegate)
        {
            if (_initialized)
            {
                Debug.Log("[LvlUp] Unity IAP integration already initialized");
                return;
            }

            _trackIAPDelegate = trackInAppPurchaseDelegate;
            _initialized = true;
            Debug.Log("[LvlUp] Unity IAP auto-capture initialized");
        }

        /// <summary>
        /// Track a Unity IAP purchase by extracting all fields from the Product object.
        /// Call this in your IStoreListener.ProcessPurchase() for one-line integration.
        ///
        /// Extracts: productId, localizedPrice, isoCurrencyCode, transactionID,
        /// store name, product type, and product title.
        /// </summary>
        /// <param name="product">Unity IAP Product from PurchaseEventArgs.purchasedProduct</param>
        public static void TrackPurchase(UnityEngine.Purchasing.Product product)
        {
            if (product == null)
            {
                Debug.LogError("[LvlUp] Cannot track null product");
                return;
            }

            if (_trackIAPDelegate == null)
            {
                Debug.LogError("[LvlUp] IAP integration not initialized. Ensure LvlUpSDK.Initialize() is called first.");
                return;
            }

            try
            {
                string productId = product.definition.id;
                double revenue = (double)product.metadata.localizedPrice;
                string currency = product.metadata.isoCurrencyCode ?? "USD";
                string transactionId = product.transactionID ?? "";
                string productName = product.metadata.localizedTitle ?? "";
                string productType = MapProductType(product.definition.type);
                string store = DetectStore();

                // revenueUSD: set to 0 so the backend converts via FX rates.
                // If currency is USD, the backend uses the revenue value directly.
                double revenueUSD = (currency == "USD") ? revenue : 0;

                _trackIAPDelegate.Invoke(
                    productId,
                    revenue,
                    currency,
                    revenueUSD,
                    transactionId,
                    store,
                    productName,
                    productType,
                    1,     // quantity
                    false  // isVerified — receipt validation not yet supported
                );

                Debug.Log($"[LvlUp] IAP auto-tracked: {productId} - {revenue} {currency} (txn: {transactionId})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LvlUp] Failed to auto-track IAP: {ex.Message}");
            }
        }

        /// <summary>
        /// Map Unity's ProductType enum to LvlUp's string representation
        /// </summary>
        private static string MapProductType(UnityEngine.Purchasing.ProductType type)
        {
            switch (type)
            {
                case UnityEngine.Purchasing.ProductType.Consumable:
                    return "CONSUMABLE";
                case UnityEngine.Purchasing.ProductType.NonConsumable:
                    return "NON_CONSUMABLE";
                case UnityEngine.Purchasing.ProductType.Subscription:
                    return "SUBSCRIPTION";
                default:
                    return "CONSUMABLE";
            }
        }

        /// <summary>
        /// Detect the current app store based on platform
        /// </summary>
        private static string DetectStore()
        {
#if UNITY_IOS
            return "APPLE_APP_STORE";
#elif UNITY_ANDROID
            return "GOOGLE_PLAY";
#elif UNITY_STANDALONE_OSX
            return "APPLE_MAC_APP_STORE";
#else
            return "UNKNOWN";
#endif
        }
#else
        // Stub methods when com.unity.purchasing is not installed.
        // These allow the code to compile but log a warning at runtime.

        public static void Initialize(
            Action<string, double, string, double, string, string, string, string, int, bool> trackInAppPurchaseDelegate)
        {
            // No-op: Unity IAP package not installed
        }

        /// <summary>
        /// Fallback when com.unity.purchasing is not installed.
        /// Accepts object to avoid compile errors; logs a warning.
        /// </summary>
        public static void TrackPurchase(object product)
        {
            Debug.LogWarning("[LvlUp] Unity IAP package (com.unity.purchasing) is not installed. " +
                "TrackPurchase() requires the Unity IAP package. Use TrackInAppPurchase() manually instead.");
        }
#endif
    }
}
