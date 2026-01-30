using System;
using UnityEngine;

namespace LvlUp.Utils
{
    public static class DeviceInfo
    {
        public static string GetAppBuild() 
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Android-specific metadata
            try
            {
                // Try to get app build number
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject packageManager = currentActivity.Call<AndroidJavaObject>("getPackageManager"))
                using (AndroidJavaObject packageInfo = packageManager.Call<AndroidJavaObject>("getPackageInfo", Application.identifier, 0))
                {
                    return packageInfo.Get<int>("versionCode").ToString();
                }
            }
            catch (Exception)
            {
                return "Unknown";
            }
#elif UNITY_IOS && !UNITY_EDITOR
            // Get app build number from Info.plist (CFBundleVersion)
            try
            {
                string buildNumber = LvlUp.Plugins.iOSBuildNumber.GetBuildNumber();
                if (!string.IsNullOrEmpty(buildNumber))
                {
                    return buildNumber;
                }
            }
            catch (Exception)
            {
                // Silently fail if not available
            }
#elif UNITY_EDITOR
            // In editor, try to get build number from PlayerSettings based on build target
            try
            {
#if UNITY_ANDROID
                return UnityEditor.PlayerSettings.Android.bundleVersionCode.ToString();
#elif UNITY_IOS
                return UnityEditor.PlayerSettings.iOS.buildNumber;
#else
                return "Unknown" // Default for editor
#endif
            }
            catch (Exception)
            {
                return "Unknown"; // Fallback
            }
#endif
        }
        
    }
}