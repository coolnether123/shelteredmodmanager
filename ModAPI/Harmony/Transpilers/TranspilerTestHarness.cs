using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;

namespace ModAPI.Harmony
{
    public enum TranspilerWarningLevel
    {
        Info,       // DeclareLocal, position logging
        Warning,    // Stack analysis soft failures
        Error       // No match found, invalid operation
    }

    public class TranspilerWarning
    {
        public TranspilerWarningLevel Level;
        public string Message;
        public int? InstructionIndex;
        public string Operation; // "MatchCall", "ReplaceWith", etc.

        public override string ToString() => $"[{Level}] {Operation}: {Message} {(InstructionIndex.HasValue ? $"@ {InstructionIndex}" : "")}";
    }

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
        /// Runs the full transpilation process and returns the final instructions.
        /// Throws if any matching operations failed (if AssertValid was called).
        /// </summary>
        public static List<CodeInstruction> RunTest(FluentTranspiler transpiler)
        {
            return transpiler.Instructions().ToList();
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
                throw new Exception(message + ": " + (transpiler.Warnings.LastOrDefault() ?? "No details"));
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
    }
}
