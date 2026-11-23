using System.IO;
using LvlUp.Models;
using LvlUp.Services;
using NUnit.Framework;
using UnityEngine;

namespace LvlUp.Tests
{
    public class SessionForegroundTests
    {
        private double _now;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _now = 0;
            _path = Path.Combine(Path.GetTempPath(), $"lvlup_session_snapshot_test_{System.Guid.NewGuid():N}.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_path))
                File.Delete(_path);
        }

        [Test]
        public void UnityPauseClockLeavesOutPausedTime()
        {
            var clock = new UnityPauseForegroundClock(() => _now);
            _now = 10;
            clock.OnUnityPause(true);
            _now = 100;
            clock.OnUnityPause(false);
            _now = 105;

            Assert.AreEqual(15, clock.Seconds, 0.0001);
        }

        [Test]
        public void RepeatedPauseEventsAreIgnored()
        {
            var clock = new UnityPauseForegroundClock(() => _now);
            _now = 10;
            clock.OnUnityPause(true);
            _now = 20;
            clock.OnUnityPause(true);
            clock.OnUnityPause(false);
            clock.OnUnityPause(false);
            _now = 25;

            Assert.AreEqual(15, clock.Seconds, 0.0001);
        }

        [Test]
        public void FirstSessionCountsFromLaunch()
        {
            var tracker = new SessionForegroundTracker(() => _now, _path);
            _now = 12;
            tracker.BeginSession();
            _now = 40;

            Assert.AreEqual(40, tracker.Seconds);
        }

        [Test]
        public void LaterSessionCountsFromItsStart()
        {
            var tracker = new SessionForegroundTracker(() => _now, _path);
            tracker.BeginSession();
            _now = 300;
            tracker.BeginSession();
            _now = 330.9;

            Assert.AreEqual(30, tracker.Seconds);
        }

        [Test]
        public void SnapshotSurvivesARelaunchOnce()
        {
            var tracker = new SessionForegroundTracker(() => _now, _path);
            tracker.BeginSession();
            _now = 75;
            tracker.SaveSnapshot("session_1");

            var relaunched = new SessionForegroundTracker(() => 0, _path);
            var snapshot = relaunched.TakeSnapshot();

            Assert.AreEqual("session_1", snapshot.sessionId);
            Assert.AreEqual(75, snapshot.foregroundSeconds);
            Assert.IsFalse(string.IsNullOrEmpty(snapshot.endTime));
            Assert.IsNull(relaunched.TakeSnapshot());
        }

        [Test]
        public void TickSavesAtMostEveryEightSeconds()
        {
            var tracker = new SessionForegroundTracker(() => _now, _path);
            tracker.BeginSession();
            _now = 1;
            tracker.Tick("session_1");
            File.Delete(_path);

            _now = 5;
            tracker.Tick("session_1");
            Assert.IsFalse(File.Exists(_path));

            _now = 9;
            tracker.Tick("session_1");
            Assert.IsTrue(File.Exists(_path));
        }

        [Test]
        public void CorruptSnapshotIsDropped()
        {
            File.WriteAllText(_path, "{\"sessionId\":\"s");

            var tracker = new SessionForegroundTracker(() => 0, _path);

            Assert.IsNull(tracker.TakeSnapshot());
            Assert.IsFalse(File.Exists(_path));
        }

        [Test]
        public void PendingEndKeepsForegroundSecondsThroughJsonUtility()
        {
            var restored = JsonUtility.FromJson<SessionEndRequest>(
                JsonUtility.ToJson(new SessionEndRequest { sessionId = "s1", foregroundSeconds = 42 }));

            Assert.AreEqual(42, restored.foregroundSeconds);
            Assert.AreEqual(SessionEndRequest.FOREGROUND_NOT_MEASURED, new SessionEndRequest().foregroundSeconds);
        }
    }
}
