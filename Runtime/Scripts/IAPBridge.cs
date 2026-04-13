using System;

namespace LvlUp
{
    /// <summary>
    /// Registration point for the optional LvlUp.IAP assembly.
    /// When com.unity.purchasing is installed, the IAP assembly auto-registers
    /// its handlers here via [RuntimeInitializeOnLoadMethod]. The main assembly
    /// never references LvlUp.IAP directly, avoiding circular dependencies.
    /// </summary>
    public static class IAPBridge
    {
        /// <summary>
        /// Called by LvlUpManager.InitializeServices() to wire up the IAP delegate.
        /// Set by LvlUp.IAP assembly at load time.
        /// </summary>
        public static Action<Action<string, double, string, double, string, string, string, string, int, bool>> InitializeHandler;

        /// <summary>
        /// Called by LvlUpSDK.Revenue.TrackPurchase(object) to forward the product.
        /// Set by LvlUp.IAP assembly at load time.
        /// </summary>
        public static Action<object> TrackPurchaseHandler;
    }
}
