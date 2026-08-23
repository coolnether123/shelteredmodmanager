using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using ModAPI.Core;

namespace ModAPI.Harmony
{
    public partial class FluentTranspiler
    {
        /// <summary>
        /// Matches an instruction sequence and records diagnostics when the sequence is absent.
        /// </summary>
        /// <remarks>
        /// Unlike a direct matcher call, this method names the failed step and reports the nearest
        /// remaining anchor. Use it when a patch has several independent matches.
        /// </remarks>
        /// <param name="intent">Description of the target sequence, such as "water calculation loop".</param>
        /// <param name="matches">The Harmony <see cref="CodeMatch"/> patterns to search for.</param>
        public FluentTranspiler MatchIntent(string intent, params CodeMatch[] matches)
        {
            if (!_matcher.IsValid) return this;
            
            int startPos = _matcher.Pos;
            LogTrace($"[FluentTranspiler:{_callerMod}] MatchIntent: Attempting '{intent}'...");
            _matcher.MatchStartForward(matches);
            
            if (!_matcher.IsValid)
            {
                var message = $"MatchIntent Failed: {intent}. Could not find specified IL sequence in method {_originalMethod.Name}.";
                AddSoftFailure(message);
                
                // Heuristic: Try to find partially matching anchors to guide the developer.
                try 
                {
                    var report = this.MapAnchors();
                    if (report.SafeAnchors.Count > 0)
                    {
                        var nearest = report.SafeAnchors
                            .OrderBy(a => Math.Abs(a.Index - startPos))
                            .FirstOrDefault();
                            
                        if (nearest != null)
                        {
                            AddNote($"Potential nearby anchor found at index {nearest.Index}: {nearest.Instruction}");
                        }
                    }
                }
                catch { /* Diagnostics must not abort the transpiler. */ }
            }
            else
            {
                LogTrace($"[FluentTranspiler:{_callerMod}] MatchIntent Success: '{intent}' at index {_matcher.Pos}");
            }
            
            return this;
        }

        #region Contracts & Stack Safety

        /// <summary>
        /// Requires the stack depth at the current instruction to equal <paramref name="depth"/>.
        /// <c>Build</c> evaluates this checkpoint.
        /// </summary>
        /// <remarks>
        /// Use this after a sequence of pushes and pops. A failed check reports the expected and
        /// actual stack depths and types.
        /// </remarks>
        /// <param name="depth">The expected number of elements on the stack.</param>
        public FluentTranspiler ExpectStack(int depth)
        {
            if (_matcher.IsValid)
            {
                _stackExpectations.Add(new StackExpectation { index = _matcher.Pos, expectedDepth = depth });
            }
            return this;
        }

        private int _lastStackCheckPos = -1;

        /// <summary>
        /// Requires the instructions since the previous stack check to change the stack depth by
        /// <paramref name="delta"/>.
        /// </summary>
        /// <remarks>
        /// A call that takes two arguments and returns one value has a delta of -1.
        /// </remarks>
        /// <param name="delta">The expected stack-depth change. A consumer uses -1 and a producer uses 1.</param>
        public FluentTranspiler EnsureStack(int delta)
        {
            if (!_matcher.IsValid) return this;
            
            // Record the expected delta for Build to validate against one shared stack analysis.
            if (_lastStackCheckPos == -1)
            {
                AddNote("EnsureStack: No previous operation to calculate delta from. Use ExpectStack for absolute anchoring.");
                return this;
            }

            _stackDeltaExpectations.Add(new StackDeltaExpectation 
            { 
                startIndex = _lastStackCheckPos, 
                endIndex = _matcher.Pos, 
                expectedDelta = delta 
            });
            
            _lastStackCheckPos = _matcher.Pos;
            return this;
        }

        private struct StackDeltaExpectation
        {
            public int startIndex;
            public int endIndex;
            public int expectedDelta;
        }

        private List<StackDeltaExpectation> _stackDeltaExpectations = new List<StackDeltaExpectation>();

        #endregion

        #region Pattern Combinators

        /// <summary>
        /// Tries <paramref name="patternA"/>, then tries <paramref name="patternB"/> if the first pattern fails.
        /// </summary>
        /// <remarks>
        /// Use the second pattern for a known alternate IL shape, such as another storefront build
        /// or a method that another mod has already patched.
        /// </remarks>
        public FluentTranspiler MatchEither(
            Func<FluentTranspiler, FluentTranspiler> patternA,
            Func<FluentTranspiler, FluentTranspiler> patternB)
        {
            int startPos = _matcher.Pos;
            
            // Try A
            try 
            {
                patternA(this);
                if (_matcher.IsValid && _matcher.Pos > startPos) return this; // A matched and advanced
            }
            catch { /* Ignore failure */ }
            
            // Rewind
            _matcher.Start();
            _matcher.Advance(startPos);
            
            // Try B
            patternB(this);
            
            return this;
        }

        /// <summary>
        /// Matches two boundary patterns separated by at most <paramref name="maxGap"/> instructions.
        /// </summary>
        /// <remarks>
        /// Use this when the boundary instructions are stable but compiler output or another patch
        /// may change the instructions between them.
        /// </remarks>
        /// <param name="startPattern">Logic to find the entry point.</param>
        /// <param name="endPattern">Logic to find the exit point.</param>
        /// <param name="maxGap">The maximum number of instructions to search through before giving up.</param>
        public FluentTranspiler MatchWithGap(
            Func<FluentTranspiler, FluentTranspiler> startPattern,
            Func<FluentTranspiler, FluentTranspiler> endPattern,
            int maxGap = 10)
        {
            if (!_matcher.IsValid) return this;
            int entryPos = _matcher.Pos;

            // Match start
            startPattern(this);
            if (!_matcher.IsValid)
            {
                _matcher.Start().Advance(entryPos);
                return this;
            }
            
            int afterStart = _matcher.Pos;
            
            // Search forward for end pattern within gap
            for (int i = 0; i <= maxGap; i++)
            {
                // Probe each position in the allowed gap and reset the matcher before every attempt.
                
                int currentProbe = afterStart + i;
                if (currentProbe >= _matcher.Instructions().Count) break;
                
                _matcher.Start(); // Reset
                _matcher.Advance(currentProbe);
                
                // Check if end pattern matches here
                int preCheck = _matcher.Pos;
                endPattern(this);
                if (_matcher.IsValid && _matcher.Pos > preCheck) 
                {
                    // Leave the matcher at the end pattern selected by the callback.
                    return this; 
                }
            }
            
            AddSoftFailure($"MatchWithGap: End pattern not found within {maxGap} instructions of start.");
            _matcher.Start().Advance(entryPos);
            return this;
        }

        #endregion

        #region Ghost Mode

        /// <summary>
        /// Runs the transpiler logic in a "dry run" mode without modifying the original method.
        /// Returns a report of potential changes.
        /// </summary>
        public TranspilerReport DryRun()
        {
            var report = new TranspilerReport();
            
            var currentInstrs = _matcher.Instructions();
            report.InstructionCount = currentInstrs.Count;

            // Summarize diagnostics and structural state without mutating the instruction stream.
            if (_originalMethod != null)
            {
                try 
                {
                    // DryRun has only the current stream, so report counts instead of a before-and-after diff.
                    
                    if (Warnings.Count > 0 || SoftFailures.Count > 0 || Notes.Count > 0)
                    {
                        report.Modifications.Add(
                            $"Warnings={Warnings.Count}, SoftFailures={SoftFailures.Count}, Notes={Notes.Count}");
                    }
                    
                    // Count potential issues like unmatched labels
                    int labelCount = currentInstrs.Sum(i => i.labels.Count);
                    report.Modifications.Add($"Total Labels: {labelCount}");
                    report.Modifications.Add($"Current Length: {currentInstrs.Count}");
                    
                    // Stack check simulation
                    string err;
                    var stack = StackSentinel.Analyze(currentInstrs.ToList(), _originalMethod, out err);
                    if (stack != null)
                        report.StackDelta(stack.Count > 0 && stack.ContainsKey(currentInstrs.Count-1) ? stack[currentInstrs.Count-1].Count : 0);
                    else 
                        report.Modifications.Add($"Stack Analysis Failed: {err}");
                }
                catch (Exception ex)
                {
                    report.Modifications.Add($"DryRun Analysis Failed: {ex.Message}");
                }
            }
            
            return report;
        }

        public class TranspilerReport
        {
            public int InstructionCount { get; set; }
            public List<string> Modifications { get; set; } = new List<string>();
            public Dictionary<int, int> LabelShifts { get; set; } = new Dictionary<int, int>();
            
            public void WillModify(int count) => Modifications.Add($"Modifies {count} instructions");
            public void WillShiftLabels(Dictionary<int, int> shifts) => LabelShifts = shifts;
            public void StackDelta(int delta) => Modifications.Add($"Stack Delta: {delta}");
        }

        #endregion

        #region Debugging Helpers

        /// <summary>
        /// Dumps a diff against a previous state using the cached Mod ID.
        /// Eliminates expensive stack walking in TranspilerDebugger.
        /// </summary>
        public FluentTranspiler DumpDiffFrom(IEnumerable<CodeInstruction> originalInstructions, string label = "Patch")
        {
            TranspilerDebugger.DumpWithDiff(label, originalInstructions, Instructions(), modId: _callerMod, originalMethod: _originalMethod);
            return this;
        }

        #endregion

        #region Linting

        /// <summary>
        /// Checks operands, branch labels, local indices, argument indices, casts, and exception handlers.
        /// Callers can suppress a check for a known game method shape.
        /// </summary>
        private void Lint(List<CodeInstruction> instructions)
        {
            if (_originalMethod == null) return;
            var labelAnchors = BuildLabelAnchorMap(instructions);
            var targetedLabels = new HashSet<Label>();
            MethodBody body = null;
            try { body = _originalMethod.GetMethodBody(); } catch { }
            int localCount = body != null && body.LocalVariables != null ? body.LocalVariables.Count : -1;
            int argumentCount = _originalMethod.GetParameters().Length + (_originalMethod.IsStatic ? 0 : 1);

            // Check for Callvirt vs Call correctness
            for (int i = 0; i < instructions.Count; i++)
            {
                var instr = instructions[i];

                // Check for null operands in opcodes that require them
                if (instr.operand == null && 
                    (instr.opcode.OperandType == OperandType.InlineMethod || 
                     instr.opcode.OperandType == OperandType.InlineField || 
                     instr.opcode.OperandType == OperandType.InlineType))
                {
                    AddWarning($"[CRITICAL LINT] {instr.opcode} at index {i} has a null operand. Expected {instr.opcode.OperandType}.");
                }

                // Check for Callvirt vs Call correctness
                if (instr.opcode == OpCodes.Call || instr.opcode == OpCodes.Callvirt)
                {
                     if (instr.operand is MethodInfo mi)
                     {
                         if (!mi.IsStatic)
                         {
                             // Instance methods usually need Callvirt unless specific optimization
                             if (instr.opcode == OpCodes.Call
                                 && mi.IsVirtual
                                 && !mi.IsFinal
                                 && TranspilerSafetyPolicy.WarnOnVirtualCallMismatch)
                             {
                                 AddNote($"Lint: Suspicious 'call' on virtual method {mi.Name} at {i}. Should probably be 'callvirt'.");
                             }
                         }
                     }
                }

                // Check if branch target is valid index
                if (instr.operand is Label label)
                {
                    bool found = labelAnchors.ContainsKey(label);
                    targetedLabels.Add(label);
                    if (!found)
                    {
                        AddWarning($"[CRITICAL LINT] {instr.opcode} at index {i} refers to label that is not attached to any instruction in this method body.");
                    }
                }
                else if (instr.operand is Label[] labels)
                {
                    for (int l = 0; l < labels.Length; l++)
                    {
                        targetedLabels.Add(labels[l]);
                        if (!labelAnchors.ContainsKey(labels[l]))
                        {
                            AddWarning($"[CRITICAL LINT] {instr.opcode} at index {i} contains switch/branch label not attached to any instruction.");
                            break;
                        }
                    }
                }
                else if (instr.opcode.FlowControl == FlowControl.Branch || instr.opcode.FlowControl == FlowControl.Cond_Branch)
                {
                    // Catch common mistake: using integer instead of Label for branch operand
                    if (instr.operand != null && !(instr.operand is Label) && !(instr.operand is Label[]))
                    {
                        AddWarning($"[CRITICAL LINT] {instr.opcode} at index {i} has invalid operand type '{instr.operand.GetType().Name}'. Branch instructions require a Label operand, not an integer.");
                    }
                }

                int localIndex;
                if (TryGetLocalIndex(instr, out localIndex) && localCount >= 0 && (localIndex < 0 || localIndex >= localCount))
                {
                    AddWarning($"[CRITICAL LINT] {instr.opcode} at index {i} targets local {localIndex}, but method declares {localCount} locals.");
                }

                int argumentIndex;
                if (TryGetArgumentIndex(instr, out argumentIndex) && (argumentIndex < 0 || argumentIndex >= argumentCount))
                {
                    AddWarning($"[CRITICAL LINT] {instr.opcode} at index {i} targets argument {argumentIndex}, but method argument range is 0..{argumentCount - 1}.");
                }

                if (instr.opcode == OpCodes.Castclass && !(instr.operand is Type))
                {
                    AddWarning($"[CRITICAL LINT] castclass at index {i} has invalid operand type '{(instr.operand != null ? instr.operand.GetType().Name : "null")}'.");
                }
            }

            foreach (var kv in labelAnchors)
            {
                // Entry label at index 0 is often valid without explicit branch target.
                if (kv.Value == 0) continue;
                if (!targetedLabels.Contains(kv.Key))
                {
                    AddNote($"Lint: label at instruction index {kv.Value} is never targeted by any branch instruction.");
                }
            }

            // Warn when the original method has exception handlers. Mapping shifted instructions
            // back to exception regions requires metadata that this linter does not retain.
            try
            {
                var methodBody = _originalMethod.GetMethodBody();
                if (methodBody != null
                    && methodBody.ExceptionHandlingClauses.Count > 0
                    && TranspilerSafetyPolicy.WarnOnExceptionHandlerMethods)
                {
                    AddNote("Lint: Method contains exception handlers; use exact index-aligned replacements and avoid structural IL edits.");
                }
            }
            catch {}
        }

        private static Dictionary<Label, int> BuildLabelAnchorMap(List<CodeInstruction> instructions)
        {
            var map = new Dictionary<Label, int>();
            if (instructions == null) return map;

            for (int i = 0; i < instructions.Count; i++)
            {
                var instr = instructions[i];
                if (instr == null || instr.labels == null) continue;
                for (int j = 0; j < instr.labels.Count; j++)
                {
                    var label = instr.labels[j];
                    if (!map.ContainsKey(label)) map[label] = i;
                }
            }
            return map;
        }

        private static bool TryGetLocalIndex(CodeInstruction instr, out int localIndex)
        {
            localIndex = -1;
            if (instr == null) return false;

            try
            {
                // Harmony helper handles ldloc/stloc/ldloca short and long forms.
                localIndex = instr.LocalIndex();
                return true;
            }
            catch { }

            return false;
        }

        private static bool TryGetArgumentIndex(CodeInstruction instr, out int argumentIndex)
        {
            argumentIndex = -1;
            if (instr == null) return false;

            try
            {
                // Harmony helper handles ldarg/starg/ldarga short and long forms.
                argumentIndex = instr.ArgumentIndex();
                return true;
            }
            catch { }

            if (instr.opcode == OpCodes.Ldarg_0) { argumentIndex = 0; return true; }
            if (instr.opcode == OpCodes.Ldarg_1) { argumentIndex = 1; return true; }
            if (instr.opcode == OpCodes.Ldarg_2) { argumentIndex = 2; return true; }
            if (instr.opcode == OpCodes.Ldarg_3) { argumentIndex = 3; return true; }

            return false;
        }

        #endregion
    }
}
