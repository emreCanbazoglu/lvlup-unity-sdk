using System;
using System.Collections.Generic;
using UnityEngine;

namespace LvlUp.RemoteConfig
{
    /// <summary>
    /// Represents a single remote configuration value
    /// </summary>
    [Serializable]
    public class ConfigData
    {
        public string key;
        public string value;
        public string dataType;
        public bool isEnabled = true;
        public string environment;
        public long createdAt;
        public long updatedAt;
    }

    /// <summary>
    /// Response wrapper for configs fetch
    /// </summary>
    [Serializable]
    public class ConfigsResponse
    {
        public ConfigData[] configs;
        public long timestamp;
    }

    /// <summary>
    /// Container for all fetched configs
    /// </summary>
    [Serializable]
    public class RemoteConfigCache
    {
        public List<ConfigData> configs = new List<ConfigData>();
        public long fetchedAt;
        public string environment;
    }

    /// <summary>
    /// Fetch request parameters
    /// </summary>
    public class ConfigFetchParams
    {
        public string gameId;
        public string environment = "production";
        public string platform;
        public string version;
        public string country;
        public string segment;
    }

    /// <summary>
    /// Event fired when configs are updated
    /// </summary>
    public class ConfigsUpdatedEvent
    {
        public List<ConfigData> configs;
        public bool isFromCache;
        public long fetchedAt;
    }
}

