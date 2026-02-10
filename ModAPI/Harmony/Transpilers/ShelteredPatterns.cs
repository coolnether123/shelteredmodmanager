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
    /// These are kept separate to keep the core API game-agnostic.
    /// </summary>
    public static class ShelteredPatterns
    {
        /// <summary>
        /// A helper that matches the common pattern of accessing a Manager Instance in Sheltered.
        /// Common usage: GameModeManager.instance, InventoryManager.instance, etc.
        /// Handles both static properties (get_instance) and static fields (instance).
        /// </summary>
        /// <param name="managerType">The manager class type.</param>
        public static FluentTranspiler MatchManager(this FluentTranspiler t, Type managerType)
        {
            // Search for get_instance property first, then fall back to static field
            t.MatchCall(managerType, "get_instance");
            if (!t.HasMatch)
                t.MatchFieldLoad(managerType, "instance");
            
            return t;
        }

        /// <summary>
        /// Replace 'new Vector2(0,0)' followed immediately by a method call that consumes it.
        /// Example: new Vector2(0,0) -> WorldPosToGridRef(vec) becomes GetShelterGridRef()
        /// This is common in Sheltered when calculating grid positions relative to the bunker.
        /// </summary>
        /// <param name="consumingMethodType">Type declaring the method that currently takes the Vector2.</param>
        /// <param name="consumingMethodName">Name of the method (e.g. WorldPosToGridRef).</param>
        /// <param name="replacementType">Type containing your new static replacement method.</param>
        /// <param name="replacementMethod">Name of your method (which should return the final type, e.g. IntVector2).</param>
        public static FluentTranspiler ReplaceVectorZeroThenMethodCall(
            this FluentTranspiler t,
            Type consumingMethodType,
            string consumingMethodName,
            Type replacementType,
            string replacementMethod)
        {
            // Build the composite pattern: PatternVector2Zero + the consuming call
            var vectorPattern = UnityPatterns.PatternVector2Zero();
            var pattern = new Func<CodeInstruction, bool>[vectorPattern.Length + 1];
            Array.Copy(vectorPattern, pattern, vectorPattern.Length);
            
            pattern[vectorPattern.Length] = instr => 
                (instr.opcode == OpCodes.Call || instr.opcode == OpCodes.Callvirt) &&
                 instr.operand is MethodInfo mi &&
                 mi.DeclaringType == consumingMethodType &&
                 mi.Name == consumingMethodName;

            var replacementCall = replacementType.GetMethod(replacementMethod, 
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            
            if (replacementCall == null) 
            {
                throw new ArgumentException($"Method {replacementType.Name}.{replacementMethod} not found or not static");
            }

            // Auto-detect if consuming method is instance method
            bool isInstance = false;
            try 
            {
                var consumingMethod = consumingMethodType.GetMethod(consumingMethodName, 
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(UnityEngine.Vector2) },
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
            {
                replacements.Add(new CodeInstruction(OpCodes.Pop));
            }
            replacements.Add(new CodeInstruction(OpCodes.Call, replacementCall));

            // Replace entire 4-instruction sequence with result. 
            // We set preserveInstructionCount to true to keep the Harmony validator happy by maintaining instruction indices.
            return t.ReplaceAllPatterns(pattern, replacements.ToArray(), preserveInstructionCount: true);
        }



        /// <summary>Matches Manager.instance static field access.</summary>
        public static FluentTranspiler MatchManagerSingleton(this FluentTranspiler t, Type managerType)
        {
            return t.MatchFieldLoad(managerType, "instance");
        }

        /// <summary>Matches Localization.Get(key).</summary>
        public static FluentTranspiler MatchUILocalization(this FluentTranspiler t, string key = null)
        {
             return t.MatchCall(typeof(Localization), "Get");
        }

        /// <summary>Matches StartCoroutine call.</summary>
        public static FluentTranspiler MatchCoroutineStart(this FluentTranspiler t)
        {
            return t.MatchCall(typeof(MonoBehaviour), "StartCoroutine");
        }

        /// <summary>Matches generic null check pattern (ldnull).</summary>
        public static FluentTranspiler MatchNullCheck(this FluentTranspiler t)
        {
            return t.MatchOpCode(OpCodes.Ldnull);
        }

        /// <summary>
        /// Safely replaces 'this.m_field = value' with a call to a static replacement method.
        /// The replacement method must have signature: void Replacement(InstanceType instance, FieldType value).
        /// </summary>
        public static FluentTranspiler ReplaceFieldAssignment(this FluentTranspiler t, 
            Type instanceType, 
            string fieldName, 
            Type replacementType,
            string replacementMethodName)
        {
            // Pattern:
            // ldarg.0
            // ... load value ...
            // stfld field
            
            // We want to match the stfld and capture the preceding load if possible, 
            // OR just replace `stfld field` with `call Replacement`.
            // Since `stfld` takes (instance, value) on stack, and `Replacement` takes (instance, value),
            // we can just replace the opcode!
            
            // BUT: stfld pops 2. Call pops 2.
            // So yes, we can just replace stfld with call.
            
            return t
                .MatchFieldStore(instanceType, fieldName)
                .ReplaceWithCall(replacementType, replacementMethodName);
        }

        /// <summary>Matches a call to any MMLog writing method.</summary>
        public static FluentTranspiler MatchLog(this FluentTranspiler t)
        {
             return t.MatchCall(typeof(MMLog), "WriteInfo")
                .MatchCall(typeof(MMLog), "WriteDebug")
                .MatchCall(typeof(MMLog), "WriteWarning")
                .MatchCall(typeof(MMLog), "WriteError");
        }

        /// <summary>Matches the GameModeManager.instance.m_bunkerPos access.</summary>
        public static FluentTranspiler MatchBunkerLocation(this FluentTranspiler t)
        {
            return t.MatchManager(typeof(GameModeManager))
                    .MatchFieldLoad(typeof(GameModeManager), "m_bunkerPos");
        }
    }
}
