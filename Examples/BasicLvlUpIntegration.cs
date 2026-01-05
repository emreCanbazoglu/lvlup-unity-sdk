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
        TrackEvent("game_started", new Dictionary<string, object>
        {
            { "platform", Application.platform.ToString() },
            { "version", Application.version }
        });
    }

    /// <summary>
    /// Example: Track level completion
    /// Call this when player completes a level
    /// </summary>
    public void OnLevelComplete(int levelId, int score, int stars, float timeSeconds)
    {
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

    private string GetOrCreateUserId()
    {
        // Check if we have a saved user ID
        string userId = PlayerPrefs.GetString("LvlUp_UserId", "");
        
        if (string.IsNullOrEmpty(userId))
        {
            // Generate a new unique user ID
            userId = $"user_{System.Guid.NewGuid().ToString()}";
            PlayerPrefs.SetString("LvlUp_UserId", userId);
            PlayerPrefs.Save();
        }
        
        return userId;
    }

    private string GetUserCountry()
    {
        // In production, you might use a geolocation API
        // For now, return a default or use system locale
        return "US";
    }

    private int GetCurrentLevelId()
    {
        // Return current level ID from your game state
        return 1;
    }

    #endregion
}

