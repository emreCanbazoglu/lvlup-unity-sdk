using System;
using System.Collections.Generic;
using System.Reflection;
using LvlUp.Models;
using LvlUp.Services;
using NUnit.Framework;
using UnityEngine;

namespace LvlUp.Tests
{
    public class RevenueTrackingPersistenceTests
    {
        private const string CountKey = "LvlUp_OfflineRevenueCount";
        private const string RevenuePrefix = "LvlUp_OfflineRevenue_";
        private const int SavedSlots = 4;

        private bool _hadCount;
        private int _oldCount;
        private Dictionary<string, string> _saved;

        [SetUp]
        public void SetUp()
        {
            _hadCount = PlayerPrefs.HasKey(CountKey);
            _oldCount = PlayerPrefs.GetInt(CountKey, 0);
            _saved = new Dictionary<string, string>();
            for (int i = 0; i < Math.Max(_oldCount, SavedSlots); i++)
            {
                string key = RevenuePrefix + i;
                if (PlayerPrefs.HasKey(key)) _saved[key] = PlayerPrefs.GetString(key);
                PlayerPrefs.DeleteKey(key);
            }
            PlayerPrefs.DeleteKey(CountKey);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < Math.Max(_oldCount, SavedSlots); i++) PlayerPrefs.DeleteKey(RevenuePrefix + i);
            foreach (var pair in _saved) PlayerPrefs.SetString(pair.Key, pair.Value);
            if (_hadCount) PlayerPrefs.SetInt(CountKey, _oldCount);
            else PlayerPrefs.DeleteKey(CountKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void AdImpressionsGetUniqueImpressionIds()
        {
            var tracker = CreateTracker(null);
            tracker.TrackAdImpression("AppLovin", "INTER", 0.01);
            tracker.TrackAdImpression("AppLovin", "INTER", 0.01);

            var queued = GetQueue(tracker);
            Assert.IsFalse(string.IsNullOrEmpty(queued[0].adImpressionId));
            Assert.AreNotEqual(queued[0].adImpressionId, queued[1].adImpressionId);
        }

        [Test]
        public void CallerImpressionIdIsKept()
        {
            var tracker = CreateTracker(null);
            tracker.TrackRevenue(new RevenueData { revenueType = "AD_IMPRESSION", adImpressionId = "network_id" });

            Assert.AreEqual("network_id", GetQueue(tracker)[0].adImpressionId);
        }

        [Test]
        public void PurchasesGetNoImpressionId()
        {
            var tracker = CreateTracker(null);
            tracker.TrackInAppPurchase("coins_100", 1.99, "USD", 1.99, "txn_1");

            Assert.IsNull(GetQueue(tracker)[0].adImpressionId);
        }

        [Test]
        public void PersistedRevenueKeepsImpressionIdAndAbTests()
        {
            var abTests = new Dictionary<string, string> { { "first_win_inter_level", "late_inter" } };
            var tracker = CreateTracker(abTests);
            tracker.TrackAdImpression("AppLovin", "INTER", 0.01);
            tracker.TrackInAppPurchase("coins_100", 1.99, "USD", 1.99, "txn_1");
            GetQueue(tracker)[1].currentLevelOrder = 17;
            string impressionId = GetQueue(tracker)[0].adImpressionId;
            tracker.PersistRevenue();

            var restored = CreateTracker(null);
            restored.Initialize();

            var queued = GetQueue(restored);
            Assert.AreEqual(2, queued.Count);
            Assert.AreEqual(impressionId, queued[0].adImpressionId);
            Assert.AreEqual("late_inter", queued[0].abTests["first_win_inter_level"]);
            Assert.AreEqual(17, queued[1].currentLevelOrder);
        }

        [Test]
        public void RevenuePersistedByEarlierVersionsStillLoads()
        {
            var legacy = new RevenueData { revenueType = "AD_IMPRESSION", revenue = 0.02, adFormat = "INTER" };
            PlayerPrefs.SetString(RevenuePrefix + "0", JsonUtility.ToJson(legacy));
            PlayerPrefs.SetInt(CountKey, 1);

            var restored = CreateTracker(null);
            restored.Initialize();

            var queued = GetQueue(restored);
            Assert.AreEqual(1, queued.Count);
            Assert.AreEqual("INTER", queued[0].adFormat);
            Assert.AreEqual(0.02, queued[0].revenue);
        }

        private static RevenueTrackingService CreateTracker(Dictionary<string, string> abTests)
        {
            var config = new LvlUpConfig { eventBatchSize = 1000, persistQueueToDisk = true };
            return new RevenueTrackingService(null, config, null, () => "test_user", () => "test_session",
                () => 1, () => abTests, () => null, () => "android", () => "test");
        }

        private static List<RevenueData> GetQueue(RevenueTrackingService tracker)
        {
            return (List<RevenueData>)typeof(RevenueTrackingService)
                .GetField("_revenueBatch", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(tracker);
        }
    }
}
