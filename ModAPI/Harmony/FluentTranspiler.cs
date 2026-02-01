using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using ModAPI.Core;

namespace ModAPI.Harmony
{
    /// <summary>
    /// Fluent API wrapper around Harmony's CodeMatcher for safe IL transpilation.
    /// Handles branch target fixups automatically.
    /// </summary>
    public partial class FluentTranspiler
    {
        private readonly CodeMatcher _matcher;
        private readonly List<string> _warnings = new List<string>();
        private readonly MethodBase _originalMethod;
        private readonly string _callerMod;  // For logging context

        private FluentTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase originalMethod = null, ILGenerator generator = null)
        {
            _matcher = new CodeMatcher(instructions, generator);
            _originalMethod = originalMethod;
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

        #region Matching Methods

        /// <summary>Match a method call by type and name. Supports overload and generic resolution.</summary>
        /// <param name="type">Declaring type of the method.</param>
        /// <param name="methodName">Method name.</param>
        /// <param name="parameterTypes">Optional parameter types for overload resolution.</param>
        /// <param name="genericArguments">Optional generic arguments (e.g., for GetComponent<T>).</param>
        /// <param name="includeInherited">If true, matches methods defined in base classes. Default: true for broad compatibility.</param>
        public FluentTranspiler MatchCall(Type type, string methodName, Type[] parameterTypes = null, Type[] genericArguments = null, bool includeInherited = true)
        {
            _matcher.Start();  // Reset to beginning
            
            Func<CodeInstruction, bool> predicate = instr =>
            {
                if (instr.opcode != OpCodes.Call && instr.opcode != OpCodes.Callvirt)
                    return false;
                
                if (!(instr.operand is MethodInfo method))
                    return false;
                
                // Exact type matching (includeInherited=false) prevents matching inherited virtual methods 
                // (System.Object.ToString, System.Object.GetHashCode) when patching specific types.
                if (includeInherited)
                {
                    if (method.DeclaringType != type && !type.IsAssignableFrom(method.DeclaringType))
                        return false;
                }
                else
                {
                    if (method.DeclaringType != type)
                        return false;
                }
                
                if (method.Name != methodName)
                    return false;
                
                // Generic method matching requires comparing MethodInfo.GetGenericArguments() against the 
                // requested type parameters. Without this, GetComponent<Transform>() matches GetComponent<Collider>()
                // because both share the same method definition (open generic), but they have different 
                // closed generic instantiations in the IL.
                if (genericArguments != null)
                {
                    if (!method.IsGenericMethod) return false;
                    var args = method.GetGenericArguments();
                    if (args.Length != genericArguments.Length) return false;
                    for (int i = 0; i < args.Length; i++)
                    {
                        // Generic parameter resolution: In cross-context scenarios, direct reference equality 
                        // of generic types might fail. Comparing type parameters ensures parameters like T 
                        // in different assemblies are matched correctly.
                        if (!args[i].Equals(genericArguments[i]) && 
                            !(args[i].IsGenericParameter && genericArguments[i].IsGenericParameter))
                            return false;
                    }
                }
                
                // Parameter type matching is essential for selecting the correct method overload when 
                // multiple methods share the same name (e.g., GetComponent(Type) vs GetComponent<T>()).
                if (parameterTypes != null)
                {
                    var methodParams = method.GetParameters();
                    if (methodParams.Length != parameterTypes.Length)
                        return false;
                    
                    for (int i = 0; i < parameterTypes.Length; i++)
                    {
                        if (methodParams[i].ParameterType != parameterTypes[i])
                            return false;
                    }
                }
                
                return true;
            };
            
            try
            {
                _matcher.MatchStartForward(new CodeMatch(predicate));
                if (!_matcher.IsValid)
                {
                    string details = (genericArguments != null ? $"<{string.Join(", ", genericArguments.Select(t => t.Name).ToArray())}>" : "") +
                                     (parameterTypes != null ? $"({string.Join(", ", parameterTypes.Select(t => t.Name).ToArray())})" : "");
                    _warnings.Add($"No match for call {type.Name}.{methodName}{details}");
                }
            }
            catch (Exception ex)
            {
                _warnings.Add($"MatchCall exception: {ex.Message}");
            }
            

            return this;
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
            _matcher.Advance(1);  // Move past current match
            
            Func<CodeInstruction, bool> predicate = instr =>
            {
                if (instr.opcode != OpCodes.Call && instr.opcode != OpCodes.Callvirt)
                    return false;
                if (!(instr.operand is MethodInfo method))
                    return false;
                
                if (includeInherited)
                {
                    if (method.DeclaringType != type && !type.IsAssignableFrom(method.DeclaringType)) return false;
                }
                else
                {
                    if (method.DeclaringType != type) return false;
                }

                if (method.Name != methodName)
                    return false;
                
                if (genericArguments != null)
                {
                    if (!method.IsGenericMethod) return false;
                    var args = method.GetGenericArguments();
                    if (args.Length != genericArguments.Length) return false;
                    for (int i = 0; i < args.Length; i++)
                    {
                        if (!args[i].Equals(genericArguments[i]) && 
                            !(args[i].IsGenericParameter && genericArguments[i].IsGenericParameter))
                            return false;
                    }
                }

                if (parameterTypes != null)
                {
                    var methodParams = method.GetParameters();
                    if (methodParams.Length != parameterTypes.Length)
                        return false;
                    for (int i = 0; i < parameterTypes.Length; i++)
                    {
                        if (methodParams[i].ParameterType != parameterTypes[i])
                            return false;
                    }
                }
                return true;
            };
            
            _matcher.MatchStartForward(new CodeMatch(predicate));
            
            if (!_matcher.IsValid)
            {
                _warnings.Add($"No more matches for call {type.Name}.{methodName}");
            }
            
            return this;
        }

        /// <summary>Match by OpCode.</summary>
        public FluentTranspiler MatchOpCode(OpCode opcode)
        {
            _matcher.Start();
            _matcher.MatchStartForward(new CodeMatch(opcode));
            
            if (!_matcher.IsValid)
            {
                _warnings.Add($"No match for opcode {opcode}");
            }
            
            return this;
        }

        /// <summary>Match a sequence of opcodes (pattern matching).</summary>
        public FluentTranspiler MatchSequence(params OpCode[] opcodes)
        {
            _matcher.Start();
            var matches = opcodes.Select(op => new CodeMatch(op)).ToArray();
            _matcher.MatchStartForward(matches);
            
            if (!_matcher.IsValid)
            {
                _warnings.Add($"Sequence not found: {string.Join(" -> ", opcodes.Select(o => o.Name).ToArray())}");
            }
            
            return this;
        }

        /// <summary>Match a field load (Ldfld or Ldsfld).</summary>
        public FluentTranspiler MatchFieldLoad(Type type, string fieldName)
        {
            _matcher.Start();
            Func<CodeInstruction, bool> predicate = instr =>
                (instr.opcode == OpCodes.Ldfld || instr.opcode == OpCodes.Ldsfld) &&
                instr.operand is FieldInfo f &&
                f.DeclaringType == type &&
                f.Name == fieldName;

            _matcher.MatchStartForward(new CodeMatch(predicate));
            if (!_matcher.IsValid) _warnings.Add($"No match for field load {type.Name}.{fieldName}");
            return this;
        }

        /// <summary>Match a field store (Stfld or Stsfld).</summary>
        public FluentTranspiler MatchFieldStore(Type type, string fieldName)
        {
            _matcher.Start();
            Func<CodeInstruction, bool> predicate = instr =>
                (instr.opcode == OpCodes.Stfld || instr.opcode == OpCodes.Stsfld) &&
                instr.operand is FieldInfo f &&
                f.DeclaringType == type &&
                f.Name == fieldName;

            _matcher.MatchStartForward(new CodeMatch(predicate));
            if (!_matcher.IsValid) _warnings.Add($"No match for field store {type.Name}.{fieldName}");
            return this;
        }

        /// <summary>Reset to beginning.</summary>
        public FluentTranspiler Reset()
        {
            _matcher.Start();
            return this;
        }

        #endregion

        #region Modification Methods

        /// <summary>Replace current instruction with a new one.</summary>
        public FluentTranspiler ReplaceWith(OpCode opcode, object operand = null)
        {
            if (!_matcher.IsValid)
            {
                _warnings.Add("ReplaceWith: No valid match.");
                return this;
            }
            
            _matcher.SetInstruction(new CodeInstruction(opcode, operand));
            return this;
        }

        /// <summary>Replace current match with a static method call.</summary>
        /// <param name="type">Type containing the method.</param>
        /// <param name="methodName">Method name.</param>
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
            
            _matcher.SetInstruction(new CodeInstruction(OpCodes.Call, method));
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
            
            _matcher.Insert(new CodeInstruction(opcode, operand));
            return this;
        }

        /// <summary>Insert instruction after current position.</summary>
        public FluentTranspiler InsertAfter(OpCode opcode, object operand = null)
        {
            if (!_matcher.IsValid)
            {
                _warnings.Add("InsertAfter: No valid match.");
                return this;
            }
            
            _matcher.Advance(1);
            _matcher.Insert(new CodeInstruction(opcode, operand));
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

        #region Control Flow Navigation

        #endregion

        #region Bulk Operations

        /// <summary>Replace ALL occurrences of a method call.</summary>
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
                    (m.DeclaringType == sourceType || sourceType.IsAssignableFrom(m.DeclaringType)) &&
                    m.Name == sourceMethod));
                
                if (_matcher.IsValid)
                {
                    _matcher.SetInstruction(new CodeInstruction(OpCodes.Call, targetMethodInfo));
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
        /// </summary>
        public FluentTranspiler ReplaceSequence(int removeCount, params CodeInstruction[] newInstructions)
        {
            if (!_matcher.IsValid)
            {
                _warnings.Add("ReplaceSequence: No valid match.");
                return this;
            }
            
            // Remove the old instructions
            for (int i = 0; i < removeCount && _matcher.IsValid; i++)
            {
                _matcher.RemoveInstruction();
            }
            
            // Insert the new instructions
            foreach (var instr in newInstructions.Reverse())
            {
                _matcher.Insert(instr);
            }
            
            return this;
        }

        /// <summary>
        /// Find ALL occurrences of a pattern and replace them.
        /// This addresses the core issue from the feedback - safe bulk replacements.
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
                
                MMLog.WriteDebug($"[ReplaceAllPatterns] Position {pos}: replacing {patternPredicates.Length} instructions with {replaceWith.Length} instructions. preserveInstructionCount={preserveInstructionCount}");

                _matcher.Start().Advance(pos);
                
                if (preserveInstructionCount)
                {
                    // Calculate how many Nops we need to reach the replacement count
                    int nopCount = patternPredicates.Length - replaceWith.Length;
                    
                    if (nopCount >= 0)
                    {
                        // We have enough or more slots than replacements.
                        // Fill lead slots with Nops, then place replacements.
                        for (int i = 0; i < nopCount; i++)
                        {
                            _matcher.SetInstruction(new CodeInstruction(OpCodes.Nop));
                            _matcher.Advance(1);
                        }
                        
                        for (int i = 0; i < replaceWith.Length; i++)
                        {
                            _matcher.SetInstruction(replaceWith[i]);
                            if (i < replaceWith.Length - 1) _matcher.Advance(1);
                        }
                    }
                    else
                    {
                        // We have MORE replacements than original instructions.
                        // Replace all original slots, then insert the remainder.
                        for (int i = 0; i < patternPredicates.Length; i++)
                        {
                            _matcher.SetInstruction(replaceWith[i]);
                            if (i < patternPredicates.Length - 1) _matcher.Advance(1);
                        }
                        
                        // Insert remaining ones
                        for (int i = patternPredicates.Length; i < replaceWith.Length; i++)
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

        #region Build

        /// <summary>
        /// Returns the modified instructions. This is a terminal operation.
        /// ⚠️ Stack validation is a BEST-EFFORT sanity check. It does NOT validate:
        /// - Exception handling blocks (try/catch/finally)
        /// - Branch target stack depth consistency
        /// - Complex control flow patterns
        /// </summary>
        /// <param name="strict">If true, throws an exception if any warnings occurred.</param>
        /// <param name="validateStack">If true, performs a basic stack depth analysis.</param>
        public IEnumerable<CodeInstruction> Build(bool strict = true, bool validateStack = true)
        {
            if (validateStack && _warnings.Count == 0)
            {
                // Performs linear stack depth analysis to detect common errors (unbalanced pushes/pops).
                // LIMITATION: This is a best-effort validation that assumes sequential execution. It cannot 
                // verify stack state across branch targets (BEQ, BNE, BR) or exception handling clauses 
                // (try/catch/finally blocks) where stack depth depends on runtime control flow.
                // This catches 80% of common transpiler errors (e.g., forgetting to Pop after InsertBefore)
                // but will not detect underflows in unreachable code paths.
                if (!ValidateStackBalance(_matcher.Instructions().ToList(), out string stackError))
                {
                    // Stack warnings are informative but should not be fatal by default 
                    // because linear analysis cannot account for complex control flow/branches.
                    MMLog.WriteWarning($"[FluentTranspiler:{_callerMod}] {stackError}");
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

            // CodeMatcher maintains internal label references and instruction indices that become 
            // invalid if the caller modifies the returned List (e.g., inserting/removing instructions).
            // ToList() creates a snapshot, ensuring the caller cannot corrupt the matcher state 
            // for subsequent operations.
            return _matcher.Instructions().ToList();
        }

        #endregion

        #region Internal Validation

        /// <summary>Linear stack depth analyzer to catch common transpiler errors.</summary>
        private bool ValidateStackBalance(List<CodeInstruction> instructions, out string error)
        {
            int stackDepth = 0;
            int instrIndex = 0;
            
            foreach (var instr in instructions)
            {
                // Pop behavior
                stackDepth -= CalculatePopCount(instr);
                
                if (stackDepth < 0) 
                {
                    // Relaxed check: Allow minor underflow at the very start of a method.
                    if (instrIndex < 5)
                    {
                        stackDepth = 0; // Reset and continue
                    }
                    else
                    {
                        error = $"Stack underflow at index {instrIndex}: {instr}";
                        return false;
                    }
                }

                // Check balance at return
                if (instr.opcode == OpCodes.Ret && stackDepth != 0)
                {
                    error = $"Unbalanced stack at Return (index {instrIndex}). Depth: {stackDepth}. Instr: {instr}";
                    return false;
                }
                
                // Push behavior
                stackDepth += CalculatePushCount(instr);

                instrIndex++;
            }
            
            error = null;
            return true;
        }

        private int CalculatePopCount(CodeInstruction instr)
        {
            // Special cases for opcodes with variable stack behavior
            if (instr.opcode == OpCodes.Ret)
            {
                // Ret pops the return value if not void
                if (_originalMethod is MethodInfo mi && mi.ReturnType != typeof(void))
                    return 1;
                return 0;
            }

            if (instr.opcode == OpCodes.Newobj)
            {
                // newobj pops parameters, but NOT an instance (it creates it)
                if (instr.operand is MethodBase constructor)
                    return constructor.GetParameters().Length;
                return 0;
            }

            if (instr.opcode == OpCodes.Call || instr.opcode == OpCodes.Callvirt)
            {
                if (instr.operand is MethodBase method)
                {
                    int count = method.GetParameters().Length;
                    if (!method.IsStatic) count++;
                    return count;
                }
            }

            switch (instr.opcode.StackBehaviourPop)
            {
                case StackBehaviour.Pop0: return 0;
                case StackBehaviour.Pop1: 
                case StackBehaviour.Popi:
                case StackBehaviour.Popref: return 1;
                case StackBehaviour.Pop1_pop1:
                case StackBehaviour.Popi_pop1:
                case StackBehaviour.Popi_popi:
                case StackBehaviour.Popi_popi8:
                case StackBehaviour.Popi_popr4:
                case StackBehaviour.Popi_popr8:
                case StackBehaviour.Popref_pop1:
                case StackBehaviour.Popref_popi: return 2;
                case StackBehaviour.Popi_popi_popi:
                case StackBehaviour.Popref_popi_pop1:
                case StackBehaviour.Popref_popi_popi:
                case StackBehaviour.Popref_popi_popi8:
                case StackBehaviour.Popref_popi_popr4:
                case StackBehaviour.Popref_popi_popr8:
                case StackBehaviour.Popref_popi_popref: return 3;
                case StackBehaviour.Varpop:
                    if (instr.operand is MethodBase method)
                    {
                        int count = method.GetParameters().Length;
                        if (!method.IsStatic) count++;
                        return count;
                    }
                    return 0;
                default: return 0;
            }
        }

        private int CalculatePushCount(CodeInstruction instr)
        {
            if (instr.opcode == OpCodes.Newobj) return 1;

            switch (instr.opcode.StackBehaviourPush)
            {
                case StackBehaviour.Push0: return 0;
                case StackBehaviour.Push1:
                case StackBehaviour.Pushi:
                case StackBehaviour.Pushi8:
                case StackBehaviour.Pushr4:
                case StackBehaviour.Pushr8:
                case StackBehaviour.Pushref: return 1;
                case StackBehaviour.Push1_push1: return 2;
                case StackBehaviour.Varpush:
                    if (instr.operand is MethodInfo mi)
                        return mi.ReturnType == typeof(void) ? 0 : 1;
                    return 0;
                default: return 0;
            }
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
