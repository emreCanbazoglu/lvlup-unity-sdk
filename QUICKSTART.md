# LvlUp Unity SDK - Quick Start Guide

## Installation

### Method 1: Unity Package Manager (Git URL)
1. Open Unity Package Manager (Window > Package Manager)
2. Click '+' → "Add package from git URL"
3. Enter: `https://github.com/yourusername/lvlup-unity-sdk.git`

### Method 2: Download and Import
1. Download the latest release
2. Copy the `LvlUp` folder into your project's `Assets/` directory

## 5-Minute Setup

### Step 1: Get Your API Key
1. Go to your LvlUp dashboard
2. Navigate to Games section
3. Create a new game or select existing one
4. Copy your API key (starts with `lvl_`)

### Step 2: Initialize in Your Game

Create a new script or add to your existing game initialization script:

```csharp
using UnityEngine;
using LvlUp;

public class GameManager : MonoBehaviour
{
    void Start()
    {
        // Initialize LvlUp with callback
        LvlUpManager.Initialize(
            apiKey: "lvl_your_api_key_here",
            baseUrl: "https://your-backend.com/api",
            config: null,
            onComplete: (success, message) =>
            {
                if (success)
                {
                    Debug.Log($"✅ LvlUp Ready: {message}");
                    // SDK is ready - start tracking events
                    OnSdkReady();
                }
                else
                {
                    Debug.LogError($"❌ Failed: {message}");
                }
            }
        );
    }

    void OnSdkReady()
    {
        // Start tracking your game events here
        LvlUpManager.Instance.TrackEvent("game_started", null);
    }
}
```

**Note:** Sessions start automatically! The callback tells you when everything is ready.

### Step 3: Track Your First Event

```csharp
// Track a level completion
public void OnLevelComplete(int level, int score)
{
    LvlUpManager.Instance.TrackEvent("level_complete", new Dictionary<string, object>
    {
        { "level", level },
        { "score", score }
    });
}
```

That's it! You're now tracking events. 🎉

## Common Use Cases

### Track Button Clicks
```csharp
public void OnPlayButtonClick()
{
    LvlUpManager.Instance.TrackEvent("button_click", new Dictionary<string, object>
    {
        { "button", "play" }
    });
}
```

### Track In-App Purchases
```csharp
public void OnPurchase(string productId, float price)
{
    LvlUpManager.Instance.TrackEvent("purchase", new Dictionary<string, object>
    {
        { "productId", productId },
        { "price", price },
        { "currency", "USD" }
    });
}
```

### Track Player Deaths
```csharp
public void OnPlayerDeath(string cause)
{
    LvlUpManager.Instance.TrackEvent("player_death", new Dictionary<string, object>
    {
        { "cause", cause },
        { "position", transform.position.ToString() }
    });
}
```

## Advanced Features

### Player Journey Tracking
```csharp
// Create checkpoint
LvlUpManager.Instance.CreateCheckpoint(
    name: "Tutorial Complete",
    description: "Player finished tutorial",
    type: "tutorial",
    order: 1
);

// Record when player reaches checkpoint
LvlUpManager.Instance.RecordCheckpoint(checkpointId);
```


## Configuration Options

```csharp
var config = new LvlUpConfig
{
    enableDebugLogs = true,         // Show debug logs
    autoTrackSessions = true,       // Auto track sessions
    eventBatchSize = 50,            // Events per batch
    eventFlushInterval = 30f,       // Seconds between flushes
    sendImmediately = false,        // Send events immediately or batch
    levelFunnel = "live_v1",        // Level design variant name
    levelFunnelVersion = 2          // Level design version number
};

LvlUpManager.Initialize(apiKey, baseUrl, config);
```

### Level Funnel Tracking (A/B Testing Level Designs)

Track different level designs to compare performance. Level funnel data is automatically added to all level events.

#### Recommended: Dynamic Configuration (from Backend)

```csharp
using UnityEngine;
using LvlUp;

public class GameManager : MonoBehaviour
{
    void Start()
    {
        // Step 1: Initialize SDK
        LvlUpManager.Initialize(
            apiKey: "your_api_key",
            baseUrl: "https://api.lvlup.com/api",
            config: new LvlUpConfig { enableDebugLogs = true },
            onComplete: OnSdkReady
        );
    }
    
    void OnSdkReady(bool success, string message)
    {
        if (success)
        {
            // Step 2: Fetch funnel assignment from backend
            FetchFunnelAssignment();
        }
    }
    
    void FetchFunnelAssignment()
    {
        // Get from Remote Config or A/B Test
        LvlUpManager.Instance.GetRemoteConfig("level_funnel", (response) =>
        {
            if (response.success)
            {
                string funnel = response.data["funnel"];        // e.g., "live_v1"
                int version = (int)response.data["version"];    // e.g., 2
                
                // Step 3: Set the funnel
                LvlUpManager.Instance.SetLevelFunnel(funnel, version);
                
                // Step 4: Start game - events now include funnel data
                StartGame();
            }
        });
    }
    
    void StartGame()
    {
        // All level events automatically include funnel data
        LvlUpEvents.TrackLevelStart(1);
        LvlUpEvents.TrackLevelComplete(1, 1000, 45.5f);
    }
}
```

#### Alternative: Static Configuration (Hardcoded)

Use this only if your funnel is determined at build time:

```csharp
// Configure at initialization
var config = new LvlUpConfig
{
    levelFunnel = "live_v1",        // Current level design
    levelFunnelVersion = 2           // Version of this design
};
LvlUpManager.Initialize(apiKey, baseUrl, config);

// Events automatically include funnel data
LvlUpEvents.TrackLevelStart(1);
```

#### What Gets Tracked

When you call:
```csharp
LvlUpEvents.TrackLevelStart(levelId: 1);
```

The backend receives:
```json
{
  "eventName": "level_start",
  "properties": { "levelId": 1 },
  "levelFunnel": "live_v1",
  "levelFunnelVersion": 2
}
```

#### Dashboard Analytics

In your dashboard, filter by:
- **levelFunnel**: "live_v1", "live_v2", "test_hard", etc.
- **levelFunnelVersion**: 1, 2, 3, etc.

Compare metrics:
- Win Rate, Fail Rate, Churn
- Attempts Per Success (APS)
- Completion/Fail durations
- Booster usage, EGP rate

**Use Cases:**
- A/B test easy vs hard level designs
- Track performance improvements across design iterations
- Compare different tutorial flows
- Measure impact of level layout changes

## Troubleshooting

**Events not showing in dashboard?**
- Check your API key is correct
- Verify backend URL is accessible
- Call `FlushEventQueue()` to force send events
- Enable debug logs to see what's happening

**Session not starting?**
- Make sure you call `Initialize()` before `StartSession()`
- Check console for error messages
- Verify your backend is running

## Next Steps

1. Check the `Examples/` folder for complete examples
2. Read the full documentation at https://docs.lvlup.com
3. Join our Discord community for support

## Support

- 📖 Docs: https://docs.lvlup.com
- 💬 Discord: https://discord.gg/lvlup
- 📧 Email: support@lvlup.com
- 🐛 Issues: https://github.com/yourusername/lvlup-unity-sdk/issues

