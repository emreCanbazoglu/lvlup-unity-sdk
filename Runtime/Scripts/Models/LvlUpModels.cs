using System;
using System.Collections.Generic;

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
    /// Event data for tracking
    /// </summary>
    [Serializable]
    public class LvlUpEvent
    {
        public string eventName;
        public Dictionary<string, object> properties;
        public string timestamp;

        public LvlUpEvent(string eventName, Dictionary<string, object> properties = null)
        {
            this.eventName = eventName;
            this.properties = properties ?? new Dictionary<string, object>();
            this.timestamp = DateTime.UtcNow.ToString("o");
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

    [Serializable]
    public class EventDataItem
    {
        public string eventName;
        public Dictionary<string, object> properties;
        public string timestamp;
    }

    [Serializable]
    public class DeviceInfo
    {
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
    }

    /// <summary>
    /// Session end request
    /// </summary>
    [Serializable]
    public class SessionEndRequest
    {
        public string sessionId;
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

