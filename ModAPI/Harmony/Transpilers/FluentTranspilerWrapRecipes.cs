using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace ModAPI.Harmony
{
    /// <summary>
    /// High-level recipes for numeric wraparound logic such as increment/decrement UI cycling.
    /// </summary>
    public static class FluentTranspilerWrapRecipes
    {
        /// <summary>
        /// Selects wraparound logic based on the result of a known value-provider call.
        /// </summary>
        /// <example>
        /// <code>
        /// t.ForCallResult(getPriority)
        ///  .AsWrappedRange(0, 4)
        ///  .ReplaceUpperBoundWithCall(getMaxPriority);
        /// </code>
        /// </example>
        public static FluentCallResultSelection ForCallResult(this FluentTranspiler transpiler, MethodInfo method)
        {
            return new FluentCallResultSelection(transpiler, method);
        }

        public static FluentWrapBoundsReplacementResult ReplaceWrappedRangeUpperBoundWithCallOrCompatibleProvider(
            this FluentTranspiler transpiler,
            MethodInfo valueProvider,
            int lowerBound,
            int upperBound,
            int step,
            MethodInfo replacementMethod,
            Func<MethodInfo, bool> compatibleProviderPredicate,
            string editLabel = null,
            SearchMode mode = SearchMode.Start)
        {
            return transpiler.ForCallResult(valueProvider)
                             .AsWrappedRange(lowerBound, upperBound, step)
                             .ReplaceUpperBoundWithCallOrCompatibleProvider(
                                 replacementMethod,
                                 compatibleProviderPredicate,
                                 editLabel,
                                 mode);
        }
    }

    public sealed class FluentCallResultSelection
    {
        private readonly FluentTranspiler _transpiler;
        private readonly MethodInfo _method;

        internal FluentCallResultSelection(FluentTranspiler transpiler, MethodInfo method)
        {
            _transpiler = transpiler;
            _method = method;
        }

        /// <summary>
        /// Matches increment/decrement wraparound around a lower and upper bound.
        /// </summary>
        /// <example>
        /// <code>
        /// t.ForCallResult(getPriority)
        ///  .AsWrappedRange(0, 4)
        ///  .ReplaceUpperBoundWithCall(getMaxPriority);
        /// </code>
        /// </example>
        public FluentWrappedRangeSelection AsWrappedRange(int lowerBound, int upperBound, int step = 1)
        {
            return new FluentWrappedRangeSelection(_transpiler, _method, lowerBound, upperBound, step);
        }
    }

    public sealed class FluentWrappedRangeSelection
    {
        private const int UnderflowUpperBoundPatternOffset = 7;
        private const int OverflowUpperBoundPatternOffset = 5;

        private readonly FluentTranspiler _transpiler;
        private readonly MethodInfo _valueProvider;
        private readonly int _lowerBound;
        private readonly int _upperBound;
        private readonly int _step;
        private string _name;
        private string _editLabel;
        private string _failureMessage;
        private bool _optional;
        private bool _strict;

        internal FluentWrappedRangeSelection(
            FluentTranspiler transpiler,
            MethodInfo valueProvider,
            int lowerBound,
            int upperBound,
            int step)
        {
            _transpiler = transpiler;
            _valueProvider = valueProvider;
            _lowerBound = lowerBound;
            _upperBound = upperBound;
            _step = step;
        }

        public FluentWrappedRangeSelection Named(string name)
        {
            _name = name;
            return this;
        }

        public FluentWrappedRangeSelection WithEditLabel(string editLabel)
        {
            _editLabel = editLabel;
            return this;
        }

        public FluentWrappedRangeSelection WithFailureMessage(string failureMessage)
        {
            _failureMessage = failureMessage;
            return this;
        }

        public FluentWrappedRangeSelection RequireSingleMatch()
        {
            return this;
        }

        public FluentWrappedRangeSelection Optional()
        {
            _optional = true;
            return this;
        }

        public FluentWrappedRangeSelection Strict()
        {
            _strict = true;
            return this;
        }

        /// <summary>
        /// Replaces the upper-bound literal in both underflow and overflow wraparound paths.
        /// </summary>
        /// <example>
        /// <code>
        /// t.ForCallResult(getPriority)
        ///  .AsWrappedRange(0, 4)
        ///  .ReplaceUpperBoundWithCall(getMaxPriority);
        /// </code>
        /// </example>
        public FluentWrapBoundsReplacementResult ReplaceUpperBoundWithCall(MethodInfo replacementMethod, SearchMode mode = SearchMode.Start)
        {
            if (!IsValid())
            {
                return FluentWrapBoundsReplacementResult.NoMatch;
            }

            if (!ValidateReplacementProvider(replacementMethod, nameof(ReplaceUpperBoundWithCall)))
            {
                return FluentWrapBoundsReplacementResult.NoMatch;
            }

            WrappedBoundSlotPlan underflowPlan = BuildPlan(
                WrappedRangePathKind.Underflow,
                allowCompatibleProvider: false,
                compatibleProviderPredicate: null,
                replacementMethod: replacementMethod,
                mode: mode);
            WrappedBoundSlotPlan overflowPlan = BuildPlan(
                WrappedRangePathKind.Overflow,
                allowCompatibleProvider: false,
                compatibleProviderPredicate: null,
                replacementMethod: replacementMethod,
                mode: mode);

            bool completed = ApplyPlansIfComplete(underflowPlan, overflowPlan, replacementMethod);

            var result = new FluentWrapBoundsReplacementResult(
                FinalizeResult(underflowPlan, completed),
                FinalizeResult(overflowPlan, completed),
                completed && HasProviderCoverage(underflowPlan, overflowPlan),
                completed && ReplacedCompatibleProvider(underflowPlan, overflowPlan));
            AddFailureDiagnostic(result, null);
            return result;
        }

        public FluentWrapBoundsReplacementResult ReplaceUpperBoundWithCallOrCompatibleProvider(
            MethodInfo replacementMethod,
            Func<MethodInfo, bool> compatibleProviderPredicate,
            string editLabel = null,
            SearchMode mode = SearchMode.Start)
        {
            return ReplaceUpperBoundWithCallOrFallbackCall(
                replacementMethod,
                compatibleProviderPredicate,
                editLabel,
                mode);
        }

        public FluentWrapBoundsReplacementResult ReplaceUpperBoundWithCallOrFallbackCall(
            MethodInfo replacementMethod,
            Func<MethodInfo, bool> fallbackMethodPredicate,
            string editLabel = null,
            SearchMode mode = SearchMode.Start)
        {
            if (!IsValid())
            {
                return FluentWrapBoundsReplacementResult.NoMatch;
            }

            if (!ValidateReplacementProvider(replacementMethod, nameof(ReplaceUpperBoundWithCallOrFallbackCall)))
            {
                return FluentWrapBoundsReplacementResult.NoMatch;
            }

            WrappedBoundSlotPlan underflowPlan = BuildPlan(
                WrappedRangePathKind.Underflow,
                allowCompatibleProvider: true,
                compatibleProviderPredicate: fallbackMethodPredicate,
                replacementMethod: replacementMethod,
                mode: mode);
            WrappedBoundSlotPlan overflowPlan = BuildPlan(
                WrappedRangePathKind.Overflow,
                allowCompatibleProvider: true,
                compatibleProviderPredicate: fallbackMethodPredicate,
                replacementMethod: replacementMethod,
                mode: mode);

            bool completed = ApplyPlansIfComplete(underflowPlan, overflowPlan, replacementMethod);

            var result = new FluentWrapBoundsReplacementResult(
                FinalizeResult(underflowPlan, completed),
                FinalizeResult(overflowPlan, completed),
                completed && HasProviderCoverage(underflowPlan, overflowPlan),
                completed && ReplacedCompatibleProvider(underflowPlan, overflowPlan));
            AddFailureDiagnostic(result, editLabel);
            return result;
        }

        private void AddFailureDiagnostic(FluentWrapBoundsReplacementResult result, string editLabel)
        {
            if (_transpiler == null || result == null || result.Succeeded)
            {
                return;
            }

            _transpiler.AddPatchDiagnostic(new FluentPatchDiagnostic
            {
                RecipeName = "ForCallResult(" + FluentTranspilerFormatting.FormatMethod(_valueProvider) + ").AsWrappedRange(" + _lowerBound + ", " + _upperBound + ", " + _step + ").ReplaceUpperBoundWithCall(GetMaxPriority)",
                ExpectedShape = !string.IsNullOrEmpty(_failureMessage)
                    ? _failureMessage
                    : "underflow and overflow wraparound checks from " + _lowerBound + " to " + _upperBound + ".",
                FoundShape = "underflow=" + result.UnderflowResult + ", overflow=" + result.OverflowResult + ", global provider=" + result.HasGlobalProviderCoverage + ".",
                ActionTaken = "patch skipped; original IL returned.",
                Severity = _strict ? FluentPatchSeverity.Critical : (_optional ? FluentPatchSeverity.Info : FluentPatchSeverity.Warning),
                EditLabel = !string.IsNullOrEmpty(_editLabel) ? _editLabel : (!string.IsNullOrEmpty(editLabel) ? editLabel : _name)
            });
        }

        private bool IsValid()
        {
            if (_transpiler == null)
            {
                return false;
            }

            if (_valueProvider == null)
            {
                _transpiler.AddWarning("ForCallResult received a null method.");
                return false;
            }

            if (_lowerBound > _upperBound)
            {
                _transpiler.AddWarning($"AsWrappedRange lower bound {_lowerBound} is greater than upper bound {_upperBound}.");
                return false;
            }

            if (_step <= 0)
            {
                _transpiler.AddWarning($"AsWrappedRange step {_step} must be greater than zero.");
                return false;
            }

            return true;
        }

        private bool ValidateReplacementProvider(MethodInfo replacementMethod, string caller)
        {
            return FluentTranspilerRecipeValidation.ValidateIntCompatibleReturn(_transpiler, replacementMethod, caller) &&
                   FluentTranspilerRecipeValidation.ValidateParameterCount(_transpiler, replacementMethod, 0, caller);
        }

        private WrappedBoundSlotPlan BuildPlan(
            WrappedRangePathKind path,
            bool allowCompatibleProvider,
            Func<MethodInfo, bool> compatibleProviderPredicate,
            MethodInfo replacementMethod,
            SearchMode mode)
        {
            List<WrappedBoundSlotMatch> matches = FindMatches(
                path,
                allowCompatibleProvider,
                compatibleProviderPredicate,
                replacementMethod,
                mode);

            if (matches.Count == 0)
            {
                return new WrappedBoundSlotPlan(path, FluentReplacementResult.NoMatch, null);
            }

            if (matches.Count > 1)
            {
                _transpiler.AddWarning(
                    "AsWrappedRange found " + matches.Count + " " + path.ToString().ToLowerInvariant() +
                    " upper-bound candidate(s); refusing ambiguous edit.");
                return new WrappedBoundSlotPlan(path, FluentReplacementResult.AmbiguousMatch, null);
            }

            WrappedBoundSlotMatch match = matches[0];
            return new WrappedBoundSlotPlan(path, ResultFor(match), match);
        }

        private List<WrappedBoundSlotMatch> FindMatches(
            WrappedRangePathKind path,
            bool allowCompatibleProvider,
            Func<MethodInfo, bool> compatibleProviderPredicate,
            MethodInfo replacementMethod,
            SearchMode mode)
        {
            var instructions = _transpiler.Instructions().ToList();
            int startIndex = FluentRecipeUtility.GetSearchStartIndex(_transpiler, mode);
            int offset = UpperBoundOffset(path);
            Func<CodeInstruction, bool>[] pattern = path == WrappedRangePathKind.Underflow
                ? BuildUnderflowPattern(instruction => IsUpperBoundSlot(instruction, allowCompatibleProvider, compatibleProviderPredicate, replacementMethod))
                : BuildOverflowPattern(instruction => IsUpperBoundSlot(instruction, allowCompatibleProvider, compatibleProviderPredicate, replacementMethod));
            var matches = new List<WrappedBoundSlotMatch>();

            for (int i = Math.Max(0, startIndex); i <= instructions.Count - pattern.Length; i++)
            {
                if (!MatchesPattern(instructions, i, pattern))
                {
                    continue;
                }

                CodeInstruction slot = instructions[i + offset];
                matches.Add(new WrappedBoundSlotMatch
                {
                    Path = path,
                    StartIndex = i,
                    ReplacementIndex = i + offset,
                    State = ClassifyUpperBoundSlot(slot, allowCompatibleProvider, compatibleProviderPredicate, replacementMethod)
                });
            }

            return matches;
        }

        private bool ApplyPlansIfComplete(
            WrappedBoundSlotPlan underflowPlan,
            WrappedBoundSlotPlan overflowPlan,
            MethodInfo replacementMethod)
        {
            if (underflowPlan == null ||
                overflowPlan == null ||
                !underflowPlan.Result.Succeeded() ||
                !overflowPlan.Result.Succeeded())
            {
                return false;
            }

            var matchesToPatch = new[] { underflowPlan.Match, overflowPlan.Match }
                .Where(match => match != null && match.RequiresMutation)
                .OrderByDescending(match => match.ReplacementIndex)
                .ToList();

            for (int i = 0; i < matchesToPatch.Count; i++)
            {
                _transpiler.ReplaceAtWithCall(matchesToPatch[i].ReplacementIndex, replacementMethod);
            }

            return true;
        }

        private static bool MatchesPattern(
            IList<CodeInstruction> instructions,
            int startIndex,
            Func<CodeInstruction, bool>[] pattern)
        {
            for (int i = 0; i < pattern.Length; i++)
            {
                if (!pattern[i](instructions[startIndex + i]))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsUpperBoundSlot(
            CodeInstruction instruction,
            bool allowCompatibleProvider,
            Func<MethodInfo, bool> compatibleProviderPredicate,
            MethodInfo replacementMethod)
        {
            return ClassifyUpperBoundSlot(
                       instruction,
                       allowCompatibleProvider,
                       compatibleProviderPredicate,
                       replacementMethod) != WrappedBoundSlotState.None;
        }

        private WrappedBoundSlotState ClassifyUpperBoundSlot(
            CodeInstruction instruction,
            bool allowCompatibleProvider,
            Func<MethodInfo, bool> compatibleProviderPredicate,
            MethodInfo replacementMethod)
        {
            if (instruction != null && instruction.IsLdcI4(_upperBound))
            {
                return WrappedBoundSlotState.LiteralUpperBound;
            }

            MethodInfo method = GetCallMethod(instruction);
            if (method == null)
            {
                return WrappedBoundSlotState.None;
            }

            if (replacementMethod != null && method == replacementMethod)
            {
                return WrappedBoundSlotState.ReplacementAlreadyPresent;
            }

            if (allowCompatibleProvider &&
                compatibleProviderPredicate != null &&
                compatibleProviderPredicate(method))
            {
                return WrappedBoundSlotState.CompatibleProvider;
            }

            return WrappedBoundSlotState.None;
        }

        private static MethodInfo GetCallMethod(CodeInstruction instruction)
        {
            if (instruction == null ||
                (instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt))
            {
                return null;
            }

            return instruction.operand as MethodInfo;
        }

        private static FluentReplacementResult ResultFor(WrappedBoundSlotMatch match)
        {
            if (match == null)
            {
                return FluentReplacementResult.NoMatch;
            }

            switch (match.State)
            {
                case WrappedBoundSlotState.LiteralUpperBound:
                    return FluentReplacementResult.PatternReplaced;
                case WrappedBoundSlotState.ReplacementAlreadyPresent:
                    return FluentReplacementResult.ReplacementAlreadyPresent;
                case WrappedBoundSlotState.CompatibleProvider:
                    return FluentReplacementResult.FallbackCallReplaced;
                default:
                    return FluentReplacementResult.NoMatch;
            }
        }

        private static FluentReplacementResult FinalizeResult(WrappedBoundSlotPlan plan, bool completed)
        {
            if (plan == null)
            {
                return FluentReplacementResult.NoMatch;
            }

            if (completed)
            {
                return plan.Result;
            }

            return plan.Result == FluentReplacementResult.ReplacementAlreadyPresent ||
                   plan.Result == FluentReplacementResult.AmbiguousMatch ||
                   plan.Result == FluentReplacementResult.UnsafeMatch ||
                   plan.Result == FluentReplacementResult.Failed
                ? plan.Result
                : FluentReplacementResult.NoMatch;
        }

        private static int UpperBoundOffset(WrappedRangePathKind path)
        {
            return path == WrappedRangePathKind.Underflow
                ? UnderflowUpperBoundPatternOffset
                : OverflowUpperBoundPatternOffset;
        }

        private static bool HasProviderCoverage(WrappedBoundSlotPlan underflowPlan, WrappedBoundSlotPlan overflowPlan)
        {
            return IsProviderCovered(underflowPlan) && IsProviderCovered(overflowPlan);
        }

        private static bool IsProviderCovered(WrappedBoundSlotPlan plan)
        {
            return plan != null &&
                   plan.Result.Succeeded() &&
                   plan.Match != null &&
                   (plan.Match.State == WrappedBoundSlotState.ReplacementAlreadyPresent ||
                    plan.Match.State == WrappedBoundSlotState.CompatibleProvider);
        }

        private static bool ReplacedCompatibleProvider(WrappedBoundSlotPlan underflowPlan, WrappedBoundSlotPlan overflowPlan)
        {
            return RequiresCompatibleProviderReplacement(underflowPlan) ||
                   RequiresCompatibleProviderReplacement(overflowPlan);
        }

        private static bool RequiresCompatibleProviderReplacement(WrappedBoundSlotPlan plan)
        {
            return plan != null &&
                   plan.Match != null &&
                   plan.Match.State == WrappedBoundSlotState.CompatibleProvider &&
                   plan.Result.Succeeded();
        }

        private Func<CodeInstruction, bool>[] BuildUnderflowPattern(Func<CodeInstruction, bool> upperBoundPredicate)
        {
            return new Func<CodeInstruction, bool>[]
            {
                instruction => instruction != null && instruction.Calls(_valueProvider),
                instruction => instruction != null && instruction.IsLdcI4(_step),
                instruction => instruction != null && instruction.opcode == OpCodes.Sub,
                instruction => instruction.IsStoreLocal(),
                instruction => instruction.IsLoadLocal(),
                instruction => instruction != null && instruction.IsLdcI4(_lowerBound),
                instruction => instruction.IsBranch(OpCodes.Bge, OpCodes.Bge_S),
                upperBoundPredicate
            };
        }

        private Func<CodeInstruction, bool>[] BuildOverflowPattern(Func<CodeInstruction, bool> upperBoundPredicate)
        {
            return new Func<CodeInstruction, bool>[]
            {
                instruction => instruction != null && instruction.Calls(_valueProvider),
                instruction => instruction != null && instruction.IsLdcI4(_step),
                instruction => instruction != null && instruction.opcode == OpCodes.Add,
                instruction => instruction.IsStoreLocal(),
                instruction => instruction.IsLoadLocal(),
                upperBoundPredicate,
                instruction => instruction.IsBranch(OpCodes.Ble, OpCodes.Ble_S),
                instruction => instruction != null && instruction.IsLdcI4(_lowerBound)
            };
        }

        private enum WrappedRangePathKind
        {
            Underflow,
            Overflow
        }

        private enum WrappedBoundSlotState
        {
            None,
            LiteralUpperBound,
            ReplacementAlreadyPresent,
            CompatibleProvider
        }

        private sealed class WrappedBoundSlotMatch
        {
            public WrappedRangePathKind Path;
            public int StartIndex;
            public int ReplacementIndex;
            public WrappedBoundSlotState State;

            public bool RequiresMutation
            {
                get
                {
                    return State == WrappedBoundSlotState.LiteralUpperBound ||
                           State == WrappedBoundSlotState.CompatibleProvider;
                }
            }
        }

        private sealed class WrappedBoundSlotPlan
        {
            public WrappedBoundSlotPlan(
                WrappedRangePathKind path,
                FluentReplacementResult result,
                WrappedBoundSlotMatch match)
            {
                Path = path;
                Result = result;
                Match = match;
            }

            public WrappedRangePathKind Path { get; private set; }
            public FluentReplacementResult Result { get; private set; }
            public WrappedBoundSlotMatch Match { get; private set; }
        }
    }

    public sealed class FluentWrapBoundsReplacementResult
    {
        public static readonly FluentWrapBoundsReplacementResult NoMatch =
            new FluentWrapBoundsReplacementResult(FluentReplacementResult.NoMatch, FluentReplacementResult.NoMatch);

        public FluentWrapBoundsReplacementResult(
            FluentReplacementResult underflowResult,
            FluentReplacementResult overflowResult,
            bool hasGlobalProviderCoverage = false,
            bool replacedFallbackProvider = false)
        {
            UnderflowResult = underflowResult;
            OverflowResult = overflowResult;
            HasGlobalProviderCoverage = hasGlobalProviderCoverage;
            ReplacedFallbackProvider = replacedFallbackProvider;
        }

        public FluentReplacementResult UnderflowResult { get; }

        public FluentReplacementResult OverflowResult { get; }

        public bool HasGlobalProviderCoverage { get; }

        public bool ReplacedFallbackProvider { get; }

        public bool Succeeded
        {
            get
            {
                return UnderflowResult.Succeeded() &&
                       OverflowResult.Succeeded();
            }
        }

        public bool ReplacedFallbackCall
        {
            get
            {
                return ReplacedFallbackProvider;
            }
        }

        public override string ToString()
        {
            return $"underflow={UnderflowResult}, overflow={OverflowResult}, globalProvider={HasGlobalProviderCoverage}, fallbackProviderReplaced={ReplacedFallbackProvider}";
        }
    }
}
