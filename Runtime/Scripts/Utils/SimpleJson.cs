using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace LvlUp.Utils
{
    /// <summary>
    /// Simple JSON serializer that handles Dictionary and null values
    /// Used instead of JsonUtility which has limitations
    /// </summary>
    public static class SimpleJson
    {
        public static string ToJson(object obj)
        {
            if (obj == null)
                return "null";

            var type = obj.GetType();

            // Handle primitives
            if (type == typeof(string))
                return $"\"{EscapeString((string)obj)}\"";
            
            if (type == typeof(bool))
                return obj.ToString().ToLower();
            
            // Handle numeric types with invariant culture to avoid locale issues (e.g., comma as decimal separator in Turkish)
            if (type.IsPrimitive || type == typeof(decimal))
            {
                if (obj is float floatVal)
                    return floatVal.ToString(CultureInfo.InvariantCulture);
                if (obj is double doubleVal)
                    return doubleVal.ToString(CultureInfo.InvariantCulture);
                if (obj is decimal decimalVal)
                    return decimalVal.ToString(CultureInfo.InvariantCulture);
                
                return obj.ToString();
            }

            // Handle Dictionary
            if (obj is IDictionary dict)
            {
                var sb = new StringBuilder();
                sb.Append("{");
                bool first = true;
                foreach (DictionaryEntry entry in dict)
                {
                    if (!first) sb.Append(",");
                    first = false;
                    sb.Append($"\"{EscapeString(entry.Key.ToString())}\":");
                    sb.Append(ToJson(entry.Value));
                }
                sb.Append("}");
                return sb.ToString();
            }

            // Handle List/Array
            if (obj is IList list)
            {
                var sb = new StringBuilder();
                sb.Append("[");
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append(ToJson(list[i]));
                }
                sb.Append("]");
                return sb.ToString();
            }

            // Handle objects with fields (fallback to JsonUtility for complex types)
            // But manually construct JSON to include null fields
            var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            var sb2 = new StringBuilder();
            sb2.Append("{");
            bool firstField = true;
            
            foreach (var field in fields)
            {
                // Skip Unity-specific attributes that shouldn't be serialized
                if (field.Name.StartsWith("m_"))
                    continue;
                    
                if (!firstField) sb2.Append(",");
                firstField = false;
                
                var fieldValue = field.GetValue(obj);
                sb2.Append($"\"{field.Name}\":");
                
                // Always serialize, even if null
                sb2.Append(ToJson(fieldValue));
            }
            
            sb2.Append("}");
            return sb2.ToString();
        }

        /// <summary>
        /// Deserialize JSON string to object of type T
        /// Uses JsonUtility for serializable classes, custom parser for complex structures
        /// </summary>
        public static T FromJson<T>(string json)
        {
            if (string.IsNullOrEmpty(json))
                return default(T);

            var type = typeof(T);
            
            try
            {
                // Try JsonUtility first - it's more reliable for serializable classes
                var result = JsonUtility.FromJson<T>(json);
                if (result != null)
                    return result;
            }
            catch { }

            // Fallback for primitives and simple types
            try
            {
                // Handle primitives and simple types
                if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
                {
                    return (T)ConvertJsonValue(json, type);
                }
                
                // Handle Dictionary and List types
                if (typeof(IDictionary).IsAssignableFrom(type))
                {
                    return (T)(object)ParseJsonDictionary(json);
                }
                
                if (typeof(IList).IsAssignableFrom(type))
                {
                    return (T)(object)ParseJsonArray(json);
                }
            }
            catch { }

            return default(T);
        }

        /// <summary>
        /// Extract a field value from JSON string
        /// </summary>
        private static string ExtractJsonField(string json, string fieldName)
        {
            string searchPattern = $"\"{fieldName}\"\\s*:\\s*";
            var match = System.Text.RegularExpressions.Regex.Match(json, searchPattern);
            
            if (!match.Success)
                return null;

            int startIndex = match.Index + match.Length;
            int depth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = startIndex; i < json.Length; i++)
            {
                char c = json[i];

                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (c == '"' && !escaped)
                {
                    inString = !inString;
                    continue;
                }

                if (!inString)
                {
                    if (c == '{' || c == '[')
                        depth++;
                    else if (c == '}' || c == ']')
                    {
                        if (depth == 0)
                            return json.Substring(startIndex, i - startIndex).Trim();
                        depth--;
                    }
                    else if ((c == ',' || c == '}') && depth == 0)
                        return json.Substring(startIndex, i - startIndex).Trim();
                }
            }

            return json.Substring(startIndex).Trim();
        }

        /// <summary>
        /// Convert JSON value string to proper type
        /// </summary>
        private static object ConvertJsonValue(string jsonValue, Type targetType)
        {
            jsonValue = jsonValue.Trim();

            if (jsonValue == "null")
                return null;

            if (targetType == typeof(string))
            {
                return jsonValue.Trim('"');
            }

            if (targetType == typeof(bool))
            {
                return jsonValue.ToLower() == "true";
            }

            if (targetType == typeof(int))
            {
                if (int.TryParse(jsonValue, out int intVal))
                    return intVal;
            }

            if (targetType == typeof(long))
            {
                if (long.TryParse(jsonValue, out long longVal))
                    return longVal;
            }

            if (targetType == typeof(float))
            {
                if (float.TryParse(jsonValue, NumberStyles.Any, CultureInfo.InvariantCulture, out float floatVal))
                    return floatVal;
            }

            if (targetType == typeof(double))
            {
                if (double.TryParse(jsonValue, NumberStyles.Any, CultureInfo.InvariantCulture, out double doubleVal))
                    return doubleVal;
            }

            if (targetType == typeof(object))
            {
                // Return as Dictionary for dynamic objects
                return ParseJsonObject(jsonValue);
            }


            return null;
        }

        /// <summary>
        /// Parse JSON object/array string to Dictionary or List
        /// </summary>
        private static object ParseJsonObject(string json)
        {
            json = json.Trim();

            if (json.StartsWith("{") && json.EndsWith("}"))
            {
                return ParseJsonDictionary(json);
            }
            else if (json.StartsWith("[") && json.EndsWith("]"))
            {
                return ParseJsonArray(json);
            }

            return null;
        }

        private static Dictionary<string, object> ParseJsonDictionary(string json)
        {
            var dict = new Dictionary<string, object>();
            json = json.Substring(1, json.Length - 2).Trim();

            if (string.IsNullOrEmpty(json))
                return dict;

            var parts = SplitJsonFields(json);
            foreach (var part in parts)
            {
                var colonIndex = part.IndexOf(':');
                if (colonIndex > 0)
                {
                    string key = part.Substring(0, colonIndex).Trim().Trim('"');
                    string value = part.Substring(colonIndex + 1).Trim();
                    dict[key] = ParseJsonObject(value) ?? value;
                }
            }

            return dict;
        }

        private static List<object> ParseJsonArray(string json)
        {
            var list = new List<object>();
            json = json.Substring(1, json.Length - 2).Trim();

            if (string.IsNullOrEmpty(json))
                return list;

            var parts = SplitJsonFields(json);
            foreach (var part in parts)
            {
                list.Add(ParseJsonObject(part.Trim()) ?? part.Trim());
            }

            return list;
        }

        private static List<string> SplitJsonFields(string json)
        {
            var parts = new List<string>();
            var currentPart = new StringBuilder();
            int depth = 0;
            bool inString = false;
            bool escaped = false;

            foreach (char c in json)
            {
                if (escaped)
                {
                    escaped = false;
                    currentPart.Append(c);
                    continue;
                }

                if (c == '\\')
                {
                    escaped = true;
                    currentPart.Append(c);
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    currentPart.Append(c);
                    continue;
                }

                if (!inString)
                {
                    if (c == '{' || c == '[')
                    {
                        depth++;
                        currentPart.Append(c);
                    }
                    else if (c == '}' || c == ']')
                    {
                        depth--;
                        currentPart.Append(c);
                    }
                    else if (c == ',' && depth == 0)
                    {
                        parts.Add(currentPart.ToString());
                        currentPart = new StringBuilder();
                    }
                    else
                    {
                        currentPart.Append(c);
                    }
                }
                else
                {
                    currentPart.Append(c);
                }
            }

            if (currentPart.Length > 0)
                parts.Add(currentPart.ToString());

            return parts;
        }

        private static string EscapeString(string str)
        {
            if (string.IsNullOrEmpty(str))
                return str ?? "";

            return str
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }
    }
}

