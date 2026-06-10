using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LvlUp.RemoteConfig;
using LvlUp;
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
        private Dictionary<string, string> _abTests = new Dictionary<string, string>();
        private List<AbDebugLayer> _abDebugCatalog = new List<AbDebugLayer>();
        private string _currentEnvironment = "production";
        private bool _currentAbTestsForced = false;
        private bool _isInitialized = false;
        private bool _isFetching = false;
        private bool _logConfigResult = false;

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
        public void Initialize(LvlUpHttpClient httpClient, string environment = "production", bool debugLogs = false, bool logConfigResult = false)
        {
            if (_isInitialized)
            {
                Debug.LogWarning("[LvlUp] RemoteConfigService already initialized");
                return;
            }

            _httpClient = httpClient;
            _currentEnvironment = environment;
            _logConfigResult = logConfigResult;

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
                // Check for environment override first (works in editor and builds)
                string environmentToUse = _currentEnvironment;
                if (LvlUpDebugSettings.HasEnvironmentOverride)
                {
                    environmentToUse = LvlUpDebugSettings.EnvironmentOverride;
                }
                
                string endpoint = $"config/configs?environment={environmentToUse}";
                
                // Use platform override if set (for editor testing), otherwise use context platform
                string platformToUse = _contextPlatform;
#if UNITY_EDITOR
                if (LvlUpDebugSettings.HasPlatformOverride)
                {
                    platformToUse = LvlUpDebugSettings.PlatformOverride;
                }
#endif
                
                endpoint = AppendQueryParameter(endpoint, "platform", platformToUse);
                endpoint = AppendQueryParameter(endpoint, "version", _contextVersion);
                endpoint = AppendQueryParameter(endpoint, "country", _contextCountry);
                endpoint = AppendQueryParameter(endpoint, "segment", _contextSegment);

                bool hasForcedAbOverride = LvlUpDebugSettings.HasForcedAbOverride;
                if (hasForcedAbOverride)
                {
                    endpoint = AppendQueryParameter(endpoint, "debug", "true");
                    endpoint = AppendQueryParameter(endpoint, "forceAbLayerKey", LvlUpDebugSettings.ForceAbLayerKey);
                    endpoint = AppendQueryParameter(endpoint, "forceAbTestKey", LvlUpDebugSettings.ForceAbTestKey);
                    endpoint = AppendQueryParameter(endpoint, "forceAbVariantKey", LvlUpDebugSettings.ForceAbVariantKey);
                }

                // Use GET request - backend identifies game from API key in header
                // Note: LvlUpHttpClient.Get<T> already wraps response in ApiResponse<T>
                yield return _httpClient.Get<ConfigsResponse>(endpoint, 
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
            if (_cacheService.TryLoadConfigs(_currentEnvironment, out var cachedConfigsResponse))
            {
                ProcessConfigsResponse(cachedConfigsResponse);
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

        public void FetchAbDebugCatalogAsync(MonoBehaviour coroutineRunner, Action<bool, List<AbDebugLayer>> onComplete = null)
        {
            if (!_isInitialized)
            {
                Debug.LogError("[LvlUp] RemoteConfigService not initialized");
                onComplete?.Invoke(false, new List<AbDebugLayer>());
                return;
            }

            coroutineRunner.StartCoroutine(FetchAbDebugCatalogInternal(onComplete));
        }

        private IEnumerator FetchAbDebugCatalogInternal(Action<bool, List<AbDebugLayer>> onComplete)
        {
            string environmentToUse = GetCurrentEnvironment();
            string endpoint = $"config/ab-tests/debug-catalog?environment={Uri.EscapeDataString(environmentToUse)}";

            bool fetchComplete = false;
            bool success = false;
            List<AbDebugLayer> layers = new List<AbDebugLayer>();

            yield return _httpClient.Get<AbDebugCatalogResponse>(endpoint,
                callback: (response) =>
                {
                    success = response.success;
                    if (response.success && response.data?.layers != null)
                    {
                        layers = new List<AbDebugLayer>(response.data.layers);
                        _abDebugCatalog = layers;
                    }
                    else
                    {
                        Debug.LogWarning($"[LvlUp] Failed to fetch A/B debug catalog: {response.error}");
                    }
                    fetchComplete = true;
                }
            );

            if (!fetchComplete)
                yield return new WaitForSeconds(0.1f);

            onComplete?.Invoke(success, layers);
        }

        private void ProcessConfigsResponse(ConfigsResponse configsResponse)
        {
            if (configsResponse?.configs == null)
            {
                Debug.LogWarning("[LvlUp] Invalid configs response");
                return;
            }

            // Convert array to list
            List<ConfigData> configList = new List<ConfigData>(configsResponse.configs);

            // Process the configs
            ProcessConfigs(configList, configsResponse, isFromCache: false);
        }


        private void ProcessConfigs(List<ConfigData> configs, ConfigsResponse configsResponse, bool isFromCache)
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

            _abTests = configsResponse?.abTests != null
                ? new Dictionary<string, string>(configsResponse.abTests)
                : new Dictionary<string, string>();
            _currentAbTestsForced = configsResponse?.debug?.forcedAbOverride != null &&
                configsResponse.debug.forcedAbOverride.forced;

            // Save to cache if from server
            if (!isFromCache && configsResponse != null && !_currentAbTestsForced)
            {
                _cacheService.SaveConfigs(configsResponse, _currentEnvironment);
            }

            // Fire event
            OnConfigsUpdated?.Invoke(new ConfigsUpdatedEvent
            {
                configs = configs,
                isFromCache = isFromCache,
                fetchedAt = DateTime.UtcNow.ToUnixTimestamp()
            });

            Debug.Log($"[LvlUp] Loaded {configs.Count} configs (from cache: {isFromCache})");

            if (_logConfigResult)
                LogConfigResult(configs, isFromCache);
        }

        /// <summary>
        /// Dump the full remote config result (every key/value and A/B assignment) to the console.
        /// Gated behind LvlUpConfig.logRemoteConfigResult.
        /// </summary>
        private void LogConfigResult(List<ConfigData> configs, bool isFromCache)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[LvlUp] Remote config result (environment: {_currentEnvironment}, from cache: {isFromCache}, forced A/B: {_currentAbTestsForced}):");

            sb.AppendLine($"  Configs ({configs.Count}):");
            foreach (var config in configs)
            {
                sb.AppendLine($"    - {config.key} = {config.GetValueAsString()}");
            }

            sb.AppendLine($"  A/B tests ({_abTests.Count}):");
            foreach (var kvp in _abTests)
            {
                sb.AppendLine($"    - {kvp.Key} = {kvp.Value}");
            }

            Debug.Log(sb.ToString());
        }

        private string AppendQueryParameter(string endpoint, string key, string value)
        {
            if (string.IsNullOrEmpty(value))
                return endpoint;

            return $"{endpoint}&{key}={Uri.EscapeDataString(value)}";
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
                string valueStr = config.GetValueAsString();
                if (int.TryParse(valueStr, out int result))
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

            string valueStr = config.GetValueAsString();
            return !string.IsNullOrEmpty(valueStr) ? valueStr : defaultValue;
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
                string valueStr = config.GetValueAsString();
                if (bool.TryParse(valueStr, out bool result))
                    return result;

                // Also check for 0/1 convention
                if (int.TryParse(valueStr, out int intValue))
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
                string valueStr = config.GetValueAsString();
                if (float.TryParse(valueStr, out float result))
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
                // Use the GetValue<T> method from ConfigData which handles
                // JToken, object, and string conversions properly
                return config.GetValue<T>() ?? defaultValue;
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
        /// Get current AB assignments returned by remote config.
        /// Forced debug overrides are included here for display/debugging.
        /// </summary>
        public Dictionary<string, string> GetCurrentAbTests()
        {
            return new Dictionary<string, string>(_abTests);
        }

        public IEnumerable<AbDebugLayer> GetAbDebugCatalog()
        {
            return _abDebugCatalog;
        }

        /// <summary>
        /// Get AB assignments that are safe to attach to analytics payloads.
        /// Forced debug overrides are intentionally excluded.
        /// </summary>
        public Dictionary<string, string> GetAnalyticsAbTests()
        {
            return _currentAbTestsForced ? null : new Dictionary<string, string>(_abTests);
        }

        public bool HasForcedAbOverrideActive()
        {
            return _currentAbTestsForced;
        }

        /// <summary>
        /// Clear all cached data
        /// </summary>
        public void ClearCache()
        {
            _cacheService.ClearCache();
            _configs.Clear();
            _abTests.Clear();
            _currentAbTestsForced = false;
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

        /// <summary>
        /// Get current environment
        /// </summary>
        public string GetCurrentEnvironment()
        {
            // Check for override first (from debug settings)
            if (!string.IsNullOrEmpty(LvlUpDebugSettings.EnvironmentOverride))
            {
                return LvlUpDebugSettings.EnvironmentOverride;
            }
            return _currentEnvironment;
        }

        #endregion
    }
}
