using System;
using System.Collections.Generic;
using LvlUp.Models;
using LvlUp.Services;
using NUnit.Framework;
using UnityEngine;

namespace LvlUp.Tests
{
    public class LiveOpsTrackingServiceTests
    {
        private const string PrefsKey = "LvlUp_LiveOps_OncePerRun";
        private LvlUpConfig _config;
        private LiveOpsTrackingService _service;
        private List<(string name, Dictionary<string, object> properties)> _events;
        private LiveOpsRun Run => LiveOpsRun.ForSeason("Lava Quest", LiveOpsFeatureType.Event, 100, 2, null, 200);

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(PrefsKey);
            _config = new LvlUpConfig();
            _events = new List<(string, Dictionary<string, object>)>();
            Restart();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }

        private void Restart()
        {
            _service = new LiveOpsTrackingService(_config, (name, props) => _events.Add((name, props)));
        }

        [Test]
        public void HelpersBuildStableRunIds()
        {
            Assert.AreEqual("lava_quest:100:2", Run.RunId);
            Assert.AreEqual(100, Run.RunStartedAt);
            Assert.AreEqual(200, Run.RunEndsAt);
            var offer = LiveOpsRun.ForOffer("NoAds", 123, "control");
            Assert.AreEqual("noads:123", offer.RunId);
            Assert.AreEqual(LiveOpsFeatureType.Offer, offer.FeatureType);
            Assert.IsNull(offer.RunEndsAt);
        }

        [Test]
        public void NormalizesCommonPropertiesAndInjectsKnownLevel()
        {
            _service.Eligible(Run);
            var props = _events[0].properties;
            Assert.AreEqual("liveops", _events[0].name);
            Assert.AreEqual("lava_quest", props["featureId"]);
            Assert.AreEqual("event", props["featureType"]);
            Assert.AreEqual("lava_quest:100:2", props["runId"]);
            Assert.AreEqual("default", props["variantId"]);
            Assert.AreEqual(100L, props["runStartedAt"]);
            Assert.AreEqual(200L, props["runEndsAt"]);
            Assert.IsFalse(props.ContainsKey("levelOrder"));
            _config.currentLevelOrder = 56;
            _service.Impression(Run, "WIN");
            Assert.AreEqual(56, _events[1].properties["levelOrder"]);
            _service.Eligible(new LiveOpsRun("A-B!", LiveOpsFeatureType.Pass, "r", new string('x', 65)));
            Assert.AreEqual("a_b_", _events[2].properties["featureId"]);
            Assert.AreEqual("pass", _events[2].properties["featureType"]);
            Assert.AreEqual(new string('x', 64), _events[2].properties["variantId"]);
        }

        [Test]
        public void RejectsBadIdsWithoutThrowing()
        {
            foreach (var id in new[] { null, "", new string('a', 65) })
                Assert.DoesNotThrow(() => _service.Eligible(new LiveOpsRun(id, LiveOpsFeatureType.Event, "r", null)));
            foreach (var id in new[] { null, "", new string('a', 129) })
                Assert.DoesNotThrow(() => _service.Eligible(new LiveOpsRun("event", LiveOpsFeatureType.Event, id, null)));
            Assert.DoesNotThrow(() => _service.Eligible(default(LiveOpsRun)));
            Assert.AreEqual(0, _events.Count);
        }

        [Test]
        public void EveryActionUsesContractPropertyNames()
        {
            _service.Eligible(Run);
            Extras("eligible");
            _service.Impression(Run, "progression", true);
            Extras("impression", "surface", "trigger");
            Assert.AreEqual("manual", Last["trigger"]);
            _service.Join(Run, true, 7);
            Extras("join", "trigger", "target");
            Assert.AreEqual("opt_in", Last["trigger"]);
            _service.Progress(Run, 3, 7, 2, 1);
            Extras("progress", "progress", "target", "stage", "delta");
            _service.Setback(Run, 2, 1, 2);
            Extras("setback", "progress", "lost", "stage");
            _service.Finish(Run, LiveOpsOutcome.Failed, "Level Failed!", 2, 2);
            Extras("finish", "outcome", "reason", "progress", "stage");
            Assert.AreEqual("failed", Last["outcome"]);
            Assert.AreEqual("level_failed_", Last["reason"]);
            _service.Claim(Run, 2, new LiveOpsClaim
            {
                RewardId = "RewardA", ItemId = "BoosterA", Track = "pass", Cost = LiveOpsCost.Soft,
                CostResource = "coin", CostAmount = 5, StoreItemId = "StoreA", ProductId = "com.game.a"
            });
            Extras("claim", "stage", "rewardId", "itemId", "track", "cost", "costResource", "costAmount", "storeItemId", "productId");
            Assert.AreEqual("BoosterA", Last["itemId"]);
            Assert.AreEqual("soft", Last["cost"]);
            _service.ClaimFailed(Run, 2, "PurchaseFailed", LiveOpsCost.Iap);
            Extras("claim_failed", "stage", "result", "cost");
            Assert.AreEqual("purchasefailed", Last["result"]);
            _service.Purchase(Run, "com.game.a", "StoreA", 2);
            Extras("purchase", "productId", "storeItemId", "stage");
            _service.Dismiss(Run, "offer");
            Extras("dismiss", "surface");
            _service.Revive(Run, true, "gem", 2);
            Extras("revive", "result", "costResource", "costAmount");
            Assert.AreEqual("revived", Last["result"]);
            _service.Expire(Run, 3);
            Extras("expire", "unclaimed");
        }

        private Dictionary<string, object> Last => _events[_events.Count - 1].properties;

        private void Extras(string action, params string[] keys)
        {
            Assert.AreEqual("liveops", _events[_events.Count - 1].name);
            Assert.AreEqual(action, Last["action"]);
            var expected = new List<string> { "action", "featureId", "featureType", "runId", "variantId", "runStartedAt", "runEndsAt" };
            expected.AddRange(keys);
            CollectionAssert.AreEquivalent(expected, Last.Keys);
        }

        [Test]
        public void OptionalPropertiesAreOmittedAndStagesAreClamped()
        {
            var run = LiveOpsRun.ForOffer("noads", 100, null);
            _service.Claim(run, -2);
            Assert.AreEqual(0, Last["stage"]);
            Assert.AreEqual(7, Last.Count);
            Assert.IsFalse(Last.ContainsKey("runEndsAt"));
            Assert.IsFalse(Last.ContainsKey("rewardId"));
            _service.Progress(run, 0, stage: 201);
            Assert.AreEqual(201, Last["stage"]);
            _service.Expire(run, -1);
            Assert.AreEqual(0, Last["unclaimed"]);
            _service.Revive(run, false);
            Assert.AreEqual("declined", Last["result"]);
        }

        [Test]
        public void OncePerRunSurvivesRestartAndKeepsActionsAndRunsDistinct()
        {
            Action send = () => { _service.Eligible(Run); _service.Join(Run, false); _service.Finish(Run, LiveOpsOutcome.Won); _service.Expire(Run); };
            send();
            send();
            Restart();
            send();
            Assert.AreEqual(4, _events.Count);
            _service.Eligible(LiveOpsRun.ForSeason("lava_quest", LiveOpsFeatureType.Event, 100, 3, null));
            _service.Eligible(new LiveOpsRun("other", LiveOpsFeatureType.Event, Run.RunId, null));
            _service.Impression(Run, "starter");
            _service.Impression(Run, "starter");
            Assert.AreEqual(8, _events.Count);
        }

        [Test]
        public void GuardRetainsOnlyMostRecent500Entries()
        {
            for (int i = 0; i < 501; i++)
                _service.Eligible(new LiveOpsRun("event", LiveOpsFeatureType.Event, i.ToString(), null));
            Restart();
            _service.Eligible(new LiveOpsRun("event", LiveOpsFeatureType.Event, "500", null));
            Assert.AreEqual(501, _events.Count);
            _service.Eligible(new LiveOpsRun("event", LiveOpsFeatureType.Event, "0", null));
            Assert.AreEqual(502, _events.Count);
        }

        [TestCase("starter")]
        [TestCase("progression")]
        [TestCase("win")]
        [TestCase("rewards")]
        [TestCase("purchase")]
        [TestCase("ended")]
        [TestCase("revive")]
        [TestCase("offer")]
        [TestCase("chip")]
        [TestCase("other")]
        public void PreservesKnownSurfaces(string surface)
        {
            _service.Impression(Run, surface);
            Assert.AreEqual(surface, Last["surface"]);
            Assert.AreEqual("auto", Last["trigger"]);
        }

        [TestCase(null)]
        [TestCase("unknown")]
        public void MapsUnknownSurfaces(string surface)
        {
            _service.Dismiss(Run, surface);
            Assert.AreEqual("other", Last["surface"]);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void RejectsNonFiniteNumbers(double value)
        {
            _service.Join(Run, false, value);
            _service.Progress(Run, value);
            _service.Progress(Run, 1, value);
            _service.Progress(Run, 1, delta: value);
            _service.Setback(Run, value, 1);
            _service.Setback(Run, 1, value);
            _service.Finish(Run, LiveOpsOutcome.Won, progress: value);
            _service.Claim(Run, 1, new LiveOpsClaim { CostAmount = value });
            _service.Revive(Run, true, costAmount: value);
            Assert.AreEqual(0, _events.Count);
            _service.Join(Run, false);
            Assert.AreEqual(1, _events.Count);
        }

        [Test]
        public void RejectsClaimsMissingConditionalPaymentDetails()
        {
            _service.Claim(Run, 1, new LiveOpsClaim { Cost = LiveOpsCost.Soft });
            _service.Claim(Run, 1, new LiveOpsClaim { Cost = LiveOpsCost.Soft, CostResource = "coin" });
            _service.Claim(Run, 1, new LiveOpsClaim { Cost = LiveOpsCost.Soft, CostAmount = 1 });
            _service.Claim(Run, 1, new LiveOpsClaim { Cost = LiveOpsCost.Iap });
            _service.Claim(Run, 1, new LiveOpsClaim { Cost = LiveOpsCost.Iap, ProductId = "com.game.a" });
            _service.Claim(Run, 1, new LiveOpsClaim { Cost = LiveOpsCost.Iap, StoreItemId = "StoreA" });
            Assert.AreEqual(0, _events.Count);
            _service.Claim(Run, 1, new LiveOpsClaim { Cost = LiveOpsCost.Iap, ProductId = "com.game.a", StoreItemId = "StoreA" });
            Assert.AreEqual(1, _events.Count);
        }

        [TestCase(LiveOpsOutcome.Won, "won")]
        [TestCase(LiveOpsOutcome.Failed, "failed")]
        [TestCase(LiveOpsOutcome.Timeout, "timeout")]
        [TestCase(LiveOpsOutcome.Disabled, "disabled")]
        public void SerializesOutcomes(LiveOpsOutcome outcome, string expected)
        {
            _service.Finish(Run, outcome);
            Assert.AreEqual(expected, Last["outcome"]);
        }

        [Test]
        public void ContainsTrackingCallbackFailuresAndReentry()
        {
            _service = new LiveOpsTrackingService(_config, (name, props) =>
            {
                _events.Add((name, props));
                _service.Eligible(Run);
                throw new InvalidOperationException("Game callback failed.");
            });
            Assert.DoesNotThrow(() => _service.Eligible(Run));
            Assert.AreEqual(1, _events.Count);
        }

        [Test]
        public void RejectsInvalidRequiredValues()
        {
            _service.Progress(Run, -1);
            _service.Setback(Run, 1, 0);
            _service.Finish(Run, (LiveOpsOutcome)99);
            _service.Purchase(Run, "");
            _service.ClaimFailed(Run, 1, null);
            _service.Claim(Run, 1, new LiveOpsClaim { Track = "invalid" });
            Assert.AreEqual(0, _events.Count);
        }
    }
}
