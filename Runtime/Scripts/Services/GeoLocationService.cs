using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace LvlUp.Services
{
    /// <summary>
    /// Service for fetching geographic location information using IP geolocation
    /// Uses ipapi.co free API (no API key required for basic usage)
    /// </summary>
    public class GeoLocationService
    {
        private const string GEO_API_URL = "https://ipapi.co/json/";
        private const float CACHE_DURATION = 3600f; // Cache for 1 hour
        
        private GeoData _cachedGeoData;
        private float _cacheTimestamp;
        private bool _isFetching;

        /// <summary>
        /// Fetch geographic location data
        /// </summary>
        public IEnumerator FetchGeoLocation(Action<GeoData> onSuccess, Action<string> onError = null)
        {
            // Return cached data if still valid
            if (_cachedGeoData != null && Time.realtimeSinceStartup - _cacheTimestamp < CACHE_DURATION)
            {
                onSuccess?.Invoke(_cachedGeoData);
                yield break;
            }

            // Prevent multiple simultaneous requests
            if (_isFetching)
            {
                // Wait for existing request to complete
                while (_isFetching)
                {
                    yield return new WaitForSeconds(0.1f);
                }
                
                if (_cachedGeoData != null)
                {
                    onSuccess?.Invoke(_cachedGeoData);
                }
                yield break;
            }

            _isFetching = true;

            using (UnityWebRequest request = UnityWebRequest.Get(GEO_API_URL))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();

                _isFetching = false;

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        string jsonResponse = request.downloadHandler.text;
                        _cachedGeoData = ParseGeoData(jsonResponse);
                        _cacheTimestamp = Time.realtimeSinceStartup;
                        
                        onSuccess?.Invoke(_cachedGeoData);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[LvlUp] Failed to parse geo data: {e.Message}");
                        onError?.Invoke($"Parse error: {e.Message}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[LvlUp] Geo location request failed: {request.error}");
                    onError?.Invoke(request.error);
                }
            }
        }

        /// <summary>
        /// Parse JSON response from ipapi.co
        /// </summary>
        private GeoData ParseGeoData(string json)
        {
            // Simple JSON parsing without using JsonUtility (which doesn't handle all fields well)
            var geoData = new GeoData();

            try
            {
                // Extract fields from JSON string
                geoData.country = ExtractJsonValue(json, "country_name");
                geoData.countryCode = ExtractJsonValue(json, "country_code");
                geoData.region = ExtractJsonValue(json, "region");
                geoData.city = ExtractJsonValue(json, "city");
                geoData.timezone = ExtractJsonValue(json, "timezone");
                
                string latStr = ExtractJsonValue(json, "latitude");
                string lonStr = ExtractJsonValue(json, "longitude");
                
                if (float.TryParse(latStr, System.Globalization.NumberStyles.Float, 
                    System.Globalization.CultureInfo.InvariantCulture, out float lat))
                {
                    geoData.latitude = lat;
                }
                
                if (float.TryParse(lonStr, System.Globalization.NumberStyles.Float, 
                    System.Globalization.CultureInfo.InvariantCulture, out float lon))
                {
                    geoData.longitude = lon;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LvlUp] Error parsing geo fields: {e.Message}");
            }

            return geoData;
        }

        /// <summary>
        /// Simple JSON value extractor
        /// </summary>
        private string ExtractJsonValue(string json, string key)
        {
            string searchKey = $"\"{key}\":";
            int startIndex = json.IndexOf(searchKey);
            
            if (startIndex == -1)
                return null;
            
            startIndex += searchKey.Length;
            
            // Skip whitespace
            while (startIndex < json.Length && (json[startIndex] == ' ' || json[startIndex] == '\t'))
                startIndex++;
            
            // Check if value is string (starts with ")
            if (startIndex < json.Length && json[startIndex] == '"')
            {
                startIndex++; // Skip opening quote
                int endIndex = json.IndexOf('"', startIndex);
                if (endIndex > startIndex)
                {
                    return json.Substring(startIndex, endIndex - startIndex);
                }
            }
            else
            {
                // Value is not a string (number, boolean, null)
                int endIndex = json.IndexOfAny(new char[] { ',', '}', ']' }, startIndex);
                if (endIndex > startIndex)
                {
                    return json.Substring(startIndex, endIndex - startIndex).Trim();
                }
            }
            
            return null;
        }

        /// <summary>
        /// Clear cached geo data
        /// </summary>
        public void ClearCache()
        {
            _cachedGeoData = null;
            _cacheTimestamp = 0;
        }
    }

    /// <summary>
    /// Geographic location data
    /// </summary>
    [Serializable]
    public class GeoData
    {
        public string country;
        public string countryCode;
        public string region;
        public string city;
        public float? latitude;
        public float? longitude;
        public string timezone;

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(countryCode);
        }
    }
}

