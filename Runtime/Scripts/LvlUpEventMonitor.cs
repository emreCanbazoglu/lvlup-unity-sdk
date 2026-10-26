using System;
using Newtonsoft.Json;
using UnityEngine;

namespace LvlUp
{
    public enum LvlUpTrackedKind
    {
        Event,
        Revenue,
        Session
    }

    /// <summary>
    /// A record of something the SDK accepted for sending, captured when it was tracked (not when
    /// its batch is flushed).
    /// </summary>
    public class LvlUpTrackedEvent
    {
        public LvlUpTrackedKind Kind { get; }
        public string Name { get; }
        public string PayloadJson { get; }
        public DateTime TrackedAtUtc { get; }

        public LvlUpTrackedEvent(LvlUpTrackedKind kind, string name, string payloadJson, DateTime trackedAtUtc)
        {
            Kind = kind;
            Name = name;
            PayloadJson = payloadJson;
            TrackedAtUtc = trackedAtUtc;
        }
    }

    /// <summary>
    /// Debug hook for in-app event viewers. Raised on the main thread for every event, revenue item
    /// and session start/end the SDK tracks, including the ones it tracks itself (app_paused,
    /// economy flushes, ad impressions). Payloads are serialized only while someone is subscribed.
    /// </summary>
    public static class LvlUpEventMonitor
    {
        private static readonly JsonSerializerSettings _serializerSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };

        public static event Action<LvlUpTrackedEvent> OnTracked;

        internal static void Report(LvlUpTrackedKind kind, string name, object payload)
        {
            var onTracked = OnTracked;
            if (onTracked == null) return;

            string payloadJson;
            try
            {
                payloadJson = JsonConvert.SerializeObject(payload, _serializerSettings);
            }
            catch (Exception e)
            {
                payloadJson = $"<unserializable: {e.Message}>";
            }

            try
            {
                onTracked(new LvlUpTrackedEvent(kind, name, payloadJson, DateTime.UtcNow));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
