using System.Collections.Generic;
using LvlUp.RemoteConfig;
using NUnit.Framework;

namespace LvlUp.Tests
{
    public class RemoteConfigFetchGateTests
    {
        [Test]
        public void FirstRequestBeginsAFetch()
        {
            var gate = new RemoteConfigFetchGate();

            Assert.IsTrue(gate.TryBegin(null));
            Assert.IsTrue(gate.IsFetching);
        }

        [Test]
        public void EndingWithoutRequestsMeanwhileStopsFetching()
        {
            var gate = new RemoteConfigFetchGate();
            gate.TryBegin(null);

            Assert.IsFalse(gate.End(out var followUpOnComplete));
            Assert.IsNull(followUpOnComplete);
            Assert.IsFalse(gate.IsFetching);
        }

        [Test]
        public void RequestsDuringAFetchRunAsOneFollowUp()
        {
            var gate = new RemoteConfigFetchGate();
            var results = new List<string>();
            gate.TryBegin(null);

            Assert.IsFalse(gate.TryBegin(success => results.Add("a:" + success)));
            Assert.IsFalse(gate.TryBegin(success => results.Add("b:" + success)));

            Assert.IsTrue(gate.End(out var followUpOnComplete));
            Assert.IsTrue(gate.IsFetching);

            followUpOnComplete(true);
            CollectionAssert.AreEqual(new[] { "a:True", "b:True" }, results);

            Assert.IsFalse(gate.End(out _));
            Assert.IsFalse(gate.IsFetching);
        }

        [Test]
        public void RequestDuringTheFollowUpGetsItsOwnFollowUp()
        {
            var gate = new RemoteConfigFetchGate();
            gate.TryBegin(null);
            gate.TryBegin(null);
            gate.End(out _);

            Assert.IsFalse(gate.TryBegin(null));

            Assert.IsTrue(gate.End(out _));
            Assert.IsFalse(gate.End(out _));
        }
    }
}
