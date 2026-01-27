using System;
using System.Collections.Generic;

namespace LvlUp.Models
{
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