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
        /// </summary>
        /// <param name="managerType">The manager class type.</param>
        public static FluentTranspiler MatchManager(this FluentTranspiler t, Type managerType)
        {
            return t.MatchCall(managerType, "get_instance");
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
            catch 
            {
                // Fallback: assume static if lookup failed
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
    }
}
