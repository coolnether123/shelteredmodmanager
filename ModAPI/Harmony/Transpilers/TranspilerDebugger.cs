using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using System.IO;
using System.Text;
using ModAPI.Core;

namespace ModAPI.Harmony
{
    public static class TranspilerDebugger
    {
        public class PatchEdit
        {
            public string Kind;
            public int StartIndexBefore;
            public int RemovedCount;
            public int StartIndexAfter;
            public int AddedCount;
            public List<string> RemovedInstructions;
            public List<string> AddedInstructions;
            public string Note;
            public string Confidence;
        }

        private static int _dumpCounter = 0;  // Thread-safe counter for unique filenames
        /// <summary>
        /// Dump IL before/after and generate a diff file.
        /// Logs are written to the calling mod's Logs/TranspilerDumps/ folder.
        /// </summary>
        /// <param name="label">Descriptive label for the patch (used as folder name).</param>
        /// <param name="before">Original instructions.</param>
        /// <param name="after">Modified instructions.</param>
        /// <param name="modId">Optional mod ID override (defaults to calling assembly name).</param>
        /// <param name="force">Force dump even if DebugTranspilers is disabled.</param>
        /// <param name="originalMethod">Optional method being patched (enables stack analysis).</param>
        public static IEnumerable<CodeInstruction> DumpWithDiff(
            string label, 
            IEnumerable<CodeInstruction> before, 
            IEnumerable<CodeInstruction> after,
            string modId = null,
            bool force = false,
            MethodBase originalMethod = null)
        {
            if (!ModPrefs.DebugTranspilers && !force) return after;


            var listBefore = before.ToList();
            var listAfter = after.ToList();

            // Use proper comparison
            bool changed = !InstructionsEqual(listBefore, listAfter);

            if (!changed && !force)
            {
                MMLog.WriteDebug($"[TranspilerDebugger] {label}: No changes.");
                return after;
            }

            // Robust stack walking to identify the calling mod's assembly. This allows the 
            // debugger to route logs to the correct mod-specific directory even when 
            // called through helper libraries.
            string modName = modId ?? GetCallingModName();
            string safeLabel = SanitizePath(label);
            
            // Path: Mods/ModName/Logs/TranspilerDumps/Label/
            string dumpDir = Path.Combine("Mods", modName);
            dumpDir = Path.Combine(dumpDir, "Logs");
            dumpDir = Path.Combine(dumpDir, "TranspilerDumps");
            dumpDir = Path.Combine(dumpDir, safeLabel);
            string fullPath = Path.GetFullPath(dumpDir);

            // Windows API has a 260-character path limit. Hashing long labels ensures 
            // dump directories can still be created for deeply nested or descriptively named patches.
            if (fullPath.Length > 240)
            {
                string hash = label.GetHashCode().ToString("X8");
                safeLabel = $"hashed_{hash}";
                dumpDir = Path.Combine("Mods", modName);
                dumpDir = Path.Combine(dumpDir, "Logs");
                dumpDir = Path.Combine(dumpDir, "TranspilerDumps");
                dumpDir = Path.Combine(dumpDir, safeLabel);
            }
            
            // File I/O is a blocking operation. Moving dumps to a background task prevents 
            // the main game thread from stuttering during heavy transpilation phases.
            // Unique timestamps prevent file access collisions when multiple transpilers 
            // are running in parallel or quick succession.
            // An interlocked counter ensures thread-safe uniqueness even for dumps 
            // requested within the same millisecond.
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            string uniqueId = $"{timestamp}_{System.Threading.Interlocked.Increment(ref _dumpCounter):D3}";

            // Perform dump synchronously for .NET 3.5 compatibility.
            try
            {
                if (!Directory.Exists(dumpDir)) Directory.CreateDirectory(dumpDir);
                
                // Attempt stack analysis (best effort)
                Dictionary<int, List<Type>> stacksBefore = null;
                Dictionary<int, List<Type>> stacksAfter = null;
                HashSet<int> targetsBefore = null;
                HashSet<int> targetsAfter = null;
                
                try
                {
                    string err;
                    stacksBefore = StackSentinel.Analyze(listBefore, originalMethod, out err);
                    stacksAfter = StackSentinel.Analyze(listAfter, originalMethod, out err);
                    // Simple heuristic for branch targets: Any instruction with labels
                    targetsBefore = new HashSet<int>(listBefore.Select((instr, i) => instr.labels.Count > 0 ? i : -1).Where(i => i >= 0));
                    targetsAfter = new HashSet<int>(listAfter.Select((instr, i) => instr.labels.Count > 0 ? i : -1).Where(i => i >= 0));
                }
                catch { /* Ignore analysis errors during dump */ }

                WriteToFile(Path.Combine(dumpDir, uniqueId + "_Before.txt"), listBefore, stacksBefore, targetsBefore);
                WriteToFile(Path.Combine(dumpDir, uniqueId + "_After.txt"), listAfter, stacksAfter, targetsAfter);
                WriteDiff(Path.Combine(dumpDir, uniqueId + "_Diff.txt"), listBefore, listAfter, originalMethod: originalMethod);
                
                RecordSnapshot(modName, label, listBefore, listAfter, method: originalMethod);
                MMLog.WriteInfo("[TranspilerDebugger] Dumped " + label + " to " + dumpDir);
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[TranspilerDebugger] Failed to write dump: " + ex.Message);
            }
            
            return listAfter;
        }



        private static void WriteToFile(string path, List<CodeInstruction> instructions, 
            Dictionary<int, List<Type>> stacks = null, HashSet<int> branchTargets = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"// Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"// Total Instructions: {instructions.Count}");
            sb.AppendLine();
            
            for (int i = 0; i < instructions.Count; i++)
            {
                var instr = instructions[i];
                // Include labels for branch target debugging
                string labels = instr.labels.Count > 0 
                    ? $"[{string.Join(",", instr.labels.Select(l => $"L_{l.GetHashCode():X4}").ToArray())}] " 
                    : "";
                
                string branchMarker = (branchTargets != null && branchTargets.Contains(i)) ? "[TARGET] " : "";
                string stackMarker = "";
                if (stacks != null && stacks.TryGetValue(i, out var stack))
                {
                    string stackTypes = string.Join(", ", stack.Select(t => t != null ? t.Name : "object").ToArray());
                    stackMarker = $"[Stack:{stack.Count} ({stackTypes})] ";
                }

                
                sb.AppendLine($"{i:D4}: {branchMarker}{stackMarker}{labels}{instr}");
            }
            File.WriteAllText(path, sb.ToString());
        }
        private static string GetCallingModName()
        {
            // Walk up the stack to find the actual mod assembly (Robust walking).
            var trace = new System.Diagnostics.StackTrace();
            for (int i = 0; i < trace.FrameCount; i++)
            {
                var method = trace.GetFrame(i).GetMethod();
                var assembly = method?.DeclaringType?.Assembly;
                if (assembly == null) continue;
                
                if (assembly != typeof(TranspilerDebugger).Assembly &&
                    assembly != typeof(HarmonyLib.Harmony).Assembly &&
                    !assembly.FullName.StartsWith("System.") &&
                    !assembly.FullName.StartsWith("mscorlib") &&
                    !assembly.FullName.StartsWith("UnityEngine"))
                {
                    return assembly.GetName().Name;
                }
            }
            return "UnknownMod";
        }

        /// <summary>Proper IL comparison that handles operand differences correctly.</summary>
        private static bool InstructionsEqual(List<CodeInstruction> a, List<CodeInstruction> b)
        {
            if (a.Count != b.Count) return false;
            
            for (int i = 0; i < a.Count; i++)
            {
                if (!InstructionEqual(a[i], b[i])) return false;
            }
            return true;
        }

        private static bool InstructionEqual(CodeInstruction a, CodeInstruction b)
        {
            if (a.opcode != b.opcode) return false;
            if (a.operand == null && b.operand == null) return true;
            if (a.operand == null || b.operand == null) return false;
            
            // MetadataToken is the most reliable way to compare method references within 
            // the same module, as it is immutable and unique. For cross-module comparisons,
            // we use full signature matching to handle type forwarding and assembly versioning.
            if (a.operand is MemberInfo ma && b.operand is MemberInfo mb)
            {
                // Same module: Use MetadataToken (fast, reliable)
                if (ma.Module == mb.Module)
                    return ma.MetadataToken == mb.MetadataToken;
                    
                // Cross-module: Use composite signature for methods
                if (ma is MethodInfo methodA && mb is MethodInfo methodB)
                {
                    return GetMethodSignature(methodA) == GetMethodSignature(methodB);
                }
                
                // Cross-module: For fields, use AssemblyQualifiedName + field name
                if (ma is FieldInfo fieldA && mb is FieldInfo fieldB)
                {
                    return fieldA.DeclaringType.AssemblyQualifiedName == fieldB.DeclaringType.AssemblyQualifiedName
                        && fieldA.Name == fieldB.Name;
                }
            }
            
            return a.operand.Equals(b.operand);
        }

        /// <summary>Sanitize path and limit length to avoid Windows path limit.</summary>
        private static string SanitizePath(string input)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var result = new string(input.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            
            // Path sanitization prevents directory traversal attacks and accidental 
            // sub-directory creation from labels containing slashes or dots.
            result = result.Replace("..", "_").Replace("/", "_").Replace("\\", "_");
            
            // Hard limit on path segments ensures compatibility with legacy file systems 
            // and prevents excessively long filenames.
            const int MAX_LENGTH = 50;
            if (result.Length > MAX_LENGTH)
            {
                result = result.Substring(0, MAX_LENGTH);
            }
            
            return result;
        }

        private static string GetMethodSignature(MethodInfo mi)
        {
            // Format: AssemblyQualifiedName::MethodName(ParameterTypes)ReturnType
            var sb = new System.Text.StringBuilder();
            sb.Append(mi.DeclaringType.AssemblyQualifiedName);
            sb.Append("::");
            sb.Append(mi.Name);
            sb.Append("(");
            
            var parameters = mi.GetParameters();
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(parameters[i].ParameterType.AssemblyQualifiedName);
            }
            
            sb.Append(")");
            sb.Append(mi.ReturnType.AssemblyQualifiedName);
            
            return sb.ToString();
        }



        private static void WriteDiff(string path, List<CodeInstruction> before, List<CodeInstruction> after, MethodBase originalMethod = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("╔══════════════════════════════════════════════════╗");
            sb.AppendLine("║            TRANSPILER DIFF REPORT                ║");
            sb.AppendLine("╚══════════════════════════════════════════════════╝");
            sb.AppendLine();
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Before: {before.Count} instructions");
            sb.AppendLine($"After:  {after.Count} instructions");
            sb.AppendLine($"Delta:  {after.Count - before.Count:+#;-#;0}");
            sb.AppendLine();
            sb.AppendLine("Legend: >>> = Changed, [+] = Added, [-] = Removed");
            sb.AppendLine(new string('─', 100));
            
            // Analyze stacks for both states using context if available
            var respBefore = StackSentinel.Analyze(before, originalMethod, out _);
            var respAfter = StackSentinel.Analyze(after, originalMethod, out _);

            // Identify changed indices and context hunk regions
            const int CONTEXT_SIZE = 3;
            HashSet<int> hunkIndices = new HashSet<int>();
            int loopMax = Math.Max(before.Count, after.Count);
            
            for (int i = 0; i < loopMax; i++)
            {
                var b = (i < before.Count) ? before[i] : null;
                var a = (i < after.Count) ? after[i] : null;
                if (b == null || a == null || !InstructionEqual(b, a))
                {
                    for (int j = Math.Max(0, i - CONTEXT_SIZE); j <= Math.Min(loopMax - 1, i + CONTEXT_SIZE); j++)
                        hunkIndices.Add(j);
                }
            }

            bool inHunk = false;
            for (int i = 0; i < loopMax; i++)
            {
                if (hunkIndices.Contains(i))
                {
                    if (!inHunk)
                    {
                        sb.AppendLine($"\n--- @@ HUNK AT +{i} @@ ---");
                        inHunk = true;
                    }

                    var instrBefore = i < before.Count ? before[i] : null;
                    var instrAfter = i < after.Count ? after[i] : null;

                    string marker;
                    if (instrBefore == null) marker = "[+] ";
                    else if (instrAfter == null) marker = "[-] ";
                    else if (!InstructionEqual(instrBefore, instrAfter)) marker = ">>> ";
                    else marker = "    ";

                    // Gather stack visuals
                    int dBefore = (respBefore != null && i < before.Count && respBefore.ContainsKey(i)) ? respBefore[i].Count : -1;
                    int dAfter = (respAfter != null && i < after.Count && respAfter.ContainsKey(i)) ? respAfter[i].Count : -1;
                    string sB = dBefore >= 0 ? $"[S:{dBefore:D2}]" : "[SCAN_FAIL]";
                    string sA = dAfter >= 0 ? $"[S:{dAfter:D2}]" : "[SCAN_FAIL]";

                    if (marker == ">>> ")
                    {
                        sb.AppendLine($"{i:D3} - {sB} {instrBefore} // {ExplainOpCode(instrBefore.opcode.Name)}");
                        sb.AppendLine($"{i:D3} + {sA} {instrAfter} // {ExplainOpCode(instrAfter.opcode.Name)}");
                    }
                    else if (marker == "[+] ")
                    {
                        sb.AppendLine($"{i:D3} + {sA} {instrAfter} // {ExplainOpCode(instrAfter.opcode.Name)}");
                    }
                    else if (marker == "[-] ")
                    {
                        sb.AppendLine($"{i:D3} - {sB} {instrBefore} // {ExplainOpCode(instrBefore.opcode.Name)}");
                    }
                    else
                    {
                        sb.AppendLine($"{i:D3}   {sA} {instrAfter}");
                    }
                }
                else if (inHunk)
                {
                    sb.AppendLine("...");
                    inHunk = false;
                }
            }
            
            // Label Shift Analysis
            sb.AppendLine();
            sb.AppendLine("Label Shifts:");
            sb.AppendLine(new string('-', 50));
            var beforeLabels = new Dictionary<Label, int>();
            for(int k=0; k<before.Count; k++) 
                foreach(var l in before[k].labels) beforeLabels[l] = k;

            var afterLabels = new Dictionary<Label, int>();
            for(int k=0; k<after.Count; k++) 
                foreach(var l in after[k].labels) afterLabels[l] = k;

            bool anyShift = false;
            foreach(var kvp in beforeLabels)
            {
                if (afterLabels.TryGetValue(kvp.Key, out int newIndex))
                {
                    if (newIndex != kvp.Value)
                    {
                        sb.AppendLine($"Label {kvp.Key.GetHashCode():X4}: {kvp.Value:D3} -> {newIndex:D3} (Delta: {newIndex - kvp.Value:+0;-0})");
                        anyShift = true;
                    }
                }
                else
                {
                    sb.AppendLine($"Label {kvp.Key.GetHashCode():X4}: {kvp.Value:D3} -> DELETED");
                    anyShift = true;
                }
            }
            if (!anyShift) sb.AppendLine("No label shifts detected.");

            File.WriteAllText(path, sb.ToString());
        }

        public class Snapshot
        {
            public string ModId;
            public string StepName;
            public string MethodName;
            public string AssemblyName;
            public int MethodToken;
            public string PatchOrigin;

            public List<string> Instructions; // After
            public List<string> BeforeInstructions; // Before
            public List<int> StackDepths; // After
            public List<int> BeforeStackDepths; // Before
            public List<List<string>> AfterStackTypes;
            public List<List<string>> BeforeStackTypes;
            public DateTime Timestamp;
            public string DiffSummary; // e.g. "+5 lines"
            public int AddedCount;
            public int RemovedCount;
            public int ChangedCount;
            public double DurationMs;
            public int WarningCount;
            public List<string> Warnings;
            public List<PatchEdit> PatchEdits;
        }


        public static List<Snapshot> History = new List<Snapshot>();
        
        // Helper for UI tooltips
        public static string ExplainOpCode(string opCodeName)
        {
            switch(opCodeName.ToLower())
            {
                case "nop": return "No Operation (Placeholder)";
                case "ldarg.0": return "Load 'this' (instance) or arg 0";
                case "ldarg.1": return "Load argument 1";
                case "ldloc.0": return "Load local variable 0";
                case "stloc.0": return "Store into local variable 0";
                case "call": return "Call static method (or instance non-virtually)";
                case "callvirt": return "Call instance method (virtual check)";
                case "ret": return "Return from method";
                case "br": return "Unconditional branch (jump)";
                case "brtrue": return "Jump if value is true/non-zero";
                case "brfalse": return "Jump if value is false/zero";
                case "ldc.i4.0": return "Push constant 0 (int)";
                case "ldc.i4.1": return "Push constant 1 (int)";
                case "dup": return "Duplicate top stack value";
                case "pop": return "Remove top stack value";
                default: return "IL Instruction";
            }
        }

        public static void RecordSnapshot(
            string modId,
            string stepName,
            IEnumerable<CodeInstruction> before,
            IEnumerable<CodeInstruction> after,
            double durationMs = 0,
            int warningsCount = 0,
            MethodBase method = null,
            string patchOrigin = null,
            IEnumerable<PatchEdit> patchEdits = null,
            IEnumerable<string> warnings = null)
        {
            var afterList = after.ToList();
            var beforeList = before.ToList();
            var methodIdentifier = BuildMethodIdentifier(method, stepName);
            var snapshotStepName = !string.IsNullOrEmpty(stepName)
                ? stepName
                : (!string.IsNullOrEmpty(methodIdentifier) ? methodIdentifier : "<unresolved method>");
            
            // Analyze stacks
            StackSentinel.GetVisualStack(beforeList, method, out var beforeDepths, out var beforeTypes);
            StackSentinel.GetVisualStack(afterList, method, out var afterDepths, out var afterTypes);


            int delta = afterList.Count - beforeList.Count;
            string diffText = delta > 0 ? $"+{delta}" : delta < 0 ? $"{delta}" : "0";
            

            History.Add(new Snapshot 
            {
                ModId = modId ?? "Unknown",
                StepName = snapshotStepName,
                MethodName = methodIdentifier,
                AssemblyName = method != null && method.Module != null ? method.Module.Assembly.GetName().Name : string.Empty,
                MethodToken = method != null ? method.MetadataToken : 0,
                PatchOrigin = patchOrigin ?? string.Empty,
                Instructions = afterList.Select(i => i.ToString()).ToList(),
                BeforeInstructions = beforeList.Select(i => i.ToString()).ToList(),
                StackDepths = afterDepths,
                BeforeStackDepths = beforeDepths,
                AfterStackTypes = afterTypes,
                BeforeStackTypes = beforeTypes,
                Timestamp = DateTime.Now,
                DiffSummary = diffText,
                AddedCount = Math.Max(0, delta),
                RemovedCount = Math.Max(0, -delta),
                DurationMs = durationMs,
                WarningCount = warningsCount,
                Warnings = warnings != null ? warnings.ToList() : new List<string>(),
                PatchEdits = patchEdits != null
                    ? patchEdits.Select(e => new PatchEdit
                    {
                        Kind = e.Kind ?? string.Empty,
                        StartIndexBefore = e.StartIndexBefore,
                        RemovedCount = e.RemovedCount,
                        StartIndexAfter = e.StartIndexAfter,
                        AddedCount = e.AddedCount,
                        RemovedInstructions = e.RemovedInstructions != null ? new List<string>(e.RemovedInstructions) : new List<string>(),
                        AddedInstructions = e.AddedInstructions != null ? new List<string>(e.AddedInstructions) : new List<string>(),
                        Note = e.Note ?? string.Empty,
                        Confidence = e.Confidence ?? string.Empty
                    }).ToList()
                    : new List<PatchEdit>()
            });

            
            if (History.Count > 100) History.RemoveAt(0);
        }

        private static string BuildMethodIdentifier(MethodBase method, string stepName)
        {
            if (method != null && method.DeclaringType != null)
            {
                return method.DeclaringType.FullName + "." + method.Name;
            }

            if (string.IsNullOrEmpty(stepName))
            {
                return string.Empty;
            }

            var candidate = stepName.Trim();
            var bracketStart = candidate.LastIndexOf('[');
            var bracketEnd = candidate.LastIndexOf(']');
            if (bracketStart >= 0 && bracketEnd > bracketStart)
            {
                var bracketValue = candidate.Substring(bracketStart + 1, bracketEnd - bracketStart - 1).Trim();
                if (bracketValue.IndexOf('.') > 0)
                {
                    candidate = bracketValue;
                }
            }

            var paren = candidate.IndexOf('(');
            if (paren > 0)
            {
                candidate = candidate.Substring(0, paren).Trim();
            }

            return candidate.IndexOf('.') > 0 ? candidate : string.Empty;
        }
    }
}
