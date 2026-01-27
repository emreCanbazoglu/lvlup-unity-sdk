using System;

namespace LvlUp.Models
{
    /// <summary>
    /// Checkpoint data
    /// </summary>
    [Serializable]
    public class Checkpoint
    {
        public string id;
        public string name;
        public string description;
        public string type;
        public int order;
        public string[] tags;
        public string createdAt;
        public string gameId;
    }
}