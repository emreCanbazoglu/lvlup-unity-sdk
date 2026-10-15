using System;
using System.Collections.Generic;
using System.Reflection;
using LvlUp.Models;
using LvlUp.Services;
using NUnit.Framework;
using UnityEngine;

namespace LvlUp.Tests
{
    public class EventTrackingPersistenceTests
    {
        [Test]
        public void EconomyPropertiesSurviveOfflinePersistence()
        {
            const string countKey = "LvlUp_OfflineEventCount";
            const string eventPrefix = "LvlUp_OfflineEvents_";
            bool hadCount = PlayerPrefs.HasKey(countKey);
            int oldCount = PlayerPrefs.GetInt(countKey, 0);
            var saved = new Dictionary<string, string>();
            for (int i = 0; i < Math.Max(oldCount, 2); i++)
            {
                string key = eventPrefix + i;
                if (PlayerPrefs.HasKey(key)) saved[key] = PlayerPrefs.GetString(key);
            }
            try
            {
                PlayerPrefs.DeleteKey(countKey);
                var config = new LvlUpConfig { eventBatchSize = 1000, persistQueueToDisk = true };
                var tracker = CreateTracker(config);
                var economy = new EconomyTrackingService(config, (name, props) => tracker.TrackEvent(name, props));
                economy.RegisterResource("coin", ResourceType.Currency);
                economy.Source("coin", 2, "reward", 10);
                economy.Snapshot(new Dictionary<string, double> { { "coin", 10 } });
                tracker.PersistEvents();
                string flowJson = PlayerPrefs.GetString(eventPrefix + "0");
                Assert.IsTrue(flowJson.Contains("\"resourceId\":\"coin\""));
                string snapshotJson = PlayerPrefs.GetString(eventPrefix + "1");
                Assert.IsTrue(snapshotJson.Contains("\"balances\":{\"coin\":10"));
                var restored = CreateTracker(config);
                restored.Initialize();
                var events = (List<LvlUpEvent>)typeof(EventTrackingService)
                    .GetField("_eventBatch", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(restored);
                Assert.AreEqual(2, events.Count);
                Assert.AreEqual("coin", events[0].properties["resourceId"]);
                Assert.AreEqual(10d, Convert.ToDouble(events[0].properties["balanceAfter"]));
                Assert.AreEqual("manual", events[1].properties["trigger"]);
                Assert.IsTrue(events[1].properties["balances"].ToString().Contains("coin"));
            }
            finally
            {
                for (int i = 0; i < Math.Max(oldCount, 2); i++) PlayerPrefs.DeleteKey(eventPrefix + i);
                foreach (var pair in saved) PlayerPrefs.SetString(pair.Key, pair.Value);
                if (hadCount) PlayerPrefs.SetInt(countKey, oldCount);
                else PlayerPrefs.DeleteKey(countKey);
                PlayerPrefs.Save();
            }
        }

        [Test]
        public void QueueFlushDrainsCoalescedSourcesOnceEvenWhenReentered()
        {
            var config = new LvlUpConfig { eventBatchSize = 1, economyCoalesceSeconds = 10 };
            var tracker = CreateTracker(config);
            // Model an in-flight send, so the queue flush needs no HTTP or coroutine runner.
            typeof(EventTrackingService).GetField("_isSendingEvents", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(tracker, true);
            var economy = new EconomyTrackingService(config, (name, props) => tracker.TrackEvent(name, props));
            tracker.BeforeFlush = () => economy.Flush();
            economy.RegisterResource("coin", ResourceType.Currency);
            economy.Source("coin", 2, "reward", 10);
            tracker.FlushEventQueue();
            tracker.FlushEventQueue();
            var events = (List<LvlUpEvent>)typeof(EventTrackingService)
                .GetField("_eventBatch", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(tracker);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual("economy_flow", events[0].eventName);
            Assert.AreEqual(2d, events[0].properties["amount"]);
        }

        private static EventTrackingService CreateTracker(LvlUpConfig config)
        {
            return new EventTrackingService(null, config, null, () => "test_user", () => "test_session",
                () => 1, () => null, null);
        }
    }
}
