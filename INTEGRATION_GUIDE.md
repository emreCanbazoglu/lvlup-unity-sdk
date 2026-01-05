# Integration Guide: Adding LvlUp to Your Existing Game

This guide shows how to integrate LvlUp SDK into an existing Unity game with minimal code changes.

## Before You Start

- ✅ Unity 2019.4 or later
- ✅ LvlUp backend URL
- ✅ API key from LvlUp dashboard

## Step-by-Step Integration

### 1. Add SDK to Project

Copy the `LvlUp` folder to your `Assets/` directory.

### 2. Initialize on Game Start

Find your main game initialization script (usually attached to a persistent GameObject) and add:

```csharp
using LvlUp;

public class YourGameManager : MonoBehaviour
{
    void Start()
    {
        InitializeLvlUp();
    }

    private void InitializeLvlUp()
    {
        LvlUpManager.Initialize(
            apiKey: "YOUR_API_KEY",
            baseUrl: "YOUR_BACKEND_URL"
        );

        // Start session with user ID
        string userId = GetOrCreateUserId();
        LvlUpManager.Instance.StartSession(userId);
    }

    private string GetOrCreateUserId()
    {
        string userId = PlayerPrefs.GetString("UserId", "");
        if (string.IsNullOrEmpty(userId))
        {
            userId = $"user_{System.Guid.NewGuid()}";
            PlayerPrefs.SetString("UserId", userId);
        }
        return userId;
    }
}
```

### 3. Track Key Game Events

#### Level System

If you have a level management system:

```csharp
public class LevelManager : MonoBehaviour
{
    public void StartLevel(int levelId)
    {
        // Your existing code...
        
        // Add LvlUp tracking
        LvlUpManager.Instance.TrackEvent("level_start", new Dictionary<string, object>
        {
            { "levelId", levelId },
            { "timestamp", System.DateTime.UtcNow.ToString("o") }
        });
    }

    public void CompleteLevel(int levelId, int score, float timeSeconds)
    {
        // Your existing code...
        
        // Add LvlUp tracking
        LvlUpManager.Instance.TrackEvent("level_complete", new Dictionary<string, object>
        {
            { "levelId", levelId },
            { "score", score },
            { "timeSeconds", timeSeconds },
            { "stars", CalculateStars(score) }
        });
    }

    public void FailLevel(int levelId, string reason)
    {
        // Your existing code...
        
        // Add LvlUp tracking
        LvlUpManager.Instance.TrackEvent("level_fail", new Dictionary<string, object>
        {
            { "levelId", levelId },
            { "reason", reason }
        });
    }
}
```

#### UI Buttons

Add tracking to important buttons:

```csharp
public class UIManager : MonoBehaviour
{
    public void OnPlayButton()
    {
        LvlUpManager.Instance.TrackEvent("button_click", new Dictionary<string, object>
        {
            { "button", "play" },
            { "screen", "main_menu" }
        });
        
        // Your existing button code...
        StartGame();
    }

    public void OnShopButton()
    {
        LvlUpManager.Instance.TrackEvent("button_click", new Dictionary<string, object>
        {
            { "button", "shop" },
            { "screen", "main_menu" }
        });
        
        // Your existing button code...
        OpenShop();
    }
}
```

#### In-App Purchases

Track purchases in your IAP handler:

```csharp
public class IAPManager : MonoBehaviour
{
    public void OnPurchaseComplete(string productId, decimal price, string currency)
    {
        // Your existing IAP code...
        
        // Add LvlUp tracking
        LvlUpManager.Instance.TrackEvent("purchase", new Dictionary<string, object>
        {
            { "productId", productId },
            { "price", (float)price },
            { "currency", currency },
            { "platform", Application.platform.ToString() }
        });
    }

    public void OnPurchaseFailed(string productId, string reason)
    {
        LvlUpManager.Instance.TrackEvent("purchase_failed", new Dictionary<string, object>
        {
            { "productId", productId },
            { "reason", reason }
        });
    }
}
```

#### Player Actions

Track important player actions:

```csharp
public class PlayerController : MonoBehaviour
{
    public void OnPlayerDeath(string cause)
    {
        // Your existing death handling...
        
        // Add LvlUp tracking
        LvlUpManager.Instance.TrackEvent("player_death", new Dictionary<string, object>
        {
            { "cause", cause },
            { "health", currentHealth },
            { "level", currentLevel },
            { "position", transform.position.ToString() }
        });
    }

    public void OnPowerUpCollected(string powerUpType)
    {
        // Your existing power-up code...
        
        // Add LvlUp tracking
        LvlUpManager.Instance.TrackEvent("powerup_collected", new Dictionary<string, object>
        {
            { "type", powerUpType },
            { "level", currentLevel }
        });
    }
}
```

### 4. Add Player Journey Checkpoints

Create important checkpoints in your game flow:

```csharp
public class GameProgressManager : MonoBehaviour
{
    private string tutorialCheckpointId;
    private string firstWinCheckpointId;

    void Start()
    {
        SetupCheckpoints();
    }

    private void SetupCheckpoints()
    {
        // Create tutorial checkpoint
        LvlUpManager.Instance.CreateCheckpoint(
            name: "Tutorial Complete",
            description: "Player completed the tutorial",
            type: "tutorial",
            order: 1,
            tags: new[] { "onboarding" },
            callback: response =>
            {
                if (response.success)
                    tutorialCheckpointId = response.data.id;
            }
        );

        // Create first win checkpoint
        LvlUpManager.Instance.CreateCheckpoint(
            name: "First Victory",
            description: "Player won their first game",
            type: "achievement",
            order: 2,
            tags: new[] { "milestone" },
            callback: response =>
            {
                if (response.success)
                    firstWinCheckpointId = response.data.id;
            }
        );
    }

    public void OnTutorialComplete()
    {
        // Your existing code...
        
        // Record checkpoint
        LvlUpManager.Instance.RecordCheckpoint(tutorialCheckpointId);
    }

    public void OnFirstWin()
    {
        // Your existing code...
        
        // Record checkpoint
        LvlUpManager.Instance.RecordCheckpoint(firstWinCheckpointId);
    }
}
```

### 5. Optional: Add Scene Tracking

If you want to automatically track scene changes:

```csharp
using UnityEngine.SceneManagement;

public class SceneTracker : MonoBehaviour
{
    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        LvlUpManager.Instance.TrackEvent("scene_loaded", new Dictionary<string, object>
        {
            { "sceneName", scene.name },
            { "buildIndex", scene.buildIndex }
        });
    }
}
```

### 6. Clean Up on App Close

Make sure events are flushed when app closes:

```csharp
public class YourGameManager : MonoBehaviour
{
    void OnApplicationQuit()
    {
        // Flush any pending events
        LvlUpManager.Instance.FlushEventQueue();
        
        // End session
        LvlUpManager.Instance.EndSession();
    }
}
```

## Minimal Integration Example

If you want the absolute minimum integration:

```csharp
using UnityEngine;
using LvlUp;
using System.Collections.Generic;

public class MinimalLvlUpIntegration : MonoBehaviour
{
    void Start()
    {
        // 1. Initialize
        LvlUpManager.Initialize("YOUR_API_KEY", "YOUR_BACKEND_URL");
        
        // 2. Start session
        LvlUpManager.Instance.StartSession(SystemInfo.deviceUniqueIdentifier);
    }

    // 3. Track events wherever needed in your game
    public void TrackGameEvent(string eventName, Dictionary<string, object> data = null)
    {
        LvlUpManager.Instance.TrackEvent(eventName, data);
    }

    void OnApplicationQuit()
    {
        // 4. Clean up
        LvlUpManager.Instance.FlushEventQueue();
        LvlUpManager.Instance.EndSession();
    }
}
```

Then use it anywhere in your code:
```csharp
FindObjectOfType<MinimalLvlUpIntegration>()?.TrackGameEvent("level_complete", 
    new Dictionary<string, object> { { "level", 5 } });
```

## Testing Your Integration

1. **Enable Debug Logs**
```csharp
var config = new LvlUpConfig { enableDebugLogs = true };
LvlUpManager.Initialize(apiKey, baseUrl, config);
```

2. **Check Console** - You should see:
   - `[LvlUp] SDK Initialized`
   - `[LvlUp] Session started`
   - `[LvlUp] Event queued: event_name`

3. **Verify in Dashboard** - After playing, check your LvlUp dashboard for:
   - Active sessions
   - Event counts
   - Player journey progress

## Common Patterns

### Wrapper Class (Recommended)

Create a wrapper to make it easier to track events consistently:

```csharp
public static class AnalyticsHelper
{
    public static void TrackLevelStart(int levelId)
    {
        LvlUpManager.Instance.TrackEvent("level_start", new Dictionary<string, object>
        {
            { "levelId", levelId },
            { "timestamp", System.DateTime.UtcNow.ToString("o") }
        });
    }

    public static void TrackLevelComplete(int levelId, int score, float time)
    {
        LvlUpManager.Instance.TrackEvent("level_complete", new Dictionary<string, object>
        {
            { "levelId", levelId },
            { "score", score },
            { "timeSeconds", time }
        });
    }

    // Add more helper methods as needed
}
```

Usage:
```csharp
AnalyticsHelper.TrackLevelStart(5);
```

## Performance Considerations

- Events are batched automatically (default: 50 events)
- Events are flushed every 30 seconds by default
- Minimal impact on game performance
- All network calls are async/non-blocking

## Need Help?

- Check the example scripts in `Examples/` folder
- Read full documentation at https://docs.lvlup.com
- Contact support at support@lvlup.com

