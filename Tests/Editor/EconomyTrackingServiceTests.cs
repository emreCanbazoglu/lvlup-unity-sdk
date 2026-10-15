using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using LvlUp.Models;
using LvlUp.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LvlUp.Tests
{
    public class EconomyTrackingServiceTests
    {
        private LvlUpConfig _config;
        private EconomyTrackingService _service;
        private List<(string name, Dictionary<string, object> properties)> _events;
        private double _time;

        [SetUp]
        public void SetUp()
        {
            _config = new LvlUpConfig();
            _events = new List<(string, Dictionary<string, object>)>();
            _time = 0;
            _service = new EconomyTrackingService(_config, (name, props) => _events.Add((name, props)), () => _time);
            _service.RegisterResource("coin", ResourceType.Currency);
        }

        [Test]
        public void SanitizesIdsAndReasonsAndOmitsUnknownLevel()
        {
            _service.Source("COIN", 5, "Daily Reward!", 10);
            var props = _events[0].properties;
            Assert.AreEqual("economy_flow", _events[0].name);
            Assert.AreEqual("coin", props["resourceId"]);
            Assert.AreEqual("currency", props["resourceType"]);
            Assert.AreEqual("daily_reward_", props["reason"]);
            Assert.AreEqual(false, props["inLevel"]);
            Assert.IsFalse(props.ContainsKey("levelOrder"));
            _service.Source("coin", 1, null, 11);
            Assert.AreEqual("unknown", _events[1].properties["reason"]);
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void RejectsInvalidAmounts(double amount)
        {
            LogAssert.Expect(LogType.Error, new Regex("\\[LvlUp\\] Economy:"));
            Assert.DoesNotThrow(() => _service.Source("coin", amount, "reward", 10));
            Assert.AreEqual(0, _events.Count);
        }

        [TestCase(-1)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void RejectsInvalidBalances(double balance)
        {
            LogAssert.Expect(LogType.Error, new Regex("\\[LvlUp\\] Economy:"));
            _service.Sink("coin", 1, "store", balance);
            Assert.AreEqual(0, _events.Count);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklm")]
        public void RejectsInvalidResourceIds(string id)
        {
            LogAssert.Expect(LogType.Error, new Regex("\\[LvlUp\\] Economy:"));
            _service.Source(id, 1, "reward", 10);
            Assert.AreEqual(0, _events.Count);
        }

        [Test]
        public void FillsBalanceFromProviderAndCapturesLevelContext()
        {
            _config.currentLevelOrder = 42;
            _config.inLevel = true;
            _service.SetBalanceProvider(() => new Dictionary<string, double> { { "COIN", 12 } });
            _service.Source("coin", 2, "reward");
            Assert.AreEqual(12d, _events[0].properties["balanceAfter"]);
            Assert.AreEqual(42, _events[0].properties["levelOrder"]);
            Assert.AreEqual(true, _events[0].properties["inLevel"]);
            _config.inLevel = false;
            _service.Sink("coin", 1, "continue", 11);
            Assert.AreEqual(false, _events[1].properties["inLevel"]);
        }

        [Test]
        public void MissingProviderOrMissingBalanceDropsFlow()
        {
            LogAssert.Expect(LogType.Error, new Regex("\\[LvlUp\\] Economy:"));
            _service.Source("coin", 1, "reward");
            _service.SetBalanceProvider(() => new Dictionary<string, double>());
            LogAssert.Expect(LogType.Error, new Regex("\\[LvlUp\\] Economy:"));
            _service.Source("coin", 1, "reward");
            Assert.AreEqual(0, _events.Count);
        }

        [Test]
        public void ProviderExceptionsDoNotEscape()
        {
            _service.SetBalanceProvider(() => throw new InvalidOperationException("wallet unavailable"));
            LogAssert.Expect(LogType.Error, new Regex("\\[LvlUp\\] Economy:"));
            Assert.DoesNotThrow(() => _service.Source("coin", 1, "reward"));
            LogAssert.Expect(LogType.Error, new Regex("\\[LvlUp\\] Economy:"));
            Assert.DoesNotThrow(() => _service.Snapshot());
            Assert.AreEqual(0, _events.Count);
        }

        [Test]
        public void UnregisteredTypeWarnsOnceAndExplicitTypeWins()
        {
            LogAssert.Expect(LogType.Warning, new Regex("\\[LvlUp\\] Economy:"));
            _service.Source("New Resource", 1, "reward", 1);
            _service.Source("new_resource", 1, "reward", 2);
            Assert.AreEqual("other", _events[0].properties["resourceType"]);
            _service.Source("coin", 1, "reward", 3, type: ResourceType.Consumable);
            Assert.AreEqual("consumable", _events[2].properties["resourceType"]);
        }

        [Test]
        public void TradeEmitsSeparateLegsWithOneTransactionAndOneProviderRead()
        {
            _config.economyCoalesceSeconds = 10;
            _service.RegisterResource("booster_a", ResourceType.Booster);
            int reads = 0;
            _service.SetBalanceProvider(() => { reads++; return new Dictionary<string, double> { { "coin", 350 }, { "booster_a", 3 } }; });
            _service.Trade("Booster Purchase", "booster_a",
                new[] { ("coin", 900d) }, new[] { ("booster_a", 3d) });
            Assert.AreEqual(2, _events.Count);
            Assert.AreEqual(1, reads);
            Assert.AreEqual("sink", _events[0].properties["flowType"]);
            Assert.AreEqual("source", _events[1].properties["flowType"]);
            Assert.AreEqual(_events[0].properties["transactionId"], _events[1].properties["transactionId"]);
            Assert.AreEqual(32, ((string)_events[0].properties["transactionId"]).Length);
            Assert.AreEqual(350d, _events[0].properties["balanceAfter"]);
            Assert.AreEqual(3d, _events[1].properties["balanceAfter"]);
        }

        [Test]
        public void InvalidTradeDoesNotEmitPartialTransaction()
        {
            _service.SetBalanceProvider(() => new Dictionary<string, double> { { "coin", 5 } });
            LogAssert.Expect(LogType.Error, new Regex("\\[LvlUp\\] Economy:"));
            _service.Trade("store", null, new[] { ("coin", 1d) }, new[] { ("coin", 0d) });
            Assert.AreEqual(0, _events.Count);
        }

        [Test]
        public void SnapshotIncludesRegisteredZerosAndProviderExtras()
        {
            _service.RegisterResource("booster_a", ResourceType.Booster);
            _service.SetBalanceProvider(() => new Dictionary<string, double> { { "coin", 15 }, { "lives", 2 } });
            LogAssert.Expect(LogType.Warning, new Regex("\\[LvlUp\\] Economy:"));
            _service.Snapshot();
            var props = _events[0].properties;
            var balances = (Dictionary<string, double>)props["balances"];
            var types = (Dictionary<string, string>)props["resourceTypes"];
            Assert.AreEqual(0d, balances["booster_a"]);
            Assert.AreEqual(2d, balances["lives"]);
            Assert.AreEqual("booster", types["booster_a"]);
            Assert.AreEqual(balances.Count, types.Count);
            Assert.AreEqual("manual", props["trigger"]);
            Assert.IsFalse(props.ContainsKey("inLevel"));
        }

        [Test]
        public void AutomaticSnapshotsRequireProvider()
        {
            _service.AutoSnapshot("session_start");
            Assert.AreEqual(0, _events.Count);
            _service.SetBalanceProvider(() => new Dictionary<string, double>());
            _service.AutoSnapshot("session_start");
            _config.currentLevelOrder = 7;
            _service.AutoSnapshot("level_start");
            Assert.AreEqual("session_start", _events[0].properties["trigger"]);
            Assert.AreEqual("level_start", _events[1].properties["trigger"]);
            Assert.AreEqual(7, _events[1].properties["levelOrder"]);
        }

        [Test]
        public void OversizedSnapshotIsDroppedRatherThanOmittingRegisteredResources()
        {
            var balances = new Dictionary<string, double>();
            for (int i = 0; i < 51; i++) balances["resource_" + i] = i;
            LogAssert.Expect(LogType.Error, new Regex("\\[LvlUp\\] Economy:"));
            _service.Snapshot(balances);
            Assert.AreEqual(0, _events.Count);
        }

        [Test]
        public void CoalescesSourcesUntilDeadlineKeepingLastBalance()
        {
            _config.economyCoalesceSeconds = 2;
            _service.Source("coin", 2, "reward", 5);
            _time = 1;
            _service.Source("coin", 3, "reward", 8);
            Assert.AreEqual(0, _events.Count);
            _time = 2;
            _service.FlushExpired();
            Assert.AreEqual(1, _events.Count);
            Assert.AreEqual(5d, _events[0].properties["amount"]);
            Assert.AreEqual(8d, _events[0].properties["balanceAfter"]);
        }

        [Test]
        public void CoalesceKeyIncludesReasonLevelStateAndItem()
        {
            _config.economyCoalesceSeconds = 10;
            _service.Source("coin", 1, "a", 1);
            _service.Source("coin", 1, "b", 2);
            _config.currentLevelOrder = 1;
            _service.Source("coin", 1, "a", 3);
            _config.inLevel = true;
            _service.Source("coin", 1, "a", 4);
            _service.Source("coin", 1, "a", 5, "item");
            _service.Flush();
            Assert.AreEqual(5, _events.Count);
        }

        [Test]
        public void SinkAndSnapshotFlushPendingSourcesBeforeSending()
        {
            _config.economyCoalesceSeconds = 10;
            _service.Source("coin", 2, "reward", 5);
            _service.Sink("coin", 1, "store", 4);
            Assert.AreEqual("source", _events[0].properties["flowType"]);
            Assert.AreEqual("sink", _events[1].properties["flowType"]);
            _service.Source("coin", 2, "reward", 6);
            _service.Snapshot(new Dictionary<string, double> { { "coin", 6 } });
            Assert.AreEqual("economy_flow", _events[2].name);
            Assert.AreEqual("economy_snapshot", _events[3].name);
        }
        [Test]
        public void DeadlineFlushesBeforeStartingANewWindow()
        {
            _config.economyCoalesceSeconds = 2;
            _service.Source("coin", 1, "reward", 1);
            _time = 2;
            _service.Source("coin", 2, "reward", 3);
            Assert.AreEqual(1, _events.Count);
            Assert.AreEqual(1d, _events[0].properties["amount"]);
            _service.Flush();
            Assert.AreEqual(2d, _events[1].properties["amount"]);
        }

        [Test]
        public void TradeFlushesOnlyItsResourcesAndNeverCoalescesItsSources()
        {
            _config.economyCoalesceSeconds = 10;
            _service.RegisterResource("gem", ResourceType.Currency);
            _service.Source("coin", 1, "reward", 2);
            _service.Source("gem", 1, "reward", 2);
            _service.SetBalanceProvider(() => new Dictionary<string, double> { { "coin", 2 } });
            _service.Trade("store", null, new[] { ("coin", 1d) }, new[] { ("coin", 1d) });
            Assert.AreEqual(3, _events.Count);
            Assert.AreEqual("reward", _events[0].properties["reason"]);
            Assert.IsFalse(_events[0].properties.ContainsKey("transactionId"));
            _service.Flush();
            Assert.AreEqual("gem", _events[3].properties["resourceId"]);
        }

        [Test]
        public void ExplicitSnapshotWorksWithoutProviderAndRegisteredResourcesArePreserved()
        {
            _service.Snapshot(new Dictionary<string, double>());
            Assert.AreEqual(0d, ((Dictionary<string, double>)_events[0].properties["balances"])["coin"]);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void NonPositiveLevelOrderIsOmitted(int order)
        {
            _config.currentLevelOrder = order;
            _service.Source("coin", 1, "reward", 1);
            Assert.IsFalse(_events[0].properties.ContainsKey("levelOrder"));
        }

        [Test]
        public void LongReasonIsBoundedAndInvalidItemIsDropped()
        {
            _service.Source("coin", 1, new string('A', 80), 1);
            Assert.AreEqual(new string('a', 64), _events[0].properties["reason"]);
            LogAssert.Expect(LogType.Error, new Regex("\\[LvlUp\\] Economy:"));
            _service.Source("coin", 1, "store", 2, new string('a', 65));
            Assert.AreEqual(1, _events.Count);
        }

        [Test]
        public void InvalidSnapshotBalanceIsDropped()
        {
            LogAssert.Expect(LogType.Error, new Regex("\\[LvlUp\\] Economy:"));
            _service.Snapshot(new Dictionary<string, double> { { "coin", double.NaN } });
            Assert.AreEqual(0, _events.Count);
        }

        [Test]
        public void TransportExceptionsDoNotEscape()
        {
            _service = new EconomyTrackingService(_config, (name, props) => throw new InvalidOperationException("tracking failed"));
            _service.RegisterResource("coin", ResourceType.Currency);
            LogAssert.Expect(LogType.Error, new Regex("\\[LvlUp\\] Economy:"));
            Assert.DoesNotThrow(() => _service.Source("coin", 1, "reward", 1));
        }

        [Test]
        public void FlushIsSafeWhenTrackingReentersFlush()
        {
            _config.economyCoalesceSeconds = 10;
            _service = new EconomyTrackingService(_config, (name, props) =>
            {
                _events.Add((name, props));
                _service.Flush();
            });
            _service.RegisterResource("coin", ResourceType.Currency);
            _service.Source("coin", 1, "reward", 1);
            _service.Flush();
            Assert.AreEqual(1, _events.Count);
            _service.Flush();
            Assert.AreEqual(1, _events.Count);
        }

    }
}
