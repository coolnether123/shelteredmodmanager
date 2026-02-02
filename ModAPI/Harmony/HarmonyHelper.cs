using System;
using System.Reflection;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Reflection;
using System.Linq;
using System.Collections.Generic;

namespace ModAPI.Harmony
{
    /// <summary>
    /// Provides safe, high-level utilities for Harmony patching and reflection.
    /// Reduces boilerplate and ensures mods don't crash the game due to missing methods.
    /// </summary>
    public static class HarmonyHelper
    {
        private static readonly HarmonyLib.Harmony _harmony = new HarmonyLib.Harmony("com.modapi.shared");

        /// <summary>
        /// Adds a Prefix patch to a method.
        /// </summary>
        /// <typeparam name="T">The target class containing the method to patch.</typeparam>
        /// <param name="methodName">The name of the method to patch.</param>
        /// <param name="patchType">The class containing the patch method.</param>
        /// <param name="patchMethodName">The name of the prefix method.</param>
        public static void AddPrefix<T>(string methodName, Type patchType, string patchMethodName)
        {
            var prefix = new HarmonyMethod(AccessTools.Method(patchType, patchMethodName));
            TryPatchMethod(typeof(T), methodName, prefix: prefix);
        }

        /// <summary>
        /// Adds a Postfix patch to a method.
        /// </summary>
        /// <typeparam name="T">The target class containing the method to patch.</typeparam>
        /// <param name="methodName">The name of the method to patch.</param>
        /// <param name="patchType">The class containing the patch method.</param>
        /// <param name="patchMethodName">The name of the postfix method.</param>
        public static void AddPostfix<T>(string methodName, Type patchType, string patchMethodName)
        {
            var postfix = new HarmonyMethod(AccessTools.Method(patchType, patchMethodName));
            TryPatchMethod(typeof(T), methodName, postfix: postfix);
        }

        /// <summary>
        /// Attempts to patch a method with provided Harmony methods. 
        /// Logs errors if the target method is not found.
        /// </summary>
        public static bool TryPatchMethod(Type targetType, string methodName, 
            HarmonyMethod prefix = null, HarmonyMethod postfix = null, 
            HarmonyMethod transpiler = null)
        {
            try
            {
                if (targetType == null)
                {
                    MMLog.WriteError("[HarmonyHelper] Target type is null.");
                    return false;
                }

                var original = AccessTools.Method(targetType, methodName);
                if (original == null)
                {
                    MMLog.WriteError($"[HarmonyHelper] Target method '{methodName}' not found on type '{targetType.FullName}'.");
                    return false;
                }

                _harmony.Patch(original, prefix, postfix, transpiler);
                
                string patchType = prefix != null ? "Prefix" : (postfix != null ? "Postfix" : "Transpiler");
                MMLog.Write($"[HarmonyHelper] Success: Applied {patchType} to {targetType.Name}.{methodName}");
                return true;
            }
            catch (Exception ex)
            {
                MMLog.WriteError($"[HarmonyHelper] Critical error patching {targetType?.Name}.{methodName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Safely invokes a method via reflection, returning the result or default(T) on failure.
        /// Wraps ModAPI.Reflection.Safe.TryCall.
        /// </summary>
        public static T SafeInvoke<T>(object instance, string methodName, params object[] args)
        {
            T result;
            if (Safe.TryCall(instance, methodName, out result, args))
            {
                return result;
            }
            return default(T);
        }

        /// <summary>
        /// Patch ALL overloads of a method by name.
        /// Useful for methods like Foo(), Foo(int), Foo(string), etc.
        /// </summary>
        /// <param name="harmony">The Harmony instance to use for patching.</param>
        /// <param name="type">Target type containing the method.</param>
        /// <param name="methodName">Name of the method to patch.</param>
        /// <param name="parameterTypes">Optional filter: ONLY patch overloads matching these param types. Use null for no filter (all overloads).</param>
        /// <param name="ignoreGenerics">If true, skip generic methods. Default: false.</param>
        /// <param name="prefix">Prefix patch.</param>
        /// <param name="postfix">Postfix patch.</param>
        /// <param name="transpiler">Transpiler patch.</param>
        /// <remarks>
        /// Only patches methods declared on the specified type.
        /// Does NOT patch inherited methods or base class overloads.
        /// </remarks>
        public static void PatchAllOverloads(
            HarmonyLib.Harmony harmony,
            Type type,
            string methodName,
            Type[] parameterTypes = null,
            bool ignoreGenerics = false,
            HarmonyMethod prefix = null,
            HarmonyMethod postfix = null,
            HarmonyMethod transpiler = null)
        {
            if (harmony == null || type == null || string.IsNullOrEmpty(methodName))
                throw new ArgumentNullException();

            if (prefix == null && postfix == null && transpiler == null)
            {
                MMLog.WriteWarning("[HarmonyHelper.PatchAllOverloads] No patches provided");
                return;
            }

            var methods = type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static);

            var matches = methods.Where(m => 
                m.Name == methodName && 
                (!ignoreGenerics || !m.IsGenericMethodDefinition))
                .ToList();

            // Optional parameter type filtering
            if (parameterTypes != null)
            {
                matches = matches.Where(m =>
                {
                    var mparams = m.GetParameters().Select(p => p.ParameterType).ToArray();
                    if (mparams.Length != parameterTypes.Length) return false;

                    for (int i = 0; i < parameterTypes.Length; i++)
                    {
                        if (parameterTypes[i] != mparams[i]) return false;
                    }
                    return true;
                }).ToList();
            }

            if (matches.Count == 0)
            {
                MMLog.WriteWarning(
                    $"[HarmonyHelper.PatchAllOverloads] No overloads found for " +
                    $"{type.FullName}.{methodName}");
                return;
            }

            int successCount = 0;
            var failedSignatures = new List<string>();

            foreach (var method in matches)
            {
                var sig = method.GetParameters().Length == 0
                        ? "()"
                        : $"({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name).ToArray())})";

                try
                {
                    harmony.Patch(method, prefix, postfix, transpiler);
                    successCount++;

                    MMLog.WriteDebug(
                        $"[HarmonyHelper] Patched overload {type.Name}.{methodName}{sig}");
                }
                catch (Exception ex)
                {
                    failedSignatures.Add($"{methodName}{sig}");
                    MMLog.WriteError(
                        $"[HarmonyHelper] Failed to patch {type.Name}.{methodName}{sig}: " +
                        $"{ex.Message}");
                }
            }

            if (failedSignatures.Count > 0)
            {
                MMLog.WriteWarning(
                    $"[HarmonyHelper] {failedSignatures.Count} overload(s) failed: " +
                    string.Join(", ", failedSignatures.ToArray()));
            }

            MMLog.Write(
                $"[HarmonyHelper] PatchAllOverloads completed: {successCount}/{matches.Count} " +
                $"overloads patched for {type.Name}.{methodName}");
        }

        /// <summary>
        /// Patch all parameterless overloads (no arguments).
        /// Convenience wrapper for PatchAllOverloads with Type.EmptyTypes.
        /// </summary>
        public static void PatchAllParameterlessOverloads(
            HarmonyLib.Harmony harmony,
            Type type,
            string methodName,
            HarmonyMethod prefix = null,
            HarmonyMethod postfix = null,
            HarmonyMethod transpiler = null)
        {
            PatchAllOverloads(harmony, type, methodName, Type.EmptyTypes, false, prefix, postfix, transpiler);
        }
    }
}
