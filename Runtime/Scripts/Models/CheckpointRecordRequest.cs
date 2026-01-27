using System;
using System.Collections.Generic;

namespace LvlUp.Models
{
    /// <summary>
    /// Checkpoint record request - extends LvlUpEvent to track checkpoint completion as a full event
    /// </summary>
    [Serializable]
    public class CheckpointRecordRequest : LvlUpEvent
    {
        // Checkpoint-specific fields
        public string userId;
        public string checkpointId;
        public Dictionary<string, object> metadata;
        
        // Constructor with event name for LvlUpEvent base
        public CheckpointRecordRequest() : base("checkpoint_recorded", null)
        {
        }
        
        /// <summary>
        /// Override to include checkpoint-specific fields in properties
        /// </summary>
        public override EventDataItem ToEventDataItem()
        {
            var item = base.ToEventDataItem();
            
            // Add checkpoint-specific fields to properties
            item.properties = item.properties ?? new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(this.userId))
                item.properties["userId"] = this.userId;
            if (!string.IsNullOrEmpty(this.checkpointId))
                item.properties["checkpointId"] = this.checkpointId;
            if (this.metadata != null && this.metadata.Count > 0)
                item.properties["metadata"] = this.metadata;
            
            return item;
        }
        
        // Note: All device/platform/app/geo metadata auto-populated by LvlUpEvent base class
        // This allows full analytics on WHERE and WHEN users complete checkpoints
    }
}