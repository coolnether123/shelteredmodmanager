using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace ModAPI.Harmony
{
    /// <summary>
    /// High-level recipes for common clamp calls such as <c>Mathf.Clamp(arg, lower, upper)</c>.
    /// </summary>
    public static class FluentTranspilerClampRecipes
    {
        /// <summary>
        /// Matches a clamp call fed by the selected argument and literal lower/upper bounds.
        /// </summary>
        /// <example>
        /// <code>
        /// t.ForArgument(1)
        ///  .InClamp(0, 4)
        ///  .ReplaceUpperBoundWithCall(getMaxPriority);
        /// </code>
        /// </example>
        public static FluentClampSelection InClamp(this FluentArgumentSelection argument, int lowerBound, int upperBound)
        {
            return new FluentClampSelection(argument, lowerBound, upperBound);
        }
    }

    public sealed class FluentClampSelection
    {
        private readonly FluentArgumentSelection _argumentSelection;
        private readonly int _lowerBound;
        private readonly int _upperBound;

        internal FluentClampSelection(FluentArgumentSelection argumentSelection, int lowerBound, int upperBound)
        {
            _argumentSelection = argumentSelection;
            _lowerBound = lowerBound;
            _upperBound = upperBound;
        }

        /// <summary>
        /// Replaces the clamp upper-bound constant with a parameterless static value provider.
        /// </summary>
        public FluentReplacementResult ReplaceUpperBoundWithCall(MethodInfo replacementMethod, SearchMode mode = SearchMode.Start)
        {
            FluentTranspiler transpiler;
            int argumentIndex;
            if (!TryReadArgumentSelection(out transpiler, out argumentIndex))
            {
                return FluentReplacementResult.Failed;
            }

            if (!FluentTranspilerRecipeValidation.ValidateIntCompatibleReturn(
                    transpiler,
                    replacementMethod,
                    nameof(ReplaceUpperBoundWithCall)) ||
                !FluentTranspilerRecipeValidation.ValidateParameterCount(
                    transpiler,
                    replacementMethod,
                    0,
                    nameof(ReplaceUpperBoundWithCall)))
            {
                return FluentReplacementResult.UnsafeMatch;
            }

            IList<CodeInstruction> instructions = transpiler.Instructions().ToList();
            int startIndex = FluentRecipeUtility.GetSearchStartIndex(transpiler, mode);
            var candidates = FindClampMatches(instructions, argumentIndex, startIndex).ToList();

            if (candidates.Count == 0)
            {
                transpiler.AddSoftFailure($"InClamp({_lowerBound}, {_upperBound}) found no clamp call for argument {argumentIndex}.");
                return FluentReplacementResult.NoMatch;
            }

            if (candidates.Count > 1)
            {
                transpiler.AddWarning($"InClamp({_lowerBound}, {_upperBound}) matched {candidates.Count} clamp calls for argument {argumentIndex}; refusing ambiguous edit.");
                return FluentReplacementResult.AmbiguousMatch;
            }

            transpiler.ReplaceAtWithCall(candidates[0].UpperConstantInstructionIndex, replacementMethod);
            return FluentReplacementResult.PatternReplaced;
        }

        private bool TryReadArgumentSelection(out FluentTranspiler transpiler, out int argumentIndex)
        {
            transpiler = _argumentSelection?.Transpiler;
            argumentIndex = _argumentSelection != null ? _argumentSelection.ArgumentIndex : -1;

            if (transpiler == null || argumentIndex < 0)
            {
                transpiler?.AddWarning($"InClamp received invalid argument index {argumentIndex}.");
                return false;
            }

            return true;
        }

        private IEnumerable<ClampMatch> FindClampMatches(IList<CodeInstruction> instructions, int argumentIndex, int startIndex)
        {
            var meaningful = FluentRecipeUtility.BuildMeaningfulIndex(instructions, startIndex);
            for (int i = 0; i <= meaningful.Count - 4; i++)
            {
                CodeInstruction value = instructions[meaningful[i]];
                CodeInstruction lower = instructions[meaningful[i + 1]];
                CodeInstruction upper = instructions[meaningful[i + 2]];
                CodeInstruction call = instructions[meaningful[i + 3]];

                if (!value.IsLoadArgument(argumentIndex))
                {
                    continue;
                }

                int lowerValue;
                int upperValue;
                if (!FluentRecipeUtility.TryGetLdcI4Value(lower, out lowerValue) ||
                    !FluentRecipeUtility.TryGetLdcI4Value(upper, out upperValue) ||
                    lowerValue != _lowerBound ||
                    upperValue != _upperBound)
                {
                    continue;
                }

                if (IsClampCall(call))
                {
                    yield return new ClampMatch { UpperConstantInstructionIndex = meaningful[i + 2] };
                }
            }
        }

        private static bool IsClampCall(CodeInstruction instruction)
        {
            if (instruction == null ||
                (instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt) ||
                !(instruction.operand is MethodInfo method))
            {
                return false;
            }

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != 3 || method.ReturnType == typeof(void))
            {
                return false;
            }

            bool intClamp = method.ReturnType == typeof(int) &&
                            parameters.All(parameter => parameter.ParameterType == typeof(int));

            return intClamp &&
                   method.Name.IndexOf("Clamp", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private sealed class ClampMatch
        {
            public int UpperConstantInstructionIndex { get; set; }
        }
    }
}
