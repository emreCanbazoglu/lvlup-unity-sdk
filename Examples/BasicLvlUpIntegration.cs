using UnityEngine;
using System.Collections.Generic;
using LvlUp;
using LvlUp.Models;

/// <summary>
/// Basic example showing how to integrate LvlUp SDK into your game
/// Attach this to a GameObject in your initial scene
/// </summary>
public class BasicLvlUpIntegration : MonoBehaviour
{
    [Header("LvlUp Configuration")]
    [Tooltip("Your game's API key from LvlUp dashboard")]
    public string apiKey = "lvl_your_api_key_here";
    
    [Tooltip("Backend URL (without /api at the end)")]
    public string backendUrl = "https://your-backend-url.com/api";
    
    [Header("Debug Settings")]
    public bool enableDebugLogs = true;

    private void Start()
    {
        InitializeLvlUp();
    }

    /// <summary>
    /// Initialize the LvlUp SDK
    /// </summary>
    private void InitializeLvlUp()
    {
        // Create custom configuration
        var config = new LvlUpConfig
        {
            enableDebugLogs = enableDebugLogs,
            autoTrackSessions = true,
            autoTrackAppLifecycle = true,
            eventBatchSize = 20,
            eventFlushInterval = 30f,
            sendImmediately = false
        };

        // Initialize SDK with callback
        LvlUpManager.Initialize(
            apiKey: apiKey,
            baseUrl: backendUrl,
            config: config,
            onComplete: (success, message) =>
            {
                if (success)
                {
                    Debug.Log($"✅ LvlUp SDK Ready: {message}");
                    OnLvlUpReady();
                }
                else
                {
                    Debug.LogError($"❌ LvlUp Initialization Failed: {message}");
                }
            }
        );
    }

    /// <summary>
    /// Called when LvlUp SDK is ready to use
    /// </summary>
    private void OnLvlUpReady()
    {
        Debug.Log("LvlUp SDK initialized and ready to track events!");
        
        // Example: Track game start event
        LvlUpManager.Instance.TrackEvent("game_started", new Dictionary<string, object>
        {
            { "platform", Application.platform.ToString() },
            { "version", Application.version }
        });
    }

    /// <summary>
    /// Example: Track level start using helper method
    /// Call this when a level begins
    /// </summary>
    public void OnLevelStart(int levelId)
    {
        // Using the standard helper method from LvlUpEvents
        LvlUpEvents.TrackLevelStart(levelId);
        
        Debug.Log($"📍 Level {levelId} started");
    }

    /// <summary>
    /// Example: Track level start with additional data
    /// </summary>
    public void OnLevelStart(int levelId, string levelName, string difficulty)
    {
        // Using helper method with additional properties
        LvlUpEvents.TrackLevelStart(levelId, levelName, new Dictionary<string, object>
        {
            { "difficulty", difficulty },
            { "previousBestScore", GetPreviousBestScore(levelId) }
        });
    }

    /// <summary>
    /// Example: Track level completion using helper method
    /// Call this when player completes a level
    /// </summary>
    public void OnLevelComplete(int levelId, int score, float timeSeconds)
    {
        // Using the standard helper method with stars
        LvlUpEvents.TrackLevelComplete(levelId, score, timeSeconds);
        
        Debug.Log($"✅ Level {levelId} complete! Score: {score}");
    }

    /// <summary>
    /// Example: Track level completion with additional data
    /// </summary>
    public void OnLevelCompleteAdvanced(int levelId, int score, float timeSeconds)
    {
        // Using helper method with additional properties
        LvlUpEvents.TrackLevelComplete(levelId, score, timeSeconds, new Dictionary<string, object>
        {
            { "coinsCollected", GetCoinsCollected() },
            { "enemiesDefeated", GetEnemiesDefeated() }
        });
    }

    /// <summary>
    /// Example: Track level failure using helper method
    /// Call this when player fails a level
    /// </summary>
    public void OnLevelFailed(int levelId, string reason, float timeSeconds)
    {
        // Using the standard helper method
        LvlUpEvents.TrackLevelFailed(levelId, reason, timeSeconds);
        
        Debug.Log($"❌ Level {levelId} failed. Reason: {reason}");
    }

    /// <summary>
    /// Example: Track level failure with attempts
    /// </summary>
    public void OnLevelFailedWithAttempts(int levelId, string reason, float timeSeconds, int attempts)
    {
        // Using helper method with attempts
        LvlUpEvents.TrackLevelFailed(levelId, reason, timeSeconds, attempts, new Dictionary<string, object>
        {
            { "healthRemaining", GetPlayerHealth() },
            { "checkpointReached", GetLastCheckpoint() }
        });
    }

    /// <summary>
    /// Example: Track level completion (legacy method for comparison)
    /// This shows the old way vs using the helper method
    /// </summary>
    public void OnLevelCompleteOldWay(int levelId, int score, int stars, float timeSeconds)
    {
        // Old way - manually creating properties with LvlUpManager
        var properties = new Dictionary<string, object>
        {
            { "levelId", levelId },
            { "score", score },
            { "stars", stars },
            { "timeSeconds", timeSeconds },
            { "timestamp", System.DateTime.UtcNow.ToString("o") }
        };

        LvlUpManager.Instance.TrackEvent("level_complete", properties, response =>
        {
            if (response.success)
            {
                Debug.Log($"✅ Level complete event tracked!");
            }
        });
    }

    /// <summary>
    /// Example: Track button clicks
    /// </summary>
    public void OnButtonClick(string buttonName)
    {
        var properties = new Dictionary<string, object>
        {
            { "buttonName", buttonName },
            { "screenName", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name }
        };

        LvlUpManager.Instance.TrackEvent("button_click", properties);
    }

    /// <summary>
    /// Example: Track in-app purchase
    /// </summary>
    public void OnPurchase(string productId, float price, string currency)
    {
        var properties = new Dictionary<string, object>
        {
            { "productId", productId },
            { "price", price },
            { "currency", currency },
            { "purchaseTime", System.DateTime.UtcNow.ToString("o") }
        };

        LvlUpManager.Instance.TrackEvent("purchase", properties, response =>
        {
            if (response.success)
            {
                Debug.Log($"✅ Purchase event tracked: {productId}");
            }
        });
    }

    /// <summary>
    /// Example: Track player death
    /// </summary>
    public void OnPlayerDeath(string causeOfDeath, Vector3 position)
    {
        var properties = new Dictionary<string, object>
        {
            { "cause", causeOfDeath },
            { "positionX", position.x },
            { "positionY", position.y },
            { "positionZ", position.z },
            { "levelId", GetCurrentLevelId() }
        };

        LvlUpManager.Instance.TrackEvent("player_death", properties);
    }

    /// <summary>
    /// Example: Batch multiple events at once
    /// </summary>
    public void TrackMultipleEvents()
    {
        var events = new List<LvlUpEvent>
        {
            new LvlUpEvent("tutorial_start", new Dictionary<string, object> 
            { 
                { "tutorialId", "basics" } 
            }),
            new LvlUpEvent("tutorial_step_complete", new Dictionary<string, object> 
            { 
                { "step", 1 } 
            }),
            new LvlUpEvent("tutorial_step_complete", new Dictionary<string, object> 
            { 
                { "step", 2 } 
            })
        };

        LvlUpManager.Instance.TrackEventsBatch(events, response =>
        {
            if (response.success)
            {
                Debug.Log($"✅ Batch of {events.Count} events tracked!");
            }
        });
    }

    private void OnApplicationQuit()
    {
        // Flush any remaining events before quitting
        LvlUpManager.Instance.FlushEventQueue(response =>
        {
            Debug.Log($"Final event flush: {response.success}");
        });
    }

    #region Helper Methods

    private int GetCurrentLevelId()
    {
        // Return current level ID from your game state
        return 1;
    }

    private int GetPreviousBestScore(int levelId)
    {
        // Return the player's best score for this level
        return PlayerPrefs.GetInt($"BestScore_Level_{levelId}", 0);
    }

    private int GetCoinsCollected()
    {
        // Return coins collected in current level
        return 0;
    }

    private int GetEnemiesDefeated()
    {
        // Return enemies defeated in current level
        return 0;
    }

    private float GetPlayerHealth()
    {
        // Return player's remaining health
        return 0f;
    }

    private int GetLastCheckpoint()
    {
        // Return last checkpoint reached
        return 0;
    }

    #endregion
}

