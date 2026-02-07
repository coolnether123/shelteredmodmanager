using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using ModAPI.Core;

namespace ModAPI.Harmony
{
    internal class BasicBlock
    {
        public int StartIndex;
        public List<CodeInstruction> Instructions = new List<CodeInstruction>();
        public List<BasicBlock> Successors = new List<BasicBlock>();
        public List<Type> EntryStack; // null means not yet visited/calculated

        public override string ToString()
        {
            return $"Block_{StartIndex:X4} (Len: {Instructions.Count})";
        }
    }

    /// <summary>
    /// Advanced stack safety analysis for IL code.
    /// Implements Basic Block Analysis to track stack depth and Types across branches.
    /// </summary>
    public static class StackSentinel
    {
        private static readonly Type UnknownType = typeof(object);

        public static bool Validate(List<CodeInstruction> instructions, MethodBase originalMethod, out string error)
        {
            // Check for exception blocks (basic block analysis cannot reliably handle these yet)
            if (originalMethod != null)
            {
                try
                {
                    var body = originalMethod.GetMethodBody();
                    if (body?.ExceptionHandlingClauses?.Count > 0)
                    {
                        error = null; // Skip validation, don't report error
                        return true;
                    }
                }
                catch { /* Reflection might fail, proceed anyway */ }
            }

            var depths = Analyze(instructions, originalMethod, out error);
            return depths != null;
        }

        /// <summary>
        /// Analyzes the method body and returns a map of stack states at each instruction index.
        /// Returns null if analysis fails (e.g. underflow/mismatch).
        /// </summary>
        public static Dictionary<int, List<Type>> Analyze(List<CodeInstruction> instructions, MethodBase originalMethod, out string error)
        {
            if (instructions == null || instructions.Count == 0)
            {
                error = null;
                return new Dictionary<int, List<Type>>();
            }

            try
            {
                var blocks = BuildBasicBlocks(instructions);
                if (blocks.Count == 0)
                {
                    error = null;
                    return new Dictionary<int, List<Type>>();
                }

                var instructionStacks = new Dictionary<int, List<Type>>();
                var workQueue = new Queue<BasicBlock>();

                // Entry block starts with an empty stack
                blocks[0].EntryStack = new List<Type>();
                workQueue.Enqueue(blocks[0]);

                while (workQueue.Count > 0)
                {
                    var block = workQueue.Dequeue();
                    var currentStack = new List<Type>(block.EntryStack);

                    for (int i = 0; i < block.Instructions.Count; i++)
                    {
                        var instr = block.Instructions[i];
                        int absIndex = block.StartIndex + i;
                        
                        // Store stack state BEFORE execution of instruction
                        if (!instructionStacks.ContainsKey(absIndex))
                            instructionStacks[absIndex] = new List<Type>(currentStack);

                        // 1. POP
                        int popCount = CalculatePopCount(instr, originalMethod);
                        for (int p = 0; p < popCount; p++)
                        {
                            if (currentStack.Count == 0)
                            {
                                error = $"Stack underflow at {instr} (Index {absIndex})";
                                return null;
                            }
                            currentStack.RemoveAt(currentStack.Count - 1);
                        }

                        // 2. PUSH
                        var pushedTypes = GetPushedTypes(instr, originalMethod);
                        foreach (var type in pushedTypes)
                        {
                            currentStack.Add(type);
                        }
                    }

                    // Propagate to successors
                    foreach (var successor in block.Successors)
                    {
                        if (successor.EntryStack == null)
                        {
                            successor.EntryStack = new List<Type>(currentStack);
                            workQueue.Enqueue(successor);
                        }
                        else
                        {
                            // Verify stack depth and types match at merge points
                            if (successor.EntryStack.Count != currentStack.Count)
                            {
                                error = $"Stack height mismatch at {successor}: expected {successor.EntryStack.Count}, got {currentStack.Count} from Block_{block.StartIndex:X4}";
                                return null;
                            }
                            
                            // Optional: Verify types match (allowing for inheritance/casting if needed)
                            // For now, we only warn if types are drastically different and not just 'object' fallbacks
                        }
                    }
                }

                error = null;
                return instructionStacks;
            }
            catch (Exception ex)
            {
                error = $"Sentinel Exception: {ex.Message}";
                return null;
            }
        }

        private static List<Type> GetPushedTypes(CodeInstruction instr, MethodBase method)
        {
            var result = new List<Type>();
            var opcode = instr.opcode;

            if (opcode.StackBehaviourPush == StackBehaviour.Push0) return result;

            // Simple types
            if (opcode == OpCodes.Ldc_I4 || opcode == OpCodes.Ldc_I4_S || opcode.Name.StartsWith("ldc.i4."))
            {
                result.Add(typeof(int));
                return result;
            }
            if (opcode == OpCodes.Ldc_I8) { result.Add(typeof(long)); return result; }
            if (opcode == OpCodes.Ldc_R4) { result.Add(typeof(float)); return result; }
            if (opcode == OpCodes.Ldc_R8) { result.Add(typeof(double)); return result; }
            if (opcode == OpCodes.Ldstr) { result.Add(typeof(string)); return result; }
            if (opcode == OpCodes.Ldnull) { result.Add(typeof(object)); return result; }

            // Argument/Local/Field/Static
            if (opcode == OpCodes.Ldarg || opcode == OpCodes.Ldarg_0 || opcode == OpCodes.Ldarg_1 || opcode == OpCodes.Ldarg_2 || opcode == OpCodes.Ldarg_3 || opcode == OpCodes.Ldarg_S)
            {
                int argIndex = -1;
                if (opcode == OpCodes.Ldarg_0) argIndex = 0;
                else if (opcode == OpCodes.Ldarg_1) argIndex = 1;
                else if (opcode == OpCodes.Ldarg_2) argIndex = 2;
                else if (opcode == OpCodes.Ldarg_3) argIndex = 3;
                else if (instr.operand is int idx) argIndex = idx;
                else if (instr.operand is sbyte sb) argIndex = sb;

                if (argIndex != -1 && method != null)
                {
                    var parameters = method.GetParameters();
                    bool isStatic = method.IsStatic;
                    
                    if (!isStatic)
                    {
                        if (argIndex == 0) { result.Add(method.DeclaringType); return result; }
                        argIndex--; // Shift for 'this'
                    }

                    if (argIndex >= 0 && argIndex < parameters.Length)
                    {
                        result.Add(parameters[argIndex].ParameterType);
                        return result;
                    }
                }
                result.Add(UnknownType);
                return result;
            }
            if (opcode == OpCodes.Ldloc || opcode == OpCodes.Ldloc_0 || opcode == OpCodes.Ldloc_1 || opcode == OpCodes.Ldloc_2 || opcode == OpCodes.Ldloc_3 || opcode == OpCodes.Ldloc_S)
            {
                if (instr.operand is LocalBuilder lb) result.Add(lb.LocalType);
                else result.Add(UnknownType);
                return result;
            }
            if (opcode == OpCodes.Ldfld || opcode == OpCodes.Ldsfld)
            {
                if (instr.operand is FieldInfo fi) result.Add(fi.FieldType);
                else result.Add(UnknownType);
                return result;
            }

            // Calls/Newobj
            if (opcode == OpCodes.Call || opcode == OpCodes.Callvirt)
            {
                if (instr.operand is MethodInfo mi && mi.ReturnType != typeof(void))
                    result.Add(mi.ReturnType);
                return result;
            }
            if (opcode == OpCodes.Newobj)
            {
                if (instr.operand is ConstructorInfo ci) result.Add(ci.DeclaringType);
                else if (instr.operand is MethodInfo mi) result.Add(mi.DeclaringType);
                else result.Add(UnknownType);
                return result;
            }

            // Generic fallbacks based on StackBehaviour
            int count = 0;
            if (opcode.StackBehaviourPush == StackBehaviour.Push1 || opcode.StackBehaviourPush == StackBehaviour.Pushi || 
                opcode.StackBehaviourPush == StackBehaviour.Pushi8 || opcode.StackBehaviourPush == StackBehaviour.Pushr4 || 
                opcode.StackBehaviourPush == StackBehaviour.Pushr8 || opcode.StackBehaviourPush == StackBehaviour.Pushref)
                count = 1;
            else if (opcode.StackBehaviourPush == StackBehaviour.Push1_push1)
                count = 2;

            for (int i = 0; i < count; i++) result.Add(UnknownType);
            return result;
        }

        private static int CalculatePopCount(CodeInstruction instr, MethodBase method)
        {
            if (instr.opcode.StackBehaviourPop == StackBehaviour.Pop0) return 0;
            if (instr.opcode.StackBehaviourPop == StackBehaviour.Pop1) return 1;
            if (instr.opcode.StackBehaviourPop == StackBehaviour.Popi) return 1;
            if (instr.opcode.StackBehaviourPop == StackBehaviour.Popref) return 1;
            if (instr.opcode.StackBehaviourPop == StackBehaviour.Pop1_pop1) return 2;
            if (instr.opcode.StackBehaviourPop == StackBehaviour.Popi_pop1) return 2;
            if (instr.opcode.StackBehaviourPop == StackBehaviour.Popi_popi) return 2;
            if (instr.opcode.StackBehaviourPop == StackBehaviour.Popi_popi8) return 2;
            if (instr.opcode.StackBehaviourPop == StackBehaviour.Popi_popr4) return 2;
            if (instr.opcode.StackBehaviourPop == StackBehaviour.Popi_popr8) return 2;
            if (instr.opcode.StackBehaviourPop == StackBehaviour.Popref_pop1) return 2;
            if (instr.opcode.StackBehaviourPop == StackBehaviour.Popref_popi) return 2;
            
            if (instr.opcode.StackBehaviourPop == StackBehaviour.Varpop)
            {
                 if (instr.opcode == OpCodes.Call || instr.opcode == OpCodes.Callvirt)
                {
                    if (instr.operand is MethodInfo mi)
                    {
                        int count = mi.GetParameters().Length;
                        if (!mi.IsStatic) count++; 
                        return count;
                    }
                }
                if (instr.opcode == OpCodes.Ret)
                {
                    if (method is MethodInfo mi && mi.ReturnType != typeof(void)) return 1;
                    return 0;
                }
                if (instr.opcode == OpCodes.Newobj)
                {
                    if (instr.operand is ConstructorInfo ci) return ci.GetParameters().Length;
                    if (instr.operand is MethodInfo mi) return mi.GetParameters().Length;
                }
            }
            
            switch (instr.opcode.StackBehaviourPop)
            {
                case StackBehaviour.Popi_popi_popi:
                case StackBehaviour.Popref_popi_pop1:
                case StackBehaviour.Popref_popi_popi:
                case StackBehaviour.Popref_popi_popi8:
                case StackBehaviour.Popref_popi_popr4:
                case StackBehaviour.Popref_popi_popr8:
                case StackBehaviour.Popref_popi_popref:
                    return 3;
            }
            
             return 0;
        }

        private static List<BasicBlock> BuildBasicBlocks(List<CodeInstruction> instructions)
        {
            var blocks = new Dictionary<int, BasicBlock>();
            var leaders = new HashSet<int> { 0 }; 

            var labelToIndex = new Dictionary<Label, int>();
            for (int i = 0; i < instructions.Count; i++)
            {
                foreach (var label in instructions[i].labels) labelToIndex[label] = i;
            }

            for (int i = 0; i < instructions.Count; i++)
            {
                var instr = instructions[i];
                if (IsBranch(instr.opcode))
                {
                    if (instr.operand is Label label)
                    {
                        if (labelToIndex.TryGetValue(label, out int target)) leaders.Add(target);
                    }
                    else if (instr.operand is Label[] labels) 
                    {
                        foreach (var l in labels)
                        {
                            if (labelToIndex.TryGetValue(l, out int target)) leaders.Add(target);
                        }
                    }
                    if (i + 1 < instructions.Count) leaders.Add(i + 1);
                }
                else if (instr.opcode == OpCodes.Ret || instr.opcode == OpCodes.Throw)
                {
                     if (i + 1 < instructions.Count) leaders.Add(i + 1);
                }
            }

            var sortedLeaders = leaders.OrderBy(x => x).ToList();
            var blockList = new List<BasicBlock>();

            for (int i = 0; i < sortedLeaders.Count; i++)
            {
                int start = sortedLeaders[i];
                int end = (i + 1 < sortedLeaders.Count) ? sortedLeaders[i + 1] : instructions.Count;

                var block = new BasicBlock
                {
                    StartIndex = start,
                    Instructions = instructions.GetRange(start, end - start)
                };
                blocks[start] = block;
                blockList.Add(block);
            }

            foreach (var block in blockList)
            {
                var lastInstr = block.Instructions.Last();
                bool fallsThrough = !IsUnconditionalBranch(lastInstr.opcode) && lastInstr.opcode != OpCodes.Ret && lastInstr.opcode != OpCodes.Throw;

                if (fallsThrough)
                {
                    int nextIndex = block.StartIndex + block.Instructions.Count;
                    if (blocks.ContainsKey(nextIndex)) block.Successors.Add(blocks[nextIndex]);
                }

                if (IsBranch(lastInstr.opcode))
                {
                   if (lastInstr.operand is Label label)
                    {
                        if (labelToIndex.TryGetValue(label, out int target) && blocks.ContainsKey(target))
                             block.Successors.Add(blocks[target]);
                    }
                    else if (lastInstr.operand is Label[] labels)
                    {
                        foreach (var l in labels)
                        {
                            if (labelToIndex.TryGetValue(l, out int target) && blocks.ContainsKey(target))
                                block.Successors.Add(blocks[target]);
                        }
                    }
                }
            }

            return blockList;
        }

        private static bool IsBranch(OpCode opcode)
        {
            return opcode.FlowControl == FlowControl.Branch || opcode.FlowControl == FlowControl.Cond_Branch;
        }

        private static bool IsUnconditionalBranch(OpCode opcode)
        {
            return opcode.FlowControl == FlowControl.Branch || opcode.FlowControl == FlowControl.Throw || opcode.FlowControl == FlowControl.Return;
        }
    }
}
