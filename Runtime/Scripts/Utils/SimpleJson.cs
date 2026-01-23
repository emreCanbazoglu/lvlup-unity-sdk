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
        /// Uses custom parser that handles dictionaries, generics, and complex structures
        /// </summary>
        public static T FromJson<T>(string json)
        {
            if (string.IsNullOrEmpty(json))
                return default(T);

            var type = typeof(T);
            
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

                // Handle complex objects with reflection-based parsing
                // This supports classes with Dictionary fields, generic types, etc.
                var parsedObj = ParseJsonToType(json, type);
                if (parsedObj != null)
                {
                    return (T)parsedObj;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SimpleJson] Failed to parse JSON to type {type.Name}: {ex.Message}");
            }

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

        /// <summary>
        /// Parse JSON to a specific type using reflection
        /// Handles complex objects, generics, and nested structures
        /// </summary>
        private static object ParseJsonToType(string json, Type targetType)
        {
            json = json.Trim();

            // Handle null
            if (json == "null")
                return null;

            // Handle primitives
            if (targetType.IsPrimitive || targetType == typeof(string) || targetType == typeof(decimal))
            {
                return ConvertJsonValue(json, targetType);
            }

            // Handle Dictionary types
            if (typeof(IDictionary).IsAssignableFrom(targetType))
            {
                var dict = ParseJsonDictionary(json);
                
                // If it's a generic Dictionary<TKey, TValue>, convert values to the right type
                if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                {
                    var keyType = targetType.GetGenericArguments()[0];
                    var valueType = targetType.GetGenericArguments()[1];
                    var typedDict = Activator.CreateInstance(targetType) as IDictionary;
                    
                    foreach (var kvp in dict)
                    {
                        var key = Convert.ChangeType(kvp.Key, keyType);
                        var value = ConvertToType(kvp.Value, valueType);
                        typedDict[key] = value;
                    }
                    return typedDict;
                }
                
                return dict;
            }

            // Handle List/Array types
            if (typeof(IList).IsAssignableFrom(targetType))
            {
                var list = ParseJsonArray(json);
                
                // If it's a generic List<T>, convert items to the right type
                if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var itemType = targetType.GetGenericArguments()[0];
                    var typedList = Activator.CreateInstance(targetType) as IList;
                    
                    foreach (var item in list)
                    {
                        typedList.Add(ConvertToType(item, itemType));
                    }
                    return typedList;
                }
                
                return list;
            }

            // Handle complex objects - deserialize field by field
            if (!json.StartsWith("{") || !json.EndsWith("}"))
                return null;

            var instance = Activator.CreateInstance(targetType);
            var parsedDict = ParseJsonDictionary(json);

            // Get all fields (public instance fields)
            var fields = targetType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            
            foreach (var field in fields)
            {
                // Try to find matching key in parsed dictionary (case-insensitive)
                string matchingKey = null;
                foreach (var key in parsedDict.Keys)
                {
                    if (string.Equals(key, field.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        matchingKey = key;
                        break;
                    }
                }

                if (matchingKey != null && parsedDict.ContainsKey(matchingKey))
                {
                    var value = parsedDict[matchingKey];
                    var convertedValue = ConvertToType(value, field.FieldType);
                    field.SetValue(instance, convertedValue);
                }
            }

            return instance;
        }

        /// <summary>
        /// Convert a value to a specific type, handling nested objects and collections
        /// </summary>
        private static object ConvertToType(object value, Type targetType)
        {
            if (value == null)
                return null;

            // If types match, return as-is
            if (targetType.IsAssignableFrom(value.GetType()))
                return value;

            // Handle string representation of complex types
            if (value is string strValue)
            {
                strValue = strValue.Trim();
                
                // If it's a JSON object/array string, parse it
                if ((strValue.StartsWith("{") || strValue.StartsWith("[")))
                {
                    return ParseJsonToType(strValue, targetType);
                }
                
                return ConvertJsonValue(strValue, targetType);
            }

            // Handle Dictionary to complex object
            if (value is Dictionary<string, object> dict)
            {
                // If target is also Dictionary, handle type conversion
                if (typeof(IDictionary).IsAssignableFrom(targetType))
                {
                    if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                    {
                        var keyType = targetType.GetGenericArguments()[0];
                        var valueType = targetType.GetGenericArguments()[1];
                        var typedDict = Activator.CreateInstance(targetType) as IDictionary;
                        
                        foreach (var kvp in dict)
                        {
                            var key = Convert.ChangeType(kvp.Key, keyType);
                            var val = ConvertToType(kvp.Value, valueType);
                            typedDict[key] = val;
                        }
                        return typedDict;
                    }
                    return dict;
                }

                // Convert Dictionary to object with fields
                var instance = Activator.CreateInstance(targetType);
                var fields = targetType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                
                foreach (var field in fields)
                {
                    // Try to find matching key (case-insensitive)
                    string matchingKey = null;
                    foreach (var key in dict.Keys)
                    {
                        if (string.Equals(key, field.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            matchingKey = key;
                            break;
                        }
                    }

                    if (matchingKey != null && dict.ContainsKey(matchingKey))
                    {
                        var fieldValue = dict[matchingKey];
                        var convertedValue = ConvertToType(fieldValue, field.FieldType);
                        field.SetValue(instance, convertedValue);
                    }
                }
                return instance;
            }

            // Handle List to typed List
            if (value is List<object> list)
            {
                if (typeof(IList).IsAssignableFrom(targetType))
                {
                    if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        var itemType = targetType.GetGenericArguments()[0];
                        var typedList = Activator.CreateInstance(targetType) as IList;
                        
                        foreach (var item in list)
                        {
                            typedList.Add(ConvertToType(item, itemType));
                        }
                        return typedList;
                    }
                    return list;
                }
            }

            // Try direct conversion for primitives
            try
            {
                return Convert.ChangeType(value, targetType);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Parse JSON object/array string to Dictionary or List (legacy method kept for compatibility)
        /// </summary>
        private static object ParseJsonObjectLegacy(string json)
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
                    
                    // Parse the value appropriately
                    if (value == "null")
                    {
                        dict[key] = null;
                    }
                    else if (value.StartsWith("{"))
                    {
                        dict[key] = ParseJsonDictionary(value);
                    }
                    else if (value.StartsWith("["))
                    {
                        dict[key] = ParseJsonArray(value);
                    }
                    else if (value.StartsWith("\"") && value.EndsWith("\""))
                    {
                        dict[key] = value.Substring(1, value.Length - 2);
                    }
                    else if (value == "true" || value == "false")
                    {
                        dict[key] = value == "true";
                    }
                    else if (int.TryParse(value, out int intVal))
                    {
                        dict[key] = intVal;
                    }
                    else if (float.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out float floatVal))
                    {
                        dict[key] = floatVal;
                    }
                    else
                    {
                        dict[key] = value;
                    }
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
                var value = part.Trim();
                
                // Parse the value appropriately
                if (value == "null")
                {
                    list.Add(null);
                }
                else if (value.StartsWith("{"))
                {
                    list.Add(ParseJsonDictionary(value));
                }
                else if (value.StartsWith("["))
                {
                    list.Add(ParseJsonArray(value));
                }
                else if (value.StartsWith("\"") && value.EndsWith("\""))
                {
                    list.Add(value.Substring(1, value.Length - 2));
                }
                else if (value == "true" || value == "false")
                {
                    list.Add(value == "true");
                }
                else if (int.TryParse(value, out int intVal))
                {
                    list.Add(intVal);
                }
                else if (float.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out float floatVal))
                {
                    list.Add(floatVal);
                }
                else
                {
                    list.Add(value);
                }
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

