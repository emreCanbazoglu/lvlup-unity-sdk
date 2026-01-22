using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using LvlUp.Models;
using LvlUp.Utils;

namespace LvlUp.RemoteConfig
{
    /// <summary>
    /// HTTP client for Remote Config API communication
    /// Internal service, use RemoteConfigService from LvlUp.Services for public API
    /// </summary>
    public class RemoteConfigHttpClient
    {
        private readonly string _baseUrl;
        private readonly string _gameId;
        private readonly float _timeout;
        private readonly bool _debugLogs;

        // Context for requests
        private string _platform;
        private string _version;
        private string _country;
        private string _segment;

        /// <summary>
        /// Creates a Remote Config HTTP client instance
        /// </summary>
        public RemoteConfigHttpClient(string baseUrl, string gameId, float timeout = 30f, bool debugLogs = false)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _gameId = gameId;
            _timeout = timeout;
            _debugLogs = debugLogs;
            
            // Auto-detect platform
            _platform = DetectPlatform();
            
            // Get version from Application
            _version = Application.version;
        }

        /// <summary>
        /// Set context for config evaluation on server
        /// </summary>
        public void SetContext(string platform = null, string version = null, string country = null, string segment = null)
        {
            if (!string.IsNullOrEmpty(platform))
                _platform = platform;
            if (!string.IsNullOrEmpty(version))
                _version = version;
            if (!string.IsNullOrEmpty(country))
                _country = country;
            if (!string.IsNullOrEmpty(segment))
                _segment = segment;
        }

        /// <summary>
        /// Fetch configs from server
        /// </summary>
        public IEnumerator FetchConfigs(string environment, Action<ConfigsResponse> onSuccess, Action<string> onError)
        {
            string url = BuildFetchUrl(environment);
            
            if (_debugLogs)
                Debug.Log($"[LvlUp] Fetching configs from: {url}");

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = (int)_timeout;
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        string jsonResponse = request.downloadHandler.text;
                        ConfigsResponse response = SimpleJson.FromJson<ConfigsResponse>(jsonResponse);
                        
                        if (response != null)
                        {
                            onSuccess?.Invoke(response);
                        }
                        else
                        {
                            onError?.Invoke("Failed to parse configs response");
                        }
                    }
                    catch (Exception e)
                    {
                        onError?.Invoke($"Parse error: {e.Message}");
                    }
                }
                else
                {
                    string errorMsg = string.IsNullOrEmpty(request.error) 
                        ? $"HTTP {request.responseCode}" 
                        : request.error;
                    onError?.Invoke(errorMsg);
                }
            }
        }

        private string BuildFetchUrl(string environment)
        {
            StringBuilder sb = new StringBuilder($"{_baseUrl}/api/config/configs/{_gameId}");
            
            List<string> queryParams = new List<string>();
            
            if (!string.IsNullOrEmpty(environment))
                queryParams.Add($"environment={UnityWebRequest.EscapeURL(environment)}");
            
            if (!string.IsNullOrEmpty(_platform))
                queryParams.Add($"platform={UnityWebRequest.EscapeURL(_platform)}");
            
            if (!string.IsNullOrEmpty(_version))
                queryParams.Add($"version={UnityWebRequest.EscapeURL(_version)}");
            
            if (!string.IsNullOrEmpty(_country))
                queryParams.Add($"country={UnityWebRequest.EscapeURL(_country)}");
            
            if (!string.IsNullOrEmpty(_segment))
                queryParams.Add($"segment={UnityWebRequest.EscapeURL(_segment)}");

            if (queryParams.Count > 0)
            {
                sb.Append("?");
                sb.Append(string.Join("&", queryParams));
            }

            return sb.ToString();
        }

        private string DetectPlatform()
        {
#if UNITY_IOS
            return "iOS";
#elif UNITY_ANDROID
            return "Android";
#elif UNITY_WEBGL
            return "WebGL";
#else
            return "Unknown";
#endif
        }
    }
}

