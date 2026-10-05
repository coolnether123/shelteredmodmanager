using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Web.Script.Serialization;

namespace ShelteredModManager.ContentPacks
{
    public sealed class ContentPackSerializationResult
    {
        public bool Success;
        public ContentPackDocument Document;
        public string Json;
        public string ErrorMessage;
    }

    /// <summary>Bounded JSON serialization for untrusted downloaded content packs.</summary>
    public static class ContentPackJsonSerializer
    {
        public const int MaximumJsonLength = 1024 * 1024;
        public const int MaximumRecursionLimit = 32;

        public static ContentPackSerializationResult Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json))
                return Failed("Content-pack JSON is empty.");
            if (json.Length > MaximumJsonLength)
                return Failed("Content-pack JSON exceeds the 1 MiB limit.");

            try
            {
                JavaScriptSerializer serializer = CreateSerializer();
                ContentPackDocument document = serializer.Deserialize<ContentPackDocument>(json);
                if (document == null)
                    return Failed("Content-pack JSON did not contain a document.");

                return new ContentPackSerializationResult
                {
                    Success = true,
                    Document = document
                };
            }
            catch (Exception ex)
            {
                return Failed("Content-pack JSON is invalid: " + ex.Message);
            }
        }

        public static ContentPackSerializationResult Serialize(ContentPackDocument document)
        {
            if (document == null)
                return Failed("Content-pack document is required.");

            try
            {
                JavaScriptSerializer serializer = CreateSerializer();
                string json = StableJson.Serialize(serializer, document);
                if (json.Length > MaximumJsonLength)
                    return Failed("Serialized content-pack JSON exceeds the 1 MiB limit.");

                return new ContentPackSerializationResult
                {
                    Success = true,
                    Document = document,
                    Json = json
                };
            }
            catch (Exception ex)
            {
                return Failed("Content-pack serialization failed: " + ex.Message);
            }
        }

        private static JavaScriptSerializer CreateSerializer()
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = MaximumJsonLength;
            serializer.RecursionLimit = MaximumRecursionLimit;
            return serializer;
        }

        private static ContentPackSerializationResult Failed(string error)
        {
            return new ContentPackSerializationResult
            {
                Success = false,
                ErrorMessage = error
            };
        }
    }

    /// <summary>
    /// JavaScriptSerializer writes members in the order reflection returns
    /// them, and on .NET 3.5 that order can change within one run (after a
    /// type has been read by name). Objects are turned into dictionaries in
    /// declaration order first, so the same data always gives the same text.
    /// Output otherwise matches what the serializer writes for the object.
    /// </summary>
    internal static class StableJson
    {
        public static string Serialize(JavaScriptSerializer serializer, object value)
        {
            return serializer.Serialize(ToGraph(value, 0, serializer.RecursionLimit));
        }

        private static object ToGraph(object value, int depth, int limit)
        {
            if (value == null || IsScalar(value.GetType()))
                return value;
            // Guards against reference loops, which would otherwise recurse until the stack overflows.
            if (depth > limit)
                throw new ArgumentException("RecursionLimit exceeded.");

            IDictionary dictionary = value as IDictionary;
            if (dictionary != null)
            {
                Dictionary<string, object> entries = new Dictionary<string, object>();
                foreach (DictionaryEntry entry in dictionary)
                {
                    string key = entry.Key as string;
                    if (key == null)
                        return value; // Left to the serializer, which rejects non-string keys itself.
                    entries.Add(key, ToGraph(entry.Value, depth + 1, limit));
                }
                return entries;
            }

            IEnumerable sequence = value as IEnumerable;
            if (sequence != null)
            {
                List<object> items = new List<object>();
                foreach (object item in sequence)
                    items.Add(ToGraph(item, depth + 1, limit));
                return items;
            }

            // The serializer's own rules: public instance fields, then readable
            // non-indexed properties, each skipping [ScriptIgnore].
            Dictionary<string, object> members = new Dictionary<string, object>();
            foreach (FieldInfo field in Ordered(value.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)))
            {
                if (!field.IsDefined(typeof(ScriptIgnoreAttribute), true))
                    members[field.Name] = ToGraph(field.GetValue(value), depth + 1, limit);
            }
            foreach (PropertyInfo property in Ordered(value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)))
            {
                MethodInfo getter = property.GetGetMethod();
                if (getter == null || getter.GetParameters().Length > 0 || property.IsDefined(typeof(ScriptIgnoreAttribute), true))
                    continue;
                members[property.Name] = ToGraph(getter.Invoke(value, null), depth + 1, limit);
            }
            return members;
        }

        private static bool IsScalar(Type type)
        {
            return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal)
                || type == typeof(DateTime) || type == typeof(Guid) || type == typeof(Uri);
        }

        /// <summary>Declaration order: the most derived type's members first, each type in source order.</summary>
        private static List<T> Ordered<T>(T[] members) where T : MemberInfo
        {
            List<T> list = new List<T>(members);
            list.Sort(delegate(T a, T b)
            {
                int depth = Depth(b.DeclaringType).CompareTo(Depth(a.DeclaringType));
                return depth != 0 ? depth : a.MetadataToken.CompareTo(b.MetadataToken);
            });
            return list;
        }

        private static int Depth(Type type)
        {
            int depth = 0;
            for (Type t = type; t != null; t = t.BaseType)
                depth++;
            return depth;
        }
    }
}
