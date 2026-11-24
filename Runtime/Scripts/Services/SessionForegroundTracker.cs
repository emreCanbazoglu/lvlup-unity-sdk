using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace LvlUp.Services
{
    internal class SessionForegroundSnapshot
    {
        public string sessionId;
        public int foregroundSeconds;
        public string endTime;
    }

    /// <summary>
    /// On-screen seconds of the current session, and a small file that keeps them across a kill. The
    /// file is rewritten every few seconds and on pause, so the next launch can send the end of a session
    /// that never got one, as GameAnalytics does with its session table.
    /// </summary>
    internal class SessionForegroundTracker
    {
        private const double SNAPSHOT_INTERVAL_SECONDS = 8;

        private readonly Func<double> _foregroundClock;
        private readonly string _snapshotPath;

        private double _sessionStart;
        private double _lastSnapshotAt = double.NegativeInfinity;
        private bool _hasBegunSession;
        private int? _secondsAtPause;

        internal SessionForegroundTracker(Func<double> foregroundClock, string snapshotPath)
        {
            _foregroundClock = foregroundClock;
            _snapshotPath = snapshotPath;
        }

        /// <summary>
        /// The first session of the process counts from app launch, like GameAnalytics counting from SDK
        /// start; later ones count from their own start.
        /// </summary>
        internal void BeginSession()
        {
            _sessionStart = _hasBegunSession ? _foregroundClock() : 0;
            _hasBegunSession = true;
            _secondsAtPause = null;
        }

        internal int Seconds => (int)Math.Floor(Math.Max(0, _foregroundClock() - _sessionStart));

        /// <summary>
        /// On-screen seconds when the app last went to the background, for a session that ends there.
        /// Reading the clock on resume instead would add whatever the clock counted in between.
        /// </summary>
        internal int SecondsAtPause => _secondsAtPause ?? Seconds;

        internal void MarkPaused()
        {
            _secondsAtPause = Seconds;
        }

        internal void Tick(string sessionId)
        {
            if (_foregroundClock() - _lastSnapshotAt < SNAPSHOT_INTERVAL_SECONDS)
                return;

            SaveSnapshot(sessionId);
        }

        internal void SaveSnapshot(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
                return;

            _lastSnapshotAt = _foregroundClock();

            try
            {
                File.WriteAllText(_snapshotPath, JsonConvert.SerializeObject(new SessionForegroundSnapshot
                {
                    sessionId = sessionId,
                    foregroundSeconds = Seconds,
                    endTime = DateTime.UtcNow.ToString("o")
                }));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LvlUp] Failed to save session snapshot: {ex.Message}");
            }
        }

        /// <summary>Returns and removes the snapshot a previous run left behind, if any.</summary>
        internal SessionForegroundSnapshot TakeSnapshot()
        {
            try
            {
                if (!File.Exists(_snapshotPath))
                    return null;

                var snapshot = JsonConvert.DeserializeObject<SessionForegroundSnapshot>(File.ReadAllText(_snapshotPath));
                ClearSnapshot();
                return snapshot != null && !string.IsNullOrEmpty(snapshot.sessionId) ? snapshot : null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LvlUp] Failed to read session snapshot: {ex.Message}");
                ClearSnapshot();
                return null;
            }
        }

        internal void ClearSnapshot()
        {
            try
            {
                if (File.Exists(_snapshotPath))
                    File.Delete(_snapshotPath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LvlUp] Failed to delete session snapshot: {ex.Message}");
            }
        }
    }
}
