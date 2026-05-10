using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using ModAPI.Core;

namespace ShelteredAPI.Content
{
    internal static class ContentOwnerAssemblyResolver
    {
        private static readonly Assembly ShelteredApiAssembly = typeof(ContentOwnerAssemblyResolver).Assembly;
        private static readonly Assembly ModApiAssembly = typeof(ModRegistry).Assembly;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void EnsureOwner(ItemDefinition definition)
        {
            if (definition == null || definition.OwnerAssembly != null)
                return;

            definition.OwnerAssembly = ResolveCallingAssembly();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Assembly ResolveCallingAssembly()
        {
            try
            {
                StackTrace stackTrace = new StackTrace(false);

                for (int i = 1; i < stackTrace.FrameCount; i++)
                {
                    Assembly assembly = GetFrameAssembly(stackTrace, i);
                    if (assembly == null || IsFrameworkAssembly(assembly))
                        continue;

                    ModEntry entry;
                    if (ModRegistry.TryGetModByAssembly(assembly, out entry) && entry != null)
                        return assembly;
                }

                for (int i = 1; i < stackTrace.FrameCount; i++)
                {
                    Assembly assembly = GetFrameAssembly(stackTrace, i);
                    if (assembly != null && !IsFrameworkAssembly(assembly))
                        return assembly;
                }
            }
            catch
            {
            }

            return null;
        }

        public static string ResolveModId(ItemDefinition definition)
        {
            return ResolveModId(definition != null ? definition.OwnerAssembly : null);
        }

        public static string ResolveModId(Assembly assembly)
        {
            try
            {
                ModEntry entry;
                if (ModRegistry.TryGetModByAssembly(assembly, out entry)
                    && entry != null
                    && !string.IsNullOrEmpty(entry.Id))
                {
                    return entry.Id;
                }
            }
            catch
            {
            }

            try { return assembly != null ? assembly.GetName().Name : "mod"; }
            catch { return "mod"; }
        }

        public static string ResolveOwnerKey(Assembly assembly)
        {
            if (assembly == null)
                return "unknown";

            try
            {
                ModEntry entry;
                if (ModRegistry.TryGetModByAssembly(assembly, out entry)
                    && entry != null
                    && !string.IsNullOrEmpty(entry.Id))
                {
                    return entry.Id;
                }

                return assembly.GetName().Name;
            }
            catch
            {
                return "unknown";
            }
        }

        public static string ResolveAssetCacheOwnerKey(Assembly assembly)
        {
            if (assembly == null)
                return null;

            ModEntry entry;
            string modId = ModRegistry.TryGetModByAssembly(assembly, out entry) && entry != null
                ? entry.Id ?? entry.RootPath
                : null;

            return modId ?? SafeAssemblyName(assembly);
        }

        public static bool TryResolveModRoot(Assembly assembly, out string modRootPath)
        {
            modRootPath = null;
            if (assembly == null)
                return false;

            ModEntry entry;
            if (ModRegistry.TryGetModByAssembly(assembly, out entry)
                && entry != null
                && !string.IsNullOrEmpty(entry.RootPath))
            {
                modRootPath = entry.RootPath;
                return true;
            }

            return false;
        }

        public static string SafeAssemblyName(Assembly assembly)
        {
            try { return assembly != null ? assembly.GetName().Name : "unknown"; }
            catch { return "unknown"; }
        }

        private static Assembly GetFrameAssembly(StackTrace stackTrace, int index)
        {
            StackFrame frame = stackTrace.GetFrame(index);
            if (frame == null)
                return null;

            MethodBase method = frame.GetMethod();
            if (method == null || method.DeclaringType == null)
                return null;

            return method.DeclaringType.Assembly;
        }

        private static bool IsFrameworkAssembly(Assembly assembly)
        {
            if (assembly == null || assembly == ShelteredApiAssembly || assembly == ModApiAssembly)
                return true;

            string name;
            try { name = assembly.GetName().Name; }
            catch { return true; }

            if (string.IsNullOrEmpty(name))
                return true;

            return string.Equals(name, "mscorlib", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "System", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "System.Core", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "UnityEngine", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Assembly-CSharp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "0Harmony", StringComparison.OrdinalIgnoreCase);
        }
    }
}
