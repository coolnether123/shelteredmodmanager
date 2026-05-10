using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace ModAPI.Harmony
{
    /// <summary>
    /// Intent-level operations for common transpiler edits.
    /// These helpers keep mod code close to C# intent and centralize signature and branch safety checks.
    /// </summary>
    public partial class FluentTranspiler
    {
        /// <summary>
        /// Finds exactly one method call. If the call is missing or ambiguous, records diagnostics and invalidates the matcher.
        /// </summary>
        public FluentTranspiler FindUniqueCall(
            Type targetType,
            string targetMethod,
            SearchMode mode = SearchMode.Start,
            Type[] targetParameterTypes = null,
            Type[] genericArguments = null,
            bool includeInherited = true)
        {
            MethodInfo ignored;
            MoveToCallMatch(
                "FindUniqueCall",
                targetType,
                targetMethod,
                mode,
                targetParameterTypes,
                genericArguments,
                includeInherited,
                true,
                out ignored);
            return this;
        }

        /// <summary>
        /// Inserts a static side-effect hook before a known method call.
        /// The hook must be parameterless unless you use <see cref="InsertBeforeCallWithLocals"/>.
        /// </summary>
        public FluentTranspiler InsertBeforeCall(
            Type targetType,
            string targetMethod,
            Type hookType,
            string hookMethod,
            SearchMode mode = SearchMode.Start,
            Type[] targetParameterTypes = null,
            Type[] hookParameterTypes = null,
            bool includeInherited = true,
            bool requireSingleMatch = true)
        {
            return InsertHookNearCall(
                "InsertBeforeCall",
                true,
                targetType,
                targetMethod,
                hookType,
                hookMethod,
                mode,
                targetParameterTypes,
                hookParameterTypes,
                includeInherited,
                requireSingleMatch,
                null);
        }

        /// <summary>
        /// Inserts a static side-effect hook after a known method call.
        /// The hook must be parameterless unless you use <see cref="InsertAfterCallWithLocals"/>.
        /// </summary>
        public FluentTranspiler InsertAfterCall(
            Type targetType,
            string targetMethod,
            Type hookType,
            string hookMethod,
            SearchMode mode = SearchMode.Start,
            Type[] targetParameterTypes = null,
            Type[] hookParameterTypes = null,
            bool includeInherited = true,
            bool requireSingleMatch = true)
        {
            return InsertHookNearCall(
                "InsertAfterCall",
                false,
                targetType,
                targetMethod,
                hookType,
                hookMethod,
                mode,
                targetParameterTypes,
                hookParameterTypes,
                includeInherited,
                requireSingleMatch,
                null);
        }

        /// <summary>
        /// Inserts a static hook before a known call and loads validated local variables as hook arguments.
        /// </summary>
        public FluentTranspiler InsertBeforeCallWithLocals(
            Type targetType,
            string targetMethod,
            Type hookType,
            string hookMethod,
            int[] localIndexes,
            SearchMode mode = SearchMode.Start,
            Type[] targetParameterTypes = null,
            Type[] hookParameterTypes = null,
            bool includeInherited = true,
            bool requireSingleMatch = true)
        {
            return InsertHookNearCall(
                "InsertBeforeCallWithLocals",
                true,
                targetType,
                targetMethod,
                hookType,
                hookMethod,
                mode,
                targetParameterTypes,
                hookParameterTypes,
                includeInherited,
                requireSingleMatch,
                localIndexes);
        }

        /// <summary>
        /// Inserts a static hook after a known call and loads validated local variables as hook arguments.
        /// </summary>
        public FluentTranspiler InsertAfterCallWithLocals(
            Type targetType,
            string targetMethod,
            Type hookType,
            string hookMethod,
            int[] localIndexes,
            SearchMode mode = SearchMode.Start,
            Type[] targetParameterTypes = null,
            Type[] hookParameterTypes = null,
            bool includeInherited = true,
            bool requireSingleMatch = true)
        {
            return InsertHookNearCall(
                "InsertAfterCallWithLocals",
                false,
                targetType,
                targetMethod,
                hookType,
                hookMethod,
                mode,
                targetParameterTypes,
                hookParameterTypes,
                includeInherited,
                requireSingleMatch,
                localIndexes);
        }

        /// <summary>
        /// Replaces a matched method call with a compatible static replacement method.
        /// Instance calls are supported when the replacement accepts the instance as its first parameter.
        /// </summary>
        public FluentTranspiler ReplaceMethodCall(
            Type originalType,
            string originalMethod,
            Type replacementType,
            string replacementMethod,
            SearchMode mode = SearchMode.Start,
            Type[] originalParameterTypes = null,
            Type[] replacementParameterTypes = null,
            bool includeInherited = true,
            bool requireSingleMatch = true)
        {
            MethodInfo original;
            if (!MoveToCallMatch(
                "ReplaceMethodCall",
                originalType,
                originalMethod,
                mode,
                originalParameterTypes,
                null,
                includeInherited,
                requireSingleMatch,
                out original))
            {
                return this;
            }

            MethodInfo replacement = ResolveStaticIntentMethod(
                "ReplaceMethodCall",
                replacementType,
                replacementMethod,
                replacementParameterTypes);
            if (replacement == null) return this;

            if (!ValidateReplacementSignature(original, replacement, "ReplaceMethodCall"))
                return this;

            return ReplaceWith(OpCodes.Call, replacement);
        }

        /// <summary>
        /// Replaces every compatible occurrence of a method call with a static replacement method.
        /// All matches are validated before any instruction is changed.
        /// </summary>
        public FluentTranspiler ReplaceMethodCallAll(
            Type originalType,
            string originalMethod,
            Type replacementType,
            string replacementMethod,
            Type[] originalParameterTypes = null,
            Type[] replacementParameterTypes = null,
            bool includeInherited = true)
        {
            MethodInfo replacement = ResolveStaticIntentMethod(
                "ReplaceMethodCallAll",
                replacementType,
                replacementMethod,
                replacementParameterTypes);
            if (replacement == null) return this;

            var positions = FindCallIndexes(
                originalType,
                originalMethod,
                SearchMode.Start,
                originalParameterTypes,
                null,
                includeInherited);

            if (positions.Count == 0)
            {
                AddIntentWarning("ReplaceMethodCallAll", BuildPatternNotFoundMessage(originalType, originalMethod, originalParameterTypes));
                InvalidateMatcher();
                return this;
            }

            var instructions = _matcher.Instructions();
            for (int i = 0; i < positions.Count; i++)
            {
                var original = instructions[positions[i]].operand as MethodInfo;
                if (original == null || !ValidateReplacementSignature(original, replacement, "ReplaceMethodCallAll"))
                    return this;
            }

            for (int i = 0; i < positions.Count; i++)
            {
                MoveTo(positions[i]);
                ReplaceWith(OpCodes.Call, replacement);
            }

            return this;
        }

        /// <summary>
        /// Wraps every return value with a static method. For void methods, inserts a void hook before each return.
        /// Non-void wrappers must accept the original return type and return a compatible type.
        /// </summary>
        public FluentTranspiler WrapReturnValue(
            Type wrapperType,
            string wrapperMethod,
            Type[] wrapperParameterTypes = null)
        {
            const string operation = "WrapReturnValue";
            if (_originalMethod == null)
            {
                AddCriticalIntentWarning(operation, "Original MethodBase is required so return type and stack shape can be validated.");
                return this;
            }
            if (HasStructuralEditRisk(operation)) return this;

            MethodInfo wrapper = ResolveStaticIntentMethod(operation, wrapperType, wrapperMethod, wrapperParameterTypes);
            if (wrapper == null) return this;

            Type returnType = GetMethodReturnType(_originalMethod);
            var wrapperParameters = wrapper.GetParameters();
            if (returnType == typeof(void))
            {
                if (wrapperParameters.Length != 0 || wrapper.ReturnType != typeof(void))
                {
                    AddCriticalIntentWarning(operation, "Invalid wrapper signature. Void methods require a static void wrapper with no parameters.");
                    return this;
                }
            }
            else
            {
                if (wrapperParameters.Length != 1
                    || !IsParameterCompatible(returnType, wrapperParameters[0].ParameterType)
                    || !IsReturnCompatible(wrapper.ReturnType, returnType))
                {
                    AddCriticalIntentWarning(
                        operation,
                        "Invalid wrapper signature. Expected static " + FormatType(returnType) + " "
                        + wrapperMethod + "(" + FormatType(returnType) + ") or a compatible reference-type variant.");
                    return this;
                }
            }

            var instructions = _matcher.Instructions();
            var returnIndexes = new List<int>();
            for (int i = 0; i < instructions.Count; i++)
            {
                if (instructions[i].opcode == OpCodes.Ret)
                    returnIndexes.Add(i);
            }

            if (returnIndexes.Count == 0)
            {
                AddIntentWarning(operation, "Pattern not found: no ret instruction exists in the current method body.");
                InvalidateMatcher();
                return this;
            }

            for (int i = returnIndexes.Count - 1; i >= 0; i--)
            {
                MoveTo(returnIndexes[i]);
                if (HasCurrentBlockBoundaryRisk(operation)) return this;
                InsertBefore(new CodeInstruction(OpCodes.Call, wrapper));
            }

            return this;
        }

        /// <summary>
        /// Guards a known method call behind a static bool guard. If the guard blocks the call,
        /// the helper pops the original call arguments and pushes a default return value when needed.
        /// </summary>
        public FluentTranspiler InjectGuardBeforeCall(
            Type targetType,
            string targetMethod,
            Type guardType,
            string guardMethod,
            SearchMode mode = SearchMode.Start,
            Type[] targetParameterTypes = null,
            Type[] guardParameterTypes = null,
            bool includeInherited = true,
            bool executeWhenTrue = true,
            bool requireSingleMatch = true)
        {
            const string operation = "InjectGuardBeforeCall";
            if (_generator == null)
            {
                AddCriticalIntentWarning(operation, "ILGenerator is required for guard branch labels. Use a transpiler signature that accepts ILGenerator.");
                return this;
            }
            if (HasStructuralEditRisk(operation)) return this;

            MethodInfo target;
            if (!MoveToCallMatch(
                operation,
                targetType,
                targetMethod,
                mode,
                targetParameterTypes,
                null,
                includeInherited,
                requireSingleMatch,
                out target))
            {
                return this;
            }
            if (HasCurrentBlockBoundaryRisk(operation)) return this;

            MethodInfo guard = ResolveStaticIntentMethod(operation, guardType, guardMethod, guardParameterTypes);
            if (guard == null) return this;
            if (guard.ReturnType != typeof(bool) || guard.GetParameters().Length != 0)
            {
                AddCriticalIntentWarning(operation, "Invalid guard signature. Guard methods must be static bool methods with no parameters.");
                return this;
            }

            int callIndex = _matcher.Pos;
            Label doCall = _generator.DefineLabel();
            Label afterCall = _generator.DefineLabel();

            var prefix = new List<CodeInstruction>();
            prefix.Add(new CodeInstruction(OpCodes.Call, guard));
            prefix.Add(new CodeInstruction(executeWhenTrue ? OpCodes.Brtrue : OpCodes.Brfalse, doCall));
            AddPopInstructions(prefix, GetCallArgumentCount(target));
            AddDefaultValueInstructions(prefix, target.ReturnType, operation);
            prefix.Add(new CodeInstruction(OpCodes.Br, afterCall));

            InsertBefore(prefix.ToArray());

            int shiftedCallIndex = callIndex + prefix.Count;
            MoveTo(shiftedCallIndex);
            AddLabel(doCall);

            var currentInstructions = _matcher.Instructions();
            if (shiftedCallIndex + 1 < currentInstructions.Count)
            {
                MoveTo(shiftedCallIndex + 1);
                AddLabel(afterCall);
            }
            else
            {
                MoveTo(shiftedCallIndex);
                InsertAfter(new CodeInstruction(OpCodes.Nop));
                MoveTo(shiftedCallIndex + 1);
                AddLabel(afterCall);
            }

            MoveTo(shiftedCallIndex);
            return this;
        }

        private FluentTranspiler InsertHookNearCall(
            string operation,
            bool before,
            Type targetType,
            string targetMethod,
            Type hookType,
            string hookMethod,
            SearchMode mode,
            Type[] targetParameterTypes,
            Type[] hookParameterTypes,
            bool includeInherited,
            bool requireSingleMatch,
            int[] localIndexes)
        {
            if (HasStructuralEditRisk(operation)) return this;

            MethodInfo target;
            if (!MoveToCallMatch(
                operation,
                targetType,
                targetMethod,
                mode,
                targetParameterTypes,
                null,
                includeInherited,
                requireSingleMatch,
                out target))
            {
                return this;
            }
            if (HasCurrentBlockBoundaryRisk(operation)) return this;

            MethodInfo hook = ResolveStaticIntentMethod(operation, hookType, hookMethod, hookParameterTypes);
            if (hook == null) return this;

            List<CodeInstruction> hookInstructions;
            if (!TryBuildSideEffectHookInstructions(operation, hook, localIndexes, out hookInstructions))
                return this;

            if (before)
                return InsertBefore(hookInstructions.ToArray());

            return InsertAfter(hookInstructions.ToArray());
        }

        private bool MoveToCallMatch(
            string operation,
            Type targetType,
            string targetMethod,
            SearchMode mode,
            Type[] targetParameterTypes,
            Type[] genericArguments,
            bool includeInherited,
            bool requireSingleMatch,
            out MethodInfo matchedMethod)
        {
            matchedMethod = null;
            var positions = FindCallIndexes(targetType, targetMethod, mode, targetParameterTypes, genericArguments, includeInherited);
            if (positions.Count == 0)
            {
                AddIntentWarning(operation, BuildPatternNotFoundMessage(targetType, targetMethod, targetParameterTypes));
                InvalidateMatcher();
                return false;
            }
            if (requireSingleMatch && positions.Count > 1)
            {
                AddCriticalIntentWarning(
                    operation,
                    "Pattern found multiple times: call " + FormatMethodPattern(targetType, targetMethod, targetParameterTypes)
                    + " matched " + positions.Count + " locations. Use a stronger overload, SearchMode.Next, or requireSingleMatch=false.");
                InvalidateMatcher();
                return false;
            }

            MoveTo(positions[0]);
            matchedMethod = _matcher.Instruction.operand as MethodInfo;
            if (matchedMethod == null)
            {
                AddCriticalIntentWarning(operation, "Matched instruction is not a MethodInfo call operand.");
                return false;
            }
            return true;
        }

        private List<int> FindCallIndexes(
            Type targetType,
            string targetMethod,
            SearchMode mode,
            Type[] targetParameterTypes,
            Type[] genericArguments,
            bool includeInherited)
        {
            var result = new List<int>();
            if (targetType == null || string.IsNullOrEmpty(targetMethod))
                return result;

            var predicate = BuildCallPredicate(targetType, targetMethod, targetParameterTypes, genericArguments, includeInherited);
            var instructions = _matcher.Instructions();
            int start = 0;
            if (mode == SearchMode.Current && _matcher.IsValid)
                start = Math.Max(0, _matcher.Pos);
            else if (mode == SearchMode.Next && _matcher.IsValid)
                start = Math.Max(0, _matcher.Pos + 1);

            for (int i = start; i < instructions.Count; i++)
            {
                if (predicate(instructions[i]))
                    result.Add(i);
            }

            return result;
        }

        private MethodInfo ResolveStaticIntentMethod(string operation, Type type, string methodName, Type[] parameterTypes)
        {
            if (type == null || string.IsNullOrEmpty(methodName))
            {
                AddCriticalIntentWarning(operation, "Method lookup failed because type or method name is null.");
                return null;
            }

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            MethodInfo method = null;
            try
            {
                method = parameterTypes != null
                    ? type.GetMethod(methodName, flags, null, parameterTypes, null)
                    : type.GetMethod(methodName, flags);
            }
            catch (AmbiguousMatchException)
            {
                AddCriticalIntentWarning(operation, "Method " + type.Name + "." + methodName + " is ambiguous. Supply parameter types.");
                return null;
            }

            if (method == null)
            {
                AddCriticalIntentWarning(operation, "Method " + type.Name + "." + methodName + " was not found.");
                return null;
            }
            if (!method.IsStatic)
            {
                AddCriticalIntentWarning(operation, "Method " + type.Name + "." + methodName + " must be static.");
                return null;
            }

            return method;
        }

        private bool TryBuildSideEffectHookInstructions(
            string operation,
            MethodInfo hook,
            int[] localIndexes,
            out List<CodeInstruction> instructions)
        {
            instructions = new List<CodeInstruction>();
            var parameters = hook.GetParameters();
            if (localIndexes == null)
            {
                if (parameters.Length != 0)
                {
                    AddCriticalIntentWarning(operation, "Stack risk detected: hook " + hook.Name + " requires parameters. Use the local-capture overload or a parameterless hook.");
                    return false;
                }
            }
            else
            {
                if (parameters.Length != localIndexes.Length)
                {
                    AddCriticalIntentWarning(operation, "Invalid local capture signature. Hook parameter count must match localIndexes length.");
                    return false;
                }

                for (int i = 0; i < localIndexes.Length; i++)
                {
                    Type localType;
                    if (!TryGetLocalType(localIndexes[i], out localType))
                    {
                        AddCriticalIntentWarning(operation, "Local variable capture failed. Local index " + localIndexes[i] + " does not exist on the original method.");
                        return false;
                    }
                    if (!IsParameterCompatible(localType, parameters[i].ParameterType))
                    {
                        AddCriticalIntentWarning(
                            operation,
                            "Invalid local capture signature. Local " + localIndexes[i] + " has type "
                            + FormatType(localType) + " but hook parameter " + i + " expects "
                            + FormatType(parameters[i].ParameterType) + ".");
                        return false;
                    }
                    instructions.Add(CreateLoadLocalInstruction(localIndexes[i]));
                }
            }

            instructions.Add(new CodeInstruction(OpCodes.Call, hook));
            if (hook.ReturnType != typeof(void))
                instructions.Add(new CodeInstruction(OpCodes.Pop));

            return true;
        }

        private bool ValidateReplacementSignature(MethodInfo original, MethodInfo replacement, string operation)
        {
            if (original == null || replacement == null)
            {
                AddCriticalIntentWarning(operation, "Invalid replacement signature. Original or replacement method is null.");
                return false;
            }

            var expectedStack = new List<Type>();
            if (!original.IsStatic)
                expectedStack.Add(original.DeclaringType);
            var originalParameters = original.GetParameters();
            for (int i = 0; i < originalParameters.Length; i++)
                expectedStack.Add(originalParameters[i].ParameterType);

            var replacementParameters = replacement.GetParameters();
            if (replacementParameters.Length != expectedStack.Count)
            {
                AddCriticalIntentWarning(
                    operation,
                    "Invalid replacement signature. " + original.Name + " leaves " + expectedStack.Count
                    + " argument(s) on the stack, but " + replacement.Name + " accepts "
                    + replacementParameters.Length + ".");
                return false;
            }

            for (int i = 0; i < expectedStack.Count; i++)
            {
                if (!IsParameterCompatible(expectedStack[i], replacementParameters[i].ParameterType))
                {
                    AddCriticalIntentWarning(
                        operation,
                        "Invalid replacement signature. Stack argument " + i + " is "
                        + FormatType(expectedStack[i]) + " but replacement expects "
                        + FormatType(replacementParameters[i].ParameterType) + ".");
                    return false;
                }
            }

            if (!IsReturnCompatible(replacement.ReturnType, original.ReturnType))
            {
                AddCriticalIntentWarning(
                    operation,
                    "Invalid replacement signature. Replacement returns " + FormatType(replacement.ReturnType)
                    + " but original call returns " + FormatType(original.ReturnType) + ".");
                return false;
            }

            return true;
        }

        private bool TryGetLocalType(int localIndex, out Type localType)
        {
            localType = null;
            if (localIndex < 0 || _originalMethod == null) return false;

            try
            {
                MethodBody body = _originalMethod.GetMethodBody();
                if (body == null || body.LocalVariables == null) return false;
                if (localIndex >= body.LocalVariables.Count) return false;
                localType = body.LocalVariables[localIndex].LocalType;
                return localType != null;
            }
            catch
            {
                return false;
            }
        }

        private CodeInstruction CreateLoadLocalInstruction(int localIndex)
        {
            switch (localIndex)
            {
                case 0: return new CodeInstruction(OpCodes.Ldloc_0);
                case 1: return new CodeInstruction(OpCodes.Ldloc_1);
                case 2: return new CodeInstruction(OpCodes.Ldloc_2);
                case 3: return new CodeInstruction(OpCodes.Ldloc_3);
                default: return new CodeInstruction(OpCodes.Ldloc, localIndex);
            }
        }

        private bool HasStructuralEditRisk(string operation)
        {
            if (MethodHasExceptionHandlingClauses())
            {
                AddCriticalIntentWarning(operation, "Branch/label risk detected: structural insertions are blocked on methods with exception handlers. Use exact replacement helpers instead.");
                return true;
            }
            return false;
        }

        private bool HasCurrentBlockBoundaryRisk(string operation)
        {
            if (!_matcher.IsValid || _matcher.Instruction == null) return false;
            var blocks = _matcher.Instruction.blocks;
            if (blocks != null && blocks.Count > 0)
            {
                AddCriticalIntentWarning(operation, "Branch/label risk detected: target instruction carries exception block markers; structural insertion was aborted.");
                return true;
            }
            return false;
        }

        private void AddPopInstructions(List<CodeInstruction> instructions, int count)
        {
            for (int i = 0; i < count; i++)
                instructions.Add(new CodeInstruction(OpCodes.Pop));
        }

        private void AddDefaultValueInstructions(List<CodeInstruction> instructions, Type returnType, string operation)
        {
            if (returnType == null || returnType == typeof(void)) return;

            if (!returnType.IsValueType)
            {
                instructions.Add(new CodeInstruction(OpCodes.Ldnull));
                return;
            }
            if (IsInt32StackType(returnType))
            {
                instructions.Add(new CodeInstruction(OpCodes.Ldc_I4_0));
                return;
            }
            if (returnType == typeof(long) || returnType == typeof(ulong))
            {
                instructions.Add(new CodeInstruction(OpCodes.Ldc_I8, 0L));
                return;
            }
            if (returnType == typeof(float))
            {
                instructions.Add(new CodeInstruction(OpCodes.Ldc_R4, 0f));
                return;
            }
            if (returnType == typeof(double))
            {
                instructions.Add(new CodeInstruction(OpCodes.Ldc_R8, 0d));
                return;
            }

            if (_generator == null)
            {
                AddCriticalIntentWarning(operation, "Stack risk detected: cannot create a default value for " + FormatType(returnType) + " without ILGenerator.");
                return;
            }

            LocalBuilder local = _generator.DeclareLocal(returnType);
            instructions.Add(new CodeInstruction(OpCodes.Ldloca, local));
            instructions.Add(new CodeInstruction(OpCodes.Initobj, returnType));
            instructions.Add(new CodeInstruction(OpCodes.Ldloc, local));
        }

        private static int GetCallArgumentCount(MethodInfo method)
        {
            if (method == null) return 0;
            return method.GetParameters().Length + (method.IsStatic ? 0 : 1);
        }

        private static Type GetMethodReturnType(MethodBase method)
        {
            MethodInfo methodInfo = method as MethodInfo;
            return methodInfo != null ? methodInfo.ReturnType : typeof(void);
        }

        private static bool IsParameterCompatible(Type stackType, Type parameterType)
        {
            if (stackType == null || parameterType == null) return false;
            if (stackType == parameterType) return true;
            if (stackType.IsByRef || parameterType.IsByRef) return stackType == parameterType;
            if (stackType.IsValueType || parameterType.IsValueType) return IsSameEnumOrPrimitiveStackType(stackType, parameterType);
            return parameterType.IsAssignableFrom(stackType);
        }

        private static bool IsReturnCompatible(Type replacementReturn, Type originalReturn)
        {
            if (replacementReturn == null || originalReturn == null) return false;
            if (replacementReturn == originalReturn) return true;
            if (originalReturn == typeof(void) || replacementReturn == typeof(void)) return false;
            if (replacementReturn.IsByRef || originalReturn.IsByRef) return replacementReturn == originalReturn;
            if (replacementReturn.IsValueType || originalReturn.IsValueType) return IsSameEnumOrPrimitiveStackType(replacementReturn, originalReturn);
            return originalReturn.IsAssignableFrom(replacementReturn);
        }

        private static bool IsSameEnumOrPrimitiveStackType(Type first, Type second)
        {
            Type a = first.IsEnum ? Enum.GetUnderlyingType(first) : first;
            Type b = second.IsEnum ? Enum.GetUnderlyingType(second) : second;
            return a == b;
        }

        private static bool IsInt32StackType(Type type)
        {
            Type normalized = type.IsEnum ? Enum.GetUnderlyingType(type) : type;
            return normalized == typeof(bool)
                || normalized == typeof(byte)
                || normalized == typeof(sbyte)
                || normalized == typeof(short)
                || normalized == typeof(ushort)
                || normalized == typeof(char)
                || normalized == typeof(int)
                || normalized == typeof(uint);
        }

        private void InvalidateMatcher()
        {
            _matcher.Start();
            _matcher.MatchStartForward(new CodeMatch(delegate(CodeInstruction instruction) { return false; }));
        }

        private void AddIntentWarning(string operation, string message)
        {
            _warnings.Add(operation + ": " + message);
        }

        private void AddCriticalIntentWarning(string operation, string message)
        {
            _warnings.Add("[CRITICAL SAFETY] " + operation + ": " + message);
        }

        private static string BuildPatternNotFoundMessage(Type type, string methodName, Type[] parameterTypes)
        {
            return "Pattern not found: call " + FormatMethodPattern(type, methodName, parameterTypes) + ".";
        }

        private static string FormatMethodPattern(Type type, string methodName, Type[] parameterTypes)
        {
            string typeName = type != null ? type.Name : "<null-type>";
            string parameters = parameterTypes == null
                ? string.Empty
                : "(" + string.Join(", ", parameterTypes.Select(FormatType).ToArray()) + ")";
            return typeName + "." + (methodName ?? "<null-method>") + parameters;
        }

        private static string FormatType(Type type)
        {
            if (type == null) return "<null>";
            return type.Name;
        }
    }
}
