using System;
using System.Collections.Generic;

namespace LvlUp
{
    /// <summary>
    /// Static helper class for tracking standard game events with consistent structure
    /// </summary>
    public static class LvlUpEvents
    {
        #region Level Events

        /// <summary>
        /// Track a level start event with standard properties
        /// </summary>
        /// <param name="levelId">Level identifier</param>
        /// <param name="additionalProperties">Optional additional properties</param>
        public static void TrackLevelStart(int levelId, Dictionary<string, object> additionalProperties = null)
        {
            // Auto-capture current level order for IAP revenue attribution (ARPU-by-level).
            // Games with non-sequential level IDs can override via LvlUpSDK.Level.SetCurrent(order).
            LvlUpManager.Instance?.SetCurrentLevel(levelId);

            var properties = new Dictionary<string, object>
            {
                { "levelId", levelId },
            };

            MergeProperties(properties, additionalProperties);

            // Create LevelEvent and apply global level funnel config
            var levelEvent = new LvlUp.Models.LevelEvent("level_start", properties);
            var (funnel, version) = LvlUpManager.Instance.GetLevelFunnel();
            if (!string.IsNullOrEmpty(funnel))
            {
                levelEvent.levelFunnel = funnel;
                levelEvent.levelFunnelVersion = version;
            }

            LvlUpManager.Instance.TrackEvent(levelEvent);
        }

        /// <summary>
        /// Track a level start event with level name
        /// </summary>
        /// <param name="levelId">Level identifier</param>
        /// <param name="levelName">Level name</param>
        /// <param name="additionalProperties">Optional additional properties</param>
        public static void TrackLevelStart(int levelId, string levelName, Dictionary<string, object> additionalProperties = null)
        {
            // Add levelName to additional properties and call base method
            var props = additionalProperties ?? new Dictionary<string, object>();
            props["levelName"] = levelName;
            
            TrackLevelStart(levelId, props);
        }

        /// <summary>
        /// Track a level complete event with standard properties
        /// </summary>
        /// <param name="levelId">Level identifier</param>
        /// <param name="score">Player's score</param>
        /// <param name="timeSeconds">Time taken in seconds</param>
        /// <param name="additionalProperties">Optional additional properties</param>
        public static void TrackLevelComplete(int levelId, int score, float timeSeconds, Dictionary<string, object> additionalProperties = null)
        {
            var properties = new Dictionary<string, object>
            {
                { "levelId", levelId },
                { "score", score },
                { "timeSeconds", timeSeconds },
            };

            MergeProperties(properties, additionalProperties);
            
            // Create LevelEvent and apply global level funnel config
            var levelEvent = new LvlUp.Models.LevelEvent("level_complete", properties);
            var (funnel, version) = LvlUpManager.Instance.GetLevelFunnel();
            if (!string.IsNullOrEmpty(funnel))
            {
                levelEvent.levelFunnel = funnel;
                levelEvent.levelFunnelVersion = version;
            }
            
            LvlUpManager.Instance.TrackEvent(levelEvent);
        }

        /// <summary>
        /// Track a level failed event with standard properties
        /// </summary>
        /// <param name="levelId">Level identifier</param>
        /// <param name="reason">Reason for failure</param>
        /// <param name="timeSeconds">Time spent before failing</param>
        /// <param name="additionalProperties">Optional additional properties</param>
        public static void TrackLevelFailed(int levelId, string reason, float timeSeconds, Dictionary<string, object> additionalProperties = null)
        {
            var properties = new Dictionary<string, object>
            {
                { "levelId", levelId },
                { "reason", reason },
                { "timeSeconds", timeSeconds },
            };

            MergeProperties(properties, additionalProperties);
            
            // Create LevelEvent and apply global level funnel config
            var levelEvent = new LvlUp.Models.LevelEvent("level_failed", properties);
            var (funnel, version) = LvlUpManager.Instance.GetLevelFunnel();
            if (!string.IsNullOrEmpty(funnel))
            {
                levelEvent.levelFunnel = funnel;
                levelEvent.levelFunnelVersion = version;
            }
            
            LvlUpManager.Instance.TrackEvent(levelEvent);
        }

        /// <summary>
        /// Track a level failed event with attempts count
        /// </summary>
        /// <param name="levelId">Level identifier</param>
        /// <param name="reason">Reason for failure</param>
        /// <param name="timeSeconds">Time spent before failing</param>
        /// <param name="attempts">Number of attempts so far</param>
        /// <param name="additionalProperties">Optional additional properties</param>
        public static void TrackLevelFailed(int levelId, string reason, float timeSeconds, int attempts, Dictionary<string, object> additionalProperties = null)
        {
            var properties = new Dictionary<string, object>
            {
                { "levelId", levelId },
                { "reason", reason },
                { "timeSeconds", timeSeconds },
                { "attempts", attempts },
            };

            MergeProperties(properties, additionalProperties);

            // Create LevelEvent and apply global level funnel config
            var levelEvent = new LvlUp.Models.LevelEvent("level_failed", properties);
            var (funnel, version) = LvlUpManager.Instance.GetLevelFunnel();
            if (!string.IsNullOrEmpty(funnel))
            {
                levelEvent.levelFunnel = funnel;
                levelEvent.levelFunnelVersion = version;
            }

            LvlUpManager.Instance.TrackEvent(levelEvent);
        }

        /// <summary>
        /// Track a level failed event with attempts count and normalized objective progress.
        /// `objectiveProgress` is sent as a custom property on the event — the analytics
        /// dashboard reads it from the event properties bag when computing failure-by-
        /// progress breakdowns.
        ///
        /// Normalized to [0, 1] so the metric is comparable across levels with different
        /// objective counts: 0.0 = nothing completed, 1.0 = all objectives complete.
        /// (At `level_failed` it will typically be &lt; 1.0.) The SDK does not clamp —
        /// callers are expected to pass a value in range.
        /// </summary>
        /// <param name="levelId">Level identifier</param>
        /// <param name="reason">Reason for failure</param>
        /// <param name="timeSeconds">Time spent before failing</param>
        /// <param name="attempts">Number of attempts so far</param>
        /// <param name="objectiveProgress">Normalized objective progress in [0, 1] (e.g., 0.6 = 60% of objectives complete when the level failed)</param>
        /// <param name="additionalProperties">Optional additional properties</param>
        public static void TrackLevelFailed(int levelId, string reason, float timeSeconds, int attempts, float objectiveProgress, Dictionary<string, object> additionalProperties = null)
        {
            var properties = new Dictionary<string, object>
            {
                { "levelId", levelId },
                { "reason", reason },
                { "timeSeconds", timeSeconds },
                { "attempts", attempts },
                { "objectiveProgress", objectiveProgress },
            };

            MergeProperties(properties, additionalProperties);

            // Create LevelEvent and apply global level funnel config
            var levelEvent = new LvlUp.Models.LevelEvent("level_failed", properties);
            var (funnel, version) = LvlUpManager.Instance.GetLevelFunnel();
            if (!string.IsNullOrEmpty(funnel))
            {
                levelEvent.levelFunnel = funnel;
                levelEvent.levelFunnelVersion = version;
            }

            LvlUpManager.Instance.TrackEvent(levelEvent);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Merge additional properties into the base properties
        /// </summary>
        private static void MergeProperties(Dictionary<string, object> baseProperties, Dictionary<string, object> additionalProperties)
        {
            if (additionalProperties != null)
            {
                foreach (var prop in additionalProperties)
                {
                    baseProperties[prop.Key] = prop.Value;
                }
            }
        }

        #endregion
    }
}

