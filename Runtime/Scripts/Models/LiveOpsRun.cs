using System.Globalization;

namespace LvlUp.Models
{
    public enum LiveOpsFeatureType { Event, Pass, Offer }
    public enum LiveOpsOutcome { Won, Failed, Timeout, Disabled }
    public enum LiveOpsCost { Free, Soft, Iap }

    /// <summary>Identity and pinned variant of one LiveOps run.</summary>
    public readonly struct LiveOpsRun
    {
        public string FeatureId { get; }
        public LiveOpsFeatureType FeatureType { get; }
        public string RunId { get; }
        public string VariantId { get; }
        public long? RunStartedAt { get; }
        public long? RunEndsAt { get; }

        public LiveOpsRun(string featureId, LiveOpsFeatureType featureType, string runId, string variantId,
            long? runStartedAt = null, long? runEndsAt = null)
        {
            FeatureId = featureId;
            FeatureType = featureType;
            RunId = runId;
            VariantId = variantId;
            RunStartedAt = runStartedAt;
            RunEndsAt = runEndsAt;
        }

        public static LiveOpsRun ForSeason(string featureId, LiveOpsFeatureType type, long seasonStartUnix,
            int participationCount, string variantId, long? runEndsAt = null)
        {
            string id = Sanitize(featureId);
            return new LiveOpsRun(id, type, id + ":" + seasonStartUnix.ToString(CultureInfo.InvariantCulture) +
                ":" + participationCount.ToString(CultureInfo.InvariantCulture), variantId, seasonStartUnix, runEndsAt);
        }

        public static LiveOpsRun ForOffer(string featureId, long activationUnix, string variantId, long? runEndsAt = null)
        {
            string id = Sanitize(featureId);
            return new LiveOpsRun(id, LiveOpsFeatureType.Offer,
                id + ":" + activationUnix.ToString(CultureInfo.InvariantCulture), variantId, activationUnix, runEndsAt);
        }

        internal static string Sanitize(string value)
        {
            if (value == null) return null;
            var chars = value.ToLowerInvariant().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (!(chars[i] >= 'a' && chars[i] <= 'z') && !(chars[i] >= '0' && chars[i] <= '9') && chars[i] != '_')
                    chars[i] = '_';
            return new string(chars);
        }
    }

    /// <summary>Optional reward and payment details for a granted claim.</summary>
    public sealed class LiveOpsClaim
    {
        public string RewardId { get; set; }
        public string ItemId { get; set; }
        public string Track { get; set; }
        public LiveOpsCost? Cost { get; set; }
        public string CostResource { get; set; }
        public double? CostAmount { get; set; }
        public string StoreItemId { get; set; }
        public string ProductId { get; set; }
    }
}
