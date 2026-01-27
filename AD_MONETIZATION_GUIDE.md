# Ad Monetization Tracking Guide

This guide explains how to use the LvlUp SDK's Ad Monetization service to track ad impressions and revenue, similar to GameAnalytics.

## Overview

The Ad Monetization Service allows you to:
- Track ad impressions from multiple ad networks (MAX, AdMob, IronSource, etc.)
- Monitor revenue from different ad formats (banners, interstitials, rewarded ads, etc.)
- Query revenue analytics by network, format, or total
- Register callbacks for specific ad networks

## Quick Start

### Basic Ad Impression Tracking

Once the LvlUp SDK is initialized and a session has started, you can track ad impressions:

```csharp
using LvlUp;

// Get the AdMonetizationService
var adService = LvlUpManager.Instance.GetAdMonetizationService();

// Track a simple ad impression
adService.TrackAdImpression(
    adNetworkName: "MAX",
    adFormat: "INTER",
    adUnitId: "banner_bottom",
    placement: "gameplay",
    revenue: 0.0123,
    country: "US"
);
```

### Network-Specific Methods

For convenience, the service provides network-specific methods:

#### MAX (AppLovin MAX)
```csharp
adService.TrackMaxAdImpression(
    format: "INTER",
    adUnitId: "inter_ad_unit",
    networkName: "AppLovin",
    placement: "level_complete",
    creativeId: "creative_123",
    revenue: 0.0456
);
```

#### AdMob
```csharp
adService.TrackAdMobImpression(
    format: "REWARDED",
    adUnitId: "ca-app-pub-xxxxxxxxxxxxxxxx/yyyyyyyyyy",
    placement: "bonus_coins",
    revenue: 0.0789
);
```

#### IronSource
```csharp
adService.TrackIronSourceImpression(
    format: "BANNER",
    adUnitId: "ironSource_banner",
    placement: "bottom",
    revenue: 0.0123
);
```

## Integration with Ad Networks

### MAX (AppLovin MAX) Integration

The SDK includes an integration helper for MAX. Enable it by:

1. Adding the following define to your build settings:
   - `MAX_ENABLED`

2. Use the MaxAdIntegration helper in your initialization code:

```csharp
using LvlUp.AdIntegration;
using UnityEngine;

public class GameStartup : MonoBehaviour
{
    async void Start()
    {
        // Initialize LvlUp SDK first
        LvlUpSDK.Initialize(onComplete: (success, message) =>
        {
            if (success)
            {
                // Initialize MAX ad tracking with LvlUp
                MaxAdIntegration.Initialize();
                Debug.Log("MAX ad impressions will now be tracked");
            }
        });
    }
}
```

The MaxAdIntegration automatically:
- Listens to all MAX ad formats (banners, interstitials, rewarded, MRec, app open)
- Extracts impression data (network name, ad unit, revenue, etc.)
- Forwards impressions to the LvlUp Ad Monetization Service

### AdMob Integration

For AdMob, you'll need to implement your own callback:

```csharp
using LvlUp;
using GoogleMobileAds.Api;

public class AdMobHelper : MonoBehaviour
{
    void InitializeAdMob()
    {
        MobileAds.Initialize();
        
        // Hook into your ad loading to track impressions
        // Example (adapt to your actual ad setup):
        var adService = LvlUpManager.Instance.GetAdMonetizationService();
        
        // When an ad generates revenue:
        adService.TrackAdMobImpression(
            format: "REWARDED",
            adUnitId: rewardedAd.AdUnitId,
            placement: "level_complete",
            revenue: 0.05  // Your calculated revenue
        );
    }
}
```

### IronSource Integration

Similar to AdMob, hook into IronSource callbacks:

```csharp
using LvlUp;

public class IronSourceHelper : MonoBehaviour
{
    void InitializeIronSource()
    {
        var adService = LvlUpManager.Instance.GetAdMonetizationService();
        
        // Hook into IronSource impression event
        IronSourceEvents.onImpressionDataReadyEvent += (impressionData) =>
        {
            adService.TrackIronSourceImpression(
                format: impressionData.adFormat,
                adUnitId: impressionData.adUnit,
                placement: impressionData.placement,
                revenue: impressionData.revenue
            );
        };
    }
}
```

## Revenue Analytics

### Query Total Revenue

```csharp
var adService = LvlUpManager.Instance.GetAdMonetizationService();

double totalRevenue = adService.GetTotalRevenue();
Debug.Log($"Total ad revenue: ${totalRevenue}");
```

### Query Revenue by Network

```csharp
double maxRevenue = adService.GetNetworkRevenue("MAX");
double admobRevenue = adService.GetNetworkRevenue("AdMob");

Debug.Log($"MAX revenue: ${maxRevenue}");
Debug.Log($"AdMob revenue: ${admobRevenue}");
```

### Query Revenue by Ad Format

```csharp
double bannerRevenue = adService.GetFormatRevenue("BANNER");
double rewardedRevenue = adService.GetFormatRevenue("REWARDED");
double interRevenue = adService.GetFormatRevenue("INTER");

Debug.Log($"Banner revenue: ${bannerRevenue}");
Debug.Log($"Rewarded revenue: ${rewardedRevenue}");
Debug.Log($"Interstitial revenue: ${interRevenue}");
```

## Event Metadata

Each ad impression automatically includes event metadata:
- **Platform**: Device platform (iOS, Android, etc.)
- **OS Version**: Operating system version
- **Device**: Device model
- **App Version**: Your app version
- **Country**: Geographic location (if geo tracking enabled)
- **Session Number**: Current session number
- **Timestamp**: When the impression occurred

This data is automatically populated from the LvlUp SDK configuration.

## Advanced Features

### Register Network Callbacks

Get notified whenever an ad from a specific network is tracked:

```csharp
var adService = LvlUpManager.Instance.GetAdMonetizationService();

adService.RegisterNetworkCallback("MAX", (impression) =>
{
    Debug.Log($"MAX Ad: {impression.adFormat} - ${impression.revenue}");
    // Your custom handling here
});
```

### Get Pending Impressions

Retrieve all impressions that haven't been sent yet:

```csharp
var adService = LvlUpManager.Instance.GetAdMonetizationService();

var pendingImpressions = adService.GetPendingAdEvents();
Debug.Log($"Pending impressions: {pendingImpressions.Count}");

foreach (var impression in pendingImpressions)
{
    Debug.Log($"{impression.adData.adNetworkName} - {impression.adData.adFormat}");
}
```

### Clear Impressions

Remove all pending impressions (useful for testing):

```csharp
var adService = LvlUpManager.Instance.GetAdMonetizationService();
adService.ClearPendingAdEvents();
```

### Update Metadata

Update the event metadata if user context changes:

```csharp
var adService = LvlUpManager.Instance.GetAdMonetizationService();

EventMetadata newMetadata = new EventMetadata();
newMetadata.PopulateDeviceInfo();
// ... set other properties ...

adService.UpdateEventMetadata(newMetadata);
```

## Data Structure

### AdImpressionData

```csharp
public class AdImpressionData
{
    public string adNetworkName;      // e.g., "MAX", "AdMob", "IronSource"
    public string adFormat;           // e.g., "BANNER", "INTER", "REWARDED", "MREC", "APPOPEN"
    public string adUnitId;           // Ad unit identifier
    public string adUnitName;         // Human-readable ad unit name
    public string placement;          // Ad placement identifier
    public string creativeId;         // Creative identifier
    public double revenue;            // Revenue in USD
    public string revenueCurrency;    // Currency code (e.g., "USD")
    public string country;            // Country code where ad was shown
    public string impressionId;       // Unique impression identifier
    public long impressionTimestamp;  // Unix timestamp in milliseconds
    public string customData;         // Optional custom data as JSON string
}
```

### AdMonetizationEvent

```csharp
public class AdMonetizationEvent
{
    public string eventType;          // "ad_impression"
    public AdImpressionData adData;   // The impression data
    public EventMetadata metadata;    // Event metadata (platform, device, etc.)
    public long timestamp;            // Unix timestamp in milliseconds
}
```

## Best Practices

1. **Initialize After Session Start**: The AdMonetizationService is automatically initialized when a session starts. Don't initialize it manually.

2. **Track Revenue Accurately**: Ensure the revenue value represents the actual earnings in USD.

3. **Include Custom Data**: Use the `customData` parameter to track additional information:
   ```csharp
   adService.TrackAdImpression(
       adNetworkName: "MAX",
       adFormat: "REWARDED",
       adUnitId: "reward_ad",
       revenue: 0.05,
       customData: JsonUtility.ToJson(new { gameLevel = 5, difficulty = "hard" })
   );
   ```

4. **Monitor Revenue by Network**: Use `GetNetworkRevenue()` to compare performance of different networks:
   ```csharp
   var maxRev = adService.GetNetworkRevenue("MAX");
   var admobRev = adService.GetNetworkRevenue("AdMob");
   Debug.Log($"MAX: ${maxRev}, AdMob: ${admobRev}");
   ```

5. **Implement Network Callbacks**: Use callbacks for real-time tracking:
   ```csharp
   adService.RegisterNetworkCallback("MAX", (imp) =>
   {
       // Update UI, analytics, or other systems
       OnAdImpression?.Invoke(imp);
   });
   ```

6. **Geo Tracking**: Enable geo tracking in LvlUp config to get accurate country data for impressions:
   ```csharp
   // In LvlUpConfig
   config.enableGeoTracking = true;
   ```

## Troubleshooting

### Service Not Initialized
**Error**: "AdMonetizationService not initialized"
**Solution**: Ensure LvlUpSDK.Initialize() has completed and a session has been started.

### No Revenue Data
**Issue**: Revenue values are 0 or missing
**Solution**: 
- Verify you're passing the correct revenue values from your ad network
- Check that impressions are being tracked during gameplay (not just in testing)
- Ensure the ad network is providing revenue data (some test ads don't)

### Missing Country Data
**Issue**: Country field is empty in impressions
**Solution**: 
- Enable geo tracking in LvlUp config: `config.enableGeoTracking = true`
- Wait a few moments after app launch for geo data to be fetched
- The country will be set to empty string if geo is disabled or unavailable

## Supported Ad Formats

- `BANNER` - Banner ads
- `INTER` - Interstitial ads
- `REWARDED` - Rewarded video ads
- `MREC` - Medium Rectangle ads
- `APPOPEN` - App Open ads
- Custom formats supported via the generic `TrackAdImpression()` method

## Next Steps

1. Set up your ad network integration (MAX, AdMob, IronSource)
2. Initialize the integration helper in your game startup
3. Monitor revenue in the LvlUp dashboard
4. Analyze performance by network and format
5. Optimize ad placements based on revenue data

For more information about LvlUp SDK, see the main README.md file.

