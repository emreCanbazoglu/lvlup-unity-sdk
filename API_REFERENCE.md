# LvlUp Unity SDK - API Reference

Complete API documentation for the LvlUp Unity SDK.

## Table of Contents
- [LvlUpManager](#lvlupmanager)
- [LvlUpConfig](#lvlupconfig)
- [Models](#models)
- [Events](#events)

---

## LvlUpManager

Main singleton class for interacting with the LvlUp SDK.

### Static Methods

#### Initialize
```csharp
public static void Initialize(string apiKey, string baseUrl, LvlUpConfig config = null, Action<bool, string> onComplete = null)
```
Initialize the LvlUp SDK with your API credentials.

**Parameters:**
- `apiKey` (string): Your game's API key from LvlUp dashboard
- `baseUrl` (string): Backend API URL (e.g., "https://api.lvlup.com/api")
- `config` (LvlUpConfig, optional): Custom configuration options
- `onComplete` (Action<bool, string>, optional): Callback when initialization completes
  - First parameter (bool): `true` if successful, `false` if failed
  - Second parameter (string): Success/error message

**Callback Behavior:**
- With `autoTrackSessions = true` (default): Fires after session starts
- With `autoTrackSessions = false`: Fires immediately after initialization

**Example:**
```csharp
LvlUpManager.Initialize(
    apiKey: "lvl_your_api_key",
    baseUrl: "https://api.lvlup.com/api",
    config: null,
    onComplete: (success, message) =>
    {
        if (success)
        {
            Debug.Log($"✅ SDK Ready: {message}");
            // Start tracking events
        }
        else
        {
            Debug.LogError($"❌ Init Failed: {message}");
        }
    }
);
```

### Instance Methods

#### Event Tracking

##### StartSession
```csharp
public void StartSession(string userId, UserMetadata metadata = null, Action<ApiResponse<SessionData>> callback = null)
```
Start a new tracking session for a user.

**Parameters:**
- `userId` (string): Unique identifier for the user
- `metadata` (UserMetadata, optional): Additional user information
- `callback` (Action, optional): Callback with session data

**Example:**
```csharp
var metadata = new UserMetadata
{
    deviceId = SystemInfo.deviceUniqueIdentifier,
    platform = Application.platform.ToString(),
    version = Application.version
};

LvlUpManager.Instance.StartSession("user_123", metadata, response =>
{
    if (response.success)
        Debug.Log($"Session started: {response.data.sessionId}");
});
```

##### EndSession
```csharp
public void EndSession(Action<ApiResponse<SessionData>> callback = null)
```
End the current tracking session.

**Example:**
```csharp
LvlUpManager.Instance.EndSession(response =>
{
    if (response.success)
        Debug.Log("Session ended");
});
```

##### GetCurrentSession
```csharp
public SessionData GetCurrentSession()
```
Get the current active session data.

**Returns:** Current SessionData or null if no active session

#### Event Tracking

##### TrackEvent
```csharp
public void TrackEvent(string eventName, Dictionary<string, object> properties, Action<ApiResponse> callback = null)
```
Track a single event.

**Parameters:**
- `eventName` (string): Name of the event
- `properties` (Dictionary<string, object>): Event properties
- `callback` (Action, optional): Callback with response

**Example:**
```csharp
LvlUpManager.Instance.TrackEvent("level_complete", new Dictionary<string, object>
{
    { "levelId", 5 },
    { "score", 12500 },
    { "stars", 3 }
});
```

##### TrackEventsBatch
```csharp
public void TrackEventsBatch(List<LvlUpEvent> events, Action<ApiResponse> callback = null)
```
Track multiple events in a single batch request.

**Parameters:**
- `events` (List<LvlUpEvent>): List of events to track
- `callback` (Action, optional): Callback with response

**Example:**
```csharp
var events = new List<LvlUpEvent>
{
    new LvlUpEvent("button_click", new Dictionary<string, object> { { "button", "play" } }),
    new LvlUpEvent("button_click", new Dictionary<string, object> { { "button", "shop" } })
};

LvlUpManager.Instance.TrackEventsBatch(events);
```

##### FlushEventQueue
```csharp
public void FlushEventQueue(Action<ApiResponse> callback = null)
```
Manually flush all queued events to the server.

**Example:**
```csharp
LvlUpManager.Instance.FlushEventQueue(response =>
{
    Debug.Log($"Flush complete: {response.success}");
});
```

##### GetQueuedEventCount
```csharp
public int GetQueuedEventCount()
```
Get the number of events currently queued.

**Returns:** Number of queued events

#### Player Journey

##### CreateCheckpoint
```csharp
public void CreateCheckpoint(string name, string description, string type, int order, string[] tags = null, Action<ApiResponse<Checkpoint>> callback = null)
```
Create a new checkpoint in the player journey.

**Parameters:**
- `name` (string): Checkpoint name
- `description` (string): Checkpoint description
- `type` (string): Checkpoint type (e.g., "tutorial", "level", "achievement")
- `order` (int): Order/sequence number
- `tags` (string[], optional): Additional tags
- `callback` (Action, optional): Callback with created checkpoint

**Example:**
```csharp
LvlUpManager.Instance.CreateCheckpoint(
    name: "Tutorial Complete",
    description: "Player completed the tutorial",
    type: "tutorial",
    order: 1,
    tags: new[] { "onboarding" },
    callback: response =>
    {
        if (response.success)
            Debug.Log($"Checkpoint created: {response.data.id}");
    }
);
```

##### RecordCheckpoint
```csharp
public void RecordCheckpoint(string checkpointId, Dictionary<string, object> metadata = null, Action<ApiResponse> callback = null)
```
Record when a user reaches a checkpoint.

**Parameters:**
- `checkpointId` (string): ID of the checkpoint
- `metadata` (Dictionary<string, object>, optional): Additional data
- `callback` (Action, optional): Callback with response

**Example:**
```csharp
LvlUpManager.Instance.RecordCheckpoint("checkpoint_id", new Dictionary<string, object>
{
    { "timeSpent", 120 },
    { "attempts", 1 }
});
```

##### GetPlayerJourneyProgress
```csharp
public void GetPlayerJourneyProgress(Action<ApiResponse<PlayerJourneyProgress>> callback = null)
```
Get the current user's journey progress.

**Example:**
```csharp
LvlUpManager.Instance.GetPlayerJourneyProgress(response =>
{
    if (response.success)
    {
        var progress = response.data;
        Debug.Log($"Completed: {progress.completedCheckpoints}/{progress.totalCheckpoints}");
    }
});
```

#### Utility Methods

##### IsInitialized
```csharp
public bool IsInitialized()
```
Check if the SDK has been initialized.

**Returns:** true if initialized, false otherwise

##### GetCurrentUserId
```csharp
public string GetCurrentUserId()
```
Get the current user ID.

**Returns:** Current user ID or null

---

## LvlUpConfig

Configuration class for customizing SDK behavior.

### Properties

```csharp
public class LvlUpConfig
{
    public bool enableDebugLogs = false;
    public bool autoTrackSessions = true;
    public int eventBatchSize = 50;
    public float eventFlushInterval = 30f;
    public int maxQueueSize = 1000;
    public int retryAttempts = 3;
    public float timeout = 30f;
    public bool sendImmediately = false;
    public bool autoTrackAppLifecycle = true;
    public bool enableGeoTracking = false;
    public bool autoTrackScenes = false;
    public bool persistQueueToDisk = true;
    public bool enableCrashReporting = true;
    public string levelFunnel = null;
    public int levelFunnelVersion = 1;
}
```

### Property Descriptions

- **enableDebugLogs**: Show debug logs in console
- **autoTrackSessions**: Automatically manage session lifecycle
- **eventBatchSize**: Number of events to batch before sending
- **eventFlushInterval**: Seconds between automatic flushes
- **maxQueueSize**: Maximum events to queue offline
- **retryAttempts**: Number of retry attempts for failed requests
- **timeout**: Request timeout in seconds
- **sendImmediately**: Send events immediately instead of batching
- **autoTrackAppLifecycle**: Track app pause/resume events
- **enableGeoTracking**: Enable automatic geographic location tracking
- **autoTrackScenes**: Automatically track scene changes
- **persistQueueToDisk**: Save queue to disk for persistence
- **enableCrashReporting**: Enable automatic crash and exception reporting
- **levelFunnel**: Level funnel name for A/B testing different level designs (e.g., "live_v1", "test_hard")
- **levelFunnelVersion**: Level funnel version number, incremented when funnel is modified

**Example:**
```csharp
var config = new LvlUpConfig
{
    enableDebugLogs = true,
    eventBatchSize = 20,
    eventFlushInterval = 15f,
    levelFunnel = "live_v1",
    levelFunnelVersion = 2
};

LvlUpManager.Initialize(apiKey, baseUrl, config);
```

### Level Funnel Tracking

Level funnel tracking helps you A/B test different level designs and track their performance separately. When configured, the SDK automatically adds `levelFunnel` and `levelFunnelVersion` to all level events (`level_start`, `level_complete`, `level_failed`).

**Use Cases:**
- Compare different level layouts ("live_v1" vs "live_v2")
- Track performance across level design iterations (version 1, 2, 3...)
- Measure impact of level difficulty changes
- Separate test levels from production levels

#### Method 1: Static Configuration (Simple)
Use this when your funnel is hardcoded or determined at build time:

```csharp
// Configure level funnel at initialization
var config = new LvlUpConfig
{
    levelFunnel = "live_v1",      // Current level design name
    levelFunnelVersion = 2         // Current version number
};
LvlUpManager.Initialize(apiKey, baseUrl, config);

// All level events will automatically include funnel data
LvlUpEvents.TrackLevelStart(levelId: 1, levelName: "Tutorial");
```

#### Method 2: Dynamic Configuration (Recommended for A/B Tests)
Use this when fetching funnel assignment from backend (Remote Config or A/B Test):

```csharp
// Step 1: Initialize SDK without funnel config
LvlUpManager.Initialize(apiKey, baseUrl, config: null, onComplete: (success, message) =>
{
    if (success)
    {
        // Step 2: Fetch funnel assignment from Remote Config or A/B Test
        FetchRemoteConfig((remoteConfig) =>
        {
            string funnel = remoteConfig["level_funnel"];        // e.g., "live_v1"
            int version = remoteConfig["level_funnel_version"];  // e.g., 2
            
            // Step 3: Set the funnel configuration
            LvlUpManager.Instance.SetLevelFunnel(funnel, version);
            
            // Step 4: Now start tracking level events
            LvlUpEvents.TrackLevelStart(1);
        });
    }
});
```

#### Available Methods

##### SetLevelFunnel
```csharp
public void SetLevelFunnel(string levelFunnel, int levelFunnelVersion)
```
Set or update level funnel configuration after initialization. Useful when fetching funnel assignment from backend.

**Parameters:**
- `levelFunnel` (string): Level funnel name (e.g., "live_v1", "test_hard")
- `levelFunnelVersion` (int): Level funnel version number (e.g., 1, 2, 3)

**Example:**
```csharp
// After fetching from Remote Config or A/B Test
LvlUpManager.Instance.SetLevelFunnel("live_v1", 2);
```

##### GetLevelFunnel
```csharp
public (string funnel, int version) GetLevelFunnel()
```
Get current level funnel configuration.

**Returns:** Tuple with (funnel name, version number)

**Example:**
```csharp
var (funnel, version) = LvlUpManager.Instance.GetLevelFunnel();
Debug.Log($"Current funnel: {funnel} v{version}");
```

#### Complete A/B Test Example

```csharp
using UnityEngine;
using LvlUp;

public class GameInitializer : MonoBehaviour
{
    void Start()
    {
        // Initialize SDK first
        LvlUpManager.Initialize(
            apiKey: "your_api_key",
            baseUrl: "https://api.lvlup.com/api",
            config: new LvlUpConfig { enableDebugLogs = true },
            onComplete: OnSdkInitialized
        );
    }
    
    void OnSdkInitialized(bool success, string message)
    {
        if (!success)
        {
            Debug.LogError($"SDK init failed: {message}");
            return;
        }
        
        // Fetch Remote Config to get funnel assignment
        FetchLevelFunnelFromBackend();
    }
    
    void FetchLevelFunnelFromBackend()
    {
        // Option 1: Use LvlUp Remote Config
        LvlUpManager.Instance.GetRemoteConfig("level_funnel_config", (response) =>
        {
            if (response.success)
            {
                string funnel = response.data["funnel"];
                int version = (int)response.data["version"];
                
                // Set the funnel
                LvlUpManager.Instance.SetLevelFunnel(funnel, version);
                
                // Ready to track level events
                StartGame();
            }
        });
        
        // Option 2: Use A/B Test Assignment
        // The backend assigns user to a test variant, each variant has different funnel
        LvlUpManager.Instance.GetABTestAssignment("level_design_test", (response) =>
        {
            if (response.success && response.data.variant != null)
            {
                // Each variant config contains funnel info
                var variantConfig = response.data.variant.config;
                string funnel = variantConfig["funnel"];
                int version = (int)variantConfig["version"];
                
                LvlUpManager.Instance.SetLevelFunnel(funnel, version);
                StartGame();
            }
        });
    }
    
    void StartGame()
    {
        // Now all level events will include the funnel data
        Debug.Log("Game ready with funnel configuration!");
    }
}
```

---

## Models

### UserMetadata
```csharp
public class UserMetadata
{
    public string deviceId;
    public string platform;
    public string version;
    public string country;
    public string language;
}
```

### SessionData
```csharp
public class SessionData
{
    public string sessionId;
    public string userId;
    public string startTime;
    public string endTime;
    public int duration;
}
```

### LvlUpEvent
```csharp
public class LvlUpEvent
{
    public string eventName;
    public Dictionary<string, object> properties;
    public string timestamp;

    public LvlUpEvent(string eventName, Dictionary<string, object> properties = null)
}
```

### Checkpoint
```csharp
public class Checkpoint
{
    public string id;
    public string name;
    public string description;
    public string type;
    public int order;
    public string[] tags;
    public string createdAt;
    public string gameId;
}
```

### PlayerJourneyProgress
```csharp
public class PlayerJourneyProgress
{
    public string userId;
    public int completedCheckpoints;
    public int totalCheckpoints;
    public float completionRate;
    public CheckpointCompletion[] checkpoints;
    public string lastCheckpointDate;
}
```


### ApiResponse<T>
```csharp
public class ApiResponse<T>
{
    public bool success;
    public T data;
    public string error;
    public string message;
}
```

---

## LvlUpEvents (Static Helper Class)

Static helper class for tracking standard game events with consistent structure. Decoupled from LvlUpManager for cleaner API.

### Static Methods

#### TrackLevelStart
```csharp
public static void TrackLevelStart(int levelId, Dictionary<string, object> additionalProperties = null)
public static void TrackLevelStart(int levelId, string levelName, Dictionary<string, object> additionalProperties = null)
```
Track when a player starts a level.

**Example:**
```csharp
LvlUpEvents.TrackLevelStart(5);
LvlUpEvents.TrackLevelStart(5, "Forest Battle", new Dictionary<string, object> { { "difficulty", "hard" } });
```

#### TrackLevelComplete
```csharp
public static void TrackLevelComplete(int levelId, int score, float timeSeconds, Dictionary<string, object> additionalProperties = null)
public static void TrackLevelComplete(int levelId, int score, float timeSeconds, int stars, Dictionary<string, object> additionalProperties = null)
```
Track when a player completes a level.

**Example:**
```csharp
LvlUpEvents.TrackLevelComplete(5, 12500, 125.5f);
LvlUpEvents.TrackLevelComplete(5, 12500, 125.5f, 3);
```

#### TrackLevelFailed
```csharp
public static void TrackLevelFailed(int levelId, string reason, float timeSeconds, Dictionary<string, object> additionalProperties = null)
public static void TrackLevelFailed(int levelId, string reason, float timeSeconds, int attempts, Dictionary<string, object> additionalProperties = null)
```
Track when a player fails a level.

**Example:**
```csharp
LvlUpEvents.TrackLevelFailed(5, "player_death", 45.2f);
LvlUpEvents.TrackLevelFailed(5, "time_expired", 180f, 3);
```

**See STANDARD_EVENT_HELPERS.md for complete documentation.**

---

## Events

### Standard Events

The SDK recommends using these standard event names for consistency:

#### Gameplay Events
- `level_start` - Player starts a level
- `level_complete` - Player completes a level
- `level_fail` - Player fails a level
- `player_death` - Player character dies
- `game_over` - Game over state

#### Progression Events
- `tutorial_start` - Tutorial begins
- `tutorial_complete` - Tutorial completes
- `tutorial_skip` - Player skips tutorial
- `achievement_unlocked` - Achievement earned
- `milestone_reached` - Milestone reached

#### UI Events
- `button_click` - Button interaction
- `screen_view` - Screen/menu viewed
- `popup_shown` - Popup displayed
- `popup_dismissed` - Popup closed

#### Monetization Events
- `purchase` - In-app purchase completed
- `purchase_failed` - Purchase failed
- `ad_viewed` - Ad watched
- `ad_clicked` - Ad clicked
- `shop_opened` - Shop accessed

#### Social Events
- `invite_sent` - Friend invite sent
- `invite_accepted` - Invite accepted
- `share_completed` - Content shared

#### Engagement Events
- `daily_login` - Daily login completed
- `session_start` - Manual session start
- `session_end` - Manual session end
- `powerup_used` - Power-up activated
- `item_collected` - Item collected

### Custom Events

You can track any custom event with properties:

```csharp
LvlUpManager.Instance.TrackEvent("custom_event_name", new Dictionary<string, object>
{
    { "property1", value1 },
    { "property2", value2 }
});
```

---

## Error Handling

All callbacks include an `ApiResponse` that indicates success/failure:

```csharp
LvlUpManager.Instance.TrackEvent("event_name", data, response =>
{
    if (response.success)
    {
        // Success
        Debug.Log("Event tracked successfully");
    }
    else
    {
        // Error
        Debug.LogError($"Error: {response.error}");
    }
});
```

## Thread Safety

All SDK methods are thread-safe and can be called from any thread. Network operations are automatically handled on Unity's main thread via coroutines.

## Performance

- Events are batched automatically to minimize network calls
- All network operations are asynchronous and non-blocking
- Minimal memory footprint (~100KB)
- Minimal CPU usage (<1% on most devices)

## Support

For questions or issues:
- 📖 Documentation: https://docs.lvlup.com
- 💬 Discord: https://discord.gg/lvlup
- 📧 Email: support@lvlup.com
- 🐛 GitHub Issues: https://github.com/yourusername/lvlup-unity-sdk/issues

