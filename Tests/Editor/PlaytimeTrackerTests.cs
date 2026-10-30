using LvlUp.Services;
using NUnit.Framework;

namespace LvlUp.Tests
{
    public class PlaytimeTrackerTests
    {
        private double _now;
        private double _stored;
        private int _saves;

        [SetUp]
        public void SetUp()
        {
            _now = 100;
            _stored = 0;
            _saves = 0;
        }

        private PlaytimeTracker Create() =>
            new PlaytimeTracker(() => _now, () => _stored, seconds => { _stored = seconds; _saves++; });

        [Test]
        public void CountsOnlyForegroundTime()
        {
            var tracker = Create();
            tracker.Start();
            _now += 12;
            tracker.Pause();
            _now += 600; // backgrounded
            tracker.Start();
            _now += 8.9;

            Assert.AreEqual(20, tracker.TotalSeconds);
        }

        [Test]
        public void ContinuesFromPersistedTotalAfterRelaunch()
        {
            var first = Create();
            first.Start();
            _now += 45;
            first.Pause();

            _now = 0; // new process: realtimeSinceStartup restarts
            var second = Create();
            second.Start();
            _now += 5;

            Assert.AreEqual(50, second.TotalSeconds);
        }

        [Test]
        public void TickPersistsEveryThirtySecondsSoAKillLosesLittle()
        {
            var tracker = Create();
            tracker.Start();
            _now += 29;
            tracker.Tick();
            Assert.AreEqual(0, _saves);

            _now += 2;
            tracker.Tick();
            Assert.AreEqual(1, _saves);
            Assert.AreEqual(31, _stored, 0.001);
            Assert.AreEqual(31, tracker.TotalSeconds);
        }

        [Test]
        public void RepeatedStartAndPauseCallsAreIgnored()
        {
            var tracker = Create();
            tracker.Start();
            _now += 10;
            tracker.Start(); // must not reset the running segment
            _now += 10;
            tracker.Pause();
            tracker.Pause();

            Assert.AreEqual(20, tracker.TotalSeconds);
            Assert.AreEqual(1, _saves);
        }

        [Test]
        public void NegativePersistedValueIsTreatedAsZero()
        {
            _stored = -50;
            var tracker = Create();

            Assert.AreEqual(0, tracker.TotalSeconds);
        }
    }
}
