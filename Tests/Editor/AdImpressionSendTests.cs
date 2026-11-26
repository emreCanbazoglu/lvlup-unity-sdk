using System;
using System.Collections.Generic;
using System.Reflection;
using LvlUp.Models;
using LvlUp.Services;
using NUnit.Framework;
using UnityEngine;

namespace LvlUp.Tests
{
    public class AdImpressionSendTests
    {
        private const string CountKey = "LvlUp_OfflineRevenueCount";
        private const string RevenuePrefix = "LvlUp_OfflineRevenue_";
        private const int SavedSlots = 8;

        private bool _hadCount;
        private int _oldCount;
        private Dictionary<string, string> _saved;

        private List<List<RevenueData>> _sent;
        private List<Action<bool>> _pending;

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

            _sent = new List<List<RevenueData>>();
            _pending = new List<Action<bool>>();
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
        public void AdImpressionIsSentAsSoonAsItIsTracked()
        {
            var tracker = CreateTracker(true);
            tracker.TrackAdImpression("AppLovin", "INTER", 0.01);

            Assert.AreEqual(1, _sent.Count);
            Assert.AreEqual(1, _sent[0].Count);
            Assert.AreEqual("AD_IMPRESSION", _sent[0][0].revenueType);
            Assert.AreEqual(0, GetQueue(tracker).Count);
        }

        [Test]
        public void PurchaseWaitsForTheBatch()
        {
            var tracker = CreateTracker(true);
            tracker.TrackInAppPurchase("coins_100", 1.99, "USD", 1.99, "txn_1");

            Assert.AreEqual(0, _sent.Count);
            Assert.AreEqual(1, GetQueue(tracker).Count);
        }

        [Test]
        public void AdImpressionWaitsWhenTheSwitchIsOff()
        {
            var tracker = CreateTracker(false);
            tracker.TrackAdImpression("AppLovin", "INTER", 0.01);

            Assert.AreEqual(0, _sent.Count);
            Assert.AreEqual(1, GetQueue(tracker).Count);
        }

        [Test]
        public void ImpressionTrackedDuringASendGoesOutWhenItSucceeds()
        {
            var tracker = CreateTracker(true);
            tracker.TrackAdImpression("AppLovin", "INTER", 0.01);
            tracker.TrackAdImpression("AppLovin", "REWARDED", 0.02);

            Assert.AreEqual(1, _sent.Count, "only one request at a time");
            Assert.AreEqual(1, GetQueue(tracker).Count);

            _pending[0](true);

            Assert.AreEqual(2, _sent.Count);
            Assert.AreEqual("REWARDED", _sent[1][0].adFormat);
            Assert.AreEqual(0, GetQueue(tracker).Count);
        }

        [Test]
        public void FailedSendKeepsTheImpressionAndWaitsForTheInterval()
        {
            var tracker = CreateTracker(true);
            tracker.TrackAdImpression("AppLovin", "INTER", 0.01);
            string impressionId = _sent[0][0].adImpressionId;
            tracker.TrackAdImpression("AppLovin", "REWARDED", 0.02);

            _pending[0](false);

            Assert.AreEqual(1, _sent.Count, "no immediate retry after a failure");
            var queued = GetQueue(tracker);
            Assert.AreEqual(2, queued.Count);
            Assert.AreEqual(impressionId, queued[0].adImpressionId, "failed item keeps its place and ID");

            var restored = CreateTracker(true);
            restored.SendBatchOverride = null;
            restored.Initialize();
            Assert.AreEqual(2, GetQueue(restored).Count, "both are on disk for the next launch");
        }

        [Test]
        public void SentImpressionIsClearedFromDisk()
        {
            var tracker = CreateTracker(true);
            tracker.TrackAdImpression("AppLovin", "INTER", 0.01);
            _pending[0](true);

            Assert.AreEqual(0, PlayerPrefs.GetInt(CountKey, 0));
        }

        private RevenueTrackingService CreateTracker(bool sendAdImpressionsImmediately)
        {
            var config = new LvlUpConfig
            {
                eventBatchSize = 1000,
                persistQueueToDisk = true,
                sendAdImpressionsImmediately = sendAdImpressionsImmediately
            };
            var tracker = new RevenueTrackingService(null, config, null, () => "test_user", () => "test_session",
                () => 1, () => null, () => null, () => "android", () => "test");
            tracker.SendBatchOverride = (batch, done) =>
            {
                _sent.Add(new List<RevenueData>(batch));
                _pending.Add(done);
            };
            return tracker;
        }

        private static List<RevenueData> GetQueue(RevenueTrackingService tracker)
        {
            return (List<RevenueData>)typeof(RevenueTrackingService)
                .GetField("_revenueBatch", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(tracker);
        }
    }
}
