using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace GameModding.Shared.Serialization
{
    public static class ManualJson
    {
        public static T Deserialize<T>(string json) where T : class
        {
            var value = Parse(json);
            return ConvertToObject(typeof(T), value) as T;
        }

        public static object Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            return new Parser(json).ParseValue();
        }

        public static string Serialize(object value)
        {
            var builder = new StringBuilder();
            WriteValue(builder, value);
            return builder.ToString();
        }

        private static object ConvertToObject(Type targetType, object value)
        {
            if (targetType == null)
            {
                return null;
            }

            if (value == null)
            {
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }

            if (targetType == typeof(string))
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(int))
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(float))
            {
                return Convert.ToSingle(value, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(double))
            {
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(bool))
            {
                return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
            }

            if (targetType.IsArray)
            {
                var source = value as IList;
                if (source == null)
                {
                    return Array.CreateInstance(targetType.GetElementType(), 0);
                }

                var elementType = targetType.GetElementType();
                var array = Array.CreateInstance(elementType, source.Count);
                for (var i = 0; i < source.Count; i++)
                {
                    array.SetValue(ConvertToObject(elementType, source[i]), i);
                }

                return array;
            }

            var dictionary = value as IDictionary<string, object>;
            if (dictionary == null)
            {
                return null;
            }

            var instance = Activator.CreateInstance(targetType);
            var fields = targetType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            for (var i = 0; i < fields.Length; i++)
            {
                object fieldValue;
                if (!TryGetValue(dictionary, fields[i].Name, out fieldValue))
                {
                    continue;
                }

                fields[i].SetValue(instance, ConvertToObject(fields[i].FieldType, fieldValue));
            }

            return instance;
        }

        private static bool TryGetValue(IDictionary<string, object> dictionary, string key, out object value)
        {
            if (dictionary.TryGetValue(key, out value))
            {
                return true;
            }

            foreach (var pair in dictionary)
            {
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = pair.Value;
                    return true;
                }
            }

            value = null;
            return false;
        }

        private static void WriteValue(StringBuilder builder, object value)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            var type = value.GetType();
            if (type == typeof(string))
            {
                WriteString(builder, (string)value);
                return;
            }

            if (type == typeof(bool))
            {
                builder.Append((bool)value ? "true" : "false");
                return;
            }

            if (type.IsPrimitive || type == typeof(decimal) || type == typeof(double) || type == typeof(float))
            {
                builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }

            var enumerable = value as IEnumerable;
            if (!(value is string) && enumerable != null)
            {
                WriteArray(builder, enumerable);
                return;
            }

            WriteObject(builder, value);
        }

        private static void WriteArray(StringBuilder builder, IEnumerable values)
        {
            builder.Append("[");
            var first = true;
            foreach (var item in values)
            {
                if (!first)
                {
                    builder.Append(",");
                }

                WriteValue(builder, item);
                first = false;
            }
            builder.Append("]");
        }

        private static void WriteObject(StringBuilder builder, object value)
        {
            builder.Append("{");
            var fields = value.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            for (var i = 0; i < fields.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(",");
                }

                WriteString(builder, fields[i].Name);
                builder.Append(":");
                WriteValue(builder, fields[i].GetValue(value));
            }
            builder.Append("}");
        }

        private static void WriteString(StringBuilder builder, string value)
        {
            builder.Append("\"");
            for (var i = 0; i < (value ?? string.Empty).Length; i++)
            {
                var c = value[i];
                switch (c)
                {
                    case '\\': builder.Append("\\\\"); break;
                    case '"': builder.Append("\\\""); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default: builder.Append(c); break;
                }
            }
            builder.Append("\"");
        }

        private sealed class Parser
        {
            private readonly string _json;
            private int _index;

            public Parser(string json)
            {
                _json = json ?? string.Empty;
            }

            public object ParseValue()
            {
                SkipWhitespace();
                if (_index >= _json.Length)
                {
                    return null;
                }

                var current = _json[_index];
                switch (current)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't': return ParseLiteral("true", true);
                    case 'f': return ParseLiteral("false", false);
                    case 'n': return ParseLiteral("null", null);
                    default: return ParseNumber();
                }
            }

            private Dictionary<string, object> ParseObject()
            {
                var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                _index++;
                while (true)
                {
                    SkipWhitespace();
                    if (_index >= _json.Length)
                    {
                        return result;
                    }

                    if (_json[_index] == '}')
                    {
                        _index++;
                        return result;
                    }

                    var key = ParseString();
                    SkipWhitespace();
                    if (_index < _json.Length && _json[_index] == ':')
                    {
                        _index++;
                    }

                    var value = ParseValue();
                    result[key] = value;

                    SkipWhitespace();
                    if (_index < _json.Length && _json[_index] == ',')
                    {
                        _index++;
                    }
                }
            }

            private List<object> ParseArray()
            {
                var result = new List<object>();
                _index++;
                while (true)
                {
                    SkipWhitespace();
                    if (_index >= _json.Length)
                    {
                        return result;
                    }

                    if (_json[_index] == ']')
                    {
                        _index++;
                        return result;
                    }

                    result.Add(ParseValue());
                    SkipWhitespace();
                    if (_index < _json.Length && _json[_index] == ',')
                    {
                        _index++;
                    }
                }
            }

            private string ParseString()
            {
                var builder = new StringBuilder();
                if (_json[_index] == '"')
                {
                    _index++;
                }

                while (_index < _json.Length)
                {
                    var c = _json[_index++];
                    if (c == '"')
                    {
                        break;
                    }

                    if (c == '\\' && _index < _json.Length)
                    {
                        c = _json[_index++];
                        switch (c)
                        {
                            case '"': builder.Append('"'); break;
                            case '\\': builder.Append('\\'); break;
                            case '/': builder.Append('/'); break;
                            case 'b': builder.Append('\b'); break;
                            case 'f': builder.Append('\f'); break;
                            case 'n': builder.Append('\n'); break;
                            case 'r': builder.Append('\r'); break;
                            case 't': builder.Append('\t'); break;
                            case 'u':
                                if (_index + 4 <= _json.Length)
                                {
                                    builder.Append((char)int.Parse(_json.Substring(_index, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                                    _index += 4;
                                }
                                break;
                            default:
                                builder.Append(c);
                                break;
                        }
                    }
                    else
                    {
                        builder.Append(c);
                    }
                }

                return builder.ToString();
            }

            private object ParseNumber()
            {
                var start = _index;
                while (_index < _json.Length && "-+0123456789.eE".IndexOf(_json[_index]) >= 0)
                {
                    _index++;
                }

                var raw = _json.Substring(start, _index - start);
                if (raw.IndexOf('.') >= 0 || raw.IndexOf('e') >= 0 || raw.IndexOf('E') >= 0)
                {
                    double doubleValue;
                    return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out doubleValue) ? doubleValue : 0d;
                }

                int intValue;
                return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out intValue) ? intValue : 0;
            }

            private object ParseLiteral(string token, object value)
            {
                if (_index + token.Length <= _json.Length && string.Compare(_json, _index, token, 0, token.Length, StringComparison.Ordinal) == 0)
                {
                    _index += token.Length;
                }

                return value;
            }

            private void SkipWhitespace()
            {
                while (_index < _json.Length && char.IsWhiteSpace(_json[_index]))
                {
                    _index++;
                }
            }
        }
    }
}
