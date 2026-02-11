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
        private sealed class UnknownStackTypeMarker { }
        private static readonly Type UnknownType = typeof(UnknownStackTypeMarker);

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
                        error = $"StackSentinel does not support exception-handler analysis for {originalMethod.DeclaringType?.Name}.{originalMethod.Name}. Method has {body.ExceptionHandlingClauses.Count} exception clause(s).";
                        MMLog.WriteWarning("[StackSentinel] " + error);
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    error = "StackSentinel failed to inspect exception clauses: " + ex.Message;
                    return false;
                }
            }

            if (instructions.Any(i => i != null && i.opcode == OpCodes.Endfilter))
            {
                error = "StackSentinel does not support filter exception regions (Endfilter opcode).";
                MMLog.WriteWarning("[StackSentinel] " + error);
                return false;
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
                    types.Add(list.Select(FormatStackType).ToList());
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
                            if (successor.EntryStack.Count != currentStack.Count)
                            {
                                var sb = new StringBuilder();
                                sb.AppendLine($"Stack height mismatch at instruction {successor.StartIndex:D4} (Block start)");
                                sb.AppendLine($"  - Target expects depth {successor.EntryStack.Count}: [{string.Join(", ", successor.EntryStack.Select(FormatStackType).ToArray())}]");
                                sb.AppendLine($"  - Source (Block_{block.StartIndex:X4}) provides depth {currentStack.Count}: [{string.Join(", ", currentStack.Select(FormatStackType).ToArray())}]");
                                sb.AppendLine("  - Last instructions in source block:");
                                for (int k = Math.Max(0, block.Instructions.Count - 3); k < block.Instructions.Count; k++)
                                    sb.AppendLine($"    {block.StartIndex + k:D4}: {block.Instructions[k]}");
                                
                                error = sb.ToString().Trim();
                                return null;
                            }

                            bool changed = false;
                            for (int stackIndex = 0; stackIndex < successor.EntryStack.Count; stackIndex++)
                            {
                                var merged = MergeStackType(successor.EntryStack[stackIndex], currentStack[stackIndex]);
                                if (!ReferenceEquals(merged, successor.EntryStack[stackIndex]))
                                {
                                    successor.EntryStack[stackIndex] = merged;
                                    changed = true;
                                }
                            }

                            if (changed)
                            {
                                workQueue.Enqueue(successor);
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
            if (opcode == OpCodes.Ldnull) { result.Add(UnknownType); return result; }

            // Argument/Local/Field/Static
            if (opcode == OpCodes.Ldarg || opcode == OpCodes.Ldarg_0 || opcode == OpCodes.Ldarg_1 || opcode == OpCodes.Ldarg_2 || opcode == OpCodes.Ldarg_3 || opcode == OpCodes.Ldarg_S || opcode == OpCodes.Ldarga || opcode == OpCodes.Ldarga_S)
            {
                int argIndex = -1;
                try { argIndex = instr.ArgumentIndex(); }
                catch
                {
                    if (opcode == OpCodes.Ldarg_0) argIndex = 0;
                    else if (opcode == OpCodes.Ldarg_1) argIndex = 1;
                    else if (opcode == OpCodes.Ldarg_2) argIndex = 2;
                    else if (opcode == OpCodes.Ldarg_3) argIndex = 3;
                    else if (instr.operand is int idx) argIndex = idx;
                    else if (instr.operand is sbyte sb) argIndex = sb;
                    else if (instr.operand is byte b) argIndex = b;
                }

                if (argIndex != -1 && method != null)
                {
                    var parameters = method.GetParameters();
                    bool isStatic = method.IsStatic;
                    
                    if (!isStatic)
                    {
                        if (argIndex == 0)
                        {
                            result.Add(ResolveStackType(method.DeclaringType, method));
                            return result;
                        }
                        argIndex--; // Shift for 'this'
                    }

                    if (argIndex >= 0 && argIndex < parameters.Length)
                    {
                        var resolved = ResolveStackType(parameters[argIndex].ParameterType, method);
                        if (opcode == OpCodes.Ldarga || opcode == OpCodes.Ldarga_S)
                        {
                            try { resolved = resolved.MakeByRefType(); }
                            catch { resolved = UnknownType; }
                        }
                        result.Add(resolved);
                        return result;
                    }
                }
                result.Add(UnknownType);
                return result;
            }
            if (opcode == OpCodes.Ldloc || opcode == OpCodes.Ldloc_0 || opcode == OpCodes.Ldloc_1 || opcode == OpCodes.Ldloc_2 || opcode == OpCodes.Ldloc_3 || opcode == OpCodes.Ldloc_S || opcode == OpCodes.Ldloca || opcode == OpCodes.Ldloca_S)
            {
                if (instr.operand is LocalBuilder lb)
                {
                    var localResolved = ResolveStackType(lb.LocalType, method);
                    if (opcode == OpCodes.Ldloca || opcode == OpCodes.Ldloca_S)
                    {
                        try { localResolved = localResolved.MakeByRefType(); }
                        catch { localResolved = UnknownType; }
                    }
                    result.Add(localResolved);
                }
                else if (TryResolveLocalType(instr, method, out Type localType))
                {
                    if (opcode == OpCodes.Ldloca || opcode == OpCodes.Ldloca_S)
                    {
                        try { localType = localType.MakeByRefType(); }
                        catch { localType = UnknownType; }
                    }
                    result.Add(localType);
                }
                else result.Add(UnknownType);
                return result;
            }
            if (opcode == OpCodes.Ldfld || opcode == OpCodes.Ldsfld)
            {
                if (instr.operand is FieldInfo fi) result.Add(ResolveStackType(fi.FieldType, method));
                else result.Add(UnknownType);
                return result;
            }
            if (opcode == OpCodes.Ldflda || opcode == OpCodes.Ldsflda)
            {
                if (instr.operand is FieldInfo fi)
                {
                    try
                    {
                        result.Add(ResolveStackType(fi.FieldType, method).MakeByRefType());
                    }
                    catch
                    {
                        result.Add(UnknownType);
                    }
                }
                else result.Add(UnknownType);
                return result;
            }

            // Array and Special Ops
            if (opcode == OpCodes.Ldelem || opcode == OpCodes.Ldelem_Ref || opcode.Name.StartsWith("ldelem."))
            {
                if (popped != null && popped.Count > 0 && TryGetArrayElementType(popped[0], out Type elementType))
                    result.Add(elementType);
                else
                    result.Add(UnknownType);
                return result;
            }
            if (opcode == OpCodes.Ldtoken)
            {
                if (instr.operand is Type) result.Add(typeof(RuntimeTypeHandle));
                else if (instr.operand is MethodBase) result.Add(typeof(RuntimeMethodHandle));
                else if (instr.operand is FieldInfo) result.Add(typeof(RuntimeFieldHandle));
                else result.Add(UnknownType);
                return result;
            }
            if (opcode == OpCodes.Ldftn || opcode == OpCodes.Ldvirtftn)
            {
                result.Add(typeof(IntPtr));
                return result;
            }
            if (opcode == OpCodes.Castclass || opcode == OpCodes.Isinst)
            {
                if (instr.operand is Type castType) result.Add(ResolveStackType(castType, method));
                else result.Add(UnknownType);
                return result;
            }
            if (opcode == OpCodes.Unbox_Any)
            {
                if (instr.operand is Type unboxType) result.Add(ResolveStackType(unboxType, method));
                else result.Add(UnknownType);
                return result;
            }
            if (opcode == OpCodes.Box)
            {
                if (instr.operand is Type boxedType) result.Add(ResolveStackType(boxedType, method));
                else result.Add(UnknownType);
                return result;
            }
            if (opcode == OpCodes.Sizeof || opcode == OpCodes.Ldlen)
            {
                result.Add(typeof(int));
                return result;
            }

            // Calls/Newobj
            if (opcode == OpCodes.Call || opcode == OpCodes.Callvirt)
            {
                if (instr.operand is MethodInfo mi)
                {
                    if (mi.ReturnType != typeof(void)) result.Add(ResolveStackType(mi.ReturnType, method));
                }
                else if (instr.operand is MethodBase mb)
                {
                    // For non-MethodInfo MethodBase, we try our best. 
                    // Constructors (newobj) are handled separately below.
                    // But Call to a constructor is possible!
                    if (mb is MethodInfo mi2 && mi2.ReturnType != typeof(void)) result.Add(ResolveStackType(mi2.ReturnType, method));
                    else if (mb.IsConstructor) { /* Void return, push nothing */ }
                }
                else result.Add(UnknownType);
                return result;
            }
            if (opcode == OpCodes.Newobj)
            {
                if (instr.operand is ConstructorInfo ci) result.Add(ResolveStackType(ci.DeclaringType, method));
                else if (instr.operand is MethodBase mb && mb.IsConstructor) result.Add(ResolveStackType(mb.DeclaringType, method));
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

        private static Type MergeStackType(Type existing, Type incoming)
        {
            existing = existing ?? UnknownType;
            incoming = incoming ?? UnknownType;

            if (existing == incoming) return existing;
            if (existing == UnknownType || incoming == UnknownType) return UnknownType;
            if (existing.IsByRef || incoming.IsByRef) return existing == incoming ? existing : UnknownType;

            if (existing.IsAssignableFrom(incoming)) return existing;
            if (incoming.IsAssignableFrom(existing)) return incoming;

            return UnknownType;
        }

        private static string FormatStackType(Type t)
        {
            if (t == null || t == UnknownType) return "unknown";
            return t.Name;
        }

        private static bool TryResolveLocalType(CodeInstruction instr, MethodBase method, out Type localType)
        {
            localType = null;
            if (method == null) return false;

            MethodBody body;
            try { body = method.GetMethodBody(); }
            catch { return false; }

            if (body == null || body.LocalVariables == null || body.LocalVariables.Count == 0) return false;

            int localIndex = -1;
            try { localIndex = instr.LocalIndex(); }
            catch
            {
                if (instr.opcode == OpCodes.Ldloc_0) localIndex = 0;
                else if (instr.opcode == OpCodes.Ldloc_1) localIndex = 1;
                else if (instr.opcode == OpCodes.Ldloc_2) localIndex = 2;
                else if (instr.opcode == OpCodes.Ldloc_3) localIndex = 3;
                else if (instr.operand is int i) localIndex = i;
                else if (instr.operand is byte b) localIndex = b;
                else if (instr.operand is sbyte sb) localIndex = sb;
            }

            if (localIndex < 0 || localIndex >= body.LocalVariables.Count) return false;
            localType = ResolveStackType(body.LocalVariables[localIndex].LocalType, method);
            return true;
        }

        private static bool TryGetArrayElementType(Type arrayType, out Type elementType)
        {
            elementType = null;
            if (arrayType == null || arrayType == UnknownType) return false;

            try
            {
                if (arrayType.IsArray)
                {
                    elementType = arrayType.GetElementType() ?? UnknownType;
                    return true;
                }
                if (arrayType.IsByRef && arrayType.GetElementType() != null && arrayType.GetElementType().IsArray)
                {
                    elementType = arrayType.GetElementType().GetElementType() ?? UnknownType;
                    return true;
                }
            }
            catch { }

            return false;
        }

        private static Type ResolveStackType(Type type, MethodBase contextMethod)
        {
            if (type == null) return UnknownType;
            if (type == UnknownType) return UnknownType;

            if (type.IsGenericParameter)
            {
                return ResolveGenericParameterType(type, contextMethod);
            }

            if (type.HasElementType)
            {
                var elementType = ResolveStackType(type.GetElementType(), contextMethod);
                try
                {
                    if (type.IsByRef) return elementType.MakeByRefType();
                    if (type.IsPointer) return elementType.MakePointerType();
                    if (type.IsArray)
                    {
                        int rank = type.GetArrayRank();
                        return rank == 1 ? elementType.MakeArrayType() : elementType.MakeArrayType(rank);
                    }
                }
                catch
                {
                    return UnknownType;
                }
            }

            if (type.IsGenericType)
            {
                var genericArgs = type.GetGenericArguments();
                var resolvedArgs = new Type[genericArgs.Length];
                for (int i = 0; i < genericArgs.Length; i++)
                {
                    resolvedArgs[i] = ResolveStackType(genericArgs[i], contextMethod);
                }

                try
                {
                    return type.GetGenericTypeDefinition().MakeGenericType(resolvedArgs);
                }
                catch
                {
                    return type;
                }
            }

            return type;
        }

        private static Type ResolveGenericParameterType(Type genericParam, MethodBase contextMethod)
        {
            if (genericParam == null || !genericParam.IsGenericParameter) return genericParam ?? UnknownType;
            if (contextMethod == null) return UnknownType;

            try
            {
                if (genericParam.DeclaringMethod != null && contextMethod is MethodInfo methodInfo && methodInfo.IsGenericMethod)
                {
                    var methodArgs = methodInfo.GetGenericArguments();
                    int position = genericParam.GenericParameterPosition;
                    if (position >= 0 && position < methodArgs.Length) return methodArgs[position];
                }

                var declaringType = contextMethod.DeclaringType;
                if (declaringType != null && declaringType.IsGenericType)
                {
                    var typeArgs = declaringType.GetGenericArguments();
                    int position = genericParam.GenericParameterPosition;
                    if (position >= 0 && position < typeArgs.Length) return typeArgs[position];
                }

                var constraints = genericParam.GetGenericParameterConstraints();
                if (constraints != null && constraints.Length > 0)
                {
                    return ResolveStackType(constraints[0], contextMethod);
                }
            }
            catch { }

            return UnknownType;
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
