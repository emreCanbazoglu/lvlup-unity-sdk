using System;
using System.Collections.Generic;
using System.Text;
using LvlUp.Models;
using UnityEngine;

namespace LvlUp.Services
{
    /// <summary>Validates LiveOps telemetry and submits it through ordinary event tracking.</summary>
    internal sealed class LiveOpsTrackingService
    {
        internal const string PrefsKey = "LvlUp_LiveOps_OncePerRun";
        private readonly LvlUpConfig _config;
        private readonly Action<string, Dictionary<string, object>> _track;
        private readonly HashSet<string> _warned = new HashSet<string>();
        private readonly HashSet<string> _sent = new HashSet<string>();
        private readonly List<string> _recent = new List<string>();

        internal LiveOpsTrackingService(LvlUpConfig config, Action<string, Dictionary<string, object>> track)
        {
            _config = config;
            _track = track;
            Guard(() =>
            {
                var entries = PlayerPrefs.GetString(PrefsKey, "").Split('\n');
                for (int i = Math.Max(0, entries.Length - 500); i < entries.Length; i++)
                    if (entries[i].Length > 0 && _sent.Add(entries[i])) _recent.Add(entries[i]);
            });
        }

        internal void Eligible(LiveOpsRun run) => Send(run, "eligible", null, true);

        internal void Impression(LiveOpsRun run, string surface, bool manual = false)
        {
            Send(run, "impression", p => { p["surface"] = Surface(surface); p["trigger"] = manual ? "manual" : "auto"; });
        }

        internal void Join(LiveOpsRun run, bool optIn, double? target = null)
        {
            Send(run, "join", p => { p["trigger"] = optIn ? "opt_in" : "auto"; Number(p, "target", target); }, true);
        }

        internal void Progress(LiveOpsRun run, double progress, double? target = null, int? stage = null, double? delta = null)
        {
            Send(run, "progress", p =>
            {
                Number(p, "progress", progress, true);
                Number(p, "target", target);
                Stage(p, stage);
                Number(p, "delta", delta);
            });
        }

        internal void Setback(LiveOpsRun run, double progress, double lost, int? stage = null)
        {
            Send(run, "setback", p =>
            {
                Number(p, "progress", progress, true);
                Require(lost > 0, "lost must be positive.");
                Number(p, "lost", lost);
                Stage(p, stage);
            });
        }

        internal void Finish(LiveOpsRun run, LiveOpsOutcome outcome, string reason = null, double? progress = null, int? stage = null)
        {
            Send(run, "finish", p =>
            {
                Require(Enum.IsDefined(typeof(LiveOpsOutcome), outcome), "Unknown outcome.");
                p["outcome"] = outcome.ToString().ToLowerInvariant();
                Text(p, "reason", LiveOpsRun.Sanitize(reason));
                Number(p, "progress", progress, true);
                Stage(p, stage);
            }, true);
        }

        internal void Claim(LiveOpsRun run, int stage, LiveOpsClaim details = null)
        {
            Send(run, "claim", p =>
            {
                Stage(p, stage);
                if (details == null) return;
                Text(p, "rewardId", details.RewardId);
                Text(p, "itemId", details.ItemId);
                if (details.Track != null)
                {
                    string track = LiveOpsRun.Sanitize(details.Track);
                    Require(track == "free" || track == "pass", "track must be free or pass.");
                    p["track"] = track;
                }
                if (details.Cost == LiveOpsCost.Soft)
                    Require(!string.IsNullOrEmpty(details.CostResource) && details.CostAmount.HasValue,
                        "Soft claims require costResource and costAmount.");
                if (details.Cost == LiveOpsCost.Iap)
                    Require(!string.IsNullOrEmpty(details.StoreItemId) && !string.IsNullOrEmpty(details.ProductId),
                        "IAP claims require storeItemId and productId.");
                Cost(p, details.Cost);
                Text(p, "costResource", details.CostResource);
                Number(p, "costAmount", details.CostAmount);
                Text(p, "storeItemId", details.StoreItemId);
                Text(p, "productId", details.ProductId);
            });
        }

        internal void ClaimFailed(LiveOpsRun run, int stage, string result, LiveOpsCost? cost = null)
        {
            Send(run, "claim_failed", p =>
            {
                Require(!string.IsNullOrEmpty(result), "result is required.");
                Stage(p, stage);
                p["result"] = LiveOpsRun.Sanitize(result);
                Cost(p, cost);
            });
        }

        internal void Purchase(LiveOpsRun run, string productId, string storeItemId = null, int? stage = null)
        {
            Send(run, "purchase", p =>
            {
                Require(!string.IsNullOrEmpty(productId), "productId is required.");
                p["productId"] = productId;
                Text(p, "storeItemId", storeItemId);
                Stage(p, stage);
            });
        }

        internal void Dismiss(LiveOpsRun run, string surface) => Send(run, "dismiss", p => p["surface"] = Surface(surface));

        internal void Revive(LiveOpsRun run, bool revived, string costResource = null, double? costAmount = null)
        {
            Send(run, "revive", p =>
            {
                p["result"] = revived ? "revived" : "declined";
                Text(p, "costResource", costResource);
                Number(p, "costAmount", costAmount);
            });
        }

        internal void Expire(LiveOpsRun run, int? unclaimed = null)
        {
            Send(run, "expire", p => { if (unclaimed.HasValue) p["unclaimed"] = Math.Max(0, unclaimed.Value); }, true);
        }

        private void Send(LiveOpsRun run, string action, Action<Dictionary<string, object>> extra, bool once = false)
        {
            Guard(() =>
            {
                Require(!string.IsNullOrEmpty(run.FeatureId) && run.FeatureId.Length <= 64,
                    "featureId must contain 1–64 characters.");
                Require(!string.IsNullOrEmpty(run.RunId) && run.RunId.Length <= 128,
                    "runId must contain 1–128 characters.");
                Require(Enum.IsDefined(typeof(LiveOpsFeatureType), run.FeatureType), "Unknown featureType.");
                string id = LiveOpsRun.Sanitize(run.FeatureId);
                string variant = string.IsNullOrEmpty(run.VariantId) ? "default" : run.VariantId;
                var properties = new Dictionary<string, object>
                {
                    { "action", action }, { "featureId", id }, { "featureType", run.FeatureType.ToString().ToLowerInvariant() },
                    { "runId", run.RunId }, { "variantId", variant.Substring(0, Math.Min(variant.Length, 64)) }
                };
                if (run.RunStartedAt.HasValue) properties["runStartedAt"] = run.RunStartedAt.Value;
                if (run.RunEndsAt.HasValue) properties["runEndsAt"] = run.RunEndsAt.Value;
                if (_config.currentLevelOrder.HasValue && _config.currentLevelOrder.Value >= 1)
                    properties["levelOrder"] = _config.currentLevelOrder.Value;
                extra?.Invoke(properties);
                if (once)
                {
                    string key = id + ":" + Convert.ToBase64String(Encoding.UTF8.GetBytes(run.RunId)) + ":" + action;
                    if (!_sent.Add(key))
                    {
                        if (_config.enableDebugLogs) Debug.Log("[LvlUp] LiveOps: Duplicate " + action + " ignored.");
                        return;
                    }
                    // Record before tracking: game callbacks may re-enter the service synchronously.
                    _recent.Add(key);
                    if (_recent.Count > 500) { _sent.Remove(_recent[0]); _recent.RemoveAt(0); }
                    PlayerPrefs.SetString(PrefsKey, string.Join("\n", _recent));
                    PlayerPrefs.Save();
                }
                _track("liveops", properties);
            });
        }

        private static string Surface(string value)
        {
            string surface = LiveOpsRun.Sanitize(value);
            switch (surface)
            {
                case "starter": case "progression": case "win": case "rewards": case "purchase":
                case "ended": case "revive": case "offer": case "chip": return surface;
                default: return "other";
            }
        }

        private static void Text(Dictionary<string, object> properties, string key, string value)
        {
            if (value != null) properties[key] = value;
        }

        private static void Stage(Dictionary<string, object> properties, int? stage)
        {
            if (stage.HasValue) properties["stage"] = Math.Max(0, stage.Value);
        }

        private static void Cost(Dictionary<string, object> properties, LiveOpsCost? cost)
        {
            if (!cost.HasValue) return;
            Require(Enum.IsDefined(typeof(LiveOpsCost), cost.Value), "Unknown cost.");
            properties["cost"] = cost.Value.ToString().ToLowerInvariant();
        }

        private static void Number(Dictionary<string, object> properties, string key, double? value, bool nonNegative = false)
        {
            if (!value.HasValue) return;
            Require(!double.IsNaN(value.Value) && !double.IsInfinity(value.Value), key + " must be finite.");
            Require(!nonNegative || value.Value >= 0, key + " must be non-negative.");
            properties[key] = value.Value;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new ArgumentException(message);
        }

        private void Guard(Action action)
        {
            try { action(); }
            catch (Exception exception)
            {
                if (_warned.Add(exception.Message)) Debug.LogWarning("[LvlUp] LiveOps: " + exception.Message);
            }
        }
    }
}
