using System;

namespace LvlUp.Models
{
    /// <summary>
    /// Session data
    /// </summary>
    [Serializable]
    public class SessionData
    {
        public string sessionId;
        public string userId;
        public string startTime;
        public string endTime;
        public int duration;
    }
}