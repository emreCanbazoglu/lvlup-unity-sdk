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
        private readonly Func<GeoData> _getGeoDataFunc; // Delegate to get geo data without tight coupling

        private readonly Dictionary<Models.CrashReport, int> _failedReports 
            = new Dictionary<Models.CrashReport, int>();
        
        private string _userId;
        private string _sessionId;
        private bool _isEnabled = false;
        private bool _handlersRegistered = false; // Track if handlers are already registered
        private List<Breadcrumb> _breadcrumbs = new List<Breadcrumb>();
        private const int MAX_BREADCRUMBS = 50;
        private const int MAX_RETRY_ATTEMPTS = 3;
        private bool _isReporting = false; // Prevent recursive crash reporting

        public CrashReporter(LvlUpHttpClient httpClient, MonoBehaviour coroutineRunner, string apiKey, Func<GeoData> getGeoDataFunc = null, string userId = null, string sessionId = null)
        {
            _httpClient = httpClient;
            _coroutineRunner = coroutineRunner;
            _apiKey = apiKey;
            _getGeoDataFunc = getGeoDataFunc;
            _userId = userId;
            _sessionId = sessionId;
            
            SetEnabled(_isEnabled);
        }

        /// <summary>
        /// Enable or disable crash reporting
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            if(enabled == _isEnabled) return; // No change, skip
            
            _isEnabled = enabled;
            
            if (enabled)
            {
                RegisterExceptionHandlers();
            }
            else if (!enabled)
            {
                UnregisterExceptionHandlers();
            }
        }
        
        /// <summary>
        /// Set the user ID for crash reports
        /// </summary>
        public void SetUserId(string userId)
        {
            _userId = userId;
        }

        public void SetSessionId(string sessionId)
        {
            _sessionId = sessionId;
        }

        /// <summary>
        /// Register Unity exception handlers
        /// </summary>
        private void RegisterExceptionHandlers()
        {
            if (_handlersRegistered) return; // Already registered, skip
            
            Application.logMessageReceived += HandleLogMessage;
            _handlersRegistered = true;
        }

        /// <summary>
        /// Unregister Unity exception handlers
        /// </summary>
        private void UnregisterExceptionHandlers()
        {
            if (!_handlersRegistered) return; // Not registered, skip
            
            Application.logMessageReceived -= HandleLogMessage;
            _handlersRegistered = false;
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
                Timestamp = DateTime.UtcNow.ToString("o"), // ISO 8601 format for proper JSON serialization
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

            // Prevent infinite loop: if we're already reporting a crash, don't report crashes from the crash reporter itself
            if (_isReporting)
            {
                Debug.LogWarning("[LvlUp CrashReporter] Already reporting a crash, skipping to prevent infinite loop");
                return;
            }

            Debug.Log($"[LvlUp CrashReporter] Reporting crash: {crashType} / {severity} / {message}");

            _isReporting = true;
            try
            {
                var report = new Models.CrashReport
                {
                    UserId = _userId,
                    SessionId = _sessionId,
                    CrashType = crashType,
                    Severity = severity,
                    Message = message,
                    StackTrace = stackTrace,
                    ExceptionType = exceptionType ?? "UnknownException",
                    
                    // System info
                    MemoryUsage = (long)UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(),
                    BatteryLevel = SystemInfo.batteryLevel,
                    
                    // Context
                    Breadcrumbs = new List<Breadcrumb>(_breadcrumbs),
                    CustomData = customData,
                    
                    Timestamp = DateTime.UtcNow
                };

                // Apply geo data if available via the delegate function
                if (_getGeoDataFunc != null)
                {
                    var geoData = _getGeoDataFunc();
                    if (geoData != null && geoData.IsValid())
                    {
                        report.SetGeoLocation(
                            geoData.country,
                            geoData.countryCode,
                            geoData.region,
                            geoData.city,
                            geoData.latitude,
                            geoData.longitude,
                            geoData.timezone
                        );
                    }
                }

                // Send immediately
                Debug.Log("[LvlUp CrashReporter] Sending crash report immediately");
                SendSingleCrashReport(report);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LvlUp] Failed to create crash report: {ex.Message}");
                // Reset flag on exception during report creation
                _isReporting = false;
            }
        }

        /// <summary>
        /// Send a single crash report asynchronously
        /// </summary>
        private void SendSingleCrashReport(Models.CrashReport report)
        {
            try
            {
                // API key is sent in X-API-Key header, not in URL (same pattern as event tracking)
                string endpoint = "/crashes";
                
                Debug.Log($"[LvlUp CrashReporter] POST {endpoint} - {report.Message}");
                
                // Send the crash report (fire and forget with callback for retry logic)
                _coroutineRunner.StartCoroutine(_httpClient.Post<object>(endpoint, report, (response) =>
                {
                    try
                    {
                        if (!response.success)
                        {
                            Debug.LogWarning($"[LvlUp CrashReporter] Failed to send crash report: {response.error}");
                            
                            // Track retry attempts
                            if (!_failedReports.ContainsKey(report))
                            {
                                _failedReports[report] = 1;
                            }
                            else
                            {
                                _failedReports[report]++;
                            }

                            // Log if max retries reached
                            if (_failedReports[report] >= MAX_RETRY_ATTEMPTS)
                            {
                                Debug.LogError($"[LvlUp CrashReporter] Dropping crash report after {MAX_RETRY_ATTEMPTS} failed attempts: {report.Message}");
                                _failedReports.Remove(report);
                            }
                        }
                        else
                        {
                            Debug.Log("[LvlUp CrashReporter] Crash report sent successfully");
                            // Remove from failed reports if it was there
                            _failedReports.Remove(report);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Catch exceptions in callback to prevent infinite loop
                        Debug.LogError($"[LvlUp CrashReporter] Exception in crash report callback: {ex.Message}");
                    }
                    finally
                    {
                        // Reset reporting flag after callback completes (whether success or failure)
                        _isReporting = false;
                    }
                }));
            }
            catch (Exception ex)
            {
                // Catch exceptions during report sending to prevent infinite loop
                Debug.LogError($"[LvlUp CrashReporter] Exception while sending crash report: {ex.Message}");
                // Reset flag if exception occurs before coroutine starts
                _isReporting = false;
            }
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
        /// Clear all breadcrumbs
        /// </summary>
        public void ClearBreadcrumbs()
        {
            _breadcrumbs.Clear();
        }
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

