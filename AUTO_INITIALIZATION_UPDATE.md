# AUTO-INITIALIZATION UPDATE

## What Changed

The `AdMonetizationService` now **automatically initializes MAX integration** when the `lvlup_max_enabled` preprocessor directive is defined.

---

## How It Works

### Before (Manual)
```csharp
// You had to manually initialize MAX
LvlUpSDK.Initialize(onComplete: (success, msg) =>
{
    if (success)
    {
        MaxAdIntegration.Initialize();  // ← Manual call required
    }
});
```

### After (Automatic)
```csharp
// MAX initializes automatically if lvlup_max_enabled is defined
LvlUpSDK.Initialize(onComplete: (success, msg) =>
{
    if (success)
    {
        Debug.Log("MAX tracking ready!");  // MAX already initialized
    }
});
```

---

## Setup

### Define the Preprocessor Symbol

Add `lvlup_max_enabled` to your Scripting Define Symbols:

**In Unity Editor:**
1. Edit > Project Settings > Player
2. Other Settings
3. Scripting Define Symbols
4. Add: `lvlup_max_enabled`

**Or in code:**
```csharp
#if lvlup_max_enabled
    // MAX integration is enabled
#endif
```

---

## Flow

```
LvlUpSDK.Initialize()
    ↓
LvlUpManager creates session
    ↓
AdMonetizationService.Initialize()
    ↓
#if lvlup_max_enabled
    ↓
InitializeMaxIntegration()
    ↓
MaxAdIntegration.Initialize()
    ↓
Subscribe to all MAX ad callbacks
```

---

## Benefits

✅ **Zero Manual Setup** - Just define the symbol  
✅ **Automatic** - No need to call initialize  
✅ **Conditional** - Only if MAX SDK is actually installed  
✅ **Error Safe** - Wrapped in try-catch  
✅ **Debug Ready** - Logs confirm initialization  

---

## Conditional Compilation

The initialization is protected by:
- `#if lvlup_max_enabled` - Your custom symbol
- `#if UNITY_ANDROID || UNITY_IOS` - Platform availability

This ensures:
- Editor builds won't break (even without MAX SDK)
- Only compiles when you explicitly enable it
- Safe from missing MAX SDK references

---

## Updated Code Structure

```csharp
public class AdMonetizationService
{
    public void Initialize(LvlUpHttpClient httpClient, EventMetadata eventMetadata)
    {
        // ...setup code...
        
        // Initialize MAX integration if enabled
#if lvlup_max_enabled
        InitializeMaxIntegration();
#endif
    }

    private void InitializeMaxIntegration()
    {
#if lvlup_max_enabled && (UNITY_ANDROID || UNITY_IOS)
        try
        {
            LvlUp.AdIntegration.MaxAdIntegration.Initialize();
            Debug.Log("[LvlUp] MAX ad integration initialized automatically");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[LvlUp] Failed to initialize MAX integration: {ex.Message}");
        }
#endif
    }
}
```

---

## Comparison with GameAnalytics

This mirrors the GameAnalytics approach in `GAMaxIntegration.cs`:

```csharp
// GameAnalytics
public static void ListenForImpressions(Action<string> callback)
{
#if gameanalytics_max_enabled && !(UNITY_EDITOR)
    // Subscribe to events
#endif
}

// LvlUp (similar pattern)
private void InitializeMaxIntegration()
{
#if lvlup_max_enabled && (UNITY_ANDROID || UNITY_IOS)
    // Initialize integration
#endif
}
```

---

## Your New Setup

1. **Add to Scripting Define Symbols:** `lvlup_max_enabled`
2. **Just initialize LvlUp:**
   ```csharp
   LvlUpSDK.Initialize();
   ```
3. **Done!** MAX tracking works automatically.

---

## No Manual Call Needed

You no longer need:
```csharp
MaxAdIntegration.Initialize();  // ← NOT NEEDED ANYMORE
```

Everything happens automatically! 🎉

---

## Verification

Check the console logs:
```
[LvlUp] AdMonetizationService initialized
[LvlUp] MAX ad integration initialized automatically
```

Both logs = Everything is set up correctly ✅

---

**Now your ad monetization system is fully automatic!** 🚀

