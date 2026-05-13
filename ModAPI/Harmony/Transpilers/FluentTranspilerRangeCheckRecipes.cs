using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace ModAPI.Harmony
{
    public sealed class FluentPatternCandidate
    {
        public int StartIndex;
        public int EndIndex;
        public string Shape;
        public string Confidence;
        public string[] Notes;
    }

    /// <summary>
    /// Reusable discovery helpers for nearby IL patterns. These helpers are intentionally read-only;
    /// recipes decide separately whether a discovered shape is safe enough to patch.
    /// </summary>
    public static class FluentTranspilerPatternDiscovery
    {
        private const int DefaultRadius = 8;

        public static IList<FluentPatternCandidate> FindNearbyArgumentLoads(
            this FluentTranspiler transpiler,
            int argumentIndex,
            int centerIndex,
            int radius = DefaultRadius)
        {
            return FindNearby(
                transpiler,
                centerIndex,
                radius,
                instruction => instruction.IsLoadArgument(argumentIndex),
                instruction => $"ldarg.{argumentIndex} via {instruction.opcode.Name}",
                "nearby argument load");
        }

        public static IList<FluentPatternCandidate> FindNearbyConstants(
            this FluentTranspiler transpiler,
            int centerIndex,
            int radius = DefaultRadius,
            int? expectedValue = null)
        {
            return FindNearby(
                transpiler,
                centerIndex,
                radius,
                instruction => TryGetLdcI4(instruction, out _),
                instruction =>
                {
                    TryGetLdcI4(instruction, out int value);
                    string confidence = expectedValue.HasValue && expectedValue.Value == value ? "exact" : "nearby";
                    return $"ldc.i4 {value} ({confidence})";
                },
                "nearby constant");
        }

        public static IList<FluentPatternCandidate> FindNearbyBranches(
            this FluentTranspiler transpiler,
            int centerIndex,
            int radius = DefaultRadius)
        {
            return FindNearby(
                transpiler,
                centerIndex,
                radius,
                IsConditionalBranch,
                instruction => $"branch {instruction.opcode.Name}",
                "nearby branch");
        }

        public static IList<FluentPatternCandidate> FindEquivalentRangeChecks(
            this FluentTranspiler transpiler,
            int argumentIndex,
            int lowerBound,
            int upperBound)
        {
            return DiscoverRangeChecks(transpiler, argumentIndex, lowerBound, upperBound)
                .Select(match => match.ToCandidate())
                .ToList();
        }

        public static IList<FluentPatternCandidate> FindClampCandidates(this FluentTranspiler transpiler)
        {
            if (transpiler == null)
            {
                return new List<FluentPatternCandidate>();
            }

            var instructions = transpiler.Instructions().ToList();
            var candidates = new List<FluentPatternCandidate>();
            for (int i = 0; i < instructions.Count; i++)
            {
                var method = instructions[i].operand as MethodInfo;
                if ((instructions[i].opcode == OpCodes.Call || instructions[i].opcode == OpCodes.Callvirt) &&
                    method != null &&
                    method.Name == "Clamp")
                {
                    candidates.Add(new FluentPatternCandidate
                    {
                        StartIndex = i,
                        EndIndex = i,
                        Shape = "call " + FluentTranspilerFormatting.FormatMethod(method),
                        Confidence = "candidate",
                        Notes = new[] { "Clamp-like call site." }
                    });
                }
            }

            return candidates;
        }

        public static IList<FluentPatternCandidate> FindCallCandidates(
            this FluentTranspiler transpiler,
            MethodInfo expectedMethod = null)
        {
            if (transpiler == null)
            {
                return new List<FluentPatternCandidate>();
            }

            var instructions = transpiler.Instructions().ToList();
            var candidates = new List<FluentPatternCandidate>();
            for (int i = 0; i < instructions.Count; i++)
            {
                var method = instructions[i].operand as MethodInfo;
                if ((instructions[i].opcode != OpCodes.Call && instructions[i].opcode != OpCodes.Callvirt) ||
                    method == null)
                {
                    continue;
                }

                string confidence = expectedMethod == null || method == expectedMethod ? "candidate" : "nearby";
                candidates.Add(new FluentPatternCandidate
                {
                    StartIndex = i,
                    EndIndex = i,
                    Shape = instructions[i].opcode.Name + " " + FluentTranspilerFormatting.FormatMethod(method),
                    Confidence = confidence,
                    Notes = new[] { expectedMethod == null || method == expectedMethod ? "Call candidate." : "Different call target." }
                });
            }

            return candidates;
        }

        public static string DescribeCandidateWindow(
            this FluentTranspiler transpiler,
            FluentPatternCandidate candidate,
            int contextRadius = 2)
        {
            if (transpiler == null || candidate == null)
            {
                return "<no candidate>";
            }

            var instructions = transpiler.Instructions().ToList();
            if (instructions.Count == 0)
            {
                return "<empty instruction stream>";
            }

            int start = Math.Max(0, candidate.StartIndex - Math.Max(0, contextRadius));
            int end = Math.Min(instructions.Count - 1, candidate.EndIndex + Math.Max(0, contextRadius));
            var parts = new List<string>();
            for (int i = start; i <= end; i++)
            {
                string marker = i >= candidate.StartIndex && i <= candidate.EndIndex ? "*" : string.Empty;
                parts.Add($"{marker}{i}:{FormatInstruction(instructions[i])}{marker}");
            }

            string notes = candidate.Notes != null && candidate.Notes.Length > 0
                ? " notes=[" + string.Join("; ", candidate.Notes) + "]"
                : string.Empty;

            return $"{candidate.Shape} at {candidate.StartIndex}..{candidate.EndIndex} ({candidate.Confidence}){notes}; window: " +
                   string.Join(" | ", parts.ToArray());
        }

        internal static List<RangeCheckMatch> DiscoverRangeChecks(
            FluentTranspiler transpiler,
            int argumentIndex,
            int lowerBound,
            int upperBound)
        {
            if (transpiler == null)
            {
                return new List<RangeCheckMatch>();
            }

            var instructions = transpiler.Instructions().ToList();
            var comparisons = new List<RangeComparison>();
            for (int i = 0; i < instructions.Count; i++)
            {
                RangeComparison comparison;
                if (TryReadComparison(instructions, i, argumentIndex, lowerBound, upperBound, out comparison))
                {
                    comparisons.Add(comparison);
                }
            }

            var matches = new List<RangeCheckMatch>();
            for (int i = 0; i < comparisons.Count; i++)
            {
                for (int j = i + 1; j < comparisons.Count; j++)
                {
                    var first = comparisons[i];
                    var second = comparisons[j];
                    if (!CanPair(first, second))
                    {
                        continue;
                    }

                    int start = Math.Min(first.StartIndex, second.StartIndex);
                    int end = Math.Max(first.EndIndex, second.EndIndex);
                    if (!CanSkipBetween(instructions, Math.Min(first.EndIndex, second.EndIndex) + 1, Math.Max(first.StartIndex, second.StartIndex) - 1))
                    {
                        continue;
                    }

                    var upper = first.Kind == RangeComparisonKind.Upper ? first : second;
                    var lower = first.Kind == RangeComparisonKind.Lower ? first : second;
                    var notes = new List<string>();
                    if (upper.StartIndex < lower.StartIndex)
                    {
                        notes.Add("Upper comparison appears before lower comparison.");
                    }
                    else
                    {
                        notes.Add("Lower comparison appears before upper comparison.");
                    }

                    if (upper.ConstantValue != upperBound)
                    {
                        notes.Add($"Upper constant is {upper.ConstantValue}; expected {upperBound}.");
                    }

                    if (upper.Adjustment != 0)
                    {
                        notes.Add($"> {upperBound} represented as >= {upperBound + 1}.");
                    }

                    if (lower.ConstantValue != lowerBound)
                    {
                        notes.Add($"Lower constant is {lower.ConstantValue}; expected {lowerBound}.");
                    }

                    notes.Add("Branch opcodes: " + lower.BranchOpcode.Name + ", " + upper.BranchOpcode.Name + ".");

                    bool safe = IsWindowSafe(instructions, start, end);
                    if (!safe)
                    {
                        notes.Add("Unsafe candidate: labels or exception block markers occur inside the candidate span.");
                    }

                    bool exactBounds = lower.ConstantValue == lowerBound &&
                                       (upper.ConstantValue == upperBound || upper.ConstantValue == upperBound + 1);

                    matches.Add(new RangeCheckMatch
                    {
                        StartIndex = start,
                        EndIndex = end,
                        Lower = lower,
                        Upper = upper,
                        IsSafe = safe,
                        IsExactBounds = exactBounds,
                        Notes = notes
                    });
                }
            }

            return matches
                .GroupBy(match => match.StartIndex + ":" + match.EndIndex + ":" + match.Upper.ConstantIndex)
                .Select(group => group.First())
                .OrderBy(match => match.StartIndex)
                .ToList();
        }

        internal static bool TryGetLdcI4(CodeInstruction instruction, out int value)
        {
            value = 0;
            if (instruction == null)
            {
                return false;
            }

            if (instruction.opcode == OpCodes.Ldc_I4)
            {
                value = (int)instruction.operand;
                return true;
            }

            if (instruction.opcode == OpCodes.Ldc_I4_S)
            {
                if (instruction.operand is sbyte signedByte)
                {
                    value = signedByte;
                    return true;
                }

                if (instruction.operand is byte unsignedByte)
                {
                    value = unchecked((sbyte)unsignedByte);
                    return true;
                }

                if (instruction.operand is int intValue)
                {
                    value = intValue;
                    return true;
                }

                if (instruction.operand is short shortValue)
                {
                    value = shortValue;
                    return true;
                }

                return false;
            }

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
            return false;
        }

        private static IList<FluentPatternCandidate> FindNearby(
            FluentTranspiler transpiler,
            int centerIndex,
            int radius,
            Func<CodeInstruction, bool> predicate,
            Func<CodeInstruction, string> shape,
            string note)
        {
            if (transpiler == null)
            {
                return new List<FluentPatternCandidate>();
            }

            var instructions = transpiler.Instructions().ToList();
            int start = Math.Max(0, centerIndex - Math.Max(0, radius));
            int end = Math.Min(instructions.Count - 1, centerIndex + Math.Max(0, radius));
            var candidates = new List<FluentPatternCandidate>();
            for (int i = start; i <= end; i++)
            {
                if (!predicate(instructions[i]))
                {
                    continue;
                }

                candidates.Add(new FluentPatternCandidate
                {
                    StartIndex = i,
                    EndIndex = i,
                    Shape = shape(instructions[i]),
                    Confidence = Math.Abs(i - centerIndex) <= 2 ? "near" : "far",
                    Notes = new[] { note }
                });
            }

            return candidates;
        }

        private static bool TryReadComparison(
            IList<CodeInstruction> instructions,
            int startIndex,
            int argumentIndex,
            int lowerBound,
            int upperBound,
            out RangeComparison comparison)
        {
            comparison = null;
            if (!instructions[startIndex].IsLoadArgument(argumentIndex))
            {
                return false;
            }

            int constantIndex = NextNonNop(instructions, startIndex + 1);
            if (constantIndex < 0 || !TryGetLdcI4(instructions[constantIndex], out int constantValue))
            {
                return false;
            }

            int branchIndex = NextNonNop(instructions, constantIndex + 1);
            if (branchIndex < 0 || !IsConditionalBranch(instructions[branchIndex]))
            {
                return false;
            }

            OpCode branch = instructions[branchIndex].opcode;
            RangeComparisonKind kind;
            int adjustment = 0;
            if (constantValue == lowerBound && IsLowerBranch(branch))
            {
                kind = RangeComparisonKind.Lower;
            }
            else if (constantValue == upperBound && IsUpperBranch(branch))
            {
                kind = RangeComparisonKind.Upper;
            }
            else if (constantValue == upperBound + 1 && IsUpperPlusOneBranch(branch))
            {
                kind = RangeComparisonKind.Upper;
                adjustment = 1;
            }
            else if (constantValue == lowerBound)
            {
                kind = RangeComparisonKind.Unsupported;
            }
            else if (constantValue == upperBound || constantValue == upperBound + 1)
            {
                kind = RangeComparisonKind.Unsupported;
            }
            else
            {
                return false;
            }

            comparison = new RangeComparison
            {
                StartIndex = startIndex,
                ConstantIndex = constantIndex,
                EndIndex = branchIndex,
                ConstantValue = constantValue,
                BranchOpcode = branch,
                Kind = kind,
                Adjustment = adjustment
            };
            return true;
        }

        private static int NextNonNop(IList<CodeInstruction> instructions, int startIndex)
        {
            for (int i = startIndex; i < instructions.Count; i++)
            {
                if (instructions[i] == null || instructions[i].opcode != OpCodes.Nop)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool CanPair(RangeComparison first, RangeComparison second)
        {
            return first.Kind != RangeComparisonKind.Unsupported &&
                   second.Kind != RangeComparisonKind.Unsupported &&
                   first.Kind != second.Kind;
        }

        private static bool CanSkipBetween(IList<CodeInstruction> instructions, int start, int end)
        {
            if (start > end)
            {
                return true;
            }

            for (int i = start; i <= end; i++)
            {
                if (instructions[i] != null && instructions[i].opcode != OpCodes.Nop)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsWindowSafe(IList<CodeInstruction> instructions, int start, int end)
        {
            for (int i = start; i <= end; i++)
            {
                var instruction = instructions[i];
                if (instruction == null)
                {
                    return false;
                }

                if (instruction.blocks != null && instruction.blocks.Count > 0)
                {
                    return false;
                }

                if (i > start && instruction.labels != null && instruction.labels.Count > 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsLowerBranch(OpCode opcode)
        {
            return opcode == OpCodes.Blt || opcode == OpCodes.Blt_S ||
                   opcode == OpCodes.Bge || opcode == OpCodes.Bge_S;
        }

        private static bool IsUpperBranch(OpCode opcode)
        {
            return opcode == OpCodes.Ble || opcode == OpCodes.Ble_S ||
                   opcode == OpCodes.Bgt || opcode == OpCodes.Bgt_S;
        }

        private static bool IsUpperPlusOneBranch(OpCode opcode)
        {
            return opcode == OpCodes.Blt || opcode == OpCodes.Blt_S ||
                   opcode == OpCodes.Bge || opcode == OpCodes.Bge_S;
        }

        private static bool IsConditionalBranch(CodeInstruction instruction)
        {
            if (instruction == null)
            {
                return false;
            }

            return instruction.opcode == OpCodes.Blt || instruction.opcode == OpCodes.Blt_S ||
                   instruction.opcode == OpCodes.Ble || instruction.opcode == OpCodes.Ble_S ||
                   instruction.opcode == OpCodes.Bgt || instruction.opcode == OpCodes.Bgt_S ||
                   instruction.opcode == OpCodes.Bge || instruction.opcode == OpCodes.Bge_S ||
                   instruction.opcode == OpCodes.Brtrue || instruction.opcode == OpCodes.Brtrue_S ||
                   instruction.opcode == OpCodes.Brfalse || instruction.opcode == OpCodes.Brfalse_S ||
                   instruction.opcode == OpCodes.Beq || instruction.opcode == OpCodes.Beq_S ||
                   instruction.opcode == OpCodes.Bne_Un || instruction.opcode == OpCodes.Bne_Un_S;
        }

        private static string FormatInstruction(CodeInstruction instruction)
        {
            if (instruction == null)
            {
                return "<null>";
            }

            string operand = instruction.operand == null ? string.Empty : " " + instruction.operand;
            return instruction.opcode.Name + operand;
        }
    }

    internal enum RangeComparisonKind
    {
        Unsupported,
        Lower,
        Upper
    }

    internal sealed class RangeComparison
    {
        internal int StartIndex;
        internal int ConstantIndex;
        internal int EndIndex;
        internal int ConstantValue;
        internal OpCode BranchOpcode;
        internal RangeComparisonKind Kind;
        internal int Adjustment;
    }

    internal sealed class RangeCheckMatch
    {
        internal int StartIndex;
        internal int EndIndex;
        internal RangeComparison Lower;
        internal RangeComparison Upper;
        internal bool IsSafe;
        internal bool IsExactBounds;
        internal List<string> Notes;

        internal FluentPatternCandidate ToCandidate()
        {
            return new FluentPatternCandidate
            {
                StartIndex = StartIndex,
                EndIndex = EndIndex,
                Shape = $"argument range check, upper constant at {Upper.ConstantIndex}",
                Confidence = IsSafe && IsExactBounds ? "safe-equivalent" : "diagnostic",
                Notes = Notes != null ? Notes.ToArray() : new string[0]
            };
        }
    }

    internal enum FluentRangeAmbiguityPolicy
    {
        RequireSingle,
        PatchFirst,
        PatchAll,
        Occurrence
    }

    /// <summary>
    /// High-level recipes for common argument validation shapes.
    /// </summary>
    public static class FluentTranspilerRangeCheckRecipes
    {
        public static FluentArgumentSelection ForArgument(this FluentTranspiler transpiler, int argumentIndex)
        {
            return new FluentArgumentSelection(transpiler, argumentIndex);
        }

        public static FluentBoundCheckSelection InUpperBoundCheck(this FluentArgumentSelection argument, int upperBound)
        {
            return argument.InUpperBoundCheck(upperBound);
        }

        public static FluentReplacementResult ReplaceArgumentRangeUpperBoundWithCall(
            this FluentTranspiler transpiler,
            int argumentIndex,
            int lowerBound,
            int upperBound,
            MethodInfo replacementMethod,
            SearchMode mode = SearchMode.Start)
        {
            return transpiler.ForArgument(argumentIndex)
                             .InRangeCheck(lowerBound, upperBound)
                             .ReplaceUpperBoundWithCall(replacementMethod, mode);
        }

        public static FluentReplacementResult ReplaceArgumentRangeUpperBoundWithCallOrCompatibleProvider(
            this FluentTranspiler transpiler,
            int argumentIndex,
            int lowerBound,
            int upperBound,
            MethodInfo replacementMethod,
            Func<MethodInfo, bool> compatibleProviderPredicate,
            string editLabel = null,
            SearchMode mode = SearchMode.Start)
        {
            return transpiler.ForArgument(argumentIndex)
                             .InRangeCheck(lowerBound, upperBound)
                             .ReplaceUpperBoundWithCallOrCompatibleProvider(
                                 replacementMethod,
                                 compatibleProviderPredicate,
                                 editLabel,
                                 mode);
        }
    }

    public sealed class FluentArgumentSelection
    {
        private readonly FluentTranspiler _transpiler;
        private readonly int _argumentIndex;

        internal FluentArgumentSelection(FluentTranspiler transpiler, int argumentIndex)
        {
            _transpiler = transpiler;
            _argumentIndex = argumentIndex;
        }

        internal FluentTranspiler Transpiler
        {
            get { return _transpiler; }
        }

        internal int ArgumentIndex
        {
            get { return _argumentIndex; }
        }

        public FluentRangeCheckSelection InRangeCheck(int lowerBound, int upperBound)
        {
            return new FluentRangeCheckSelection(_transpiler, _argumentIndex, lowerBound, upperBound);
        }

        /// <summary>
        /// Matches a single upper-bound check for this argument.
        /// </summary>
        /// <example>
        /// <code>
        /// t.ForArgument(1)
        ///  .InUpperBoundCheck(4)
        ///  .ReplaceBoundWithCall(getMaxPriority);
        /// </code>
        /// </example>
        public FluentBoundCheckSelection InUpperBoundCheck(int upperBound)
        {
            return new FluentBoundCheckSelection(_transpiler, _argumentIndex, RangeComparisonKind.Upper, upperBound);
        }

        /// <summary>
        /// Matches a single lower-bound check for this argument.
        /// </summary>
        /// <example>
        /// <code>
        /// t.ForArgument(1)
        ///  .InLowerBoundCheck(0)
        ///  .ReplaceBoundWithConstant(0);
        /// </code>
        /// </example>
        public FluentBoundCheckSelection InLowerBoundCheck(int lowerBound)
        {
            return new FluentBoundCheckSelection(_transpiler, _argumentIndex, RangeComparisonKind.Lower, lowerBound);
        }
    }

    public sealed class FluentRangeCheckSelection
    {
        private readonly FluentTranspiler _transpiler;
        private readonly int _argumentIndex;
        private readonly int _lowerBound;
        private readonly int _upperBound;
        private string _name;
        private string _editLabel;
        private string _failureMessage;
        private bool _optional;
        private bool _strict;
        private FluentRangeAmbiguityPolicy _ambiguityPolicy = FluentRangeAmbiguityPolicy.RequireSingle;
        private int _occurrenceIndex;

        internal FluentRangeCheckSelection(
            FluentTranspiler transpiler,
            int argumentIndex,
            int lowerBound,
            int upperBound)
        {
            _transpiler = transpiler;
            _argumentIndex = argumentIndex;
            _lowerBound = lowerBound;
            _upperBound = upperBound;
        }

        public FluentRangeCheckSelection Named(string name)
        {
            _name = name;
            return this;
        }

        public FluentRangeCheckSelection WithEditLabel(string editLabel)
        {
            _editLabel = editLabel;
            return this;
        }

        public FluentRangeCheckSelection WithFailureMessage(string failureMessage)
        {
            _failureMessage = failureMessage;
            return this;
        }

        public FluentRangeCheckSelection Optional()
        {
            _optional = true;
            return this;
        }

        public FluentRangeCheckSelection Strict()
        {
            _strict = true;
            return this;
        }

        public FluentRangeCheckSelection RequireSingleMatch()
        {
            _ambiguityPolicy = FluentRangeAmbiguityPolicy.RequireSingle;
            _occurrenceIndex = 0;
            return this;
        }

        public FluentRangeCheckSelection AllowMultipleAndPatchFirst()
        {
            _ambiguityPolicy = FluentRangeAmbiguityPolicy.PatchFirst;
            _occurrenceIndex = 0;
            return this;
        }

        public FluentRangeCheckSelection AllowMultipleAndPatchAll()
        {
            _ambiguityPolicy = FluentRangeAmbiguityPolicy.PatchAll;
            _occurrenceIndex = 0;
            return this;
        }

        public FluentRangeCheckSelection AtOccurrence(int index)
        {
            _ambiguityPolicy = FluentRangeAmbiguityPolicy.Occurrence;
            _occurrenceIndex = Math.Max(0, index);
            return this;
        }

        public FluentReplacementResult ReplaceUpperBoundWithCall(MethodInfo replacementMethod, SearchMode mode = SearchMode.Start)
        {
            return ReplaceUpperBoundWithCall(replacementMethod, mode, true);
        }

        private FluentReplacementResult ReplaceUpperBoundWithCall(MethodInfo replacementMethod, SearchMode mode, bool recordFailureDiagnostic)
        {
            if (!IsValid() || !IsValidStaticReplacementMethod(replacementMethod, nameof(ReplaceUpperBoundWithCall)))
            {
                if (recordFailureDiagnostic)
                {
                    AddPatchDiagnostic(
                        "replacement method compatible with an int upper-bound constant.",
                        "signature mismatch or invalid replacement method: " + FluentTranspilerFormatting.FormatMethod(replacementMethod) + ".",
                        "patch skipped; original IL returned.",
                        FluentPatchSeverity.Critical);
                }

                return FluentReplacementResult.NoMatch;
            }

            string signatureReason = "validation failed";
            if (!FluentTranspilerRecipeValidation.ValidateIntCompatibleReturn(
                    _transpiler,
                    replacementMethod,
                    nameof(ReplaceUpperBoundWithCall)) ||
                !FluentTranspilerRecipeValidation.ValidateParameterCount(
                    _transpiler,
                    replacementMethod,
                    0,
                    nameof(ReplaceUpperBoundWithCall)) ||
                !FluentTranspilerCompatibilityExtensions.IsCompatibleConstantReplacement(
                replacementMethod,
                typeof(int),
                out signatureReason))
            {
                if (recordFailureDiagnostic)
                {
                    AddPatchDiagnostic(
                        "replacement method compatible with an int upper-bound constant.",
                        "signature mismatch: " + signatureReason + ".",
                        "patch skipped; original IL returned.",
                        FluentPatchSeverity.Critical);
                }

                return FluentReplacementResult.Failed;
            }

            var matches = FluentTranspilerPatternDiscovery
                .DiscoverRangeChecks(_transpiler, _argumentIndex, _lowerBound, _upperBound)
                .Where(match => IsInSearchWindow(match.StartIndex, mode))
                .ToList();

            FluentReplacementResult result = PatchSelectedMatches(matches, replacementMethod, recordFailureDiagnostic);
            if (recordFailureDiagnostic && result != FluentReplacementResult.PatternReplaced)
            {
                AddPatchDiagnostic(
                    ExpectedRangeShape(),
                    DescribeRangeEvidence(result, matches),
                    "patch skipped; original IL returned.",
                    SeverityFor(result));
            }

            return result;
        }

        public FluentReplacementResult ReplaceUpperBoundWithCallOrCompatibleProvider(
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

        public FluentReplacementResult ReplaceUpperBoundWithCallOrFallbackCall(
            MethodInfo replacementMethod,
            Func<MethodInfo, bool> fallbackMethodPredicate,
            string editLabel = null,
            SearchMode mode = SearchMode.Start)
        {
            FluentReplacementResult rangeResult = ReplaceUpperBoundWithCall(replacementMethod, mode, false);
            if (rangeResult == FluentReplacementResult.PatternReplaced)
            {
                return rangeResult;
            }

            int fallbackReplacements = _transpiler.ReplaceMatchingCalls(
                fallbackMethodPredicate,
                replacementMethod,
                editLabel);

            if (fallbackReplacements > 0)
            {
                return FluentReplacementResult.FallbackCallReplaced;
            }

            if (replacementMethod != null && _transpiler.HasMatchingCall(method => method == replacementMethod))
            {
                AddPatchDiagnostic(
                    ExpectedRangeShape(),
                    "replacement call already present: " + FluentTranspilerFormatting.FormatMethod(replacementMethod) + ".",
                    "patch already applied; no IL changes made.",
                    FluentPatchSeverity.Info,
                    editLabel);
                return FluentReplacementResult.ReplacementAlreadyPresent;
            }

            var matches = FluentTranspilerPatternDiscovery
                .DiscoverRangeChecks(_transpiler, _argumentIndex, _lowerBound, _upperBound)
                .Where(match => IsInSearchWindow(match.StartIndex, mode))
                .ToList();
            AddPatchDiagnostic(
                ExpectedRangeShape(),
                DescribeRangeEvidence(rangeResult, matches),
                "patch skipped; original IL returned.",
                SeverityFor(rangeResult),
                editLabel);
            return rangeResult;
        }

        private FluentReplacementResult PatchSelectedMatches(List<RangeCheckMatch> matches, MethodInfo replacementMethod, bool recordLowLevelDiagnostics)
        {
            if (matches.Count == 0)
            {
                if (recordLowLevelDiagnostics)
                {
                    ReportNoRangeCandidate();
                }
                return FluentReplacementResult.NoMatch;
            }

            var unsafeMatches = matches.Where(match => !match.IsSafe).ToList();
            var safeMatches = matches.Where(match => match.IsSafe).ToList();
            if (safeMatches.Count == 0)
            {
                if (recordLowLevelDiagnostics)
                {
                    _transpiler.AddWarning("Range-check candidate was found but is unsafe to patch. " +
                                           _transpiler.DescribeCandidateWindow(unsafeMatches[0].ToCandidate()));
                }
                return FluentReplacementResult.UnsafeMatch;
            }

            List<RangeCheckMatch> selected;
            switch (_ambiguityPolicy)
            {
                case FluentRangeAmbiguityPolicy.PatchFirst:
                    selected = safeMatches.Take(1).ToList();
                    break;
                case FluentRangeAmbiguityPolicy.PatchAll:
                    selected = safeMatches;
                    break;
                case FluentRangeAmbiguityPolicy.Occurrence:
                    if (_occurrenceIndex >= safeMatches.Count)
                    {
                        if (recordLowLevelDiagnostics)
                        {
                            _transpiler.AddWarning($"AtOccurrence({_occurrenceIndex}) requested but only {safeMatches.Count} safe range-check candidate(s) were found.");
                        }
                        return FluentReplacementResult.NoMatch;
                    }
                    selected = new List<RangeCheckMatch> { safeMatches[_occurrenceIndex] };
                    break;
                default:
                    if (safeMatches.Count != 1)
                    {
                        if (recordLowLevelDiagnostics)
                        {
                            _transpiler.AddWarning($"Range-check pattern is ambiguous: found {safeMatches.Count} safe candidate(s). " +
                                                   "Use RequireSingleMatch, AllowMultipleAndPatchFirst, AllowMultipleAndPatchAll, or AtOccurrence(index) explicitly. " +
                                                   _transpiler.DescribeCandidateWindow(safeMatches[0].ToCandidate()));
                        }
                        return FluentReplacementResult.AmbiguousMatch;
                    }
                    selected = safeMatches;
                    break;
            }

            int patched = 0;
            foreach (RangeCheckMatch match in selected.OrderByDescending(match => match.Upper.ConstantIndex))
            {
                CodeInstruction[] replacement = BuildReplacementInstructions(replacementMethod, match.Upper.Adjustment);
                _transpiler.MoveTo(match.Upper.ConstantIndex)
                           .ReplaceSequence(1, replacement);
                patched++;
            }

            if (patched > 0)
            {
                _transpiler.AddNote($"Patched {patched} range-check upper-bound candidate(s).");
                return FluentReplacementResult.PatternReplaced;
            }

            return FluentReplacementResult.NoMatch;
        }

        private void ReportNoRangeCandidate()
        {
            var allConstants = _transpiler.FindNearbyConstants(0, int.MaxValue, _upperBound).ToList();
            var branches = _transpiler.FindNearbyBranches(0, int.MaxValue).ToList();
            var argumentLoads = _transpiler.FindNearbyArgumentLoads(_argumentIndex, 0, int.MaxValue).ToList();

            string constantDetail = allConstants.Count > 0
                ? " Found constants: " + string.Join(", ", allConstants.Select(candidate => candidate.Shape).Distinct().Take(8).ToArray()) + "."
                : " No nearby int constants found.";

            string branchDetail = branches.Count > 0
                ? " Branch opcodes found: " + string.Join(", ", branches.Select(candidate => candidate.Shape).Distinct().Take(8).ToArray()) + "."
                : " No nearby branch opcodes found.";

            string argumentDetail = argumentLoads.Count > 0
                ? $" Argument {_argumentIndex} loads found: {argumentLoads.Count}."
                : $" No loads of argument {_argumentIndex} found.";

            _transpiler.AddSoftFailure(
                $"Expected argument {_argumentIndex} range check {_lowerBound}..{_upperBound} was not found." +
                constantDetail +
                branchDetail +
                argumentDetail);
        }

        private string DescribeRangeEvidence(FluentReplacementResult result, List<RangeCheckMatch> matches)
        {
            matches = matches ?? new List<RangeCheckMatch>();
            if (result == FluentReplacementResult.AmbiguousMatch)
            {
                return "multiple matches: found " + matches.Count + " matching argument range checks.";
            }

            if (result == FluentReplacementResult.UnsafeMatch)
            {
                RangeCheckMatch unsafeMatch = matches.FirstOrDefault(match => !match.IsSafe);
                string notes = unsafeMatch != null && unsafeMatch.Notes != null && unsafeMatch.Notes.Count > 0
                    ? string.Join("; ", unsafeMatch.Notes.ToArray())
                    : "labels or exception block markers occur inside the candidate span.";
                return "unsafe labels or exception block involved: " + notes;
            }

            bool lowerMatched = HasArgumentConstantBranch(_lowerBound);
            bool upperMatched = HasArgumentConstantBranch(_upperBound);
            bool upperNearby = _transpiler.FindNearbyConstants(0, int.MaxValue, _upperBound).Any();
            bool unsupportedUpper = HasUnsupportedUpperBranch();

            if (lowerMatched && !upperMatched)
            {
                if (unsupportedUpper)
                {
                    return "lower-bound check for " + _lowerBound + ", but upper-bound check for " + _upperBound + " used unsupported branch polarity.";
                }

                if (upperNearby)
                {
                    return "lower-bound check for " + _lowerBound + ", and constant " + _upperBound + " was nearby, but not in the expected upper-bound branch shape.";
                }

                return "lower-bound check for " + _lowerBound + ", but no upper-bound check for " + _upperBound + ".";
            }

            if (!lowerMatched && upperMatched)
            {
                return "upper-bound check for " + _upperBound + ", but no lower-bound check for " + _lowerBound + ".";
            }

            if (upperNearby)
            {
                return "constant " + _upperBound + " was present, but not in an argument " + _argumentIndex + " range check from " + _lowerBound + " to " + _upperBound + ".";
            }

            return "no matching argument " + _argumentIndex + " range check from " + _lowerBound + " to " + _upperBound + ".";
        }

        private bool HasArgumentConstantBranch(int constantValue)
        {
            var instructions = _transpiler.Instructions().ToList();
            for (int i = 0; i <= instructions.Count - 3; i++)
            {
                int value;
                if (instructions[i].IsLoadArgument(_argumentIndex) &&
                    FluentTranspilerPatternDiscovery.TryGetLdcI4(instructions[i + 1], out value) &&
                    value == constantValue &&
                    instructions[i + 2].opcode.FlowControl == FlowControl.Cond_Branch)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasUnsupportedUpperBranch()
        {
            var instructions = _transpiler.Instructions().ToList();
            for (int i = 0; i <= instructions.Count - 3; i++)
            {
                int value;
                if (instructions[i].IsLoadArgument(_argumentIndex) &&
                    FluentTranspilerPatternDiscovery.TryGetLdcI4(instructions[i + 1], out value) &&
                    value == _upperBound &&
                    instructions[i + 2].opcode.FlowControl == FlowControl.Cond_Branch &&
                    instructions[i + 2].opcode != OpCodes.Ble &&
                    instructions[i + 2].opcode != OpCodes.Ble_S &&
                    instructions[i + 2].opcode != OpCodes.Bgt &&
                    instructions[i + 2].opcode != OpCodes.Bgt_S)
                {
                    return true;
                }
            }

            return false;
        }

        private void AddPatchDiagnostic(
            string expected,
            string found,
            string action,
            FluentPatchSeverity severity,
            string fallbackEditLabel = null)
        {
            _transpiler.AddPatchDiagnostic(new FluentPatchDiagnostic
            {
                RecipeName = RecipeText(),
                ExpectedShape = !string.IsNullOrEmpty(_failureMessage) ? _failureMessage : expected,
                FoundShape = found,
                ActionTaken = action,
                Severity = severity,
                EditLabel = EffectiveEditLabel(fallbackEditLabel)
            });
        }

        private FluentPatchSeverity SeverityFor(FluentReplacementResult result)
        {
            if (_strict || result == FluentReplacementResult.UnsafeMatch || result == FluentReplacementResult.Failed)
            {
                return FluentPatchSeverity.Critical;
            }

            return _optional ? FluentPatchSeverity.Info : FluentPatchSeverity.Warning;
        }

        private string EffectiveEditLabel(string fallbackEditLabel)
        {
            if (!string.IsNullOrEmpty(_editLabel))
            {
                return _editLabel;
            }

            if (!string.IsNullOrEmpty(fallbackEditLabel))
            {
                return fallbackEditLabel;
            }

            return _name;
        }

        private string RecipeText()
        {
            return "ForArgument(" + _argumentIndex + ").InRangeCheck(" + _lowerBound + ", " + _upperBound + ").ReplaceUpperBoundWithCall(GetMaxPriority)";
        }

        private string ExpectedRangeShape()
        {
            return "argument " + _argumentIndex + " range check from " + _lowerBound + " to " + _upperBound + ".";
        }

        private bool IsValid()
        {
            if (_transpiler == null)
            {
                return false;
            }

            if (_argumentIndex < 0)
            {
                _transpiler.AddWarning($"ForArgument received invalid argument index {_argumentIndex}.");
                return false;
            }

            if (_lowerBound > _upperBound)
            {
                _transpiler.AddWarning($"InRangeCheck lower bound {_lowerBound} is greater than upper bound {_upperBound}.");
                return false;
            }

            return true;
        }

        private bool IsInSearchWindow(int candidateStart, SearchMode mode)
        {
            if (mode == SearchMode.Start)
            {
                return true;
            }

            int currentIndex = Math.Max(0, _transpiler.CurrentIndex);
            if (mode == SearchMode.Current)
            {
                return candidateStart >= currentIndex;
            }

            return candidateStart > currentIndex;
        }

        private bool IsValidStaticReplacementMethod(MethodInfo method, string caller)
        {
            return FluentTranspilerRecipeValidation.ValidateStaticMethod(_transpiler, method, caller);
        }

        private static CodeInstruction[] BuildReplacementInstructions(MethodInfo replacementMethod, int adjustment)
        {
            if (adjustment == 0)
            {
                return new[] { new CodeInstruction(OpCodes.Call, replacementMethod) };
            }

            return new[]
            {
                new CodeInstruction(OpCodes.Call, replacementMethod),
                new CodeInstruction(OpCodes.Ldc_I4, adjustment),
                new CodeInstruction(OpCodes.Add)
            };
        }

    }

    public sealed class FluentBoundCheckSelection
    {
        private readonly FluentTranspiler _transpiler;
        private readonly int _argumentIndex;
        private readonly RangeComparisonKind _kind;
        private readonly int _boundValue;

        internal FluentBoundCheckSelection(
            FluentTranspiler transpiler,
            int argumentIndex,
            RangeComparisonKind kind,
            int boundValue)
        {
            _transpiler = transpiler;
            _argumentIndex = argumentIndex;
            _kind = kind;
            _boundValue = boundValue;
        }

        /// <summary>
        /// Replaces the matched bound constant with a parameterless static provider call.
        /// </summary>
        public FluentReplacementResult ReplaceBoundWithCall(MethodInfo replacementMethod, SearchMode mode = SearchMode.Start)
        {
            if (!FluentTranspilerRecipeValidation.ValidateIntCompatibleReturn(
                    _transpiler,
                    replacementMethod,
                    nameof(ReplaceBoundWithCall)) ||
                !FluentTranspilerRecipeValidation.ValidateParameterCount(
                    _transpiler,
                    replacementMethod,
                    0,
                    nameof(ReplaceBoundWithCall)))
            {
                return FluentReplacementResult.UnsafeMatch;
            }

            BoundCheckCandidate candidate;
            FluentReplacementResult result = TryFindSingleCandidate(mode, out candidate);
            if (result != FluentReplacementResult.PatternReplaced)
            {
                return result;
            }

            _transpiler.ReplaceAtWithCall(candidate.ConstantIndex, replacementMethod);
            return FluentReplacementResult.PatternReplaced;
        }

        /// <summary>
        /// Replaces the matched bound constant with another literal integer.
        /// </summary>
        public FluentReplacementResult ReplaceBoundWithConstant(int replacementValue, SearchMode mode = SearchMode.Start)
        {
            BoundCheckCandidate candidate;
            FluentReplacementResult result = TryFindSingleCandidate(mode, out candidate);
            if (result != FluentReplacementResult.PatternReplaced)
            {
                return result;
            }

            _transpiler.ReplaceAt(candidate.ConstantIndex, new CodeInstruction(OpCodes.Ldc_I4, replacementValue));
            return FluentReplacementResult.PatternReplaced;
        }

        private FluentReplacementResult TryFindSingleCandidate(SearchMode mode, out BoundCheckCandidate candidate)
        {
            candidate = null;
            if (_transpiler == null)
            {
                return FluentReplacementResult.Failed;
            }

            if (_argumentIndex < 0)
            {
                _transpiler.AddWarning($"ForArgument received invalid argument index {_argumentIndex}.");
                return FluentReplacementResult.Failed;
            }

            var candidates = FindCandidates(mode).ToList();
            if (candidates.Count == 1)
            {
                candidate = candidates[0];
                return FluentReplacementResult.PatternReplaced;
            }

            if (candidates.Count == 0)
            {
                _transpiler.AddSoftFailure($"{_kind} bound check for argument {_argumentIndex} and bound {_boundValue} was not found.");
                return FluentReplacementResult.NoMatch;
            }

            _transpiler.AddWarning($"{_kind} bound check for argument {_argumentIndex} and bound {_boundValue} matched {candidates.Count} places; refusing ambiguous edit.");
            return FluentReplacementResult.AmbiguousMatch;
        }

        private IEnumerable<BoundCheckCandidate> FindCandidates(SearchMode mode)
        {
            var instructions = _transpiler.Instructions().ToList();
            var meaningful = FluentRecipeUtility.BuildMeaningfulIndex(
                instructions,
                FluentRecipeUtility.GetSearchStartIndex(_transpiler, mode));

            for (int i = 0; i <= meaningful.Count - 3; i++)
            {
                int argIndex = meaningful[i];
                int constantIndex = meaningful[i + 1];
                int branchIndex = meaningful[i + 2];
                if (!instructions[argIndex].IsLoadArgument(_argumentIndex))
                {
                    continue;
                }

                int constantValue;
                if (!FluentRecipeUtility.TryGetLdcI4Value(instructions[constantIndex], out constantValue))
                {
                    continue;
                }

                foreach (BoundCheckCandidate candidate in Classify(instructions[branchIndex].opcode, constantValue, constantIndex))
                {
                    if (candidate.Kind == _kind && candidate.NormalizedBound == _boundValue)
                    {
                        yield return candidate;
                    }
                }
            }
        }

        private static IEnumerable<BoundCheckCandidate> Classify(OpCode branch, int constantValue, int constantIndex)
        {
            if (branch == OpCodes.Blt || branch == OpCodes.Blt_S)
            {
                yield return new BoundCheckCandidate { Kind = RangeComparisonKind.Lower, NormalizedBound = constantValue, ConstantIndex = constantIndex };
                yield return new BoundCheckCandidate { Kind = RangeComparisonKind.Upper, NormalizedBound = constantValue - 1, ConstantIndex = constantIndex };
            }

            if (branch == OpCodes.Bge || branch == OpCodes.Bge_S)
            {
                yield return new BoundCheckCandidate { Kind = RangeComparisonKind.Lower, NormalizedBound = constantValue, ConstantIndex = constantIndex };
                yield return new BoundCheckCandidate { Kind = RangeComparisonKind.Upper, NormalizedBound = constantValue - 1, ConstantIndex = constantIndex };
            }

            if (branch == OpCodes.Bgt || branch == OpCodes.Bgt_S ||
                branch == OpCodes.Ble || branch == OpCodes.Ble_S)
            {
                yield return new BoundCheckCandidate { Kind = RangeComparisonKind.Upper, NormalizedBound = constantValue, ConstantIndex = constantIndex };
            }
        }

        private sealed class BoundCheckCandidate
        {
            public RangeComparisonKind Kind;
            public int NormalizedBound;
            public int ConstantIndex;
        }
    }
}
