using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace ModAPI.Harmony
{
    /// <summary>
    /// Shared validation for high-level FluentTranspiler recipes.
    /// Recipes should fail before mutating IL when hook signatures or replacement ranges are unsafe.
    /// </summary>
    internal static class FluentTranspilerRecipeValidation
    {
        internal static bool ValidateStaticMethod(FluentTranspiler transpiler, MethodInfo method, string caller)
        {
            if (method == null)
            {
                Warn(transpiler, $"{caller} received a null method.");
                return false;
            }

            if (!method.IsStatic)
            {
                Warn(transpiler, $"{caller} expected static method {FluentTranspilerFormatting.FormatMethod(method)}.");
                return false;
            }

            return true;
        }

        internal static bool ValidateParameterCount(FluentTranspiler transpiler, MethodInfo method, int expectedCount, string caller)
        {
            if (method == null)
            {
                Warn(transpiler, $"{caller} received a null method.");
                return false;
            }

            int actualCount = method.GetParameters().Length;
            if (actualCount != expectedCount)
            {
                Warn(transpiler, $"{caller} expected {expectedCount} parameter(s) on {FluentTranspilerFormatting.FormatMethod(method)}, found {actualCount}.");
                return false;
            }

            return true;
        }

        internal static bool ValidateReturnTypeAssignable(
            FluentTranspiler transpiler,
            Type actualReturnType,
            Type expectedReturnType,
            string caller)
        {
            actualReturnType = actualReturnType ?? typeof(void);
            expectedReturnType = expectedReturnType ?? typeof(void);

            if (IsStackAssignable(actualReturnType, expectedReturnType))
            {
                return true;
            }

            Warn(
                transpiler,
                $"{caller} return type mismatch: {FormatType(actualReturnType)} cannot replace {FormatType(expectedReturnType)}.");
            return false;
        }

        internal static bool ValidateReplacementCallSignature(
            FluentTranspiler transpiler,
            MethodInfo originalMethod,
            MethodInfo replacementMethod,
            string caller)
        {
            if (originalMethod == null)
            {
                Warn(transpiler, $"{caller} could not determine the original call being replaced.");
                return false;
            }

            if (!ValidateStaticMethod(transpiler, replacementMethod, caller))
            {
                return false;
            }

            Type[] originalStackInputs = GetCallStackInputTypes(originalMethod);
            ParameterInfo[] replacementParameters = replacementMethod.GetParameters();
            if (replacementParameters.Length != originalStackInputs.Length)
            {
                Warn(
                    transpiler,
                    $"{caller} replacement {FluentTranspilerFormatting.FormatMethod(replacementMethod)} consumes {replacementParameters.Length} stack value(s), but original call {FluentTranspilerFormatting.FormatMethod(originalMethod)} consumes {originalStackInputs.Length}.");
                return false;
            }

            for (int i = 0; i < originalStackInputs.Length; i++)
            {
                Type providedType = originalStackInputs[i];
                Type expectedType = replacementParameters[i].ParameterType;
                if (IsStackAssignable(providedType, expectedType))
                {
                    continue;
                }

                Warn(
                    transpiler,
                    $"{caller} parameter {i} mismatch for {FluentTranspilerFormatting.FormatMethod(replacementMethod)}: stack provides {FormatType(providedType)} from {FluentTranspilerFormatting.FormatMethod(originalMethod)}, hook expects {FormatType(expectedType)}.");
                return false;
            }

            return ValidateReturnTypeAssignable(transpiler, replacementMethod.ReturnType, originalMethod.ReturnType, caller);
        }

        /// <summary>
        /// Validates a call redirect whose replacement takes one extra <b>trailing</b> argument beyond
        /// the original call's stack inputs (e.g. a domain/tag literal pushed just before the call).
        /// The replacement must be static, consume the original stack shape followed by
        /// <paramref name="appendedType"/>, and return a compatible type.
        /// </summary>
        internal static bool ValidateReplacementCallSignatureWithAppended(
            FluentTranspiler transpiler,
            MethodInfo originalMethod,
            MethodInfo replacementMethod,
            Type appendedType,
            string caller)
        {
            if (originalMethod == null)
            {
                Warn(transpiler, $"{caller} could not determine the original call being replaced.");
                return false;
            }

            if (!ValidateStaticMethod(transpiler, replacementMethod, caller))
            {
                return false;
            }

            if (appendedType == null)
            {
                Warn(transpiler, $"{caller} could not determine the type of the appended argument for {FluentTranspilerFormatting.FormatMethod(replacementMethod)}.");
                return false;
            }

            Type[] originalStackInputs = GetCallStackInputTypes(originalMethod);
            ParameterInfo[] replacementParameters = replacementMethod.GetParameters();
            int expectedCount = originalStackInputs.Length + 1;
            if (replacementParameters.Length != expectedCount)
            {
                Warn(
                    transpiler,
                    $"{caller} replacement {FluentTranspilerFormatting.FormatMethod(replacementMethod)} takes {replacementParameters.Length} parameter(s), but redirecting {FluentTranspilerFormatting.FormatMethod(originalMethod)} with one appended argument needs {expectedCount} (its {originalStackInputs.Length} stack input(s) plus a trailing {FormatType(appendedType)}).");
                return false;
            }

            for (int i = 0; i < originalStackInputs.Length; i++)
            {
                if (IsStackAssignable(originalStackInputs[i], replacementParameters[i].ParameterType))
                {
                    continue;
                }

                Warn(
                    transpiler,
                    $"{caller} parameter {i} mismatch for {FluentTranspilerFormatting.FormatMethod(replacementMethod)}: stack provides {FormatType(originalStackInputs[i])} from {FluentTranspilerFormatting.FormatMethod(originalMethod)}, hook expects {FormatType(replacementParameters[i].ParameterType)}.");
                return false;
            }

            Type trailingParam = replacementParameters[expectedCount - 1].ParameterType;
            if (!IsStackAssignable(appendedType, trailingParam))
            {
                Warn(
                    transpiler,
                    $"{caller} trailing parameter mismatch for {FluentTranspilerFormatting.FormatMethod(replacementMethod)}: appended argument is {FormatType(appendedType)}, hook expects {FormatType(trailingParam)}. Fix: make the replacement's last parameter accept {FormatType(appendedType)}.");
                return false;
            }

            return ValidateReturnTypeAssignable(transpiler, replacementMethod.ReturnType, originalMethod.ReturnType, caller);
        }

        internal static bool ValidateWrapperSignature(
            FluentTranspiler transpiler,
            MethodInfo wrapperMethod,
            Type originalReturnType,
            Type adjustedReturnType,
            Type[] contextParameterTypes,
            string caller)
        {
            if (!ValidateStaticMethod(transpiler, wrapperMethod, caller))
            {
                return false;
            }

            if (originalReturnType == typeof(void))
            {
                Warn(transpiler, $"{caller} cannot wrap a void return value.");
                return false;
            }

            Type[] contextTypes = contextParameterTypes ?? new Type[0];
            ParameterInfo[] parameters = wrapperMethod.GetParameters();
            int expectedCount = 1 + contextTypes.Length;
            if (parameters.Length != expectedCount)
            {
                Warn(transpiler, $"{caller} expected wrapper {FluentTranspilerFormatting.FormatMethod(wrapperMethod)} to receive original return value plus {contextTypes.Length} context parameter(s), found {parameters.Length} parameter(s).");
                return false;
            }

            if (!IsStackAssignable(originalReturnType, parameters[0].ParameterType))
            {
                Warn(transpiler, $"{caller} wrapper first parameter must accept original return type {FormatType(originalReturnType)}, found {FormatType(parameters[0].ParameterType)}.");
                return false;
            }

            for (int i = 0; i < contextTypes.Length; i++)
            {
                if (IsStackAssignable(contextTypes[i], parameters[i + 1].ParameterType))
                {
                    continue;
                }

                Warn(transpiler, $"{caller} wrapper context parameter {i} mismatch: source {FormatType(contextTypes[i])}, wrapper expects {FormatType(parameters[i + 1].ParameterType)}.");
                return false;
            }

            Type expectedReturn = adjustedReturnType ?? originalReturnType;
            return ValidateReturnTypeAssignable(transpiler, wrapperMethod.ReturnType, expectedReturn, caller);
        }

        internal static bool ValidateHookCanReceiveOriginalArguments(
            FluentTranspiler transpiler,
            MethodBase originalMethod,
            MethodInfo hookMethod,
            string caller)
        {
            if (!ValidateStaticMethod(transpiler, hookMethod, caller))
            {
                return false;
            }

            if (originalMethod == null)
            {
                Warn(transpiler, $"{caller} requires the enclosing original method to validate hook argument loads.");
                return false;
            }

            Type[] originalArgumentTypes = GetEnclosingArgumentTypes(originalMethod);
            ParameterInfo[] hookParameters = hookMethod.GetParameters();
            if (hookParameters.Length > originalArgumentTypes.Length)
            {
                Warn(transpiler, $"{caller} hook {FluentTranspilerFormatting.FormatMethod(hookMethod)} requests argument index {hookParameters.Length - 1}, but enclosing method only has {originalArgumentTypes.Length} argument slot(s).");
                return false;
            }

            for (int i = 0; i < hookParameters.Length; i++)
            {
                Type providedType = originalArgumentTypes[i];
                Type expectedType = hookParameters[i].ParameterType;
                if (IsStackAssignable(providedType, expectedType))
                {
                    continue;
                }

                Warn(transpiler, $"{caller} hook parameter {i} mismatch: enclosing argument is {FormatType(providedType)}, hook expects {FormatType(expectedType)}.");
                return false;
            }

            return true;
        }

        internal static bool ValidateStaticStackSignature(
            FluentTranspiler transpiler,
            MethodInfo method,
            Type[] stackInputTypes,
            Type expectedReturnType,
            string caller)
        {
            if (!ValidateStaticMethod(transpiler, method, caller))
            {
                return false;
            }

            Type[] inputs = stackInputTypes ?? new Type[0];
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != inputs.Length)
            {
                Warn(
                    transpiler,
                    $"{caller} expected {inputs.Length} stack value(s) for {FluentTranspilerFormatting.FormatMethod(method)}, found {parameters.Length} parameter(s).");
                return false;
            }

            for (int i = 0; i < inputs.Length; i++)
            {
                Type providedType = inputs[i];
                Type expectedType = parameters[i].ParameterType;
                if (IsStackAssignable(providedType, expectedType))
                {
                    continue;
                }

                Warn(
                    transpiler,
                    $"{caller} parameter {i} mismatch for {FluentTranspilerFormatting.FormatMethod(method)}: stack provides {FormatType(providedType)}, hook expects {FormatType(expectedType)}.");
                return false;
            }

            return ValidateReturnTypeAssignable(transpiler, method.ReturnType, expectedReturnType, caller);
        }

        internal static bool ValidateNoUnsupportedValueTypeDefault(FluentTranspiler transpiler, Type returnType, string caller)
        {
            if (returnType == null || returnType == typeof(void) || !returnType.IsValueType)
            {
                return true;
            }

            if (CanEmitDefaultValue(returnType))
            {
                return true;
            }

            Warn(transpiler, $"{caller} cannot safely synthesize a default value for value type {FormatType(returnType)}. Leaving original IL unchanged.");
            return false;
        }

        internal static bool ValidateBranchTargetsOutsideReplacementRange(
            FluentTranspiler transpiler,
            IList<CodeInstruction> instructions,
            int startIndex,
            int removeCount,
            string caller)
        {
            if (instructions == null || removeCount <= 1)
            {
                return true;
            }

            int endExclusive = startIndex + removeCount;
            var labelTargets = BuildLabelTargets(instructions);
            for (int sourceIndex = 0; sourceIndex < instructions.Count; sourceIndex++)
            {
                CodeInstruction instruction = instructions[sourceIndex];
                if (instruction == null)
                {
                    continue;
                }

                foreach (Label label in GetBranchLabels(instruction))
                {
                    int targetIndex;
                    if (!labelTargets.TryGetValue(label, out targetIndex))
                    {
                        continue;
                    }

                    bool sourceOutsideRange = sourceIndex < startIndex || sourceIndex >= endExclusive;
                    bool targetInsideRange = targetIndex >= startIndex && targetIndex < endExclusive;
                    bool targetIsEntry = targetIndex == startIndex;
                    if (sourceOutsideRange && targetInsideRange && !targetIsEntry)
                    {
                        Warn(transpiler, $"{caller} would replace range {startIndex}..{endExclusive - 1}, but branch at index {sourceIndex} targets interior index {targetIndex}. Leaving original IL unchanged.");
                        return false;
                    }
                }
            }

            return true;
        }

        internal static bool ValidateExceptionBlockSafety(
            FluentTranspiler transpiler,
            MethodBase originalMethod,
            int removeCount,
            int insertCount,
            string caller)
        {
            if (!MethodHasExceptionHandlingClauses(originalMethod))
            {
                return true;
            }

            if (removeCount == insertCount)
            {
                return true;
            }

            Warn(transpiler, $"{caller} would change instruction count inside a method with exception handlers. Use exact index-aligned replacement.");
            return false;
        }

        internal static bool ValidateIntCompatibleReturn(FluentTranspiler transpiler, MethodInfo method, string caller)
        {
            if (method == null)
            {
                Warn(transpiler, $"{caller} received a null replacement method.");
                return false;
            }

            if (!ValidateStaticMethod(transpiler, method, caller))
            {
                return false;
            }

            if (IsInt32StackType(method.ReturnType))
            {
                return true;
            }

            Warn(transpiler, $"{caller} expected {FluentTranspilerFormatting.FormatMethod(method)} to return an int-compatible value, found {FormatType(method.ReturnType)}.");
            return false;
        }

        internal static bool CanEmitDefaultValue(Type type)
        {
            if (type == null || type == typeof(void) || !type.IsValueType)
            {
                return true;
            }

            return IsInt32StackType(type)
                || type == typeof(long)
                || type == typeof(ulong)
                || type == typeof(float)
                || type == typeof(double);
        }

        internal static CodeInstruction CreateDefaultValueInstruction(Type type)
        {
            if (type == null || type == typeof(void))
            {
                return null;
            }

            if (!type.IsValueType)
            {
                return new CodeInstruction(OpCodes.Ldnull);
            }

            if (IsInt32StackType(type))
            {
                return new CodeInstruction(OpCodes.Ldc_I4_0);
            }

            if (type == typeof(long) || type == typeof(ulong))
            {
                return new CodeInstruction(OpCodes.Ldc_I8, 0L);
            }

            if (type == typeof(float))
            {
                return new CodeInstruction(OpCodes.Ldc_R4, 0f);
            }

            if (type == typeof(double))
            {
                return new CodeInstruction(OpCodes.Ldc_R8, 0d);
            }

            return null;
        }

        private static Type[] GetCallStackInputTypes(MethodInfo method)
        {
            var inputs = new List<Type>();
            if (!method.IsStatic)
            {
                inputs.Add(method.DeclaringType);
            }

            inputs.AddRange(method.GetParameters().Select(parameter => parameter.ParameterType));
            return inputs.ToArray();
        }

        private static Type[] GetEnclosingArgumentTypes(MethodBase method)
        {
            var inputs = new List<Type>();
            if (!method.IsStatic)
            {
                inputs.Add(method.DeclaringType);
            }

            inputs.AddRange(method.GetParameters().Select(parameter => parameter.ParameterType));
            return inputs.ToArray();
        }

        private static bool IsStackAssignable(Type providedType, Type expectedType)
        {
            bool providedByRef = providedType != null && providedType.IsByRef;
            bool expectedByRef = expectedType != null && expectedType.IsByRef;
            if (providedByRef || expectedByRef)
            {
                if (!providedByRef || !expectedByRef)
                {
                    return false;
                }

                Type providedElement = providedType.GetElementType();
                Type expectedElement = expectedType.GetElementType();
                return providedElement == expectedElement;
            }

            providedType = NormalizeByRef(providedType);
            expectedType = NormalizeByRef(expectedType);

            if (providedType == expectedType)
            {
                return true;
            }

            if (providedType == typeof(void) || expectedType == typeof(void))
            {
                return false;
            }

            if (IsInt32StackType(providedType) && IsInt32StackType(expectedType))
            {
                return true;
            }

            if (providedType.IsValueType || expectedType.IsValueType)
            {
                return false;
            }

            return expectedType.IsAssignableFrom(providedType);
        }

        private static Type NormalizeByRef(Type type)
        {
            if (type == null)
            {
                return typeof(void);
            }

            return type.IsByRef ? type.GetElementType() : type;
        }

        private static bool IsInt32StackType(Type type)
        {
            type = NormalizeByRef(type);
            if (type.IsEnum)
            {
                type = Enum.GetUnderlyingType(type);
            }

            return type == typeof(int)
                || type == typeof(uint)
                || type == typeof(short)
                || type == typeof(ushort)
                || type == typeof(byte)
                || type == typeof(sbyte)
                || type == typeof(char);
        }

        private static Dictionary<Label, int> BuildLabelTargets(IList<CodeInstruction> instructions)
        {
            var result = new Dictionary<Label, int>();
            for (int i = 0; i < instructions.Count; i++)
            {
                CodeInstruction instruction = instructions[i];
                if (instruction == null || instruction.labels == null)
                {
                    continue;
                }

                foreach (Label label in instruction.labels)
                {
                    if (!result.ContainsKey(label))
                    {
                        result.Add(label, i);
                    }
                }
            }

            return result;
        }

        private static IEnumerable<Label> GetBranchLabels(CodeInstruction instruction)
        {
            Label? single;
            if (instruction.Branches(out single) && single.HasValue)
            {
                yield return single.Value;
            }

            var many = instruction.operand as Label[];
            if (many == null)
            {
                yield break;
            }

            for (int i = 0; i < many.Length; i++)
            {
                yield return many[i];
            }
        }

        private static bool MethodHasExceptionHandlingClauses(MethodBase method)
        {
            if (method == null)
            {
                return false;
            }

            try
            {
                MethodBody body = method.GetMethodBody();
                return body != null && body.ExceptionHandlingClauses != null && body.ExceptionHandlingClauses.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private static string FormatType(Type type)
        {
            return type == null ? "<null>" : type.FullName ?? type.Name;
        }

        private static void Warn(FluentTranspiler transpiler, string message)
        {
            if (transpiler != null)
            {
                transpiler.AddWarning(TranspilerDiagnosticCategory.Validation, message);
            }
        }
    }

    internal static class FluentRecipeUtility
    {
        internal static MethodInfo ResolveStaticMethod(Type type, string methodName, Type[] parameterTypes)
        {
            if (type == null || string.IsNullOrEmpty(methodName))
            {
                return null;
            }

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            try
            {
                return parameterTypes != null
                    ? type.GetMethod(methodName, flags, null, parameterTypes, null)
                    : type.GetMethod(methodName, flags);
            }
            catch (AmbiguousMatchException)
            {
                return null;
            }
        }

        /// <summary>
        /// Builds the IL instruction that pushes <paramref name="literal"/> onto the stack, along with
        /// its CLR type (for signature validation). Supports the constant kinds a redirect tag needs:
        /// string, bool, the integral types, char, float, double and long. Returns false for null or
        /// an unsupported type.
        /// </summary>
        internal static bool TryCreateLiteralLoad(object literal, out CodeInstruction load, out Type literalType)
        {
            load = null;
            literalType = null;
            if (literal == null)
            {
                return false;
            }

            literalType = literal.GetType();

            if (literal is string s)
            {
                load = new CodeInstruction(OpCodes.Ldstr, s);
                return true;
            }
            if (literal is bool b)
            {
                load = new CodeInstruction(b ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                return true;
            }
            if (literal is float f)
            {
                load = new CodeInstruction(OpCodes.Ldc_R4, f);
                return true;
            }
            if (literal is double d)
            {
                load = new CodeInstruction(OpCodes.Ldc_R8, d);
                return true;
            }
            if (literal is long l)
            {
                load = new CodeInstruction(OpCodes.Ldc_I8, l);
                return true;
            }
            if (literal is int || literal is short || literal is ushort ||
                literal is byte || literal is sbyte || literal is char)
            {
                load = new CodeInstruction(OpCodes.Ldc_I4, Convert.ToInt32(literal));
                return true;
            }

            return false;
        }

        internal static bool IsStaticParameterlessValueProvider(MethodInfo method)
        {
            return method != null &&
                   method.IsStatic &&
                   method.ReturnType != typeof(void) &&
                   method.GetParameters().Length == 0;
        }

        internal static bool IsCompatibleUnaryWrapper(MethodInfo method, Type valueType)
        {
            if (method == null || valueType == null)
            {
                return false;
            }

            return method.IsStatic &&
                   method.ReturnType == valueType &&
                   method.GetParameters().Length == 1 &&
                   method.GetParameters()[0].ParameterType == valueType;
        }

        internal static int GetSearchStartIndex(FluentTranspiler transpiler, SearchMode mode)
        {
            if (transpiler == null)
            {
                return 0;
            }

            switch (mode)
            {
                case SearchMode.Current:
                    return Math.Max(0, transpiler.CurrentIndex);
                case SearchMode.Next:
                    return Math.Max(0, transpiler.CurrentIndex + 1);
                default:
                    return 0;
            }
        }

        internal static List<int> BuildMeaningfulIndex(IList<CodeInstruction> instructions, int startIndex)
        {
            var indexes = new List<int>();
            if (instructions == null)
            {
                return indexes;
            }

            for (int i = Math.Max(0, startIndex); i < instructions.Count; i++)
            {
                if (!IsIgnorable(instructions[i]))
                {
                    indexes.Add(i);
                }
            }

            return indexes;
        }

        internal static bool IsIgnorable(CodeInstruction instruction)
        {
            return instruction == null ||
                   instruction.opcode == OpCodes.Nop;
        }

        internal static bool TryGetLdcI4Value(CodeInstruction instruction, out int value)
        {
            value = 0;
            if (instruction == null)
            {
                return false;
            }

            try
            {
                if (instruction.opcode == OpCodes.Ldc_I4_M1) { value = -1; return true; }
                if (instruction.opcode == OpCodes.Ldc_I4_0) { value = 0; return true; }
                if (instruction.opcode == OpCodes.Ldc_I4_1) { value = 1; return true; }
                if (instruction.opcode == OpCodes.Ldc_I4_2) { value = 2; return true; }
                if (instruction.opcode == OpCodes.Ldc_I4_3) { value = 3; return true; }
                if (instruction.opcode == OpCodes.Ldc_I4_4) { value = 4; return true; }
                if (instruction.opcode == OpCodes.Ldc_I4_5) { value = 5; return true; }
                if (instruction.opcode == OpCodes.Ldc_I4_6) { value = 6; return true; }
                if (instruction.opcode == OpCodes.Ldc_I4_7) { value = 7; return true; }
                if (instruction.opcode == OpCodes.Ldc_I4_8) { value = 8; return true; }
                if (instruction.opcode == OpCodes.Ldc_I4 || instruction.opcode == OpCodes.Ldc_I4_S)
                {
                    value = Convert.ToInt32(instruction.operand);
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }
    }
}
