using System;
using System.Collections.Generic;
using UnityEngine;

namespace LvlUp.Models
{
    /// <summary>
    /// Base API response wrapper
    /// </summary>
    [Serializable]
    public class ApiResponse<T>
    {
        public bool success;
        public T data;
        public string error;
        public string message;
    }

    /// <summary>
    /// Generic API response for simple operations
    /// </summary>
    [Serializable]
    public class ApiResponse
    {
        public bool success;
        public string message;
        public string error;
    }

    /// <summary>
    /// User metadata for session tracking
    /// </summary>
    [Serializable]
    public class UserMetadata
    {
        public string deviceId;
        public string platform;
        public string version;
        public string country;
        public string language;
    }

    /// <summary>
    /// Session data
    /// </summary>
    [Serializable]
    public class SessionData
    {
        public string sessionId;
        public string userId;
        public string startTime;
        public string endTime;
        public int duration;
    }

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
        public string bundleId;
        public string engineVersion;
        public string sdkVersion;
        
        // Network & Additional
        public string connectionType;
        public int? sessionNum;
        public string appSignature;
        public string channelId;
        
        // Geographic location
        public string country;        // ISO country code, e.g., "US", "TR"
        public string countryCode;    // ISO 3166-1 alpha-2, e.g., "US"
        public string region;         // Region/State, e.g., "California", "Istanbul"
        public string city;           // City name, e.g., "San Francisco", "Istanbul"
        public float? latitude;       // Latitude coordinate
        public float? longitude;      // Longitude coordinate
        public string timezone;       // IANA timezone, e.g., "America/Los_Angeles"

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
            target.bundleId = this.bundleId;
            target.engineVersion = this.engineVersion;
            target.sdkVersion = this.sdkVersion;
            target.connectionType = this.connectionType;
            target.sessionNum = this.sessionNum;
            target.appSignature = this.appSignature;
            target.channelId = this.channelId;
            target.country = this.country;
            target.countryCode = this.countryCode;
            target.region = this.region;
            target.city = this.city;
            target.latitude = this.latitude;
            target.longitude = this.longitude;
            target.timezone = this.timezone;
        }
    }

    /// <summary>
    /// Event data for tracking - includes all metadata from backend Event model
    /// </summary>
    [Serializable]
    public class LvlUpEvent : EventMetadata
    {
        public string eventName;
        public Dictionary<string, object> properties;
        public string timestamp;

        public LvlUpEvent(string eventName, Dictionary<string, object> properties = null)
        {
            this.eventName = eventName;
            this.properties = properties ?? new Dictionary<string, object>();
            this.timestamp = DateTime.UtcNow.ToString("o");
            
            // Generate unique event ID
            this.eventUuid = Guid.NewGuid().ToString();
            this.clientTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            
            // Auto-populate device and platform info
            PopulateDeviceInfo();
        }

        /// <summary>
        /// Convert this LvlUpEvent to an EventDataItem for batch sending
        /// </summary>
        public EventDataItem ToEventDataItem()
        {
            var item = new EventDataItem
            {
                eventName = this.eventName,
                properties = this.properties,
                timestamp = this.timestamp
            };
            
            // Copy all metadata
            this.CopyTo(item);
            
            return item;
        }
        
        private void PopulateDeviceInfo()
        {
#if UNITY_EDITOR
            this.platform = "editor";
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
            this.bundleId = Application.identifier;
            this.engineVersion = $"unity {Application.unityVersion}";
            this.sdkVersion = "unity 1.0.0";
            
            // Connection type
            if (Application.internetReachability == NetworkReachability.NotReachable)
                this.connectionType = "offline";
            else if (Application.internetReachability == NetworkReachability.ReachableViaCarrierDataNetwork)
                this.connectionType = "wwan";
            else if (Application.internetReachability == NetworkReachability.ReachableViaLocalAreaNetwork)
                this.connectionType = "wifi";
            
#if UNITY_ANDROID
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
                
                // Try to get channel ID (installer package)
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject packageManager = currentActivity.Call<AndroidJavaObject>("getPackageManager"))
                {
                    this.channelId = packageManager.Call<string>("getInstallerPackageName", Application.identifier);
                }
            }
            catch (Exception)
            {
                // Silently fail if Android APIs are not available
                this.manufacturer = "Unknown";
            }
#elif UNITY_IOS
            this.manufacturer = "Apple";
            this.device = SystemInfo.deviceModel;
#endif
            
            // Note: Geographic location is NOT auto-populated here because it requires async network call
            // Use LvlUpManager.FetchAndPopulateGeoLocation() or manually set geo fields if needed
        }
        
        /// <summary>
        /// Manually set geographic location data
        /// </summary>
        public void SetGeoLocation(string country, string countryCode, string region, string city, 
            float? latitude = null, float? longitude = null, string timezone = null)
        {
            this.country = country;
            this.countryCode = countryCode;
            this.region = region;
            this.city = city;
            this.latitude = latitude;
            this.longitude = longitude;
            this.timezone = timezone;
        }
    }

    /// <summary>
    /// Batch event request
    /// </summary>
    [Serializable]
    public class BatchEventRequest
    {
        public string userId;
        public string sessionId;
        public List<EventDataItem> events;
        public DeviceInfo deviceInfo;
    }

    /// <summary>
    /// Event data item for batch requests - extends EventMetadata to avoid duplication
    /// </summary>
    [Serializable]
    public class EventDataItem : EventMetadata
    {
        public string eventName;
        public Dictionary<string, object> properties;
        public string timestamp;
    }

    /// <summary>
    /// Device info for backward compatibility - minimal fields only
    /// Event-level metadata in EventDataItem is the primary source
    /// </summary>
    [Serializable]
    public class DeviceInfo
    {
        // Core identifiers only (kept for backward compatibility with old API contracts)
        public string platform;
        public string version;
        public string deviceId;
    }

    /// <summary>
    /// Checkpoint data
    /// </summary>
    [Serializable]
    public class Checkpoint
    {
        public string id;
        public string name;
        public string description;
        public string type;
        public int order;
        public string[] tags;
        public string createdAt;
        public string gameId;
    }

    /// <summary>
    /// Checkpoint creation request
    /// </summary>
    [Serializable]
    public class CheckpointRequest
    {
        public string name;
        public string description;
        public string type;
        public int order;
        public string[] tags;
    }

    /// <summary>
    /// Checkpoint record request
    /// </summary>
    [Serializable]
    public class CheckpointRecordRequest
    {
        public string userId;
        public string checkpointId;
        public Dictionary<string, object> metadata;
    }

    /// <summary>
    /// Player journey progress
    /// </summary>
    [Serializable]
    public class PlayerJourneyProgress
    {
        public string userId;
        public int completedCheckpoints;
        public int totalCheckpoints;
        public float completionRate;
        public CheckpointCompletion[] checkpoints;
        public string lastCheckpointDate;
    }

    [Serializable]
    public class CheckpointCompletion
    {
        public string checkpointId;
        public string checkpointName;
        public string completedAt;
        public bool completed;
        public Dictionary<string, object> metadata;
    }


    /// <summary>
    /// Game data
    /// </summary>
    [Serializable]
    public class GameData
    {
        public string id;
        public string name;
        public string description;
        public string apiKey;
        public string createdAt;
        public string updatedAt;
        public GameStats stats;
    }

    [Serializable]
    public class GameStats
    {
        public int events;
        public int users;
        public int sessions;
        public int checkpoints;
        public int playerJourneys;
    }

    /// <summary>
    /// Analytics query parameters
    /// </summary>
    [Serializable]
    public class AnalyticsFilters
    {
        public string startDate;
        public string endDate;
        public string[] countries;
        public string[] platforms;
        public string[] versions;
        public int[] retentionDays;
        public string groupBy; // "day", "week", "month"
    }

    /// <summary>
    /// Session start request
    /// </summary>
    [Serializable]
    public class SessionStartRequest
    {
        public string userId;
        public string deviceId;
        public string platform;
        public string version;
        public string country;
        public string language;
        public string startTime;  // ISO date format
    }

    /// <summary>
    /// Session end request
    /// </summary>
    [Serializable]
    public class SessionEndRequest
    {
        public string sessionId;  // Used internally, not sent in body (goes in URL)
        public string endTime;    // Optional: ISO date format, defaults to current time on backend
    }

    /// <summary>
    /// Event tracking request
    /// </summary>
    [Serializable]
    public class EventTrackRequest
    {
        public string userId;
        public string sessionId;
        public string type;
        public Dictionary<string, object> data;
    }
}

