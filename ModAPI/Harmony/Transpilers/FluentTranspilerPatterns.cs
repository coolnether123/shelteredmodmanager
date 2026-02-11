using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using ModAPI.Core;

namespace ModAPI.Harmony
{
    /// <summary>
    /// Extension methods for FluentTranspiler to support general IL pattern matching.
    /// These are game-agnostic and focus on raw instruction manipulation.
    /// </summary>
    public static class FluentTranspilerPatterns
    {
        #region Instruction Predicates (CodeInstruction Extensions)

        /// <summary>Check if instruction loads a constant float.</summary>
        public static bool IsLdcR4(this CodeInstruction instr, float value)
            => instr.opcode == OpCodes.Ldc_R4 && instr.operand is float f && Math.Abs(f - value) < 0.0001f;

        /// <summary>Check if instruction loads a constant int (handles all short forms).</summary>
        public static bool IsLdcI4(this CodeInstruction instr, int value)
        {
            if (instr.opcode == OpCodes.Ldc_I4) 
                return (int)instr.operand == value;
            if (instr.opcode == OpCodes.Ldc_I4_S) 
                return (sbyte)instr.operand == value;
            if (instr.opcode == OpCodes.Ldc_I4_M1) return value == -1;
            if (instr.opcode == OpCodes.Ldc_I4_0) return value == 0;
            if (instr.opcode == OpCodes.Ldc_I4_1) return value == 1;
            if (instr.opcode == OpCodes.Ldc_I4_2) return value == 2;
            if (instr.opcode == OpCodes.Ldc_I4_3) return value == 3;
            if (instr.opcode == OpCodes.Ldc_I4_4) return value == 4;
            if (instr.opcode == OpCodes.Ldc_I4_5) return value == 5;
            if (instr.opcode == OpCodes.Ldc_I4_6) return value == 6;
            if (instr.opcode == OpCodes.Ldc_I4_7) return value == 7;
            if (instr.opcode == OpCodes.Ldc_I4_8) return value == 8;
            return false;
        }

        /// <summary>Check if instruction is a newobj for a specific type.</summary>
        public static bool IsNewobj(this CodeInstruction instr, Type type)
            => instr.opcode == OpCodes.Newobj && instr.operand is ConstructorInfo ci && ci.DeclaringType == type;

        /// <summary>Check if instruction calls a specific method (handles Call and Callvirt).</summary>
        public static bool IsCall(this CodeInstruction instr, Type type, string methodName)
        {
            if (instr.opcode != OpCodes.Call && instr.opcode != OpCodes.Callvirt) return false;
            return instr.operand is MethodInfo method && method.DeclaringType == type && method.Name == methodName;
        }

        #endregion

        #region FluentTranspiler Wrappers for Predicates

        /// <summary>Check if current instruction loads a constant float.</summary>
        public static bool IsLdcR4(this FluentTranspiler t, float value)
            => t.HasMatch && t.Current.IsLdcR4(value);

        /// <summary>Check if current instruction loads a constant int.</summary>
        public static bool IsLdcI4(this FluentTranspiler t, int value)
            => t.HasMatch && t.Current.IsLdcI4(value);

        /// <summary>Check if current instruction is a newobj for a specific type.</summary>
        public static bool IsNewobj(this FluentTranspiler t, Type type)
            => t.HasMatch && t.Current.IsNewobj(type);

        /// <summary>Check if current instruction calls a specific method.</summary>
        public static bool IsCall(this FluentTranspiler t, Type type, string methodName)
            => t.HasMatch && t.Current.IsCall(type, methodName);

        #endregion

        #region Context Inspection

        /// <summary>
        /// Check if a pattern exists backward from current position.
        /// </summary>
        public static bool CheckBackward(this FluentTranspiler t, int steps, params Func<CodeInstruction, bool>[] predicates)
        {
            if (!t.HasMatch) return false;
            if (predicates.Length != steps) 
                throw new ArgumentException($"CheckBackward: predicates count ({predicates.Length}) must equal steps ({steps})");
            
            int originalPos = t.CurrentIndex;
            if (originalPos - steps < 0) return false;
            
            try
            {
                for (int i = steps - 1; i >= 0; i--)
                {
                    t.Previous();
                    if (!t.HasMatch) return false;
                    if (!predicates[steps - 1 - i](t.Current)) return false;
                }
                return true;
            }
            finally
            {
                // Restore position reliable way
                t.MoveTo(originalPos);
            }
        }

        #endregion

        #region Safer Removal Operations

        /// <summary>
        /// Remove the current instruction plus N previous instructions.
        /// </summary>
        /// <summary>
        /// Remove the current instruction plus N previous instructions.
        /// Preserves labels by moving them to the next instruction after the gap.
        /// </summary>
        public static FluentTranspiler RemoveWithPrevious(this FluentTranspiler t, int previousCount)
        {
            if (!t.HasMatch) return t;

            int startIndex = t.CurrentIndex - previousCount;
            if (startIndex < 0) return t;

            return t.MoveTo(startIndex).ReplaceSequence(previousCount + 1, new CodeInstruction[0]);
        }

        /// <summary>
        /// Conditional execution based on a predicate.
        /// </summary>
        public static FluentTranspiler If(this FluentTranspiler t, Func<bool> condition, Action<FluentTranspiler> thenAction)
        {
            if (condition())
                thenAction(t);
            return t;
        }

        #endregion

        #region Pattern Replacement Helpers

        /// <summary>
        /// Replace all occurrences of a pattern with a static method call.
        /// </summary>
        public static FluentTranspiler ReplacePatternWithCall(
            this FluentTranspiler t, 
            Func<CodeInstruction, bool>[] pattern, 
            Type type, 
            string method,
            bool preserveInstructionCount = true)
        {
            var callMethod = type.GetMethod(method, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (callMethod == null) 
            {
                throw new ArgumentException($"Method {type.Name}.{method} not found or not static");
            }
            
            return t.ReplaceAllPatterns(pattern, new[] { new CodeInstruction(OpCodes.Call, callMethod) }, preserveInstructionCount);
        }

        /// <summary>
        /// Alias for ReplacePatternWithCall with preserveInstructionCount = false.
        /// </summary>
        public static FluentTranspiler ReplaceSequenceWithCall(
            this FluentTranspiler t,
            Func<CodeInstruction, bool>[] sequencePattern,
            Type replacementType,
            string replacementMethod)
        {
            return t.ReplacePatternWithCall(sequencePattern, replacementType, replacementMethod, preserveInstructionCount: false);
        }

        #endregion
    }
}
