using UnityEngine;

namespace LvlUp.Utils
{
    /// <summary>
    /// Debug settings for LvlUp SDK - used for testing environment and platform overrides
    /// Persists settings using EditorPrefs (editor) or PlayerPrefs (runtime/mobile builds)
    /// </summary>
    public static class LvlUpDebugSettings
    {
        private const string PLATFORM_OVERRIDE_KEY = "LvlUp.Debug.PlatformOverride";
        private const string ENVIRONMENT_OVERRIDE_KEY = "LvlUp.Debug.EnvironmentOverride";

        /// <summary>
        /// Override the platform reported by the SDK
        /// Valid values: null, "ios", "android", "webgl", "windows", "macos", "linux"
        /// </summary>
        public static string PlatformOverride
        {
            get
            {

                var value = PlayerPrefs.GetString(PLATFORM_OVERRIDE_KEY, "");
                return string.IsNullOrEmpty(value) ? null : value;
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
        /// Override the remote config environment
        /// Valid values: null, "production", "staging", "development"
        /// </summary>
        public static string EnvironmentOverride
        {
            get
            {
                var value = PlayerPrefs.GetString(ENVIRONMENT_OVERRIDE_KEY, "");
                return string.IsNullOrEmpty(value) ? null : value;
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
        /// Check if platform override is active
        /// </summary>
        public static bool HasPlatformOverride => !string.IsNullOrEmpty(PlatformOverride);

        /// <summary>
        /// Check if environment override is active
        /// </summary>
        public static bool HasEnvironmentOverride => !string.IsNullOrEmpty(EnvironmentOverride);

        /// <summary>
        /// Clear platform override
        /// </summary>
        public static void ClearPlatformOverride()
        {
            PlayerPrefs.DeleteKey(PLATFORM_OVERRIDE_KEY);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Clear environment override
        /// </summary>
        public static void ClearEnvironmentOverride()
        {
            PlayerPrefs.DeleteKey(ENVIRONMENT_OVERRIDE_KEY);
            PlayerPrefs.Save();
        }
    }
}
