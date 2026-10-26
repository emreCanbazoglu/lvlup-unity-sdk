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

## Adjust install attribution (1.9.0)

For games already shipping Adjust Unity SDK 5.6.0 under `Assets/Adjust`, add
`lvlup_adjust_enabled` to **Player Settings > Other Settings > Scripting Define Symbols**
for both **iOS** and **Android**. No other LvlUp setup is required.

LvlUp reads Adjust on initialization and each new session, including Organic installs.
If attribution is not ready, it retries after 10, 30, 60, 120 and 300 seconds, then waits
until the next session. Callbacks are processed on the main thread. Only changes to
normalized attribution or user/device identity are sent; the last successful value is
saved locally. Failed sends remain pending for the next session in the current SDK instance.
The observation timestamp does not trigger a resend. Without the define, manual
attribution remains available and no Adjust API is referenced by C# code.

```csharp
// After LvlUp initialization:
LvlUpSDK.Attribution.Refresh(); // Re-read Adjust immediately.
LvlUpSDK.Attribution.Set("Organic"); // Manual provider, also usable without Adjust.
LvlUpSDK.Attribution.Set("Partner", campaign: "autumn", trackerName: "promo");
```

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
            baseUrl: "https://your-backend-url.com/api",
            config: null,
            onComplete: (success, message) =>
            {
                if (success)
                {
                    Debug.Log($"✅ LvlUp SDK Ready: {message}");
                }
                else
                {
                    Debug.LogError($"❌ LvlUp Init Failed: {message}");
                }
            }
        );
    }
}
```

**Note:** With `autoTrackSessions = true` (default), sessions start automatically. The callback fires when the SDK and session are ready.

### 2. Track Sessions

Sessions are tracked automatically by default, but you can manually control them:

```csharp
// Sessions start automatically when you Initialize (if autoTrackSessions = true)
LvlUpManager.Initialize(apiKey, baseUrl, null, (success, message) =>
{
    if (success)
    {
        Debug.Log("Session started automatically!");
    }
});

// Or manually start a session (if autoTrackSessions = false)
await LvlUpManager.Instance.StartSession("user_12345", new UserMetadata
{
    deviceId = SystemInfo.deviceUniqueIdentifier,
    platform = Application.platform.ToString(),
    version = Application.version,
    country = "US",
    language = Application.systemLanguage.ToString()
});

// End session (automatic on application quit)
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

// Or use LvlUpEvents helper for standard events - much cleaner!
LvlUpEvents.TrackLevelComplete(5, 12500, 75f, new Dictionary<string, object> { { "stars", 3 } });

// Track multiple events in a batch
var events = new List<LvlUpEvent>
{
    new LvlUpEvent("button_click", new Dictionary<string, object> { { "button", "shop" } }),
    new LvlUpEvent("item_purchase", new Dictionary<string, object> { { "item", "sword" }, { "price", 100 } })
};
await LvlUpManager.Instance.TrackEventsBatch(events);
```

### Standard Event Helpers (LvlUpEvents)

The SDK provides a static `LvlUpEvents` helper class for common game events to ensure consistency:

```csharp
// Level start
LvlUpEvents.TrackLevelStart(levelId);
LvlUpEvents.TrackLevelStart(levelId, "Level Name", additionalProperties);

// Level complete
LvlUpEvents.TrackLevelComplete(levelId, score, timeSeconds);
LvlUpEvents.TrackLevelComplete(levelId, score, timeSeconds, additionalProperties);
LvlUpEvents.TrackLevelComplete(levelId, score, timeSeconds, winMargin, additionalProperties);

// Level failed
LvlUpEvents.TrackLevelFailed(levelId, reason, timeSeconds);
LvlUpEvents.TrackLevelFailed(levelId, reason, timeSeconds, attempts, additionalProperties);
LvlUpEvents.TrackLevelFailed(levelId, reason, timeSeconds, attempts, objectiveProgress, additionalProperties);
```

#### How close was it? (`winMargin` and `objectiveProgress`)

Send these two values so the level funnel can show how close wins and fails were. Both are in [0, 1]:

| Value | Event | Meaning | Dashboard columns |
|---|---|---|---|
| `winMargin` | `TrackLevelComplete` | Remaining ÷ starting resource at the win (balls, moves, time, lives...). 0 = won with nothing left. | Avg Win Margin, Near-miss Wins % (margin ≤ 0.1) |
| `objectiveProgress` | `TrackLevelFailed` | Share of the level objective done at the fail. | Avg Fail Progress, Near-miss Fails % (progress ≥ 0.9) |

```csharp
// Won with 2 of 8 balls left
LvlUpEvents.TrackLevelComplete(levelId, score, timeSeconds, winMargin: 2f / 8f);

// Failed with 9 of 10 targets cleared
LvlUpEvents.TrackLevelFailed(levelId, "out_of_balls", timeSeconds, attempts, objectiveProgress: 0.9f);
```

Pick the resource that best shows how hard the level was and keep it the same for every level of a game. Values outside [0, 1] are dropped with a warning.

**Benefits:** Clean static API, consistent event structure, auto-timestamps, better analytics.

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


## Economy (wallet) tracking

Use `LvlUpSDK.Economy` after initialization. Track each change once, where the wallet is
mutated. `amount` is the actual change after clamping; skip zero changes. Pass the
GameBase `IWorthSource.GetSourceId()` / `WorthSourceId` as `reason`.

```csharp
using System.Collections.Generic;
using LvlUp;
using LvlUp.Models;

// After Initialize returns, before the first session starts:
// use autoTrackSessions = false if setup must precede the initial session snapshot.
LvlUpSDK.Economy.RegisterResource("coin", ResourceType.Currency);
LvlUpSDK.Economy.RegisterResource("gem", ResourceType.Currency);
LvlUpSDK.Economy.RegisterResource("booster_a", ResourceType.Booster);
LvlUpSDK.Economy.SetBalanceProvider(() => new Dictionary<string, double>
{
    { "coin", wallet.Coins },
    { "gem", wallet.Gems },
    { "booster_a", wallet.BoosterA }
});
LvlUpSDK.Session.Start(playerId);

// GameBase-style gain: use the worth source id, and measure the real applied delta.
double before = wallet.Coins;
wallet.IncreaseCoins(requestedGrant); // May clamp at the wallet maximum.
double gained = wallet.Coins - before;
if (gained > 0)
    LvlUpSDK.Economy.Source("coin", gained, worthSource.GetSourceId(), wallet.Coins);

// Spend: likewise measure the real decrease. Balance may be omitted with a provider.
before = wallet.Coins;
wallet.DecreaseCoins(continueCost);
double spent = before - wallet.Coins;
if (spent > 0)
    LvlUpSDK.Economy.Sink("coin", spent, "continue");

// After consuming a booster (do not derive spends from level-event booster properties):
wallet.UseBoosterA();
LvlUpSDK.Economy.Sink("booster_a", 1, "booster_use");

// After applying a coin-to-booster exchange, report its actual applied legs together.
// Trade reads final balances once and generates one shared transactionId.
double coinsBefore = wallet.Coins;
double boostersBefore = wallet.BoosterA;
wallet.BuyBoosterA();
LvlUpSDK.Economy.Trade("booster_purchase", "booster_a",
    sinks: new[] { ("coin", coinsBefore - wallet.Coins) },
    sources: new[] { ("booster_a", wallet.BoosterA - boostersBefore) });

LvlUpSDK.Economy.Snapshot(); // Manual full-wallet snapshot from the provider.
```

The wallet and worth-source methods above represent your game's own APIs. Never
also send separate Source/Sink calls for the same trade. Only report nonzero trade
legs after a successful exchange.

The SDK sends `economy_flow` and `economy_snapshot` through the ordinary event queue.
Resource ids and reasons are lower-cased, with unsupported characters replaced by
`_`. Resource ids must contain 1–64 characters; reasons are limited to 64 characters
and empty reasons become `unknown`. Item ids retain their casing and must be at most
64 characters. Invalid inputs and provider failures are logged and dropped.

With a provider installed, snapshots run automatically on session start and after
each `LvlUpEvents.TrackLevelStart` event. Registered resources missing from a snapshot
are included as zero; provider-only resources are also included. Snapshots have at
most 50 keys; an oversized wallet is logged and dropped. Explicit manual balances
can be supplied to `Snapshot(balances)` without a provider. Missing flow balances
require a provider containing that resource; a missing flow balance never defaults
to zero.

Flows capture `levelOrder` when known and `inLevel` from standard level helpers.
Snapshots capture `levelOrder`. Optional `LvlUpConfig.economyCoalesceSeconds` defaults
to `0` (off). A positive value merges sources sharing resource, reason, level order,
in-level state and item id, retaining the final balance. Sinks and trades are never
merged. Pending sources flush on window expiry, spends/trades of that resource,
snapshots, SDK flush, pause and quit. Call the economy API on Unity's main thread,
as with the other tracking APIs. The tuple-based Trade API requires Unity 2021+
with C# 9 support.

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

### Ad placements and revenue

- `LvlUpSDK.Ads.SetPlacement(adType, placement)` - Tag the next full-screen ad or all banner/MRec refreshes with a game-defined usage name.
- `LvlUpSDK.Ads.ClearPlacement(adType)` - Clear a pending or sticky name.
- `LvlUpSDK.Revenue.TrackAdImpression(adNetworkName, adFormat, revenue, ...)` - Track an impression manually; an explicit placement takes precedence over a custom name.

See [Custom placements](AD_MONETIZATION_README.md#custom-placements) for expiry, precedence, and MAX examples.

### In-app purchase placements (1.10.0+)

- `LvlUpSDK.Revenue.SetPurchasePlacement(placement, productId = null)` - Record where the next purchase (of one product, or any) starts; call it right before opening the store sheet. Consumed by the next matching purchase, expires after 10 minutes.
- `LvlUpSDK.Revenue.ClearPurchasePlacement(productId = null)` - Clear it when a purchase is cancelled or fails.
- `LvlUpSDK.Revenue.TrackInAppPurchase(..., placement: "shop")` - An explicit placement takes precedence over a pending one.

See [Revenue tracking API](REVENUE_TRACKING_API.md#lvlupsdkrevenuesetpurchaseplacement-1100) for precedence and a Unity IAP example.

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

