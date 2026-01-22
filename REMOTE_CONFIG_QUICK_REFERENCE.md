# Remote Config SDK - Quick Reference

## Installation

The Remote Config SDK is included in LvlUp Unity SDK v2.0+

## Basic Setup (2 minutes)

```csharp
using LvlUp;
using LvlUp.Services;

// Initialize LvlUp SDK first
void InitializeSDK()
{
    LvlUpManager.Initialize(
        apiKey: "your_api_key",
        baseUrl: "https://api.yourdomain.com",
        onComplete: (success, message) =>
        {
            if (success)
                InitializeRemoteConfig();
        }
    );
}

// Then initialize Remote Config service
void InitializeRemoteConfig()
{
    LvlUpManager.InitializeRemoteConfig(
        gameId: "cmkpatjfc0000cffjzxuwfh1e",
        environment: "production"
    );
    
    // Fetch configs
    LvlUpManager.FetchRemoteConfigs();
}
```

## Using Configs

```csharp
var remoteConfig = LvlUpManager.RemoteConfig;

// Get values
int coins = remoteConfig.GetInt("daily_reward_coins", 100);
string apiUrl = remoteConfig.GetString("api_endpoint", "https://api.default.com");
bool isPremium = remoteConfig.GetBool("premium_features", false);
float difficulty = remoteConfig.GetFloat("difficulty_level", 1.0f);

// Get complex objects
var config = remoteConfig.GetJson<MyConfig>("server_settings");

// Check if exists
if (remoteConfig.HasKey("special_event"))
    StartSpecialEvent();
```

## Setting Context (for rule evaluation)

```csharp
LvlUpManager.SetRemoteConfigContext(
    platform: "iOS",              // Auto-detected if not set
    version: "1.2.3",             // From Application.version if not set
    country: "US",                // Optional
    segment: "new_users"          // Optional
);
```

## Listening to Updates

```csharp
LvlUpManager.RemoteConfig.OnConfigsUpdated += (evt) =>
{
    Debug.Log($"Got {evt.configs.Count} configs (cached: {evt.isFromCache})");
    RefreshUI();
};
```

## Changing Environment

```csharp
// Switch to staging for testing
LvlUpManager.RemoteConfig.SetEnvironment("staging");
LvlUpManager.FetchRemoteConfigs();

// Back to production
LvlUpManager.RemoteConfig.SetEnvironment("production");
LvlUpManager.FetchRemoteConfigs();
```

## Offline Support

```csharp
// Automatically uses cache when offline
// Just call GetInt/GetString/etc as normal
int reward = LvlUpManager.RemoteConfig.GetInt("coins", 100);
// If offline: uses cache if available, otherwise uses default
```

## Common Patterns

### Full Initialization with LvlUp Manager
```csharp
void Start()
{
    LvlUpManager.Initialize(apiKey, baseUrl, onComplete: (success, msg) =>
    {
        if (success)
        {
            LvlUpManager.InitializeRemoteConfig(gameId, "production");
            LvlUpManager.SetRemoteConfigContext(version: Application.version);
            LvlUpManager.FetchRemoteConfigs();
        }
    });
}
```

### Access from Anywhere
```csharp
// RemoteConfigService is managed by LvlUpManager
var remoteConfig = LvlUpManager.RemoteConfig;
int value = remoteConfig.GetInt("key");
```

### JSON Config with Custom Class
```csharp
[System.Serializable]
public class GameSettings
{
    public int maxLevel;
    public float healthMultiplier;
}

var settings = LvlUpManager.RemoteConfig.GetJson<GameSettings>("game_settings");
```

### Fallback Chain
```csharp
int difficulty = LvlUpManager.RemoteConfig.GetInt("difficulty_level", 
    PlayerPrefs.GetInt("SavedDifficulty", 3)  // Fallback to saved preference
);
```

## Type Mapping

| Method | Input Type | Return Type | Example |
|--------|-----------|-----------|---------|
| `GetInt()` | "123" | `int` | `GetInt("coins", 100)` |
| `GetString()` | "hello" | `string` | `GetString("api_url", "")` |
| `GetBool()` | "true" / "1" | `bool` | `GetBool("premium", false)` |
| `GetFloat()` | "3.14" | `float` | `GetFloat("speed", 1.0f)` |
| `GetJson<T>()` | `{"x":1}` | `T` | `GetJson<Config>("cfg", null)` |

## Public API Methods

### LvlUpManager Static Methods
- `InitializeRemoteConfig(gameId, environment)` - Initialize Remote Config service
- `FetchRemoteConfigs(onComplete)` - Fetch configs from server
- `SetRemoteConfigContext(platform, version, country, segment)` - Set rule evaluation context
- `RemoteConfig` - Property to access RemoteConfigService instance

### RemoteConfigService Methods
- `GetInt(key, defaultValue)` - Get integer config
- `GetString(key, defaultValue)` - Get string config
- `GetBool(key, defaultValue)` - Get boolean config
- `GetFloat(key, defaultValue)` - Get float config
- `GetJson<T>(key, defaultValue)` - Get JSON config and deserialize
- `SetContext(platform, version, country, segment)` - Set evaluation context
- `SetEnvironment(environment)` - Change environment
- `HasKey(key)` - Check if config exists
- `GetAllKeys()` - Get all loaded config keys
- `GetAllConfigs()` - Get all loaded ConfigData objects
- `ClearCache()` - Clear cached configs
- `IsCached()` - Check if current configs are from cache
- `GetCacheAgeMs()` - Get cache age in milliseconds

## Debugging

```csharp
// Check if using cache
if (LvlUpManager.RemoteConfig.IsCached())
{
    long ageMs = LvlUpManager.RemoteConfig.GetCacheAgeMs();
    Debug.Log($"Using cache, {ageMs}ms old");
}

// Clear cache and refetch
LvlUpManager.RemoteConfig.ClearCache();
LvlUpManager.FetchRemoteConfigs();

// List all keys
foreach (var key in LvlUpManager.RemoteConfig.GetAllKeys())
    Debug.Log($"Config: {key}");
```

## Error Handling

```csharp
// All getters have defaults - game continues even if fetch fails
LvlUpManager.FetchRemoteConfigs(success =>
{
    // success = true: fresh from server
    // success = false: using cache or defaults
    // Either way, the game works!
});

// Type mismatches are safe
int value = LvlUpManager.RemoteConfig.GetInt("string_config", 0);  // Returns 0 (default)
```

## Cache Behavior

```
Default Config → Fetch from Server → Cache for 5 minutes
                        ↓
                    If Offline → Use Cache
                        ↓
                    Cache Expired → Fetch New
```

## Performance Tips

1. **Initialize Early**: Call in SDK initialization
2. **Fetch Async**: Use callbacks, don't block
3. **Use Events**: React to updates, don't poll
4. **Batch Getters**: Get multiple values in succession
5. **Cache Lifts**: Check `IsCached()` before critical operations

## Troubleshooting

**Q: Configs not loading?**  
A: Check game ID, ensure backend is running, verify network

**Q: Getting defaults?**  
A: Fetch may have failed, check console for 404/500 errors

**Q: Offline doesn't work?**  
A: Must successfully fetch once to populate cache

**Q: Getting type mismatch?**  
A: Check backend config dataType matches your getter call

**Q: RemoteConfigService is null?**  
A: Call InitializeRemoteConfig(gameId) before using it

## Architecture

Remote Config is now a service managed by LvlUpManager:

```
LvlUpManager (Singleton)
    └── RemoteConfigService
        ├── RemoteConfigHttpClient (HTTP communication)
        ├── RemoteConfigCacheService (Persistence)
        └── Dictionary<string, ConfigData> (In-Memory cache)
```

## Links

- **Full Documentation**: REMOTE_CONFIG_README.md
- **Example Code**: RemoteConfigExample.cs
- **API Docs**: Backend /api/config/configs/{gameId}

## Support

Issues? Check:
1. LvlUpManager is initialized first
2. Game ID is correct
3. Base URL is correct
4. Environment is set
5. Network connection exists
6. Backend is running

