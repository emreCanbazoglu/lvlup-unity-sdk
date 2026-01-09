using System.Runtime.InteropServices;
using UnityEngine;

namespace LvlUp.Plugins
{
    /// <summary>
    /// Native iOS plugin to retrieve build number from Info.plist
    /// </summary>
    public static class iOSBuildNumber
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern string _GetBuildNumber();
#endif

        /// <summary>
        /// Get the iOS build number (CFBundleVersion) from Info.plist
        /// Returns null if not available or not on iOS platform
        /// </summary>
        public static string GetBuildNumber()
        {
#if UNITY_IOS && !UNITY_EDITOR
            try
            {
                return _GetBuildNumber();
            }
            catch (System.Exception)
            {
                return null;
            }
#else
            return null;
#endif
        }
    }
}

