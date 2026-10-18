using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using LvlUp.Services;
using NUnit.Framework;
using UnityEngine;

namespace LvlUp.Tests
{
    public class AttributionTrackingServiceTests
    {
        private sealed class Storage : IAttributionStorage
        {
            public string Value;
            public bool FailWrites;
            public string Read() => Value;
            public void Write(string value)
            {
                if (FailWrites) throw new InvalidOperationException("Storage unavailable");
                Value = value;
            }
        }

        private Storage _storage;
        private AttributionTrackingService _service;
        private readonly List<AttributionRequest> _requests = new List<AttributionRequest>();
        private readonly Queue<Action<bool>> _responses = new Queue<Action<bool>>();
        private readonly List<double> _reads = new List<double>();
        private Action<AttributionData> _attributionCallback;
        private string _user;
        private double _time;

        [SetUp]
        public void SetUp()
        {
            _storage = new Storage();
            _requests.Clear();
            _responses.Clear();
            _reads.Clear();
            _time = 0;
            _user = "revenue_user";
            _service = CreateService();
        }

        private AttributionTrackingService CreateService() => new AttributionTrackingService(
            new LvlUpConfig(), () => _user, () => "device", _storage,
            Send, routine => { while (routine.MoveNext()) { } }, () => _time,
            callback => { _reads.Add(_time); _attributionCallback = callback; });

        private IEnumerator Send(AttributionRequest request, Action<bool> complete)
        {
            _requests.Add(request);
            _responses.Enqueue(complete);
            yield break;
        }

        private void Observe(AttributionData value)
        {
            _attributionCallback(value);
            _service.Tick();
        }

        private void Complete(bool success)
        {
            _responses.Dequeue()(success);
            _service.Tick();
        }

        [Test]
        public void ManualSetNormalizesEveryFieldAndIncludesRevenueIdentityAndUtcTime()
        {
            _service.Set(" Organic ", " campaign ", null, " creative ", " tracker ");
            _service.Tick();
            var request = _requests[0];
            Assert.AreEqual("revenue_user", request.userId);
            Assert.AreEqual("device", request.deviceId);
            Assert.AreEqual("manual", request.attribution.provider);
            Assert.AreEqual("Organic", request.attribution.network);
            Assert.AreEqual("campaign", request.attribution.campaign);
            Assert.AreEqual("", request.attribution.adgroup);
            Assert.AreEqual("creative", request.attribution.creative);
            Assert.AreEqual("tracker", request.attribution.trackerName);
            Assert.AreEqual("", request.attribution.trackerToken);
            Assert.AreEqual("", request.attribution.clickLabel);
            Assert.IsTrue(request.observedAt.EndsWith("Z"));
            Assert.DoesNotThrow(() => DateTime.Parse(request.observedAt));
            Assert.IsNull(_storage.Value);
        }

        [Test]
        public void SuccessfulPayloadIsDeduplicatedAcrossServiceRecreationButChangesAreSent()
        {
            _service.Set("Network", "Campaign");
            _service.Tick();
            Complete(true);
            Assert.IsNotEmpty(_storage.Value);
            _service = CreateService();
            _time = 900;
            _service.Set(" Network ", "Campaign");
            _service.Tick();
            Assert.AreEqual(1, _requests.Count, "Timestamp must not cause a resend.");
            _service.Set("Network", "Other campaign");
            _service.Tick();
            Assert.AreEqual(2, _requests.Count);
        }

        [Test]
        public void FailedManualSendRemainsUnsentAndRetriesNextSession()
        {
            _service.Set("Network");
            _service.Tick();
            Complete(false);
            Assert.IsNull(_storage.Value);
            _service.Tick();
            Assert.AreEqual(1, _requests.Count);
            _service.OnSessionStarted();
            _service.Tick();
            Assert.AreEqual(2, _requests.Count);
        }

        [Test]
        public void SameAttributionIsSentForADifferentUser()
        {
            _service.Set("Network");
            _service.Tick();
            Complete(true);
            _user = "other_user";
            _service.OnSessionStarted();
            _service.Tick();
            Assert.AreEqual("other_user", _requests[1].userId);
        }

        [Test]
        public void InitializationWithoutAUserDefersSendUntilSessionStarts()
        {
            _user = null;
            _service.Initialize();
            _service.Tick();
            Observe(new AttributionData { provider = "adjust", network = "Organic" });
            Assert.IsEmpty(_requests);
            _user = "revenue_user";
            _service.OnSessionStarted();
            _service.Tick();
            Assert.AreEqual(1, _requests.Count);
        }

        [Test]
        public void EmptyResultsUseAllFiveRetryDelaysThenStopUntilNextSession()
        {
            _service.Initialize();
            _service.Tick();
            Observe(null);
            foreach (double delay in new double[] { 10, 30, 60, 120, 300 })
            {
                double due = _time + delay;
                _time = due - 0.01;
                _service.Tick();
                int previous = _reads.Count;
                _time = due;
                _service.Tick();
                Assert.AreEqual(previous + 1, _reads.Count);
                Observe(new AttributionData { network = " ", trackerToken = null });
            }
            CollectionAssert.AreEqual(new double[] { 0, 10, 40, 100, 220, 520 }, _reads);
            _time = 10000;
            _service.Tick();
            Assert.AreEqual(6, _reads.Count);
            _service.OnSessionStarted();
            _service.Tick();
            Assert.AreEqual(7, _reads.Count);
            Assert.IsEmpty(_requests);
        }

        [TestCase("Organic", null)]
        [TestCase(null, " token ")]
        public void NetworkOrTrackerTokenStopsRetriesAndNormalizesAdjustFields(string network, string token)
        {
            _service.Refresh();
            _service.Tick();
            Observe(new AttributionData { provider = "adjust", network = network, trackerToken = token,
                trackerName = " tracker ", clickLabel = " label " });
            Assert.AreEqual(network ?? "", _requests[0].attribution.network);
            Assert.AreEqual(token == null ? "" : "token", _requests[0].attribution.trackerToken);
            Assert.AreEqual("tracker", _requests[0].attribution.trackerName);
            Assert.AreEqual("label", _requests[0].attribution.clickLabel);
            _time = 10000;
            _service.Tick();
            Assert.AreEqual(1, _reads.Count);
        }

        [Test]
        public void WorkerCallbackWaitsForMainThreadTickAndOldCallbacksCannotOverwriteManualSet()
        {
            _service.Refresh();
            _service.Tick();
            var oldCallback = _attributionCallback;
            Task.Run(() => oldCallback(new AttributionData { network = "Organic", provider = "adjust" })).Wait();
            Assert.IsEmpty(_requests);
            _service.Tick();
            Assert.AreEqual(1, _requests.Count);
            Complete(true);
            _service.Set("Manual");
            _service.Tick();
            oldCallback(new AttributionData { network = "Stale", provider = "adjust" });
            _service.Tick();
            Complete(true);
            Assert.AreEqual(2, _requests.Count);
            Assert.AreEqual("Manual", _requests[1].attribution.network);
        }

        [Test]
        public void InFlightObservationsAreSerializedAndPersistOnlyAcknowledgedPayloads()
        {
            _service.Set("First");
            _service.Tick();
            _service.Set("Second");
            _service.Tick();
            Assert.AreEqual(1, _requests.Count);
            Complete(true);
            Assert.AreEqual(2, _requests.Count);
            Assert.IsTrue(_storage.Value.Contains("First"));
            Complete(false);
            Assert.IsTrue(_storage.Value.Contains("First"));
            _service.OnSessionStarted();
            _service.Tick();
            Assert.AreEqual("Second", _requests[2].attribution.network);
        }

        [Test]
        public void RefreshCancelsOldRetryAndIgnoresPreviousSessionCallback()
        {
            _service.Initialize();
            _service.Tick();
            var oldCallback = _attributionCallback;
            Observe(null);
            _time = 5;
            _service.Refresh();
            _service.Tick();
            Assert.AreEqual(2, _reads.Count);
            oldCallback(new AttributionData { network = "Stale", provider = "adjust" });
            _service.Tick();
            Assert.IsEmpty(_requests);
            Observe(new AttributionData { network = "Fresh", provider = "adjust" });
            _time = 10;
            _service.Tick();
            Assert.AreEqual(2, _reads.Count);
            Assert.AreEqual("Fresh", _requests[0].attribution.network);
        }

        [TestCase("provider")]
        [TestCase("network")]
        [TestCase("campaign")]
        [TestCase("adgroup")]
        [TestCase("creative")]
        [TestCase("trackerName")]
        [TestCase("trackerToken")]
        [TestCase("clickLabel")]
        public void AnyAttributionFieldChangeIsSent(string field)
        {
            _service.Refresh();
            _service.Tick();
            Observe(new AttributionData { network = "Network", provider = "adjust" });
            Complete(true);
            _service.Refresh();
            _service.Tick();
            var data = new AttributionData { network = "Network", provider = "adjust" };
            typeof(AttributionData).GetField(field).SetValue(data, "Changed");
            Observe(data);
            Assert.AreEqual(2, _requests.Count);
        }

        [Test]
        public void StorageWriteFailureDoesNotLoopOrEscapeAndRetriesNextSession()
        {
            _storage.FailWrites = true;
            _service.Set("Network");
            _service.Tick();
            Assert.DoesNotThrow(() => Complete(true));
            Assert.AreEqual(1, _requests.Count);
            Assert.IsNull(_storage.Value);
            _storage.FailWrites = false;
            _service.OnSessionStarted();
            _service.Tick();
            Assert.AreEqual(2, _requests.Count);
            Complete(true);
            Assert.IsNotEmpty(_storage.Value);
        }

        private IEnumerator ThrowingSend(AttributionRequest request, Action<bool> complete)
        {
            _requests.Add(request);
            yield return null;
            throw new InvalidOperationException("Transport failed after yielding");
        }

        [Test]
        public void CoroutineTransportFailureDoesNotEscapeOrPersistAndRetriesNextSession()
        {
            _service = new AttributionTrackingService(new LvlUpConfig(), () => _user, () => "device", _storage,
                ThrowingSend, routine => { while (routine.MoveNext()) { } }, () => _time, null);
            _service.Set("Network");
            Assert.DoesNotThrow(() => _service.Tick());
            Assert.AreEqual(1, _requests.Count);
            Assert.IsNull(_storage.Value);
            _service.OnSessionStarted();
            _service.Tick();
            Assert.AreEqual(2, _requests.Count);
        }

        [Test]
        public void DisposedServiceIgnoresLateCallbacksAndNewInput()
        {
            _service.Refresh();
            _service.Tick();
            _service.Dispose();
            _attributionCallback(new AttributionData { network = "Organic", provider = "adjust" });
            _service.Set("Network");
            _service.Tick();
            Assert.IsEmpty(_requests);
        }

        [Test]
        public void FacadeSetUsesManualServicePath()
        {
            var field = typeof(LvlUpManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
            var previous = field.GetValue(null);
            var go = new GameObject("Attribution facade test");
            go.SetActive(false);
            var manager = go.AddComponent<LvlUpManager>();
            try
            {
                field.SetValue(null, manager);
                typeof(LvlUpManager).GetProperty("AttributionTracking", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(manager, _service);
                LvlUpSDK.Attribution.Set(" Manual network ");
                _service.Tick();
                Assert.AreEqual("manual", _requests[0].attribution.provider);
                Assert.AreEqual("Manual network", _requests[0].attribution.network);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                field.SetValue(null, previous);
            }
        }
    }
}
