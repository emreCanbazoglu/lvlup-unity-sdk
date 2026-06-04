using UnityEngine;

namespace LvlUp
{
    /// <summary>
    /// Debug settings for LvlUp SDK - used for testing
    /// Persists across Editor and builds using PlayerPrefs
    /// </summary>
    public static class LvlUpDebugSettings
    {
        private const string PLATFORM_OVERRIDE_KEY = "LvlUp.Debug.PlatformOverride";
        private const string ENVIRONMENT_OVERRIDE_KEY = "LvlUp.Debug.EnvironmentOverride";
        private const string FORCE_AB_LAYER_KEY = "LvlUp.Debug.ForceAbLayerKey";
        private const string FORCE_AB_TEST_KEY = "LvlUp.Debug.ForceAbTestKey";
        private const string FORCE_AB_VARIANT_KEY = "LvlUp.Debug.ForceAbVariantKey";

        #region Platform Override

        /// <summary>
        /// Override the platform reported by the SDK
        /// Valid values: null, "ios", "android", "webgl", "windows", "macos", "linux"
        /// Persists in Editor and builds via PlayerPrefs
        /// </summary>
        public static string PlatformOverride
        {
            get
            {
                return PlayerPrefs.GetString(PLATFORM_OVERRIDE_KEY, null);
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    PlayerPrefs.SetString(PLATFORM_OVERRIDE_KEY, value);
                }
                else
                {
                    PlayerPrefs.DeleteKey(PLATFORM_OVERRIDE_KEY);
                }
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Check if platform override is active
        /// </summary>
        public static bool HasPlatformOverride => !string.IsNullOrEmpty(PlatformOverride);

        /// <summary>
        /// Clear platform override
        /// </summary>
        public static void ClearPlatformOverride()
        {
            PlayerPrefs.DeleteKey(PLATFORM_OVERRIDE_KEY);
            PlayerPrefs.Save();
        }

        #endregion

        #region Environment Override

        /// <summary>
        /// Override the remote config environment
        /// Valid values: null, "production", "staging", "development"
        /// Persists in Editor and builds via PlayerPrefs
        /// </summary>
        public static string EnvironmentOverride
        {
            get
            {
                return PlayerPrefs.GetString(ENVIRONMENT_OVERRIDE_KEY, null);
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    PlayerPrefs.SetString(ENVIRONMENT_OVERRIDE_KEY, value);
                }
                else
                {
                    PlayerPrefs.DeleteKey(ENVIRONMENT_OVERRIDE_KEY);
                }
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Check if environment override is active
        /// </summary>
        public static bool HasEnvironmentOverride => !string.IsNullOrEmpty(EnvironmentOverride);

        /// <summary>
        /// Clear environment override
        /// </summary>
        public static void ClearEnvironmentOverride()
        {
            PlayerPrefs.DeleteKey(ENVIRONMENT_OVERRIDE_KEY);
            PlayerPrefs.Save();
        }

        #endregion

        #region AB Test Override

        public static string ForceAbLayerKey
        {
            get
            {
                var value = PlayerPrefs.GetString(FORCE_AB_LAYER_KEY, "");
                return string.IsNullOrEmpty(value) ? null : value;
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    PlayerPrefs.SetString(FORCE_AB_LAYER_KEY, value);
                }
                else
                {
                    PlayerPrefs.DeleteKey(FORCE_AB_LAYER_KEY);
                }
                PlayerPrefs.Save();
            }
        }

        public static string ForceAbTestKey
        {
            get
            {
                var value = PlayerPrefs.GetString(FORCE_AB_TEST_KEY, "");
                return string.IsNullOrEmpty(value) ? null : value;
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    PlayerPrefs.SetString(FORCE_AB_TEST_KEY, value);
                }
                else
                {
                    PlayerPrefs.DeleteKey(FORCE_AB_TEST_KEY);
                }
                PlayerPrefs.Save();
            }
        }

        public static string ForceAbVariantKey
        {
            get
            {
                var value = PlayerPrefs.GetString(FORCE_AB_VARIANT_KEY, "");
                return string.IsNullOrEmpty(value) ? null : value;
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    PlayerPrefs.SetString(FORCE_AB_VARIANT_KEY, value);
                }
                else
                {
                    PlayerPrefs.DeleteKey(FORCE_AB_VARIANT_KEY);
                }
                PlayerPrefs.Save();
            }
        }

        public static bool HasForcedAbOverride =>
            !string.IsNullOrEmpty(ForceAbTestKey) && !string.IsNullOrEmpty(ForceAbVariantKey);

        public static void SetForcedAbOverride(string layerKey, string testKey, string variantKey)
        {
            ForceAbLayerKey = layerKey;
            ForceAbTestKey = testKey;
            ForceAbVariantKey = variantKey;
        }

        public static void ClearForcedAbOverride()
        {
            PlayerPrefs.DeleteKey(FORCE_AB_LAYER_KEY);
            PlayerPrefs.DeleteKey(FORCE_AB_TEST_KEY);
            PlayerPrefs.DeleteKey(FORCE_AB_VARIANT_KEY);
            PlayerPrefs.Save();
        }

        #endregion

        /// <summary>
        /// Clear all debug overrides
        /// </summary>
        public static void ClearAll()
        {
            ClearPlatformOverride();
            ClearEnvironmentOverride();
            ClearForcedAbOverride();
        }
    }
}






