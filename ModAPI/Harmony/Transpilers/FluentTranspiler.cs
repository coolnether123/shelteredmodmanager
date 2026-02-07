using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using ModAPI.Core;

namespace ModAPI.Harmony
{
    public enum SearchMode
    {
        Start,   // Resets to beginning before searching
        Current, // Searches forward from current position (inclusive)
        Next     // Advances 1 then searches forward (for sequential matching)
    }

    /// <summary>
    /// Fluent API wrapper around Harmony's CodeMatcher for safe IL transpilation.
    /// Handles branch target fixups automatically.
    /// </summary>
    public partial class FluentTranspiler
    {
        private readonly CodeMatcher _matcher;
        private readonly List<string> _warnings = new List<string>();
        private readonly MethodBase _originalMethod;
        private readonly ILGenerator _generator;
        private readonly string _callerMod;  // For logging context

        private FluentTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase originalMethod = null, ILGenerator generator = null)
        {
            _matcher = new CodeMatcher(instructions, generator);
            _originalMethod = originalMethod;
            _generator = generator;
            _callerMod = Assembly.GetCallingAssembly().GetName().Name;
        }

        /// <summary>Create a new FluentTranspiler from instructions.</summary>
        /// <param name="instructions">Original instructions from Harmony transpiler.</param>
        /// <param name="originalMethod">Optional method being patched (for better stack validation).</param>
        /// <param name="generator">Optional ILGenerator for label creation (pass from transpiler args).</param>
        public static FluentTranspiler For(IEnumerable<CodeInstruction> instructions, MethodBase originalMethod = null, ILGenerator generator = null)
        {
            return new FluentTranspiler(instructions, originalMethod, generator);
        }
        /// <summary>
        /// Unified method to find a method call.
        /// Replaces MatchCall and MatchCallNext logic.
        /// </summary>
        public FluentTranspiler FindCall(Type type, string methodName, SearchMode mode = SearchMode.Start, Type[] parameterTypes = null, Type[] genericArguments = null, bool includeInherited = true)
        {
            if (mode == SearchMode.Start) _matcher.Start();
            else if (mode == SearchMode.Next) _matcher.Advance(1);
            
            return MatchCallForward(type, methodName, parameterTypes, genericArguments, includeInherited);
        }

        /// <summary>Match a method call by type and name. Supports overload and generic resolution.</summary>
        public FluentTranspiler MatchCall(Type type, string methodName, Type[] parameterTypes = null, Type[] genericArguments = null, bool includeInherited = true)
        {
            return FindCall(type, methodName, SearchMode.Start, parameterTypes, genericArguments, includeInherited);
        }

        /// <summary>
        /// Match a property getter call. 
        /// Automatically handles both Call and Callvirt opcodes.
        /// </summary>
        /// <param name="type">Declaring type of the property.</param>
        /// <param name="propertyName">Name of the property (without "get_" prefix).</param>
        public FluentTranspiler MatchPropertyGetter(Type type, string propertyName)
        {
            return MatchCall(type, "get_" + propertyName, includeInherited: true);
        }

        /// <summary>Continue matching from current position (for sequential matches).</summary>
        public FluentTranspiler MatchCallNext(Type type, string methodName, Type[] parameterTypes = null, Type[] genericArguments = null, bool includeInherited = true)
        {
            return FindCall(type, methodName, SearchMode.Next, parameterTypes, genericArguments, includeInherited);
        }

        private FluentTranspiler MatchCallForward(Type type, string methodName, Type[] parameterTypes, Type[] genericArguments, bool includeInherited)
        {
            var predicate = BuildCallPredicate(type, methodName, parameterTypes, genericArguments, includeInherited);
            _matcher.MatchStartForward(new CodeMatch(predicate));

            if (!_matcher.IsValid)
            {
                string details = (genericArguments != null ? $"<{string.Join(", ", genericArguments.Select(t => t.Name).ToArray())}>" : "") +
                                 (parameterTypes != null ? $"({string.Join(", ", parameterTypes.Select(t => t.Name).ToArray())})" : "");
                _warnings.Add($"No match for call {type.Name}.{methodName}{details}");
            }
            return this;
        }

        private Func<CodeInstruction, bool> BuildCallPredicate(Type type, string methodName, Type[] parameterTypes = null, Type[] genericArguments = null, bool includeInherited = true)
        {
            return instr =>
            {
                if (instr.opcode != OpCodes.Call && instr.opcode != OpCodes.Callvirt)
                    return false;
                if (!(instr.operand is MethodInfo method))
                    return false;

                bool typeMatch = includeInherited
                    ? method.DeclaringType == type
                      || type.IsAssignableFrom(method.DeclaringType)
                      || method.DeclaringType.FullName == type.FullName
                    : method.DeclaringType == type
                      || method.DeclaringType.FullName == type.FullName;

                if (!typeMatch || method.Name != methodName) return false;

                if (genericArguments != null)
                {
                    if (!method.IsGenericMethod) return false;
                    var args = method.GetGenericArguments();
                    if (args.Length != genericArguments.Length) return false;
                    for (int i = 0; i < args.Length; i++)
                    {
                        if (!args[i].Equals(genericArguments[i]) &&
                            args[i].Name != genericArguments[i].Name &&
                            !(args[i].IsGenericParameter && genericArguments[i].IsGenericParameter))
                            return false;
                    }
                }

                if (parameterTypes != null)
                {
                    var ps = method.GetParameters();
                    if (ps.Length != parameterTypes.Length) return false;
                    for (int i = 0; i < parameterTypes.Length; i++)
                    {
                        if (ps[i].ParameterType != parameterTypes[i] &&
                            ps[i].ParameterType.FullName != parameterTypes[i].FullName)
                            return false;
                    }
                }
                return true;
            };
        }

        /// <summary>Unified method to find an OpCode.</summary>
        public FluentTranspiler FindOpCode(OpCode opcode, SearchMode mode = SearchMode.Start)
        {
            if (mode == SearchMode.Start) _matcher.Start();
            else if (mode == SearchMode.Next) _matcher.Advance(1);

            _matcher.MatchStartForward(new CodeMatch(opcode));
            
            if (!_matcher.IsValid)
            {
                _warnings.Add($"No match for opcode {opcode}");
            }
            
            return this;
        }

        /// <summary>Match by OpCode.</summary>
        public FluentTranspiler MatchOpCode(OpCode opcode)
        {
            return FindOpCode(opcode, SearchMode.Start);
        }

        /// <summary>Match the return instruction (OpCodes.Ret).</summary>
        public FluentTranspiler MatchReturn()
        {
            return MatchOpCode(OpCodes.Ret);
        }

        /// <summary>Match a sequence of opcodes (pattern matching).</summary>
        /// <summary>Unified method to find a sequence of opcodes.</summary>
        public FluentTranspiler FindSequence(SearchMode mode, params OpCode[] opcodes)
        {
            if (mode == SearchMode.Start) _matcher.Start();
            else if (mode == SearchMode.Next) _matcher.Advance(1);

            var matches = opcodes.Select(op => new CodeMatch(op)).ToArray();
            _matcher.MatchStartForward(matches);
            
            if (!_matcher.IsValid)
            {
                _warnings.Add($"Sequence not found: {string.Join(" -> ", opcodes.Select(o => o.Name).ToArray())}");
            }
            
            return this;
        }

        /// <summary>Match a sequence of opcodes (pattern matching).</summary>
        public FluentTranspiler MatchSequence(params OpCode[] opcodes)
        {
            return FindSequence(SearchMode.Start, opcodes);
        }

        /// <summary>Match a field load (Ldfld or Ldsfld).</summary>
        /// <summary>Unified method to find a field load.</summary>
        public FluentTranspiler FindFieldLoad(Type type, string fieldName, SearchMode mode = SearchMode.Start)
        {
            if (mode == SearchMode.Start) _matcher.Start();
            else if (mode == SearchMode.Next) _matcher.Advance(1);

            Func<CodeInstruction, bool> predicate = instr =>
                (instr.opcode == OpCodes.Ldfld || instr.opcode == OpCodes.Ldsfld) &&
                instr.operand is FieldInfo f &&
                f.DeclaringType == type &&
                f.Name == fieldName;

            _matcher.MatchStartForward(new CodeMatch(predicate));
            if (!_matcher.IsValid) _warnings.Add($"No match for field load {type.Name}.{fieldName}");
            return this;
        }

        /// <summary>Match a field load (Ldfld or Ldsfld).</summary>
        public FluentTranspiler MatchFieldLoad(Type type, string fieldName)
        {
            return FindFieldLoad(type, fieldName, SearchMode.Start);
        }

        /// <summary>Match a field store (Stfld or Stsfld).</summary>
        /// <summary>Unified method to find a field store.</summary>
        public FluentTranspiler FindFieldStore(Type type, string fieldName, SearchMode mode = SearchMode.Start)
        {
            if (mode == SearchMode.Start) _matcher.Start();
            else if (mode == SearchMode.Next) _matcher.Advance(1);

            Func<CodeInstruction, bool> predicate = instr =>
                (instr.opcode == OpCodes.Stfld || instr.opcode == OpCodes.Stsfld) &&
                instr.operand is FieldInfo f &&
                f.DeclaringType == type &&
                f.Name == fieldName;

            _matcher.MatchStartForward(new CodeMatch(predicate));
            if (!_matcher.IsValid) _warnings.Add($"No match for field store {type.Name}.{fieldName}");
            return this;
        }

        /// <summary>Match a field store (Stfld or Stsfld).</summary>
        public FluentTranspiler MatchFieldStore(Type type, string fieldName)
        {
            return FindFieldStore(type, fieldName, SearchMode.Start);
        }

        /// <summary>Unified method to find a string load.</summary>
        public FluentTranspiler FindString(string value, SearchMode mode = SearchMode.Start)
        {
             if (mode == SearchMode.Start) _matcher.Start();
            else if (mode == SearchMode.Next) _matcher.Advance(1);

            _matcher.MatchStartForward(new CodeMatch(OpCodes.Ldstr, value));
            if (!_matcher.IsValid)
                _warnings.Add($"No match for string \"{value}\"");
            return this;
        }

        /// <summary>Match a string load instruction.</summary>
        public FluentTranspiler MatchString(string value)
        {
            return FindString(value, SearchMode.Start);
        }

        /// <summary>Match a constant integer load.</summary>
        /// <summary>Unified method to find an int constant.</summary>
        public FluentTranspiler FindConstInt(int value, SearchMode mode = SearchMode.Start)
        {
            if (mode == SearchMode.Start) _matcher.Start();
            else if (mode == SearchMode.Next) _matcher.Advance(1);

            _matcher.MatchStartForward(new CodeMatch(instr =>
                instr.IsLdcI4(value)));
            if (!_matcher.IsValid)
                _warnings.Add($"No match for int constant {value}");
            return this;
        }

        /// <summary>Match a constant integer load.</summary>
        public FluentTranspiler MatchConstInt(int value)
        {
            return FindConstInt(value, SearchMode.Start);
        }

        /// <summary>Match a constant float load.</summary>
        /// <summary>Unified method to find a float constant.</summary>
        public FluentTranspiler FindConstFloat(float value, SearchMode mode = SearchMode.Start)
        {
            if (mode == SearchMode.Start) _matcher.Start();
            else if (mode == SearchMode.Next) _matcher.Advance(1);

            _matcher.MatchStartForward(new CodeMatch(instr =>
                instr.IsLdcR4(value)));
            if (!_matcher.IsValid)
                _warnings.Add($"No match for float constant {value}");
            return this;
        }

        /// <summary>Match a constant float load.</summary>
        public FluentTranspiler MatchConstFloat(float value)
        {
            return FindConstFloat(value, SearchMode.Start);
        }


        /// <summary>Reset to beginning.</summary>
        public FluentTranspiler Reset()
        {
            _matcher.Start();
            return this;
        }

        #region Modification Methods

        /// <summary>
        /// Replace current instruction with a new OpCode and operand.
        /// Automatically preserves any labels attached to the original instruction.
        /// </summary>
        public FluentTranspiler ReplaceWith(OpCode opcode, object operand = null)
        {
            if (!_matcher.IsValid)
            {
                _warnings.Add("ReplaceWith: No valid match.");
                return this;
            }

            SetInstructionSafe(new CodeInstruction(opcode, operand));
            return this;
        }

        /// <summary>
        /// Replace current match with a call to a static replacement method.
        /// Automatically handles label preservation and validates that the target is static.
        /// </summary>
        /// <param name="type">The class containing your static hook.</param>
        /// <param name="methodName">The name of the static method.</param>
        /// <param name="parameterTypes">Optional parameter types for overload resolution.</param>
        public FluentTranspiler ReplaceWithCall(Type type, string methodName, Type[] parameterTypes = null)
        {
            if (!_matcher.IsValid)
            {
                _warnings.Add("ReplaceWithCall: No valid match.");
                return this;
            }
            
            MethodInfo method;
            if (parameterTypes != null)
            {
                method = type.GetMethod(methodName, 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                    null, parameterTypes, null);
            }
            else
            {
                method = type.GetMethod(methodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            }
            
            if (method == null)
            {
                _warnings.Add($"Method {type.Name}.{methodName} not found");
                return this;
            }
            
            // Transpiler replacements replace an instance or static call with a static call. 
            // Replacing an instance call with a non-static method would lead to an invalid 
            // stack state (missing 'this' pointer).
            if (!method.IsStatic)
            {
                _warnings.Add($"Method {type.Name}.{methodName} must be static for transpiler replacement");
                return this;
            }
            
            SetInstructionSafe(new CodeInstruction(OpCodes.Call, method));
            return this;
        }

        /// <summary>Insert instruction before current position (handles branch fixups automatically).</summary>
        public FluentTranspiler InsertBefore(OpCode opcode, object operand = null)
        {
            if (!_matcher.IsValid)
            {
                _warnings.Add("InsertBefore: No valid match.");
                return this;
            }

            var newInstr = new CodeInstruction(opcode, operand);

            // Transfer labels so branches land on the new instruction
            var existingLabels = _matcher.Instruction.labels;
            if (existingLabels.Count > 0)
            {
                newInstr.labels.AddRange(existingLabels);
                existingLabels.Clear();
            }

            _matcher.Insert(newInstr);
            return this;
        }

        /// <summary>Insert multiple instructions before current position.</summary>
        public FluentTranspiler InsertBefore(params CodeInstruction[] instructions)
        {
            if (!_matcher.IsValid)
            {
                _warnings.Add("InsertBefore: No valid match.");
                return this;
            }

            // Transfer labels to first new instruction
            var existingLabels = _matcher.Instruction.labels;
            if (existingLabels.Count > 0 && instructions.Length > 0)
            {
                instructions[0].labels.AddRange(existingLabels);
                existingLabels.Clear();
            }

            for (int i = instructions.Length - 1; i >= 0; i--)
            {
                _matcher.Insert(instructions[i]);
            }
            return this;
        }

        /// <summary>
        /// Insert after current. Matcher stays on the ORIGINAL instruction.
        /// </summary>
        public FluentTranspiler InsertAfter(OpCode opcode, object operand = null)
        {
            if (!_matcher.IsValid)
            {
                _warnings.Add("InsertAfter: No valid match.");
                return this;
            }

            _matcher.Advance(1);
            _matcher.Insert(new CodeInstruction(opcode, operand));
            _matcher.Advance(-1); // Return to original position
            return this;
        }

        /// <summary>Insert multiple instructions after current position. Matcher stays on the ORIGINAL instruction.</summary>
        public FluentTranspiler InsertAfter(params CodeInstruction[] instructions)
        {
            if (!_matcher.IsValid)
            {
                _warnings.Add("InsertAfter: No valid match.");
                return this;
            }

            _matcher.Advance(1);
            foreach (var instr in instructions)
            {
                _matcher.InsertAndAdvance(instr);
            }
            _matcher.Advance(-instructions.Length - 1); // Restore to original position
            return this;
        }

        /// <summary>Remove current instruction.</summary>
        public FluentTranspiler Remove()
        {
            if (!_matcher.IsValid)
            {
                _warnings.Add("Remove: No valid match.");
                return this;
            }
            
            _matcher.RemoveInstruction();
            return this;
        }

        #endregion

        #region ILGenerator Wrappers

        /// <summary>
        /// Declare a new local variable for use in the transpiler.
        /// Throws if ILGenerator was not provided during construction.
        /// </summary>
        public FluentTranspiler DeclareLocal<T>(out LocalBuilder local)
        {
            if (_generator == null)
            {
                throw new InvalidOperationException(
                    "ILGenerator was not provided. " +
                    "Pass it to For(instructions, originalMethod, generator). " +
                    "Transpiler delegate signature: " +
                    "IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instr, ILGenerator gen)");
            }

            local = _generator.DeclareLocal(typeof(T));
            _warnings.Add($"DeclareLocal<{typeof(T).FullName}>() -> LocalIndex {local.LocalIndex}");
            return this;
        }

        /// <summary>
        /// Define a new label for branching.
        /// Throws if ILGenerator was not provided during construction.
        /// </summary>
        public FluentTranspiler DefineLabel(out Label label)
        {
            if (_generator == null)
            {
                throw new InvalidOperationException(
                    "ILGenerator was not provided. " +
                    "Pass it to For(instructions, originalMethod, generator).");
            }

            label = _generator.DefineLabel();
            return this;
        }

        #endregion

        #region Variable Capture

        /// <summary>
        /// Automatically emits an Ldloc instruction for a local variable by index or name.
        /// </summary>
        /// <param name="localIndexOrName">The index (e.g. "0") or name (if symbols available) of the local variable.</param>
        public FluentTranspiler CaptureLocal(string localIndexOrName)
        {
            if (!_matcher.IsValid) return this;

            // 1. Try to parse as index
            if (int.TryParse(localIndexOrName, out int index))
            {
                _matcher.Insert(new CodeInstruction(GetLdlocOpCode(index), index > 3 ? (object)index : null));
                return this;
            }

            // 2. Try to find by name via reflection (requires debug symbols on the original method)
            if (_originalMethod != null)
            {
                try
                {
                    // Note: Standard LocalVariableInfo doesn't have names. 
                    // This is a placeholder for environments where names might be injected or available via metadata.
                    var locals = _originalMethod.GetMethodBody()?.LocalVariables;
                    if (locals != null)
                    {
                        foreach (var local in locals)
                        {
                            // In some contexts (like DynamicMethod or specific debug builds), 
                            // we might be able to resolve names. For now, we log a warning if we can't find it.
                        }
                    }
                }
                catch { }
            }

            _warnings.Add($"CaptureLocal: Could not resolve variable '{localIndexOrName}' by name. Use numeric index instead.");
            return this;
        }

        private OpCode GetLdlocOpCode(int index)
        {
            switch (index)
            {
                case 0: return OpCodes.Ldloc_0;
                case 1: return OpCodes.Ldloc_1;
                case 2: return OpCodes.Ldloc_2;
                case 3: return OpCodes.Ldloc_3;
                default: return OpCodes.Ldloc_S;
            }
        }

        #endregion

        #region Control Flow Navigation

        /// <summary>Move forward or backward by N instructions.</summary>
        public FluentTranspiler Advance(int count)
        {
            _matcher.Advance(count);
            return this;
        }

        #endregion

        #region Bulk Operations

        /// <summary>
        /// Replace ALL occurrences of a specific method call throughout the entire instruction stream.
        /// Handles labels correctly and uses resilient type matching (by Name/FullName).
        /// </summary>
        /// <param name="sourceType">The class containing the method to replace.</param>
        /// <param name="sourceMethod">The name of the method to replace.</param>
        /// <param name="targetType">Your class containing the replacement static method.</param>
        /// <param name="targetMethod">The name of your static replacement method.</param>
        /// <param name="targetParams">Optional parameter types for target overload resolution.</param>
        public FluentTranspiler ReplaceAllCalls(Type sourceType, string sourceMethod, 
            Type targetType, string targetMethod, Type[] targetParams = null)
        {
            _matcher.Start();
            int replacements = 0;
            
            var targetMethodInfo = targetParams != null
                ? targetType.GetMethod(targetMethod, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, targetParams, null)
                : targetType.GetMethod(targetMethod, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            
            if (targetMethodInfo == null)
            {
                _warnings.Add($"Target method {targetType.Name}.{targetMethod} not found");
                return this;
            }
            
            if (!targetMethodInfo.IsStatic)
            {
                _warnings.Add($"Target method {targetType.Name}.{targetMethod} must be static");
                return this;
            }
            
            while (_matcher.IsValid)
            {
                _matcher.MatchStartForward(new CodeMatch(instr =>
                    (instr.opcode == OpCodes.Call || instr.opcode == OpCodes.Callvirt) &&
                    instr.operand is MethodInfo m &&
                    (m.DeclaringType == sourceType || sourceType.IsAssignableFrom(m.DeclaringType) || m.DeclaringType.FullName == sourceType.FullName) &&
                    m.Name == sourceMethod));
                
                if (_matcher.IsValid)
                {
                    SetInstructionSafe(new CodeInstruction(OpCodes.Call, targetMethodInfo));
                    _matcher.Advance(1);
                    replacements++;
                }
            }
            
            if (replacements == 0)
            {
                _warnings.Add($"No instances of {sourceType.Name}.{sourceMethod} found");
            }
            
            return this;
        }

        #endregion

        #region Navigation & Debugging

        /// <summary>Check if current position is valid.</summary>
        public bool HasMatch => _matcher.IsValid;

        /// <summary>Get current instruction (or null).</summary>
        public CodeInstruction Current => _matcher.IsValid ? _matcher.Instruction : null;

        /// <summary>Get current index.</summary>
        public int CurrentIndex => _matcher.Pos;

        /// <summary>Move to next instruction.</summary>
        public FluentTranspiler Next()
        {
            _matcher.Advance(1);
            return this;
        }

        /// <summary>Move to previous instruction.</summary>
        public FluentTranspiler Previous()
        {
            _matcher.Advance(-1);
            return this;
        }

        /// <summary>Get all warnings that occurred.</summary>
        public IList<string> Warnings { get { return _warnings.AsReadOnly(); } }

        /// <summary>Add a warning to the transpiler state.</summary>
        public void AddWarning(string message) => _warnings.Add(message);

        /// <summary>Throw an exception immediately if the current operation has no match.</summary>
        public FluentTranspiler AssertValid()
        {
            if (!_matcher.IsValid)
            {
                string lastWarning = _warnings.LastOrDefault() ?? "Unknown error";
                throw new InvalidOperationException($"[{_callerMod}] AssertValid failed: {lastWarning}");
            }
            return this;
        }

        /// <summary>Log current state to console.</summary>
        public FluentTranspiler Log(string label = "")
        {
            MMLog.WriteDebug($"[FluentTranspiler:{_callerMod}] {label}");
            MMLog.WriteDebug($"  Position: {_matcher.Pos}, Valid: {_matcher.IsValid}");
            if (_matcher.IsValid)
            {
                MMLog.WriteDebug($"  Current: {_matcher.Instruction}");
            }
            if (_warnings.Count > 0)
            {
                MMLog.WriteDebug($"  Warnings: {_warnings.Count}");
            }
            return this;
        }

        /// <summary>Dump all instructions to log (expensive, debugging only).</summary>
        public FluentTranspiler DumpAll(string label = "")
        {
            var instructions = _matcher.Instructions();
            MMLog.WriteDebug($"[FluentTranspiler:{_callerMod}] {label} ({instructions.Count} instructions):");
            for (int i = 0; i < instructions.Count; i++)
            {
                string marker = (i == _matcher.Pos) ? " >>>" : "    ";
                MMLog.WriteDebug($"{marker}{i:D3}: {instructions[i]}");
            }
            return this;
        }

        /// <summary>Get copy of current instructions.</summary>
        public IEnumerable<CodeInstruction> Instructions()
        {
            return _matcher.Instructions();
        }

        #endregion

        #region Pattern Matching & Safer Operations

        /// <summary>
        /// Move backwards in the instruction stream. 
        /// Safer than Previous() for checking context before removals.
        /// </summary>
        /// <param name="absolutePosition">Absolute index to move to.</param>
        public FluentTranspiler MoveTo(int absolutePosition)
        {
            var instructions = _matcher.Instructions().ToList();
            if (absolutePosition < 0 || absolutePosition >= instructions.Count)
            {
                _warnings.Add($"MoveTo: Position {absolutePosition} out of range.");
                return this;
            }
            
            _matcher.Start().Advance(absolutePosition);
            return this;
        }

        /// <summary>
        /// Replace a sequence of instructions with new instructions.
        /// Preserves labels from the first instruction of the removed sequence and attaches them to the first new instruction.
        /// This ensures that branches jumping to the start of the block still land on the replacement logic.
        /// </summary>
        /// <param name="removeCount">How many original instructions to remove starting from current position.</param>
        /// <param name="newInstructions">The new instructions to insert.</param>
        public FluentTranspiler ReplaceSequence(int removeCount, params CodeInstruction[] newInstructions)
        {
            if (!_matcher.IsValid)
            {
                _warnings.Add("ReplaceSequence: No valid match.");
                return this;
            }

            // LABEL PRESERVATION STRATEGY:
            // When replacing a block of code, any jump targets (labels) pointing to the first instruction 
            // in that block must be preserved. If we just deleted them, Harmony would move the label 
            // to the instruction AFTER our replacement block, potentially causing a stack imbalance 
            // or logic error. We capture them here and anchor them to our FIRST new instruction.
            var capturedLabels = new List<Label>();
            if (_matcher.Instruction.labels != null)
                capturedLabels.AddRange(_matcher.Instruction.labels);
            
            // Remove the old instructions. Harmony automatically fixes up labels for instructions 
            // that are NOT the first one in the sequence (shifting them to the next instruction).
            for (int i = 0; i < removeCount && _matcher.IsValid; i++)
            {
                _matcher.RemoveInstruction();
            }
            
            // Re-insertion logic: 
            // Insert in order using InsertAndAdvance
            for (int i = 0; i < newInstructions.Length; i++)
            {
                var instr = new CodeInstruction(newInstructions[i]);
                if (i == 0 && capturedLabels.Count > 0)
                {
                    instr.labels.AddRange(capturedLabels);
                }
                _matcher.InsertAndAdvance(instr);
            }
            
            return this;
        }

        /// <summary>
        /// Completely replaces the entire method body with new instructions.
        /// Direct manipulation of the instruction list avoids CodeMatcher invalidity issues on empty methods.
        /// Preserves labels on the method entry point to ensure external jumps remain valid.
        /// </summary>
        public FluentTranspiler ReplaceAll(IEnumerable<CodeInstruction> newInstructions)
        {
            var newCode = newInstructions.ToList();
            var oldList = _matcher.Instructions();

            if (oldList.Count > 0 && newCode.Count > 0 
                && oldList[0].labels?.Count > 0)
            {
                newCode[0].labels.AddRange(oldList[0].labels);
            }

            // WARNING: Directly mutates CodeMatcher's internal list.
            // This is a known coupling to Harmony's implementation.
            // If CodeMatcher changes to use defensive copies, this
            // will silently fail.
            oldList.Clear();
            oldList.AddRange(newCode);

            // Safety check: Verify the replacement took
            if (_matcher.Instructions().Count != newCode.Count)
            {
                _warnings.Add("ReplaceAll: Internal list mismatch detected. HarmonyLib implementation may have changed.");
            }

            return Reset();
        }

        /// <summary>
        /// Find ALL occurrences of a pattern and replace them.
        /// This addresses the core issue from the feedback - safe bulk replacements.
        /// <para>
        /// <b>WARNING:</b> When <paramref name="preserveInstructionCount"/> is false (default), removing instructions 
        /// that are targets of branches will preserve the label on the *first* replacement instruction. However, 
        /// if the replacement sequence is shorter/longer than the original, subsequent instruction indices 
        /// will shift. This handles Harmony's automatic label fixups, but be wary of implicit index dependencies.
        /// </para>
        /// </summary>
        /// <param name="patternPredicates">Pattern to match.</param>
        /// <param name="replaceWith">Replacement instructions.</param>
        /// <param name="preserveInstructionCount">If true, pads with Nops to preserve instruction count (safe for labels).</param>
        public FluentTranspiler ReplaceAllPatterns(
            Func<CodeInstruction, bool>[] patternPredicates,
            CodeInstruction[] replaceWith,
            bool preserveInstructionCount = false)
        {
            var instructions = _matcher.Instructions().ToList();
            int replacementCount = 0;
            
            // Find all match positions first (snapshot approach)
            var matchPositions = new List<int>();
            for (int i = 0; i <= instructions.Count - patternPredicates.Length; i++)
            {
                bool matches = true;
                for (int j = 0; j < patternPredicates.Length; j++)
                {
                    if (!patternPredicates[j](instructions[i + j]))
                    {
                        matches = false;
                        break;
                    }
                }
                
                if (matches)
                {
                    matchPositions.Add(i);
                    // Skip ahead to avoid overlapping matches
                    i += patternPredicates.Length - 1;
                }
            }
            
            // Apply replacements in reverse order to maintain indices
            for (int idx = matchPositions.Count - 1; idx >= 0; idx--)
            {
                int pos = matchPositions[idx];
                
                string methodContext = _originalMethod != null ? $"{_originalMethod.DeclaringType?.Name}.{_originalMethod.Name}" : "UnknownMethod";
                MMLog.WriteDebug($"[ReplaceAllPatterns] [{methodContext}] Position {pos}: replacing {patternPredicates.Length} instructions with {replaceWith.Length} instructions");

                _matcher.Start().Advance(pos);
                
                // CRITICAL FIX: Branch Target Safety Check
                // If the pattern we are replacing contains jump targets (labels) in the middle (indices 1+),
                // we can't simply NOP them out because the jump would land on a NOP with an incorrect stack state.
                // In these cases, we force preserveInstructionCount = false. This causes ReplaceSequence to run,
                // which (by default alignment) effectively moves those labels to the end of the replacement or next instruction.
                // While still risky for logic, it avoids the guaranteed stack crash of landing on a NOP.
                // FIX: Enforce padding if replacement is shorter (Option A)
                bool effectivePreserve = preserveInstructionCount || replaceWith.Length < patternPredicates.Length;

                if (effectivePreserve)
                {
                    // Safe replacement logic:
                    // 1. Fill leading slots with actual replacement instructions (preserving labels at each index)
                    // 2. Fill remaining slots with Nops (preserving labels at each index)
                    
                    int originalCount = patternPredicates.Length;
                    int replacementCountInSequence = Math.Min(originalCount, replaceWith.Length);
                    
                    // Step 1: Replace leading instructions with our logic, preserving labels at each index.
                    // This is "In-Place" replacement which is very safe for control flow.
                    for (int i = 0; i < replacementCountInSequence; i++)
                    {
                        var newI = new CodeInstruction(replaceWith[i]);
                        SetInstructionSafe(newI);
                        _matcher.Advance(1);
                    }
                    
                    // Step 2: If we have more original instructions than replacements, Nop them out
                    // while still preserving any labels that might be attached to those middle instructions.
                    for (int i = replacementCountInSequence; i < originalCount; i++)
                    {
                        var newI = new CodeInstruction(OpCodes.Nop);
                        SetInstructionSafe(newI);
                        _matcher.Advance(1);
                    }
                    
                    // Step 3: If our replacement logic is LONGER than the original pattern,
                    // we insert the overflow instructions after the Nop-padded block.
                    if (replaceWith.Length > originalCount)
                    {
                        for (int i = originalCount; i < replaceWith.Length; i++)
                        {
                            _matcher.Insert(replaceWith[i]);
                        }
                    }
                }
                else
                {
                    // Normal mode: remove and replace
                    ReplaceSequence(patternPredicates.Length, replaceWith);
                }
                
                replacementCount++;
            }
            
            if (replacementCount == 0)
            {
                _warnings.Add("ReplaceAllPatterns: No patterns found");
            }
            
            return this;
        }

        #endregion

        #region Navigation Helpers (Public for Extensions)

        /// <summary>Resolves a label to its current instruction index.</summary>
        public int LabelToIndex(Label label)
        {
            var instructions = _matcher.Instructions();
            for (int i = 0; i < instructions.Count; i++)
            {
                if (instructions[i].labels.Contains(label)) return i;
            }
            return -1;
        }

        #endregion

        #region Build

        /// <summary>
        /// Returns the modified instructions. This is a terminal operation.
        /// ⚠️ Stack validation is a BEST-EFFORT sanity check. It now tracks TYPES.
        /// </summary>
        /// <param name="strict">If true, throws an exception if any warnings occurred.</param>
        /// <param name="validateStack">If true, performs a basic stack depth and type analysis.</param>
        public IEnumerable<CodeInstruction> Build(bool strict = true, bool validateStack = true)
        {
            if (validateStack && _warnings.Count == 0)
            {
                var instructions = _matcher.Instructions().ToList();
                if (!StackSentinel.Validate(instructions, _originalMethod, out string stackError))
                {
                    string methodContext = _originalMethod != null ? $"{_originalMethod.DeclaringType?.Name}.{_originalMethod.Name}" : "UnknownMethod";
                    MMLog.WriteWarning($"[FluentTranspiler:{_callerMod}] [{methodContext}] {stackError}");
                }
            }

            if (strict && _warnings.Count > 0)
            {
                var message = $"[{_callerMod}] Transpiler validation failed ({_warnings.Count} warnings):\n" +
                    string.Join("\n", _warnings.Select(w => $"  - {w}").ToArray());
                    
                MMLog.WriteError(message);
                throw new InvalidOperationException(message);
            }

            if (_warnings.Count > 0)
            {
                foreach (var w in _warnings)
                    MMLog.WriteWarning($"[FluentTranspiler:{_callerMod}] {w}");
            }

            return _matcher.Instructions().ToList();
        }

        #endregion

        #region Internal Validation

        /// <summary>
        /// Replaces the current instruction while preserving any labels attached to the original.
        /// This ensures branch targets remain valid after replacement.
        /// </summary>
        private void SetInstructionSafe(CodeInstruction newInstr)
        {
            var oldInstr = _matcher.Instruction;
            if (oldInstr.labels != null && oldInstr.labels.Count > 0)
            {
                newInstr.labels.AddRange(oldInstr.labels);
            }
            _matcher.SetInstruction(newInstr);
        }

        #endregion
    }
    
    #region Extension Methods
    
    /// <summary>Extension methods for fluent usage.</summary>
    public static class FluentTranspilerExtensions
    {
        /// <summary>Pipe extension for chaining with debugger.</summary>
        public static T Pipe<T>(this T input, Func<T, T> func) => func(input);
        
        /// <summary>Pipe with side effect (doesn't transform).</summary>
        public static T Tap<T>(this T input, Action<T> action)
        {
            action(input);
            return input;
        }
    }
    
    #endregion
}
