using System;
using System.Collections.Generic;

namespace LvlUp.Models
{
    /// <summary>
    /// Level-specific event - extends LvlUpEvent with level funnel tracking
    /// Use this for level_start, level_complete, level_failed events
    /// </summary>
    [Serializable]
    public class LevelEvent : LvlUpEvent
    {
        // Level Funnel tracking
        public string levelFunnel;         // Level funnel name, e.g., "live_v1", "live_v2"
        public int? levelFunnelVersion;    // Level funnel version number, e.g., 1, 2, 3

        public LevelEvent(string eventName, Dictionary<string, object> properties = null) 
            : base(eventName, properties)
        {
        }

        /// <summary>
        /// Convert this LevelEvent to an EventDataItem for batch sending
        /// Overrides base to include level funnel data
        /// </summary>
        public override EventDataItem ToEventDataItem()
        {
            var item = base.ToEventDataItem();
            
            // Add level funnel data to properties if present
            if (!string.IsNullOrEmpty(this.levelFunnel))
            {
                item.properties = item.properties ?? new Dictionary<string, object>();
                item.properties["levelFunnel"] = this.levelFunnel;
                if (this.levelFunnelVersion.HasValue)
                {
                    item.properties["levelFunnelVersion"] = this.levelFunnelVersion.Value;
                }
            }
            
            return item;
        }
    }
}