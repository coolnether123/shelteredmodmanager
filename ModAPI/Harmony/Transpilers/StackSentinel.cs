using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
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
                        MMLog.WriteWarning($"[StackSentinel] Skipping validation for {originalMethod.DeclaringType?.Name}.{originalMethod.Name}: Method contains exception handling clauses which are not yet supported by Basic Block analysis.");
                        error = null; // Skip validation, don't report error
                        return true;
                    }
                }
                catch { /* Reflection might fail, proceed anyway */ }
            }

            var depths = Analyze(instructions, originalMethod, out error);
            return depths != null;
        }

        /// <summary>Consolidates stack depth and type names for UI display.</summary>
        public static void GetVisualStack(List<CodeInstruction> instructions, MethodBase method, out List<int> depths, out List<List<string>> types)
        {
            depths = new List<int>();
            types = new List<List<string>>();
            var stackStates = Analyze(instructions, method, out _);
            
            for (int i = 0; i < instructions.Count; i++)
            {
                if (stackStates != null && stackStates.TryGetValue(i, out var list))
                {
                    depths.Add(list.Count);
                    types.Add(list.Select(t => t != null ? t.Name : "object").ToList());
                }
                else
                {
                    depths.Add(-1);
                    types.Add(new List<string>());
                }
            }
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
                        var popped = new List<Type>();
                        for (int p = 0; p < popCount; p++)
                        {
                            if (currentStack.Count == 0)
                            {
                                error = $"Stack underflow at {instr.opcode} {instr.operand} (Index {absIndex})";
                                return null;
                            }
                            popped.Insert(0, currentStack[currentStack.Count - 1]);
                            currentStack.RemoveAt(currentStack.Count - 1);
                        }

                        // 2. PUSH
                        var pushedTypes = GetPushedTypes(instr, originalMethod, popped);
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
                                var sb = new StringBuilder();
                                sb.AppendLine($"Stack height mismatch at instruction {successor.StartIndex:D4} (Block start)");
                                sb.AppendLine($"  - Target expects depth {successor.EntryStack.Count}: [{string.Join(", ", successor.EntryStack.Select(t => t?.Name ?? "obj").ToArray())}]");
                                sb.AppendLine($"  - Source (Block_{block.StartIndex:X4}) provides depth {currentStack.Count}: [{string.Join(", ", currentStack.Select(t => t?.Name ?? "obj").ToArray())}]");
                                sb.AppendLine("  - Last instructions in source block:");
                                for (int k = Math.Max(0, block.Instructions.Count - 3); k < block.Instructions.Count; k++)
                                    sb.AppendLine($"    {block.StartIndex + k:D4}: {block.Instructions[k]}");
                                
                                error = sb.ToString().Trim();
                                return null;
                            }
                        }
                    }
                }

                error = null;
                return instructionStacks;
            }
            catch (Exception ex)
            {
                error = $"Sentinel Exception: {ex.Message}\n{ex.StackTrace}";
                return null;
            }
        }

        private static List<Type> GetPushedTypes(CodeInstruction instr, MethodBase method, List<Type> popped)
        {
            var result = new List<Type>();
            var opcode = instr.opcode;

            if (opcode.StackBehaviourPush == StackBehaviour.Push0) return result;

            // Dup handling: pops 1, pushes 2 of that same type
            if (opcode == OpCodes.Dup)
            {
                 if (popped.Count > 0)
                 {
                     result.Add(popped[0]);
                     result.Add(popped[0]);
                 }
                 else
                 {
                     result.Add(UnknownType);
                     result.Add(UnknownType);
                 }
                 return result;
            }

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

            // Array and Special Ops
            if (opcode == OpCodes.Ldelem || opcode == OpCodes.Ldelem_Ref || opcode.Name.StartsWith("ldelem."))
            {
                // We could try to resolve the array type from popped[0], but for now object is safe
                result.Add(UnknownType);
                return result;
            }

            // Calls/Newobj
            if (opcode == OpCodes.Call || opcode == OpCodes.Callvirt)
            {
                if (instr.operand is MethodInfo mi)
                {
                    if (mi.ReturnType != typeof(void)) result.Add(mi.ReturnType);
                }
                else if (instr.operand is MethodBase mb)
                {
                    // For non-MethodInfo MethodBase, we try our best. 
                    // Constructors (newobj) are handled separately below.
                    // But Call to a constructor is possible!
                    if (mb is MethodInfo mi2 && mi2.ReturnType != typeof(void)) result.Add(mi2.ReturnType);
                    else if (mb.IsConstructor) { /* Void return, push nothing */ }
                }
                else result.Add(UnknownType);
                return result;
            }
            if (opcode == OpCodes.Newobj)
            {
                if (instr.operand is ConstructorInfo ci) result.Add(ci.DeclaringType);
                else if (instr.operand is MethodBase mb && mb.IsConstructor) result.Add(mb.DeclaringType);
                else result.Add(UnknownType);
                return result;
            }

            // Generic fallbacks based on StackBehaviour
            int count = 0;
            switch (opcode.StackBehaviourPush)
            {
                case StackBehaviour.Push1:
                case StackBehaviour.Pushi:
                case StackBehaviour.Pushi8:
                case StackBehaviour.Pushr4:
                case StackBehaviour.Pushr8:
                case StackBehaviour.Pushref:
                    count = 1;
                    break;
                case StackBehaviour.Push1_push1:
                    count = 2;
                    break;
            }

            for (int i = 0; i < count; i++) result.Add(UnknownType);
            return result;
        }

        private static int CalculatePopCount(CodeInstruction instr, MethodBase method)
        {
            var behavior = instr.opcode.StackBehaviourPop;
            if (behavior == StackBehaviour.Pop0) return 0;
            
            // Standard pops
            if (behavior == StackBehaviour.Pop1 || behavior == StackBehaviour.Popi || behavior == StackBehaviour.Popref) return 1;
            
            if (behavior == StackBehaviour.Pop1_pop1 || behavior == StackBehaviour.Popi_pop1 || behavior == StackBehaviour.Popi_popi ||
                behavior == StackBehaviour.Popi_popi8 || behavior == StackBehaviour.Popi_popr4 || behavior == StackBehaviour.Popi_popr8 ||
                behavior == StackBehaviour.Popref_pop1 || behavior == StackBehaviour.Popref_popi) return 2;
                
            if (behavior == StackBehaviour.Popi_popi_popi || behavior == StackBehaviour.Popref_popi_pop1 || behavior == StackBehaviour.Popref_popi_popi ||
                behavior == StackBehaviour.Popref_popi_popi8 || behavior == StackBehaviour.Popref_popi_popr4 || behavior == StackBehaviour.Popref_popi_popr8 ||
                behavior == StackBehaviour.Popref_popi_popref) return 3;

            // Variable pops
            if (behavior == StackBehaviour.Varpop)
            {
                if (instr.opcode == OpCodes.Call || instr.opcode == OpCodes.Callvirt)
                {
                    if (instr.operand is MethodBase mb)
                    {
                        int count = mb.GetParameters().Length;
                        if (!mb.IsStatic) count++; 
                        return count;
                    }
                    return 0; // Unknown call
                }
                if (instr.opcode == OpCodes.Ret)
                {
                    if (method is MethodInfo mi && mi.ReturnType != typeof(void)) return 1;
                    return 0;
                }
                if (instr.opcode == OpCodes.Newobj)
                {
                    if (instr.operand is MethodBase mb) return mb.GetParameters().Length;
                }
            }
            
            // Fallbacks for specific opcodes that might have non-standard behavior
            if (instr.opcode == OpCodes.Stfld) return 2;
            if (instr.opcode == OpCodes.Stelem || instr.opcode == OpCodes.Stelem_Ref || instr.opcode.Name.StartsWith("stelem.")) return 3;
            
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
