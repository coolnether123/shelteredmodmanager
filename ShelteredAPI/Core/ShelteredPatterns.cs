using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using ModAPI.Core;

namespace ModAPI.Harmony
{
    /// <summary>
    /// Specialized transpiler helpers for Sheltered-specific patterns.
    /// Moved to ShelteredAPI to preserve ModAPI's generic nature.
    /// </summary>
    public static class ShelteredPatterns
    {
        public static FluentTranspiler MatchManager(this FluentTranspiler t, Type managerType)
        {
            t.MatchCall(managerType, "get_instance");
            if (!t.HasMatch)
                t.MatchFieldLoad(managerType, "instance");
            return t;
        }

        public static FluentTranspiler ReplaceVectorZeroThenMethodCall(
            this FluentTranspiler t,
            Type consumingMethodType,
            string consumingMethodName,
            Type replacementType,
            string replacementMethod)
        {
            var vectorPattern = UnityPatterns.PatternVector2Zero();
            var pattern = new Func<CodeInstruction, bool>[vectorPattern.Length + 1];
            Array.Copy(vectorPattern, pattern, vectorPattern.Length);

            pattern[vectorPattern.Length] = instr =>
                (instr.opcode == OpCodes.Call || instr.opcode == OpCodes.Callvirt) &&
                 instr.operand is MethodInfo mi &&
                 mi.DeclaringType == consumingMethodType &&
                 mi.Name == consumingMethodName;

            var replacementCall = replacementType.GetMethod(
                replacementMethod,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            if (replacementCall == null)
                throw new ArgumentException($"Method {replacementType.Name}.{replacementMethod} not found or not static");

            bool isInstance = false;
            try
            {
                var consumingMethod = consumingMethodType.GetMethod(
                    consumingMethodName,
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(Vector2) },
                    null);

                if (consumingMethod != null && !consumingMethod.IsStatic)
                    isInstance = true;
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning(
                    $"[ShelteredPatterns] Could not resolve " +
                    $"{consumingMethodType.Name}.{consumingMethodName}" +
                    $" for instance check: {ex.Message}. " +
                    $"Assuming static.");
            }

            var replacements = new List<CodeInstruction>();
            if (isInstance)
                replacements.Add(new CodeInstruction(OpCodes.Pop));
            replacements.Add(new CodeInstruction(OpCodes.Call, replacementCall));

            return t.ReplaceAllPatterns(pattern, replacements.ToArray(), preserveInstructionCount: true);
        }

        public static FluentTranspiler MatchManagerSingleton(this FluentTranspiler t, Type managerType)
        {
            return t.MatchFieldLoad(managerType, "instance");
        }

        public static FluentTranspiler MatchUILocalization(this FluentTranspiler t, string key = null)
        {
             return t.MatchCall(typeof(Localization), "Get");
        }

        public static FluentTranspiler MatchCoroutineStart(this FluentTranspiler t)
        {
            return t.MatchCall(typeof(MonoBehaviour), "StartCoroutine");
        }

        public static FluentTranspiler MatchNullCheck(this FluentTranspiler t)
        {
            return t.MatchOpCode(OpCodes.Ldnull);
        }

        public static FluentTranspiler ReplaceFieldAssignment(
            this FluentTranspiler t,
            Type instanceType,
            string fieldName,
            Type replacementType,
            string replacementMethodName)
        {
            return t
                .MatchFieldStore(instanceType, fieldName)
                .ReplaceWithCall(replacementType, replacementMethodName);
        }

        public static FluentTranspiler MatchLog(this FluentTranspiler t)
        {
             return t.MatchCall(typeof(MMLog), "WriteInfo")
                .MatchCall(typeof(MMLog), "WriteDebug")
                .MatchCall(typeof(MMLog), "WriteWarning")
                .MatchCall(typeof(MMLog), "WriteError");
        }

        public static FluentTranspiler MatchBunkerLocation(this FluentTranspiler t)
        {
            return t.MatchManager(typeof(GameModeManager))
                    .MatchFieldLoad(typeof(GameModeManager), "m_bunkerPos");
        }
    }
}
