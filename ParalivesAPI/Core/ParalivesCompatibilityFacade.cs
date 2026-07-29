using System;
using System.Collections.Generic;
using System.Reflection;
using ModAPI.Reflection;
using ParalivesAPI.Stable;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesCompatibilityFacade : IParalivesCompatibility
    {
        private const BindingFlags AllInstance =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;

        private const BindingFlags AllStatic =
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;

        public bool TryReadMember<T>(object source, string memberName, out T value)
        {
            return TryReadMember<T>(source, out value, memberName);
        }

        public bool TryReadMember<T>(object source, out T value, params string[] candidateNames)
        {
            value = default(T);
            string[] candidates = NormalizeCandidates(candidateNames);
            if (source == null || candidates.Length == 0)
                return false;

            for (int i = 0; i < candidates.Length; i++)
            {
                if (Safe.TryGetProperty<T>(source, candidates[i], out value))
                    return true;
            }

            return Safe.TryGetField<T>(source, out value, candidates);
        }

        public T ReadMemberOrDefault<T>(object source, string memberName, T defaultValue)
        {
            T value;
            return TryReadMember<T>(source, memberName, out value) ? value : defaultValue;
        }

        public bool TryReadGuid(object source, out ulong value, params string[] candidateNames)
        {
            value = 0UL;
            object raw;
            if (!TryReadMember<object>(source, out raw, candidateNames))
                return false;

            return TryConvertUInt64(raw, out value);
        }

        public ulong ReadGuidOrDefault(object source, ulong defaultValue, params string[] candidateNames)
        {
            ulong value;
            return TryReadGuid(source, out value, candidateNames) ? value : defaultValue;
        }

        public bool TryReadString(object source, out string value, params string[] candidateNames)
        {
            value = string.Empty;
            object raw;
            if (!TryReadMember<object>(source, out raw, candidateNames) || raw == null)
                return false;

            try
            {
                value = Convert.ToString(raw);
                return value != null;
            }
            catch
            {
                value = string.Empty;
                return false;
            }
        }

        public string ReadStringOrDefault(object source, string defaultValue, params string[] candidateNames)
        {
            string value;
            return TryReadString(source, out value, candidateNames) ? value : defaultValue;
        }

        public bool TrySetMember(object target, string memberName, object value)
        {
            return TrySetMember(target, value, memberName);
        }

        public bool TrySetMember(object target, object value, params string[] candidateNames)
        {
            string[] candidates = NormalizeCandidates(candidateNames);
            if (target == null || candidates.Length == 0)
                return false;

            for (int i = 0; i < candidates.Length; i++)
            {
                if (Safe.SetProperty(target, candidates[i], value))
                    return true;
            }

            for (int i = 0; i < candidates.Length; i++)
            {
                if (Safe.SetField(target, candidates[i], value))
                    return true;
            }

            return false;
        }

        public bool TryInvoke(object target, string methodName, params object[] args)
        {
            object ignored;
            return TryCallAllowValueTypeReturn<object>(target, methodName, out ignored, args);
        }

        public bool TryCall<T>(object target, string methodName, out T result, params object[] args)
        {
            return TryCall<T>(target, out result, NormalizeCandidates(methodName), args);
        }

        public bool TryCall<T>(object target, out T result, string[] candidateNames, params object[] args)
        {
            result = default(T);
            string[] candidates = NormalizeCandidates(candidateNames);
            if (target == null || candidates.Length == 0)
                return false;

            return Safe.TryCall<T>(target, out result, false, candidates, args);
        }

        public bool TryCallAllowValueTypeReturn<T>(object target, string methodName, out T result, params object[] args)
        {
            return TryCallAllowValueTypeReturn<T>(target, out result, NormalizeCandidates(methodName), args);
        }

        public bool TryCallAllowValueTypeReturn<T>(object target, out T result, string[] candidateNames, params object[] args)
        {
            result = default(T);
            string[] candidates = NormalizeCandidates(candidateNames);
            if (target == null || candidates.Length == 0)
                return false;

            return Safe.TryCall<T>(target, out result, true, candidates, args);
        }

        public ParalivesCompatibilityReport CheckMembers(object target, params string[] requiredMemberNames)
        {
            ParalivesCompatibilityReport report = new ParalivesCompatibilityReport();
            try
            {
                report.RequiredMembers = NormalizeRequiredMembers(requiredMemberNames);

                Type type;
                BindingFlags flags;
                if (!TryResolveType(target, out type, out flags))
                    return report;

                report.TargetExists = true;
                report.TargetTypeName = type.FullName ?? type.Name;

                List<string> found = new List<string>();
                List<string> missing = new List<string>();
                for (int i = 0; i < report.RequiredMembers.Length; i++)
                {
                    string required = report.RequiredMembers[i];
                    string[] candidates = NormalizeCandidates(required);
                    string matchedName;
                    if (TryFindMember(type, flags, candidates, out matchedName))
                        found.Add(matchedName);
                    else
                        missing.Add(required);
                }

                report.FoundMembers = found.ToArray();
                report.MissingMembers = missing.ToArray();
            }
            catch
            {
                report.FoundMembers = new string[0];
                report.MissingMembers = report.RequiredMembers;
            }

            return report;
        }

        private static bool TryResolveType(object target, out Type type, out BindingFlags flags)
        {
            type = null;
            flags = AllInstance;
            if (target == null)
                return false;

            Type targetType = target as Type;
            if (targetType != null)
            {
                type = targetType;
                flags = AllStatic;
                return true;
            }

            type = target.GetType();
            return true;
        }

        private static bool TryFindMember(Type type, BindingFlags flags, string[] candidates, out string matchedName)
        {
            matchedName = string.Empty;
            if (type == null || candidates == null)
                return false;

            for (int i = 0; i < candidates.Length; i++)
            {
                string name = candidates[i];
                if (string.IsNullOrEmpty(name))
                    continue;

                try
                {
                    if (type.GetField(name, flags) != null)
                    {
                        matchedName = name;
                        return true;
                    }
                }
                catch
                {
                }

                try
                {
                    if (type.GetProperty(name, flags) != null)
                    {
                        matchedName = name;
                        return true;
                    }
                }
                catch
                {
                }

                if (FindMethod(type, flags, name) != null)
                {
                    matchedName = name;
                    return true;
                }
            }

            return false;
        }

        private static MethodInfo FindMethod(Type type, BindingFlags flags, string name)
        {
            try
            {
                return type.GetMethod(name, flags);
            }
            catch
            {
                try
                {
                    MethodInfo[] methods = type.GetMethods(flags);
                    for (int i = 0; i < methods.Length; i++)
                    {
                        if (string.Equals(methods[i].Name, name, StringComparison.OrdinalIgnoreCase))
                            return methods[i];
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private static bool TryConvertUInt64(object raw, out ulong value)
        {
            value = 0UL;
            if (raw == null)
                return false;

            try
            {
                value = Convert.ToUInt64(raw);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string[] NormalizeRequiredMembers(string[] values)
        {
            if (values == null || values.Length == 0)
                return new string[0];

            List<string> result = new List<string>();
            for (int i = 0; i < values.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(values[i]))
                    continue;

                result.Add(values[i].Trim());
            }

            return result.ToArray();
        }

        private static string[] NormalizeCandidates(string value)
        {
            return NormalizeCandidates(new[] { value });
        }

        private static string[] NormalizeCandidates(string[] values)
        {
            if (values == null || values.Length == 0)
                return new string[0];

            List<string> result = new List<string>();
            for (int i = 0; i < values.Length; i++)
            {
                string value = values[i];
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                string[] parts = value.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                for (int j = 0; j < parts.Length; j++)
                {
                    string part = parts[j] == null ? string.Empty : parts[j].Trim();
                    if (part.Length > 0)
                        result.Add(part);
                }
            }

            return result.ToArray();
        }
    }
}
