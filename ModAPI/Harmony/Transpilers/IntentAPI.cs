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
    /// Common FluentTranspiler edits expressed as method calls instead of raw IL operations.
    /// </summary>
    public static class IntentAPI
    {
        /// <summary>
        /// "When method X is called, call Y instead." Redirects the <b>first</b> matching call.
        /// </summary>
        /// <remarks>
        /// Legacy redirect entry point retained for compatibility. To redirect a
        /// single call site is <c>t.ForCall(originalType, originalMethod).ReplaceWith(replacementType,
        /// replacementMethod)</c>, which reports an ambiguous match instead of silently taking the
        /// first. See Transpilers/README.md section 4.1.
        /// </remarks>
        [Obsolete("Use t.ForCall(type, method).ReplaceWith(type, method). See Transpilers/README.md section 4.1.", false)]
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
        /// Replaces every matching call in the method body.
        /// </summary>
        /// <remarks>
        /// Legacy redirect entry point retained for compatibility. To redirect every
        /// matching call site is <c>t.ForCall(originalType, originalMethod).ReplaceAllWith(replacementType,
        /// replacementMethod)</c>. See Transpilers/README.md section 4.1. This shim forwards to that single
        /// implementation so the two paths cannot drift.
        /// </remarks>
        [Obsolete("Use t.ForCall(type, method).ReplaceAllWith(type, method). See Transpilers/README.md section 4.1.", false)]
        public static FluentTranspiler RedirectCallAll(
            this FluentTranspiler t,
            Type originalType, string originalMethod,
            Type replacementType, string replacementMethod)
        {
            t.ForCall(originalType, originalMethod)
             .ReplaceAllWith(replacementType, replacementMethod);
            return t;
        }

        /// <summary>
        /// Replaces the first matching floating-point constant.
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
        /// Replaces every matching floating-point constant.
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
        /// Replaces the first matching integer constant.
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
        /// Replaces every matching integer constant.
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
        /// Removes the first matching method call and its arguments.
        /// Pops the call arguments and pushes a default value when the method has a return value.
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
                // Hook parameter positions must match the enclosing method arguments.
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
