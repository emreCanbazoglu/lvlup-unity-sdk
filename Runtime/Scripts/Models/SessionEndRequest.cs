using System;
using System.Collections.Generic;

namespace LvlUp.Models
{
    /// <summary>
    /// Session end request - extends LvlUpEvent to reuse auto-population logic
    /// </summary>
    [Serializable]
    public class SessionEndRequest : LvlUpEvent
    {
        // Session-specific fields
        public string sessionId;  // Used internally, not sent in body (goes in URL)
        public string endTime;    // Optional: ISO date format, defaults to current time on backend
        
        // Constructor with event name for LvlUpEvent base
        public SessionEndRequest() : base("session_end", null)
        {
            this.endTime = DateTime.UtcNow.ToString("o");
        }
        
        /// <summary>
        /// Override to include session-specific fields in properties
        /// </summary>
        public override EventDataItem ToEventDataItem()
        {
            var item = base.ToEventDataItem();
            
            // Add session-specific fields to properties
            item.properties = item.properties ?? new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(this.sessionId))
                item.properties["sessionId"] = this.sessionId;
            if (!string.IsNullOrEmpty(this.endTime))
                item.properties["endTime"] = this.endTime;
            
            return item;
        }
        
        // Note: All device/platform/app/geo metadata auto-populated by LvlUpEvent base class
        // Captures current state at session end (battery, connection, location changes, etc.)
    }
}