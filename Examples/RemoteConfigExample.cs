using System;
using UnityEngine;
using LvlUp;
using LvlUp.Services;

namespace LvlUp.Examples
{
    /// <summary>
    /// Example demonstrating Remote Config SDK usage
    /// Remote Config is now managed by LvlUpManager as a service
    /// </summary>
    public class RemoteConfigExample : MonoBehaviour
    {
        private void Start()
        {
            // Initialize main LvlUp SDK first
            LvlUpManager.Initialize(
                apiKey: "your_api_key",
                baseUrl: "https://api.lvlup.com",
                onComplete: (success, message) =>
                {
                    if (success)
                    {
                        Debug.Log("LvlUp SDK initialized");
                        InitializeRemoteConfig();
                    }
                }
            );
        }

        private void InitializeRemoteConfig()
        {
            // Initialize Remote Config service through LvlUpManager
            LvlUpManager.InitializeRemoteConfig(
                gameId: "your_game_id",
                environment: "production"
            );

            // Set context for server-side rule evaluation
            LvlUpManager.SetRemoteConfigContext(
                platform: "iOS",
                version: Application.version,
                country: "US",
                segment: "new_users"
            );

            // Fetch configs
            FetchConfigs();

            // Subscribe to config updates
            LvlUpManager.RemoteConfig.OnConfigsUpdated += OnConfigsUpdated;
        }

        private void FetchConfigs()
        {
            LvlUpManager.FetchRemoteConfigs(success =>
            {
                if (success)
                {
                    Debug.Log("Configs fetched successfully");
                    UseConfigs();
                }
                else
                {
                    Debug.LogError("Failed to fetch configs");
                }
            });
        }

        private void UseConfigs()
        {
            var remoteConfig = LvlUpManager.RemoteConfig;

            // Get different config types
            int dailyReward = remoteConfig.GetInt("daily_reward_coins", defaultValue: 100);
            string apiUrl = remoteConfig.GetString("api_endpoint", defaultValue: "https://api.default.com");
            bool enablePremiumFeatures = remoteConfig.GetBool("enable_premium_features", defaultValue: false);
            float difficultyMultiplier = remoteConfig.GetFloat("difficulty_multiplier", defaultValue: 1.0f);

            // Get JSON config
            var serverConfig = remoteConfig.GetJson<ServerConfig>("server_config", defaultValue: null);

            Debug.Log($"Daily Reward: {dailyReward}");
            Debug.Log($"API Endpoint: {apiUrl}");
            Debug.Log($"Premium Features Enabled: {enablePremiumFeatures}");
            Debug.Log($"Difficulty Multiplier: {difficultyMultiplier}");
            
            if (serverConfig != null)
            {
                Debug.Log($"Server Config - Host: {serverConfig.host}, Port: {serverConfig.port}");
            }

            // Check if specific config exists
            if (remoteConfig.HasKey("special_event_active"))
            {
                Debug.Log("Special event config found");
            }

            // Get all loaded config keys
            foreach (var key in remoteConfig.GetAllKeys())
            {
                Debug.Log($"Loaded config key: {key}");
            }

            // Check cache status
            if (remoteConfig.IsCached())
            {
                long ageMs = remoteConfig.GetCacheAgeMs();
                Debug.Log($"Configs are from cache, age: {ageMs}ms");
            }
        }

        private void OnConfigsUpdated(RemoteConfig.ConfigsUpdatedEvent evt)
        {
            Debug.Log($"Configs updated! Loaded {evt.configs.Count} configs (from cache: {evt.isFromCache})");
            
            // React to config updates
            ApplyConfigChanges();
        }

        private void ApplyConfigChanges()
        {
            var remoteConfig = LvlUpManager.RemoteConfig;
            
            // Apply new values to your game
            int newReward = remoteConfig.GetInt("daily_reward_coins", 100);
            // Apply this value to your game logic
            Debug.Log($"Applying new daily reward: {newReward}");
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            var remoteConfig = LvlUpManager.RemoteConfig;
            if (remoteConfig != null)
            {
                remoteConfig.OnConfigsUpdated -= OnConfigsUpdated;
            }
        }

        /// <summary>
        /// Example JSON config structure
        /// </summary>
        [System.Serializable]
        public class ServerConfig
        {
            public string host;
            public int port;
            public string[] endpoints;
        }
    }
}

