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
    /// Event data for tracking - includes all metadata from backend Event model
    /// Base class for all trackable events
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
        /// Virtual so subclasses can override to include their custom fields
        /// </summary>
        public virtual EventDataItem ToEventDataItem()
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
    }

    /// <summary>
    /// Level-specific event - extends LvlUpEvent with level funnel tracking
    /// Use this for level_start, level_complete, level_failed events
    /// </summary>
    [Serializable]
    public class LevelEvent : LvlUpEvent
    {
        // Level Funnel tracking
        public string levelFunnel;         // Level funnel name, e.g., "live_v1", "live_v2"
        public int? levelFunnelVersion;    // Level funnel version number, e.g., 1, 2, 3

        public LevelEvent(string eventName, Dictionary<string, object> properties = null) 
            : base(eventName, properties)
        {
        }

        /// <summary>
        /// Convert this LevelEvent to an EventDataItem for batch sending
        /// Overrides base to include level funnel data
        /// </summary>
        public override EventDataItem ToEventDataItem()
        {
            var item = base.ToEventDataItem();
            
            // Add level funnel data to properties if present
            if (!string.IsNullOrEmpty(this.levelFunnel))
            {
                item.properties = item.properties ?? new Dictionary<string, object>();
                item.properties["levelFunnel"] = this.levelFunnel;
                if (this.levelFunnelVersion.HasValue)
                {
                    item.properties["levelFunnelVersion"] = this.levelFunnelVersion.Value;
                }
            }
            
            return item;
        }
    }

    /// <summary>
    /// Set geographic location for an event
    /// </summary>
    [Serializable]
    public class BatchEventRequest
    {
        public string userId;
        public string sessionId;
        public List<EventDataItem> events;
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
    /// Checkpoint record request - extends LvlUpEvent to track checkpoint completion as a full event
    /// </summary>
    [Serializable]
    public class CheckpointRecordRequest : LvlUpEvent
    {
        // Checkpoint-specific fields
        public string userId;
        public string checkpointId;
        public Dictionary<string, object> metadata;
        
        // Constructor with event name for LvlUpEvent base
        public CheckpointRecordRequest() : base("checkpoint_recorded", null)
        {
        }
        
        /// <summary>
        /// Override to include checkpoint-specific fields in properties
        /// </summary>
        public override EventDataItem ToEventDataItem()
        {
            var item = base.ToEventDataItem();
            
            // Add checkpoint-specific fields to properties
            item.properties = item.properties ?? new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(this.userId))
                item.properties["userId"] = this.userId;
            if (!string.IsNullOrEmpty(this.checkpointId))
                item.properties["checkpointId"] = this.checkpointId;
            if (this.metadata != null && this.metadata.Count > 0)
                item.properties["metadata"] = this.metadata;
            
            return item;
        }
        
        // Note: All device/platform/app/geo metadata auto-populated by LvlUpEvent base class
        // This allows full analytics on WHERE and WHEN users complete checkpoints
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
    /// Session start request - extends LvlUpEvent to reuse auto-population logic
    /// </summary>
    [Serializable]
    public class SessionStartRequest : LvlUpEvent
    {
        // Session-specific fields
        public string userId;
        public string startTime;  // ISO date format
        
        // Constructor with event name for LvlUpEvent base
        public SessionStartRequest() : base("session_start", null)
        {
            this.startTime = DateTime.UtcNow.ToString("o");
        }
        
        /// <summary>
        /// Override to include session-specific fields in properties
        /// </summary>
        public override EventDataItem ToEventDataItem()
        {
            var item = base.ToEventDataItem();
            
            // Add session-specific fields to properties
            item.properties = item.properties ?? new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(this.userId))
                item.properties["userId"] = this.userId;
            if (!string.IsNullOrEmpty(this.startTime))
                item.properties["startTime"] = this.startTime;
            
            return item;
        }
        
        // Note: All device/platform/app/geo metadata auto-populated by LvlUpEvent base class
    }

    /// <summary>
    /// Session end request - extends LvlUpEvent to reuse auto-population logic
    /// </summary>
    [Serializable]
    public class SessionEndRequest : LvlUpEvent
    {
        // Session-specific fields
        public string sessionId;  // Used internally, not sent in body (goes in URL)
        public string endTime;    // Optional: ISO date format, defaults to current time on backend
        
        // Constructor with event name for LvlUpEvent base
        public SessionEndRequest() : base("session_end", null)
        {
            this.endTime = DateTime.UtcNow.ToString("o");
        }
        
        /// <summary>
        /// Override to include session-specific fields in properties
        /// </summary>
        public override EventDataItem ToEventDataItem()
        {
            var item = base.ToEventDataItem();
            
            // Add session-specific fields to properties
            item.properties = item.properties ?? new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(this.sessionId))
                item.properties["sessionId"] = this.sessionId;
            if (!string.IsNullOrEmpty(this.endTime))
                item.properties["endTime"] = this.endTime;
            
            return item;
        }
        
        // Note: All device/platform/app/geo metadata auto-populated by LvlUpEvent base class
        // Captures current state at session end (battery, connection, location changes, etc.)
    }
    
    /// <summary>
    /// Crash report data model - extends EventMetadata for consistency
    /// </summary>
    [Serializable]
    public class CrashReport : EventMetadata
    {
        // Crash-specific fields
        public string GameId; // Note: This contains the API key, used as game identifier
        public string UserId;
        public string SessionId;
        public string CrashType;
        public string Severity;
        public string Message;
        public string StackTrace;
        public string ExceptionType;
        
        // System info (not in EventMetadata)
        public long? MemoryUsage;
        public float? BatteryLevel;
        
        // Context
        public List<Breadcrumb> Breadcrumbs;
        public Dictionary<string, object> CustomData;
        public DateTime Timestamp;
        
        public CrashReport()
        {
            // Generate unique event ID
            this.eventUuid = Guid.NewGuid().ToString();
            this.clientTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            this.Timestamp = DateTime.UtcNow;
            
            // Auto-populate device and platform info
            PopulateDeviceInfo();
        }
        
        // Note: Device/Platform/App metadata inherited from EventMetadata:
        // - platform, osVersion, manufacturer, device, deviceId
        // - appVersion, appBuild, bundleId, engineVersion, sdkVersion
        // - connectionType, sessionNum, country, city, etc.
        // Auto-populated in constructor via PopulateDeviceInfo()
    }
    
    /// <summary>
    /// Breadcrumb for tracking user actions
    /// </summary>
    [Serializable]
    public class Breadcrumb
    {
        public string Timestamp; // ISO 8601 string format for proper JSON serialization
        public string Message;
        public string Type;
        public Dictionary<string, object> Data;
    }
}

