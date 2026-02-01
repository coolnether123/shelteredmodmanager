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
        public FluentTranspiler MatchNextBranch()
        {
            _matcher.MatchStartForward(new CodeMatch(instr => 
                instr.opcode == OpCodes.Br || 
                instr.opcode == OpCodes.Br_S ||
                instr.opcode == OpCodes.Brtrue ||
                instr.opcode == OpCodes.Brtrue_S ||
                instr.opcode == OpCodes.Brfalse ||
                instr.opcode == OpCodes.Brfalse_S ||
                instr.opcode == OpCodes.Beq ||
                instr.opcode == OpCodes.Beq_S ||
                instr.opcode == OpCodes.Bne_Un ||
                instr.opcode == OpCodes.Bne_Un_S));
            
            if (!_matcher.IsValid)
                _warnings.Add("No branch instruction found");
                
            return this;
        }

        /// <summary>
        /// Match the instruction with a specific label (branch target).
        /// Advances to the target label of a branch instruction.
        /// </summary>
        public FluentTranspiler MatchBranchTarget(Label targetLabel)
        {
            _matcher.Start();
            _matcher.MatchStartForward(new CodeMatch(instr => instr.labels.Contains(targetLabel)));
            
            if (!_matcher.IsValid)
            {
                _warnings.Add($"Branch target label not found in method");
            }
            
            return this;
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
        public static FluentTranspiler NukeDontDestroyOnLoad(this FluentTranspiler t)
        {
            return t
                .MatchCall(typeof(UnityEngine.Object), "DontDestroyOnLoad")
                .ReplaceWith(OpCodes.Pop);
        }

        /// <summary>
        /// Remove ALL calls to DontDestroyOnLoad in the method.
        /// </summary>
        public static FluentTranspiler NukeAllDontDestroyOnLoad(this FluentTranspiler t)
        {
            t.Reset();
            while (true)
            {
                t.MatchCallNext(typeof(UnityEngine.Object), "DontDestroyOnLoad");
                if (!t.HasMatch) break;
                t.ReplaceWith(OpCodes.Pop);
            }
            return t;
        }
        
        #endregion
    }
}
