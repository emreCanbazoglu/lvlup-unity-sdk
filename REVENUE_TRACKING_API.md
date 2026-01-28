# Revenue Tracking API

The LvlUp SDK now provides a clean, static API for tracking revenue from ad impressions and in-app purchases through `LvlUpSDK.Revenue`.

## Quick Start

```csharp
using LvlUp;
using LvlUp.Models;

// Track an ad impression
LvlUpSDK.Revenue.TrackAdImpression(
    adNetworkName: "MAX",
    adFormat: "REWARDED",
    revenue: 0.005,
    adUnitId: "ad_unit_123",
    placement: "level_complete",
    creativeId: "creative_456"
);

// Track an in-app purchase
LvlUpSDK.Revenue.TrackInAppPurchase(
    productId: "com.game.coins_100",
    revenue: 0.99,
    transactionId: "txn_123456789",
    store: "APPLE_APP_STORE",
    productName: "100 Coins",
    quantity: 1,
    isVerified: true
);

// Flush revenue queue manually
LvlUpSDK.Revenue.Flush();

// Check queued revenue count
int queuedCount = LvlUpSDK.Revenue.QueuedCount;
```

## API Reference

### LvlUpSDK.Revenue.Track()

Track custom revenue data with full control over all fields.

```csharp
var revenueData = new RevenueData
{
    revenueType = "AD_IMPRESSION", // or "IN_APP_PURCHASE"
    revenue = 0.005,
    currency = "USD",
    adNetworkName = "MAX",
    adFormat = "REWARDED",
    adUnitId = "ad_unit_123",
    adPlacement = "level_complete"
};

LvlUpSDK.Revenue.Track(revenueData, response =>
{
    if (response.success)
    {
        Debug.Log("Revenue tracked successfully!");
    }
});
```

### LvlUpSDK.Revenue.TrackAdImpression()

Convenience method for tracking ad impressions.

**Parameters:**
- `adNetworkName` (string): Ad network name (e.g., "MAX", "AdMob", "IronSource")
- `adFormat` (string): Ad format (e.g., "BANNER", "INTER", "REWARDED")
- `revenue` (double): Revenue amount in USD
- `adUnitId` (string, optional): Ad unit identifier
- `placement` (string, optional): Placement name
- `creativeId` (string, optional): Creative identifier
- `country` (string, optional): Country code

**Example:**
```csharp
LvlUpSDK.Revenue.TrackAdImpression(
    adNetworkName: "MAX",
    adFormat: "REWARDED",
    revenue: 0.005,
    adUnitId: "rewarded_ad_unit",
    placement: "after_level"
);
```

### LvlUpSDK.Revenue.TrackInAppPurchase()

Convenience method for tracking in-app purchases.

**Parameters:**
- `productId` (string): Product identifier (e.g., "com.game.coins_100")
- `revenue` (double): Purchase amount in USD
- `transactionId` (string): Unique transaction ID
- `store` (string, optional): Store name (e.g., "APPLE_APP_STORE", "GOOGLE_PLAY")
- `productName` (string, optional): Human-readable product name
- `quantity` (int, optional): Quantity purchased (default: 1)
- `isVerified` (bool, optional): Whether purchase is verified (default: false)

**Example:**
```csharp
LvlUpSDK.Revenue.TrackInAppPurchase(
    productId: "com.game.coins_100",
    revenue: 0.99,
    transactionId: "txn_abc123",
    store: "APPLE_APP_STORE",
    productName: "100 Coins",
    quantity: 1,
    isVerified: true
);
```

### LvlUpSDK.Revenue.Flush()

Manually flush the revenue queue to send all pending revenue data to the server immediately.

```csharp
LvlUpSDK.Revenue.Flush();
```

**Note:** Revenue data is automatically flushed based on batch size and interval configured in `LvlUpConfig`.

### LvlUpSDK.Revenue.QueuedCount

Get the number of revenue items currently queued for sending.

```csharp
int queuedCount = LvlUpSDK.Revenue.QueuedCount;
Debug.Log($"Queued revenue items: {queuedCount}");
```

## Revenue Data Model

The `RevenueData` class supports both ad impressions and in-app purchases:

### Ad Impression Fields
- `revenueType`: "AD_IMPRESSION"
- `revenue`: Revenue amount
- `currency`: Currency code (default: "USD")
- `adNetworkName`: Ad network (e.g., "MAX", "AdMob")
- `adFormat`: Ad format (e.g., "BANNER", "INTER", "REWARDED")
- `adUnitId`: Ad unit identifier
- `adPlacement`: Placement name
- `adCreativeId`: Creative identifier
- `adImpressionId`: Unique impression ID (auto-generated)

### In-App Purchase Fields
- `revenueType`: "IN_APP_PURCHASE"
- `revenue`: Purchase amount
- `currency`: Currency code (default: "USD")
- `productId`: Product identifier
- `productName`: Product name
- `transactionId`: Unique transaction ID
- `store`: Store name (e.g., "APPLE_APP_STORE", "GOOGLE_PLAY")
- `quantity`: Quantity purchased
- `isVerified`: Whether purchase is verified

### Context Fields (Auto-populated)
The SDK automatically populates these fields:
- `platform`: iOS, Android, etc.
- `osVersion`: Operating system version
- `manufacturer`: Device manufacturer
- `device`: Device model
- `deviceId`: Unique device identifier
- `appVersion`: App version
- `bundleId`: App bundle identifier
- `engineVersion`: Unity version
- `sdkVersion`: LvlUp SDK version
- `connectionType`: wifi, wwan, offline
- `sessionNum`: Current session number
- `country`, `countryCode`, `region`, `city`: Geo data (if enabled)
- `latitude`, `longitude`: Geo coordinates (if enabled)
- `timezone`: User's timezone

## Features

### Automatic Batching
Revenue data is automatically batched based on `eventBatchSize` configuration to optimize network requests.

### Offline Support
Revenue data is persisted to local storage if the device is offline and automatically sent when connectivity is restored.

### Session Context
Revenue data is automatically associated with the current user session and includes session number for analytics.

### Geo Location
If geo tracking is enabled, revenue data includes country, region, city, and coordinates.

## Configuration

Configure revenue tracking behavior in `LvlUpConfig`:

```csharp
var config = new LvlUpConfig
{
    eventBatchSize = 20,        // Batch size for revenue data
    eventFlushInterval = 30f,   // Auto-flush interval in seconds
    sendImmediately = false,    // Send immediately or batch
    enableGeoTracking = true    // Include geo data in revenue
};

LvlUpSDK.Initialize(apiKey, baseUrl, config);
```

## Integration Examples

### MAX Ad Network Integration
```csharp
void OnRewardedAdRevenuePaid(string adUnitId, MaxSdkBase.AdInfo adInfo)
{
    LvlUpSDK.Revenue.TrackAdImpression(
        adNetworkName: "MAX",
        adFormat: "REWARDED",
        revenue: adInfo.Revenue,
        adUnitId: adUnitId,
        placement: adInfo.Placement,
        creativeId: adInfo.CreativeId
    );
}
```

### Unity IAP Integration
```csharp
public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
{
    LvlUpSDK.Revenue.TrackInAppPurchase(
        productId: args.purchasedProduct.definition.id,
        revenue: (double)args.purchasedProduct.metadata.localizedPrice,
        transactionId: args.purchasedProduct.transactionID,
        store: "APPLE_APP_STORE", // or determine dynamically
        productName: args.purchasedProduct.metadata.localizedTitle,
        quantity: 1,
        isVerified: true // after receipt validation
    );
    
    return PurchaseProcessingResult.Complete;
}
```

## Migration from Direct Service Access

If you were previously using the service directly, migrate to the new static API:

### Before
```csharp
var revenueService = LvlUpManager.Instance.GetRevenueTrackingService();
revenueService.TrackAdImpression("MAX", "REWARDED", 0.005, "ad_unit", "placement", "creative");
```

### After
```csharp
LvlUpSDK.Revenue.TrackAdImpression("MAX", "REWARDED", 0.005, "ad_unit", "placement", "creative");
```

## Best Practices

1. **Track Real Revenue**: Only track actual revenue received, not estimated or predicted revenue
2. **Use Transaction IDs**: Always provide transaction IDs for IAP to prevent duplicate tracking
3. **Verify Purchases**: Set `isVerified = true` only after server-side receipt validation
4. **Consistent Formats**: Use consistent ad format names ("BANNER", "INTER", "REWARDED")
5. **Test in Editor**: Use the example scripts to test revenue tracking during development
6. **Monitor Queue**: Check `QueuedCount` to ensure revenue is being sent properly

## See Also

- [RevenueTrackingExample.cs](Examples/RevenueTrackingExample.cs) - Example implementation
- [API_REFERENCE.md](API_REFERENCE.md) - Complete API documentation
- [QUICKSTART.md](QUICKSTART.md) - Getting started guide

