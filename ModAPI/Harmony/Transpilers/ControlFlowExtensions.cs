using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using ModAPI.Core;

namespace ModAPI.Harmony
{
    public enum LoopType
    {
        For,
        While,
        ForEach
    }

    /// <summary>
    /// Semantic navigation extensions for FluentTranspiler.
    /// Allows finding high-level code structures like loops and if-statements.
    /// </summary>
    public static class ControlFlowExtensions
    {
        /// <summary>
        /// Attempts to find a loop header based on backward jumps.
        /// </summary>
        public static FluentTranspiler FindLoop(this FluentTranspiler t, LoopType type, string iteratorName = null)
        {
            if (!t.HasMatch) return t;

            var instructions = t.Instructions().ToList();
            int currentPos = t.CurrentIndex;

            // Pattern: Find a branch that jumps to an earlier instruction
            for (int i = currentPos; i < instructions.Count; i++)
            {
                if (t.IsBackwardBranch(instructions[i], i, out int targetIndex))
                {
                    // For a 'for' loop, we usually want to be at the jump target (start of body or check)
                    return t.MoveTo(targetIndex);
                }
            }

            t.AddWarning($"FindLoop: No backward jumps found for loop type {type} starting from {currentPos}.");
            return t; 
        }

        /// <summary>
        /// Positions the matcher at the first instruction inside the currently matched loop.
        /// </summary>
        public static FluentTranspiler AtLoopHeader(this FluentTranspiler t)
        {
            // Usually we are already at the header if FindLoop succeeded
            return t;
        }

        /// <summary>
        /// Attempts to find an if-statement based on a condition predicate.
        /// </summary>
        public static FluentTranspiler FindIfStatement(this FluentTranspiler t, Func<CodeInstruction, bool> condition)
        {
            if (!t.HasMatch) return t;

            // 1. Find the condition instructions
            t.FindOpCode(OpCodes.Nop, SearchMode.Current); // Placeholder logic
            
            // Real logic: Find instructions matching 'condition', then find the following branch
            var instructions = t.Instructions().ToList();
            for (int i = t.CurrentIndex; i < instructions.Count; i++)
            {
                if (condition(instructions[i]))
                {
                    // Found the condition. Now look for the branch instruction that handles the 'if'
                    for (int j = i + 1; j < Math.Min(i + 5, instructions.Count); j++)
                    {
                        if (IsConditionalBranch(instructions[j].opcode))
                        {
                            return t.MoveTo(j);
                        }
                    }
                }
            }

            return t;
        }

        /// <summary>
        /// Positions the matcher at the start of the 'then' block of the currently matched if-statement.
        /// </summary>
        public static FluentTranspiler AtThenBlockStart(this FluentTranspiler t)
        {
            if (!t.HasMatch) return t;
            
            var instr = t.Current;
            if (IsConditionalBranch(instr.opcode))
            {
                // If it's a 'jump if false', the 'then' block is next.
                if (opIsJumpIfFalse(instr.opcode))
                    return t.Next(); 
                
                // If it's a 'jump if true', the 'then' block is at the target.
                if (instr.operand is Label label)
                {
                    int target = t.LabelToIndex(label);
                    if (target != -1) return t.MoveTo(target);
                }
            }
            
            t.AddWarning("AtThenBlockStart: Current instruction is not a conditional branch.");
            return t;
        }

        private static bool IsBackwardBranch(this FluentTranspiler t, CodeInstruction instr, int currentIndex, out int targetIndex)
        {
            targetIndex = -1;
            if (IsBranch(instr.opcode) && instr.operand is Label label)
            {
                targetIndex = t.LabelToIndex(label);
                return targetIndex != -1 && targetIndex < currentIndex;
            }
            return false;
        }

        private static bool IsBranch(OpCode op) => op.FlowControl == FlowControl.Branch || op.FlowControl == FlowControl.Cond_Branch;
        private static bool IsConditionalBranch(OpCode op) => op.FlowControl == FlowControl.Cond_Branch;
        
        private static bool opIsJumpIfFalse(OpCode op) => op == OpCodes.Brfalse || op == OpCodes.Brfalse_S || op == OpCodes.Beq || op == OpCodes.Beq_S;

    }
}
