# 🎮 LvlUp Unity SDK - Quick Reference Card

## Initialization
```csharp
LvlUpManager.Initialize("lvl_your_api_key", "https://api.url.com/api");
```

## Session
```csharp
// Start
LvlUpManager.Instance.StartSession("user_id", metadata);

// End
LvlUpManager.Instance.EndSession();
```

## Events
```csharp
// Single event
LvlUpManager.Instance.TrackEvent("event_name", new Dictionary<string, object>
{
    { "key", value }
});

// Batch
var events = new List<LvlUpEvent> { ... };
LvlUpManager.Instance.TrackEventsBatch(events);

// Flush
LvlUpManager.Instance.FlushEventQueue();
```

## Player Journey
```csharp
// Create checkpoint
LvlUpManager.Instance.CreateCheckpoint(
    name: "Tutorial Complete",
    description: "...",
    type: "tutorial",
    order: 1
);

// Record checkpoint
LvlUpManager.Instance.RecordCheckpoint(checkpointId);

// Get progress
LvlUpManager.Instance.GetPlayerJourneyProgress(callback);
```

## AI
```csharp
// Chat
LvlUpManager.Instance.SendAIMessage("question", context, callback);

// Insights
LvlUpManager.Instance.GetAIInsights(filters, callback);
```

## Config
```csharp
var config = new LvlUpConfig
{
    enableDebugLogs = true,
    eventBatchSize = 50,
    eventFlushInterval = 30f
};
LvlUpManager.Initialize(apiKey, url, config);
```

## Common Events
```csharp
// Level
"level_start", "level_complete", "level_fail"

// Gameplay
"player_death", "powerup_used", "item_collected"

// UI
"button_click", "screen_view"

// Monetization
"purchase", "ad_viewed"

// Tutorial
"tutorial_start", "tutorial_complete"
```

## Callbacks
```csharp
LvlUpManager.Instance.TrackEvent("event", data, response =>
{
    if (response.success)
        Debug.Log("Success!");
    else
        Debug.LogError(response.error);
});
```

## Utilities
```csharp
// Check if initialized
bool isInit = LvlUpManager.Instance.IsInitialized();

// Get current session
SessionData session = LvlUpManager.Instance.GetCurrentSession();

// Get user ID
string userId = LvlUpManager.Instance.GetCurrentUserId();

// Queue count
int count = LvlUpManager.Instance.GetQueuedEventCount();
```

---

## 📚 Documentation
- **README.md** - Overview & features
- **QUICKSTART.md** - 5-minute guide
- **INTEGRATION_GUIDE.md** - Detailed steps
- **API_REFERENCE.md** - Complete API
- **Examples/** - Working code samples

## 🚀 Free Deployment
- Backend: Render.com (FREE)
- Frontend: Vercel (FREE)
- Database: Neon.tech (FREE)

## ⏱️ Timeline
- Deploy: 2-4 hours
- Unity Basic: 30 minutes
- Unity Complete: 1-2 days

---

**LvlUp Unity SDK v1.0.0** | MIT License | Unity 2019.4+

