using System;
using System.Collections.Generic;
using System.IO;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Modules.Shared;

namespace Cortex.Services
{
    internal sealed class UsingDirectiveOrganizationService
    {
        public bool TryBuildPreviewPlan(
            CortexShellState state,
            string documentPath,
            out DocumentEditPreviewPlan previewPlan,
            out string updatedText,
            out string statusMessage)
        {
            previewPlan = null;
            updatedText = string.Empty;
            statusMessage = "Using directives could not be organized.";
            if (string.IsNullOrEmpty(documentPath))
            {
                statusMessage = "The current context does not resolve to a source document.";
                return false;
            }

            var session = CortexModuleUtil.FindOpenDocument(state, documentPath);
            string originalText;
            if (!TryReadText(session, documentPath, out originalText))
            {
                statusMessage = "The resolved source document could not be read.";
                return false;
            }

            UsingDirectiveOrganizationResult result;
            if (!TryOrganizeTopLevelUsings(originalText, out result))
            {
                statusMessage = "No using directives were found to organize.";
                return false;
            }

            if (string.Equals(originalText, result.UpdatedText, StringComparison.Ordinal))
            {
                statusMessage = "Using directives are already organized.";
                return false;
            }

            updatedText = result.UpdatedText;
            previewPlan = new DocumentEditPreviewPlan
            {
                CommandId = "cortex.editor.removeAndSortUsings",
                Title = "Remove and Sort Usings",
                ApplyLabel = "Apply Changes",
                StatusMessage = "Preview organized using directives for " + Path.GetFileName(documentPath) + ".",
                PrimaryDocumentPath = documentPath,
                Documents = new[]
                {
                    new LanguageServiceDocumentChange
                    {
                        DocumentPath = documentPath,
                        DisplayPath = documentPath,
                        ChangeCount = 1,
                        Edits = new[]
                        {
                            new LanguageServiceTextEdit
                            {
                                Range = BuildRange(originalText, result.BlockStart, result.BlockLength),
                                OldText = result.OriginalBlock,
                                NewText = result.UpdatedBlock,
                                PreviewText = "Organize top-level using directives."
                            }
                        }
                    }
                },
                CanApply = true
            };
            statusMessage = previewPlan.StatusMessage;
            return true;
        }

        private static bool TryReadText(DocumentSession session, string documentPath, out string text)
        {
            text = string.Empty;
            if (session != null)
            {
                text = session.Text ?? string.Empty;
                return true;
            }

            try
            {
                text = File.ReadAllText(documentPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static LanguageServiceRange BuildRange(string text, int start, int length)
        {
            var range = new LanguageServiceRange
            {
                Start = Math.Max(0, start),
                Length = Math.Max(0, length)
            };

            var safeText = text ?? string.Empty;
            var clampedStart = Math.Max(0, Math.Min(range.Start, safeText.Length));
            var clampedEnd = Math.Max(clampedStart, Math.Min(safeText.Length, clampedStart + range.Length));
            var startLocation = GetLineAndColumn(safeText, clampedStart);
            var endLocation = GetLineAndColumn(safeText, clampedEnd);
            range.StartLine = startLocation.Line;
            range.StartColumn = startLocation.Column;
            range.EndLine = endLocation.Line;
            range.EndColumn = endLocation.Column;
            return range;
        }

        private static TextLocation GetLineAndColumn(string text, int index)
        {
            var line = 1;
            var column = 1;
            var safeText = text ?? string.Empty;
            var safeIndex = Math.Max(0, Math.Min(index, safeText.Length));
            for (var i = 0; i < safeIndex; i++)
            {
                if (safeText[i] == '\n')
                {
                    line++;
                    column = 1;
                }
                else if (safeText[i] != '\r')
                {
                    column++;
                }
            }

            return new TextLocation
            {
                Line = line,
                Column = column
            };
        }

        private static bool TryOrganizeTopLevelUsings(string text, out UsingDirectiveOrganizationResult result)
        {
            result = null;
            var original = text ?? string.Empty;
            if (original.Length == 0)
            {
                return false;
            }

            var usesCrLf = original.IndexOf("\r\n", StringComparison.Ordinal) >= 0;
            var normalized = original.Replace("\r\n", "\n");
            var lines = normalized.Split('\n');
            var blockStartLine = -1;
            var blockEndLine = -1;

            for (var i = 0; i < lines.Length; i++)
            {
                var trimmed = (lines[i] ?? string.Empty).Trim();
                if (blockStartLine < 0)
                {
                    if (trimmed.Length == 0 || IsDirectivePreambleLine(trimmed))
                    {
                        continue;
                    }

                    if (!IsUsingDirective(trimmed))
                    {
                        break;
                    }

                    blockStartLine = i;
                    blockEndLine = i + 1;
                    continue;
                }

                if (trimmed.Length == 0 || IsUsingDirective(trimmed))
                {
                    blockEndLine = i + 1;
                    continue;
                }

                break;
            }

            if (blockStartLine < 0 || blockEndLine <= blockStartLine)
            {
                return false;
            }

            var usingLines = new List<string>();
            for (var i = blockStartLine; i < blockEndLine; i++)
            {
                var trimmed = (lines[i] ?? string.Empty).Trim();
                if (IsUsingDirective(trimmed))
                {
                    usingLines.Add(trimmed);
                }
            }

            if (usingLines.Count == 0)
            {
                return false;
            }

            usingLines.Sort(StringComparer.OrdinalIgnoreCase);
            var distinctUsings = new List<string>();
            for (var i = 0; i < usingLines.Count; i++)
            {
                if (distinctUsings.Count == 0 ||
                    !string.Equals(distinctUsings[distinctUsings.Count - 1], usingLines[i], StringComparison.OrdinalIgnoreCase))
                {
                    distinctUsings.Add(usingLines[i]);
                }
            }

            var rebuilt = new List<string>();
            for (var i = 0; i < blockStartLine; i++)
            {
                rebuilt.Add(lines[i]);
            }

            for (var i = 0; i < distinctUsings.Count; i++)
            {
                rebuilt.Add(distinctUsings[i]);
            }

            var firstTrailingIndex = blockEndLine;
            while (firstTrailingIndex < lines.Length && string.IsNullOrEmpty(lines[firstTrailingIndex]))
            {
                firstTrailingIndex++;
            }

            if (distinctUsings.Count > 0 &&
                firstTrailingIndex < lines.Length &&
                rebuilt.Count > 0 &&
                !string.IsNullOrEmpty(rebuilt[rebuilt.Count - 1]))
            {
                rebuilt.Add(string.Empty);
            }

            for (var i = firstTrailingIndex; i < lines.Length; i++)
            {
                rebuilt.Add(lines[i]);
            }

            var updatedText = string.Join("\n", rebuilt.ToArray());
            if (usesCrLf)
            {
                updatedText = updatedText.Replace("\n", "\r\n");
            }

            var blockStartIndex = FindLineStartIndex(normalized, blockStartLine);
            var blockEndIndex = FindLineStartIndex(normalized, blockEndLine);
            if (blockEndIndex < blockStartIndex)
            {
                blockEndIndex = blockStartIndex;
            }

            var originalBlock = normalized.Substring(blockStartIndex, blockEndIndex - blockStartIndex);
            var updatedBlock = BuildUpdatedBlock(distinctUsings, lines, blockEndLine, lines.Length);
            if (usesCrLf)
            {
                originalBlock = originalBlock.Replace("\n", "\r\n");
                updatedBlock = updatedBlock.Replace("\n", "\r\n");
            }

            result = new UsingDirectiveOrganizationResult
            {
                UpdatedText = updatedText,
                OriginalBlock = originalBlock,
                UpdatedBlock = updatedBlock,
                BlockStart = blockStartIndex,
                BlockLength = blockEndIndex - blockStartIndex
            };
            return true;
        }

        private static string BuildUpdatedBlock(IList<string> distinctUsings, string[] lines, int blockEndLine, int totalLineCount)
        {
            var builder = new List<string>();
            for (var i = 0; i < distinctUsings.Count; i++)
            {
                builder.Add(distinctUsings[i]);
            }

            var firstTrailingIndex = blockEndLine;
            while (firstTrailingIndex < totalLineCount && string.IsNullOrEmpty(lines[firstTrailingIndex]))
            {
                firstTrailingIndex++;
            }

            if (distinctUsings.Count > 0 && firstTrailingIndex < totalLineCount)
            {
                builder.Add(string.Empty);
            }

            return string.Join("\n", builder.ToArray());
        }

        private static int FindLineStartIndex(string text, int lineIndex)
        {
            if (lineIndex <= 0)
            {
                return 0;
            }

            var currentLine = 0;
            for (var i = 0; i < text.Length; i++)
            {
                if (currentLine == lineIndex)
                {
                    return i;
                }

                if (text[i] == '\n')
                {
                    currentLine++;
                    if (currentLine == lineIndex)
                    {
                        return i + 1;
                    }
                }
            }

            return text.Length;
        }

        private static bool IsDirectivePreambleLine(string trimmed)
        {
            return trimmed.StartsWith("//", StringComparison.Ordinal) ||
                trimmed.StartsWith("#", StringComparison.Ordinal);
        }

        private static bool IsUsingDirective(string trimmed)
        {
            return !string.IsNullOrEmpty(trimmed) &&
                trimmed.StartsWith("using ", StringComparison.Ordinal) &&
                trimmed.EndsWith(";", StringComparison.Ordinal);
        }

        private sealed class TextLocation
        {
            public int Line;
            public int Column;
        }

        private sealed class UsingDirectiveOrganizationResult
        {
            public string UpdatedText = string.Empty;
            public string OriginalBlock = string.Empty;
            public string UpdatedBlock = string.Empty;
            public int BlockStart;
            public int BlockLength;
        }
    }
}
