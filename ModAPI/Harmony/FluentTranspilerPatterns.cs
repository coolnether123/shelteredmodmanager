using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace ModAPI.Harmony
{
    /// <summary>
    /// Extension methods for FluentTranspiler to support advanced pattern matching.
    /// Addresses feedback from transpiler users regarding safer bulk operations.
    /// </summary>
    public static class FluentTranspilerPatterns
    {
        #region Instruction Type Checking

        /// <summary>Check if current instruction loads a constant float.</summary>
        public static bool IsLdcR4(this FluentTranspiler t, float value)
        {
            if (!t.HasMatch) return false;
            var instr = t.Current;
            return instr != null && instr.opcode == OpCodes.Ldc_R4 && instr.operand is float f && Math.Abs(f - value) < 0.0001f;
        }

        /// <summary>Check if current instruction loads a constant int.</summary>
        public static bool IsLdcI4(this FluentTranspiler t, int value)
        {
            if (!t.HasMatch) return false;
            var instr = t.Current;
            if (instr == null) return false;
            
            if (instr.opcode == OpCodes.Ldc_I4) return (int)instr.operand == value;
            if (instr.opcode == OpCodes.Ldc_I4_0) return value == 0;
            if (instr.opcode == OpCodes.Ldc_I4_1) return value == 1;
            if (instr.opcode == OpCodes.Ldc_I4_M1) return value == -1;
            if (instr.opcode == OpCodes.Ldc_I4_S) return (sbyte)instr.operand == value;
            return false;
        }

        /// <summary>Check if current instruction is a newobj for a specific type.</summary>
        public static bool IsNewobj(this FluentTranspiler t, Type type)
        {
            if (!t.HasMatch) return false;
            var instr = t.Current;
            return instr != null && instr.opcode == OpCodes.Newobj && instr.operand is ConstructorInfo ci && ci.DeclaringType == type;
        }

        /// <summary>Check if current instruction is a newobj for Vector2.</summary>
        public static bool IsNewobjVector2(this FluentTranspiler t)
        {
            return t.IsNewobj(typeof(UnityEngine.Vector2));
        }

        /// <summary>Check if current instruction is a newobj for Vector3.</summary>
        public static bool IsNewobjVector3(this FluentTranspiler t)
        {
            return t.IsNewobj(typeof(UnityEngine.Vector3));
        }

        /// <summary>Check if current instruction calls a specific method.</summary>
        public static bool IsCall(this FluentTranspiler t, Type type, string methodName)
        {
            if (!t.HasMatch) return false;
            var instr = t.Current;
            if (instr == null) return false;
            if (instr.opcode != OpCodes.Call && instr.opcode != OpCodes.Callvirt) return false;
            if (!(instr.operand is MethodInfo method)) return false;
            return method.DeclaringType == type && method.Name == methodName;
        }

        #endregion

        #region Context Inspection

        /// <summary>
        /// Check if a pattern exists backward from current position.
        /// Usage: t.MatchCall(...).CheckBackward(3, i => i.IsLdcR4(0), i => i.IsLdcR4(0), i => i.IsNewobjVector2())
        /// </summary>
        public static bool CheckBackward(this FluentTranspiler t, int steps, params Func<CodeInstruction, bool>[] predicates)
        {
            if (!t.HasMatch) return false;
            if (predicates.Length != steps) 
                throw new ArgumentException($"CheckBackward: predicates count ({predicates.Length}) must equal steps ({steps})");
            
            int currentIdx = t.CurrentIndex;
            
            // We need access to the instruction list - work around this limitation
            // by checking position validity
            if (currentIdx - steps < 0) return false;
            
            // Since we can't easily access the instruction list from FluentTranspiler,
            // we'll use a workaround: move back, check, then restore position
            int originalPos = currentIdx;
            
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
                // Restore position by moving forward
                while (t.CurrentIndex < originalPos && t.HasMatch)
                {
                    t.Next();
                }
            }
        }

        #endregion

        #region Safer Removal Operations

        /// <summary>
        /// Remove the current instruction plus N previous instructions.
        /// Example: t.MatchCall().RemoveWithPrevious(3) removes the call and 3 instructions before it.
        /// </summary>
        public static FluentTranspiler RemoveWithPrevious(this FluentTranspiler t, int previousCount)
        {
            if (!t.HasMatch) return t;
            
            // Move back to start of removal range
            for (int i = 0; i < previousCount; i++)
            {
                t.Previous();
                if (!t.HasMatch) return t;
            }
            
            // Remove all instructions (current + previousCount)
            for (int i = 0; i <= previousCount; i++)
            {
                if (t.HasMatch)
                    t.Remove();
            }
            
            return t;
        }

        /// <summary>
        /// Conditional execution - only run action if condition is true.
        /// Example: t.MatchCall().If(() => t.CheckBackward(3, ...), t => t.RemoveWithPrevious(3))
        /// </summary>
        public static FluentTranspiler If(this FluentTranspiler t, Func<bool> condition, Action<FluentTranspiler> thenAction)
        {
            if (condition())
                thenAction(t);
            return t;
        }

        #endregion

        #region Pattern Sequence Helpers

        /// <summary>
        /// Helper to create a pattern that matches: ldc.r4 0, ldc.r4 0, newobj Vector2
        /// Common pattern in Unity code for Vector2.zero before it was optimized.
        /// </summary>
        public static Func<CodeInstruction, bool>[] PatternVector2Zero()
        {
            return new Func<CodeInstruction, bool>[]
            {
                instr => instr.opcode == OpCodes.Ldc_R4 && instr.operand is float f1 && Math.Abs(f1) < 0.0001f,
                instr => instr.opcode == OpCodes.Ldc_R4 && instr.operand is float f2 && Math.Abs(f2) < 0.0001f,
                instr => instr.opcode == OpCodes.Newobj && instr.operand is ConstructorInfo ci && ci.DeclaringType == typeof(UnityEngine.Vector2)
            };
        }

        /// <summary>
        /// Helper to create a pattern that matches: ldc.r4 0, ldc.r4 0, ldc.r4 0, newobj Vector3
        /// </summary>
        public static Func<CodeInstruction, bool>[] PatternVector3Zero()
        {
            return new Func<CodeInstruction, bool>[]
            {
                instr => instr.opcode == OpCodes.Ldc_R4 && instr.operand is float f1 && Math.Abs(f1) < 0.0001f,
                instr => instr.opcode == OpCodes.Ldc_R4 && instr.operand is float f2 && Math.Abs(f2) < 0.0001f,
                instr => instr.opcode == OpCodes.Ldc_R4 && instr.operand is float f3 && Math.Abs(f3) < 0.0001f,
                instr => instr.opcode == OpCodes.Newobj && instr.operand is ConstructorInfo ci && ci.DeclaringType == typeof(UnityEngine.Vector3)
            };
        }

        #endregion
    }
}
