using System;
using System.Collections.Generic;

namespace LvlUp.Models
{
    /// <summary>
    /// Set geographic location for an event
    /// </summary>
    [Serializable]
    public class BatchEventRequest
    {
        public string userId;
        public string sessionId;
        public List<EventDataItem> events;
    }
    
    /// <summary>
    /// Event data item for batch requests - extends EventMetadata to avoid duplication
    /// </summary>
    [Serializable]
    public class EventDataItem : EventMetadata
    {
        public string eventName;
        public Dictionary<string, object> properties;
        public Dictionary<string, string> abTests;
        public string timestamp;
    }
}
