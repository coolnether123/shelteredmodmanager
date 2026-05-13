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
        /// <example>
        /// <code>
        /// t.ChangeConstant(4f, 8f);
        /// </code>
        /// </example>
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
        /// <example>
        /// <code>
        /// t.ChangeConstant(4, 8);
        /// </code>
        /// </example>
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
            if (!FluentTranspilerRecipeValidation.ValidateNoUnsupportedValueTypeDefault(
                t,
                mi.ReturnType,
                nameof(RemoveCall)))
            {
                return t;
            }

            // Build replacement: pop all args, push dummy return if needed
            var replacement = new List<CodeInstruction>();
            for (int i = 0; i < argCount; i++)
                replacement.Add(new CodeInstruction(OpCodes.Pop));

            if (hasReturn)
            {
                CodeInstruction defaultValue = FluentTranspilerRecipeValidation.CreateDefaultValueInstruction(mi.ReturnType);
                if (defaultValue == null)
                {
                    t.AddWarning($"{nameof(RemoveCall)} could not create a safe default for {FluentTranspilerFormatting.FormatMethod(mi)}.");
                    return t;
                }

                replacement.Add(defaultValue);
            }

            var instructions = t.Instructions().ToList();
            if (!FluentTranspilerRecipeValidation.ValidateBranchTargetsOutsideReplacementRange(
                    t,
                    instructions,
                    t.CurrentIndex,
                    1,
                    nameof(RemoveCall)) ||
                !FluentTranspilerRecipeValidation.ValidateExceptionBlockSafety(
                    t,
                    t.OriginalMethod,
                    1,
                    replacement.Count,
                    nameof(RemoveCall)))
            {
                return t;
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
            {
                t.AddWarning($"{nameof(InjectBeforeCall)} hook {hookType?.Name}.{hookMethod} not found.");
                return t;
            }

            if (!FluentTranspilerRecipeValidation.ValidateHookCanReceiveOriginalArguments(
                t,
                t.OriginalMethod,
                hook,
                nameof(InjectBeforeCall)))
            {
                return t;
            }

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

            t.FindCall(targetType, targetMethod, mode);
            if (!t.HasMatch)
            {
                return t;
            }

            if (!FluentTranspilerRecipeValidation.ValidateExceptionBlockSafety(
                t,
                t.OriginalMethod,
                0,
                insertions.Count,
                nameof(InjectBeforeCall)))
            {
                return t;
            }

            return t.InsertBefore(insertions.ToArray());
        }
    }
}
