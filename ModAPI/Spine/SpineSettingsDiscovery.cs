using System;
using System.Collections.Generic;
using System.Reflection;

namespace ModAPI.Spine
{
    /// <summary>
    /// Shared reflection helpers for locating Spine settings declarations and settings holder objects.
    /// </summary>
    public static class SpineSettingsDiscovery
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags DeclaredInstanceFlags = InstanceFlags | BindingFlags.DeclaredOnly;

        public static bool HasSettings(object target)
        {
            return target != null && HasSettings(target.GetType());
        }

        public static bool HasSettings(Type type)
        {
            if (type == null)
                return false;

            try
            {
                foreach (FieldInfo field in type.GetFields(InstanceFlags))
                {
                    if (Attribute.IsDefined(field, typeof(ModSettingAttribute), true))
                        return true;
                }

                foreach (PropertyInfo property in type.GetProperties(InstanceFlags))
                {
                    if (Attribute.IsDefined(property, typeof(ModSettingAttribute), true))
                        return true;
                }

                foreach (MethodInfo method in type.GetMethods(InstanceFlags))
                {
                    if (Attribute.IsDefined(method, typeof(ModSettingAttribute), true))
                        return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        public static bool TryFindSettingsObject(object owner, out object settingsObject, out string sourceName)
        {
            settingsObject = null;
            sourceName = null;

            if (owner == null)
                return false;

            foreach (MemberInfo member in EnumerateCandidateMembers(owner.GetType()))
            {
                object value;
                if (!TryGetMemberValue(member, owner, out value))
                    continue;

                if (value == null || ReferenceEquals(value, owner))
                    continue;

                if (!HasSettings(value))
                    continue;

                settingsObject = value;
                sourceName = member.Name;
                return true;
            }

            return false;
        }

        private static IEnumerable<MemberInfo> EnumerateCandidateMembers(Type type)
        {
            List<MemberInfo> members = new List<MemberInfo>();
            Type current = type;

            while (current != null && current != typeof(object))
            {
                foreach (FieldInfo field in current.GetFields(DeclaredInstanceFlags))
                {
                    if (!field.IsStatic && IsCandidateMember(field))
                        members.Add(field);
                }

                foreach (PropertyInfo property in current.GetProperties(DeclaredInstanceFlags))
                {
                    if (property.GetIndexParameters().Length == 0 && property.CanRead && IsCandidateMember(property))
                        members.Add(property);
                }

                current = current.BaseType;
            }

            members.Sort(CompareCandidateMembers);
            return members;
        }

        private static bool IsCandidateMember(MemberInfo member)
        {
            if (GetNameRank(member.Name) < 2)
                return true;

            Type memberType = GetMemberType(member);
            return HasSettings(memberType);
        }

        private static Type GetMemberType(MemberInfo member)
        {
            FieldInfo field = member as FieldInfo;
            if (field != null)
                return field.FieldType;

            PropertyInfo property = member as PropertyInfo;
            if (property != null)
                return property.PropertyType;

            return null;
        }

        private static int CompareCandidateMembers(MemberInfo left, MemberInfo right)
        {
            int rankCompare = GetNameRank(left.Name).CompareTo(GetNameRank(right.Name));
            if (rankCompare != 0)
                return rankCompare;

            return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        }

        private static int GetNameRank(string name)
        {
            if (string.Equals(name, "Settings", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "Config", StringComparison.OrdinalIgnoreCase))
                return 0;

            if (name != null &&
                (name.IndexOf("Settings", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 name.IndexOf("Config", StringComparison.OrdinalIgnoreCase) >= 0))
                return 1;

            return 2;
        }

        private static bool TryGetMemberValue(MemberInfo member, object owner, out object value)
        {
            value = null;

            try
            {
                FieldInfo field = member as FieldInfo;
                if (field != null)
                {
                    value = field.GetValue(owner);
                    return true;
                }

                PropertyInfo property = member as PropertyInfo;
                if (property != null)
                {
                    value = property.GetValue(owner, null);
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }
    }
}
