using System;
using UnityEngine;

namespace LvlUp.Services
{
    internal interface IForegroundClock
    {
        /// <summary>Seconds the app has been in the foreground since the clock started.</summary>
        double Seconds { get; }

        void OnUnityPause(bool paused);
    }

    /// <summary>
    /// Process-wide foreground time, started before the first scene loads so a session counts from app
    /// launch. On an Android device it is the native activity-lifecycle clock (ads and purchase dialogs keep
    /// counting); elsewhere, or if that class is unavailable (e.g. stripped by R8), it follows Unity's
    /// OnApplicationPause, which on Android also pauses during full-screen ads.
    /// </summary>
    internal static class ForegroundClock
    {
        private static IForegroundClock _current;

        internal static IForegroundClock Current => _current ?? (_current = Create());

        internal static double Seconds => Current.Seconds;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void StartAtLaunch()
        {
            _ = Current;
        }

        private static IForegroundClock Create()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var native = AndroidForegroundClock.TryCreate();
            if (native != null)
                return native;
#endif
            return new UnityPauseForegroundClock(() => Time.realtimeSinceStartup);
        }
    }

    internal class UnityPauseForegroundClock : IForegroundClock
    {
        private readonly Func<double> _realtime;
        private double _accumulated;
        private double _segmentStart;
        private bool _paused;

        internal UnityPauseForegroundClock(Func<double> realtime)
        {
            _realtime = realtime;
            _segmentStart = _realtime();
        }

        public double Seconds => _paused ? _accumulated : _accumulated + Math.Max(0, _realtime() - _segmentStart);

        public void OnUnityPause(bool paused)
        {
            if (paused == _paused)
                return;

            if (paused)
                _accumulated = Seconds;
            else
                _segmentStart = _realtime();

            _paused = paused;
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    internal class AndroidForegroundClock : IForegroundClock
    {
        private const string CLOCK_CLASS = "com.lvlup.analytics.LvlUpForegroundClock";

        private readonly AndroidJavaClass _clock;

        private AndroidForegroundClock(AndroidJavaClass clock)
        {
            _clock = clock;
        }

        internal static AndroidForegroundClock TryCreate()
        {
            try
            {
                var clock = new AndroidJavaClass(CLOCK_CLASS);
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    clock.CallStatic("start", activity);
                }
                return new AndroidForegroundClock(clock);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LvlUp] Native foreground clock unavailable, using Unity pause events: {ex.Message}");
                return null;
            }
        }

        public double Seconds => _clock.CallStatic<long>("foregroundMillis") / 1000.0;

        public void OnUnityPause(bool paused)
        {
        }
    }
#endif
}
