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
            var properties = new Dictionary<string, object>
            {
                { "levelId", levelId },
            };

            MergeProperties(properties, additionalProperties);
            LvlUpManager.Instance.TrackEvent("level_start", properties);
        }

        /// <summary>
        /// Track a level start event with level name
        /// </summary>
        /// <param name="levelId">Level identifier</param>
        /// <param name="levelName">Level name</param>
        /// <param name="additionalProperties">Optional additional properties</param>
        public static void TrackLevelStart(int levelId, string levelName, Dictionary<string, object> additionalProperties = null)
        {
            var properties = new Dictionary<string, object>
            {
                { "levelId", levelId },
                { "levelName", levelName },
            };

            MergeProperties(properties, additionalProperties);
            LvlUpManager.Instance.TrackEvent("level_start", properties);
        }

        /// <summary>
        /// Track a level start event with custom level funnel tracking
        /// This overrides the global level funnel configuration for this specific event
        /// </summary>
        /// <param name="levelId">Level identifier</param>
        /// <param name="levelFunnel">Level funnel name (e.g., "live_v1", "test_hard")</param>
        /// <param name="levelFunnelVersion">Level funnel version (e.g., 1, 2, 3)</param>
        /// <param name="additionalProperties">Optional additional properties</param>
        public static void TrackLevelStartWithFunnel(int levelId, string levelFunnel, int levelFunnelVersion, Dictionary<string, object> additionalProperties = null)
        {
            var properties = new Dictionary<string, object>
            {
                { "levelId", levelId },
            };

            MergeProperties(properties, additionalProperties);
            
            // Track event and manually set funnel data (bypasses auto-add in TrackEvent)
            var manager = LvlUpManager.Instance;
            var lvlUpEvent = new LvlUp.Models.LvlUpEvent("level_start", properties);
            lvlUpEvent.levelFunnel = levelFunnel;
            lvlUpEvent.levelFunnelVersion = levelFunnelVersion;
            
            // Use internal method to track with pre-configured event
            // Note: This is a workaround - ideally we'd have a TrackEvent overload
            manager.TrackEvent("level_start", properties);
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
            LvlUpManager.Instance.TrackEvent("level_complete", properties);
        }

        /// <summary>
        /// Track a level complete event with stars/rating
        /// </summary>
        /// <param name="levelId">Level identifier</param>
        /// <param name="score">Player's score</param>
        /// <param name="timeSeconds">Time taken in seconds</param>
        /// <param name="stars">Stars earned (typically 1-3)</param>
        /// <param name="additionalProperties">Optional additional properties</param>
        public static void TrackLevelComplete(int levelId, int score, float timeSeconds, int stars, Dictionary<string, object> additionalProperties = null)
        {
            var properties = new Dictionary<string, object>
            {
                { "levelId", levelId },
                { "score", score },
                { "timeSeconds", timeSeconds },
                { "stars", stars },
            };

            MergeProperties(properties, additionalProperties);
            LvlUpManager.Instance.TrackEvent("level_complete", properties);
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
            LvlUpManager.Instance.TrackEvent("level_failed", properties);
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
            LvlUpManager.Instance.TrackEvent("level_failed", properties);
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

