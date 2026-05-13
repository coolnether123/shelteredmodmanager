using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace ModAPI.Harmony
{
    /// <summary>
    /// High-level recipes for field and property access patches.
    /// </summary>
    public static class FluentTranspilerFieldRecipes
    {
        /// <summary>
        /// Selects field-read instructions (<c>ldfld</c> or <c>ldsfld</c>).
        /// </summary>
        /// <example>
        /// <code>
        /// t.ForFieldRead(typeof(PlaySettings), "useWorkPriorities")
        ///  .ReplaceWithCall(typeof(PriorityCommandRouter), "GetUseWorkPriorities");
        /// </code>
        /// </example>
        public static FluentFieldReadSelection ForFieldRead(this FluentTranspiler transpiler, Type declaringType, string fieldName)
        {
            return new FluentFieldReadSelection(transpiler, ResolveField(declaringType, fieldName));
        }

        /// <summary>
        /// Selects field-write instructions (<c>stfld</c> or <c>stsfld</c>).
        /// </summary>
        public static FluentFieldWriteSelection ForFieldWrite(this FluentTranspiler transpiler, Type declaringType, string fieldName)
        {
            return new FluentFieldWriteSelection(transpiler, ResolveField(declaringType, fieldName));
        }

        /// <summary>
        /// Selects property getter calls.
        /// </summary>
        /// <example>
        /// <code>
        /// t.ForPropertyGetter(typeof(SomeType), "Value")
        ///  .WrapReturnValue(typeof(MyHooks), "AdjustValue");
        /// </code>
        /// </example>
        public static FluentCallSelection ForPropertyGetter(this FluentTranspiler transpiler, Type declaringType, string propertyName)
        {
            MethodInfo getter = ResolveProperty(declaringType, propertyName)?.GetGetMethod(true);
            return transpiler.ForCall(getter);
        }

        /// <summary>
        /// Selects property setter calls.
        /// </summary>
        public static FluentPropertySetterSelection ForPropertySetter(this FluentTranspiler transpiler, Type declaringType, string propertyName)
        {
            MethodInfo setter = ResolveProperty(declaringType, propertyName)?.GetSetMethod(true);
            return new FluentPropertySetterSelection(transpiler, setter);
        }

        private static FieldInfo ResolveField(Type declaringType, string fieldName)
        {
            return declaringType?.GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        }

        private static PropertyInfo ResolveProperty(Type declaringType, string propertyName)
        {
            return declaringType?.GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        }
    }

    public sealed class FluentFieldReadSelection
    {
        private readonly FluentTranspiler _transpiler;
        private readonly FieldInfo _field;

        internal FluentFieldReadSelection(FluentTranspiler transpiler, FieldInfo field)
        {
            _transpiler = transpiler;
            _field = field;
        }

        /// <summary>
        /// Replaces the first matching field read with a static method call.
        /// </summary>
        public FluentReplacementResult ReplaceWithCall(Type hookType, string hookMethod, SearchMode mode = SearchMode.Start)
        {
            return ReplaceWithCall(FluentRecipeUtility.ResolveStaticMethod(hookType, hookMethod, null), mode);
        }

        /// <summary>
        /// Replaces the first matching field read with a static method call.
        /// For instance fields the hook must accept the field owner and return the field type.
        /// For static fields the hook must be parameterless and return the field type.
        /// </summary>
        public FluentReplacementResult ReplaceWithCall(MethodInfo hookMethod, SearchMode mode = SearchMode.Start)
        {
            if (!ValidateField("ForFieldRead"))
            {
                return FluentReplacementResult.Failed;
            }

            if (!IsCompatibleReadReplacement(hookMethod))
            {
                _transpiler.AddWarning($"ForFieldRead replacement is not stack-compatible with {FluentTranspilerFormatting.FormatField(_field)}: {FluentTranspilerFormatting.FormatMethod(hookMethod)}.");
                return FluentReplacementResult.UnsafeMatch;
            }

            var candidates = FindFieldReadIndexes(mode);
            if (!TrySingle(candidates, "ForFieldRead", out int index))
            {
                return candidates.Count == 0 ? FluentReplacementResult.NoMatch : FluentReplacementResult.AmbiguousMatch;
            }

            _transpiler.ReplaceAtWithCall(index, hookMethod);
            return FluentReplacementResult.PatternReplaced;
        }

        private bool IsCompatibleReadReplacement(MethodInfo hookMethod)
        {
            Type[] stackInputs = _field.IsStatic
                ? new Type[0]
                : new[] { _field.DeclaringType };

            return FluentTranspilerRecipeValidation.ValidateStaticStackSignature(
                _transpiler,
                hookMethod,
                stackInputs,
                _field.FieldType,
                "ForFieldRead.ReplaceWithCall");
        }

        private List<int> FindFieldReadIndexes(SearchMode mode)
        {
            return FindFieldIndexes(mode, instruction =>
                instruction != null &&
                (instruction.opcode == OpCodes.Ldfld || instruction.opcode == OpCodes.Ldsfld) &&
                Equals(instruction.operand, _field));
        }

        private bool ValidateField(string caller)
        {
            if (_transpiler == null)
            {
                return false;
            }

            if (_field == null)
            {
                _transpiler.AddWarning($"{caller} received an unknown field.");
                return false;
            }

            return true;
        }

        private List<int> FindFieldIndexes(SearchMode mode, Func<CodeInstruction, bool> predicate)
        {
            IList<CodeInstruction> instructions = _transpiler.Instructions().ToList();
            int startIndex = FluentRecipeUtility.GetSearchStartIndex(_transpiler, mode);
            var indexes = new List<int>();
            for (int i = startIndex; i < instructions.Count; i++)
            {
                if (predicate(instructions[i]))
                {
                    indexes.Add(i);
                }
            }

            return indexes;
        }

        private bool TrySingle(List<int> indexes, string caller, out int index)
        {
            index = -1;
            if (indexes.Count == 1)
            {
                index = indexes[0];
                return true;
            }

            if (indexes.Count == 0)
            {
                _transpiler.AddSoftFailure($"{caller} found no reads of {FluentTranspilerFormatting.FormatField(_field)}.");
                return false;
            }

            _transpiler.AddWarning($"{caller} found {indexes.Count} reads of {FluentTranspilerFormatting.FormatField(_field)}; refusing ambiguous edit.");
            return false;
        }
    }

    public sealed class FluentFieldWriteSelection
    {
        private readonly FluentTranspiler _transpiler;
        private readonly FieldInfo _field;

        internal FluentFieldWriteSelection(FluentTranspiler transpiler, FieldInfo field)
        {
            _transpiler = transpiler;
            _field = field;
        }

        /// <summary>
        /// Replaces a matching field write with a static interceptor call.
        /// </summary>
        public FluentReplacementResult InterceptWithCall(Type hookType, string hookMethod, SearchMode mode = SearchMode.Start)
        {
            return InterceptWithCall(FluentRecipeUtility.ResolveStaticMethod(hookType, hookMethod, null), mode);
        }

        /// <summary>
        /// Replaces a matching field write with a static interceptor call.
        /// Static-field hooks must be <c>void Hook(T value)</c>; instance-field hooks must be
        /// <c>void Hook(TOwner owner, T value)</c>.
        /// </summary>
        public FluentReplacementResult InterceptWithCall(MethodInfo hookMethod, SearchMode mode = SearchMode.Start)
        {
            if (_transpiler == null)
            {
                return FluentReplacementResult.Failed;
            }

            if (_field == null)
            {
                _transpiler.AddWarning("ForFieldWrite received an unknown field.");
                return FluentReplacementResult.Failed;
            }

            if (!IsCompatibleWriteInterceptor(hookMethod))
            {
                _transpiler.AddWarning($"ForFieldWrite interceptor is not stack-compatible with {FluentTranspilerFormatting.FormatField(_field)}: {FluentTranspilerFormatting.FormatMethod(hookMethod)}.");
                return FluentReplacementResult.UnsafeMatch;
            }

            IList<CodeInstruction> instructions = _transpiler.Instructions().ToList();
            int startIndex = FluentRecipeUtility.GetSearchStartIndex(_transpiler, mode);
            var candidates = new List<int>();
            for (int i = startIndex; i < instructions.Count; i++)
            {
                if (instructions[i] != null &&
                    (instructions[i].opcode == OpCodes.Stfld || instructions[i].opcode == OpCodes.Stsfld) &&
                    Equals(instructions[i].operand, _field))
                {
                    candidates.Add(i);
                }
            }

            if (candidates.Count == 0)
            {
                _transpiler.AddSoftFailure($"ForFieldWrite found no writes to {FluentTranspilerFormatting.FormatField(_field)}.");
                return FluentReplacementResult.NoMatch;
            }

            if (candidates.Count > 1)
            {
                _transpiler.AddWarning($"ForFieldWrite found {candidates.Count} writes to {FluentTranspilerFormatting.FormatField(_field)}; refusing ambiguous edit.");
                return FluentReplacementResult.AmbiguousMatch;
            }

            _transpiler.ReplaceAtWithCall(candidates[0], hookMethod);
            return FluentReplacementResult.PatternReplaced;
        }

        private bool IsCompatibleWriteInterceptor(MethodInfo hookMethod)
        {
            Type[] stackInputs = _field.IsStatic
                ? new[] { _field.FieldType }
                : new[] { _field.DeclaringType, _field.FieldType };

            return FluentTranspilerRecipeValidation.ValidateStaticStackSignature(
                _transpiler,
                hookMethod,
                stackInputs,
                typeof(void),
                "ForFieldWrite.InterceptWithCall");
        }
    }

    public sealed class FluentPropertySetterSelection
    {
        private readonly FluentTranspiler _transpiler;
        private readonly MethodInfo _setter;

        internal FluentPropertySetterSelection(FluentTranspiler transpiler, MethodInfo setter)
        {
            _transpiler = transpiler;
            _setter = setter;
        }

        /// <summary>
        /// Replaces a property setter call with a static interceptor call.
        /// </summary>
        public FluentReplacementResult InterceptWithCall(Type hookType, string hookMethod, SearchMode mode = SearchMode.Start)
        {
            return InterceptWithCall(FluentRecipeUtility.ResolveStaticMethod(hookType, hookMethod, null), mode);
        }

        /// <summary>
        /// Replaces a property setter call with a static interceptor call.
        /// </summary>
        public FluentReplacementResult InterceptWithCall(MethodInfo hookMethod, SearchMode mode = SearchMode.Start)
        {
            if (_setter == null)
            {
                _transpiler.AddWarning("ForPropertySetter received an unknown property setter.");
                return FluentReplacementResult.Failed;
            }

            return _transpiler.ForCall(_setter).ReplaceWith(hookMethod, mode);
        }
    }
}
