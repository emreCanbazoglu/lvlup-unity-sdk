using System;
using System.Collections.Generic;

namespace LvlUp.Models
{
    /// <summary>
    /// Player journey progress
    /// </summary>
    [Serializable]
    public class PlayerJourneyProgress
    {
        public string userId;
        public int completedCheckpoints;
        public int totalCheckpoints;
        public float completionRate;
        public CheckpointCompletion[] checkpoints;
        public string lastCheckpointDate;
    }
    
    [Serializable]
    public class CheckpointCompletion
    {
        public string checkpointId;
        public string checkpointName;
        public string completedAt;
        public bool completed;
        public Dictionary<string, object> metadata;
    }
}