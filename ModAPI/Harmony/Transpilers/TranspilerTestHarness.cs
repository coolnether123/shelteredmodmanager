using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using ModAPI.Util;

namespace ModAPI.Harmony
{
    /// <summary>
    /// Test harness for FluentTranspiler logic without needing a running game instance.
    /// Useful for unit testing transpilers.
    /// </summary>
    public static class TranspilerTestHarness
    {
        /// <summary>
        /// Creates a FluentTranspiler from raw instructions for testing.
        /// No ILGenerator, no original method — pure instruction manipulation testing.
        /// </summary>
        public static FluentTranspiler FromInstructions(params CodeInstruction[] instructions)
        {
            return FluentTranspiler.For(instructions);
        }

        /// <summary>
        /// Creates a FluentTranspiler with the same context supplied by a real Harmony transpiler.
        /// </summary>
        public static FluentTranspiler FromInstructions(IEnumerable<CodeInstruction> instructions, MethodBase originalMethod, ILGenerator generator = null)
        {
            return FluentTranspiler.For(instructions, originalMethod, generator);
        }

        /// <summary>
        /// Runs the full transpilation process and returns the final instructions.
        /// Throws if any matching operations failed (if AssertValid was called).
        /// </summary>
        public static List<CodeInstruction> RunTest(FluentTranspiler transpiler, bool strict = true, bool validateStack = true)
        {
            return transpiler.Build(strict: strict, validateStack: validateStack).ToList();
        }

        public static List<CodeInstruction> RunTest(FluentTranspiler transpiler, FluentTranspiler.BuildProfile profile)
        {
            return transpiler.Build(profile).ToList();
        }

        /// <summary>
        /// Validates stack depth and types. Throws on error.
        /// </summary>
        public static void RunStackAnalysis(IEnumerable<CodeInstruction> instructions, out string error)
        {
            if (!StackSentinel.Validate(instructions.ToList(), null, out error))
            {
               throw new Exception("Stack Analysis Failed: " + error);
            }
        }

        /// <summary>
        /// Asserts that a match was found at the current position.
        /// </summary>
        public static void AssertMatch(FluentTranspiler transpiler, string message = "Expected match not found")
        {
            if (!transpiler.HasMatch)
                throw new Exception(message + ": " + (transpiler.SoftFailures.LastOrDefault() ?? transpiler.Warnings.LastOrDefault() ?? "No details"));
        }

        /// <summary>
        /// Asserts that the instruction at index matches expectations.
        /// </summary>
        public static void AssertInstruction(
            IEnumerable<CodeInstruction> result, 
            int index, OpCode expectedOpcode,
            object expectedOperand = null)
        {
            var list = result.ToList();
            if (index < 0 || index >= list.Count)
                throw new Exception(
                    $"Index {index} out of range " +
                    $"(total {list.Count} instructions)");

            var instr = list[index];
            if (instr.opcode != expectedOpcode)
                throw new Exception(
                    $"Index {index}: expected {expectedOpcode}," +
                    $" got {instr.opcode}");

            if (expectedOperand != null)
            {
                if (instr.operand == null)
                     throw new Exception($"Index {index}: expected operand {expectedOperand}, got null");
                
                if (!Equals(instr.operand, expectedOperand))
                {
                    // Basic string comparison fallback for complex types
                    if (instr.operand.ToString() != expectedOperand.ToString())
                    {
                        throw new Exception(
                            $"Index {index}: expected operand " +
                            $"{expectedOperand}, got {instr.operand}");
                    }
                }
            }
        }

        /// <summary>
        /// Exercises range-check discovery against compiler-equivalent shapes without needing a running game.
        /// The returned strings are compact pass/fail case summaries for ad hoc debug tooling.
        /// </summary>
        public static IReadOnlyList<string> RunPatternDiscoveryHarnessCases()
        {
            MethodInfo replacement = typeof(TranspilerTestHarness).GetMethod(
                nameof(PatternDiscoveryReplacementProvider),
                BindingFlags.Static | BindingFlags.NonPublic);

            var results = new List<string>();
            results.Add(RunPatternDiscoveryCase("exact range check", replacement, FluentReplacementResult.PatternReplaced, ExactRangeCheck()));
            results.Add(RunPatternDiscoveryCase("upper-first range check", replacement, FluentReplacementResult.PatternReplaced, UpperFirstRangeCheck()));
            results.Add(RunPatternDiscoveryCase("> upper form", replacement, FluentReplacementResult.PatternReplaced, GreaterThanUpperRangeCheck()));
            results.Add(RunPatternDiscoveryCase(">= upper + 1 form", replacement, FluentReplacementResult.PatternReplaced, GreaterOrEqualUpperPlusOneRangeCheck()));
            results.Add(RunPatternDiscoveryCase("missing upper constant with nearby different constant", replacement, FluentReplacementResult.NoMatch, DifferentUpperConstantRangeCheck()));
            results.Add(RunPatternDiscoveryCase("two range checks causing ambiguity", replacement, FluentReplacementResult.AmbiguousMatch, TwoRangeChecks()));
            results.Add(RunPatternDiscoveryCase("nop gap", replacement, FluentReplacementResult.PatternReplaced, NopGapRangeCheck()));
            results.Add(RunPatternDiscoveryCase("unsafe branch target", replacement, FluentReplacementResult.UnsafeMatch, LabelBoundaryRangeCheck()));
            results.AddRange(RunRecipeExpansionHarnessCases());
            results.AddRange(RunIntentionalFailureDiagnosticCases());
            results.AddRange(RunRecipeSignatureSafetyHarnessCases());
            return results.ToReadOnlyList();
        }

        /// <summary>
        /// Runs every built-in harness case (pattern discovery, recipe expansion, intentional
        /// failure diagnostics, signature safety, and anchor mapping) and returns their compact
        /// pass/fail summaries. Each line begins with "PASS" or "FAIL".
        /// </summary>
        /// <remarks>
        /// This is the single entry point a developer or agent should call to smoke-test the fluent
        /// transpiler after touching the framework. Pair it with <see cref="AssertAllHarnessCasesPass"/>
        /// to turn the results into a hard failure.
        /// </remarks>
        public static IReadOnlyList<string> RunAllHarnessCases()
        {
            var results = new List<string>();
            results.AddRange(RunPatternDiscoveryHarnessCases());
            results.AddRange(RunAnchorMapHarnessCases());
            return results.ToReadOnlyList();
        }

        /// <summary>
        /// Runs <see cref="RunAllHarnessCases"/> and throws if any case reports a failure.
        /// Suitable for wiring into an automated smoke test.
        /// </summary>
        public static void AssertAllHarnessCasesPass()
        {
            var failures = RunAllHarnessCases()
                .Where(line => line != null && line.StartsWith("FAIL", StringComparison.Ordinal))
                .ToList();

            if (failures.Count > 0)
            {
                throw new Exception(
                    "TranspilerTestHarness reported " + failures.Count + " failing case(s):\n  " +
                    string.Join("\n  ", failures.ToArray()));
            }
        }

        /// <summary>
        /// Verifies the Cartographer anchor mapper produces stable, de-duplicated anchor indices.
        /// This guards the O(n) adjacency-scoring path used by MatchIntent/FindNextAnchor.
        /// </summary>
        public static IReadOnlyList<string> RunAnchorMapHarnessCases()
        {
            var results = new List<string>();

            var transpiler = FromInstructions(
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldstr, "ANCHOR_ALPHA"),
                new CodeInstruction(OpCodes.Ldstr, "ANCHOR_BETA"),
                new CodeInstruction(OpCodes.Ret));

            AnchorReport report = transpiler.MapAnchors();

            var indices = report.SafeAnchors.Select(a => a.Index).ToList();
            bool foundUniqueStrings = indices.Contains(1) && indices.Contains(2);
            bool noDuplicateIndices = indices.Count == indices.Distinct().Count();
            bool adjacencyBoosted = report.SafeAnchors.All(a => a.UniquenessScore >= 1.2f);

            results.Add(foundUniqueStrings
                ? "PASS anchor map finds unique string anchors: [" + string.Join(",", indices.Select(i => i.ToString()).ToArray()) + "]"
                : "FAIL anchor map finds unique string anchors: expected indices 1 and 2, got [" + string.Join(",", indices.Select(i => i.ToString()).ToArray()) + "]");
            results.Add(noDuplicateIndices
                ? "PASS anchor map indices are unique"
                : "FAIL anchor map indices are unique: got duplicates in [" + string.Join(",", indices.Select(i => i.ToString()).ToArray()) + "]");
            results.Add(adjacencyBoosted
                ? "PASS anchor map adjacency scoring applied"
                : "FAIL anchor map adjacency scoring applied: an anchor scored below threshold");

            return results.ToReadOnlyList();
        }

        /// <summary>
        /// Signature-safety cases for high-level recipes that should fail before mutating unsafe IL.
        /// </summary>
        public static IReadOnlyList<string> RunRecipeSignatureSafetyHarnessCases()
        {
            var results = new List<string>();
            MethodInfo intProvider = typeof(SignatureSafetyHooks).GetMethod(nameof(SignatureSafetyHooks.IntProvider));
            MethodInfo voidProvider = typeof(SignatureSafetyHooks).GetMethod(nameof(SignatureSafetyHooks.VoidProvider));
            MethodInfo stringProvider = typeof(SignatureSafetyHooks).GetMethod(nameof(SignatureSafetyHooks.StringProvider));
            MethodInfo instanceProvider = typeof(SignatureSafetyHooks).GetMethod(
                nameof(SignatureSafetyHooks.InstanceProvider),
                BindingFlags.Instance | BindingFlags.Public);

            results.Add(RangeSignatureCase("valid int upper-bound call replacement", intProvider, FluentReplacementResult.PatternReplaced, true));
            results.Add(RangeSignatureCase("invalid void replacement method", voidProvider, FluentReplacementResult.Failed, false));
            results.Add(RangeSignatureCase("invalid string replacement method for int bound", stringProvider, FluentReplacementResult.Failed, false));
            results.Add(RangeSignatureCase("invalid instance method replacement", instanceProvider, FluentReplacementResult.NoMatch, false));
            results.Add(RemoveCallSafetyCase("RemoveCall on void method", nameof(SignatureSafetyHooks.VoidCall), true));
            results.Add(RemoveCallSafetyCase("RemoveCall on int-return method", nameof(SignatureSafetyHooks.IntCall), true));
            results.Add(RemoveCallSafetyCase("RemoveCall on struct-return method should fail safely", nameof(SignatureSafetyHooks.StructCall), false));
            results.Add(InjectBeforeCallSafetyCase("InjectBeforeCall with matching args", nameof(SignatureSafetyHooks.HookMatchingArgs), true));
            results.Add(InjectBeforeCallSafetyCase("InjectBeforeCall with wrong args should fail safely", nameof(SignatureSafetyHooks.HookWrongArgs), false));
            return results.ToReadOnlyList();
        }

        /// <summary>
        /// Exercises the broader recipe expansion surface with representative C# intent shapes.
        /// </summary>
        public static IReadOnlyList<string> RunRecipeExpansionHarnessCases()
        {
            MethodInfo max = typeof(TranspilerTestHarness).GetMethod(
                nameof(PatternDiscoveryReplacementProvider),
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo source = typeof(RecipeExpansionHooks).GetMethod(nameof(RecipeExpansionHooks.Source));
            MethodInfo replacement = typeof(RecipeExpansionHooks).GetMethod(nameof(RecipeExpansionHooks.Replacement));
            MethodInfo clamp = typeof(RecipeExpansionHooks).GetMethod(nameof(RecipeExpansionHooks.Clamp));
            MethodInfo wrap = typeof(RecipeExpansionHooks).GetMethod(nameof(RecipeExpansionHooks.WrapInt));

            var results = new List<string>();
            results.Add(RecipeCase(
                "simple 0..4 range upper-bound replacement",
                FluentReplacementResult.PatternReplaced,
                FromInstructions(ExactRangeCheck())
                    .ForArgument(2)
                    .InRangeCheck(0, 4)
                    .ReplaceUpperBoundWithCall(max)));
            results.Add(RecipeCase(
                "inverted upper-bound replacement",
                FluentReplacementResult.PatternReplaced,
                FromInstructions(GreaterOrEqualUpperPlusOneRangeCheck())
                    .ForArgument(2)
                    .InRangeCheck(0, 4)
                    .ReplaceUpperBoundWithCall(max)));
            results.Add(RecipeCase(
                "clamp upper-bound replacement",
                FluentReplacementResult.PatternReplaced,
                FromInstructions(
                    new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Ldc_I4_0),
                    new CodeInstruction(OpCodes.Ldc_I4_4),
                    new CodeInstruction(OpCodes.Call, clamp))
                    .ForArgument(1)
                    .InClamp(0, 4)
                    .ReplaceUpperBoundWithCall(max)));
            results.Add(RecipeCase(
                "call replacement",
                FluentReplacementResult.PatternReplaced,
                FromInstructions(new CodeInstruction(OpCodes.Call, source))
                    .ForCall(typeof(RecipeExpansionHooks), nameof(RecipeExpansionHooks.Source))
                    .ReplaceWith(replacement)));
            results.Add(RecipeCase(
                "return-value wrapper",
                FluentReplacementResult.PatternReplaced,
                FromInstructions(new CodeInstruction(OpCodes.Ldc_I4_1), new CodeInstruction(OpCodes.Ret))
                    .Returns<int>()
                    .WrapAll(wrap)));
            results.Add(RecipeCase(
                "failure when pattern is missing",
                FluentReplacementResult.NoMatch,
                FromInstructions(new CodeInstruction(OpCodes.Ldc_I4_0))
                    .ForArgument(1)
                    .InUpperBoundCheck(4)
                    .ReplaceBoundWithCall(max)));
            results.Add(RecipeCase(
                "failure when pattern is ambiguous",
                FluentReplacementResult.AmbiguousMatch,
                FromInstructions(
                    new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Ldc_I4_4),
                    Branch(OpCodes.Ble_S),
                    new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Ldc_I4_4),
                    Branch(OpCodes.Ble_S))
                    .ForArgument(1)
                    .InUpperBoundCheck(4)
                    .ReplaceBoundWithCall(max)));
            return results.ToReadOnlyList();
        }

        /// <summary>
        /// Intentional failure cases used to verify human-readable FluentPatchDiagnostic output.
        /// </summary>
        public static IReadOnlyList<string> RunIntentionalFailureDiagnosticCases()
        {
            MethodInfo max = typeof(TranspilerTestHarness).GetMethod(
                nameof(PatternDiscoveryReplacementProvider),
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo wrongMax = typeof(TranspilerTestHarness).GetMethod(
                nameof(WrongSignatureReplacementProvider),
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo source = typeof(RecipeExpansionHooks).GetMethod(nameof(RecipeExpansionHooks.Source));
            MethodInfo replacement = typeof(RecipeExpansionHooks).GetMethod(nameof(RecipeExpansionHooks.Replacement));

            var results = new List<string>();
            results.Add(DiagnosticCase(
                "missing range check",
                FluentReplacementResult.NoMatch,
                FromInstructions(
                    new CodeInstruction(OpCodes.Ldarg_2),
                    new CodeInstruction(OpCodes.Ldc_I4_0),
                    Branch(OpCodes.Blt_S))
                    .ForArgument(2)
                    .InRangeCheck(0, 4)
                    .Named("Harness missing range check")
                    .RequireSingleMatch()
                    .ReplaceUpperBoundWithCall(max)));
            results.Add(DiagnosticCase(
                "ambiguous range checks",
                FluentReplacementResult.AmbiguousMatch,
                FromInstructions(TwoRangeChecks())
                    .ForArgument(2)
                    .InRangeCheck(0, 4)
                    .Named("Harness ambiguous range checks")
                    .RequireSingleMatch()
                    .ReplaceUpperBoundWithCall(max)));
            results.Add(DiagnosticCase(
                "wrong replacement method signature",
                FluentReplacementResult.Failed,
                FromInstructions(ExactRangeCheck())
                    .ForArgument(2)
                    .InRangeCheck(0, 4)
                    .Named("Harness wrong replacement method signature")
                    .RequireSingleMatch()
                    .ReplaceUpperBoundWithCall(wrongMax)));
            results.Add(DiagnosticCase(
                "unsafe branch target",
                FluentReplacementResult.UnsafeMatch,
                FromInstructions(LabelBoundaryRangeCheck())
                    .ForArgument(2)
                    .InRangeCheck(0, 4)
                    .Named("Harness unsafe branch target")
                    .RequireSingleMatch()
                    .ReplaceUpperBoundWithCall(max)));
            results.Add(DiagnosticCase(
                "missing method call",
                FluentReplacementResult.NoMatch,
                FromInstructions(new CodeInstruction(OpCodes.Ldc_I4_0))
                    .ReplaceCalls(source)
                    .Named("Harness missing method call")
                    .RequireSingleMatch()
                    .WithCall(replacement)));
            return results.ToReadOnlyList();
        }

        private static string DiagnosticCase(string name, FluentReplacementResult expected, FluentReplacementResult actual)
        {
            return actual == expected
                ? $"PASS diagnostic {name}: {actual}"
                : $"FAIL diagnostic {name}: expected {expected}, got {actual}";
        }

        private static string RecipeCase(string name, FluentReplacementResult expected, FluentReplacementResult actual)
        {
            return actual == expected
                ? $"PASS {name}: {actual}"
                : $"FAIL {name}: expected {expected}, got {actual}";
        }

        private static string RunPatternDiscoveryCase(
            string name,
            MethodInfo replacement,
            FluentReplacementResult expected,
            CodeInstruction[] instructions)
        {
            FluentReplacementResult actual = FromInstructions(instructions)
                .ForArgument(2)
                .InRangeCheck(0, 4)
                .ReplaceUpperBoundWithCall(replacement);

            return actual == expected
                ? $"PASS {name}: {actual}"
                : $"FAIL {name}: expected {expected}, got {actual}";
        }

        private static string RangeSignatureCase(string name, MethodInfo replacement, FluentReplacementResult expected, bool expectMutation)
        {
            var transpiler = FromInstructions(ExactRangeCheck());
            FluentReplacementResult actual = transpiler.ForArgument(2)
                .InRangeCheck(0, 4)
                .ReplaceUpperBoundWithCall(replacement);
            bool mutated = replacement != null &&
                           transpiler.Instructions().Any(instruction => instruction != null && instruction.Calls(replacement));
            bool passed = actual == expected && mutated == expectMutation;
            return passed
                ? $"PASS signature {name}: {actual}"
                : $"FAIL signature {name}: expected {expected}/mutated={expectMutation}, got {actual}/mutated={mutated}";
        }

        private static string RemoveCallSafetyCase(string name, string targetMethod, bool shouldSucceed)
        {
            MethodInfo target = typeof(SignatureSafetyHooks).GetMethod(targetMethod);
            var instructions = new List<CodeInstruction> { new CodeInstruction(OpCodes.Call, target) };
            if (target.ReturnType != typeof(void))
            {
                instructions.Add(new CodeInstruction(OpCodes.Pop));
            }

            var transpiler = FromInstructions(instructions.ToArray());
            transpiler.RemoveCall(typeof(SignatureSafetyHooks), targetMethod);
            bool stillHasCall = transpiler.Instructions().Any(instruction =>
                instruction != null &&
                instruction.opcode == OpCodes.Call &&
                Equals(instruction.operand, target));
            bool removed = !stillHasCall;
            bool passed = removed == shouldSucceed;
            return passed
                ? $"PASS signature {name}: removed={removed}"
                : $"FAIL signature {name}: expected removed={shouldSucceed}, got removed={removed}";
        }

        private static string InjectBeforeCallSafetyCase(string name, string hookMethod, bool shouldSucceed)
        {
            MethodInfo target = typeof(SignatureSafetyHooks).GetMethod(nameof(SignatureSafetyHooks.VoidCall));
            MethodInfo enclosing = typeof(SignatureSafetyHooks).GetMethod(nameof(SignatureSafetyHooks.EnclosingMethod));
            var transpiler = FromInstructions(new[] { new CodeInstruction(OpCodes.Call, target) }, enclosing);
            transpiler.InjectBeforeCall(
                typeof(SignatureSafetyHooks),
                nameof(SignatureSafetyHooks.VoidCall),
                typeof(SignatureSafetyHooks),
                hookMethod);
            bool hasWarning = transpiler.Warnings.Count > 0;
            bool passed = shouldSucceed ? !hasWarning : hasWarning;
            return passed
                ? $"PASS signature {name}: warnings={hasWarning}"
                : $"FAIL signature {name}: expected success={shouldSucceed}, warnings={hasWarning}";
        }

        private static int PatternDiscoveryReplacementProvider()
        {
            return 4;
        }

        private static int WrongSignatureReplacementProvider(int value)
        {
            return value;
        }

        private static class RecipeExpansionHooks
        {
            public static int Source()
            {
                return 1;
            }

            public static int Replacement()
            {
                return 2;
            }

            public static int Clamp(int value, int lower, int upper)
            {
                return Math.Max(lower, Math.Min(upper, value));
            }

            public static int WrapInt(int value)
            {
                return value;
            }
        }

        private struct UnsupportedReturnStruct
        {
        }

        private class SignatureSafetyHooks
        {
            public static int IntProvider()
            {
                return 4;
            }

            public static void VoidProvider()
            {
            }

            public static string StringProvider()
            {
                return string.Empty;
            }

            public int InstanceProvider()
            {
                return 4;
            }

            public static void VoidCall()
            {
            }

            public static int IntCall()
            {
                return 1;
            }

            public static UnsupportedReturnStruct StructCall()
            {
                return new UnsupportedReturnStruct();
            }

            public static void EnclosingMethod(int value)
            {
                VoidCall();
            }

            public static void HookMatchingArgs(int value)
            {
            }

            public static void HookWrongArgs(string value)
            {
            }
        }

        private static CodeInstruction[] ExactRangeCheck()
        {
            return new[]
            {
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Ldc_I4_0),
                Branch(OpCodes.Blt_S),
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Ldc_I4_4),
                Branch(OpCodes.Ble_S)
            };
        }

        private static CodeInstruction[] UpperFirstRangeCheck()
        {
            return new[]
            {
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Ldc_I4_4),
                Branch(OpCodes.Bgt_S),
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Ldc_I4_0),
                Branch(OpCodes.Bge_S)
            };
        }

        private static CodeInstruction[] GreaterThanUpperRangeCheck()
        {
            return UpperFirstRangeCheck();
        }

        private static CodeInstruction[] GreaterOrEqualUpperPlusOneRangeCheck()
        {
            return new[]
            {
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Ldc_I4_0),
                Branch(OpCodes.Blt),
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Ldc_I4_5),
                Branch(OpCodes.Bge)
            };
        }

        private static CodeInstruction[] DifferentUpperConstantRangeCheck()
        {
            return new[]
            {
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Ldc_I4_0),
                Branch(OpCodes.Blt_S),
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Ldc_I4, 7),
                Branch(OpCodes.Ble_S)
            };
        }

        private static CodeInstruction[] TwoRangeChecks()
        {
            return ExactRangeCheck().Concat(ExactRangeCheck()).ToArray();
        }

        private static CodeInstruction[] NopGapRangeCheck()
        {
            return new[]
            {
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Nop),
                new CodeInstruction(OpCodes.Ldc_I4_0),
                new CodeInstruction(OpCodes.Nop),
                Branch(OpCodes.Blt_S),
                new CodeInstruction(OpCodes.Nop),
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Ldc_I4_4),
                new CodeInstruction(OpCodes.Nop),
                Branch(OpCodes.Ble_S)
            };
        }

        private static CodeInstruction Branch(OpCode opcode)
        {
            return new CodeInstruction(opcode, default(Label));
        }

        private static CodeInstruction[] LabelBoundaryRangeCheck()
        {
            var instructions = ExactRangeCheck();
            instructions[4].labels.Add(default(Label));
            return instructions;
        }
    }
}
