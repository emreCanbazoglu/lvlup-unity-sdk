using UnityEngine;
using LvlUp;
using System.Collections.Generic;

/// <summary>
/// Example showing how to use LvlUp Remote Config in your game
/// Uses the new clean LvlUpSDK static API
/// </summary>
public class RemoteConfigExample : MonoBehaviour
{
    private void Start()
    {
        // Wait for SDK to initialize and remote config to be ready
        StartCoroutine(WaitForRemoteConfigAndUse());
    }

    private System.Collections.IEnumerator WaitForRemoteConfigAndUse()
    {
        // Wait for Remote Config to be ready
        // This ensures configs have been fetched from server
        while (!LvlUpSDK.Config.IsReady)
        {
            Debug.Log("[Example] Waiting for RemoteConfig to initialize...");
            yield return new WaitForSeconds(0.5f);
        }

        Debug.Log("[Example] RemoteConfig is ready! Fetching values...");

        // Now you can safely use remote config values
        UseRemoteConfigValues();
    }

    private void UseRemoteConfigValues()
    {
        // ===========================================
        // EXAMPLE 1: Try to get simple values
        // ===========================================

        // Try to get integer value
        if (LvlUpSDK.Config.TryGetInt("daily_reward_coins", out int dailyRewardCoins))
        {
            Debug.Log($"[Example] Daily reward coins: {dailyRewardCoins}");
        }
        else
        {
            Debug.Log("[Example] daily_reward_coins not found, using default: 100");
            dailyRewardCoins = 100; // Use your default
        }

        // Try to get string value
        if (LvlUpSDK.Config.TryGetString("welcome_message", out string welcomeMessage))
        {
            Debug.Log($"[Example] Welcome message: {welcomeMessage}");
        }
        else
        {
            welcomeMessage = "Welcome to the game!"; // Default
        }

        // Try to get boolean value
        if (LvlUpSDK.Config.TryGetBool("tutorial_enabled", out bool isTutorialEnabled))
        {
            Debug.Log($"[Example] Tutorial enabled: {isTutorialEnabled}");
        }
        else
        {
            isTutorialEnabled = true; // Default
        }

        // Try to get float value
        if (LvlUpSDK.Config.TryGetFloat("player_speed", out float playerSpeed))
        {
            Debug.Log($"[Example] Player speed: {playerSpeed}");
        }
        else
        {
            playerSpeed = 5.0f; // Default
        }

        // ===========================================
        // EXAMPLE 2: Get complex JSON objects
        // ===========================================

        // Try to get a JSON object and deserialize to a class
        if (LvlUpSDK.Config.TryGetJson<DailyRewardConfig>("daily_reward_amount", out DailyRewardConfig rewardConfig))
        {
            Debug.Log($"[Example] Daily reward - Coins: {rewardConfig.coins}, Gems: {rewardConfig.gems}");
        }
        else
        {
            Debug.Log("[Example] daily_reward_amount not found or failed to parse");
            rewardConfig = new DailyRewardConfig { coins = 100, gems = 5 }; // Default
        }

        // Try to get difficulty settings as a nested object
        if (LvlUpSDK.Config.TryGetJson<DifficultySettings>("difficulty_settings", out DifficultySettings difficultySettings))
        {
            if (difficultySettings.easy != null)
            {
                Debug.Log($"[Example] Easy mode - Multiplier: {difficultySettings.easy.multiplier}, Lives: {difficultySettings.easy.lives}");
            }
        }
        else
        {
            Debug.Log("[Example] difficulty_settings not found");
        }

        // Try to get an array of strings
        if (LvlUpSDK.Config.TryGetJson<List<string>>("shop_featured_items", out List<string> featuredItems))
        {
            Debug.Log($"[Example] Featured items count: {featuredItems.Count}");
            foreach (var item in featuredItems)
            {
                Debug.Log($"[Example] - Featured: {item}");
            }
        }
        else
        {
            Debug.Log("[Example] shop_featured_items not found");
            featuredItems = new List<string> { "default_item_1", "default_item_2" };
        }

        // ===========================================
        // EXAMPLE 3: Check if key exists
        // ===========================================

        if (LvlUpSDK.Config.HasKey("special_event_active"))
        {
            if (LvlUpSDK.Config.TryGetBool("special_event_active", out bool isEventActive))
            {
                Debug.Log($"[Example] Special event is {(isEventActive ? "ACTIVE" : "INACTIVE")}");
            }
        }
        else
        {
            Debug.Log("[Example] No special event configured");
        }

        // ===========================================
        // EXAMPLE 4: Use config values in game logic
        // ===========================================

        ApplyGameSettings();
    }

    private void ApplyGameSettings()
    {
        // Example: Apply difficulty settings based on remote config
        float difficulty = 1.0f; // Default
        if (LvlUpSDK.Config.TryGetFloat("game_difficulty", out float configDifficulty))
        {
            difficulty = configDifficulty;
        }
        
        // Apply to game
        Debug.Log($"[Example] Applying difficulty: {difficulty}x");
        
        // Example: Show/hide features based on config
        if (LvlUpSDK.Config.TryGetBool("show_new_ui_feature", out bool showNewFeature) && showNewFeature)
        {
            Debug.Log("[Example] Enabling new UI feature!");
            // Enable your new feature here
        }

        // Example: A/B testing with remote config
        string abTestVariant = "control"; // Default
        if (LvlUpSDK.Config.TryGetString("ab_test_variant", out string configVariant))
        {
            abTestVariant = configVariant;
        }
        
        Debug.Log($"[Example] A/B Test variant: {abTestVariant}");
        
        switch (abTestVariant)
        {
            case "variant_a":
                Debug.Log("[Example] Using variant A logic");
                break;
            case "variant_b":
                Debug.Log("[Example] Using variant B logic");
                break;
            default:
                Debug.Log("[Example] Using control logic");
                break;
        }
    }

    // ===========================================
    // EXAMPLE 5: Manually refresh config
    // ===========================================
    
    public void RefreshConfigButton()
    {
        Debug.Log("[Example] Manually refreshing remote config...");
        
        LvlUpSDK.Config.Refresh(success =>
        {
            if (success)
            {
                Debug.Log("[Example] Config refreshed successfully!");
                UseRemoteConfigValues(); // Re-apply values
            }
            else
            {
                Debug.LogError("[Example] Failed to refresh config");
            }
        });
    }

    // ===========================================
    // Example data classes for JSON deserialization
    // ===========================================

    [System.Serializable]
    public class DailyRewardConfig
    {
        public int coins;
        public int gems;
    }

    [System.Serializable]
    public class DifficultySettings
    {
        public DifficultyLevel easy;
        public DifficultyLevel medium;
        public DifficultyLevel hard;
    }

    [System.Serializable]
    public class DifficultyLevel
    {
        public float multiplier;
        public int lives;
    }
}

