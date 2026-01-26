#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LvlUp.Utils
{
    /// <summary>
    /// Debug settings for LvlUp SDK - used in editor only for testing
    /// Persists settings across play mode transitions using EditorPrefs
    /// </summary>
    public static class LvlUpDebugSettings
    {
        private const string PLATFORM_OVERRIDE_KEY = "LvlUp.Debug.PlatformOverride";

        /// <summary>
        /// Override the platform reported by the SDK (editor only)
        /// Valid values: null, "ios", "android", "webgl", "windows", "macos", "linux"
        /// </summary>
        public static string PlatformOverride
        {
            get
            {
#if UNITY_EDITOR
                return EditorPrefs.GetString(PLATFORM_OVERRIDE_KEY, null);
#else
                return null;
#endif
            }
            set
            {
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(value))
                {
                    EditorPrefs.SetString(PLATFORM_OVERRIDE_KEY, value);
                }
                else
                {
                    EditorPrefs.DeleteKey(PLATFORM_OVERRIDE_KEY);
                }
#endif
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
#if UNITY_EDITOR
            EditorPrefs.DeleteKey(PLATFORM_OVERRIDE_KEY);
#endif
        }
    }
}

