# Quick Start: Ad Monetization Tracking

## 5-Minute Setup

### Step 1: Initialize LvlUp SDK
```csharp
// In your game startup script
LvlUpSDK.Initialize(onComplete: (success, message) =>
{
    if (success)
    {
        Debug.Log("LvlUp ready!");
    }
});
```

### Step 2: Initialize Ad Network Integration (MAX example)
```csharp
// After LvlUp is initialized, initialize MAX ad tracking
using LvlUp.AdIntegration;

MaxAdIntegration.Initialize();
// That's it! All MAX ads are now tracked
```

### Step 3: Track Manual Ad Impressions (if not using integration)
```csharp
// Get the ad service
var adService = LvlUpManager.Instance.GetAdMonetizationService();

// Track an ad
adService.TrackAdImpression(
    adNetworkName: "MAX",
    adFormat: "INTER",
    adUnitId: "inter_levelup",
    revenue: 0.0123
);
```

### Step 4: Query Revenue (optional)
```csharp
var totalRevenue = adService.GetTotalRevenue();
var bannerRevenue = adService.GetFormatRevenue("BANNER");
Debug.Log($"Total: ${totalRevenue}, Banners: ${bannerRevenue}");
```

---

## Common Use Cases

### Track Banner Ad
```csharp
adService.TrackAdImpression(
    adNetworkName: "AdMob",
    adFormat: "BANNER",
    adUnitId: "ca-app-pub-xxx/yyy",
    placement: "bottom_menu",
    revenue: 0.0045
);
```

### Track Rewarded Ad
```csharp
adService.TrackAdImpression(
    adNetworkName: "MAX",
    adFormat: "REWARDED",
    adUnitId: "rewarded_coins",
    placement: "level_boost",
    revenue: 0.0456
);
```

### React to Ad Impressions
```csharp
adService.RegisterNetworkCallback("MAX", (impression) =>
{
    // Called when MAX ad is tracked
    Debug.Log($"MAX ad: {impression.adFormat} - ${impression.revenue}");
    
    // Update UI, give rewards, log to analytics, etc.
    UpdateRevenueUI(impression.revenue);
});
```

### Get Revenue Stats
```csharp
// Total revenue
double total = adService.GetTotalRevenue();

// By network
double maxRev = adService.GetNetworkRevenue("MAX");
double admobRev = adService.GetNetworkRevenue("AdMob");

// By format
double bannerRev = adService.GetFormatRevenue("BANNER");
double rewardedRev = adService.GetFormatRevenue("REWARDED");

// Pending count
int pending = adService.GetPendingAdEventCount();
```

---

## Supported Ad Networks

| Network | Integration Available | Manual Tracking |
|---------|----------------------|-----------------|
| MAX (AppLovin) | ✅ Yes - `MaxAdIntegration.Initialize()` | ✅ Yes |
| AdMob | 🔧 Placeholder provided | ✅ Yes |
| IronSource | 🔧 Placeholder provided | ✅ Yes |
| Custom Networks | - | ✅ Yes |

---

## Ad Formats

- `BANNER` - Standard banner ads
- `INTER` - Interstitial ads  
- `REWARDED` - Rewarded video ads
- `MREC` - Medium Rectangle ads
- `APPOPEN` - App Open ads
- Any custom format string

---

## Data Automatically Captured

Each ad impression includes:
- **Device**: Platform (iOS/Android), OS version, device model
- **App**: Version, build number, bundle ID
- **Session**: Session number, session ID
- **Location**: Country, region, city (if geo tracking enabled)
- **Timestamp**: Unix millisecond timestamp
- **Event UUID**: Unique identifier for each impression

---

## Enable MAX Integration

If you have MAX SDK installed, define the preprocessor symbol:

**Unity Editor:**
1. Edit > Project Settings > Player > Other Settings
2. Add `MAX_ENABLED` to Scripting Define Symbols

**Or in code:**
```csharp
#if MAX_ENABLED
    MaxAdIntegration.Initialize();
#endif
```

---

## Troubleshooting

### "AdMonetizationService not available"
- Ensure `LvlUpSDK.Initialize()` is called first
- Wait for initialization to complete (check the callback)
- Ensure session has started (auto-starts if config enabled)

### Revenue showing as 0
- Check ad network is actually returning revenue values
- Test ads may not generate revenue
- Verify you're passing correct revenue parameter

### No impressions recorded
- Check integration is initialized (see logs for confirmation)
- Verify ad network is showing ads to users
- For MAX: check `MAX_ENABLED` define symbol is set

---

## Next Steps

1. Set up your ad network (MAX, AdMob, IronSource)
2. Add the integration to your game startup
3. Monitor revenue in real-time
4. Use callbacks to react to impressions
5. Query analytics to optimize ad placement

For detailed documentation, see `AD_MONETIZATION_GUIDE.md`

For implementation examples, see `Example_AdMonetization.cs`

