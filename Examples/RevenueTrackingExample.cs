using LvlUp;
using UnityEngine;

public class RevenueTrackingExample : MonoBehaviour
{
    [ContextMenu("Track Sample Ad Impression")]
    public void TrackSampleAdImpression()
    {
        // Example of tracking an ad impression using LvlUpSDK.Revenue API
        LvlUpSDK.Revenue.TrackAdImpression(
            adNetworkName: "SampleAdNetwork",
            adFormat: "REWARDED",
            revenue: 0.005, // Example revenue amount
            adUnitId: "sample_ad_unit_123",
            placement: "level_complete",
            creativeId: "creative_456"
        );

        Debug.Log("[LvlUp] Sample Ad Impression tracked.");
    }

    [ContextMenu("Track Sample In-App Purchase")]
    public void TrackSampleInAppPurchase()
    {
        // Example of tracking an in-app purchase
        LvlUpSDK.Revenue.TrackInAppPurchase(
            productId: "com.game.coins_100",
            revenue: 0.99,
            currency: "USD",
            transactionId: "txn_123456789",
            store: "APPLE_APP_STORE",
            productName: "100 Coins",
            quantity: 1,
            isVerified: true
        );

        Debug.Log("[LvlUp] Sample In-App Purchase tracked.");
    }
    
    [ContextMenu("Flush Revenue Queue")]
    public void FlushRevenueQueue()
    {
        // Manually flush the revenue queue
        int queuedCount = LvlUpSDK.Revenue.QueuedCount;
        Debug.Log($"[LvlUp] Flushing {queuedCount} queued revenue items...");
        
        LvlUpSDK.Revenue.Flush();
    }
}
