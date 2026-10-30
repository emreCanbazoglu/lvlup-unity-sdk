using System;
using UnityEngine;

namespace LvlUp.Services
{
    /// <summary>
    /// Lifetime foreground playtime of this install, in seconds. Persisted across
    /// launches and attached to level events so the dashboard can show how much
    /// real time (menus, retries and ads included) players need to reach a level.
    /// </summary>
    internal class PlaytimeTracker
    {
        internal const string PREF_TOTAL_PLAYTIME = "LvlUp_TotalPlaytimeSeconds";

        // Bounds what is lost when the process is killed without a pause callback.
        private const double PERSIST_INTERVAL_SECONDS = 30;

        private readonly Func<double> _clock;
        private readonly Func<double> _load;
        private readonly Action<double> _save;

        private double _persistedSeconds;
        private double _segmentStart;
        private double _lastPersistAt;
        private bool _running;

        public PlaytimeTracker()
            : this(
                () => Time.realtimeSinceStartup,
                () => PlayerPrefs.GetFloat(PREF_TOTAL_PLAYTIME, 0f),
                seconds =>
                {
                    PlayerPrefs.SetFloat(PREF_TOTAL_PLAYTIME, (float)seconds);
                    PlayerPrefs.Save();
                })
        {
        }

        internal PlaytimeTracker(Func<double> clock, Func<double> load, Action<double> save)
        {
            _clock = clock;
            _load = load;
            _save = save;
            _persistedSeconds = Math.Max(0, _load());
        }

        public void Start()
        {
            if (_running)
                return;

            _running = true;
            _segmentStart = _clock();
            _lastPersistAt = _segmentStart;
        }

        public void Pause()
        {
            if (!_running)
                return;

            _persistedSeconds = CurrentTotal();
            _running = false;
            _save(_persistedSeconds);
        }

        /// <summary>Call every frame; persists at most every 30 seconds.</summary>
        public void Tick()
        {
            if (!_running || _clock() - _lastPersistAt < PERSIST_INTERVAL_SECONDS)
                return;

            _persistedSeconds = CurrentTotal();
            _segmentStart = _clock();
            _lastPersistAt = _segmentStart;
            _save(_persistedSeconds);
        }

        public int TotalSeconds => (int)Math.Floor(CurrentTotal());

        private double CurrentTotal()
        {
            if (!_running)
                return _persistedSeconds;

            return _persistedSeconds + Math.Max(0, _clock() - _segmentStart);
        }
    }
}
