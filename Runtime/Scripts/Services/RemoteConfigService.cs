using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LvlUp.RemoteConfig;
using LvlUp.Models;
using LvlUp.Utils;

namespace LvlUp.Services
{
    /// <summary>
    /// Remote Config Service - Managed by LvlUpManager
    /// Handles config fetching, caching, and retrieval
    /// </summary>
    public class RemoteConfigService
    {
        // HTTP client (shared from LvlUpManager - has API key)
        private LvlUpHttpClient _httpClient;
        private RemoteConfigCacheService _cacheService;

        // State
        private Dictionary<string, ConfigData> _configs = new Dictionary<string, ConfigData>();
        private string _currentEnvironment = "production";
        private bool _isInitialized = false;
        private bool _isFetching = false;

        // Events
        public event Action<ConfigsUpdatedEvent> OnConfigsUpdated;

        // Retry logic
        private const int MAX_RETRIES = 3;
        private const float INITIAL_RETRY_DELAY = 1f;
        private const float MAX_RETRY_DELAY = 10f;

        #region Initialization

        /// <summary>
        /// Initialize Remote Config Service
        /// Uses API key from LvlUpHttpClient - backend identifies game from API key
        /// </summary>
        public void Initialize(LvlUpHttpClient httpClient, string environment = "production", bool debugLogs = false)
        {
            if (_isInitialized)
            {
                Debug.LogWarning("[LvlUp] RemoteConfigService already initialized");
                return;
            }

            _httpClient = httpClient;
            _currentEnvironment = environment;

            try
            {
                // Initialize cache service - uses a generic cache key since backend handles game identification
                _cacheService = new RemoteConfigCacheService("default");

                _isInitialized = true;

                if (debugLogs)
                    Debug.Log($"[LvlUp] RemoteConfigService initialized with environment: {environment}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[LvlUp] Failed to initialize RemoteConfig services: {e.Message}");
            }
        }

        // Context for rule evaluation
        private string _contextPlatform;
        private string _contextVersion;
        private string _contextCountry;
        private string _contextSegment;

        #endregion

        #region Context Management

        /// <summary>
        /// Set context for server-side rule evaluation
        /// Context is sent with fetch requests for rule evaluation
        /// </summary>
        public void SetContext(string platform = null, string version = null, string country = null, string segment = null)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[LvlUp] RemoteConfigService not initialized");
                return;
            }

            _contextPlatform = platform;
            _contextVersion = version;
            _contextCountry = country;
            _contextSegment = segment;
        }

        /// <summary>
        /// Set the environment for config fetching
        /// </summary>
        public void SetEnvironment(string environment)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[LvlUp] RemoteConfigService not initialized");
                return;
            }

            _currentEnvironment = environment;
        }

        #endregion

        #region Fetching Configs

        /// <summary>
        /// Fetch configs from server with retry logic
        /// Falls back to cache if server fails
        /// </summary>
        public void FetchAsync(MonoBehaviour coroutineRunner, Action<bool> onComplete = null)
        {
            if (!_isInitialized)
            {
                Debug.LogError("[LvlUp] RemoteConfigService not initialized");
                onComplete?.Invoke(false);
                return;
            }

            coroutineRunner.StartCoroutine(FetchAsyncInternal(onComplete));
        }

        private IEnumerator FetchAsyncInternal(Action<bool> onComplete)
        {
            if (_isFetching)
            {
                Debug.LogWarning("[LvlUp] Already fetching configs");
                yield break;
            }

            _isFetching = true;
            int retryCount = 0;
            float retryDelay = INITIAL_RETRY_DELAY;

            while (retryCount < MAX_RETRIES)
            {
                bool success = false;
                bool fetchComplete = false;

                // Build endpoint with query parameters for context
                string endpoint = $"config/configs?environment={_currentEnvironment}";
                if (!string.IsNullOrEmpty(_contextPlatform))
                    endpoint += $"&platform={_contextPlatform}";
                if (!string.IsNullOrEmpty(_contextVersion))
                    endpoint += $"&version={_contextVersion}";
                if (!string.IsNullOrEmpty(_contextCountry))
                    endpoint += $"&country={_contextCountry}";
                if (!string.IsNullOrEmpty(_contextSegment))
                    endpoint += $"&segment={_contextSegment}";

                // Use GET request - backend identifies game from API key in header
                // Note: LvlUpHttpClient.Get<T> already wraps response in ApiResponse<T>
                yield return _httpClient.Get<ConfigsData>(endpoint, 
                    callback: (response) =>
                    {
                        success = response.success;
                        if (response.success && response.data != null)
                        {
                            ProcessConfigsResponse(response.data);
                        }
                        else
                        {
                            Debug.LogWarning($"[LvlUp] Fetch failed (attempt {retryCount + 1}): {response.error}");
                        }
                        fetchComplete = true;
                    }
                );

                // Wait for fetch to complete
                if (!fetchComplete)
                    yield return new WaitForSeconds(0.1f);

                if (success)
                {
                    Debug.Log("[LvlUp] Configs fetched successfully");
                    _isFetching = false;
                    onComplete?.Invoke(true);
                    yield break;
                }

                retryCount++;
                if (retryCount < MAX_RETRIES)
                {
                    Debug.Log($"[LvlUp] Retrying in {retryDelay}s...");
                    yield return new WaitForSeconds(retryDelay);
                    retryDelay = Mathf.Min(retryDelay * 2f, MAX_RETRY_DELAY);
                }
            }

            // All retries failed, try to load from cache
            Debug.Log("[LvlUp] All fetch attempts failed, trying cache...");
            if (_cacheService.TryLoadConfigs(_currentEnvironment, out var cachedConfigsData))
            {
                ProcessConfigsResponse(cachedConfigsData);
                _isFetching = false;
                onComplete?.Invoke(true);
            }
            else
            {
                Debug.LogError("[LvlUp] Failed to fetch configs and no valid cache available");
                _isFetching = false;
                onComplete?.Invoke(false);
            }
        }

        private void ProcessConfigsResponse(ConfigsData configsData)
        {
            if (configsData?.configs == null)
            {
                Debug.LogWarning("[LvlUp] Invalid configs response");
                return;
            }

            // Convert the configs object (dictionary) to a list of ConfigData
            List<ConfigData> configList = ConvertConfigsToConfigDataList(configsData.configs);

            ProcessConfigs(configList, configsData, isFromCache: false);
        }

        private List<ConfigData> ConvertConfigsToConfigDataList(object configsObject)
        {
            var configList = new List<ConfigData>();

            if (configsObject == null)
                return configList;

            // Handle Dictionary<string, object> format from backend
            if (configsObject is Dictionary<string, object> configDict)
            {
                foreach (var kvp in configDict)
                {
                    configList.Add(new ConfigData
                    {
                        key = kvp.Key,
                        value = SimpleJson.ToJson(kvp.Value),
                        dataType = kvp.Value?.GetType().Name ?? "object",
                        isEnabled = true,
                        environment = _currentEnvironment,
                        createdAt = DateTime.UtcNow.ToUnixTimestamp(),
                        updatedAt = DateTime.UtcNow.ToUnixTimestamp(),
                    });
                }
            }
            else
            {
                // Fallback: try to reflect over object's fields
                try
                {
                    var fields = configsObject.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    
                    foreach (var field in fields)
                    {
                        var value = field.GetValue(configsObject);
                        configList.Add(new ConfigData
                        {
                            key = field.Name,
                            value = SimpleJson.ToJson(value),
                            dataType = value?.GetType().Name ?? "object",
                            isEnabled = true,
                            environment = _currentEnvironment,
                            createdAt = DateTime.UtcNow.ToUnixTimestamp(),
                            updatedAt = DateTime.UtcNow.ToUnixTimestamp(),
                        });
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[LvlUp] Could not convert configs object: {e.Message}");
                }
            }

            return configList;
        }

        private void ProcessConfigs(List<ConfigData> configs, ConfigsData configsData, bool isFromCache)
        {
            // Clear and rebuild dictionary
            _configs.Clear();
            foreach (var config in configs)
            {
                if (!string.IsNullOrEmpty(config.key))
                {
                    _configs[config.key] = config;
                }
            }

            // Save to cache if from server
            if (!isFromCache && configsData != null)
            {
                _cacheService.SaveConfigs(configsData, _currentEnvironment);
            }

            // Fire event
            OnConfigsUpdated?.Invoke(new ConfigsUpdatedEvent
            {
                configs = configs,
                isFromCache = isFromCache,
                fetchedAt = DateTime.UtcNow.ToUnixTimestamp()
            });

            Debug.Log($"[LvlUp] Loaded {configs.Count} configs (from cache: {isFromCache})");
        }

        #endregion

        #region Type-Safe Getters

        /// <summary>
        /// Get integer config value
        /// </summary>
        public int GetInt(string key, int defaultValue = 0)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[LvlUp] RemoteConfigService not initialized");
                return defaultValue;
            }

            if (!_configs.TryGetValue(key, out var config))
            {
                Debug.LogWarning($"[LvlUp] Config key not found: {key}");
                return defaultValue;
            }

            try
            {
                if (int.TryParse(config.value, out int result))
                    return result;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LvlUp] Failed to parse int for key '{key}': {e.Message}");
            }

            return defaultValue;
        }

        /// <summary>
        /// Get string config value
        /// </summary>
        public string GetString(string key, string defaultValue = "")
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[LvlUp] RemoteConfigService not initialized");
                return defaultValue;
            }

            if (!_configs.TryGetValue(key, out var config))
            {
                Debug.LogWarning($"[LvlUp] Config key not found: {key}");
                return defaultValue;
            }

            return !string.IsNullOrEmpty(config.value) ? config.value : defaultValue;
        }

        /// <summary>
        /// Get boolean config value
        /// </summary>
        public bool GetBool(string key, bool defaultValue = false)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[LvlUp] RemoteConfigService not initialized");
                return defaultValue;
            }

            if (!_configs.TryGetValue(key, out var config))
            {
                Debug.LogWarning($"[LvlUp] Config key not found: {key}");
                return defaultValue;
            }

            try
            {
                if (bool.TryParse(config.value, out bool result))
                    return result;

                // Also check for 0/1 convention
                if (int.TryParse(config.value, out int intValue))
                    return intValue != 0;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LvlUp] Failed to parse bool for key '{key}': {e.Message}");
            }

            return defaultValue;
        }

        /// <summary>
        /// Get float config value
        /// </summary>
        public float GetFloat(string key, float defaultValue = 0f)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[LvlUp] RemoteConfigService not initialized");
                return defaultValue;
            }

            if (!_configs.TryGetValue(key, out var config))
            {
                Debug.LogWarning($"[LvlUp] Config key not found: {key}");
                return defaultValue;
            }

            try
            {
                if (float.TryParse(config.value, out float result))
                    return result;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LvlUp] Failed to parse float for key '{key}': {e.Message}");
            }

            return defaultValue;
        }

        /// <summary>
        /// Get JSON config value and deserialize to type T
        /// </summary>
        public T GetJson<T>(string key, T defaultValue = null) where T : class
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[LvlUp] RemoteConfigService not initialized");
                return defaultValue;
            }

            if (!_configs.TryGetValue(key, out var config))
            {
                Debug.LogWarning($"[LvlUp] Config key not found: {key}");
                return defaultValue;
            }

            try
            {
                if (string.IsNullOrEmpty(config.value))
                    return defaultValue;

                return JsonUtility.FromJson<T>(config.value);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LvlUp] Failed to parse JSON for key '{key}': {e.Message}");
            }

            return defaultValue;
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Check if a config key exists
        /// </summary>
        public bool HasKey(string key)
        {
            return _configs.ContainsKey(key);
        }

        /// <summary>
        /// Get all loaded config keys
        /// </summary>
        public IEnumerable<string> GetAllKeys()
        {
            return _configs.Keys;
        }

        /// <summary>
        /// Get all loaded configs
        /// </summary>
        public IEnumerable<ConfigData> GetAllConfigs()
        {
            return _configs.Values;
        }

        /// <summary>
        /// Clear all cached data
        /// </summary>
        public void ClearCache()
        {
            _cacheService.ClearCache();
            _configs.Clear();
            Debug.Log("[LvlUp] Remote config cache cleared");
        }

        /// <summary>
        /// Check if configs are from cache
        /// </summary>
        public bool IsCached()
        {
            return _cacheService.IsValidCache(_currentEnvironment);
        }

        /// <summary>
        /// Get cache age in milliseconds
        /// </summary>
        public long GetCacheAgeMs()
        {
            return _cacheService.GetCacheAgeMs(_currentEnvironment);
        }

        /// <summary>
        /// Check if service is initialized
        /// </summary>
        public bool IsInitialized => _isInitialized;

        #endregion
    }
}

