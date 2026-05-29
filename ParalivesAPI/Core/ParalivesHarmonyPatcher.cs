using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;

namespace ParalivesAPI.Core
{
    internal static class ParalivesHarmonyPatcher
    {
        private const string HarmonyId = "ParalivesAPI.Core";

        private static readonly object Sync = new object();
        private static bool _patched;

        public static void EnsurePatched()
        {
            lock (Sync)
            {
                if (_patched)
                    return;

                _patched = true;
            }

            try
            {
                Harmony harmony = new Harmony(HarmonyId);
                Assembly assembly = typeof(ParalivesHarmonyPatcher).Assembly;
                Type[] types = GetLoadableTypes(assembly);
                HarmonyUtil.PatchOptions options = new HarmonyUtil.PatchOptions
                {
                    OnResult = delegate(object target, string result)
                    {
                        if (!string.IsNullOrEmpty(result))
                            MMLog.WriteDebug("[ParalivesAPI] " + DescribeTarget(target) + " -> " + result);
                    }
                };

                for (int i = 0; i < types.Length; i++)
                {
                    Type type = types[i];
                    if (type == null || !HarmonyUtil.HasHarmonyPatchAttributes(type) || IsPatchTypeAlreadyApplied(type))
                        continue;

                    HarmonyUtil.PatchType(harmony, type, options);
                }
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ParalivesHarmonyPatcher.EnsurePatched", "Failed to apply ParalivesAPI Harmony patches: " + ex.Message);
            }
        }

        private static Type[] GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                List<Type> types = new List<Type>();
                for (int i = 0; i < ex.Types.Length; i++)
                {
                    if (ex.Types[i] != null)
                        types.Add(ex.Types[i]);
                }
                return types.ToArray();
            }
        }

        private static bool IsPatchTypeAlreadyApplied(Type patchType)
        {
            IEnumerable<MethodBase> patchedMethods;
            try
            {
                patchedMethods = Harmony.GetAllPatchedMethods();
            }
            catch
            {
                return false;
            }

            foreach (MethodBase method in patchedMethods)
            {
                HarmonyLib.Patches patches = Harmony.GetPatchInfo(method);
                if (patches == null)
                    continue;

                if (ContainsPatchFromType(patches.Prefixes, patchType)
                    || ContainsPatchFromType(patches.Postfixes, patchType)
                    || ContainsPatchFromType(patches.Transpilers, patchType)
                    || ContainsPatchFromType(patches.Finalizers, patchType))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsPatchFromType(IEnumerable<Patch> patches, Type patchType)
        {
            foreach (Patch patch in patches)
            {
                if (patch != null
                    && patch.PatchMethod != null
                    && patch.PatchMethod.DeclaringType == patchType)
                {
                    return true;
                }
            }

            return false;
        }

        private static string DescribeTarget(object target)
        {
            MemberInfo member = target as MemberInfo;
            if (member == null)
                return target != null ? target.ToString() : "<null>";

            return member.DeclaringType != null
                ? member.DeclaringType.FullName + "." + member.Name
                : member.Name;
        }
    }
}
