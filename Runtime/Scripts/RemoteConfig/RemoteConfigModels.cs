using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LvlUp.RemoteConfig
{
    /// <summary>
    /// Represents a single remote configuration value
    /// </summary>
    [Serializable]
    public class ConfigData
    {
        public string key;
        public object value; // Changed from string to object to handle any JSON type
        public string dataType;
        public bool isEnabled = true;
        public string environment;
        public long createdAt;
        public long updatedAt;

        /// <summary>
        /// Get the value as a string (serialized JSON if object/array)
        /// </summary>
        public string GetValueAsString()
        {
            if (value == null)
                return null;

            // If it's already a string, return it
            if (value is string str)
                return str;

            // If it's a JToken (Newtonsoft's JSON type), convert to string
            if (value is JToken jToken)
                return jToken.ToString(Formatting.None);

            // Otherwise serialize it
            return JsonConvert.SerializeObject(value);
        }

        /// <summary>
        /// Get the value as a typed object
        /// </summary>
        public T GetValue<T>()
        {
            if (value == null)
                return default(T);

            // If it's already the right type, return it
            if (value is T typed)
                return typed;

            // If it's a JToken, convert it
            if (value is JToken jToken)
                return jToken.ToObject<T>();

            // Try to convert from string
            if (value is string str)
                return JsonConvert.DeserializeObject<T>(str);

            // Try direct conversion
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return default(T);
            }
        }
    }


    /// <summary>
    /// Response wrapper for configs fetch
    /// </summary>
    [Serializable]
    public class ConfigsResponse
    {
        public ConfigData[] configs;
        public Dictionary<string, string> abTests;
        public ConfigDebugData debug;
        public long timestamp;
    }

    [Serializable]
    public class ConfigDebugData
    {
        public ForcedAbOverrideData forcedAbOverride;
    }

    [Serializable]
    public class ForcedAbOverrideData
    {
        public bool forced;
        public string layerKey;
        public string layerName;
        public string testKey;
        public string testName;
        public string testStatus;
        public string variantKey;
        public string variantName;
    }

    [Serializable]
    public class AbDebugCatalogResponse
    {
        public AbDebugLayer[] layers;
    }

    [Serializable]
    public class AbDebugLayer
    {
        public string id;
        public string key;
        public string name;
        public string environment;
        public AbDebugTest[] tests;
    }

    [Serializable]
    public class AbDebugTest
    {
        public string id;
        public string key;
        public string name;
        public string status;
        public int priority;
        public double allocationPercent;
        public AbDebugVariant[] variants;
    }

    [Serializable]
    public class AbDebugVariant
    {
        public string id;
        public string key;
        public string name;
        public double weightPercent;
        public bool isControl;
    }
    
    /// <summary>
    /// Game configuration data structure - generic key-value based
    /// </summary>
    [Serializable]
    public class ConfigsData
    {
        public Dictionary<string, object> configs;  // Dictionary of key-value pairs - can be any config values
        public ConfigMetadata metadata; // Metadata about the configs fetch
    }

    /// <summary>
    /// Config metadata
    /// </summary>
    [Serializable]
    public class ConfigMetadata
    {
        public string gameId;
        public string environment;
        public string fetchedAt;
        public string cacheUntil;
        public int totalConfigs;
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

    /// <summary>
    /// Helper for JSON serialization
    /// </summary>
    public static class SimpleJsonHelper
    {
        public static string ToJsonString(object obj)
        {
            if (obj == null)
                return "null";
            
            // Use Unity's JsonUtility if possible
            try
            {
                return JsonUtility.ToJson(obj);
            }
            catch
            {
                // Fallback to ToString for simple types
                return obj.ToString();
            }
        }
    }
}
