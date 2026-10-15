using System;
using System.Collections.Generic;
using LvlUp.Models;
using UnityEngine;

namespace LvlUp.Services
{
    /// <summary>Validates wallet telemetry and submits it through ordinary event tracking.</summary>
    internal sealed class EconomyTrackingService
    {
        private readonly LvlUpConfig _config;
        private readonly Action<string, Dictionary<string, object>> _track;
        private readonly Func<double> _now;
        private readonly Dictionary<string, ResourceType> _resources = new Dictionary<string, ResourceType>();
        private readonly HashSet<string> _warned = new HashSet<string>();
        private readonly List<PendingSource> _pending = new List<PendingSource>();
        private Func<IReadOnlyDictionary<string, double>> _balanceProvider;

        private sealed class PendingSource
        {
            internal Dictionary<string, object> Properties;
            internal double Deadline;
        }

        internal EconomyTrackingService(LvlUpConfig config, Action<string, Dictionary<string, object>> track,
            Func<double> now = null)
        {
            _config = config;
            _track = track;
            _now = now ?? (() => Time.realtimeSinceStartup);
        }

        internal void RegisterResource(string resourceId, ResourceType type)
        {
            Guard(() =>
            {
                var id = ResourceId(resourceId);
                if (!_resources.ContainsKey(id) && _resources.Count >= 50)
                    throw new ArgumentException("Cannot register more than 50 resources.");
                _resources[id] = type;
            });
        }

        internal void SetBalanceProvider(Func<IReadOnlyDictionary<string, double>> provider)
        {
            _balanceProvider = provider;
        }

        internal void Source(string resourceId, double amount, string reason, double? balanceAfter = null,
            string itemId = null, ResourceType? type = null)
        {
            Guard(() =>
            {
                FlushExpired();
                var properties = Flow("source", resourceId, amount, reason, balanceAfter, itemId, type, null);
                double window = _config.economyCoalesceSeconds;
                if (window <= 0 || double.IsNaN(window) || double.IsInfinity(window))
                {
                    FlushResource((string)properties["resourceId"]);
                    Emit("economy_flow", properties);
                    return;
                }

                foreach (var pending in _pending)
                {
                    if (!SameKey(pending.Properties, properties)) continue;
                    double total = (double)pending.Properties["amount"] + amount;
                    Require(Finite(total), "Coalesced amount must be finite.");
                    pending.Properties["amount"] = total;
                    pending.Properties["balanceAfter"] = properties["balanceAfter"];
                    return;
                }
                _pending.Add(new PendingSource { Properties = properties, Deadline = _now() + window });
            });
        }

        internal void Sink(string resourceId, double amount, string reason, double? balanceAfter = null,
            string itemId = null, ResourceType? type = null)
        {
            Guard(() =>
            {
                var properties = Flow("sink", resourceId, amount, reason, balanceAfter, itemId, type, null);
                FlushResource((string)properties["resourceId"]);
                Emit("economy_flow", properties);
            });
        }

        internal void Trade(string reason, string itemId,
            IEnumerable<(string resourceId, double amount)> sinks,
            IEnumerable<(string resourceId, double amount)> sources)
        {
            Guard(() =>
            {
                Require(sinks != null && sources != null, "Trade legs cannot be null.");
                var balances = ReadProvider();
                string transactionId = Guid.NewGuid().ToString("N");
                var legs = new List<Dictionary<string, object>>();
                foreach (var leg in sinks)
                    legs.Add(Flow("sink", leg.resourceId, leg.amount, reason, null, itemId, null, balances));
                foreach (var leg in sources)
                    legs.Add(Flow("source", leg.resourceId, leg.amount, reason, null, itemId, null, balances));
                // Prepare every leg first, so invalid input cannot send half a trade.
                foreach (var leg in legs) FlushResource((string)leg["resourceId"]);
                foreach (var leg in legs)
                {
                    leg["transactionId"] = transactionId;
                    Emit("economy_flow", leg);
                }
            });
        }

        internal void Snapshot(IReadOnlyDictionary<string, double> balances = null)
        {
            Guard(() => SendSnapshot(balances == null ? ReadProvider() : NormalizeBalances(balances), "manual"));
        }

        internal void AutoSnapshot(string trigger)
        {
            if (_balanceProvider == null) return;
            Guard(() => SendSnapshot(ReadProvider(), trigger));
        }

        private void SendSnapshot(Dictionary<string, double> balances, string trigger)
        {
            foreach (var resource in _resources)
                if (!balances.ContainsKey(resource.Key)) balances[resource.Key] = 0;
            Require(balances.Count <= 50, "Snapshots cannot contain more than 50 resources.");
            var types = new Dictionary<string, string>();
            foreach (var balance in balances)
            {
                Require(Finite(balance.Value) && balance.Value >= 0, "Snapshot balances must be finite and non-negative.");
                types[balance.Key] = ResolveType(balance.Key, null);
            }
            var properties = new Dictionary<string, object>
            {
                { "balances", balances }, { "resourceTypes", types }, { "trigger", trigger }
            };
            AddLevel(properties);
            Flush();
            Emit("economy_snapshot", properties);
        }

        private Dictionary<string, object> Flow(string flowType, string resourceId, double amount, string reason,
            double? balanceAfter, string itemId, ResourceType? type, Dictionary<string, double> balances)
        {
            string id = ResourceId(resourceId);
            Require(Finite(amount) && amount > 0, "Amount must be positive and finite.");
            Require(itemId == null || itemId.Length <= 64, "itemId cannot exceed 64 characters.");
            if (!balanceAfter.HasValue)
            {
                balances = balances ?? ReadProvider();
                double value;
                Require(balances.TryGetValue(id, out value), "Balance provider has no balance for " + id + ".");
                balanceAfter = value;
            }
            Require(Finite(balanceAfter.Value) && balanceAfter.Value >= 0, "balanceAfter must be finite and non-negative.");
            var properties = new Dictionary<string, object>
            {
                { "flowType", flowType }, { "resourceType", ResolveType(id, type) }, { "resourceId", id },
                { "amount", amount }, { "balanceAfter", balanceAfter.Value },
                { "reason", Sanitize(string.IsNullOrEmpty(reason) ? "unknown" : reason, 64) },
                { "inLevel", _config.inLevel }
            };
            if (itemId != null) properties["itemId"] = itemId;
            AddLevel(properties);
            return properties;
        }

        private void AddLevel(Dictionary<string, object> properties)
        {
            if (_config.currentLevelOrder.HasValue && _config.currentLevelOrder.Value >= 1)
                properties["levelOrder"] = _config.currentLevelOrder.Value;
        }

        private string ResolveType(string id, ResourceType? type)
        {
            if (type.HasValue) return type.Value.ToWireString();
            ResourceType registered;
            if (_resources.TryGetValue(id, out registered)) return registered.ToWireString();
            if (_warned.Add(id)) Debug.LogWarning("[LvlUp] Economy: Unregistered resource " + id + "; using other.");
            return "other";
        }

        private Dictionary<string, double> ReadProvider()
        {
            Require(_balanceProvider != null, "A balance provider is required when no balance is supplied.");
            var balances = _balanceProvider();
            Require(balances != null, "Balance provider returned null.");
            return NormalizeBalances(balances);
        }

        private static Dictionary<string, double> NormalizeBalances(IReadOnlyDictionary<string, double> balances)
        {
            var result = new Dictionary<string, double>();
            foreach (var entry in balances)
            {
                string id = ResourceId(entry.Key);
                Require(!result.ContainsKey(id), "Balance provider contains duplicate sanitized resource ids.");
                result.Add(id, entry.Value);
            }
            return result;
        }

        private static string ResourceId(string id)
        {
            Require(!string.IsNullOrEmpty(id) && id.Length <= 64, "Resource ids must contain 1–64 characters.");
            return Sanitize(id, 64);
        }

        private static string Sanitize(string value, int maxLength)
        {
            var chars = value.ToLowerInvariant().ToCharArray();
            int length = Math.Min(chars.Length, maxLength);
            for (int i = 0; i < length; i++)
                if (!(chars[i] >= 'a' && chars[i] <= 'z') && !(chars[i] >= '0' && chars[i] <= '9') && chars[i] != '_')
                    chars[i] = '_';
            return new string(chars, 0, length);
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new ArgumentException(message);
        }

        private static void Guard(Action action)
        {
            try { action(); }
            catch (Exception exception) { Debug.LogError("[LvlUp] Economy: " + exception.Message); }
        }

        private void Emit(string name, Dictionary<string, object> properties)
        {
            // Tracking can invoke game callbacks; contain failures at the economy boundary.
            Guard(() => _track(name, properties));
        }

        private static bool SameKey(Dictionary<string, object> left, Dictionary<string, object> right)
        {
            foreach (string key in new[] { "resourceId", "reason", "levelOrder", "inLevel", "itemId" })
            {
                object a, b;
                left.TryGetValue(key, out a);
                right.TryGetValue(key, out b);
                if (!Equals(a, b)) return false;
            }
            return true;
        }

        internal void FlushExpired()
        {
            if (_pending.Count == 0) return;
            Guard(() => FlushMatching(pending => pending.Deadline <= _now()));
        }

        internal void Flush()
        {
            Guard(() => FlushMatching(pending => true));
        }

        private void FlushResource(string resourceId)
        {
            FlushMatching(pending => (string)pending.Properties["resourceId"] == resourceId);
        }

        private void FlushMatching(Predicate<PendingSource> match)
        {
            var ready = _pending.FindAll(match);
            // Remove before sending: queue-size flushes may re-enter Flush synchronously.
            _pending.RemoveAll(match);
            foreach (var pending in ready) Emit("economy_flow", pending.Properties);
        }
    }
}
