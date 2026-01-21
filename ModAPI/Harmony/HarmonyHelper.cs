using System;
using System.Reflection;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Reflection;

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
    }
}
