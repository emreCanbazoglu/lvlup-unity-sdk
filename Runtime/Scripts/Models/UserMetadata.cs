using System;

namespace LvlUp.Models
{
    /// <summary>
    /// User metadata for session tracking
    /// </summary>
    [Serializable]
    public class UserMetadata
    {
        public string deviceId;
        public string platform;
        public string version;
        public string country;
        public string language;
    }
}