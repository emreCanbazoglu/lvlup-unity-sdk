using System.Collections.Generic;
using System.Reflection;
using LvlUp.Models;
using LvlUp.Services;
using NUnit.Framework;
using UnityEngine;

namespace LvlUp.Tests
{
    public class EconomyLifecycleTests
    {
        private GameObject _object;
        private LvlUpManager _manager;
        private LvlUpConfig _config;
        private List<LvlUpEvent> _events;
        private LvlUpManager _previousManager;

        [SetUp]
        public void SetUp()
        {
            _previousManager = (LvlUpManager)typeof(LvlUpManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            _object = new GameObject("Economy lifecycle test");
            _object.SetActive(false); // Avoid Awake's persistent scene object in EditMode.
            _manager = _object.AddComponent<LvlUpManager>();
            typeof(LvlUpManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, _manager);
            _config = new LvlUpConfig { eventBatchSize = 1000, persistQueueToDisk = false };
            SetField("_config", _config);
            SetField("_isInitialized", true);
            var tracking = new EventTrackingService(null, _config, _manager, () => "test_user", () => "test_session",
                () => 1, () => null, null);
            SetField("_eventTrackingService", tracking);
            _events = (List<LvlUpEvent>)typeof(EventTrackingService).GetField("_eventBatch", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(tracking);
            var economy = new EconomyTrackingService(_config, (name, props) => _manager.TrackEvent(name, props));
            typeof(LvlUpManager).GetProperty("EconomyTracking", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_manager, economy);
            LvlUpSDK.Economy.RegisterResource("coin", ResourceType.Currency);
            LvlUpSDK.Economy.SetBalanceProvider(() => new Dictionary<string, double> { { "coin", 10 } });
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_object);
            typeof(LvlUpManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, _previousManager);
        }

        private void SetField(string name, object value)
        {
            typeof(LvlUpManager).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_manager, value);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LevelStartQueuesSnapshotAfterLevelEvent(bool named)
        {
            if (named) LvlUpEvents.TrackLevelStart(42, "Test level");
            else LvlUpEvents.TrackLevelStart(42);
            Assert.IsTrue(_config.inLevel);
            Assert.AreEqual(42, _config.currentLevelOrder);
            Assert.AreEqual("level_start", _events[0].eventName);
            Assert.AreEqual("economy_snapshot", _events[1].eventName);
            Assert.AreEqual("level_start", _events[1].properties["trigger"]);
            Assert.AreEqual(42, _events[1].properties["levelOrder"]);
            LvlUpSDK.Economy.Source("coin", 1, "reward");
            Assert.AreEqual(true, _events[2].properties["inLevel"]);
            Assert.AreEqual(42, _events[2].properties["levelOrder"]);
            Assert.AreEqual(1, _events[2].sessionNum);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void EveryLevelResultOverloadClearsInLevel(int overload)
        {
            LvlUpEvents.TrackLevelStart(42);
            switch (overload)
            {
                case 0: LvlUpEvents.TrackLevelComplete(42, 100, 5f); break;
                case 1: LvlUpEvents.TrackLevelFailed(42, "fail", 5f); break;
                case 2: LvlUpEvents.TrackLevelFailed(42, "fail", 5f, 2); break;
                case 3: LvlUpEvents.TrackLevelFailed(42, "fail", 5f, 2, 0.5f); break;
                case 4: LvlUpEvents.TrackLevelComplete(42, 100, 5f, 0.25f); break;
            }
            LvlUpSDK.Economy.Source("coin", 1, "reward");
            Assert.IsFalse(_config.inLevel);
            Assert.AreEqual(false, _events[_events.Count - 1].properties["inLevel"]);
            Assert.AreEqual(42, _events[_events.Count - 1].properties["levelOrder"]);
        }

        [TestCase(0f)]
        [TestCase(0.1f)]
        [TestCase(1f)]
        public void WinMarginOverloadSendsMarginAndKeepsExtras(float margin)
        {
            var extras = new Dictionary<string, object> { { "ballsLeft", 3 }, { "winMargin", 0.9f } };
            LvlUpEvents.TrackLevelComplete(42, 100, 5f, margin, extras);
            var complete = _events.Find(e => e.eventName == "level_complete");
            Assert.AreEqual(margin, complete.properties["winMargin"]);
            Assert.AreEqual(3, complete.properties["ballsLeft"]);
            Assert.AreEqual(42, complete.properties["levelId"]);
            Assert.AreEqual(0.9f, extras["winMargin"], "caller's dictionary is not modified");
        }

        [TestCase(-0.1f)]
        [TestCase(3f)]
        [TestCase(float.NaN)]
        public void WinMarginOverloadDropsOutOfRangeMargin(float margin)
        {
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("winMargin must be in \\[0, 1\\]"));
            LvlUpEvents.TrackLevelComplete(42, 100, 5f, margin);
            var complete = _events.Find(e => e.eventName == "level_complete");
            Assert.IsNotNull(complete);
            Assert.IsFalse(complete.properties.ContainsKey("winMargin"));
        }
    }
}
