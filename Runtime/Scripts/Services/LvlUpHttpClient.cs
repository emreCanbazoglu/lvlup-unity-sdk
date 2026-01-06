using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using LvlUp.Models;
using LvlUp.Utils;

namespace LvlUp.Services
{
    /// <summary>
    /// HTTP client for LvlUp API communication
    /// </summary>
    public class LvlUpHttpClient
    {
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly float _timeout;
        private readonly bool _debugLogs;

        public LvlUpHttpClient(string baseUrl, string apiKey, float timeout = 30f, bool debugLogs = false)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _apiKey = apiKey;
            _timeout = timeout;
            _debugLogs = debugLogs;
        }

        /// <summary>
        /// Send a GET request
        /// </summary>
        public IEnumerator Get<T>(string endpoint, Action<ApiResponse<T>> callback)
        {
            string url = $"{_baseUrl}/{endpoint.TrimStart('/')}";
            
            if (_debugLogs)
                Debug.Log($"[LvlUp] GET {url}");

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = (int)_timeout;
                request.SetRequestHeader("X-API-Key", _apiKey);
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                var response = HandleResponse<T>(request);
                callback?.Invoke(response);
            }
        }

        /// <summary>
        /// Send a POST request
        /// </summary>
        public IEnumerator Post<T>(string endpoint, object body, Action<ApiResponse<T>> callback)
        {
            string url = $"{_baseUrl}/{endpoint.TrimStart('/')}";
            string jsonBody = SimpleJson.ToJson(body);

            if (_debugLogs)
                Debug.Log($"[LvlUp] POST {url}\nBody: {jsonBody}");

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = (int)_timeout;
                request.SetRequestHeader("X-API-Key", _apiKey);
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                var response = HandleResponse<T>(request);
                callback?.Invoke(response);
            }
        }

        /// <summary>
        /// Send a PUT request
        /// </summary>
        public IEnumerator Put<T>(string endpoint, object body, Action<ApiResponse<T>> callback)
        {
            string url = $"{_baseUrl}/{endpoint.TrimStart('/')}";
            string jsonBody = SimpleJson.ToJson(body);

            if (_debugLogs)
                Debug.Log($"[LvlUp] PUT {url}\nBody: {jsonBody}");

            using (UnityWebRequest request = new UnityWebRequest(url, "PUT"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = (int)_timeout;
                request.SetRequestHeader("X-API-Key", _apiKey);
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                var response = HandleResponse<T>(request);
                callback?.Invoke(response);
            }
        }

        /// <summary>
        /// Send a DELETE request
        /// </summary>
        public IEnumerator Delete(string endpoint, Action<ApiResponse> callback)
        {
            string url = $"{_baseUrl}/{endpoint.TrimStart('/')}";

            if (_debugLogs)
                Debug.Log($"[LvlUp] DELETE {url}");

            using (UnityWebRequest request = UnityWebRequest.Delete(url))
            {
                request.timeout = (int)_timeout;
                request.SetRequestHeader("X-API-Key", _apiKey);
                request.SetRequestHeader("Content-Type", "application/json");
                request.downloadHandler = new DownloadHandlerBuffer();

                yield return request.SendWebRequest();

                var response = HandleResponse(request);
                callback?.Invoke(response);
            }
        }

        /// <summary>
        /// Handle the response from UnityWebRequest
        /// </summary>
        private ApiResponse<T> HandleResponse<T>(UnityWebRequest request)
        {
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string responseText = request.downloadHandler.text;
                    
                    if (_debugLogs)
                        Debug.Log($"[LvlUp] Response: {responseText}");

                    var response = JsonUtility.FromJson<ApiResponse<T>>(responseText);
                    
                    if (response == null)
                    {
                        response = new ApiResponse<T>
                        {
                            success = true,
                            data = JsonUtility.FromJson<T>(responseText)
                        };
                    }

                    return response;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[LvlUp] Failed to parse response: {ex.Message}");
                    return new ApiResponse<T>
                    {
                        success = false,
                        error = $"Failed to parse response: {ex.Message}"
                    };
                }
            }
            else
            {
                string errorMsg = $"Request failed: {request.error}";
                if (!string.IsNullOrEmpty(request.downloadHandler?.text))
                {
                    errorMsg += $"\nResponse: {request.downloadHandler.text}";
                }

                Debug.LogError($"[LvlUp] {errorMsg}");
                
                return new ApiResponse<T>
                {
                    success = false,
                    error = errorMsg
                };
            }
        }

        /// <summary>
        /// Handle simple response (non-generic)
        /// </summary>
        private ApiResponse HandleResponse(UnityWebRequest request)
        {
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string responseText = request.downloadHandler.text;
                    
                    if (_debugLogs)
                        Debug.Log($"[LvlUp] Response: {responseText}");

                    var response = JsonUtility.FromJson<ApiResponse>(responseText);
                    return response ?? new ApiResponse { success = true };
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[LvlUp] Failed to parse response: {ex.Message}");
                    return new ApiResponse
                    {
                        success = false,
                        error = $"Failed to parse response: {ex.Message}"
                    };
                }
            }
            else
            {
                string errorMsg = $"Request failed: {request.error}";
                if (!string.IsNullOrEmpty(request.downloadHandler?.text))
                {
                    errorMsg += $"\nResponse: {request.downloadHandler.text}";
                }

                Debug.LogError($"[LvlUp] {errorMsg}");
                
                return new ApiResponse
                {
                    success = false,
                    error = errorMsg
                };
            }
        }
    }
}

