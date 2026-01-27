using System;

namespace LvlUp.Models
{
    /// <summary>
    /// Base API response wrapper
    /// </summary>
    [Serializable]
    public class ApiResponse<T>
    {
        public bool success;
        public T data;
        public string error;
        public string message;
    }
    
    /// <summary>
    /// Generic API response for simple operations
    /// </summary>
    [Serializable]
    public class ApiResponse
    {
        public bool success;
        public string message;
        public string error;
    }
}