using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using LvlUp.Utils;

namespace LvlUp.RemoteConfig
{
    /// <summary>
    /// Handles caching of remote configs using persistent file storage
    /// Caches configs as JSON files in the persistent data path for robustness
    /// </summary>
    public class RemoteConfigCacheService
    {
        private const long CACHE_TTL_MS = 5 * 60 * 1000; // 5 minutes in milliseconds
        private const string CACHE_FOLDER = "RemoteConfigs";
        private const string CACHE_FILE_EXTENSION = ".json";

        private string _gameId;
        private string _cacheDirectory;

        public RemoteConfigCacheService(string gameId)
        {
            _gameId = gameId;
            _cacheDirectory = Path.Combine(Application.persistentDataPath, CACHE_FOLDER);
            
            // Create cache directory if it doesn't exist
            try
            {
                if (!Directory.Exists(_cacheDirectory))
                {
                    Directory.CreateDirectory(_cacheDirectory);
                    Debug.Log($"[LvlUp] Created cache directory: {_cacheDirectory}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[LvlUp] Failed to create cache directory: {e.Message}");
            }
        }

        /// <summary>
        /// Save configs to cache file with timestamp
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
                string cacheFilePath = GetCacheFilePath(environment);

                // Write to file
                File.WriteAllText(cacheFilePath, json);

                Debug.Log($"[LvlUp] Cached {configs.Count} configs for environment '{environment}' at {cacheFilePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[LvlUp] Failed to save configs to cache file: {e.Message}");
            }
        }

        /// <summary>
        /// Load configs from cache file if valid
        /// </summary>
        public bool TryLoadConfigs(string environment, out List<ConfigData> configs)
        {
            configs = new List<ConfigData>();

            try
            {
                string cacheFilePath = GetCacheFilePath(environment);

                if (!File.Exists(cacheFilePath))
                {
                    Debug.Log("[LvlUp] No cached configs file found");
                    return false;
                }

                // Check if cache is expired
                if (!IsValidCache(environment))
                {
                    Debug.Log("[LvlUp] Cached configs expired");
                    ClearCache(environment);
                    return false;
                }

                // Load and deserialize
                string json = File.ReadAllText(cacheFilePath);
                ConfigsData configsData = SimpleJson.FromJson<ConfigsData>(json);

                if (configsData?.configs != null)
                {
                    configs = configsData.configs;
                    Debug.Log($"[LvlUp] Loaded {configs.Count} cached configs from {cacheFilePath}");
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[LvlUp] Failed to load configs from cache file: {e.Message}");
                ClearCache(environment);
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
                string cacheFilePath = GetCacheFilePath(environment);

                if (!File.Exists(cacheFilePath))
                    return false;

                // Get file modification time
                FileInfo fileInfo = new FileInfo(cacheFilePath);
                DateTime lastModified = fileInfo.LastWriteTimeUtc;
                
                // Check if expired (5-minute TTL)
                long currentTime = DateTime.UtcNow.ToUnixTimestamp();
                long fileTimestamp = ((DateTimeOffset)lastModified).ToUnixTimeSeconds();
                long ageMs = (currentTime - fileTimestamp) * 1000;

                return ageMs < CACHE_TTL_MS;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Clear cached configs for specific environment
        /// </summary>
        public void ClearCache(string environment = null)
        {
            try
            {
                if (environment != null)
                {
                    // Clear specific environment cache
                    string cacheFilePath = GetCacheFilePath(environment);
                    if (File.Exists(cacheFilePath))
                    {
                        File.Delete(cacheFilePath);
                        Debug.Log($"[LvlUp] Cache cleared for environment '{environment}'");
                    }
                }
                else
                {
                    // Clear all cache files for this game
                    string[] cacheFiles = Directory.GetFiles(_cacheDirectory, GetCacheFilePattern());
                    foreach (string file in cacheFiles)
                    {
                        File.Delete(file);
                    }
                    Debug.Log("[LvlUp] All cache files cleared");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[LvlUp] Failed to clear cache: {e.Message}");
            }
        }

        /// <summary>
        /// Get cache age in milliseconds
        /// </summary>
        public long GetCacheAgeMs(string environment)
        {
            try
            {
                string cacheFilePath = GetCacheFilePath(environment);

                if (!File.Exists(cacheFilePath))
                    return -1;

                FileInfo fileInfo = new FileInfo(cacheFilePath);
                DateTime lastModified = fileInfo.LastWriteTimeUtc;
                
                long currentTime = DateTime.UtcNow.ToUnixTimestamp();
                long fileTimestamp = ((DateTimeOffset)lastModified).ToUnixTimeSeconds();
                return (currentTime - fileTimestamp) * 1000;
            }
            catch
            {
                return -1;
            }
        }

        private string GetCacheFilePath(string environment)
        {
            string fileName = $"{_gameId}_{environment}{CACHE_FILE_EXTENSION}";
            return Path.Combine(_cacheDirectory, fileName);
        }

        private string GetCacheFilePattern()
        {
            return $"{_gameId}_*{CACHE_FILE_EXTENSION}";
        }

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

