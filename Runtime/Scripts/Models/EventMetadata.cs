using System;
using UnityEngine;

namespace LvlUp.Models
{
    /// <summary>
    /// Base class for event metadata shared between LvlUpEvent and EventDataItem
    /// </summary>
    [Serializable]
    public class EventMetadata
    {
        // Event metadata
        public string eventUuid;
        public long? clientTs;
        
        // Device & Platform info
        public string platform;
        public string osVersion;
        public string manufacturer;
        public string device;
        public string deviceId;
        
        // App info
        public string appVersion;
        public string appBuild;
        public string sdkVersion;
        
        // Network & Additional
        public string connectionType;
        public int? sessionNum;
        
        // Geographic location
        public string countryCode;    // ISO 3166-1 alpha-2, e.g., "US"


        /// <summary>
        /// Copy metadata from this object to another EventMetadata object
        /// </summary>
        public void CopyTo(EventMetadata target)
        {
            target.eventUuid = this.eventUuid;
            target.clientTs = this.clientTs;
            target.platform = this.platform;
            target.osVersion = this.osVersion;
            target.manufacturer = this.manufacturer;
            target.device = this.device;
            target.deviceId = this.deviceId;
            target.appVersion = this.appVersion;
            target.appBuild = this.appBuild;
            target.sdkVersion = this.sdkVersion;
            target.connectionType = this.connectionType;
            target.sessionNum = this.sessionNum;
            target.countryCode = this.countryCode;
        }

        /// <summary>
        /// Auto-populate device and platform information
        /// </summary>
        public void PopulateDeviceInfo()
        {
#if UNITY_EDITOR
            // Check for platform override in editor
            if (LvlUpDebugSettings.HasPlatformOverride)
            {
                this.platform = LvlUpDebugSettings.PlatformOverride;
            }
            else
            {
                this.platform = "editor";
            }
#elif UNITY_ANDROID
            this.platform = "android";
#elif UNITY_IOS
            this.platform = "ios";
#elif UNITY_WEBGL
            this.platform = "webgl";
#elif UNITY_STANDALONE_WIN
            this.platform = "windows";
#elif UNITY_STANDALONE_OSX
            this.platform = "macos";
#elif UNITY_STANDALONE_LINUX
            this.platform = "linux";
#else
            this.platform = Application.platform.ToString().ToLower();
#endif

            this.osVersion = SystemInfo.operatingSystem;
            this.device = SystemInfo.deviceModel;
            this.deviceId = SystemInfo.deviceUniqueIdentifier;
            this.appVersion = Application.version;
            this.sdkVersion = "unity 1.0.0";
            
            // Connection type
            if (Application.internetReachability == NetworkReachability.NotReachable)
                this.connectionType = "offline";
            else if (Application.internetReachability == NetworkReachability.ReachableViaCarrierDataNetwork)
                this.connectionType = "wwan";
            else if (Application.internetReachability == NetworkReachability.ReachableViaLocalAreaNetwork)
                this.connectionType = "wifi";
            
#if UNITY_ANDROID && !UNITY_EDITOR
            // Android-specific metadata
            try
            {
                using (AndroidJavaClass buildClass = new AndroidJavaClass("android.os.Build"))
                {
                    this.manufacturer = buildClass.GetStatic<string>("MANUFACTURER");
                    this.device = buildClass.GetStatic<string>("MODEL");
                }
                
                // Try to get app build number
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject packageManager = currentActivity.Call<AndroidJavaObject>("getPackageManager"))
                using (AndroidJavaObject packageInfo = packageManager.Call<AndroidJavaObject>("getPackageInfo", Application.identifier, 0))
                {
                    this.appBuild = packageInfo.Get<int>("versionCode").ToString();
                }
            }
            catch (Exception)
            {
                // Silently fail if Android APIs are not available
                this.manufacturer = "Unknown";
            }
#elif UNITY_IOS && !UNITY_EDITOR
            this.manufacturer = "Apple";
            this.device = SystemInfo.deviceModel;
            
            // Get app build number from Info.plist (CFBundleVersion)
            try
            {
                string buildNumber = LvlUp.Plugins.iOSBuildNumber.GetBuildNumber();
                if (!string.IsNullOrEmpty(buildNumber))
                {
                    this.appBuild = buildNumber;
                }
            }
            catch (Exception)
            {
                // Silently fail if not available
            }
#elif UNITY_EDITOR
            this.manufacturer = "Editor";
            // In editor, try to get build number from PlayerSettings based on build target
            try
            {
#if UNITY_ANDROID
                this.appBuild = UnityEditor.PlayerSettings.Android.bundleVersionCode.ToString();
#elif UNITY_IOS
                this.appBuild = UnityEditor.PlayerSettings.iOS.buildNumber;
#else
                this.appBuild = "1"; // Default for editor
#endif
            }
            catch (Exception)
            {
                this.appBuild = "1"; // Fallback
            }
#endif
            
            // Note: Geographic location (countryCode) is NOT auto-populated here because it requires async network call
            // Use LvlUpManager.FetchAndPopulateGeoLocation() or manually set countryCode if needed
        }

        /// <summary>
        /// Manually set geographic location data (only countryCode is used)
        /// </summary>
        public void SetGeoLocation(string countryCode)
        {
            this.countryCode = countryCode;
        }
    }
}