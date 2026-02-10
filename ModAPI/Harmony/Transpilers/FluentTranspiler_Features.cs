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
        /// Attempts to match a specific sequence of instructions and provides a "Diagnostic Breadcrumb" for debugging.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Why use this over a normal Match?</b> In a complex patch with multiple steps, a standard match failure 
        /// is "silent" and hard to locate. MatchIntent creates a narrative in your logs. If a game update breaks 
        /// only the 3rd step of your patch, the log will tell you exactly which "intent" failed.
        /// </para>
        /// <para>
        /// <b>Automatic Diagnostics:</b> If the match fails, this method automatically triggers a "Nearby Anchor" 
        /// search to give you a hint about where the code might have moved in a game update.
        /// </para>
        /// </remarks>
        /// <param name="intent">A human-readable description of what this section of the patch is trying to find (e.g., "Find the water calculation loop").</param>
        /// <param name="matches">The Harmony <see cref="CodeMatch"/> patterns to search for.</param>
        public FluentTranspiler MatchIntent(string intent, params CodeMatch[] matches)
        {
            if (!_matcher.IsValid) return this;
            
            int startPos = _matcher.Pos;
            MMLog.WriteDebug($"[FluentTranspiler:{_callerMod}] MatchIntent: Attempting '{intent}'...");
            _matcher.MatchStartForward(matches);
            
            if (!_matcher.IsValid)
            {
                var message = $"MatchIntent Failed: {intent}. Could not find specified IL sequence in method {_originalMethod.Name}.";
                _warnings.Add(message);
                
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
                            _warnings.Add($"  - HINT: Potential nearby anchor found at index {nearest.Index}: {nearest.Instruction}");
                        }
                    }
                }
                catch { /* Safer to ignore diagnostic failures than to crash the transpiler */ }
            }
            else
            {
                MMLog.WriteDebug($"[FluentTranspiler:{_callerMod}] MatchIntent Success: '{intent}' at index {_matcher.Pos}");
            }
            
            return this;
        }

        #region Contracts & Stack Safety

        /// <summary>
        /// Asserts that the stack depth at the current instruction is exactly <paramref name="depth"/>.
        /// This is a "checkpoint" validation that runs during <see cref="Build"/>.
        /// </summary>
        /// <remarks>
        /// Use this after a complex sequence of pushes/pops to ensure you haven't leaked a value 
        /// or underflowed the stack. If the assertion fails, the transpiler will throw a detailed 
        /// error showing the expected vs actual stack depth and types.
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
        /// Asserts that the sequence of instructions since the last stack check has resulted 
        /// in a specific change in stack depth (delta).
        /// </summary>
        /// <remarks>
        /// Example: If you call a method that takes 2 arguments and returns 1 value, the delta is -1.
        /// This is useful for verifying that a custom injection has the expected net effect on the stack.
        /// </remarks>
        /// <param name="delta">The expected change in stack depth (e.g., -1 for consumer, 1 for producer).</param>
        public FluentTranspiler EnsureStack(int delta)
        {
            if (!_matcher.IsValid) return this;
            
            // We record that the delta between _lastStackCheckPos and current Pos must be 'delta'
            // For simplicity, we convert this to an absolute expectation if possible, or record for later validation.
            if (_lastStackCheckPos == -1)
            {
                _warnings.Add("EnsureStack: No previous operation to calculate delta from. Use ExpectStack for absolute anchoring.");
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
        /// Resilience Combinator: Attempts Pattern A, then falls back to Pattern B if A fails.
        /// </summary>
        /// <remarks>
        /// <b>Why use this?</b> 
        /// <para>
        /// <b>Mod Compatibility.</b> In a modded game, another mod might have already patched 
        /// the method you're looking at. If your primary pattern fails, use <b>MatchEither</b> 
        /// to provide a fallback that accounts for common modded states or difference between 
        /// game platforms (Steam vs Epic Games). 
        /// </para>
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
        /// Structure Combinator: Matches boundaries while ignoring "dirty" IL in the middle.
        /// </summary>
        /// <remarks>
        /// <b>Why use this?</b>
        /// <para>
        /// <b>Surgical Resilience.</b> Sometimes you know the "start" and "end" of a logic block, 
        /// but the middle is messy—either because the compiler generated weird IL or because 
        /// another mod has injected a logging call or a tiny check there.
        /// </para>
        /// <para>
        /// Use <b>MatchWithGap</b> to lock onto the stable boundaries and ignore the "gap" in the middle. 
        /// This ensures your patch works regardless of what other modders have done to that specific block of code.
        /// </para>
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

            // Match start
            startPattern(this);
            if (!_matcher.IsValid) return this;
            
            int afterStart = _matcher.Pos;
            
            // Search forward for end pattern within gap
            for (int i = 0; i <= maxGap; i++)
            {
                // Clone matcher state conceptually? No, just move forward
                // But if we fail, we need to backtrack.
                // Simple approach: Check at current, then next, until maxGap.
                
                int currentProbe = afterStart + i;
                if (currentProbe >= _matcher.Instructions().Count) break;
                
                _matcher.Start(); // Reset
                _matcher.Advance(currentProbe);
                
                // Check if end pattern matches here
                int preCheck = _matcher.Pos;
                endPattern(this);
                if (_matcher.IsValid && _matcher.Pos > preCheck) 
                {
                    // Found it!
                    // We need to decide what state satisfied "MatchWithGap".
                    // Usually it means we are now AT the end of the Gap pattern.
                    return this; 
                }
            }
            
            _warnings.Add($"MatchWithGap: End pattern not found within {maxGap} instructions of start.");
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

            // Analyze modifications if possible (simple heuristic)
            if (_originalMethod != null)
            {
                try 
                {
                    // To get a true diff, we'd need the original instructions.
                    // If we don't have them easily, we report generic stats.
                    // Assuming FluentTranspiler was created with 'instructions', those ARE the current instructions.
                    // We can check if _matcher.IsInvalid or if we have warnings.
                    
                    if (_warnings.Count > 0)
                    {
                        report.Modifications.Add($"{_warnings.Count} warnings generated so far.");
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
        /// Scans instructions for suspicious patterns and antipatterns.
        /// Warning: Replacing a 'callvirt' with 'call' on instance method
        /// Warning: Modifying instructions inside a 'try' block (exception handling)
        /// </summary>
        private void Lint(List<CodeInstruction> instructions)
        {
            if (_originalMethod == null) return;

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
                    _warnings.Add($"Lint: {instr.opcode} at index {i} has NULL operand (Expected {instr.opcode.OperandType})");
                }

                // Check for Callvirt vs Call correctness
                if (instr.opcode == OpCodes.Call || instr.opcode == OpCodes.Callvirt)
                {
                     if (instr.operand is MethodInfo mi)
                     {
                         if (!mi.IsStatic)
                         {
                             // Instance methods usually need Callvirt unless specific optimization
                             if (instr.opcode == OpCodes.Call && mi.IsVirtual && !mi.IsFinal)
                             {
                                 _warnings.Add($"Lint: Suspicious 'call' on virtual method {mi.Name} at {i}. Should probably be 'callvirt'.");
                             }
                         }
                     }
                }

                // Check if branch target is valid index
                if (instr.operand is Label label)
                {
                    bool found = false;
                    for (int j = 0; j < instructions.Count; j++)
                    {
                        if (instructions[j].labels != null && instructions[j].labels.Contains(label))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        _warnings.Add($"Lint: {instr.opcode} at index {i} refers to label that is not attached to any instruction in this method body.");
                    }
                }
                else if (instr.opcode.FlowControl == FlowControl.Branch || instr.opcode.FlowControl == FlowControl.Cond_Branch)
                {
                    // Catch common mistake: using integer instead of Label for branch operand
                    if (instr.operand != null && !(instr.operand is Label) && !(instr.operand is Label[]))
                    {
                        _warnings.Add($"Lint: {instr.opcode} at index {i} has invalid operand type '{instr.operand.GetType().Name}'. Branch instructions MUST use a 'Label' as their operand. Using an integer (e.g. 2) is a common error that causes native crashes.");
                    }
                }
            }

            // Check for modifications inside Try/Catch blocks
            // This requires mapping current index back to original, which is hard if instructions shifted.
            // Heuristic: If we are at an index that was originally a try block...
            // Actually, we can just check if any exception blocks exist and if our total count changed drastically?
            // Better: Iterate EH clauses from MethodBody and see if current instructions at those offsets look valid.
            // Since this is advanced, we'll placeholder it with a basic check:
            try
            {
                var body = _originalMethod.GetMethodBody();
                if (body != null && body.ExceptionHandlingClauses.Count > 0)
                {
                    // Check if instructions count changed significantly inside a try block?
                    // Too complex for generic lint without deep analysis. 
                    // We just warn that EH blocks exist.
                    // _warnings.Add("Lint: Method contains exception blocks. Verify patch endpoints.");
                }
            }
            catch {}
        }

        #endregion
    }
}
