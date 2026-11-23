using System.Collections.Generic;
using System.IO;
using System.Threading;
using LvlUp.Services;
using NUnit.Framework;

namespace LvlUp.Tests
{
    public class AdImpressionJournalTests
    {
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), $"lvlup_journal_test_{System.Guid.NewGuid():N}.jsonl");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_path))
                File.Delete(_path);
        }

        [Test]
        public void JournaledImpressionSurvivesRestart()
        {
            new AdImpressionJournal(_path).Append(CreateImpression("imp_1"));

            var restored = new AdImpressionJournal(_path).TakeSnapshot();

            Assert.AreEqual(1, restored.Count);
            Assert.AreEqual("imp_1", restored[0].adImpressionId);
            Assert.AreEqual(0.05, restored[0].revenue);
        }

        [Test]
        public void AcknowledgedImpressionLeavesDisk()
        {
            var journal = new AdImpressionJournal(_path);
            journal.Append(CreateImpression("imp_1"));
            journal.Append(CreateImpression("imp_2"));

            var snapshot = journal.TakeSnapshot();
            journal.Acknowledge(new List<PendingAdImpression> { snapshot[0] });

            var restored = new AdImpressionJournal(_path).TakeSnapshot();
            Assert.AreEqual(1, restored.Count);
            Assert.AreEqual("imp_2", restored[0].adImpressionId);
        }

        [Test]
        public void TruncatedLineIsSkipped()
        {
            new AdImpressionJournal(_path).Append(CreateImpression("imp_1"));
            File.AppendAllText(_path, "{\"adImpressionId\":\"imp_2\",\"reven");

            var restored = new AdImpressionJournal(_path).TakeSnapshot();

            Assert.AreEqual(1, restored.Count);
            Assert.AreEqual("imp_1", restored[0].adImpressionId);
        }

        [Test]
        public void BackgroundImpressionIsJournaledAndQueuedOnMainThread()
        {
            var queued = new List<string>();
            long queuedTimestamp = 0;
            var service = new AdMonetizationService();
            service.Initialize((network, format, revenue, unit, placement, creative, impressionId, timestamp) =>
            {
                queued.Add(impressionId);
                queuedTimestamp = timestamp;
            }, _path);

            var reporter = new Thread(() => service.TrackMaxAdImpression("INTER", "unit", "AppLovin", null, "c", 0.05));
            reporter.Start();
            reporter.Join();

            Assert.AreEqual(0, queued.Count);
            Assert.AreEqual(1, new AdImpressionJournal(_path).TakeSnapshot().Count);

            service.DispatchPending();

            Assert.AreEqual(1, queued.Count);
            Assert.IsFalse(string.IsNullOrEmpty(queued[0]));
            Assert.Greater(queuedTimestamp, 0);
            Assert.AreEqual(0, service.PendingCount);
            Assert.IsFalse(File.Exists(_path));
        }

        [Test]
        public void MainThreadImpressionIsQueuedAtOnce()
        {
            int queuedCount = 0;
            var service = new AdMonetizationService();
            service.Initialize((network, format, revenue, unit, placement, creative, impressionId, timestamp) =>
                queuedCount++, _path);

            service.TrackMaxAdImpression("REWARDED", "unit", "AppLovin", null, "c", 0.05);

            Assert.AreEqual(1, queuedCount);
            Assert.AreEqual(0, service.PendingCount);
        }

        [Test]
        public void ImpressionsJournaledBeforeAKillReplayOnNextLaunch()
        {
            var killed = new AdMonetizationService();
            killed.Initialize((network, format, revenue, unit, placement, creative, impressionId, timestamp) => { },
                _path);
            var reporter = new Thread(() => killed.TrackMaxAdImpression("INTER", "unit", "AppLovin", null, "c", 0.05));
            reporter.Start();
            reporter.Join();

            var replayed = new List<string>();
            var relaunched = new AdMonetizationService();
            relaunched.Initialize((network, format, revenue, unit, placement, creative, impressionId, timestamp) =>
                replayed.Add(impressionId), _path);
            relaunched.DispatchPending();

            Assert.AreEqual(1, replayed.Count);
        }

        private static PendingAdImpression CreateImpression(string id)
        {
            return new PendingAdImpression
            {
                adImpressionId = id,
                timestamp = 1,
                adNetworkName = "AppLovin",
                adFormat = "INTER",
                revenue = 0.05
            };
        }
    }
}
