using UnityEngine;
using UnityEditor;
using System.Linq;
using LvlUp.RemoteConfig;
using LvlUp.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LvlUp.Editor
{
    /// <summary>
    /// Debug window for LvlUp Analytics SDK
    /// Shows API key, environment, and remote config values
    /// </summary>
    public class LvlUpDebugWindow : EditorWindow
    {
        private Vector2 _scrollPosition;
        private string _selectedEnvironment = "production";
        private string _selectedPlatform = "editor";
        private bool _showApiKey;
        private LvlUpConfigScriptable _config;
        private string _configSearchFilter = "";

        // Styles
        private GUIStyle _headerStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _keyStyle;
        private GUIStyle _valueStyle;
        private GUIStyle _labelStyle;
        private bool _stylesInitialized;

        [MenuItem("Window/LvlUp/Debug Window")]
        public static void ShowWindow()
        {
            LvlUpDebugWindow window = GetWindow<LvlUpDebugWindow>("LvlUp Debug");
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        private void OnEnable()
        {
            LoadConfig();
            
            // Load platform override if set
            if (LvlUpDebugSettings.HasPlatformOverride)
            {
                _selectedPlatform = LvlUpDebugSettings.PlatformOverride;
            }
            else
            {
                _selectedPlatform = "editor";
            }
        }

        private void LoadConfig()
        {
            _config = Resources.Load<LvlUpConfigScriptable>("LvlUpConfig");
            if (_config != null)
            {
                _selectedEnvironment = _config.remoteConfigEnvironment;
            }
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                padding = new RectOffset(10, 10, 10, 10)
            };

            _sectionStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(5, 5, 5, 5)
            };

            _keyStyle = new GUIStyle(EditorStyles.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 11
            };

            _valueStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                fontSize = 11
            };

            _labelStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11
            };

            _stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitializeStyles();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            // Header
            GUILayout.Label("LvlUp Analytics Debug", _headerStyle);
            EditorGUILayout.Space(5);

            // SDK Status Section
            DrawStatusSection();
            EditorGUILayout.Space(10);

            // Configuration Section
            DrawConfigurationSection();
            EditorGUILayout.Space(10);

            // Environment Section (Editor only)
            if (!Application.isPlaying)
            {
                DrawEnvironmentSection();
                EditorGUILayout.Space(10);
                
                // Platform Simulation Section (Editor only)
                DrawPlatformSimulationSection();
                EditorGUILayout.Space(10);
            }

            // Remote Config Section
            DrawRemoteConfigSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawStatusSection()
        {
            EditorGUILayout.BeginVertical(_sectionStyle);
            GUILayout.Label("SDK Status", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            LvlUpManager manager = FindObjectOfType<LvlUpManager>();
            bool isInitialized = manager != null && manager.IsInitialized();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Status:", _labelStyle, GUILayout.Width(120));
            GUIStyle statusStyle = new GUIStyle(EditorStyles.label);
            statusStyle.normal.textColor = isInitialized ? Color.green : Color.red;
            GUILayout.Label(isInitialized ? "Initialized" : "Not Initialized", statusStyle);
            EditorGUILayout.EndHorizontal();

            if (isInitialized && manager != null)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("User ID:", _labelStyle, GUILayout.Width(120));
                string userId = manager.GetCurrentUserId();
                GUILayout.Label(string.IsNullOrEmpty(userId) ? "N/A" : userId, _valueStyle);
                EditorGUILayout.EndHorizontal();

                var session = manager.GetCurrentSession();
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Session ID:", _labelStyle, GUILayout.Width(120));
                GUILayout.Label(session != null ? session.sessionId : "N/A", _valueStyle);
                EditorGUILayout.EndHorizontal();
                
                // Show platform override if active
#if UNITY_EDITOR
                if (LvlUpDebugSettings.HasPlatformOverride)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("Platform:", _labelStyle, GUILayout.Width(120));
                    GUIStyle platformStyle = new GUIStyle(EditorStyles.label);
                    platformStyle.normal.textColor = Color.cyan;
                    platformStyle.fontStyle = FontStyle.Bold;
                    GUILayout.Label(LvlUpDebugSettings.PlatformOverride.ToUpper() + " (Simulated)", platformStyle);
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("Platform:", _labelStyle, GUILayout.Width(120));
                    GUILayout.Label("editor", _valueStyle);
                    EditorGUILayout.EndHorizontal();
                }
#endif
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawConfigurationSection()
        {
            EditorGUILayout.BeginVertical(_sectionStyle);
            GUILayout.Label("Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            if (_config == null)
            {
                EditorGUILayout.HelpBox("LvlUpConfig not found. Please create one using Assets > LvlUp > Create Configuration", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            // API Key
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("API Key:", _labelStyle, GUILayout.Width(120));
            if (_showApiKey)
            {
                GUILayout.Label(string.IsNullOrEmpty(_config.apiKey) ? "Not Set" : _config.apiKey, _valueStyle);
            }
            else
            {
                GUILayout.Label(string.IsNullOrEmpty(_config.apiKey) ? "Not Set" : "••••••••••••••••", _valueStyle);
            }
            if (GUILayout.Button(_showApiKey ? "Hide" : "Show", GUILayout.Width(60)))
            {
                _showApiKey = !_showApiKey;
            }
            EditorGUILayout.EndHorizontal();

            // Base URL
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Base URL:", _labelStyle, GUILayout.Width(120));
            GUILayout.Label(string.IsNullOrEmpty(_config.GetBaseUrl()) ? "Not Set" : _config.GetBaseUrl(), _valueStyle);
            EditorGUILayout.EndHorizontal();

            // Environment
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Environment:", _labelStyle, GUILayout.Width(120));
            GUILayout.Label(_config.remoteConfigEnvironment, _valueStyle);
            EditorGUILayout.EndHorizontal();

            // Open Config Button
            EditorGUILayout.Space(5);
            if (GUILayout.Button("Open Configuration"))
            {
                Selection.activeObject = _config;
                EditorGUIUtility.PingObject(_config);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawEnvironmentSection()
        {
            EditorGUILayout.BeginVertical(_sectionStyle);
            GUILayout.Label("Environment Switch (Editor Only)", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox("Switch environments to test different remote config settings. Changes take effect when you enter Play mode.", MessageType.Info);
            
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Select Environment:", _labelStyle, GUILayout.Width(120));
            
            string[] environments = new string[] { "production", "staging", "development" };
            int currentIndex = System.Array.IndexOf(environments, _selectedEnvironment);
            if (currentIndex == -1) currentIndex = 0;
            
            int newIndex = EditorGUILayout.Popup(currentIndex, environments);
            if (newIndex != currentIndex)
            {
                _selectedEnvironment = environments[newIndex];
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            if (GUILayout.Button("Apply Environment"))
            {
                if (_config != null)
                {
                    Undo.RecordObject(_config, "Change Remote Config Environment");
                    _config.remoteConfigEnvironment = _selectedEnvironment;
                    EditorUtility.SetDirty(_config);
                    AssetDatabase.SaveAssets();
                    EditorUtility.DisplayDialog("Environment Changed", 
                        $"Environment changed to: {_selectedEnvironment}\n\nEnter Play mode to use the new environment.", 
                        "OK");
                    Debug.Log($"[LvlUp Debug] Environment changed to: {_selectedEnvironment}. Enter Play mode to apply changes.");
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPlatformSimulationSection()
        {
            EditorGUILayout.BeginVertical(_sectionStyle);
            GUILayout.Label("Platform Simulation (Editor Only)", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox("Simulate different platforms to test platform-specific behavior. The simulated platform will be reported in all events.", MessageType.Info);
            
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Simulate Platform:", _labelStyle, GUILayout.Width(120));
            
            string[] platforms = new string[] { "editor", "ios", "android", "webgl", "windows", "macos", "linux" };
            string[] platformLabels = new string[] { "Editor (Default)", "iOS", "Android", "WebGL", "Windows", "macOS", "Linux" };
            int currentIndex = System.Array.IndexOf(platforms, _selectedPlatform);
            if (currentIndex == -1) currentIndex = 0;
            
            int newIndex = EditorGUILayout.Popup(currentIndex, platformLabels);
            if (newIndex != currentIndex)
            {
                _selectedPlatform = platforms[newIndex];
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Apply Platform"))
            {
                if (_selectedPlatform == "editor")
                {
                    LvlUpDebugSettings.ClearPlatformOverride();
                    EditorUtility.DisplayDialog("Platform Reset", 
                        "Platform simulation disabled. SDK will report 'editor' as platform.\n\nEnter Play mode to apply changes.", 
                        "OK");
                    Debug.Log($"[LvlUp Debug] Platform override cleared. Using default 'editor' platform.");
                }
                else
                {
                    LvlUpDebugSettings.PlatformOverride = _selectedPlatform;
                    EditorUtility.DisplayDialog("Platform Simulation", 
                        $"Platform simulation set to: {_selectedPlatform}\n\nEnter Play mode to apply changes.\n\nAll events will report '{_selectedPlatform}' as the platform.", 
                        "OK");
                    Debug.Log($"[LvlUp Debug] Platform override set to: {_selectedPlatform}. Enter Play mode to apply changes.");
                }
            }
            
            if (GUILayout.Button("Reset to Default"))
            {
                _selectedPlatform = "editor";
                LvlUpDebugSettings.ClearPlatformOverride();
                Debug.Log($"[LvlUp Debug] Platform override cleared.");
            }
            
            EditorGUILayout.EndHorizontal();
            
            // Show current override if set
            if (LvlUpDebugSettings.HasPlatformOverride)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Current Override:", _labelStyle, GUILayout.Width(120));
                GUIStyle activeStyle = new GUIStyle(EditorStyles.label);
                activeStyle.normal.textColor = Color.cyan;
                activeStyle.fontStyle = FontStyle.Bold;
                GUILayout.Label(LvlUpDebugSettings.PlatformOverride.ToUpper(), activeStyle);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawRemoteConfigSection()
        {
            EditorGUILayout.BeginVertical(_sectionStyle);
            GUILayout.Label("Remote Config Values", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            LvlUpManager manager = FindObjectOfType<LvlUpManager>();
            bool isInitialized = manager != null && manager.IsInitialized();

            if (!isInitialized)
            {
                EditorGUILayout.HelpBox("SDK is not initialized. Start Play mode to see remote config values.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            var remoteConfigService = manager.GetRemoteConfigService();
            if (remoteConfigService == null || !remoteConfigService.IsInitialized)
            {
                EditorGUILayout.HelpBox("Remote Config Service is not initialized.", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            // Show current environment
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Current Environment:", _labelStyle, GUILayout.Width(140));
            GUILayout.Label(remoteConfigService.GetCurrentEnvironment(), _valueStyle);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Refresh button
            if (Application.isPlaying && GUILayout.Button("Refresh Config"))
            {
                remoteConfigService.FetchAsync(manager, success =>
                {
                    if (success)
                    {
                        Debug.Log("[LvlUp Debug] Remote config refreshed successfully");
                        Repaint();
                    }
                    else
                    {
                        Debug.LogWarning("[LvlUp Debug] Failed to refresh remote config");
                    }
                });
            }

            EditorGUILayout.Space(8);

            // Search/Filter box
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("🔍", GUILayout.Width(20));
            string newFilter = EditorGUILayout.TextField(_configSearchFilter, EditorStyles.toolbarSearchField);
            if (newFilter != _configSearchFilter)
            {
                _configSearchFilter = newFilter;
                Repaint();
            }
            if (!string.IsNullOrEmpty(_configSearchFilter) && GUILayout.Button("✕", GUILayout.Width(25)))
            {
                _configSearchFilter = "";
                Repaint();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Display all config values
            var allConfigs = remoteConfigService.GetAllConfigs().ToList();
            
            // Apply search filter
            var configs = string.IsNullOrEmpty(_configSearchFilter) 
                ? allConfigs 
                : allConfigs.Where(c => c.key.ToLower().Contains(_configSearchFilter.ToLower()) || 
                                       c.GetValueAsString().ToLower().Contains(_configSearchFilter.ToLower())).ToList();
            if (configs.Count == 0)
            {
                if (!string.IsNullOrEmpty(_configSearchFilter))
                {
                    EditorGUILayout.HelpBox($"No configs match '{_configSearchFilter}'. Try a different search term.", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox("No remote config values loaded. Click 'Refresh Config' to fetch values.", MessageType.Info);
                }
            }
            else
            {
                // Summary header
                GUIStyle summaryStyle = new GUIStyle(EditorStyles.boldLabel);
                summaryStyle.fontSize = 11;
                summaryStyle.normal.textColor = new Color(0.3f, 0.3f, 0.3f);
                
                EditorGUILayout.BeginHorizontal();
                string countText = !string.IsNullOrEmpty(_configSearchFilter) && configs.Count != allConfigs.Count
                    ? $"📊 {configs.Count} of {allConfigs.Count} Config{(allConfigs.Count != 1 ? "s" : "")}"
                    : $"📊 {configs.Count} Config Value{(configs.Count != 1 ? "s" : "")} Loaded";
                GUILayout.Label(countText, summaryStyle);
                GUILayout.FlexibleSpace();
                
                // Legend
                GUIStyle legendStyle = new GUIStyle(EditorStyles.miniLabel);
                legendStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
                GUILayout.Label("🔢 Number  📝 String  ⚡ Boolean  📦 Object", legendStyle);
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space(8);

                // Draw each config in a styled box
                foreach (var config in configs)
                {
                    DrawConfigValue(config);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawConfigValue(ConfigData config)
        {
            // Create a colored box based on data type
            Color boxColor = GetColorForDataType(config.dataType);
            GUIStyle boxStyle = new GUIStyle(EditorStyles.helpBox);
            boxStyle.normal.background = CreateColorTexture(boxColor);
            boxStyle.padding = new RectOffset(14, 14, 10, 10);
            boxStyle.margin = new RectOffset(2, 2, 3, 3);
            boxStyle.border = new RectOffset(3, 3, 3, 3);

            EditorGUILayout.BeginVertical(boxStyle);
            
            // Header row: Key and Type badge
            EditorGUILayout.BeginHorizontal();
            
            // Key with icon
            GUIStyle keyIconStyle = new GUIStyle(_keyStyle);
            keyIconStyle.normal.textColor = GetTextColorForDataType(config.dataType);
            string icon = GetIconForDataType(config.dataType);
            GUILayout.Label($"{icon} {config.key}", keyIconStyle);
            
            GUILayout.FlexibleSpace();
            
            // Copy button
            if (GUILayout.Button("📋", GUILayout.Width(30), GUILayout.Height(18)))
            {
                string valueStr = config.GetValueAsString();
                EditorGUIUtility.systemCopyBuffer = valueStr;
                Debug.Log($"[LvlUp Debug] Copied value to clipboard: {config.key} = {valueStr}");
            }
            
            GUILayout.Space(5);
            
            // Type badge with colored background
            GUIStyle typeBadgeStyle = new GUIStyle(EditorStyles.miniLabel);
            typeBadgeStyle.normal.textColor = Color.white;
            typeBadgeStyle.normal.background = CreateColorTexture(GetBadgeColorForDataType(config.dataType));
            typeBadgeStyle.padding = new RectOffset(6, 6, 2, 2);
            typeBadgeStyle.fontStyle = FontStyle.Bold;
            typeBadgeStyle.alignment = TextAnchor.MiddleCenter;
            GUILayout.Label(config.dataType.ToUpper(), typeBadgeStyle, GUILayout.MinWidth(60));
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Value with better formatting
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8);
            
            GUIStyle valueDisplayStyle = new GUIStyle(_valueStyle);
            valueDisplayStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f); // Light gray for dark backgrounds
            valueDisplayStyle.fontStyle = FontStyle.Normal;
            valueDisplayStyle.fontSize = 12;
            valueDisplayStyle.wordWrap = true;
            
            string formattedValue = FormatConfigValue(config.GetValueAsString(), config.dataType);
            GUILayout.Label(formattedValue, valueDisplayStyle);
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        private Color GetColorForDataType(string dataType)
        {
            // Dark, modern background colors
            switch (dataType?.ToLower())
            {
                case "int32":
                case "int64":
                case "int":
                case "double":
                case "single":
                case "float":
                    return new Color(0.15f, 0.20f, 0.30f); // Dark blue
                case "boolean":
                case "bool":
                    return new Color(0.25f, 0.15f, 0.30f); // Dark purple
                case "string":
                    return new Color(0.15f, 0.28f, 0.20f); // Dark green
                default:
                    return new Color(0.30f, 0.22f, 0.15f); // Dark brown
            }
        }

        private Color GetTextColorForDataType(string dataType)
        {
            // Bright, readable text colors for dark backgrounds
            switch (dataType?.ToLower())
            {
                case "int32":
                case "int64":
                case "int":
                case "double":
                case "single":
                case "float":
                    return new Color(0.50f, 0.75f, 1.0f); // Bright blue
                case "boolean":
                case "bool":
                    return new Color(0.85f, 0.60f, 1.0f); // Bright purple
                case "string":
                    return new Color(0.55f, 0.95f, 0.70f); // Bright green
                default:
                    return new Color(1.0f, 0.80f, 0.50f); // Bright orange
            }
        }

        private Color GetBadgeColorForDataType(string dataType)
        {
            // Vibrant badge colors for dark theme
            switch (dataType?.ToLower())
            {
                case "int32":
                case "int64":
                case "int":
                case "double":
                case "single":
                case "float":
                    return new Color(0.30f, 0.60f, 0.90f); // Blue badge
                case "boolean":
                case "bool":
                    return new Color(0.70f, 0.40f, 0.90f); // Purple badge
                case "string":
                    return new Color(0.35f, 0.80f, 0.55f); // Green badge
                default:
                    return new Color(0.90f, 0.65f, 0.35f); // Orange badge
            }
        }

        private string GetIconForDataType(string dataType)
        {
            switch (dataType?.ToLower())
            {
                case "int32":
                case "int64":
                case "int":
                case "double":
                case "single":
                case "float":
                    return "🔢"; // Numbers
                case "boolean":
                case "bool":
                    return "⚡"; // Boolean
                case "string":
                    return "📝"; // String
                default:
                    return "📦"; // Object/Other
            }
        }

        private string FormatConfigValue(string value, string dataType)
        {
            if (string.IsNullOrEmpty(value))
                return "<empty>";

            // Format based on type
            switch (dataType?.ToLower())
            {
                case "boolean":
                case "bool":
                    if (bool.TryParse(value, out bool boolVal))
                        return boolVal ? "✓ TRUE" : "✗ FALSE";
                    break;
                case "int32":
                case "int64":
                case "int":
                    if (int.TryParse(value, out int intVal))
                        return intVal.ToString("#,##0");
                    break;
                case "double":
                case "single":
                case "float":
                    if (double.TryParse(value, out double doubleVal))
                        return doubleVal.ToString("F2");
                    break;
                case "object":
                    // Try to parse and display as hierarchical key-value format
                    try
                    {
                        if (value.StartsWith("{") || value.StartsWith("["))
                        {
                            var parsed = JsonConvert.DeserializeObject(value);
                            if (parsed != null)
                            {
                                string formatted = FormatObjectHierarchy(parsed, 0);
                                // Limit to reasonable length
                                if (formatted.Length > 800)
                                    formatted = formatted.Substring(0, 797) + "...";
                                return formatted;
                            }
                        }
                    }
                    catch
                    {
                        // If JSON parsing fails, fall through to default handling
                    }
                    break;
            }

            // Truncate very long strings
            if (value.Length > 200)
                return value.Substring(0, 197) + "...";

            return value;
        }

        private string FormatObjectHierarchy(object obj, int indentLevel)
        {
            if (obj == null)
                return "null";

            var indent = new string(' ', indentLevel * 4);
            var result = new System.Text.StringBuilder();

            // Handle JObject (JSON objects)
            if (obj is Newtonsoft.Json.Linq.JObject jObj)
            {
                foreach (var prop in jObj.Properties())
                {
                    string key = CapitalizeFirstLetter(prop.Name);
                    var propValue = prop.Value;

                    if (propValue is Newtonsoft.Json.Linq.JObject || propValue is Newtonsoft.Json.Linq.JArray)
                    {
                        // Nested object or array
                        result.AppendLine($"{indent}{key}:");
                        result.Append(FormatObjectHierarchy(propValue, indentLevel + 1));
                    }
                    else
                    {
                        // Simple value
                        result.AppendLine($"{indent}{key}: {FormatSimpleValue(propValue)}");
                    }
                }
            }
            // Handle JArray (JSON arrays)
            else if (obj is Newtonsoft.Json.Linq.JArray jArr)
            {
                for (int i = 0; i < jArr.Count; i++)
                {
                    var item = jArr[i];
                    if (item is Newtonsoft.Json.Linq.JObject || item is Newtonsoft.Json.Linq.JArray)
                    {
                        result.AppendLine($"{indent}[{i}]:");
                        result.Append(FormatObjectHierarchy(item, indentLevel + 1));
                    }
                    else
                    {
                        result.AppendLine($"{indent}- {FormatSimpleValue(item)}");
                    }
                }
            }

            return result.ToString();
        }

        private string FormatSimpleValue(object value)
        {
            if (value == null)
                return "null";

            // Handle JValue
            if (value is Newtonsoft.Json.Linq.JValue jVal)
            {
                value = jVal.Value;
            }

            if (value is bool b)
                return b ? "true" : "false";
            if (value is string s)
                return $"\"{s}\"";
            if (value is int || value is long || value is double || value is float)
                return value.ToString();

            return value.ToString();
        }

        private string CapitalizeFirstLetter(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            
            // Split camelCase or snake_case
            text = System.Text.RegularExpressions.Regex.Replace(text, "([a-z])([A-Z])", "$1 $2");
            text = text.Replace("_", " ");
            
            // Capitalize first letter of each word
            var words = text.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1);
                }
            }
            
            return string.Join(" ", words);
        }

        private Texture2D CreateColorTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}

