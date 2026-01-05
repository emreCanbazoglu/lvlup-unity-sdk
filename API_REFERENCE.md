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
public static void Initialize(string apiKey, string baseUrl, LvlUpConfig config = null)
```
Initialize the LvlUp SDK with your API credentials.

**Parameters:**
- `apiKey` (string): Your game's API key from LvlUp dashboard
- `baseUrl` (string): Backend API URL (e.g., "https://api.lvlup.com/api")
- `config` (LvlUpConfig, optional): Custom configuration options

**Example:**
```csharp
LvlUpManager.Initialize("lvl_your_api_key", "https://api.lvlup.com/api");
```

### Instance Methods

#### Session Management

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
    public bool autoTrackScenes = false;
    public bool persistQueueToDisk = true;
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
- **autoTrackScenes**: Automatically track scene changes
- **persistQueueToDisk**: Save queue to disk for persistence

**Example:**
```csharp
var config = new LvlUpConfig
{
    enableDebugLogs = true,
    eventBatchSize = 20,
    eventFlushInterval = 15f
};

LvlUpManager.Initialize(apiKey, baseUrl, config);
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

