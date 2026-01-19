using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using LvlUp.Models;

namespace LvlUp.Services
{
    /// <summary>
    /// Crash and exception reporting service
    /// Automatically captures unhandled exceptions and provides manual crash reporting
    /// </summary>
    public class CrashReporter
    {
        private readonly LvlUpHttpClient _httpClient;
        private readonly MonoBehaviour _coroutineRunner;
        private readonly string _apiKey;
        private readonly string _userId;
        private readonly string _sessionId;
        private readonly Queue<CrashReport> _crashQueue = new Queue<CrashReport>();
        private readonly Dictionary<CrashReport, int> _failedReports = new Dictionary<CrashReport, int>();
        private bool _isEnabled = true;
        private bool _autoCapture = true;
        private bool _isSending = false; // Prevent concurrent sending
        private List<Breadcrumb> _breadcrumbs = new List<Breadcrumb>();
        private const int MAX_BREADCRUMBS = 50;
        private const int MAX_RETRY_ATTEMPTS = 3;
        
        // Batch sending configuration
        private float _lastBatchSendTime;
        private const float BATCH_SEND_INTERVAL = 30f; // Send batches every 30 seconds

        public CrashReporter(LvlUpHttpClient httpClient, MonoBehaviour coroutineRunner, string apiKey, string userId = null, string sessionId = null)
        {
            _httpClient = httpClient;
            _coroutineRunner = coroutineRunner;
            _apiKey = apiKey;
            _userId = userId;
            _sessionId = sessionId;
            _lastBatchSendTime = Time.realtimeSinceStartup;
        }

        /// <summary>
        /// Enable or disable crash reporting
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
            
            if (enabled && _autoCapture)
            {
                RegisterExceptionHandlers();
            }
            else if (!enabled)
            {
                UnregisterExceptionHandlers();
            }
        }
        
        /// <summary>
        /// Update method - should be called regularly (e.g., from MonoBehaviour Update)
        /// Handles periodic batch sending of crash reports
        /// </summary>
        public void Update()
        {
            if (!_isEnabled) return;
            
            // Check if it's time to send batched crash reports
            if (Time.realtimeSinceStartup - _lastBatchSendTime >= BATCH_SEND_INTERVAL)
            {
                if (_crashQueue.Count > 0)
                {
                    Debug.Log($"[LvlUp CrashReporter] Batch send timer triggered. Queue size: {_crashQueue.Count}");
                    SendCrashReports();
                }
                _lastBatchSendTime = Time.realtimeSinceStartup;
            }
        }

        /// <summary>
        /// Enable or disable automatic exception capture
        /// </summary>
        public void SetAutoCapture(bool autoCapture)
        {
            _autoCapture = autoCapture;
            
            if (_isEnabled)
            {
                if (autoCapture)
                {
                    RegisterExceptionHandlers();
                }
                else
                {
                    UnregisterExceptionHandlers();
                }
            }
        }

        /// <summary>
        /// Register Unity exception handlers
        /// </summary>
        private void RegisterExceptionHandlers()
        {
            Application.logMessageReceived += HandleLogMessage;
        }

        /// <summary>
        /// Unregister Unity exception handlers
        /// </summary>
        private void UnregisterExceptionHandlers()
        {
            Application.logMessageReceived -= HandleLogMessage;
        }

        /// <summary>
        /// Handle Unity log messages and capture exceptions/errors
        /// </summary>
        private void HandleLogMessage(string condition, string stackTrace, LogType type)
        {
            if (!_isEnabled) return;

            // Add breadcrumb for all log types
            AddBreadcrumb($"{type}: {condition}", BreadcrumbType.Log);

            // Only report exceptions and errors
            if (type == LogType.Exception || type == LogType.Error)
            {
                string severity = type == LogType.Exception ? "ERROR" : "HIGH";
                string crashType = type == LogType.Exception ? "exception" : "error";
                
                ReportCrash(
                    crashType: crashType,
                    severity: severity,
                    message: condition,
                    stackTrace: stackTrace,
                    exceptionType: ExtractExceptionType(condition)
                );
            }
        }

        /// <summary>
        /// Extract exception type from error message
        /// </summary>
        private string ExtractExceptionType(string message)
        {
            if (string.IsNullOrEmpty(message)) return "UnknownException";

            // Try to extract exception type (e.g., "NullReferenceException: Object reference...")
            int colonIndex = message.IndexOf(':');
            if (colonIndex > 0 && colonIndex < 100)
            {
                return message.Substring(0, colonIndex).Trim();
            }

            return "UnknownException";
        }

        /// <summary>
        /// Add a breadcrumb to track user actions leading to crashes
        /// </summary>
        public void AddBreadcrumb(string message, BreadcrumbType type = BreadcrumbType.Navigation, Dictionary<string, object> data = null)
        {
            var breadcrumb = new Breadcrumb
            {
                Timestamp = DateTime.UtcNow,
                Message = message,
                Type = type.ToString(),
                Data = data
            };

            _breadcrumbs.Add(breadcrumb);

            // Keep only last MAX_BREADCRUMBS
            if (_breadcrumbs.Count > MAX_BREADCRUMBS)
            {
                _breadcrumbs.RemoveAt(0);
            }
        }

        /// <summary>
        /// Manually report a crash or exception
        /// </summary>
        public void ReportCrash(
            string crashType,
            string severity,
            string message,
            string stackTrace,
            string exceptionType = null,
            Dictionary<string, object> customData = null)
        {
            if (!_isEnabled)
            {
                Debug.Log("[LvlUp CrashReporter] Crash reporting is disabled");
                return;
            }

            Debug.Log($"[LvlUp CrashReporter] Reporting crash: {crashType} / {severity} / {message}");

            try
            {
                var report = new CrashReport
                {
                    GameId = _apiKey,
                    UserId = _userId,
                    SessionId = _sessionId,
                    CrashType = crashType,
                    Severity = severity,
                    Message = message,
                    StackTrace = stackTrace,
                    ExceptionType = exceptionType ?? "UnknownException",
                    
                    // Device & Platform info (inherited from EventMetadata)
                    platform = GetPlatform(),
                    osVersion = SystemInfo.operatingSystem,
                    manufacturer = SystemInfo.deviceModel.Split(' ')[0],
                    device = SystemInfo.deviceModel,
                    deviceId = SystemInfo.deviceUniqueIdentifier,
                    
                    // App info (inherited from EventMetadata)
                    appVersion = Application.version,
                    bundleId = Application.identifier,
                    engineVersion = $"unity {Application.unityVersion}",
                    sdkVersion = "unity 1.0.0",
                    
                    // Connection type (inherited from EventMetadata)
                    connectionType = GetConnectionType(),
                    
                    // System info
                    MemoryUsage = (long)UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(),
                    BatteryLevel = SystemInfo.batteryLevel,
                    
                    // Context
                    Breadcrumbs = new List<Breadcrumb>(_breadcrumbs),
                    CustomData = customData,
                    
                    Timestamp = DateTime.UtcNow
                };

                _crashQueue.Enqueue(report);
                Debug.Log($"[LvlUp CrashReporter] Crash queued. Queue size: {_crashQueue.Count}");
                
                // Send immediately for high priority crashes
                if (severity == "CRITICAL" || severity == "HIGH")
                {
                    Debug.Log("[LvlUp CrashReporter] High priority crash - sending immediately");
                    SendCrashReports();
                }
                else
                {
                    Debug.Log($"[LvlUp CrashReporter] Normal priority crash - will send in batch (severity: {severity})");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LvlUp] Failed to create crash report: {ex.Message}");
            }
        }

        /// <summary>
        /// Send all queued crash reports to the backend
        /// </summary>
        public void SendCrashReports()
        {
            if (_crashQueue.Count == 0)
            {
                return;
            }

            // Prevent concurrent sending
            if (_isSending)
            {
                Debug.Log("[LvlUp CrashReporter] Already sending crash reports, skipping...");
                return;
            }

            _isSending = true;
            Debug.Log($"[LvlUp CrashReporter] Sending {_crashQueue.Count} crash report(s)");

            // Create a list of reports to send (process entire queue at once)
            var reportsToSend = new List<CrashReport>();
            while (_crashQueue.Count > 0)
            {
                reportsToSend.Add(_crashQueue.Dequeue());
            }

            // Send each report
            _coroutineRunner.StartCoroutine(SendCrashReportsCoroutine(reportsToSend));
        }

        /// <summary>
        /// Coroutine to send crash reports sequentially
        /// </summary>
        private IEnumerator SendCrashReportsCoroutine(List<CrashReport> reports)
        {
            int successCount = 0;
            int failedCount = 0;

            foreach (var report in reports)
            {
                bool completed = false;
                bool success = false;
                string errorMessage = null;

                // API key is used as the game identifier in the endpoint
                string endpoint = $"/games/{_apiKey}/crashes";
                
                Debug.Log($"[LvlUp CrashReporter] POST {endpoint} - {report.Message}");
                
                // Send the crash report
                _coroutineRunner.StartCoroutine(_httpClient.Post<object>(endpoint, report, (response) =>
                {
                    success = response.success;
                    completed = true;
                    errorMessage = response.error;
                    
                    if (!response.success)
                    {
                        Debug.LogWarning($"[LvlUp CrashReporter] Failed to send crash report: {response.error}");
                    }
                    else
                    {
                        Debug.Log("[LvlUp CrashReporter] Crash report sent successfully");
                    }
                }));

                // Wait for the request to complete (with timeout)
                float startTime = Time.realtimeSinceStartup;
                while (!completed && (Time.realtimeSinceStartup - startTime) < 10f)
                {
                    yield return null;
                }

                if (!completed)
                {
                    Debug.LogWarning("[LvlUp CrashReporter] Crash report request timed out");
                    success = false;
                }

                // Handle success/failure outside of yield scope
                if (success)
                {
                    successCount++;
                    // Remove from failed reports if it was there
                    _failedReports.Remove(report);
                }
                else
                {
                    failedCount++;
                    
                    // Track retry attempts
                    if (!_failedReports.ContainsKey(report))
                    {
                        _failedReports[report] = 1;
                    }
                    else
                    {
                        _failedReports[report]++;
                    }

                    // Re-queue if under retry limit
                    if (_failedReports[report] < MAX_RETRY_ATTEMPTS)
                    {
                        _crashQueue.Enqueue(report);
                        Debug.Log($"[LvlUp CrashReporter] Will retry crash report (attempt {_failedReports[report]}/{MAX_RETRY_ATTEMPTS})");
                    }
                    else
                    {
                        Debug.LogError($"[LvlUp CrashReporter] Dropping crash report after {MAX_RETRY_ATTEMPTS} failed attempts: {report.Message}");
                        _failedReports.Remove(report);
                    }
                }

                // Small delay between reports to avoid overwhelming the server
                yield return new WaitForSeconds(0.2f);
            }

            Debug.Log($"[LvlUp CrashReporter] Batch complete: {successCount} sent, {failedCount} failed");
            _isSending = false;
        }

        /// <summary>
        /// Report an exception with context
        /// </summary>
        public void ReportException(Exception exception, string context = null, Dictionary<string, object> customData = null)
        {
            if (!_isEnabled) return;

            string message = exception.Message;
            if (!string.IsNullOrEmpty(context))
            {
                message = $"{context}: {message}";
            }

            ReportCrash(
                crashType: "exception",
                severity: "ERROR",
                message: message,
                stackTrace: exception.StackTrace ?? "",
                exceptionType: exception.GetType().Name,
                customData: customData
            );
        }

        /// <summary>
        /// Report a handled error
        /// </summary>
        public void ReportError(string message, string stackTrace = null, Dictionary<string, object> customData = null)
        {
            if (!_isEnabled) return;

            ReportCrash(
                crashType: "error",
                severity: "MEDIUM",
                message: message,
                stackTrace: stackTrace ?? new System.Diagnostics.StackTrace().ToString(),
                exceptionType: "Error",
                customData: customData
            );
        }

        /// <summary>
        /// Get current platform string
        /// </summary>
        private string GetPlatform()
        {
#if UNITY_ANDROID
            return "android";
#elif UNITY_IOS
            return "ios";
#elif UNITY_WEBGL
            return "webgl";
#elif UNITY_STANDALONE_WIN
            return "windows";
#elif UNITY_STANDALONE_OSX
            return "macos";
#elif UNITY_STANDALONE_LINUX
            return "linux";
#else
            return "unknown";
#endif
        }

        /// <summary>
        /// Get current connection type
        /// </summary>
        private string GetConnectionType()
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
                return "offline";
            else if (Application.internetReachability == NetworkReachability.ReachableViaCarrierDataNetwork)
                return "wwan";
            else if (Application.internetReachability == NetworkReachability.ReachableViaLocalAreaNetwork)
                return "wifi";
            return "unknown";
        }

        /// <summary>
        /// Clear all breadcrumbs
        /// </summary>
        public void ClearBreadcrumbs()
        {
            _breadcrumbs.Clear();
        }
    }

    /// <summary>
    /// Crash report data model - extends EventMetadata for consistency
    /// </summary>
    [Serializable]
    public class CrashReport : EventMetadata
    {
        // Crash-specific fields
        public string GameId; // Note: This contains the API key, used as game identifier
        public string UserId;
        public string SessionId;
        public string CrashType;
        public string Severity;
        public string Message;
        public string StackTrace;
        public string ExceptionType;
        
        // System info (not in EventMetadata)
        public long? MemoryUsage;
        public float? BatteryLevel;
        
        // Context
        public List<Breadcrumb> Breadcrumbs;
        public Dictionary<string, object> CustomData;
        public DateTime Timestamp;
        
        // Note: Device/Platform/App metadata inherited from EventMetadata:
        // - platform, osVersion, manufacturer, device, deviceId
        // - appVersion, appBuild, bundleId, engineVersion, sdkVersion
        // - connectionType, sessionNum, country, city, etc.
    }

    /// <summary>
    /// Breadcrumb for tracking user actions
    /// </summary>
    [Serializable]
    public class Breadcrumb
    {
        public DateTime Timestamp;
        public string Message;
        public string Type;
        public Dictionary<string, object> Data;
    }

    /// <summary>
    /// Breadcrumb types
    /// </summary>
    public enum BreadcrumbType
    {
        Navigation,
        UserAction,
        Network,
        StateChange,
        Log,
        Error
    }
}

