using System;
using System.Collections.Generic;

namespace LvlUp.Models
{
    /// <summary>
    /// Session start request - extends LvlUpEvent to reuse auto-population logic
    /// </summary>
    [Serializable]
    public class SessionStartRequest : LvlUpEvent
    {
        // Session-specific fields
        public string userId;
        public string startTime;  // ISO date format
        
        // Constructor with event name for LvlUpEvent base
        public SessionStartRequest() : base("session_start", null)
        {
            this.startTime = DateTime.UtcNow.ToString("o");
        }
        
        /// <summary>
        /// Override to include session-specific fields in properties
        /// </summary>
        public override EventDataItem ToEventDataItem()
        {
            var item = base.ToEventDataItem();
            
            // Add session-specific fields to properties
            item.properties = item.properties ?? new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(this.userId))
                item.properties["userId"] = this.userId;
            if (!string.IsNullOrEmpty(this.startTime))
                item.properties["startTime"] = this.startTime;
            
            return item;
        }
        
        // Note: All device/platform/app/geo metadata auto-populated by LvlUpEvent base class
    }
}