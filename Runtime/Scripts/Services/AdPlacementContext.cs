using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace LvlUp.Services
{
    /// <summary>Thread-safe custom placement state shared by automatic and manual tracking.</summary>
    internal sealed class AdPlacementContext
    {
        internal static readonly AdPlacementContext Shared = new AdPlacementContext();
        private const double OneShotLifetimeSeconds = 120;
        private readonly object _gate = new object();
        private readonly Dictionary<LvlUpAdType, PendingPlacement> _placements =
            new Dictionary<LvlUpAdType, PendingPlacement>();
        private readonly Func<double> _clock;

        private struct PendingPlacement
        {
            internal string Name;
            internal double SetAt;
        }

        // A monotonic clock avoids wall-clock changes and does not depend on Unity's main thread.
        internal AdPlacementContext(Func<double> clock = null)
        {
            _clock = clock ?? (() => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency);
        }

        internal void SetPlacement(LvlUpAdType adType, string placement)
        {
            if (!Enum.IsDefined(typeof(LvlUpAdType), adType)) return;
            string normalized = Normalize(placement);
            lock (_gate)
            {
                if (normalized == null)
                {
                    _placements.Remove(adType);
                    return;
                }
                _placements[adType] = new PendingPlacement { Name = normalized, SetAt = _clock() };
            }
        }

        internal void ClearPlacement(LvlUpAdType adType)
        {
            lock (_gate)
            {
                _placements.Remove(adType);
            }
        }

        /// <summary>
        /// Resolve once for each impression. Even an explicit override consumes a pending one-shot,
        /// so it cannot leak into a later impression. Existing explicit/MAX values are preserved.
        /// </summary>
        internal string Resolve(string adFormat, string explicitPlacement, string maxPlacement = null)
        {
            string customPlacement = null;
            if (TryGetAdType(adFormat, out LvlUpAdType adType))
            {
                lock (_gate)
                {
                    if (_placements.TryGetValue(adType, out PendingPlacement pending))
                    {
                        bool sticky = adType == LvlUpAdType.Banner || adType == LvlUpAdType.MRec;
                        if (!sticky) _placements.Remove(adType);
                        if (sticky || _clock() - pending.SetAt < OneShotLifetimeSeconds)
                            customPlacement = pending.Name;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(explicitPlacement)) return explicitPlacement;
            if (customPlacement != null) return customPlacement;
            return maxPlacement ?? explicitPlacement;
        }

        private static string Normalize(string placement)
        {
            if (string.IsNullOrWhiteSpace(placement)) return null;
            string trimmed = placement.Trim();
            return trimmed.Length > 64 ? trimmed.Substring(0, 64) : trimmed;
        }

        internal static bool TryGetAdType(string format, out LvlUpAdType adType)
        {
            switch (format?.Trim().ToUpperInvariant())
            {
                case "INTER":
                case "INTERSTITIAL":
                case "FS":
                    adType = LvlUpAdType.Interstitial;
                    return true;
                case "REWARDED":
                case "REWARDED_VIDEO":
                case "RV":
                    adType = LvlUpAdType.Rewarded;
                    return true;
                case "BANNER":
                    adType = LvlUpAdType.Banner;
                    return true;
                case "MREC":
                    adType = LvlUpAdType.MRec;
                    return true;
                case "APPOPEN":
                case "APP_OPEN":
                    adType = LvlUpAdType.AppOpen;
                    return true;
                default:
                    adType = default;
                    return false;
            }
        }
    }
}
