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

        /// <summary>
        /// Clear all debug overrides
        /// </summary>
        public static void ClearAll()
        {
            ClearPlatformOverride();
            ClearEnvironmentOverride();
        }
    }
}







