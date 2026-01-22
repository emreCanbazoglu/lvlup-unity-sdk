# Remote Config System - Unity SDK Integration

## Overview

The Remote Config System is a service managed by LvlUpManager that allows game developers to update game parameters remotely without requiring app updates. The SDK automatically fetches configurations from the backend, caches them locally, and provides easy-to-use typed getters for accessing different data types.

## Architecture

Remote Config is now integrated into LvlUpManager as a managed service:

```
LvlUpManager (Singleton)
    └── RemoteConfigService
        ├── RemoteConfigHttpClient (HTTP communication)
        ├── RemoteConfigCacheService (Persistence)
        └── Dictionary<string, ConfigData> (In-Memory)
```

This design provides:
- **Unified SDK**: Single initialization point for all LvlUp features
- **Shared Resources**: Reuses LvlUpManager's base URL and configuration
- **Lifecycle Management**: Remote Config lifecycle tied to LvlUp SDK lifecycle
- **Easy Access**: Access configs from anywhere via `LvlUpManager.RemoteConfig`

## Features

- **Type-Safe Getters**: Get configs as Int, String, Bool, Float, or JSON
- **Automatic Caching**: Configs are cached locally with a 5-minute TTL
- **Offline Support**: Game works with cached values when network is unavailable
- **Server-Side Rule Evaluation**: Configurations targeted by platform, version, country, segment
- **Retry Logic**: Automatic retries with exponential backoff when fetching fails
- **Events**: Subscribe to config update events for dynamic UI/game state updates
- **Managed Service**: Lifecycle managed by LvlUpManager

## Installation

The Remote Config Service is included in the LvlUp Unity SDK. Ensure you have the latest SDK version.

## Quick Start

### 1. Initialize LvlUp SDK

```csharp
LvlUpManager.Initialize(
    apiKey: "your_api_key",
    baseUrl: "https://api.lvlup.com",
    onComplete: (success, message) =>
    {
        if (success)
        {
            InitializeRemoteConfig();
        }
    }
);
```

### 2. Initialize Remote Config Service

```csharp
void InitializeRemoteConfig()
{
    LvlUpManager.InitializeRemoteConfig(
        gameId: "your_game_id",
        environment: "production"
    );
}
```

### 3. Set Context (Optional)

Set context information for server-side rule evaluation:

```csharp
LvlUpManager.SetRemoteConfigContext(
    platform: "iOS",      // Auto-detected if not specified
    version: "1.2.3",     // From Application.version by default
    country: "US",
    segment: "new_users"
);
```

### 4. Fetch Configs

```csharp
LvlUpManager.FetchRemoteConfigs(success =>
{
    if (success)
    {
        Debug.Log("Configs loaded successfully!");
    }
});
```

### 5. Use Configs

```csharp
var remoteConfig = LvlUpManager.RemoteConfig;

// Get typed values
int dailyReward = remoteConfig.GetInt("daily_reward_coins", defaultValue: 100);
string apiUrl = remoteConfig.GetString("api_endpoint", defaultValue: "");
bool premiumEnabled = remoteConfig.GetBool("premium_features", defaultValue: false);
float multiplier = remoteConfig.GetFloat("difficulty_multiplier", defaultValue: 1.0f);

// Get JSON configs
var serverConfig = remoteConfig.GetJson<MyConfigClass>("server_settings");
```

## API Reference

### LvlUpManager Static Methods

**Initialization:**
- `InitializeRemoteConfig(gameId, environment = "production")` - Initialize Remote Config service

**Operations:**
- `FetchRemoteConfigs(onComplete = null)` - Fetch configs from server
- `SetRemoteConfigContext(platform, version, country, segment)` - Set evaluation context
- `RemoteConfig` - Property to access RemoteConfigService instance

### RemoteConfigService Instance Methods

**Context Management:**
- `SetContext(platform, version, country, segment)` - Set evaluation context
- `SetEnvironment(environment)` - Change environment (dev/staging/prod)

**Fetching:**
- `FetchAsync(coroutineRunner, onComplete)` - Fetch configs from server with retry logic

**Getters:**
- `GetInt(key, defaultValue)` - Get integer config
- `GetString(key, defaultValue)` - Get string config
- `GetBool(key, defaultValue)` - Get boolean config
- `GetFloat(key, defaultValue)` - Get float config
- `GetJson<T>(key, defaultValue)` - Get JSON config and deserialize

**Utility:**
- `HasKey(key)` - Check if config exists
- `GetAllKeys()` - Get all loaded config keys
- `GetAllConfigs()` - Get all loaded ConfigData objects
- `ClearCache()` - Clear cached configs
- `IsCached()` - Check if current configs are from cache
- `GetCacheAgeMs()` - Get cache age in milliseconds
- `IsInitialized` - Property: check if service is initialized

### Events

- `OnConfigsUpdated` - Fired when configs are loaded or updated
  - Parameters: `ConfigsUpdatedEvent` containing configs list, isFromCache flag, and timestamp

## Configuration Types

Configs can have different data types:

| Type | Usage | Example |
|------|-------|---------|
| **int** | Integer values | `GetInt("daily_reward_coins")` |
| **string** | Text values | `GetString("api_endpoint")` |
| **bool** | Boolean flags | `GetBool("enable_feature")` |
| **float** | Decimal values | `GetFloat("difficulty_multiplier")` |
| **json** | Complex objects | `GetJson<T>("config_name")` |

## Caching Strategy

- **TTL**: 5 minutes (300 seconds)
- **Storage**: PlayerPrefs (local device storage)
- **Validation**: Cache is validated against environment and timestamp
- **Fallback**: If fetch fails, cached values are used automatically
- **Isolation**: Separate cache for each environment and game ID

## Offline Support

The SDK handles offline scenarios gracefully:

1. First fetch attempt goes to server
2. If server is unreachable, local cache is used
3. Retry logic continues in background
4. When connection is restored, fresh configs are fetched

Example:
```csharp
// Works even without internet
int reward = LvlUpManager.RemoteConfig.GetInt("daily_reward_coins", 100);
```

## Server-Side Rule Evaluation

Configurations can have rules that match specific conditions:

### Platform Rules
```
Example: iOS users version 3.5.0+ get 150 coins
         Android users get default 100 coins
```

### Version Rules
```
Example: Version >= 2.0.0 gets new feature
         Version < 2.0.0 gets legacy feature
```

### Country Rules
```
Example: Germany (DE) gets special promotion
         Other countries get default config
```

### Date-Based Rules
```
Example: Special event runs between specific dates
         Event config available only during active period
```

### Segment Rules (Future)
```
Example: New users vs returning users
         Premium vs free players
```

The server evaluates all matching rules and returns the appropriate value based on priority.

## Error Handling

The SDK includes built-in error handling:

```csharp
LvlUpManager.FetchRemoteConfigs(success =>
{
    if (!success)
    {
        // Use defaults - app continues to work
        int reward = LvlUpManager.RemoteConfig.GetInt("daily_reward_coins", 100);
    }
});
```

## Advanced Usage

### Listening to Config Updates

```csharp
LvlUpManager.RemoteConfig.OnConfigsUpdated += (evt) =>
{
    Debug.Log($"Loaded {evt.configs.Count} configs");
    Debug.Log($"From cache: {evt.isFromCache}");
    
    // Update UI or game state
    RefreshGameUI();
};
```

### Switching Environments

```csharp
// Switch to staging for testing
LvlUpManager.RemoteConfig.SetEnvironment("staging");
LvlUpManager.FetchRemoteConfigs();

// Verify it switched
if (LvlUpManager.RemoteConfig.IsCached())
{
    Debug.Log("Using cached staging configs");
}
```

### JSON Config Example

```csharp
[System.Serializable]
public class ServerSettings
{
    public string host;
    public int port;
    public string[] endpoints;
}

// Fetch and use
var settings = LvlUpManager.RemoteConfig.GetJson<ServerSettings>("server_settings");
if (settings != null)
{
    ConnectToServer(settings.host, settings.port);
}
```

### Checking Cache Status

```csharp
if (LvlUpManager.RemoteConfig.IsCached())
{
    long ageMs = LvlUpManager.RemoteConfig.GetCacheAgeMs();
    Debug.Log($"Using cache, age: {ageMs}ms");
}
```

### Batch Operations

```csharp
var remoteConfig = LvlUpManager.RemoteConfig;

// Fetch all configs at once
LvlUpManager.FetchRemoteConfigs(success =>
{
    if (success)
    {
        // Access all at once
        int reward = remoteConfig.GetInt("daily_reward");
        string url = remoteConfig.GetString("api_url");
        bool premium = remoteConfig.GetBool("premium_enabled");
    }
});
```

## Best Practices

1. **Initialize Early**: Initialize both LvlUpManager and RemoteConfig during app startup
2. **Set Context**: Always set context (platform, version, etc.) for accurate rule evaluation
3. **Use Defaults**: Always provide sensible defaults in getter calls
4. **Listen to Updates**: Subscribe to OnConfigsUpdated for dynamic behavior changes
5. **Test Environments**: Use different environment values for dev/staging/production
6. **Error Tolerance**: Design game logic to work with default values if fetch fails
7. **Cache Awareness**: Check IsCached() before relying on specific features
8. **Batch Fetches**: Call FetchRemoteConfigs once during initialization, not repeatedly

## Performance Characteristics

| Metric | Value |
|--------|-------|
| **Cache TTL** | 5 minutes |
| **Retry Delay** (initial) | 1 second |
| **Max Retries** | 3 attempts |
| **Network Timeout** | 30 seconds |
| **Cache Storage** | PlayerPrefs (platform-managed) |
| **Memory Footprint** | ~1-5 KB per 100 configs |

## Integration with LvlUp SDK

Remote Config integrates seamlessly with the main LvlUp SDK:

```csharp
// Single initialization point
LvlUpManager.Initialize(apiKey, baseUrl, config, onComplete: (success, msg) =>
{
    if (success)
    {
        // All LvlUp services available
        LvlUpManager.InitializeRemoteConfig(gameId);
        LvlUpManager.StartSession(userId);
        LvlUpManager.RemoteConfig.FetchAsync(this);
    }
});

// All services share:
// - Base URL
// - API Key
// - Configuration
// - Network client
// - Lifecycle
```

## Migration from Standalone RemoteConfigManager

If you were using the standalone `RemoteConfigManager` before:

**Old Pattern (Deprecated):**
```csharp
RemoteConfigManager.Initialize(gameId, baseUrl, environment);
var value = RemoteConfigManager.Instance.GetInt("key");
```

**New Pattern (Current):**
```csharp
LvlUpManager.Initialize(apiKey, baseUrl);
LvlUpManager.InitializeRemoteConfig(gameId);
var value = LvlUpManager.RemoteConfig.GetInt("key");
```

The new pattern integrates with the rest of the LvlUp SDK for a more cohesive experience.

## Troubleshooting

### Configs Not Loading
```csharp
// Check if RemoteConfig is initialized
if (!LvlUpManager.RemoteConfig.IsInitialized)
{
    Debug.Log("Remote Config not initialized");
    LvlUpManager.InitializeRemoteConfig(gameId);
}

// Check for network errors
LvlUpManager.FetchRemoteConfigs(success =>
{
    Debug.Log($"Fetch result: {success}");
});
```

### Cache Not Working
```csharp
// Clear cache and retry
LvlUpManager.RemoteConfig.ClearCache();
LvlUpManager.FetchRemoteConfigs();
```

### Type Conversion Issues
Always use appropriate getters and defaults:

```csharp
// Don't do this - will use default
int value = LvlUpManager.RemoteConfig.GetInt("string_config");

// Do this
string value = LvlUpManager.RemoteConfig.GetString("string_config");
```

## Support

For issues or questions:
- Check the Examples folder for sample code
- Review server logs for fetch errors
- Ensure game ID and base URL are correct
- Verify network connectivity in editor console

## Version History

- **2.0.0** - Integrated into LvlUpManager as managed service
- **1.0.0** - Initial release as standalone manager

