using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using ModAPI.Core;

namespace ModAPI.Reflection
{
    /// <summary>
    /// Safer helpers around reflection and private access. Never throws.
    /// - Version-safe name resolution via multiple candidates ("a|b|c").
    /// - Type-safe out parameters that return false on errors.
    /// - Optional guard against calling methods returning value-types (struct returns).
    /// </summary>
    public static class Safe
    {
        private const BindingFlags AllInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags AllStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        // --- Field helpers --------------------------------------------------

        public static bool TryGetField<T>(object obj, string name, out T value)
        {
            return TryGetField<T>(obj, out value, SplitCandidates(name));
        }

        public static bool TryGetField<T>(object obj, out T value, params string[] candidateNames)
        {
            value = default(T);
            if (obj == null || candidateNames == null || candidateNames.Length == 0)
                return false;

            try
            {
                Type type;
                object instance;
                BindingFlags flags;
                ResolveTarget(obj, out type, out instance, out flags);

                for (int i = 0; i < candidateNames.Length; i++)
                {
                    var n = candidateNames[i];
                    if (string.IsNullOrEmpty(n)) continue;
                    
                    FieldInfo f = type.GetField(n, flags);
                    
                    // Auto-detection for backing fields if direct match fails
                    if (f == null) f = type.GetField("m_" + n, flags);
                    if (f == null) f = type.GetField("_" + n, flags);
                    
                    if (f == null)
                        continue;
                    object raw = f.GetValue(instance);
                    if (TryCast(raw, out value))
                        return true;
                }

                // Detailed failure reporting (one-time)
                var key = BuildOnceKey(type, "field-failure", candidateNames[0]);
                MMLog.LogOnce(key, () => LogDetailedFailure(type, "Field", candidateNames, flags));
            }
            catch (Exception ex)
            {
                MMLog.WriteDebug("Safe.TryGetField error: " + ex.Message);
            }

            return false;
        }

        public static T GetField<T>(object obj, string name)
        {
            T val;
            TryGetField(obj, name, out val);
            return val;
        }

        public static T GetFieldOrDefault<T>(object obj, string name, T defaultValue)
        {
            T v;
            if (TryGetField<T>(obj, name, out v))
                return v;
            return defaultValue;
        }

        public static bool SetField(object obj, string name, object value)
        {
            if (obj == null || string.IsNullOrEmpty(name)) return false;
            try
            {
                Type type; object instance; BindingFlags flags;
                ResolveTarget(obj, out type, out instance, out flags);
                
                var f = type.GetField(name, flags);
                if (f == null) f = type.GetField("m_" + name, flags);
                if (f == null) f = type.GetField("_" + name, flags);

                if (f != null)
                {
                    object valToSet = value;
                    // Sloppy cast for field setting: if types don't match, attempt conversion
                    if (value != null && !f.FieldType.IsAssignableFrom(value.GetType()))
                    {
                        if (!TryCastManual(value, f.FieldType, out valToSet))
                        {
                            try { valToSet = Convert.ChangeType(value, f.FieldType); } catch { }
                        }
                    }
                    
                    f.SetValue(instance, valToSet);
                    return true;
                }

                var key = BuildOnceKey(type, "setfield-failure", name);
                MMLog.LogOnce(key, () => LogDetailedFailure(type, "Field", new[] { name }, flags));
            }
            catch (Exception ex)
            {
                MMLog.WriteDebug("Safe.SetField error: " + ex.Message);
            }
            return false;
        }

        // --- Property helpers -----------------------------------------------

        public static bool TryGetProperty<T>(object obj, string name, out T value)
        {
            value = default(T);
            if (obj == null || string.IsNullOrEmpty(name)) return false;

            try
            {
                Type type; object instance; BindingFlags flags;
                ResolveTarget(obj, out type, out instance, out flags);

                var p = type.GetProperty(name, flags);
                if (p != null)
                {
                    object raw = p.GetValue(instance, null);
                    if (TryCast(raw, out value))
                        return true;
                }

                var key = BuildOnceKey(type, "property-failure", name);
                MMLog.LogOnce(key, () => LogDetailedFailure(type, "Property", new[] { name }, flags));
            }
            catch (Exception ex)
            {
                MMLog.WriteDebug("Safe.TryGetProperty error: " + ex.Message);
            }

            return false;
        }

        public static T GetProperty<T>(object obj, string name)
        {
            T val;
            TryGetProperty(obj, name, out val);
            return val;
        }

        public static bool SetProperty(object obj, string name, object value)
        {
            if (obj == null || string.IsNullOrEmpty(name)) return false;
            try
            {
                Type type; object instance; BindingFlags flags;
                ResolveTarget(obj, out type, out instance, out flags);
                var p = type.GetProperty(name, flags);
                if (p != null && p.CanWrite)
                {
                    object valToSet = value;
                    if (value != null && !p.PropertyType.IsAssignableFrom(value.GetType()))
                    {
                        if (!TryCastManual(value, p.PropertyType, out valToSet))
                        {
                            try { valToSet = Convert.ChangeType(value, p.PropertyType); } catch { }
                        }
                    }
                    p.SetValue(instance, valToSet, null);
                    return true;
                }

                var key = BuildOnceKey(type, "setproperty-failure", name);
                MMLog.LogOnce(key, () => LogDetailedFailure(type, "Property", new[] { name }, flags));
            }
            catch (Exception ex)
            {
                MMLog.WriteDebug("Safe.SetProperty error: " + ex.Message);
            }
            return false;
        }

        public static bool InvokeMethod(object obj, string methodName, params object[] args)
        {
            object dummy;
            return TryCall<object>(obj, methodName, out dummy, true, args);
        }

        // --- Method helpers -------------------------------------------------

        public static bool TryCall<T>(object obj, string methodName, out T result, params object[] args)
        {
            return TryCall<T>(obj, methodName, out result, false, args);
        }

        public static bool TryCall<T>(object obj, string methodName, out T result, bool dangerous, params object[] args)
        {
            return TryCall<T>(obj, out result, dangerous, SplitCandidates(methodName), args);
        }

        public static bool TryCall<T>(object obj, out T result, bool dangerous, string[] candidateNames, params object[] args)
        {
            result = default(T);
            if (obj == null || candidateNames == null || candidateNames.Length == 0)
                return false;

            try
            {
                Type type;
                object instance;
                BindingFlags flags;
                ResolveTarget(obj, out type, out instance, out flags);

                var argTypes = GetArgTypes(args);
                for (int i = 0; i < candidateNames.Length; i++)
                {
                    var n = candidateNames[i];
                    if (string.IsNullOrEmpty(n)) continue;
                    var m = FindMethod(type, n, flags, argTypes);
                    if (m == null) continue;

                    if (IsStructReturn(m) && !dangerous)
                    {
                        var key = BuildOnceKey(type, "call", n) + "#struct-return";
                        MMLog.WarnOnce(key, "Unsafe struct-return access blocked for " + type.FullName + "." + n);
                        return false;
                    }

                    object raw = m.Invoke(instance, NormalizeArgs(m, args));
                    if (TryCast(raw, out result))
                        return true;

                    // Allow null to cast to default for reference types
                    if (raw == null)
                    {
                        result = default(T);
                        return true;
                    }
                }

                var failureKey = BuildOnceKey(type, "call-failure", candidateNames[0]);
                MMLog.LogOnce(failureKey, delegate { LogDetailedFailure(type, "Method", candidateNames, flags); });
            }
            catch (Exception ex)
            {
                MMLog.WriteDebug("Safe.TryCall error: " + ex.Message);
            }

            return false;
        }

        public static bool IsStructReturn(MethodBase m)
        {
            var mi = m as MethodInfo;
            if (mi == null) return false;
            try { return mi.ReturnType != null && mi.ReturnType.IsValueType && !IsVoid(mi.ReturnType); }
            catch { return false; }
        }

        private static bool IsVoid(Type t)
        {
            try { return t == typeof(void); } catch { return false; }
        }

        // --- Internals ------------------------------------------------------

        private static void ResolveTarget(object obj, out Type type, out object instance, out BindingFlags flags)
        {
            var t = obj as Type;
            if (t != null)
            {
                type = t; instance = null; flags = AllStatic;
            }
            else
            {
                type = obj.GetType(); instance = obj; flags = AllInstance;
            }
        }

        private static bool TryCast<T>(object raw, out T value)
        {
            object output;
            if (TryCastManual(raw, typeof(T), out output))
            {
                value = (T)output;
                return true;
            }

            try
            {
                if (raw is T)
                {
                    value = (T)raw; return true;
                }
                if (raw == null)
                {
                    value = default(T); return typeof(T).IsClass || Nullable.GetUnderlyingType(typeof(T)) != null;
                }
                // Allow change-type for simple primitives/strings when possible
                var target = typeof(T);
                var underlying = Nullable.GetUnderlyingType(target) ?? target;
                if (underlying.IsEnum && raw is string)
                {
                    value = (T)Enum.Parse(underlying, (string)raw, true);
                    return true;
                }
                value = (T)System.Convert.ChangeType(raw, underlying);
                return true;
            }
            catch { value = default(T); return false; }
        }

        private static bool TryCastManual(object raw, Type targetType, out object result)
        {
            result = null;
            if (raw == null) return !targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null;

            Type sourceType = raw.GetType();
            if (targetType.IsAssignableFrom(sourceType))
            {
                result = raw; return true;
            }

            // Vector2 <-> Vector3 conversions
            if (sourceType == typeof(Vector2) && targetType == typeof(Vector3))
            {
                Vector2 v2 = (Vector2)raw;
                result = new Vector3(v2.x, v2.y, 0f); return true;
            }
            if (sourceType == typeof(Vector3) && targetType == typeof(Vector2))
            {
                Vector3 v3 = (Vector3)raw;
                result = new Vector2(v3.x, v3.y); return true;
            }

            return false;
        }

        private static string[] SplitCandidates(string input)
        {
            if (string.IsNullOrEmpty(input)) return new string[0];
            return input.IndexOf('|') >= 0 ? input.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries) : new[] { input };
        }

        private static MethodInfo FindMethod(Type type, string name, BindingFlags flags, Type[] argTypes)
        {
            // Try exact signature first
            if (argTypes != null)
            {
                var m = type.GetMethod(name, flags, null, argTypes, null);
                if (m != null) return m;
            }
            // Fallback: first by name
            try { return type.GetMethod(name, flags); } catch { return null; }
        }

        private static object[] NormalizeArgs(MethodInfo m, object[] args)
        {
            if (m == null) return args;
            var ps = m.GetParameters();
            if (ps == null || ps.Length == 0) return new object[0];
            return args ?? new object[0];
        }

        private static Type[] GetArgTypes(object[] args)
        {
            if (args == null || args.Length == 0) return Type.EmptyTypes;
            var list = new List<Type>(args.Length);
            for (int i = 0; i < args.Length; i++)
                list.Add(args[i] != null ? args[i].GetType() : typeof(object));
            return list.ToArray();
        }

        private static string SafeTypeName(object o)
        {
            try
            {
                if (o == null) return "<null>";
                var t = o as Type; if (t != null) return t.FullName;
                return o.GetType().FullName;
            }
            catch { return "<unknown>"; }
        }

        private static string BuildOnceKey(object obj, string kind, string name)
        {
            var typeName = SafeTypeName(obj);
            return "Safe:" + typeName + ":" + kind + ":" + (name ?? "");
        }

        private static void LogDetailedFailure(Type type, string memberKind, string[] candidates, BindingFlags flags)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(string.Format("{0} lookup failed for '{1}' on {2}", memberKind, string.Join("|", candidates), type.FullName));
            sb.AppendLine(string.Format("Flags: {0}", flags));
            
            if (memberKind == "Field")
            {
                sb.AppendLine("Available Fields:");
                foreach (var f in type.GetFields(flags)) sb.AppendLine(string.Format("  - {0} {1}", f.FieldType.Name, f.Name));
            }
            else if (memberKind == "Property")
            {
                sb.AppendLine("Available Properties:");
                foreach (var p in type.GetProperties(flags)) sb.AppendLine(string.Format("  - {0} {1} (R:{2} W:{3})", p.PropertyType.Name, p.Name, (p.CanRead ? "Y" : "N"), (p.CanWrite ? "Y" : "N")));
            }
            else if (memberKind == "Method")
            {
                sb.AppendLine("Available Methods:");
                foreach (var m in type.GetMethods(flags))
                {
                    var @params = m.GetParameters();
                    var pTypes = new string[@params.Length];
                    for (int i = 0; i < @params.Length; i++) pTypes[i] = @params[i].ParameterType.Name;
                    var pstr = string.Join(", ", pTypes);
                    sb.AppendLine(string.Format("  - {0} {1}({2})", m.ReturnType.Name, m.Name, pstr));
                }
            }
            
            MMLog.WriteDebug(sb.ToString());
        }
    }
}

