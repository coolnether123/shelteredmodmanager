using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using ModAPI.Core;

namespace ModAPI.Harmony
{
    /// <summary>
    /// High-Level Intent API for FluentTranspiler.
    /// Provides intent-based operations that abstract away IL details.
    /// allows developers to express "what" they want to do rather than "how" to do it in IL.
    /// </summary>
    public static class IntentAPI
    {
        /// <summary>
        /// "When method X is called, call Y instead."
        /// Automatically handles instance-to-static conversion, parameter matching validation,
        /// and OpCode replacement (Call vs Callvirt).
        /// </summary>
        public static FluentTranspiler RedirectCall(
            this FluentTranspiler t,
            Type originalType, string originalMethod,
            Type replacementType, string replacementMethod,
            SearchMode mode = SearchMode.Start)
        {
            return t
                .FindCall(originalType, originalMethod, mode)
                .ReplaceWithCall(replacementType, replacementMethod);
        }

        /// <summary>
        /// "When method X is called, call Y instead."
        /// Replaces ALL occurrences of the call in the method body.
        /// </summary>
        public static FluentTranspiler RedirectCallAll(
            this FluentTranspiler t,
            Type originalType, string originalMethod,
            Type replacementType, string replacementMethod)
        {
            return t.ReplaceAllCalls(
                originalType, originalMethod,
                replacementType, replacementMethod);
        }

        /// <summary>
        /// "Change this constant value to that value."
        /// Helper for quickly tuning magic numbers.
        /// </summary>
        public static FluentTranspiler ChangeConstant(
            this FluentTranspiler t,
            float oldValue, float newValue,
            SearchMode mode = SearchMode.Start)
        {
            return t
                .FindConstFloat(oldValue, mode)
                .ReplaceWith(OpCodes.Ldc_R4, newValue);
        }

        /// <summary>
        /// "Change this constant value to that value."
        /// Updates ALL occurrences of the float constant.
        /// </summary>
        public static FluentTranspiler ChangeConstantAll(
            this FluentTranspiler t,
            float oldValue, float newValue)
        {
            return t.ReplaceAllPatterns(
                new Func<CodeInstruction, bool>[] { instr => instr.IsLdcR4(oldValue) },
                new[] { new CodeInstruction(OpCodes.Ldc_R4, newValue) },
                preserveInstructionCount: true);
        }

        /// <summary>
        /// "Change this constant integer to that value."
        /// </summary>
        public static FluentTranspiler ChangeConstant(
            this FluentTranspiler t,
            int oldValue, int newValue,
            SearchMode mode = SearchMode.Start)
        {
            return t
                .FindConstInt(oldValue, mode)
                .ReplaceWith(OpCodes.Ldc_I4, newValue);
        }

        /// <summary>
        /// "Change this constant integer to that value."
        /// Updates ALL occurrences of the integer constant.
        /// </summary>
        public static FluentTranspiler ChangeConstantAll(
            this FluentTranspiler t,
            int oldValue, int newValue)
        {
            return t.ReplaceAllPatterns(
                new Func<CodeInstruction, bool>[] { instr => instr.IsLdcI4(oldValue) },
                new[] { new CodeInstruction(OpCodes.Ldc_I4, newValue) },
                preserveInstructionCount: true);
        }

        /// <summary>
        /// "Remove this method call and its arguments."
        /// Automatically calculates how many stack items to pop (arguments) and pushes a default value if the method has a return type.
        /// Handy for nuking logging calls or analytics tracking.
        /// </summary>
        public static FluentTranspiler RemoveCall(
            this FluentTranspiler t,
            Type type, string methodName,
            SearchMode mode = SearchMode.Start)
        {
            t.FindCall(type, methodName, mode);
            if (!t.HasMatch) return t;

            var instr = t.Current;
            if (!(instr.operand is MethodInfo mi)) return t;

            int argCount = mi.GetParameters().Length;
            if (!mi.IsStatic) argCount++; // 'this'
            bool hasReturn = mi.ReturnType != typeof(void);

            // Build replacement: pop all args, push dummy return if needed
            var replacement = new List<CodeInstruction>();
            for (int i = 0; i < argCount; i++)
                replacement.Add(new CodeInstruction(OpCodes.Pop));

            if (hasReturn)
            {
                // Push default value for return type
                if (mi.ReturnType == typeof(int) 
                    || mi.ReturnType == typeof(bool)
                    || mi.ReturnType.IsEnum) // Enums are ints generally
                    replacement.Add(
                        new CodeInstruction(OpCodes.Ldc_I4_0));
                else if (mi.ReturnType == typeof(float))
                    replacement.Add(
                        new CodeInstruction(OpCodes.Ldc_R4, 0f));
                else if (mi.ReturnType == typeof(double))
                    replacement.Add(
                        new CodeInstruction(OpCodes.Ldc_R8, 0d));
                else if (mi.ReturnType.IsValueType)
                {
                    // For structs, need initobj via local or simple null load if treated as object (unsafe)
                    // For safety in this high-level API, if we can't easily zero-init a complex struct, we warn or default to ldnull (which might crash strict verifiers)
                    // However, for most simple "RemoveCall" cases, it's void or simple types.
                     replacement.Add(
                        new CodeInstruction(OpCodes.Ldnull));
                }
                else
                    replacement.Add(
                        new CodeInstruction(OpCodes.Ldnull));
            }

            return t.ReplaceSequence(1, replacement.ToArray());
        }

        /// <summary>
        /// "Before this method call happens, call my hook first."
        /// The hook receives the arguments of the ENCLOSING method (not the target call).
        /// </summary>
        public static FluentTranspiler InjectBeforeCall(
            this FluentTranspiler t,
            Type targetType, string targetMethod,
            Type hookType, string hookMethod,
            SearchMode mode = SearchMode.Start)
        {
            var hook = hookType.GetMethod(hookMethod,
                BindingFlags.Static | BindingFlags.Public 
                | BindingFlags.NonPublic);
            if (hook == null)
                throw new ArgumentException(
                    $"{hookType.Name}.{hookMethod} not found");
            if (!hook.IsStatic)
                throw new ArgumentException(
                    $"{hookType.Name}.{hookMethod} must be static");

            // Build insertion instructions
            var insertions = new List<CodeInstruction>();

            // Load parameters for hook from the ENCLOSING method's arguments
            var hookParams = hook.GetParameters();
            for (int i = 0; i < hookParams.Length; i++)
            {
                // Note: This relies on the hook parameters matching the indices of the enclosing method arguments.
                // Ldarg with index.
                insertions.Add(new CodeInstruction(OpCodes.Ldarg, i));
            }
            insertions.Add(
                new CodeInstruction(OpCodes.Call, hook));

            // If hook returns something, pop it 
            // (it's a side-effect hook)
            if (hook.ReturnType != typeof(void))
                insertions.Add(new CodeInstruction(OpCodes.Pop));

            return t
                .FindCall(targetType, targetMethod, mode)
                .InsertBefore(insertions.ToArray());
        }
    }
}
