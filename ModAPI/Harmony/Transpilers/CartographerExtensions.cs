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
    }
}
