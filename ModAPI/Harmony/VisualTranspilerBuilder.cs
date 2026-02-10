using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using ModAPI.Inspector;

namespace ModAPI.Harmony
{
    public class VisualTranspilerBuilder 
    {
        public enum PatchAction 
        {
            InsertBefore,
            InsertAfter,
            Replace,
            ModifyOperand,
            Skip,
            ConditionalWrap
        }
        
        public class PatchConfiguration
        {
            public Type TargetType;
            public string TargetMethod;
            public PatchAction Action;
            public Type AnchorType;
            public string AnchorMethod;
            public Type InjectionType;
            public string InjectionMethod;
            public object OriginalValue;
            public object NewValue;
            public int TargetILOffset;
        }
        
        // Generates C# code from visual selection
        public static string GeneratePatchCode(MethodBase target, PatchConfiguration config)
        {
            if (config == null) return "// Error: Patch configuration is null.";
            if (config.TargetType == null || string.IsNullOrEmpty(config.TargetMethod))
                return "// Error: TargetType and TargetMethod are required.";

            var sb = new StringBuilder();
            sb.AppendLine($"[HarmonyPatch(typeof({config.TargetType.Name}), \"{config.TargetMethod}\")]");
            sb.AppendLine($"public static class {config.TargetMethod}_Patch");
            sb.AppendLine("{");
            sb.AppendLine("    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)");
            sb.AppendLine("    {");
            sb.AppendLine("        var t = FluentTranspiler.For(instructions);");
            sb.AppendLine();
            
            switch (config.Action)
            {
                case PatchAction.InsertBefore:
                    sb.AppendLine($"        t.MatchCall(typeof({config.AnchorType?.Name ?? "TargetType"}), \"{config.AnchorMethod}\")");
                    sb.AppendLine($"         .InsertBefore(OpCodes.Call, AccessTools.Method(typeof({config.InjectionType?.Name ?? "PatchHelpers"}), \"{config.InjectionMethod}\"));");
                    break;
                    
                case PatchAction.ModifyOperand:
                    sb.AppendLine($"        t.MatchConstInt({config.OriginalValue})");
                    sb.AppendLine($"         .ReplaceOperand(0, {config.NewValue});");
                    break;

                case PatchAction.InsertAfter:
                    sb.AppendLine($"        t.MatchCall(typeof({config.AnchorType?.Name ?? "TargetType"}), \"{config.AnchorMethod}\")");
                    sb.AppendLine($"         .InsertAfter(OpCodes.Call, AccessTools.Method(typeof({config.InjectionType?.Name ?? "PatchHelpers"}), \"{config.InjectionMethod}\"));");
                    break;

                case PatchAction.Replace:
                    sb.AppendLine($"        t.MatchCall(typeof({config.AnchorType?.Name ?? "TargetType"}), \"{config.AnchorMethod}\")");
                    sb.AppendLine($"         .ReplaceWith(OpCodes.Call, AccessTools.Method(typeof({config.InjectionType?.Name ?? "PatchHelpers"}), \"{config.InjectionMethod}\"));");
                    break;

                case PatchAction.Skip:
                    sb.AppendLine($"        t.MatchCall(typeof({config.AnchorType?.Name ?? "TargetType"}), \"{config.AnchorMethod}\")");
                    sb.AppendLine("         .Remove();");
                    break;

                case PatchAction.ConditionalWrap:
                    sb.AppendLine("        // Wrap target logic with a condition guard.");
                    sb.AppendLine($"        t.MatchCall(typeof({config.AnchorType?.Name ?? "TargetType"}), \"{config.AnchorMethod}\")");
                    sb.AppendLine($"         .InsertBefore(CodeInstruction.Call(typeof({config.InjectionType?.Name ?? "PatchHelpers"}), \"ShouldRun\"));");
                    break;
                    
                // ... other cases
            }
            
            sb.AppendLine("        return t.Build();");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            
            return sb.ToString();
        }
        
        // Shows preview of what the method will look like after patch.
        // Optional displayedSource allows the UI to preview against the current cached text snapshot.
        public static string GenerateCSharpPreview(MethodBase method, PatchConfiguration config, string displayedSource = null)
        {
            if (method == null || config == null) return "// Error: Missing method or patch configuration.";

            var source = string.IsNullOrEmpty(displayedSource) ? SourceCacheManager.GetSource(method) : displayedSource;
            if (source.StartsWith("// Error")) return source;

            var normalized = source.Replace("\r\n", "\n");
            var lines = normalized.Split('\n');
            var targetLine = ResolveTargetLine(method, config, lines);

            var preview = new List<string>(lines.Length + 8);
            for (var i = 0; i < lines.Length; i++)
            {
                if (i == targetLine && config.Action == PatchAction.InsertBefore)
                {
                    preview.Add($"+ {config.InjectionMethod}(); // INSERTED");
                }

                if (i == targetLine && config.Action == PatchAction.Replace)
                {
                    preview.Add("- " + lines[i] + " // REPLACED");
                    preview.Add("+ " + (config.InjectionMethod ?? "InjectedCall") + "(); // REPLACED");
                    continue;
                }

                if (i == targetLine && config.Action == PatchAction.Skip)
                {
                    preview.Add("- " + lines[i] + " // SKIPPED");
                    continue;
                }

                if (i == targetLine && config.Action == PatchAction.ModifyOperand)
                {
                    var originalValue = config.OriginalValue != null ? config.OriginalValue.ToString() : string.Empty;
                    var newValue = config.NewValue != null ? config.NewValue.ToString() : string.Empty;
                    var modifiedLine = ReplaceFirst(lines[i], originalValue, newValue);
                    preview.Add("- " + lines[i]);
                    preview.Add("+ " + modifiedLine + " // OPERAND MODIFIED");
                    continue;
                }

                if (i == targetLine && config.Action == PatchAction.ConditionalWrap)
                {
                    preview.Add("+ if (ShouldRun()) // WRAP START");
                    preview.Add("+ {");
                    preview.Add("  " + lines[i]);
                    preview.Add("+ } // WRAP END");
                    continue;
                }

                preview.Add("  " + lines[i]);
                if (i == targetLine && config.Action == PatchAction.InsertAfter)
                {
                    preview.Add($"+ {config.InjectionMethod}(); // INSERTED");
                }
            }

            return string.Join("\n", preview.ToArray());
        }

        private static int ResolveTargetLine(MethodBase method, PatchConfiguration config, string[] lines)
        {
            if (lines == null || lines.Length == 0) return 0;

            // 1) Prefer explicit IL target from map file.
            var mapped = SourceCacheManager.MapILToSourceLine(method, config.TargetILOffset);
            if (mapped > 0 && mapped - 1 < lines.Length) return mapped - 1;

            // 2) If matching a call anchor, locate it directly in source.
            if (!string.IsNullOrEmpty(config.AnchorMethod))
            {
                for (var i = 0; i < lines.Length; i++)
                {
                    if (lines[i].IndexOf(config.AnchorMethod, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return i;
                    }
                }
            }

            // 3) For constant modifications, attempt value search fallback.
            if (config.Action == PatchAction.ModifyOperand && config.OriginalValue != null)
            {
                var token = config.OriginalValue.ToString();
                if (!string.IsNullOrEmpty(token))
                {
                    for (var i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].IndexOf(token, StringComparison.Ordinal) >= 0)
                        {
                            return i;
                        }
                    }
                }
            }

            return 0;
        }

        private static string ReplaceFirst(string input, string from, string to)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(from)) return input;
            var index = input.IndexOf(from, StringComparison.Ordinal);
            if (index < 0) return input;
            return input.Substring(0, index) + to + input.Substring(index + from.Length);
        }
    }
}
