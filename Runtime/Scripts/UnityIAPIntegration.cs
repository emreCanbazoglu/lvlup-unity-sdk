using System;
using System.Reflection;
using UnityEngine;

namespace LvlUp
{
    /// <summary>
    /// Unity IAP auto-capture integration.
    ///
    /// Uses reflection on the Product object instead of a compile-time reference to
    /// UnityEngine.Purchasing / Unity.Purchasing. This lets the same code support
    /// both Unity IAP 4.x (assembly `UnityEngine.Purchasing`) and 5.x (assembly
    /// renamed to `Unity.Purchasing`) without shipping two parallel implementations.
    ///
    /// The Product API we rely on — `hasReceipt`, `transactionID`, `definition.id`,
    /// `definition.type`, `metadata.localizedPrice`, `metadata.isoCurrencyCode`,
    /// `metadata.localizedTitle` — has been stable across IAP major versions.
    /// Reflection handles are bound once on first call and cached.
    ///
    /// Usage in IStoreListener.ProcessPurchase():
    ///   LvlUpSDK.Revenue.TrackPurchase(args.purchasedProduct);
    /// </summary>
    public static class UnityIAPIntegration
    {
        // Cached reflection handles — bound lazily on first successful call.
        private static PropertyInfo _piDefinition;
        private static PropertyInfo _piMetadata;
        private static PropertyInfo _piTransactionID;
        private static PropertyInfo _piHasReceipt;
        private static PropertyInfo _piDefinitionId;
        private static PropertyInfo _piDefinitionType;
        private static PropertyInfo _piLocalizedPrice;
        private static PropertyInfo _piIsoCurrencyCode;
        private static PropertyInfo _piLocalizedTitle;
        private static bool _bound;
        private static bool _bindFailed;

        /// <summary>
        /// Extract all IAP fields from a Unity IAP Product and forward to
        /// LvlUpManager.TrackInAppPurchase. Safe to call before Bind has been
        /// attempted — the first successful call will bind and cache.
        /// </summary>
        /// <param name="product">Unity IAP Product from PurchaseEventArgs.purchasedProduct</param>
        public static void TrackPurchase(object product)
        {
            if (product == null)
            {
                Debug.LogError("[LvlUp] Cannot track null product");
                return;
            }

            var manager = LvlUpManager.Instance;
            if (manager == null)
            {
                Debug.LogError("[LvlUp] SDK not initialized. Call LvlUpSDK.Initialize() first.");
                return;
            }

            if (!manager.GetAutoTrackIAP())
            {
                // Explicit opt-out — caller should use LvlUpSDK.Revenue.TrackInAppPurchase manually
                return;
            }

            if (!Bind(product.GetType()))
            {
                Debug.LogError($"[LvlUp] TrackPurchase could not bind to type {product.GetType().FullName} — " +
                    "expected a UnityEngine.Purchasing.Product (Unity IAP 4.x or 5.x). " +
                    "If you intended a different object, use LvlUpSDK.Revenue.TrackInAppPurchase(...) manually.");
                return;
            }

            try
            {
                bool hasReceipt = (bool)_piHasReceipt.GetValue(product);
                object definition = _piDefinition.GetValue(product);
                string productId = (string)_piDefinitionId.GetValue(definition);

                if (!hasReceipt)
                {
                    Debug.LogWarning($"[LvlUp] Skipping IAP track for {productId} — no receipt (pending or failed purchase)");
                    return;
                }

                object metadata = _piMetadata.GetValue(product);
                decimal priceDec = (decimal)_piLocalizedPrice.GetValue(metadata);
                double revenue = (double)priceDec;

                if (revenue <= 0)
                {
                    Debug.LogWarning($"[LvlUp] Skipping IAP track for {productId} — revenue is {revenue} (store metadata may not be loaded)");
                    return;
                }

                string currency = (string)_piIsoCurrencyCode.GetValue(metadata);
                if (string.IsNullOrEmpty(currency))
                {
                    Debug.LogWarning($"[LvlUp] IAP currency is null for {productId}, defaulting to USD — revenue may be inaccurate if price is non-USD");
                    currency = "USD";
                }

                string transactionId = ((string)_piTransactionID.GetValue(product)) ?? "";
                string productName = ((string)_piLocalizedTitle.GetValue(metadata)) ?? "";
                string productType = MapProductType(_piDefinitionType.GetValue(definition));
                string store = DetectStore();

                // revenueUSD: set to 0 so the backend converts via FX rates.
                // If currency is USD, the backend uses the revenue value directly.
                double revenueUSD = (currency == "USD") ? revenue : 0;

                manager.TrackInAppPurchase(
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
        /// Resolve Product / ProductDefinition / ProductMetadata members via reflection.
        /// Cached after the first successful bind. On failure, sets _bindFailed so we
        /// don't spam reflection attempts on every subsequent call.
        /// </summary>
        private static bool Bind(Type productType)
        {
            if (_bound) return true;
            if (_bindFailed) return false;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;

            try
            {
                _piHasReceipt    = productType.GetProperty("hasReceipt",    flags);
                _piTransactionID = productType.GetProperty("transactionID", flags);
                _piDefinition    = productType.GetProperty("definition",    flags);
                _piMetadata      = productType.GetProperty("metadata",      flags);

                if (_piHasReceipt == null || _piTransactionID == null ||
                    _piDefinition == null || _piMetadata == null)
                {
                    _bindFailed = true;
                    return false;
                }

                Type defType = _piDefinition.PropertyType;
                _piDefinitionId   = defType.GetProperty("id",   flags);
                _piDefinitionType = defType.GetProperty("type", flags);

                Type metaType = _piMetadata.PropertyType;
                _piLocalizedPrice   = metaType.GetProperty("localizedPrice",   flags);
                _piIsoCurrencyCode  = metaType.GetProperty("isoCurrencyCode",  flags);
                _piLocalizedTitle   = metaType.GetProperty("localizedTitle",   flags);

                if (_piDefinitionId == null || _piDefinitionType == null ||
                    _piLocalizedPrice == null || _piIsoCurrencyCode == null || _piLocalizedTitle == null)
                {
                    _bindFailed = true;
                    return false;
                }

                _bound = true;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LvlUp] Reflection bind to Unity IAP Product failed: {ex.Message}");
                _bindFailed = true;
                return false;
            }
        }

        /// <summary>
        /// Map Unity's ProductType enum (compared by name so we don't need a
        /// typed reference) to LvlUp's string representation.
        /// </summary>
        private static string MapProductType(object productTypeEnum)
        {
            string name = productTypeEnum?.ToString() ?? "";
            switch (name)
            {
                case "Consumable":    return "CONSUMABLE";
                case "NonConsumable": return "NON_CONSUMABLE";
                case "Subscription":  return "SUBSCRIPTION";
                default:              return "CONSUMABLE";
            }
        }

        /// <summary>
        /// Detect the current app store based on platform.
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
    }
}
