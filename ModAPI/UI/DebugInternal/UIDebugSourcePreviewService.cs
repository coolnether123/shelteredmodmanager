using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ModAPI.Internal.DebugUI
{
    using Snapshot = ModAPI.Harmony.TranspilerDebugger.Snapshot;
    using PatchEdit = ModAPI.Harmony.TranspilerDebugger.PatchEdit;

    internal static class UIDebugSourcePreviewService
    {
        public static UIDebugSourceDiffAlignedRows BuildAlignedSourceDiffRows(string originalSource, string patchedSource)
        {
            return UIDebugSourceDiffService.BuildAlignedSourceDiffRows(originalSource, patchedSource);
        }

        public static List<string> SplitLines(string text)
        {
            return UIDebugSourceDiffService.SplitLines(text);
        }

        public static string FormatSourceLineForDisplay(string raw, bool patched)
        {
            var line = raw ?? string.Empty;
            var trimmed = line.TrimStart();
            var escaped = EscapeRichText(line);

            if (!patched) return "<color=#D8D8D8>" + escaped + "</color>";
            if (trimmed.StartsWith("//   +", StringComparison.Ordinal)) return "<color=#7CFC00>" + escaped + "</color>";
            if (trimmed.StartsWith("//   -", StringComparison.Ordinal)) return "<color=#FF8A8A>" + escaped + "</color>";
            if (trimmed.StartsWith("// [Regex Rewrite]", StringComparison.Ordinal) || line.IndexOf("[REGEX_PATCH]", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "<color=#F6D365>" + escaped + "</color>";
            }
            if (line.IndexOf("TRANSPILE INJECTION PREVIEW", StringComparison.OrdinalIgnoreCase) >= 0 ||
                trimmed.StartsWith("// Hunk", StringComparison.Ordinal))
            {
                return "<color=#8ED6FF>" + escaped + "</color>";
            }
            if (trimmed.StartsWith("//", StringComparison.Ordinal)) return "<color=#B0B0B0>" + escaped + "</color>";
            return "<color=#EDEDED>" + escaped + "</color>";
        }

        public static string EscapeRichText(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        public static UIDebugPatchedSourcePreviewResult BuildPatchedSourcePreview(
            string vanillaSource,
            Snapshot snapshot,
            MethodBase selectedMethod,
            string selectedMethodId,
            IList<UIDebugDiffLine> currentDiff)
        {
            var result = new UIDebugPatchedSourcePreviewResult();

            if (string.IsNullOrEmpty(vanillaSource))
            {
                result.PatchedSourceRewrittenText = "// Patched preview unavailable: vanilla source is empty.";
                result.PatchedSourceText = result.PatchedSourceRewrittenText;
                return result;
            }

            if (snapshot == null)
            {
                result.PatchedSourceRewrittenText = vanillaSource;
                result.PatchedSourceText = vanillaSource;
                return result;
            }

            var hunks = BuildSourcePreviewHunks(snapshot, currentDiff);
            if (hunks.Count == 0)
            {
                result.PatchedSourceRewrittenText = vanillaSource;
                result.PatchedSourceText = vanillaSource + "\n\n// [Transpiler Injection Preview] No IL additions/removals were detected.";
                return result;
            }

            List<string> rewriteSummaries;
            int rewriteCount;
            var rewritten = UIDebugSourceRewriteService.ApplyRegexSourceRewrites(
                vanillaSource,
                hunks,
                snapshot,
                selectedMethod,
                selectedMethodId,
                out rewriteSummaries,
                out rewriteCount);

            result.PatchedSourceRewrittenText = rewritten;
            result.RegexReplaceCount = rewriteCount;
            if (rewriteSummaries != null && rewriteSummaries.Count > 0)
            {
                result.RegexSummaries.AddRange(rewriteSummaries);
            }

            var lines = rewritten.Replace("\r\n", "\n").Split('\n').ToList();
            var methodName = selectedMethod != null
                ? selectedMethod.Name
                : UIDebugSourceMethodLocator.ExtractMethodNameFromSelectedId(selectedMethodId);
            var insertLine = FindBestOverlayInsertLine(lines, hunks, methodName);
            var indent = UIDebugSourceMethodLocator.GuessIndentation(lines, insertLine);
            var overlay = RenderSourcePreviewOverlay(hunks, indent, result.RegexSummaries, result.RegexReplaceCount);
            lines.InsertRange(insertLine, overlay);
            result.PatchedSourceText = string.Join("\n", lines.ToArray());
            return result;
        }

        public static List<UIDebugDiffLine> ComputeDiff(List<string> before, List<string> after)
        {
            return UIDebugSourceDiffService.ComputeDiff(before, after);
        }

        private static int FindBestOverlayInsertLine(List<string> lines, List<UIDebugSourcePreviewHunk> hunks, string methodName)
        {
            if (lines == null || lines.Count == 0) return 0;

            var methodStartLine = 0;
            var methodEndLine = lines.Count - 1;
            UIDebugSourceMethodLocator.TryGetMethodBodyLineRange(lines, methodName, out methodStartLine, out methodEndLine);

            for (var i = methodStartLine; i <= methodEndLine && i < lines.Count; i++)
            {
                var line = lines[i] ?? string.Empty;
                if (line.IndexOf("[REGEX_PATCH]", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return i;
                }
            }

            if (hunks != null && HasVector2ZeroCtorHunk(hunks))
            {
                for (var i = methodStartLine; i <= methodEndLine && i < lines.Count; i++)
                {
                    var line = lines[i] ?? string.Empty;
                    if (line.IndexOf("new Vector2(", StringComparison.Ordinal) >= 0 &&
                        line.IndexOf("0", StringComparison.Ordinal) >= 0)
                    {
                        return i;
                    }
                }
            }

            var methodBodyInsert = UIDebugSourceMethodLocator.FindMethodBodyInsertLine(lines, methodName);
            if (methodBodyInsert >= 0 && methodBodyInsert <= lines.Count)
            {
                return methodBodyInsert;
            }

            return UIDebugSourceMethodLocator.FindFirstBlockInsertLine(lines);
        }

        private static bool HasVector2ZeroCtorHunk(List<UIDebugSourcePreviewHunk> hunks)
        {
            if (hunks == null) return false;
            for (var h = 0; h < hunks.Count; h++)
            {
                var removed = hunks[h] != null ? hunks[h].Removed : null;
                if (removed == null || removed.Count == 0) continue;

                var hasCtor = false;
                var zeroLoads = 0;
                for (var i = 0; i < removed.Count; i++)
                {
                    var line = removed[i] ?? string.Empty;
                    if (line.IndexOf("Vector2::.ctor", StringComparison.OrdinalIgnoreCase) >= 0) hasCtor = true;
                    if (line.IndexOf("ldc.r4 0", StringComparison.OrdinalIgnoreCase) >= 0) zeroLoads++;
                }

                if (hasCtor && zeroLoads >= 2) return true;
            }

            return false;
        }

        private static List<UIDebugSourcePreviewHunk> BuildSourcePreviewHunks(Snapshot snapshot, IList<UIDebugDiffLine> currentDiff)
        {
            if (snapshot != null && snapshot.PatchEdits != null && snapshot.PatchEdits.Count > 0)
            {
                var manifestHunks = BuildSourcePreviewHunksFromPatchEdits(snapshot.PatchEdits);
                if (manifestHunks.Count > 0) return manifestHunks;
            }

            var diff = currentDiff != null
                ? currentDiff.ToList()
                : UIDebugSourceDiffService.ComputeDiff(snapshot.BeforeInstructions, snapshot.Instructions);

            var hunks = new List<UIDebugSourcePreviewHunk>();
            UIDebugSourcePreviewHunk current = null;

            for (var i = 0; i < diff.Count; i++)
            {
                var line = diff[i];
                var isRemoved = line != null && line.LeftMarker == "-" && !string.IsNullOrEmpty(line.LeftContent);
                var isAdded = line != null && line.RightMarker == "+" && !string.IsNullOrEmpty(line.RightContent);
                if (!isRemoved && !isAdded)
                {
                    if (current != null && (current.Removed.Count > 0 || current.Added.Count > 0))
                    {
                        hunks.Add(current);
                    }

                    current = null;
                    continue;
                }

                if (current == null)
                {
                    current = new UIDebugSourcePreviewHunk { StartIndexBefore = line.LeftIndex };
                }

                if (isRemoved) current.Removed.Add(FormatInstructionForSourcePreview(line.LeftContent));
                if (isAdded) current.Added.Add(FormatInstructionForSourcePreview(line.RightContent));
            }

            if (current != null && (current.Removed.Count > 0 || current.Added.Count > 0))
            {
                hunks.Add(current);
            }

            return hunks;
        }

        private static List<UIDebugSourcePreviewHunk> BuildSourcePreviewHunksFromPatchEdits(IList<PatchEdit> patchEdits)
        {
            var hunks = new List<UIDebugSourcePreviewHunk>();
            if (patchEdits == null) return hunks;

            for (var i = 0; i < patchEdits.Count; i++)
            {
                var edit = patchEdits[i];
                if (edit == null) continue;

                var removed = edit.RemovedInstructions ?? new List<string>();
                var added = edit.AddedInstructions ?? new List<string>();
                if (removed.Count == 0 && added.Count == 0) continue;

                var hunk = new UIDebugSourcePreviewHunk { StartIndexBefore = edit.StartIndexBefore };
                for (var r = 0; r < removed.Count; r++) hunk.Removed.Add(FormatInstructionForSourcePreview(removed[r]));
                for (var a = 0; a < added.Count; a++) hunk.Added.Add(FormatInstructionForSourcePreview(added[a]));
                if (hunk.Removed.Count > 0 || hunk.Added.Count > 0) hunks.Add(hunk);
            }

            return hunks;
        }

        private static List<string> RenderSourcePreviewOverlay(List<UIDebugSourcePreviewHunk> hunks, string indent, List<string> regexSummaries, int regexRewriteCount)
        {
            var lines = new List<string>();
            if (hunks == null || hunks.Count == 0) return lines;

            lines.Add(indent + "// === TRANSPILE INJECTION PREVIEW (estimated from IL diff) ===");
            lines.Add(indent + "// This shows likely runtime-injected operations next to original source.");
            lines.Add(indent + "// [Regex Rewrite] " + regexRewriteCount + " source replacements applied.");
            if (regexSummaries != null && regexSummaries.Count > 0)
            {
                var shown = Math.Min(4, regexSummaries.Count);
                for (var i = 0; i < shown; i++) lines.Add(indent + "// " + regexSummaries[i]);
                if (regexSummaries.Count > shown) lines.Add(indent + "// ... " + (regexSummaries.Count - shown) + " more rewrite notes");
            }

            const int maxHunks = 8;
            const int maxLinesPerSide = 6;
            var displayedHunks = Math.Min(maxHunks, hunks.Count);
            for (var h = 0; h < displayedHunks; h++)
            {
                var hunk = hunks[h];
                lines.Add(indent + "// Hunk " + (h + 1) + ":");

                var removedCount = hunk.Removed != null ? hunk.Removed.Count : 0;
                var addedCount = hunk.Added != null ? hunk.Added.Count : 0;
                if (removedCount == 0 && addedCount == 0)
                {
                    lines.Add(indent + "//   (no delta lines)");
                    continue;
                }

                if (removedCount > 0)
                {
                    var removedShown = Math.Min(maxLinesPerSide, removedCount);
                    for (var i = 0; i < removedShown; i++) lines.Add(indent + "//   - " + hunk.Removed[i]);
                    if (removedCount > removedShown) lines.Add(indent + "//   - ... " + (removedCount - removedShown) + " more removed IL lines");
                }

                if (addedCount > 0)
                {
                    var addedShown = Math.Min(maxLinesPerSide, addedCount);
                    for (var i = 0; i < addedShown; i++) lines.Add(indent + "//   + " + hunk.Added[i]);
                    if (addedCount > addedShown) lines.Add(indent + "//   + ... " + (addedCount - addedShown) + " more added IL lines");
                }
            }

            if (hunks.Count > displayedHunks) lines.Add(indent + "// ... " + (hunks.Count - displayedHunks) + " more IL diff hunks omitted");
            lines.Add(indent + "// === END INJECTION PREVIEW ===");
            lines.Add(string.Empty);
            return lines;
        }

        private static string FormatInstructionForSourcePreview(string ilLine)
        {
            if (string.IsNullOrEmpty(ilLine)) return string.Empty;
            var text = ilLine.Trim();
            return text.Length > 170 ? text.Substring(0, 167) + "..." : text;
        }
    }
}
