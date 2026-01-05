# LvlUp Unity SDK

Official Unity SDK for the LvlUp Analytics Platform.

## Features

- 📊 Session tracking with automatic lifecycle management
- 🎯 Event tracking (single and batch)
- 🗺️ Player journey and checkpoint tracking
- 🔄 Automatic retry and queuing for offline support
- 🎮 Easy Unity integration with singleton pattern
- ⚡ Async/await support with Unity coroutines
- 🔐 Secure API key management

## Installation

### Option 1: Unity Package Manager (Recommended)

1. Open Unity Package Manager (Window > Package Manager)
2. Click the '+' button in the top-left corner
3. Select "Add package from git URL"
4. Enter: `https://github.com/yourusername/lvlup-unity-sdk.git`

### Option 2: Manual Installation

1. Download the latest release from the [Releases](https://github.com/yourusername/lvlup-unity-sdk/releases) page
2. Extract the `LvlUp` folder into your Unity project's `Assets/` directory

### Option 3: Copy Files Directly

Copy all files from this repository into your Unity project's `Assets/LvlUp/` folder.

## Quick Start

### 1. Initialize the SDK

Create a new GameObject in your initial scene and attach the `LvlUpManager` script, or initialize programmatically:

```csharp
using LvlUp;

public class GameInitializer : MonoBehaviour
{
    void Start()
    {
        // Initialize with your API key and backend URL
        LvlUpManager.Initialize(
            apiKey: "lvl_your_api_key_here",
            baseUrl: "https://your-backend-url.com/api"
        );
        
        Debug.Log("LvlUp SDK Initialized!");
    }
}
```

### 2. Track Sessions

Sessions are tracked automatically, but you can manually control them:

```csharp
// Start a session (usually automatic)
await LvlUpManager.Instance.StartSession("user_12345", new UserMetadata
{
    deviceId = SystemInfo.deviceUniqueIdentifier,
    platform = Application.platform.ToString(),
    version = Application.version,
    country = "US",
    language = Application.systemLanguage.ToString()
});

// End session (automatic on application quit/pause)
await LvlUpManager.Instance.EndSession();
```

### 3. Track Events

```csharp
// Track a simple event
LvlUpManager.Instance.TrackEvent("level_complete", new Dictionary<string, object>
{
    { "levelId", 5 },
    { "score", 12500 },
    { "stars", 3 },
    { "timeMs", 75000 }
});

// Track multiple events in a batch
var events = new List<LvlUpEvent>
{
    new LvlUpEvent("button_click", new Dictionary<string, object> { { "button", "shop" } }),
    new LvlUpEvent("item_purchase", new Dictionary<string, object> { { "item", "sword" }, { "price", 100 } })
};
await LvlUpManager.Instance.TrackEventsBatch(events);
```

### 4. Track Player Journey

```csharp
// Create a checkpoint (typically done once during game setup)
var checkpoint = await LvlUpManager.Instance.CreateCheckpoint(
    name: "Tutorial Complete",
    description: "Player completed the tutorial",
    type: "tutorial",
    order: 1,
    tags: new[] { "onboarding", "tutorial" }
);

// Record when player reaches a checkpoint
await LvlUpManager.Instance.RecordCheckpoint(
    checkpointId: checkpoint.id,
    metadata: new Dictionary<string, object>
    {
        { "timeSpent", 120 },
        { "attempts", 1 }
    }
);

// Get player's journey progress
var progress = await LvlUpManager.Instance.GetPlayerJourneyProgress();
Debug.Log($"Player completed {progress.completedCheckpoints} checkpoints");
```


## Advanced Usage

### Offline Event Queuing

The SDK automatically queues events when offline and sends them when connection is restored:

```csharp
// Events are automatically queued if offline
LvlUpManager.Instance.TrackEvent("level_start", data);

// Check queue status
int queuedEvents = LvlUpManager.Instance.GetQueuedEventCount();
Debug.Log($"Queued events: {queuedEvents}");

// Manually flush queue
await LvlUpManager.Instance.FlushEventQueue();
```

### Custom Configuration

```csharp
LvlUpManager.Initialize(
    apiKey: "your_api_key",
    baseUrl: "https://your-backend.com/api",
    config: new LvlUpConfig
    {
        enableDebugLogs = true,
        autoTrackSessions = true,
        eventBatchSize = 50,
        eventFlushInterval = 30f,
        maxQueueSize = 1000,
        retryAttempts = 3,
        timeout = 30f
    }
);
```

### Error Handling

```csharp
try
{
    var response = await LvlUpManager.Instance.TrackEvent("test_event", data);
    if (response.success)
    {
        Debug.Log("Event tracked successfully!");
    }
    else
    {
        Debug.LogError($"Failed to track event: {response.error}");
    }
}
catch (System.Exception ex)
{
    Debug.LogError($"Exception tracking event: {ex.Message}");
}
```

## API Reference

### LvlUpManager

Main singleton class for SDK interaction.

#### Methods

- `Initialize(apiKey, baseUrl, config)` - Initialize the SDK
- `StartSession(userId, metadata)` - Start a new session
- `EndSession()` - End current session
- `TrackEvent(eventName, properties)` - Track a single event
- `TrackEventsBatch(events)` - Track multiple events
- `CreateCheckpoint(name, description, type, order, tags)` - Create a checkpoint
- `RecordCheckpoint(checkpointId, metadata)` - Record checkpoint completion
- `GetPlayerJourneyProgress()` - Get player's journey data
- `FlushEventQueue()` - Manually flush queued events

## Examples

Check the `Examples/` folder for complete example scenes:

- `BasicIntegration.cs` - Simple event tracking
- `PlayerJourneyExample.cs` - Checkpoint and journey tracking
- `AIIntegrationExample.cs` - AI chat and insights
- `AdvancedExample.cs` - All features combined

## Requirements

- Unity 2019.4 or later
- .NET Standard 2.0 or higher
- Internet connection (offline queuing available)

## Best Practices

1. **Initialize Early**: Call `LvlUpManager.Initialize()` in your first scene
2. **Use Async/Await**: All network calls return Tasks for better performance
3. **Batch Events**: Use `TrackEventsBatch()` for multiple events to reduce network calls
4. **Handle Errors**: Always check response.success before processing data
5. **Test Offline**: Verify your game works without network connection
6. **Session Management**: Let SDK handle sessions automatically unless you need custom control

## Troubleshooting

### Events not sending?

- Check your API key is correct
- Verify backend URL is accessible
- Check Unity logs for error messages
- Try manual `FlushEventQueue()` to force send

### Session not starting?

- Ensure `autoTrackSessions` is enabled in config
- Verify user ID is being passed correctly
- Check that `StartSession()` is called after Initialize()

### AI features not working?

- Verify your backend has AI features enabled
- Check OpenAI API key is configured in backend
- Ensure sufficient context data exists for AI analysis

## Support

- Documentation: [https://docs.lvlup.com](https://docs.lvlup.com)
- GitHub Issues: [https://github.com/yourusername/lvlup-unity-sdk/issues](https://github.com/yourusername/lvlup-unity-sdk/issues)
- Email: support@lvlup.com

## License

MIT License - see LICENSE file for details

## Contributing

Contributions are welcome! Please read CONTRIBUTING.md for guidelines.

