using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace ModAPI.Harmony
{
    public enum FluentReplacementResult
    {
        NoMatch,
        PatternReplaced,
        FallbackCallReplaced,
        ReplacementAlreadyPresent,
        AlreadyPatched,
        AmbiguousMatch,
        UnsafeMatch,
        Failed
    }

    /// <summary>
    /// Compatibility-oriented helpers for patches that need to handle either an expected
    /// base IL shape or a method body already rewritten by another transpiler.
    /// </summary>
    public static class FluentTranspilerCompatibilityExtensions
    {
        public static bool Succeeded(this FluentReplacementResult result)
        {
            return result == FluentReplacementResult.PatternReplaced ||
                   result == FluentReplacementResult.FallbackCallReplaced ||
                   result == FluentReplacementResult.ReplacementAlreadyPresent ||
                   result == FluentReplacementResult.AlreadyPatched;
        }
        /// <summary>
        /// Replaces the instruction at an absolute index while preserving labels and exception blocks.
        /// </summary>
        public static FluentTranspiler ReplaceAt(this FluentTranspiler transpiler, int absoluteIndex, CodeInstruction instruction)
        {
            if (transpiler == null)
            {
                return null;
            }

            if (instruction == null)
            {
                transpiler.AddWarning("ReplaceAt received a null replacement instruction.");
                return transpiler;
            }

            int count = transpiler.Instructions().Count();
            if (absoluteIndex < 0 || absoluteIndex >= count)
            {
                transpiler.AddSoftFailure($"ReplaceAt: index {absoluteIndex} is out of range (the stream has {count} instruction(s), valid indices 0..{count - 1}). Fix: recompute the index from a fresh Instructions() snapshot — a prior insert/remove likely shifted it.");
                return transpiler;
            }

            return transpiler.MoveTo(absoluteIndex)
                             .ReplaceSequence(1, instruction);
        }

        /// <summary>Replaces the instruction at an absolute index with a static method call.</summary>
        public static FluentTranspiler ReplaceAtWithCall(this FluentTranspiler transpiler, int absoluteIndex, MethodInfo method)
        {
            if (transpiler == null)
            {
                return null;
            }

            if (!FluentTranspilerRecipeValidation.ValidateStaticMethod(transpiler, method, nameof(ReplaceAtWithCall)))
            {
                return transpiler;
            }

            return transpiler.ReplaceAt(absoluteIndex, new CodeInstruction(OpCodes.Call, method));
        }

        /// <summary>
        /// Replaces every instruction matching a predicate with a replacement instruction.
        /// The instruction count is preserved, making this suitable for compatibility rewrites.
        /// </summary>
        public static int ReplaceMatchingInstructions(
            this FluentTranspiler transpiler,
            Func<CodeInstruction, bool> predicate,
            Func<CodeInstruction, CodeInstruction> replacementFactory,
            string editLabel = null)
        {
            if (transpiler == null)
            {
                return 0;
            }

            if (predicate == null)
            {
                transpiler.AddWarning("ReplaceMatchingInstructions received a null predicate.");
                return 0;
            }

            if (replacementFactory == null)
            {
                transpiler.AddWarning("ReplaceMatchingInstructions received a null replacement factory.");
                return 0;
            }

            var instructions = transpiler.Instructions().ToList();
            int replaced = 0;
            for (int i = 0; i < instructions.Count; i++)
            {
                if (!predicate(instructions[i]))
                {
                    continue;
                }

                CodeInstruction replacement = replacementFactory(instructions[i]);
                if (replacement == null)
                {
                    transpiler.AddWarning($"ReplaceMatchingInstructions factory returned null at index {i}.");
                    continue;
                }

                transpiler.ReplaceAt(i, replacement);
                replaced++;
            }

            if (replaced > 0 && !string.IsNullOrEmpty(editLabel))
            {
                transpiler.AddNote($"{editLabel}: replaced {replaced} instruction(s).");
            }

            return replaced;
        }

        /// <summary>Replaces every call instruction whose method matches the provided predicate.</summary>
        public static int ReplaceMatchingCalls(
            this FluentTranspiler transpiler,
            Func<MethodInfo, bool> methodPredicate,
            MethodInfo replacementMethod,
            string editLabel = null)
        {
            if (transpiler == null)
            {
                return 0;
            }

            if (methodPredicate == null)
            {
                transpiler.AddWarning("ReplaceMatchingCalls received a null method predicate.");
                return 0;
            }

            if (!FluentTranspilerRecipeValidation.ValidateStaticMethod(transpiler, replacementMethod, nameof(ReplaceMatchingCalls)))
            {
                return 0;
            }

            var instructions = transpiler.Instructions().ToList();
            int replaced = 0;
            for (int i = 0; i < instructions.Count; i++)
            {
                MethodInfo sourceMethod;
                if (!TryGetMatchingCall(instructions[i], methodPredicate, out sourceMethod))
                {
                    continue;
                }

                if (!FluentTranspilerRecipeValidation.ValidateReplacementCallSignature(
                    transpiler,
                    sourceMethod,
                    replacementMethod,
                    nameof(ReplaceMatchingCalls)))
                {
                    continue;
                }

                transpiler.ReplaceAt(i, new CodeInstruction(OpCodes.Call, replacementMethod));
                replaced++;
            }

            if (replaced > 0 && !string.IsNullOrEmpty(editLabel))
            {
                transpiler.AddNote($"{editLabel}: replaced {replaced} call(s).");
            }

            return replaced;
        }

        /// <summary>Checks whether the instruction stream contains a call matching the method predicate.</summary>
        public static bool HasMatchingCall(this FluentTranspiler transpiler, Func<MethodInfo, bool> methodPredicate)
        {
            if (transpiler == null)
            {
                return false;
            }

            if (methodPredicate == null)
            {
                transpiler.AddWarning("HasMatchingCall received a null method predicate.");
                return false;
            }

            foreach (CodeInstruction instruction in transpiler.Instructions())
            {
                if (IsCallToMethod(instruction, methodPredicate))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Replaces a matched predicate sequence at an offset with a call. Returns false without
        /// diagnostics when the sequence is absent, allowing callers to try compatibility fallbacks.
        /// </summary>
        public static bool TryReplaceSequenceOffsetWithCall(
            this FluentTranspiler transpiler,
            Func<CodeInstruction, bool>[] pattern,
            int replacementOffset,
            MethodInfo replacementMethod,
            SearchMode mode = SearchMode.Start)
        {
            if (transpiler == null)
            {
                return false;
            }

            if (!FluentTranspilerRecipeValidation.ValidateStaticMethod(transpiler, replacementMethod, nameof(TryReplaceSequenceOffsetWithCall)))
            {
                return false;
            }

            if (pattern == null || pattern.Length == 0)
            {
                transpiler.AddWarning("TryReplaceSequenceOffsetWithCall received an empty predicate sequence.");
                return false;
            }

            if (replacementOffset < 0 || replacementOffset >= pattern.Length)
            {
                transpiler.AddWarning($"TryReplaceSequenceOffsetWithCall offset {replacementOffset} is outside pattern length {pattern.Length}.");
                return false;
            }

            if (!transpiler.TryFindSequence(mode, pattern))
            {
                return false;
            }

            int replacementIndex = transpiler.CurrentIndex + replacementOffset;
            CodeInstruction originalInstruction = transpiler.Instructions().ElementAt(replacementIndex);
            MethodInfo originalCall = originalInstruction != null ? originalInstruction.operand as MethodInfo : null;
            bool validReplacement = originalCall != null
                ? FluentTranspilerRecipeValidation.ValidateReplacementCallSignature(
                    transpiler,
                    originalCall,
                    replacementMethod,
                    nameof(TryReplaceSequenceOffsetWithCall))
                : FluentTranspilerRecipeValidation.ValidateParameterCount(
                    transpiler,
                    replacementMethod,
                    0,
                    nameof(TryReplaceSequenceOffsetWithCall));
            if (!validReplacement)
            {
                return false;
            }

            transpiler.Advance(replacementOffset)
                      .ReplaceWithCall(replacementMethod);
            return true;
        }

        /// <summary>
        /// Replaces an expected sequence offset with a call. If the sequence is absent, replaces
        /// compatible calls identified by <paramref name="fallbackMethodPredicate"/> instead.
        /// </summary>
        public static FluentReplacementResult ReplaceSequenceOffsetWithCallOrFallbackCall(
            this FluentTranspiler transpiler,
            Func<CodeInstruction, bool>[] pattern,
            int replacementOffset,
            MethodInfo replacementMethod,
            Func<MethodInfo, bool> fallbackMethodPredicate,
            string editLabel = null,
            SearchMode mode = SearchMode.Start)
        {
            if (transpiler == null)
            {
                return FluentReplacementResult.NoMatch;
            }

            if (transpiler.TryReplaceSequenceOffsetWithCall(pattern, replacementOffset, replacementMethod, mode))
            {
                return FluentReplacementResult.PatternReplaced;
            }

            int fallbackReplacements = transpiler.ReplaceMatchingCalls(
                fallbackMethodPredicate,
                replacementMethod,
                editLabel);

            if (fallbackReplacements > 0)
            {
                return FluentReplacementResult.FallbackCallReplaced;
            }

            if (replacementMethod != null && transpiler.HasMatchingCall(method => method == replacementMethod))
            {
                return FluentReplacementResult.ReplacementAlreadyPresent;
            }

            return FluentReplacementResult.NoMatch;
        }

        private static bool IsCallToMethod(CodeInstruction instruction, Func<MethodInfo, bool> methodPredicate)
        {
            MethodInfo ignored;
            return TryGetMatchingCall(instruction, methodPredicate, out ignored);
        }

        private static bool TryGetMatchingCall(CodeInstruction instruction, Func<MethodInfo, bool> methodPredicate, out MethodInfo method)
        {
            method = null;
            if (instruction == null ||
                (instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt) ||
                !(instruction.operand is MethodInfo candidate))
            {
                return false;
            }

            if (!methodPredicate(candidate))
            {
                return false;
            }

            method = candidate;
            return true;
        }

        internal static bool IsCompatibleConstantReplacement(
            MethodInfo replacementMethod,
            Type expectedReturnType,
            out string reason)
        {
            reason = null;

            if (replacementMethod == null)
            {
                reason = "replacement method is null";
                return false;
            }

            if (!replacementMethod.IsStatic)
            {
                reason = "replacement " + FluentTranspilerFormatting.FormatMethod(replacementMethod) + " is not static";
                return false;
            }

            int parameterCount = replacementMethod.GetParameters().Length;
            if (parameterCount != 0)
            {
                reason = "replacement " + FluentTranspilerFormatting.FormatMethod(replacementMethod) +
                         " takes " + parameterCount + " parameter(s), expected 0";
                return false;
            }

            if (!IsReturnCompatible(expectedReturnType, replacementMethod.ReturnType))
            {
                reason = "replacement " + FluentTranspilerFormatting.FormatMethod(replacementMethod) +
                         " returns " + FormatType(replacementMethod.ReturnType) +
                         ", expected " + FormatType(expectedReturnType);
                return false;
            }

            return true;
        }

        internal static bool IsCompatibleCallReplacement(
            MethodInfo sourceMethod,
            MethodInfo replacementMethod,
            out string reason)
        {
            reason = null;

            if (sourceMethod == null)
            {
                reason = "source method is null";
                return false;
            }

            if (replacementMethod == null)
            {
                reason = "replacement method is null";
                return false;
            }

            if (!replacementMethod.IsStatic)
            {
                reason = "replacement " + FluentTranspilerFormatting.FormatMethod(replacementMethod) + " is not static";
                return false;
            }

            Type[] expectedParameters = BuildExpectedReplacementParameters(sourceMethod);
            ParameterInfo[] actualParameters = replacementMethod.GetParameters();
            if (expectedParameters.Length != actualParameters.Length)
            {
                reason = "replacement " + FluentTranspilerFormatting.FormatMethod(replacementMethod) +
                         " takes " + actualParameters.Length + " parameter(s), expected " +
                         expectedParameters.Length + " for " + FluentTranspilerFormatting.FormatMethod(sourceMethod);
                return false;
            }

            for (int i = 0; i < expectedParameters.Length; i++)
            {
                if (!IsStackParameterCompatible(expectedParameters[i], actualParameters[i].ParameterType))
                {
                    reason = "replacement parameter " + i + " is " +
                             FormatType(actualParameters[i].ParameterType) +
                             ", expected " + FormatType(expectedParameters[i]);
                    return false;
                }
            }

            if (!IsReturnCompatible(sourceMethod.ReturnType, replacementMethod.ReturnType))
            {
                reason = "replacement returns " + FormatType(replacementMethod.ReturnType) +
                         ", expected " + FormatType(sourceMethod.ReturnType);
                return false;
            }

            return true;
        }

        private static Type[] BuildExpectedReplacementParameters(MethodInfo sourceMethod)
        {
            var expectedParameters = sourceMethod.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToList();

            if (!sourceMethod.IsStatic && sourceMethod.DeclaringType != null)
            {
                expectedParameters.Insert(0, sourceMethod.DeclaringType);
            }

            return expectedParameters.ToArray();
        }

        private static bool IsStackParameterCompatible(Type providedType, Type parameterType)
        {
            bool providedByRef = providedType != null && providedType.IsByRef;
            bool parameterByRef = parameterType != null && parameterType.IsByRef;
            if (providedByRef || parameterByRef)
            {
                return providedByRef &&
                       parameterByRef &&
                       providedType.GetElementType() == parameterType.GetElementType();
            }

            if (providedType == parameterType)
            {
                return true;
            }

            if (providedType == null || parameterType == null)
            {
                return false;
            }

            if (providedType == typeof(void) || parameterType == typeof(void))
            {
                return false;
            }

            if (IsInt32StackType(providedType) && IsInt32StackType(parameterType))
            {
                return true;
            }

            if (providedType.IsValueType || parameterType.IsValueType)
            {
                return false;
            }

            return parameterType.IsAssignableFrom(providedType) ||
                   parameterType.FullName == providedType.FullName;
        }

        private static bool IsReturnCompatible(Type expected, Type actual)
        {
            if (expected == actual)
            {
                return true;
            }

            if (expected == null || actual == null)
            {
                return false;
            }

            if (expected == typeof(void) || actual == typeof(void))
            {
                return expected == actual;
            }

            if (IsInt32StackType(expected) && IsInt32StackType(actual))
            {
                return true;
            }

            return expected.IsAssignableFrom(actual) || expected.FullName == actual.FullName;
        }

        private static bool IsInt32StackType(Type type)
        {
            if (type == null)
            {
                return false;
            }

            if (type.IsEnum)
            {
                type = Enum.GetUnderlyingType(type);
            }

            return type == typeof(int) ||
                   type == typeof(uint) ||
                   type == typeof(short) ||
                   type == typeof(ushort) ||
                   type == typeof(byte) ||
                   type == typeof(sbyte) ||
                   type == typeof(char);
        }

        private static string FormatType(Type type)
        {
            return type != null ? type.FullName : "<null>";
        }

        private static bool IsValidStaticReplacementMethod(FluentTranspiler transpiler, MethodInfo method, string caller)
        {
            return FluentTranspilerRecipeValidation.ValidateStaticMethod(transpiler, method, caller);
        }
    }
}
