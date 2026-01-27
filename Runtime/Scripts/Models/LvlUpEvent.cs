using System;
using System.Collections.Generic;

namespace LvlUp.Models
{
    /// <summary>
    /// Event data for tracking - includes all metadata from backend Event model
    /// Base class for all trackable events
    /// </summary>
    [Serializable]
    public class LvlUpEvent : EventMetadata
    {
        public string eventName;
        public Dictionary<string, object> properties;
        public string timestamp;

        public LvlUpEvent(string eventName, Dictionary<string, object> properties = null)
        {
            this.eventName = eventName;
            this.properties = properties ?? new Dictionary<string, object>();
            this.timestamp = DateTime.UtcNow.ToString("o");
            
            // Generate unique event ID
            this.eventUuid = Guid.NewGuid().ToString();
            this.clientTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            
            // Auto-populate device and platform info
            PopulateDeviceInfo();
        }

        /// <summary>
        /// Convert this LvlUpEvent to an EventDataItem for batch sending
        /// Virtual so subclasses can override to include their custom fields
        /// </summary>
        public virtual EventDataItem ToEventDataItem()
        {
            var item = new EventDataItem
            {
                eventName = this.eventName,
                properties = this.properties,
                timestamp = this.timestamp
            };
            
            // Copy all metadata
            this.CopyTo(item);
            
            return item;
        }
    }
}