using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace ModAPI.Harmony
{
    /// <summary>
    /// High-level recipes for method return value patches.
    /// </summary>
    public static class FluentTranspilerReturnRecipes
    {
        /// <summary>
        /// Selects return values of the specified type.
        /// </summary>
        /// <example>
        /// <code>
        /// t.Returns&lt;int&gt;()
        ///  .WrapAll(typeof(MyHooks), "AdjustReturn");
        /// </code>
        /// </example>
        public static FluentReturnSelection<T> Returns<T>(this FluentTranspiler transpiler)
        {
            return new FluentReturnSelection<T>(transpiler);
        }
    }

    public sealed class FluentReturnSelection<T>
    {
        private readonly FluentTranspiler _transpiler;
        private readonly Type _returnType = typeof(T);

        internal FluentReturnSelection(FluentTranspiler transpiler)
        {
            _transpiler = transpiler;
        }

        /// <summary>
        /// Wraps every return value with a static unary method.
        /// </summary>
        public FluentReplacementResult WrapAll(Type wrapperType, string wrapperMethod)
        {
            return WrapAll(FluentRecipeUtility.ResolveStaticMethod(wrapperType, wrapperMethod, null));
        }

        /// <summary>
        /// Wraps every return value with a static unary method.
        /// </summary>
        public FluentReplacementResult WrapAll(MethodInfo wrapperMethod)
        {
            if (_transpiler == null)
            {
                return FluentReplacementResult.Failed;
            }

            if (!FluentTranspilerRecipeValidation.ValidateWrapperSignature(
                _transpiler,
                wrapperMethod,
                _returnType,
                _returnType,
                null,
                $"Returns<{_returnType.Name}>.WrapAll"))
            {
                return FluentReplacementResult.UnsafeMatch;
            }

            IList<int> returns = FindReturnIndexes();
            if (returns.Count == 0)
            {
                _transpiler.AddSoftFailure($"Returns<{_returnType.Name}>.WrapAll found no return instructions.");
                return FluentReplacementResult.NoMatch;
            }

            for (int i = returns.Count - 1; i >= 0; i--)
            {
                _transpiler.MoveTo(returns[i]).InsertBefore(new CodeInstruction(OpCodes.Call, wrapperMethod));
            }

            return FluentReplacementResult.PatternReplaced;
        }

        /// <summary>
        /// Replaces returns of a constant value with a static provider call.
        /// </summary>
        /// <example>
        /// <code>
        /// t.Returns&lt;bool&gt;()
        ///  .ReplaceConstant(false, typeof(MyHooks), "ShouldReturnTrue");
        /// </code>
        /// </example>
        public FluentReplacementResult ReplaceConstant(bool oldValue, Type providerType, string providerMethod)
        {
            return ReplaceConstant(oldValue ? 1 : 0, FluentRecipeUtility.ResolveStaticMethod(providerType, providerMethod, null));
        }

        /// <summary>
        /// Replaces returns of a constant integer value with a static provider call.
        /// </summary>
        public FluentReplacementResult ReplaceConstant(int oldValue, Type providerType, string providerMethod)
        {
            return ReplaceConstant(oldValue, FluentRecipeUtility.ResolveStaticMethod(providerType, providerMethod, null));
        }

        /// <summary>
        /// Replaces returns of a constant integer or boolean value with a static provider call.
        /// </summary>
        public FluentReplacementResult ReplaceConstant(int oldValue, MethodInfo providerMethod)
        {
            if (_transpiler == null)
            {
                return FluentReplacementResult.Failed;
            }

            if (!FluentTranspilerRecipeValidation.ValidateStaticMethod(
                    _transpiler,
                    providerMethod,
                    $"Returns<{_returnType.Name}>.ReplaceConstant") ||
                !FluentTranspilerRecipeValidation.ValidateParameterCount(
                    _transpiler,
                    providerMethod,
                    0,
                    $"Returns<{_returnType.Name}>.ReplaceConstant") ||
                !FluentTranspilerRecipeValidation.ValidateReturnTypeAssignable(
                    _transpiler,
                    providerMethod.ReturnType,
                    _returnType,
                    $"Returns<{_returnType.Name}>.ReplaceConstant"))
            {
                return FluentReplacementResult.UnsafeMatch;
            }

            IList<CodeInstruction> instructions = _transpiler.Instructions().ToList();
            var matches = new List<int>();
            for (int i = 0; i < instructions.Count; i++)
            {
                if (instructions[i] == null || instructions[i].opcode != OpCodes.Ret)
                {
                    continue;
                }

                int valueIndex = FindPreviousMeaningfulIndex(instructions, i - 1);
                int value;
                if (valueIndex >= 0 &&
                    FluentRecipeUtility.TryGetLdcI4Value(instructions[valueIndex], out value) &&
                    value == oldValue)
                {
                    matches.Add(valueIndex);
                }
            }

            if (matches.Count == 0)
            {
                _transpiler.AddSoftFailure($"Returns<{_returnType.Name}>.ReplaceConstant found no constant return {oldValue}.");
                return FluentReplacementResult.NoMatch;
            }

            for (int i = matches.Count - 1; i >= 0; i--)
            {
                _transpiler.ReplaceAtWithCall(matches[i], providerMethod);
            }

            return FluentReplacementResult.PatternReplaced;
        }

        /// <summary>
        /// Inserts a parameterless static void guard immediately before every return.
        /// </summary>
        public FluentReplacementResult InsertGuardBeforeReturn(Type guardType, string guardMethod)
        {
            return InsertGuardBeforeReturn(FluentRecipeUtility.ResolveStaticMethod(guardType, guardMethod, null));
        }

        /// <summary>
        /// Inserts a parameterless static void guard immediately before every return.
        /// </summary>
        public FluentReplacementResult InsertGuardBeforeReturn(MethodInfo guardMethod)
        {
            if (_transpiler == null)
            {
                return FluentReplacementResult.Failed;
            }

            if (guardMethod == null ||
                !guardMethod.IsStatic ||
                guardMethod.ReturnType != typeof(void) ||
                guardMethod.GetParameters().Length != 0)
            {
                _transpiler.AddWarning($"Returns<{_returnType.Name}>.InsertGuardBeforeReturn expected a parameterless static void guard, got {FluentTranspilerFormatting.FormatMethod(guardMethod)}.");
                return FluentReplacementResult.UnsafeMatch;
            }

            IList<int> returns = FindReturnIndexes();
            if (returns.Count == 0)
            {
                _transpiler.AddSoftFailure($"Returns<{_returnType.Name}>.InsertGuardBeforeReturn found no return instructions.");
                return FluentReplacementResult.NoMatch;
            }

            for (int i = returns.Count - 1; i >= 0; i--)
            {
                _transpiler.MoveTo(returns[i]).InsertBefore(new CodeInstruction(OpCodes.Call, guardMethod));
            }

            return FluentReplacementResult.PatternReplaced;
        }

        private IList<int> FindReturnIndexes()
        {
            IList<CodeInstruction> instructions = _transpiler.Instructions().ToList();
            var indexes = new List<int>();
            for (int i = 0; i < instructions.Count; i++)
            {
                if (instructions[i] != null && instructions[i].opcode == OpCodes.Ret)
                {
                    indexes.Add(i);
                }
            }

            return indexes;
        }

        private static int FindPreviousMeaningfulIndex(IList<CodeInstruction> instructions, int startIndex)
        {
            for (int i = startIndex; i >= 0; i--)
            {
                if (!FluentRecipeUtility.IsIgnorable(instructions[i]))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
