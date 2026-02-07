using System;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using ModAPI.Core;

namespace ModAPI.Harmony
{
    /// <summary>
    /// Advanced and rarely used transpiler tools. 
    /// Kept separate to reduce clutter for standard mod development.
    /// </summary>
    public partial class FluentTranspiler
    {
        #region Branch Matching (Advanced)

        /// <summary>
        /// Match a forward branch instruction (br, brtrue, brfalse, etc.).
        /// Useful for finding loop boundaries and conditional jumps.
        /// </summary>
        public FluentTranspiler FindNextBranch(SearchMode mode = SearchMode.Next)
        {
            if (mode == SearchMode.Start) _matcher.Start();
            else if (mode == SearchMode.Next) _matcher.Advance(1);

            _matcher.MatchStartForward(new CodeMatch(instr => 
                instr.opcode.FlowControl == FlowControl.Branch || 
                instr.opcode.FlowControl == FlowControl.Cond_Branch));
            
            if (!_matcher.IsValid)
                _warnings.Add("No branch instruction found");
                
            return this;
        }

        /// <summary>Match a forward branch instruction (alias for FindNextBranch).</summary>
        public FluentTranspiler MatchNextBranch()
        {
            return FindNextBranch(SearchMode.Next);
        }

        /// <summary>
        /// Match the instruction with a specific label (branch target).
        /// Advances to the target label of a branch instruction.
        /// </summary>
        public FluentTranspiler FindBranchTarget(Label targetLabel, SearchMode mode = SearchMode.Start)
        {
            if (mode == SearchMode.Start) _matcher.Start();
            else if (mode == SearchMode.Next) _matcher.Advance(1);

            _matcher.MatchStartForward(new CodeMatch(instr => instr.labels.Contains(targetLabel)));
            
            if (!_matcher.IsValid)
            {
                _warnings.Add($"Branch target label not found in method");
            }
            
            return this;
        }

        /// <summary>Match the instruction with a specific label (branch target).</summary>
        public FluentTranspiler MatchBranchTarget(Label targetLabel)
        {
            return FindBranchTarget(targetLabel, SearchMode.Start);
        }

        #endregion
    }

    /// <summary>
    /// Rarely used pattern matching extensions.
    /// </summary>
    public static class AdvancedPatterns
    {
        #region DontDestroyOnLoad Nuking
        
        /// <summary>
        /// Remove a call to DontDestroyOnLoad.
        /// Includes matching - do NOT pre-match.
        /// </summary>
        public static FluentTranspiler NukeDontDestroyOnLoad(this FluentTranspiler t, SearchMode mode = SearchMode.Start)
        {
            return t
                .FindCall(typeof(UnityEngine.Object), "DontDestroyOnLoad", mode)
                .ReplaceWith(OpCodes.Pop);
        }

        /// <summary>
        /// Remove ALL calls to DontDestroyOnLoad in the method.
        /// </summary>
        public static FluentTranspiler NukeAllDontDestroyOnLoad(this FluentTranspiler t)
        {
            return t.ReplaceAllPatterns(
                new Func<CodeInstruction, bool>[] { instr => 
                    (instr.opcode == OpCodes.Call || instr.opcode == OpCodes.Callvirt) &&
                    instr.operand is MethodBase mb && mb.DeclaringType == typeof(UnityEngine.Object) && mb.Name == "DontDestroyOnLoad" },
                new[] { new CodeInstruction(OpCodes.Pop) },
                preserveInstructionCount: true);
        }
        
        #endregion
    }
}
