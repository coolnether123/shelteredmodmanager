using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using ModAPI.Core;

namespace ModAPI.Harmony
{
    public class Anchor
    {
        public int Index;
        public CodeInstruction Instruction;
        public float UniquenessScore;

        public Anchor(int index, CodeInstruction instruction, float uniquenessScore)
        {
            Index = index;
            Instruction = instruction;
            UniquenessScore = uniquenessScore;
        }
    }

    public class AnchorReport
    {
        public List<Anchor> SafeAnchors = new List<Anchor>();
        public List<string> Suggestions = new List<string>();

        public string ToSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine(
                $"=== Anchor Report ({SafeAnchors.Count} anchors) ===");
            foreach (var a in SafeAnchors.OrderByDescending(
                x => x.UniquenessScore))
            {
                sb.AppendLine(
                    $"  [{a.Index:D4}] Score:{a.UniquenessScore:F1}" +
                    $" | {a.Instruction}");
            }
            if (Suggestions.Count > 0)
            {
                sb.AppendLine("Suggestions:");
                foreach (var s in Suggestions) sb.AppendLine("  * " + s);
            }
            return sb.ToString();
        }
    }

    public static class CartographerExtensions 
    {
        /// <summary>
        /// Analyzes method for unique anchors without modifying.
        /// </summary>
        public static AnchorReport MapAnchors(this FluentTranspiler t, float threshold = 1.2f)
        {
            var instructions = t.Instructions().ToList();
            var report = new AnchorReport();
            
            // Frequency analysis
            var opcodeFrequency = instructions.GroupBy(i => i.opcode)
                                              .ToDictionary(g => g.Key, g => g.Count());

            // String frequency analysis
            var stringFrequency = instructions
                .Where(i => i.operand is string)
                .GroupBy(i => (string)i.operand)
                .ToDictionary(g => g.Key, g => g.Count());
            
            // "Green Zone" detection: Rare opcodes + specific operands
            for (int i = 0; i < instructions.Count; i++)
            {
                var instr = instructions[i];
                float uniquenessScore = 0;
                
                // Low-frequency opcodes score higher
                if (opcodeFrequency[instr.opcode] < 3) uniquenessScore += 0.5f;
                
                // Specific string/method references score highest
                if (instr.operand is string s)
                {
                    int freq = stringFrequency.ContainsKey(s) ? stringFrequency[s] : 1;
                    uniquenessScore += freq == 1 ? 1.5f : 1.0f / freq;
                }
                
                if (instr.operand is MethodInfo mi && mi.DeclaringType != typeof(object)) 
                    uniquenessScore += 0.8f;
                    
                if (uniquenessScore >= threshold)
                    report.SafeAnchors.Add(new Anchor(i, instr, uniquenessScore));
            }

            // Context scoring: unique pairs
            for (int i = 0; i < instructions.Count - 1; i++)
            {
                var first = report.SafeAnchors.FirstOrDefault(a => a.Index == i);
                var second = report.SafeAnchors.FirstOrDefault(a => a.Index == i + 1);

                if (first != null && second != null)
                {
                    // Boost both anchors
                    first.UniquenessScore += 0.5f;
                    second.UniquenessScore += 0.5f;
                }
            }
            
            return report;
        }

        /// <summary>
        /// Analyzes method for anchors and logs the summary to MMLog.
        /// Useful during development to find robust jump targets.
        /// </summary>
        public static FluentTranspiler ExportAnchors(this FluentTranspiler t, float threshold = 1.2f)
        {
            var report = t.MapAnchors(threshold);
            MMLog.WriteInfo(report.ToSummary());
            return t;
        }

        /// <summary>
        /// Finds the next instruction with a high uniqueness score and moves the matcher to it.
        /// </summary>
        public static FluentTranspiler FindNextAnchor(this FluentTranspiler t, float minUniqueness = 1.0f)
        {
            // We use the full map for context scoring
            var report = t.MapAnchors(minUniqueness);
            var nextAnchor = report.SafeAnchors
                .OrderBy(a => a.Index)
                .FirstOrDefault(a => a.Index > t.CurrentIndex);

            if (nextAnchor != null)
            {
                t.MoveTo(nextAnchor.Index);
            }
            else
            {
                t.AddWarning($"FindNextAnchor: No anchor with score >= {minUniqueness} found after index {t.CurrentIndex}");
            }
            return t;
        }

        /// <summary>
        /// Inserts code safely relative to the current anchor.
        /// - If anchor is a Return/Throw, inserts BEFORE.
        /// - Otherwise inserts AFTER, ensuring we don't accidentally split a block if the next instruction is a jump target?
        ///   Actually, Harmony handles label shifting on InsertBefore/After automatically.
        ///   This method mainly ensures we don't insert dead code after a Return.
        /// </summary>
        public static FluentTranspiler SafeInsert(this FluentTranspiler t, params CodeInstruction[] instructions)
        {
            if (!t.HasMatch) return t;

            bool isTerminator = t.Current.opcode == OpCodes.Ret || t.Current.opcode == OpCodes.Throw;

            if (isTerminator)
            {
                t.InsertBefore(instructions);
            }
            else
            {
                t.InsertAfter(instructions);
            }
            return t;
        }

        /// <summary>
        /// Attempts to find a 'fuzzy' match for a failed search by looking for instructions 
        /// with similar opcodes or the same operand.
        /// </summary>
        public static void SuggestFuzzyMatches(this FluentTranspiler t, OpCode opcode, object operand)
        {
            var instructions = t.Instructions().ToList();
            var suggestions = new List<int>();

            for (int i = 0; i < instructions.Count; i++)
            {
                var instr = instructions[i];
                bool opcodeMatch = instr.opcode == opcode;
                bool operandMatch = operand != null && Equals(instr.operand, operand);

                if (opcodeMatch || operandMatch)
                {
                    suggestions.Add(i);
                }
            }

            if (suggestions.Count > 0)
            {
                string msg = $"Fuzzy match suggestions for {opcode} {operand}: Lines " + 
                             string.Join(", ", suggestions.Select(s => s.ToString()).ToArray());
                t.AddWarning(msg);
                MMLog.WriteWarning("[Cartographer] " + msg);
            }
        }

        /// <summary>
        /// Returns indices of instructions that appear to be semantic boundaries
        /// (Method entry, branch targets with many inputs, or terminal instructions).
        /// </summary>
        public static List<int> GetSemanticAnchors(this FluentTranspiler t)
        {
            var instructions = t.Instructions().ToList();
            var labels = instructions.SelectMany(i => i.labels).Distinct().ToList();
            var anchors = new List<int>();

            for (int i = 0; i < instructions.Count; i++)
            {
                var instr = instructions[i];
                // Branched-to instructions are good semantic anchors
                if (instr.labels.Count > 0) anchors.Add(i);
                // Terminators are good anchors
                if (instr.opcode == OpCodes.Ret || instr.opcode == OpCodes.Throw) anchors.Add(i);
            }

            return anchors.Distinct().OrderBy(x => x).ToList();
        }
    }
}
