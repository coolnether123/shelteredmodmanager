using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ModAPI.Util
{
    public enum ManualJsonValueType
    {
        Null,
        Object,
        Array,
        String,
        Number,
        Boolean
    }

    public sealed class ManualJsonValue
    {
        private readonly ManualJsonValueType _type;
        private readonly object _value;

        private ManualJsonValue(ManualJsonValueType type, object value)
        {
            _type = type;
            _value = value;
        }

        public ManualJsonValueType Type { get { return _type; } }
        public ManualJsonObject ObjectValue { get { return _value as ManualJsonObject; } }
        public ManualJsonArray ArrayValue { get { return _value as ManualJsonArray; } }
        public string StringValue { get { return _value as string; } }
        public string NumberText { get { return _value as string; } }
        public bool BooleanValue { get { return _value is bool && (bool)_value; } }

        public static ManualJsonValue Null()
        {
            return new ManualJsonValue(ManualJsonValueType.Null, null);
        }

        public static ManualJsonValue Object(ManualJsonObject value)
        {
            return new ManualJsonValue(ManualJsonValueType.Object, value ?? new ManualJsonObject());
        }

        public static ManualJsonValue Array(ManualJsonArray value)
        {
            return new ManualJsonValue(ManualJsonValueType.Array, value ?? new ManualJsonArray());
        }

        public static ManualJsonValue String(string value)
        {
            return new ManualJsonValue(ManualJsonValueType.String, value ?? string.Empty);
        }

        public static ManualJsonValue Number(int value)
        {
            return Number(value.ToString(CultureInfo.InvariantCulture));
        }

        public static ManualJsonValue Number(long value)
        {
            return Number(value.ToString(CultureInfo.InvariantCulture));
        }

        public static ManualJsonValue Number(string value)
        {
            return new ManualJsonValue(ManualJsonValueType.Number, string.IsNullOrEmpty(value) ? "0" : value);
        }

        public static ManualJsonValue Boolean(bool value)
        {
            return new ManualJsonValue(ManualJsonValueType.Boolean, value);
        }
    }

    public sealed class ManualJsonObject
    {
        private readonly Dictionary<string, ManualJsonValue> _values = new Dictionary<string, ManualJsonValue>(StringComparer.Ordinal);
        private readonly List<string> _order = new List<string>();

        public IEnumerable<KeyValuePair<string, ManualJsonValue>> Properties
        {
            get
            {
                for (int i = 0; i < _order.Count; i++)
                {
                    string key = _order[i];
                    yield return new KeyValuePair<string, ManualJsonValue>(key, _values[key]);
                }
            }
        }

        public void Set(string name, ManualJsonValue value)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            if (!_values.ContainsKey(name))
            {
                _order.Add(name);
            }

            _values[name] = value ?? ManualJsonValue.Null();
        }

        public ManualJsonValue Get(string name)
        {
            ManualJsonValue value;
            return !string.IsNullOrEmpty(name) && _values.TryGetValue(name, out value) ? value : null;
        }

        public ManualJsonArray GetArray(string name)
        {
            ManualJsonValue value = Get(name);
            return value != null && value.Type == ManualJsonValueType.Array ? value.ArrayValue : null;
        }

        public ManualJsonObject GetObject(string name)
        {
            ManualJsonValue value = Get(name);
            return value != null && value.Type == ManualJsonValueType.Object ? value.ObjectValue : null;
        }

        public string GetString(string name, string fallback)
        {
            ManualJsonValue value = Get(name);
            return value != null && value.Type == ManualJsonValueType.String ? value.StringValue : fallback;
        }

        public int GetInt(string name, int fallback)
        {
            ManualJsonValue value = Get(name);
            if (value == null || value.Type != ManualJsonValueType.Number)
            {
                return fallback;
            }

            int parsed;
            return int.TryParse(value.NumberText, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
        }

        public bool GetBool(string name, bool fallback)
        {
            ManualJsonValue value = Get(name);
            return value != null && value.Type == ManualJsonValueType.Boolean ? value.BooleanValue : fallback;
        }
    }

    public sealed class ManualJsonArray
    {
        private readonly List<ManualJsonValue> _items = new List<ManualJsonValue>();

        public IList<ManualJsonValue> Items
        {
            get { return _items; }
        }

        public void Add(ManualJsonValue value)
        {
            _items.Add(value ?? ManualJsonValue.Null());
        }
    }

    public static class ManualJson
    {
        public static bool TryParseObject(string json, out ManualJsonObject value, out string error)
        {
            value = null;
            error = null;

            ManualJsonValue parsed;
            if (!TryParse(json, out parsed, out error))
            {
                return false;
            }

            if (parsed == null || parsed.Type != ManualJsonValueType.Object)
            {
                error = "JSON root was not an object.";
                return false;
            }

            value = parsed.ObjectValue;
            return true;
        }

        public static bool TryParse(string json, out ManualJsonValue value, out string error)
        {
            value = null;
            error = null;

            if (string.IsNullOrEmpty(json))
            {
                error = "JSON was empty.";
                return false;
            }

            JsonParser parser = new JsonParser(json);
            if (!parser.TryReadValue(out value, out error))
            {
                return false;
            }

            parser.SkipWhitespace();
            if (!parser.IsAtEnd)
            {
                error = "Unexpected trailing JSON content at index " + parser.Position.ToString(CultureInfo.InvariantCulture) + ".";
                return false;
            }

            return true;
        }

        public static string Serialize(ManualJsonObject value, bool indented)
        {
            return Serialize(ManualJsonValue.Object(value), indented);
        }

        public static string Serialize(ManualJsonValue value, bool indented)
        {
            StringBuilder builder = new StringBuilder();
            WriteValue(builder, value ?? ManualJsonValue.Null(), indented, 0);
            return builder.ToString();
        }

        public static string EscapeString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '\\': builder.Append("\\\\"); break;
                    case '"': builder.Append("\\\""); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < 32)
                        {
                            builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(c);
                        }
                        break;
                }
            }

            return builder.ToString();
        }

        public static object ToObjectGraph(ManualJsonValue value)
        {
            if (value == null)
            {
                return null;
            }

            switch (value.Type)
            {
                case ManualJsonValueType.Object:
                    Dictionary<string, object> obj = new Dictionary<string, object>(StringComparer.Ordinal);
                    foreach (KeyValuePair<string, ManualJsonValue> property in value.ObjectValue.Properties)
                    {
                        obj[property.Key] = ToObjectGraph(property.Value);
                    }

                    return obj;
                case ManualJsonValueType.Array:
                    List<object> array = new List<object>();
                    for (int i = 0; i < value.ArrayValue.Items.Count; i++)
                    {
                        array.Add(ToObjectGraph(value.ArrayValue.Items[i]));
                    }

                    return array;
                case ManualJsonValueType.String:
                    return value.StringValue;
                case ManualJsonValueType.Number:
                    return value.NumberText;
                case ManualJsonValueType.Boolean:
                    return value.BooleanValue;
                default:
                    return null;
            }
        }

        private static void WriteValue(StringBuilder builder, ManualJsonValue value, bool indented, int depth)
        {
            switch (value.Type)
            {
                case ManualJsonValueType.Object:
                    WriteObject(builder, value.ObjectValue, indented, depth);
                    break;
                case ManualJsonValueType.Array:
                    WriteArray(builder, value.ArrayValue, indented, depth);
                    break;
                case ManualJsonValueType.String:
                    builder.Append('"').Append(EscapeString(value.StringValue)).Append('"');
                    break;
                case ManualJsonValueType.Number:
                    builder.Append(string.IsNullOrEmpty(value.NumberText) ? "0" : value.NumberText);
                    break;
                case ManualJsonValueType.Boolean:
                    builder.Append(value.BooleanValue ? "true" : "false");
                    break;
                default:
                    builder.Append("null");
                    break;
            }
        }

        private static void WriteObject(StringBuilder builder, ManualJsonObject value, bool indented, int depth)
        {
            builder.Append('{');
            bool wrote = false;
            foreach (KeyValuePair<string, ManualJsonValue> property in value.Properties)
            {
                if (wrote)
                {
                    builder.Append(',');
                }

                WriteLineAndIndent(builder, indented, depth + 1);
                builder.Append('"').Append(EscapeString(property.Key)).Append('"');
                builder.Append(indented ? ": " : ":");
                WriteValue(builder, property.Value, indented, depth + 1);
                wrote = true;
            }

            if (wrote)
            {
                WriteLineAndIndent(builder, indented, depth);
            }

            builder.Append('}');
        }

        private static void WriteArray(StringBuilder builder, ManualJsonArray value, bool indented, int depth)
        {
            builder.Append('[');
            bool wrote = false;
            for (int i = 0; i < value.Items.Count; i++)
            {
                if (wrote)
                {
                    builder.Append(',');
                }

                WriteLineAndIndent(builder, indented, depth + 1);
                WriteValue(builder, value.Items[i], indented, depth + 1);
                wrote = true;
            }

            if (wrote)
            {
                WriteLineAndIndent(builder, indented, depth);
            }

            builder.Append(']');
        }

        private static void WriteLineAndIndent(StringBuilder builder, bool indented, int depth)
        {
            if (!indented)
            {
                return;
            }

            builder.AppendLine();
            builder.Append(' ', depth * 4);
        }

        private sealed class JsonParser
        {
            private readonly string _json;
            private int _position;

            public JsonParser(string json)
            {
                _json = json;
            }

            public int Position { get { return _position; } }
            public bool IsAtEnd { get { return _position >= _json.Length; } }

            public void SkipWhitespace()
            {
                while (_position < _json.Length && char.IsWhiteSpace(_json[_position]))
                {
                    _position++;
                }
            }

            public bool TryReadValue(out ManualJsonValue value, out string error)
            {
                value = null;
                error = null;
                SkipWhitespace();

                if (IsAtEnd)
                {
                    error = "Unexpected end of JSON.";
                    return false;
                }

                char c = _json[_position];
                if (c == '{') return TryReadObject(out value, out error);
                if (c == '[') return TryReadArray(out value, out error);
                if (c == '"')
                {
                    string text;
                    if (!TryReadString(out text, out error)) return false;
                    value = ManualJsonValue.String(text);
                    return true;
                }
                if (c == '-' || char.IsDigit(c)) return TryReadNumber(out value, out error);
                if (TryConsumeLiteral("true"))
                {
                    value = ManualJsonValue.Boolean(true);
                    return true;
                }
                if (TryConsumeLiteral("false"))
                {
                    value = ManualJsonValue.Boolean(false);
                    return true;
                }
                if (TryConsumeLiteral("null"))
                {
                    value = ManualJsonValue.Null();
                    return true;
                }

                error = "Unexpected JSON token at index " + _position.ToString(CultureInfo.InvariantCulture) + ".";
                return false;
            }

            private bool TryReadObject(out ManualJsonValue value, out string error)
            {
                value = null;
                error = null;
                ManualJsonObject obj = new ManualJsonObject();
                _position++;
                SkipWhitespace();

                if (TryConsume('}'))
                {
                    value = ManualJsonValue.Object(obj);
                    return true;
                }

                while (!IsAtEnd)
                {
                    SkipWhitespace();
                    string name;
                    if (!TryReadString(out name, out error)) return false;

                    SkipWhitespace();
                    if (!TryConsume(':'))
                    {
                        error = "Expected ':' after object property at index " + _position.ToString(CultureInfo.InvariantCulture) + ".";
                        return false;
                    }

                    ManualJsonValue propertyValue;
                    if (!TryReadValue(out propertyValue, out error)) return false;
                    obj.Set(name, propertyValue);

                    SkipWhitespace();
                    if (TryConsume('}'))
                    {
                        value = ManualJsonValue.Object(obj);
                        return true;
                    }

                    if (!TryConsume(','))
                    {
                        error = "Expected ',' or '}' at index " + _position.ToString(CultureInfo.InvariantCulture) + ".";
                        return false;
                    }
                }

                error = "Unterminated JSON object.";
                return false;
            }

            private bool TryReadArray(out ManualJsonValue value, out string error)
            {
                value = null;
                error = null;
                ManualJsonArray array = new ManualJsonArray();
                _position++;
                SkipWhitespace();

                if (TryConsume(']'))
                {
                    value = ManualJsonValue.Array(array);
                    return true;
                }

                while (!IsAtEnd)
                {
                    ManualJsonValue item;
                    if (!TryReadValue(out item, out error)) return false;
                    array.Add(item);

                    SkipWhitespace();
                    if (TryConsume(']'))
                    {
                        value = ManualJsonValue.Array(array);
                        return true;
                    }

                    if (!TryConsume(','))
                    {
                        error = "Expected ',' or ']' at index " + _position.ToString(CultureInfo.InvariantCulture) + ".";
                        return false;
                    }
                }

                error = "Unterminated JSON array.";
                return false;
            }

            private bool TryReadString(out string value, out string error)
            {
                value = string.Empty;
                error = null;

                if (!TryConsume('"'))
                {
                    error = "Expected string at index " + _position.ToString(CultureInfo.InvariantCulture) + ".";
                    return false;
                }

                StringBuilder builder = new StringBuilder();
                while (!IsAtEnd)
                {
                    char c = _json[_position++];
                    if (c == '"')
                    {
                        value = builder.ToString();
                        return true;
                    }

                    if (c != '\\')
                    {
                        builder.Append(c);
                        continue;
                    }

                    if (IsAtEnd)
                    {
                        error = "Unterminated JSON escape sequence.";
                        return false;
                    }

                    char escaped = _json[_position++];
                    switch (escaped)
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
                            if (!TryReadUnicodeEscape(builder, out error)) return false;
                            break;
                        default:
                            error = "Unsupported JSON escape sequence at index " + (_position - 1).ToString(CultureInfo.InvariantCulture) + ".";
                            return false;
                    }
                }

                error = "Unterminated JSON string.";
                return false;
            }

            private bool TryReadUnicodeEscape(StringBuilder builder, out string error)
            {
                error = null;
                if (_position + 4 > _json.Length)
                {
                    error = "Incomplete JSON unicode escape.";
                    return false;
                }

                string hex = _json.Substring(_position, 4);
                int code;
                if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                {
                    error = "Invalid JSON unicode escape.";
                    return false;
                }

                builder.Append((char)code);
                _position += 4;
                return true;
            }

            private bool TryReadNumber(out ManualJsonValue value, out string error)
            {
                value = null;
                error = null;
                int start = _position;

                if (_json[_position] == '-') _position++;
                while (_position < _json.Length && char.IsDigit(_json[_position])) _position++;
                if (_position < _json.Length && _json[_position] == '.')
                {
                    _position++;
                    while (_position < _json.Length && char.IsDigit(_json[_position])) _position++;
                }
                if (_position < _json.Length && (_json[_position] == 'e' || _json[_position] == 'E'))
                {
                    _position++;
                    if (_position < _json.Length && (_json[_position] == '+' || _json[_position] == '-')) _position++;
                    while (_position < _json.Length && char.IsDigit(_json[_position])) _position++;
                }

                if (_position == start || (_position == start + 1 && _json[start] == '-'))
                {
                    error = "Invalid JSON number at index " + start.ToString(CultureInfo.InvariantCulture) + ".";
                    return false;
                }

                value = ManualJsonValue.Number(_json.Substring(start, _position - start));
                return true;
            }

            private bool TryConsume(char expected)
            {
                if (_position >= _json.Length || _json[_position] != expected)
                {
                    return false;
                }

                _position++;
                return true;
            }

            private bool TryConsumeLiteral(string literal)
            {
                if (_position + literal.Length > _json.Length)
                {
                    return false;
                }

                if (string.CompareOrdinal(_json, _position, literal, 0, literal.Length) != 0)
                {
                    return false;
                }

                _position += literal.Length;
                return true;
            }
        }
    }
}
