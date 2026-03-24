using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;

namespace Cortex.Services
{
    internal sealed class EditorMethodTargetMetadataService
    {
        private const int MaxHeaderSearchLines = 8;

        private readonly EditorMethodTargetOutlineService _outlineService = new EditorMethodTargetOutlineService();
        private readonly EditorSemanticOperationService _semanticOperationService = new EditorSemanticOperationService();

        public void EnsureSymbolContextRequest(CortexShellState state, EditorCommandTarget target)
        {
            if (state == null || state.Semantic == null || target == null)
            {
                return;
            }

            if (HasMatchingSymbolContext(target, state.Semantic.ActiveSymbolContext))
            {
                return;
            }

            if (state.Semantic.RequestedKind == SemanticRequestKind.SymbolContext &&
                string.Equals(state.Semantic.RequestedDocumentPath ?? string.Empty, target.DocumentPath ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                state.Semantic.RequestedAbsolutePosition == target.AbsolutePosition)
            {
                return;
            }

            _semanticOperationService.QueueRequest(state, target, SemanticRequestKind.SymbolContext);
        }

        public void Enrich(EditorCommandTarget target, DocumentSession session, CortexShellState state)
        {
            if (target == null)
            {
                return;
            }

            ApplySymbolContext(target, state != null && state.Semantic != null ? state.Semantic.ActiveSymbolContext : null);
            if (NeedsSyntacticFallback(target))
            {
                ApplySyntacticFallback(target, session);
            }
        }

        private static bool NeedsSyntacticFallback(EditorCommandTarget target)
        {
            return target != null &&
                (string.IsNullOrEmpty(target.QualifiedSymbolDisplay) ||
                 string.IsNullOrEmpty(target.ContainingTypeName) ||
                 string.IsNullOrEmpty(target.SymbolKind) ||
                 string.IsNullOrEmpty(target.DefinitionDocumentPath));
        }

        private static bool HasMatchingSymbolContext(EditorCommandTarget target, LanguageServiceSymbolContextResponse response)
        {
            if (target == null || response == null || !response.Success)
            {
                return false;
            }

            if (!string.Equals(response.DocumentPath ?? string.Empty, target.DocumentPath ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (response.Range != null)
            {
                var start = Math.Max(0, response.Range.Start);
                var end = start + Math.Max(1, response.Range.Length);
                if (target.AbsolutePosition >= start && target.AbsolutePosition <= end)
                {
                    return true;
                }
            }

            if (!string.IsNullOrEmpty(response.MetadataName) &&
                string.Equals(NormalizeIdentifier(response.MetadataName), NormalizeIdentifier(target.SymbolText), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return !string.IsNullOrEmpty(response.SymbolDisplay) &&
                response.SymbolDisplay.IndexOf(target.SymbolText ?? string.Empty, StringComparison.Ordinal) >= 0;
        }

        private static void ApplySymbolContext(EditorCommandTarget target, LanguageServiceSymbolContextResponse response)
        {
            if (target == null || !HasMatchingSymbolContext(target, response))
            {
                return;
            }

            target.QualifiedSymbolDisplay = response.QualifiedSymbolDisplay ?? target.QualifiedSymbolDisplay ?? string.Empty;
            target.SymbolKind = response.SymbolKind ?? target.SymbolKind ?? string.Empty;
            target.MetadataName = response.MetadataName ?? target.MetadataName ?? string.Empty;
            target.ContainingTypeName = !string.IsNullOrEmpty(response.ContainingTypeName)
                ? GetSimpleTypeName(response.ContainingTypeName)
                : target.ContainingTypeName ?? string.Empty;
            target.ContainingAssemblyName = response.ContainingAssemblyName ?? target.ContainingAssemblyName ?? string.Empty;
            target.DocumentationCommentId = response.DocumentationCommentId ?? target.DocumentationCommentId ?? string.Empty;
            target.DefinitionDocumentPath = response.DefinitionDocumentPath ?? target.DefinitionDocumentPath ?? string.Empty;
            target.DefinitionStart = response.DefinitionRange != null ? response.DefinitionRange.Start : target.DefinitionStart;
            target.DefinitionLength = response.DefinitionRange != null ? response.DefinitionRange.Length : target.DefinitionLength;
            target.DefinitionLine = response.DefinitionRange != null ? response.DefinitionRange.StartLine : target.DefinitionLine;
            target.DefinitionColumn = response.DefinitionRange != null ? response.DefinitionRange.StartColumn : target.DefinitionColumn;
        }

        private void ApplySyntacticFallback(EditorCommandTarget target, DocumentSession session)
        {
            if (target == null || session == null || string.IsNullOrEmpty(session.Text))
            {
                return;
            }

            var lines = BuildLines(session.Text);
            if (lines.Length == 0)
            {
                return;
            }

            var outline = _outlineService.FindOutline(session, target);
            var declarationStartLine = outline != null ? Math.Max(1, outline.StartLineNumber) : Math.Max(1, target.Line);
            var declarationText = BuildDeclarationText(lines, declarationStartLine, outline != null ? outline.EndLineNumber : Math.Max(declarationStartLine, target.Line));
            var namespaceName = ResolveNamespace(session.Text, lines, target.AbsolutePosition);
            var typeName = ResolveContainingType(session.Text, lines, target.AbsolutePosition);

            if (string.IsNullOrEmpty(target.ContainingTypeName) && !string.IsNullOrEmpty(typeName))
            {
                target.ContainingTypeName = typeName;
            }

            if (string.IsNullOrEmpty(target.SymbolKind))
            {
                target.SymbolKind = "Method";
            }

            if (string.IsNullOrEmpty(target.MetadataName))
            {
                target.MetadataName = target.SymbolText ?? string.Empty;
            }

            if (string.IsNullOrEmpty(target.QualifiedSymbolDisplay))
            {
                var qualified = string.Empty;
                if (!string.IsNullOrEmpty(namespaceName))
                {
                    qualified = namespaceName;
                }

                if (!string.IsNullOrEmpty(typeName))
                {
                    qualified = !string.IsNullOrEmpty(qualified)
                        ? qualified + "." + typeName
                        : typeName;
                }

                if (!string.IsNullOrEmpty(declarationText))
                {
                    var signature = NormalizeWhitespace(declarationText);
                    if (!string.IsNullOrEmpty(qualified))
                    {
                        qualified += "." + signature;
                    }
                    else
                    {
                        qualified = signature;
                    }
                }

                target.QualifiedSymbolDisplay = qualified;
            }

            if (string.IsNullOrEmpty(target.DefinitionDocumentPath))
            {
                target.DefinitionDocumentPath = target.DocumentPath ?? string.Empty;
                target.DefinitionLine = declarationStartLine;
                target.DefinitionColumn = ResolveDefinitionColumn(lines, declarationStartLine);
            }
        }

        private static LineInfo[] BuildLines(string text)
        {
            var lines = new List<LineInfo>();
            var start = 0;
            var lineNumber = 1;
            while (start <= text.Length)
            {
                var end = start;
                while (end < text.Length && text[end] != '\r' && text[end] != '\n')
                {
                    end++;
                }

                lines.Add(new LineInfo
                {
                    LineNumber = lineNumber++,
                    StartOffset = start,
                    EndOffset = end,
                    RawText = text.Substring(start, end - start)
                });

                if (end >= text.Length)
                {
                    break;
                }

                if (text[end] == '\r' && end + 1 < text.Length && text[end + 1] == '\n')
                {
                    end++;
                }

                start = end + 1;
            }

            return lines.ToArray();
        }

        private static string BuildDeclarationText(LineInfo[] lines, int startLineNumber, int endLineNumber)
        {
            if (lines == null || lines.Length == 0)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            for (var lineNumber = startLineNumber; lineNumber <= endLineNumber && lineNumber <= lines.Length; lineNumber++)
            {
                var raw = lines[lineNumber - 1].RawText ?? string.Empty;
                var trimmed = raw.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("[", StringComparison.Ordinal))
                {
                    continue;
                }

                var braceIndex = trimmed.IndexOf('{');
                if (braceIndex >= 0)
                {
                    trimmed = trimmed.Substring(0, braceIndex).Trim();
                }

                if (string.IsNullOrEmpty(trimmed))
                {
                    continue;
                }

                parts.Add(trimmed);
                if (braceIndex >= 0 || trimmed.IndexOf("=>", StringComparison.Ordinal) >= 0 || trimmed.EndsWith(";", StringComparison.Ordinal))
                {
                    break;
                }
            }

            return string.Join(" ", parts.ToArray()).Trim();
        }

        private static string ResolveNamespace(string text, LineInfo[] lines, int absolutePosition)
        {
            var blocks = BuildDeclarationBlocks(text, lines, absolutePosition);
            for (var i = blocks.Count - 1; i >= 0; i--)
            {
                if (string.Equals(blocks[i].Kind, "namespace", StringComparison.Ordinal))
                {
                    return blocks[i].Name;
                }
            }

            if (!string.IsNullOrEmpty(text))
            {
                var safeLimit = Math.Max(0, Math.Min(absolutePosition, text.Length));
                var precedingText = text.Substring(0, safeLimit);
                var matches = Regex.Matches(precedingText, @"\bnamespace\s+([A-Za-z_][A-Za-z0-9_.]*)");
                if (matches != null && matches.Count > 0)
                {
                    var match = matches[matches.Count - 1];
                    if (match != null && match.Success && match.Groups.Count > 1)
                    {
                        return match.Groups[1].Value ?? string.Empty;
                    }
                }
            }

            return string.Empty;
        }

        private static string ResolveContainingType(string text, LineInfo[] lines, int absolutePosition)
        {
            var blocks = BuildDeclarationBlocks(text, lines, absolutePosition);
            for (var i = blocks.Count - 1; i >= 0; i--)
            {
                if (string.Equals(blocks[i].Kind, "type", StringComparison.Ordinal))
                {
                    return blocks[i].Name;
                }
            }

            return string.Empty;
        }

        private static List<DeclarationBlock> BuildDeclarationBlocks(string text, LineInfo[] lines, int absolutePosition)
        {
            var blocks = new List<DeclarationBlock>();
            if (string.IsNullOrEmpty(text) || lines == null || lines.Length == 0)
            {
                return blocks;
            }

            var sanitized = SanitizeTextForBraceScan(text);
            var braceStack = new Stack<DeclarationBlock>();
            var safeLimit = Math.Max(0, Math.Min(absolutePosition, sanitized.Length));
            for (var index = 0; index < safeLimit; index++)
            {
                var value = sanitized[index];
                if (value == '{')
                {
                    var header = TryExtractDeclarationHeader(lines, text, index);
                    var block = CreateDeclarationBlock(header);
                    braceStack.Push(block);
                    if (!string.IsNullOrEmpty(block.Kind))
                    {
                        blocks.Add(block);
                    }
                }
                else if (value == '}' && braceStack.Count > 0)
                {
                    var closed = braceStack.Pop();
                    if (!string.IsNullOrEmpty(closed.Kind))
                    {
                        for (var i = blocks.Count - 1; i >= 0; i--)
                        {
                            if (ReferenceEquals(blocks[i], closed))
                            {
                                blocks.RemoveAt(i);
                                break;
                            }
                        }
                    }
                }
            }

            return blocks;
        }

        private static string SanitizeTextForBraceScan(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var chars = text.ToCharArray();
            var inLineComment = false;
            var inBlockComment = false;
            var inString = false;
            var inVerbatimString = false;
            for (var i = 0; i < chars.Length; i++)
            {
                var current = chars[i];
                var next = i + 1 < chars.Length ? chars[i + 1] : '\0';
                if (inLineComment)
                {
                    if (current == '\n')
                    {
                        inLineComment = false;
                    }
                    else
                    {
                        chars[i] = ' ';
                    }

                    continue;
                }

                if (inBlockComment)
                {
                    if (current == '*' && next == '/')
                    {
                        chars[i] = ' ';
                        chars[i + 1] = ' ';
                        i++;
                        inBlockComment = false;
                    }
                    else if (current != '\n' && current != '\r')
                    {
                        chars[i] = ' ';
                    }

                    continue;
                }

                if (inVerbatimString)
                {
                    if (current == '"' && next == '"')
                    {
                        chars[i] = ' ';
                        chars[i + 1] = ' ';
                        i++;
                    }
                    else if (current == '"')
                    {
                        chars[i] = ' ';
                        inVerbatimString = false;
                    }
                    else if (current != '\n' && current != '\r')
                    {
                        chars[i] = ' ';
                    }

                    continue;
                }

                if (inString)
                {
                    if (current == '\\' && next != '\0')
                    {
                        chars[i] = ' ';
                        chars[i + 1] = ' ';
                        i++;
                    }
                    else if (current == '"')
                    {
                        chars[i] = ' ';
                        inString = false;
                    }
                    else if (current != '\n' && current != '\r')
                    {
                        chars[i] = ' ';
                    }

                    continue;
                }

                if (current == '/' && next == '/')
                {
                    chars[i] = ' ';
                    chars[i + 1] = ' ';
                    i++;
                    inLineComment = true;
                    continue;
                }

                if (current == '/' && next == '*')
                {
                    chars[i] = ' ';
                    chars[i + 1] = ' ';
                    i++;
                    inBlockComment = true;
                    continue;
                }

                if (current == '@' && next == '"')
                {
                    chars[i] = ' ';
                    chars[i + 1] = ' ';
                    i++;
                    inVerbatimString = true;
                    continue;
                }

                if (current == '"')
                {
                    chars[i] = ' ';
                    inString = true;
                }
            }

            return new string(chars);
        }

        private static string TryExtractDeclarationHeader(LineInfo[] lines, string text, int braceIndex)
        {
            if (lines == null || string.IsNullOrEmpty(text) || braceIndex < 0)
            {
                return string.Empty;
            }

            var lineIndex = FindLineIndex(lines, braceIndex);
            if (lineIndex < 0)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            for (var i = lineIndex; i >= 0 && parts.Count < MaxHeaderSearchLines; i--)
            {
                var trimmed = (lines[i].RawText ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(trimmed))
                {
                    if (parts.Count > 0)
                    {
                        break;
                    }

                    continue;
                }

                parts.Insert(0, trimmed);
                if (trimmed.EndsWith(";", StringComparison.Ordinal) ||
                    trimmed.EndsWith("}", StringComparison.Ordinal))
                {
                    parts.RemoveAt(0);
                    break;
                }
            }

            return NormalizeWhitespace(string.Join(" ", parts.ToArray()));
        }

        private static DeclarationBlock CreateDeclarationBlock(string header)
        {
            var block = new DeclarationBlock();
            if (string.IsNullOrEmpty(header))
            {
                return block;
            }

            var namespaceMatch = Regex.Match(header, @"\bnamespace\s+([A-Za-z_][A-Za-z0-9_.]*)");
            if (namespaceMatch.Success)
            {
                block.Kind = "namespace";
                block.Name = namespaceMatch.Groups[1].Value;
                return block;
            }

            var typeMatch = Regex.Match(header, @"\b(class|struct|interface|enum)\s+([A-Za-z_][A-Za-z0-9_]*)");
            if (typeMatch.Success)
            {
                block.Kind = "type";
                block.Name = typeMatch.Groups[2].Value;
            }

            return block;
        }

        private static int FindLineIndex(LineInfo[] lines, int absoluteOffset)
        {
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line != null && absoluteOffset >= line.StartOffset && absoluteOffset <= line.EndOffset)
                {
                    return i;
                }
            }

            return lines.Length - 1;
        }

        private static int ResolveDefinitionColumn(LineInfo[] lines, int lineNumber)
        {
            if (lines == null || lineNumber <= 0 || lineNumber > lines.Length)
            {
                return 1;
            }

            var text = lines[lineNumber - 1].RawText ?? string.Empty;
            for (var i = 0; i < text.Length; i++)
            {
                if (!char.IsWhiteSpace(text[i]))
                {
                    return i + 1;
                }
            }

            return 1;
        }

        private static string NormalizeWhitespace(string text)
        {
            return string.IsNullOrEmpty(text)
                ? string.Empty
                : Regex.Replace(text, @"\s+", " ").Trim();
        }

        private static string NormalizeIdentifier(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var lastDot = text.LastIndexOf('.');
            if (lastDot >= 0 && lastDot + 1 < text.Length)
            {
                text = text.Substring(lastDot + 1);
            }

            var genericTick = text.IndexOf('`');
            if (genericTick > 0)
            {
                text = text.Substring(0, genericTick);
            }

            return text.Trim();
        }

        private static string GetSimpleTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return string.Empty;
            }

            var lastDot = typeName.LastIndexOf('.');
            return lastDot >= 0 && lastDot + 1 < typeName.Length
                ? typeName.Substring(lastDot + 1)
                : typeName;
        }

        private sealed class LineInfo
        {
            public int LineNumber;
            public int StartOffset;
            public int EndOffset;
            public string RawText = string.Empty;
        }

        private sealed class DeclarationBlock
        {
            public string Kind = string.Empty;
            public string Name = string.Empty;
        }
    }
}
