using System;

namespace LvlUp.Models
{
    /// <summary>
    /// Checkpoint creation request
    /// </summary>
    [Serializable]
    public class CheckpointRequest
    {
        public string name;
        public string description;
        public string type;
        public int order;
        public string[] tags;
    }
}