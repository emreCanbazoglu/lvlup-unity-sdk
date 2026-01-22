using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LvlUp.Utils;

namespace LvlUp.RemoteConfig
{
    /// <summary>
    /// Main manager for Remote Config functionality
    /// Singleton pattern for easy access throughout the application
    /// </summary>
    public class RemoteConfigManager : MonoBehaviour
    {
        private static RemoteConfigManager _instance;
        public static RemoteConfigManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("RemoteConfigManager");
                    _instance = go.AddComponent<RemoteConfigManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // Services
        private RemoteConfigService _configService;
        private RemoteConfigCacheService _cacheService;

        // State
        private Dictionary<string, ConfigData> _configs = new Dictionary<string, ConfigData>();
        private string _gameId;
        private string _baseUrl;
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
        /// Initialize Remote Config Manager
        /// </summary>
        public static void Initialize(string gameId, string baseUrl, string environment = "production", Action<bool, string> onComplete = null)
        {
            Instance._Initialize(gameId, baseUrl, environment, onComplete);
        }

        private void _Initialize(string gameId, string baseUrl, string environment, Action<bool, string> onComplete)
        {
            if (_isInitialized)
            {
                Debug.LogWarning("[LvlUp] RemoteConfigManager already initialized");
                onComplete?.Invoke(false, "Already initialized");
                return;
            }

            try
            {
                _gameId = gameId;
                _baseUrl = baseUrl;
                _currentEnvironment = environment;

                // Initialize services
                _configService = new RemoteConfigService(_baseUrl, _gameId, debugLogs: true);
                _cacheService = new RemoteConfigCacheService(_gameId);

                _isInitialized = true;
                Debug.Log($"[LvlUp] RemoteConfigManager initialized for game: {_gameId}");
                onComplete?.Invoke(true, "Initialized successfully");
            }
            catch (Exception e)
            {
                Debug.LogError($"[LvlUp] Failed to initialize RemoteConfigManager: {e.Message}");
                onComplete?.Invoke(false, e.Message);
            }
        }

        #endregion

        #region Context Management

        /// <summary>
        /// Set context for server-side rule evaluation
        /// </summary>
        public void SetContext(string platform = null, string version = null, string country = null, string segment = null)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[LvlUp] RemoteConfigManager not initialized");
                return;
            }

            _configService.SetContext(platform, version, country, segment);
            Debug.Log($"[LvlUp] Context set - Platform: {platform}, Version: {version}, Country: {country}, Segment: {segment}");
        }

        /// <summary>
        /// Set the environment for config fetching
        /// </summary>
        public void SetEnvironment(string environment)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[LvlUp] RemoteConfigManager not initialized");
                return;
            }

            _currentEnvironment = environment;
            Debug.Log($"[LvlUp] Environment set to: {environment}");
        }

        #endregion

        #region Fetching Configs

        /// <summary>
        /// Fetch configs from server with retry logic
        /// Falls back to cache if server fails
        /// </summary>
        public void FetchAsync(Action<bool> onComplete = null)
        {
            if (!_isInitialized)
            {
                Debug.LogError("[LvlUp] RemoteConfigManager not initialized");
                onComplete?.Invoke(false);
                return;
            }

            StartCoroutine(FetchAsyncInternal(onComplete));
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

                yield return _configService.FetchConfigs(
                    _currentEnvironment,
                    onSuccess: (response) =>
                    {
                        success = true;
                        ProcessConfigsResponse(response);
                        fetchComplete = true;
                    },
                    onError: (error) =>
                    {
                        Debug.LogWarning($"[LvlUp] Fetch failed (attempt {retryCount + 1}): {error}");
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
            if (_cacheService.TryLoadConfigs(_currentEnvironment, out var cachedConfigs))
            {
                ProcessConfigs(cachedConfigs, isFromCache: true);
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

        private void ProcessConfigsResponse(ConfigsResponse response)
        {
            if (response?.configs == null)
            {
                Debug.LogWarning("[LvlUp] Invalid configs response");
                return;
            }

            List<ConfigData> configList = new List<ConfigData>(response.configs);
            ProcessConfigs(configList, isFromCache: false);
        }

        private void ProcessConfigs(List<ConfigData> configs, bool isFromCache)
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
            if (!isFromCache)
            {
                _cacheService.SaveConfigs(configs, _currentEnvironment);
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
                Debug.LogWarning("[LvlUp] RemoteConfigManager not initialized");
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
                Debug.LogWarning("[LvlUp] RemoteConfigManager not initialized");
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
                Debug.LogWarning("[LvlUp] RemoteConfigManager not initialized");
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
                Debug.LogWarning("[LvlUp] RemoteConfigManager not initialized");
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
                Debug.LogWarning("[LvlUp] RemoteConfigManager not initialized");
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

                return SimpleJson.FromJson<T>(config.value);
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
            return _cacheService.GetCacheAgeMs();
        }

        #endregion
    }
}

