using System;
using System.Collections.Generic;
using UnityEngine;
using LvlUp.Utils;

namespace LvlUp.RemoteConfig
{
    /// <summary>
    /// Handles caching of remote configs using PlayerPrefs
    /// </summary>
    public class RemoteConfigCacheService
    {
        private const string CACHE_KEY_PREFIX = "LvlUp_RemoteConfig_";
        private const string CACHE_TIMESTAMP_SUFFIX = "_Timestamp";
        private const string CACHE_ENVIRONMENT_SUFFIX = "_Environment";
        private const long CACHE_TTL_MS = 5 * 60 * 1000; // 5 minutes in milliseconds

        private string _gameId;

        public RemoteConfigCacheService(string gameId)
        {
            _gameId = gameId;
        }

        /// <summary>
        /// Save configs to cache with timestamp
        /// </summary>
        public void SaveConfigs(List<ConfigData> configs, string environment)
        {
            try
            {
                // Serialize configs to JSON
                ConfigsData configsData = new ConfigsData
                {
                    configs = configs,
                    fetchedAt = DateTime.UtcNow.ToUnixTimestamp(),
                    environment = environment
                };

                string json = SimpleJson.ToJson(configsData);
                string cacheKey = GetCacheKey();

                // Save data
                PlayerPrefs.SetString(cacheKey, json);
                PlayerPrefs.SetLong(GetTimestampKey(), DateTime.UtcNow.ToUnixTimestamp());
                PlayerPrefs.SetString(GetEnvironmentKey(), environment);
                PlayerPrefs.Save();

                Debug.Log($"[LvlUp] Cached {configs.Count} configs for environment '{environment}'");
            }
            catch (Exception e)
            {
                Debug.LogError($"[LvlUp] Failed to save configs to cache: {e.Message}");
            }
        }

        /// <summary>
        /// Load configs from cache if valid
        /// </summary>
        public bool TryLoadConfigs(string environment, out List<ConfigData> configs)
        {
            configs = new List<ConfigData>();

            try
            {
                string cacheKey = GetCacheKey();

                if (!PlayerPrefs.HasKey(cacheKey))
                {
                    Debug.Log("[LvlUp] No cached configs found");
                    return false;
                }

                // Check if cache is expired
                if (!IsValidCache(environment))
                {
                    Debug.Log("[LvlUp] Cached configs expired");
                    ClearCache();
                    return false;
                }

                // Load and deserialize
                string json = PlayerPrefs.GetString(cacheKey);
                ConfigsData configsData = SimpleJson.FromJson<ConfigsData>(json);

                if (configsData?.configs != null)
                {
                    configs = configsData.configs;
                    Debug.Log($"[LvlUp] Loaded {configs.Count} cached configs");
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[LvlUp] Failed to load configs from cache: {e.Message}");
                ClearCache();
            }

            return false;
        }

        /// <summary>
        /// Check if cache is still valid (not expired and matches environment)
        /// </summary>
        public bool IsValidCache(string environment)
        {
            try
            {
                // Check if timestamp exists
                long timestamp = PlayerPrefs.GetLong(GetTimestampKey(), 0);
                if (timestamp == 0)
                    return false;

                // Check if environment matches
                string cachedEnvironment = PlayerPrefs.GetString(GetEnvironmentKey(), "");
                if (cachedEnvironment != environment)
                    return false;

                // Check if expired (5-minute TTL)
                long currentTime = DateTime.UtcNow.ToUnixTimestamp();
                long ageMs = (currentTime - timestamp) * 1000;

                return ageMs < CACHE_TTL_MS;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Clear all cached configs
        /// </summary>
        public void ClearCache()
        {
            try
            {
                PlayerPrefs.DeleteKey(GetCacheKey());
                PlayerPrefs.DeleteKey(GetTimestampKey());
                PlayerPrefs.DeleteKey(GetEnvironmentKey());
                PlayerPrefs.Save();
                Debug.Log("[LvlUp] Cache cleared");
            }
            catch (Exception e)
            {
                Debug.LogError($"[LvlUp] Failed to clear cache: {e.Message}");
            }
        }

        /// <summary>
        /// Get cache age in milliseconds
        /// </summary>
        public long GetCacheAgeMs()
        {
            try
            {
                long timestamp = PlayerPrefs.GetLong(GetTimestampKey(), 0);
                if (timestamp == 0)
                    return -1;

                long currentTime = DateTime.UtcNow.ToUnixTimestamp();
                return (currentTime - timestamp) * 1000;
            }
            catch
            {
                return -1;
            }
        }

        private string GetCacheKey() => $"{CACHE_KEY_PREFIX}{_gameId}";
        private string GetTimestampKey() => $"{CACHE_KEY_PREFIX}{_gameId}{CACHE_TIMESTAMP_SUFFIX}";
        private string GetEnvironmentKey() => $"{CACHE_KEY_PREFIX}{_gameId}{CACHE_ENVIRONMENT_SUFFIX}";

        /// <summary>
        /// Internal class for cache serialization
        /// </summary>
        [Serializable]
        private class ConfigsData
        {
            public List<ConfigData> configs;
            public long fetchedAt;
            public string environment;
        }
    }

    /// <summary>
    /// Helper extension methods
    /// </summary>
    public static class UnixTimeExtensions
    {
        public static long ToUnixTimestamp(this DateTime dateTime)
        {
            return (long)(dateTime.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }

        public static DateTime FromUnixTimestamp(this long timestamp)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(timestamp);
        }
    }
}

