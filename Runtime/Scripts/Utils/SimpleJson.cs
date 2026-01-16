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

