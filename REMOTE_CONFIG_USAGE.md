# Remote Config Usage Guide

## Overview

The LvlUp SDK provides a powerful Remote Config system that allows you to:
- Change game behavior without deploying new builds
- A/B test features and game parameters
- Roll out features gradually
- Personalize experiences based on platform, country, or custom segments

## Quick Start

### 1. Check if Remote Config is Ready

Always check if Remote Config is initialized before using it:

```csharp
if (LvlUpSDK.Config.IsReady)
{
    // Safe to use remote config
}
```

### 2. Try to Get Config Values

Use the TryGet pattern to safely retrieve config values:

```csharp
// Try to get integer
if (LvlUpSDK.Config.TryGetInt("daily_reward_coins", out int coins))
{
    // Key found, use coins
    Debug.Log($"Coins: {coins}");
}
else
{
    // Key not found, use default
    coins = 100;
}

// Try to get string
if (LvlUpSDK.Config.TryGetString("welcome_message", out string message))
{
    ShowWelcome(message);
}
else
{
    ShowWelcome("Welcome!"); // Default
}

// Try to get boolean
if (LvlUpSDK.Config.TryGetBool("new_feature_enabled", out bool enabled))
{
    if (enabled) EnableFeature();
}

// Try to get float
if (LvlUpSDK.Config.TryGetFloat("player_speed", out float speed))
{
    playerSpeed = speed;
}
```

### 3. Try to Get Complex JSON Objects

For complex configurations, use JSON deserialization with TryGet:

```csharp
[System.Serializable]
public class RewardConfig
{
    public int coins;
    public int gems;
}

if (LvlUpSDK.Config.TryGetJson<RewardConfig>("daily_reward", out RewardConfig reward))
{
    Debug.Log($"Coins: {reward.coins}, Gems: {reward.gems}");
}
else
{
    // Use default reward
    reward = new RewardConfig { coins = 100, gems = 5 };
}
```

## API Reference

### Static Methods

#### `LvlUpSDK.Config.IsReady`
Check if Remote Config service is initialized and ready to use.

```csharp
bool ready = LvlUpSDK.Config.IsReady;
```

**Returns:** `true` if initialized, `false` otherwise

---

#### `LvlUpSDK.Config.TryGetInt(key, out value)`
Try to get an integer config value.

```csharp
if (LvlUpSDK.Config.TryGetInt("max_level", out int maxLevel))
{
    // maxLevel contains the value
}
```

**Parameters:**
- `key` - Config key
- `value` - Output parameter that receives the value if found

**Returns:** `true` if key exists and value retrieved, `false` otherwise

---

#### `LvlUpSDK.Config.TryGetString(key, out value)`
Try to get a string config value.

```csharp
if (LvlUpSDK.Config.TryGetString("player_name", out string name))
{
    // name contains the value
}
```

**Parameters:**
- `key` - Config key
- `value` - Output parameter that receives the value if found

**Returns:** `true` if key exists and value retrieved, `false` otherwise

---

#### `LvlUpSDK.Config.TryGetBool(key, out value)`
Try to get a boolean config value.

```csharp
if (LvlUpSDK.Config.TryGetBool("feature_enabled", out bool enabled))
{
    // enabled contains the value
}
```

**Parameters:**
- `key` - Config key
- `value` - Output parameter that receives the value if found

**Returns:** `true` if key exists and value retrieved, `false` otherwise

---

#### `LvlUpSDK.Config.TryGetFloat(key, out value)`
Try to get a float config value.

```csharp
if (LvlUpSDK.Config.TryGetFloat("speed_multiplier", out float multiplier))
{
    // multiplier contains the value
}
```

**Parameters:**
- `key` - Config key
- `value` - Output parameter that receives the value if found

**Returns:** `true` if key exists and value retrieved, `false` otherwise

---

#### `LvlUpSDK.Config.TryGetJson<T>(key, out value)`
Try to get a JSON config value and deserialize to type T.

```csharp
if (LvlUpSDK.Config.TryGetJson<MyClass>("config_data", out MyClass data))
{
    // data contains the deserialized object
}
```

**Parameters:**
- `key` - Config key
- `value` - Output parameter that receives the deserialized object if successful

**Returns:** `true` if key exists and deserialization successful, `false` otherwise

---

#### `LvlUpSDK.Config.HasKey(key)`
Check if a config key exists.

```csharp
bool exists = LvlUpSDK.Config.HasKey("key");
```

**Parameters:**
- `key` - Config key to check

**Returns:** `true` if key exists, `false` otherwise

---

#### `LvlUpSDK.Config.Refresh(onComplete)`
Manually refresh remote configs from server.

```csharp
LvlUpSDK.Config.Refresh(success =>
{
    if (success)
        Debug.Log("Config refreshed!");
});
```

**Parameters:**
- `onComplete` - Callback with success status (optional)

---

## Common Patterns

### Pattern 1: Wait for Config Before Showing UI

```csharp
IEnumerator Start()
{
    // Show loading screen
    loadingScreen.SetActive(true);
    
    // Wait for remote config
    while (!LvlUpSDK.Config.IsReady)
    {
        yield return new WaitForSeconds(0.5f);
    }
    
    // Apply config and show game
    ApplyRemoteConfig();
    loadingScreen.SetActive(false);
}
```

### Pattern 2: Feature Flags

```csharp
void CheckFeatureFlags()
{
    if (LvlUpSDK.Config.TryGetBool("show_new_shop", out bool showShop) && showShop)
    {
        newShopButton.SetActive(true);
    }
    
    if (LvlUpSDK.Config.TryGetBool("enable_pvp", out bool enablePvp) && enablePvp)
    {
        pvpMenu.SetActive(true);
    }
}
```

### Pattern 3: A/B Testing

```csharp
void SetupABTest()
{
    string variant = "control"; // Default
    if (LvlUpSDK.Config.TryGetString("button_color_test", out string testVariant))
    {
        variant = testVariant;
    }
    
    switch (variant)
    {
        case "red":
            buyButton.color = Color.red;
            break;
        case "green":
            buyButton.color = Color.green;
            break;
        default:
            buyButton.color = Color.white;
            break;
    }
}
```

### Pattern 4: Dynamic Difficulty

```csharp
[System.Serializable]
public class DifficultyConfig
{
    public float enemyHealth;
    public float enemySpeed;
    public int enemyCount;
}

void ApplyDifficulty(int level)
{
    string key = $"level_{level}_difficulty";
    
    if (LvlUpSDK.Config.TryGetJson<DifficultyConfig>(key, out DifficultyConfig config))
    {
        enemySpawner.health = config.enemyHealth;
        enemySpawner.speed = config.enemySpeed;
        enemySpawner.count = config.enemyCount;
    }
    else
    {
        // Use default difficulty
        enemySpawner.health = 100f;
        enemySpawner.speed = 1f;
        enemySpawner.count = 5;
    }
}
```

### Pattern 5: Economy Balancing

```csharp
[System.Serializable]
public class ShopPrices
{
    public int coinPack1;
    public int coinPack2;
    public int coinPack3;
}

void LoadShopPrices()
{
    if (LvlUpSDK.Config.TryGetJson<ShopPrices>("shop_prices", out ShopPrices prices))
    {
        coinPack1Price.text = $"${prices.coinPack1}";
        coinPack2Price.text = $"${prices.coinPack2}";
        coinPack3Price.text = $"${prices.coinPack3}";
    }
    else
    {
        // Use default prices
        coinPack1Price.text = "$0.99";
        coinPack2Price.text = "$4.99";
        coinPack3Price.text = "$9.99";
    }
}
```

## Best Practices

### 1. Always Handle the False Case
The TryGet pattern returns false if the key doesn't exist. Always handle this:

```csharp
// ✅ Good - Handles both success and failure
if (LvlUpSDK.Config.TryGetInt("max_level", out int maxLevel))
{
    UseMaxLevel(maxLevel);
}
else
{
    UseMaxLevel(100); // Sensible default
}

// ❌ Bad - Doesn't handle failure case
LvlUpSDK.Config.TryGetInt("max_level", out int maxLevel);
UseMaxLevel(maxLevel); // maxLevel will be 0 if key not found!
```

### 2. Check Ready State
Always check if RemoteConfig is ready before using:

```csharp
// ✅ Good
if (LvlUpSDK.Config.IsReady)
{
    if (LvlUpSDK.Config.TryGetInt("key", out int value))
    {
        UseValue(value);
    }
}

// ℹ️ Also Good - TryGet automatically checks if ready
if (LvlUpSDK.Config.TryGetInt("key", out int value))
{
    UseValue(value); // Only called if ready AND key exists
}
```

### 3. Use Inline Conditionals for Simple Cases
For simple feature flags, use inline conditionals:

```csharp
// ✅ Good - Clean and concise
if (LvlUpSDK.Config.TryGetBool("show_feature", out bool show) && show)
{
    ShowFeature();
}

// ❌ Verbose
if (LvlUpSDK.Config.TryGetBool("show_feature", out bool show))
{
    if (show)
    {
        ShowFeature();
    }
}
```

### 4. Provide Meaningful Defaults
Always provide sensible defaults that keep your game playable:

```csharp
// ✅ Good - Game remains playable with defaults
int dailyReward = 100;
if (LvlUpSDK.Config.TryGetInt("daily_reward", out int configReward))
{
    dailyReward = configReward;
}

// ❌ Bad - Game might break with zero values
int dailyReward = 0;
LvlUpSDK.Config.TryGetInt("daily_reward", out dailyReward);
// If key doesn't exist, dailyReward stays 0!
```

### 5. Cache Values Locally
Don't query remote config repeatedly in Update():

```csharp
// ✅ Good - Cache value
private float _playerSpeed = 5.0f; // Default

void Start()
{
    if (LvlUpSDK.Config.TryGetFloat("player_speed", out float speed))
    {
        _playerSpeed = speed;
    }
}

void Update()
{
    transform.position += Vector3.forward * _playerSpeed * Time.deltaTime;
}

// ❌ Bad - Queries every frame
void Update()
{
    if (LvlUpSDK.Config.TryGetFloat("player_speed", out float speed))
    {
        transform.position += Vector3.forward * speed * Time.deltaTime;
    }
}
```

## Testing in Editor

### Debug Window
Use the LvlUp Debug Window (Window > LvlUp > Debug Window) to:
- View all config values in real-time
- Switch environments (production, staging, development)
- Simulate different platforms (iOS, Android, etc.)
- Manually refresh configs

### Platform Simulation
Test platform-specific configs without building:

1. Open Debug Window
2. Select platform (iOS, Android, etc.)
3. Click "Apply Platform"
4. Enter Play mode
5. Configs will be fetched for that platform

## Troubleshooting

### "RemoteConfig not ready" Warning
**Cause:** Trying to use config before it's initialized  
**Solution:** Wait for `IsRemoteConfigReady()` to return true

### Getting Default Values
**Cause:** Config key doesn't exist or hasn't been fetched yet  
**Solution:** 
- Check key name spelling
- Verify config exists in backend
- Wait for initial fetch to complete

### JSON Deserialization Fails
**Cause:** Class structure doesn't match JSON  
**Solution:**
- Add `[System.Serializable]` to your class
- Match property names exactly (case-sensitive)
- Use Unity's JsonUtility naming conventions

## Advanced Usage

### Direct Service Access
For advanced use cases, access the service directly:

```csharp
using LvlUp.Services;

RemoteConfigService service = LvlUpManager.Instance.GetRemoteConfigService();

if (service != null && service.IsInitialized)
{
    // Access all keys
    foreach (string key in service.GetAllKeys())
    {
        Debug.Log($"Key: {key}");
    }
    
    // Clear cache
    service.ClearCache();
    
    // Check cache age
    long ageMs = service.GetCacheAgeMs();
}
```

### Event Subscription
Subscribe to config update events:

```csharp
using LvlUp.RemoteConfig;

RemoteConfigService service = LvlUpManager.Instance.GetRemoteConfigService();
service.OnConfigsUpdated += OnConfigsUpdated;

void OnConfigsUpdated(ConfigsUpdatedEvent evt)
{
    Debug.Log($"Configs updated! Count: {evt.configs.Count}, From cache: {evt.isFromCache}");
    ReloadAllConfigs();
}
```

## See Also

- [API Reference](API_REFERENCE.md)
- [Quick Start Guide](QUICKSTART.md)
- [Remote Config Dashboard](https://dashboard.lvlup.com/config)

