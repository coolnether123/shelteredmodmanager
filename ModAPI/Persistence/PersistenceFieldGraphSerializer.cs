using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using ModAPI.Util;

namespace ModAPI.Persistence
{
    /// <summary>
    /// Serializes the public-field data contract used by RegisterModData.
    /// Unity's JsonUtility cannot reliably cross dynamic mod assembly boundaries for
    /// collection fields, so persistence owns this small deterministic field-graph codec.
    /// </summary>
    internal static class PersistenceFieldGraphSerializer
    {
        private const int MaximumDepth = 64;

        internal static string Serialize(object value)
        {
            HashSet<object> ancestors = new HashSet<object>(ReferenceEqualityComparer.Instance);
            return ManualJson.Serialize(SerializeValue(value, ancestors, 0), false);
        }

        internal static void DeserializeOverwrite(string json, object target)
        {
            if (target == null)
                throw new ArgumentNullException("target");

            ManualJsonValue root;
            string error;
            if (!ManualJson.TryParse(json, out root, out error))
                throw new FormatException("Could not parse persistence data: " + error);

            DeserializeValue(root, target.GetType(), target, 0);
        }

        private static ManualJsonValue SerializeValue(object value, HashSet<object> ancestors, int depth)
        {
            EnsureDepth(depth);
            if (value == null)
                return ManualJsonValue.Null();

            Type type = value.GetType();
            Type nullableType = Nullable.GetUnderlyingType(type);
            if (nullableType != null)
                type = nullableType;

            if (type.IsEnum)
                return ManualJsonValue.String(value.ToString());
            if (type == typeof(string) || type == typeof(char) || type == typeof(Guid))
                return ManualJsonValue.String(Convert.ToString(value, CultureInfo.InvariantCulture));
            if (type == typeof(DateTime))
                return ManualJsonValue.String(((DateTime)value).ToString("o", CultureInfo.InvariantCulture));
            if (type == typeof(TimeSpan))
                return ManualJsonValue.Number(((TimeSpan)value).Ticks.ToString(CultureInfo.InvariantCulture));
            if (type == typeof(bool))
                return ManualJsonValue.Boolean((bool)value);
            if (IsNumber(type))
                return ManualJsonValue.Number(FormatNumber(value, type));

            bool trackReference = !type.IsValueType;
            if (trackReference && !ancestors.Add(value))
                throw new InvalidOperationException("Persistence data contains a reference cycle at type " + type.FullName + ".");

            try
            {
                IDictionary dictionary = value as IDictionary;
                if (dictionary != null)
                    return SerializeDictionary(dictionary, ancestors, depth + 1);

                IEnumerable enumerable = value as IEnumerable;
                if (enumerable != null)
                    return SerializeCollection(enumerable, ancestors, depth + 1);

                ManualJsonObject result = new ManualJsonObject();
                FieldInfo[] fields = GetSerializableFields(type);
                for (int i = 0; i < fields.Length; i++)
                {
                    FieldInfo field = fields[i];
                    result.Set(field.Name, SerializeValue(field.GetValue(value), ancestors, depth + 1));
                }

                return ManualJsonValue.Object(result);
            }
            finally
            {
                if (trackReference)
                    ancestors.Remove(value);
            }
        }

        private static ManualJsonValue SerializeDictionary(IDictionary dictionary, HashSet<object> ancestors, int depth)
        {
            List<SerializedDictionaryEntry> entries = new List<SerializedDictionaryEntry>();
            foreach (DictionaryEntry entry in dictionary)
            {
                ManualJsonValue key = SerializeValue(entry.Key, ancestors, depth);
                ManualJsonValue value = SerializeValue(entry.Value, ancestors, depth);
                entries.Add(new SerializedDictionaryEntry(key, value));
            }

            entries.Sort(delegate(SerializedDictionaryEntry left, SerializedDictionaryEntry right)
            {
                int keyComparison = string.CompareOrdinal(left.SortKey, right.SortKey);
                return keyComparison != 0 ? keyComparison : string.CompareOrdinal(left.SortValue, right.SortValue);
            });

            ManualJsonArray array = new ManualJsonArray();
            for (int i = 0; i < entries.Count; i++)
            {
                ManualJsonObject item = new ManualJsonObject();
                item.Set("key", entries[i].Key);
                item.Set("value", entries[i].Value);
                array.Add(ManualJsonValue.Object(item));
            }

            return ManualJsonValue.Array(array);
        }

        private static ManualJsonValue SerializeCollection(IEnumerable collection, HashSet<object> ancestors, int depth)
        {
            ManualJsonArray array = new ManualJsonArray();
            foreach (object item in collection)
                array.Add(SerializeValue(item, ancestors, depth));
            return ManualJsonValue.Array(array);
        }

        private static object DeserializeValue(ManualJsonValue value, Type targetType, object existing, int depth)
        {
            EnsureDepth(depth);

            Type nullableType = Nullable.GetUnderlyingType(targetType);
            Type effectiveType = nullableType ?? targetType;
            if (value == null || value.Type == ManualJsonValueType.Null)
            {
                if (!targetType.IsValueType || nullableType != null)
                    return null;
                return Activator.CreateInstance(targetType);
            }

            if (effectiveType == typeof(object))
                return ManualJson.ToObjectGraph(value);
            if (effectiveType.IsEnum)
                return ReadEnum(value, effectiveType);
            if (effectiveType == typeof(string))
                return ReadString(value);
            if (effectiveType == typeof(char))
            {
                string text = ReadString(value);
                return string.IsNullOrEmpty(text) ? '\0' : text[0];
            }
            if (effectiveType == typeof(Guid))
                return new Guid(ReadString(value));
            if (effectiveType == typeof(DateTime))
                return DateTime.Parse(ReadString(value), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            if (effectiveType == typeof(TimeSpan))
                return new TimeSpan(Convert.ToInt64(ReadNumberText(value), CultureInfo.InvariantCulture));
            if (effectiveType == typeof(bool))
                return ReadBoolean(value);
            if (IsNumber(effectiveType))
                return Convert.ChangeType(ReadNumberText(value), effectiveType, CultureInfo.InvariantCulture);

            if (typeof(IDictionary).IsAssignableFrom(effectiveType))
                return DeserializeDictionary(value, effectiveType, existing, depth + 1);
            if (effectiveType.IsArray)
                return DeserializeArray(value, effectiveType, depth + 1);

            Type elementType;
            if (TryGetCollectionElementType(effectiveType, out elementType))
                return DeserializeCollection(value, effectiveType, elementType, existing, depth + 1);

            if (value.Type != ManualJsonValueType.Object)
                throw TypeMismatch(value, effectiveType);

            object result = existing ?? Activator.CreateInstance(effectiveType);
            ManualJsonObject source = value.ObjectValue;
            FieldInfo[] fields = GetSerializableFields(effectiveType);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                ManualJsonValue fieldValue = source.Get(field.Name);
                if (fieldValue == null)
                    continue;

                object current = field.GetValue(result);
                object restored = DeserializeValue(fieldValue, field.FieldType, current, depth + 1);
                if (field.IsInitOnly)
                {
                    if (!ReferenceEquals(current, restored) && !object.Equals(current, restored))
                        throw new InvalidOperationException("Cannot replace readonly persistence field " + effectiveType.FullName + "." + field.Name + ".");
                }
                else
                {
                    field.SetValue(result, restored);
                }
            }

            return result;
        }

        private static object DeserializeDictionary(ManualJsonValue value, Type dictionaryType, object existing, int depth)
        {
            if (value.Type != ManualJsonValueType.Array)
                throw TypeMismatch(value, dictionaryType);

            Type[] arguments = GetDictionaryArguments(dictionaryType);
            IDictionary result = existing as IDictionary;
            if (result == null)
            {
                Type concreteType = dictionaryType;
                if (dictionaryType.IsInterface || dictionaryType.IsAbstract)
                    concreteType = typeof(Dictionary<,>).MakeGenericType(arguments);
                result = (IDictionary)Activator.CreateInstance(concreteType);
            }
            else
            {
                result.Clear();
            }

            for (int i = 0; i < value.ArrayValue.Items.Count; i++)
            {
                ManualJsonValue entryValue = value.ArrayValue.Items[i];
                ManualJsonObject entry = entryValue != null && entryValue.Type == ManualJsonValueType.Object
                    ? entryValue.ObjectValue
                    : null;
                if (entry == null || entry.Get("key") == null || entry.Get("value") == null)
                    throw new FormatException("Persistence dictionary entry " + i.ToString(CultureInfo.InvariantCulture) + " is invalid.");

                object key = DeserializeValue(entry.Get("key"), arguments[0], null, depth);
                object item = DeserializeValue(entry.Get("value"), arguments[1], null, depth);
                result.Add(key, item);
            }

            return result;
        }

        private static object DeserializeArray(ManualJsonValue value, Type arrayType, int depth)
        {
            if (value.Type != ManualJsonValueType.Array)
                throw TypeMismatch(value, arrayType);

            Type elementType = arrayType.GetElementType();
            Array result = Array.CreateInstance(elementType, value.ArrayValue.Items.Count);
            for (int i = 0; i < result.Length; i++)
                result.SetValue(DeserializeValue(value.ArrayValue.Items[i], elementType, null, depth), i);
            return result;
        }

        private static object DeserializeCollection(ManualJsonValue value, Type collectionType, Type elementType, object existing, int depth)
        {
            if (value.Type != ManualJsonValueType.Array)
                throw TypeMismatch(value, collectionType);

            object result = existing;
            if (result == null)
            {
                Type concreteType = collectionType;
                if (collectionType.IsInterface || collectionType.IsAbstract)
                    concreteType = typeof(List<>).MakeGenericType(elementType);
                result = Activator.CreateInstance(concreteType);
            }

            MethodInfo clear = FindPublicMethod(result.GetType(), "Clear", Type.EmptyTypes);
            MethodInfo add = FindPublicMethod(result.GetType(), "Add", new Type[] { elementType });
            if (clear == null || add == null)
                throw new InvalidOperationException("Persistence collection type " + result.GetType().FullName + " must expose Clear() and Add(" + elementType.FullName + ").");

            clear.Invoke(result, null);
            for (int i = 0; i < value.ArrayValue.Items.Count; i++)
            {
                object item = DeserializeValue(value.ArrayValue.Items[i], elementType, null, depth);
                add.Invoke(result, new object[] { item });
            }

            return result;
        }

        private static FieldInfo[] GetSerializableFields(Type type)
        {
            FieldInfo[] all = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
            List<FieldInfo> fields = new List<FieldInfo>();
            for (int i = 0; i < all.Length; i++)
            {
                if (!all[i].IsStatic && !all[i].IsNotSerialized)
                    fields.Add(all[i]);
            }

            fields.Sort(delegate(FieldInfo left, FieldInfo right)
            {
                int nameComparison = string.CompareOrdinal(left.Name, right.Name);
                if (nameComparison != 0)
                    return nameComparison;
                return string.CompareOrdinal(left.DeclaringType.FullName, right.DeclaringType.FullName);
            });

            for (int i = 1; i < fields.Count; i++)
            {
                if (string.Equals(fields[i - 1].Name, fields[i].Name, StringComparison.Ordinal))
                    throw new InvalidOperationException("Persistence type " + type.FullName + " contains duplicate public field name " + fields[i].Name + ".");
            }

            return fields.ToArray();
        }

        private static Type[] GetDictionaryArguments(Type dictionaryType)
        {
            Type candidate = FindGenericInterface(dictionaryType, typeof(IDictionary<,>));
            if (candidate == null)
                throw new InvalidOperationException("Persistence dictionaries must implement IDictionary<TKey, TValue>: " + dictionaryType.FullName + ".");
            return candidate.GetGenericArguments();
        }

        private static bool TryGetCollectionElementType(Type collectionType, out Type elementType)
        {
            Type candidate = FindGenericInterface(collectionType, typeof(ICollection<>));
            if (candidate != null)
            {
                elementType = candidate.GetGenericArguments()[0];
                return true;
            }

            elementType = null;
            return false;
        }

        private static Type FindGenericInterface(Type type, Type genericDefinition)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == genericDefinition)
                return type;

            Type[] interfaces = type.GetInterfaces();
            for (int i = 0; i < interfaces.Length; i++)
            {
                if (interfaces[i].IsGenericType && interfaces[i].GetGenericTypeDefinition() == genericDefinition)
                    return interfaces[i];
            }

            return null;
        }

        private static MethodInfo FindPublicMethod(Type type, string name, Type[] parameters)
        {
            return type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public, null, parameters, null);
        }

        private static object ReadEnum(ManualJsonValue value, Type enumType)
        {
            if (value.Type == ManualJsonValueType.String)
                return Enum.Parse(enumType, value.StringValue, false);
            if (value.Type == ManualJsonValueType.Number)
                return Enum.ToObject(enumType, Convert.ChangeType(value.NumberText, Enum.GetUnderlyingType(enumType), CultureInfo.InvariantCulture));
            throw TypeMismatch(value, enumType);
        }

        private static string ReadString(ManualJsonValue value)
        {
            if (value.Type != ManualJsonValueType.String)
                throw TypeMismatch(value, typeof(string));
            return value.StringValue;
        }

        private static string ReadNumberText(ManualJsonValue value)
        {
            if (value.Type != ManualJsonValueType.Number)
                throw TypeMismatch(value, typeof(decimal));
            return value.NumberText;
        }

        private static bool ReadBoolean(ManualJsonValue value)
        {
            if (value.Type != ManualJsonValueType.Boolean)
                throw TypeMismatch(value, typeof(bool));
            return value.BooleanValue;
        }

        private static bool IsNumber(Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    return true;
                default:
                    return false;
            }
        }

        private static string FormatNumber(object value, Type type)
        {
            if (type == typeof(float))
            {
                float number = (float)value;
                if (float.IsNaN(number) || float.IsInfinity(number))
                    throw new InvalidOperationException("Persistence does not support non-finite floating-point values.");
                return number.ToString("R", CultureInfo.InvariantCulture);
            }
            if (type == typeof(double))
            {
                double number = (double)value;
                if (double.IsNaN(number) || double.IsInfinity(number))
                    throw new InvalidOperationException("Persistence does not support non-finite floating-point values.");
                return number.ToString("R", CultureInfo.InvariantCulture);
            }
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static Exception TypeMismatch(ManualJsonValue value, Type targetType)
        {
            return new FormatException("Cannot restore JSON " + value.Type + " as " + targetType.FullName + ".");
        }

        private static void EnsureDepth(int depth)
        {
            if (depth > MaximumDepth)
                throw new InvalidOperationException("Persistence data exceeded the maximum supported nesting depth of " + MaximumDepth + ".");
        }

        private sealed class SerializedDictionaryEntry
        {
            internal SerializedDictionaryEntry(ManualJsonValue key, ManualJsonValue value)
            {
                Key = key;
                Value = value;
                SortKey = ManualJson.Serialize(key, false);
                SortValue = ManualJson.Serialize(value, false);
            }

            internal readonly ManualJsonValue Key;
            internal readonly ManualJsonValue Value;
            internal readonly string SortKey;
            internal readonly string SortValue;
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            public new bool Equals(object left, object right)
            {
                return ReferenceEquals(left, right);
            }

            public int GetHashCode(object value)
            {
                return RuntimeHelpers.GetHashCode(value);
            }
        }
    }
}
