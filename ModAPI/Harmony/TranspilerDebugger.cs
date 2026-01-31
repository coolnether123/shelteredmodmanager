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
        public static IEnumerable<CodeInstruction> DumpWithDiff(
            string label, 
            IEnumerable<CodeInstruction> before, 
            IEnumerable<CodeInstruction> after,
            string modId = null,
            bool force = false)
        {
            if (!ModSettings.DebugTranspilers && !force) return after;

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
                WriteToFile(Path.Combine(dumpDir, uniqueId + "_Before.txt"), listBefore);
                WriteToFile(Path.Combine(dumpDir, uniqueId + "_After.txt"), listAfter);
                WriteDiff(Path.Combine(dumpDir, uniqueId + "_Diff.txt"), listBefore, listAfter);
                
                MMLog.WriteInfo("[TranspilerDebugger] Dumped " + label + " to " + dumpDir);
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[TranspilerDebugger] Failed to write dump: " + ex.Message);
            }
            
            return listAfter;
        }

        /// <summary>Get the name of the mod that called this debugger.</summary>
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

        private static void WriteToFile(string path, List<CodeInstruction> instructions)
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
                sb.AppendLine($"{i:D4}: {labels}{instr}");
            }
            File.WriteAllText(path, sb.ToString());
        }

        private static void WriteDiff(string path, List<CodeInstruction> before, List<CodeInstruction> after)
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
            
            int max = Math.Max(before.Count, after.Count);
            for (int i = 0; i < max; i++)
            {
                var instrBefore = i < before.Count ? before[i] : null;
                var instrAfter = i < after.Count ? after[i] : null;
                
                string b = instrBefore?.ToString() ?? "";
                string a = instrAfter?.ToString() ?? "";
                
                string marker;
                if (instrBefore == null)
                    marker = "[+] ";
                else if (instrAfter == null)
                    marker = "[-] ";
                else if (!InstructionEqual(instrBefore, instrAfter))
                    marker = ">>> ";
                else
                    marker = "    ";
                
                sb.AppendLine($"{i:D3} {marker} {b,-50} │ {a}");
            }
            
            File.WriteAllText(path, sb.ToString());
        }
    }
}
