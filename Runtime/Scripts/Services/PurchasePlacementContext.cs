using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace LvlUp.Services
{
    /// <summary>
    /// Thread-safe pending purchase placements, shared by Unity IAP auto-capture and manual tracking.
    /// The store callback that reports a purchase has no UI context, so the game records where the
    /// purchase started before opening the store sheet and the next purchase consumes it.
    /// </summary>
    internal sealed class PurchasePlacementContext
    {
        internal static readonly PurchasePlacementContext Shared = new PurchasePlacementContext();
        // Store sheets, payment authentication and Ask to Buy can take minutes.
        private const double LifetimeSeconds = 600;
        private const string AnyProduct = "";
        private readonly object _gate = new object();
        private readonly Dictionary<string, PendingPlacement> _placements =
            new Dictionary<string, PendingPlacement>(StringComparer.Ordinal);
        private readonly Func<double> _clock;

        private struct PendingPlacement
        {
            internal string Name;
            internal double SetAt;
        }

        // A monotonic clock avoids wall-clock changes and does not depend on Unity's main thread.
        internal PurchasePlacementContext(Func<double> clock = null)
        {
            _clock = clock ?? (() => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency);
        }

        /// <summary>A null or blank productId applies to whichever product is purchased next.</summary>
        internal void SetPlacement(string placement, string productId = null)
        {
            string key = Key(productId);
            string normalized = Normalize(placement);
            lock (_gate)
            {
                if (normalized == null)
                {
                    _placements.Remove(key);
                    return;
                }
                _placements[key] = new PendingPlacement { Name = normalized, SetAt = _clock() };
            }
        }

        /// <summary>A null or blank productId clears every pending placement.</summary>
        internal void ClearPlacement(string productId = null)
        {
            lock (_gate)
            {
                if (string.IsNullOrWhiteSpace(productId)) _placements.Clear();
                else _placements.Remove(Key(productId));
            }
        }

        /// <summary>
        /// Resolve once for each purchase: an explicit placement wins, then the product's pending
        /// placement, then the any-product one. Every match is consumed, even when the explicit value
        /// wins, so it cannot leak into a later purchase.
        /// </summary>
        internal string Resolve(string productId, string explicitPlacement)
        {
            string pending = null;
            lock (_gate)
            {
                pending = Take(Key(productId));
                string any = Take(AnyProduct);
                if (pending == null) pending = any;
            }

            string normalizedExplicit = Normalize(explicitPlacement);
            return normalizedExplicit ?? pending;
        }

        // Caller holds _gate.
        private string Take(string key)
        {
            if (!_placements.TryGetValue(key, out PendingPlacement pending)) return null;
            _placements.Remove(key);
            return _clock() - pending.SetAt < LifetimeSeconds ? pending.Name : null;
        }

        private static string Key(string productId) =>
            string.IsNullOrWhiteSpace(productId) ? AnyProduct : productId.Trim();

        private static string Normalize(string placement)
        {
            if (string.IsNullOrWhiteSpace(placement)) return null;
            string trimmed = placement.Trim();
            return trimmed.Length > 64 ? trimmed.Substring(0, 64) : trimmed;
        }
    }
}
