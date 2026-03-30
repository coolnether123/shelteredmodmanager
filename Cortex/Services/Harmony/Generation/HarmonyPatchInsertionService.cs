using System;
using System.Collections.Generic;
using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.Modules.Shared;
using Cortex.Services.Harmony.Resolution;

namespace Cortex.Services.Harmony.Generation
{
    internal sealed class HarmonyPatchInsertionService
    {
        private readonly IEditorService _editorService = new EditorService();

        private struct InsertionSelection
        {
            public int Offset;
            public string ContextLabel;
        }

        private sealed class DeclarationBlock
        {
            public string Kind = string.Empty;
            public string Name = string.Empty;
            public int OpenBraceIndex;
            public int CloseBraceIndex;
            public int ParentIndex = -1;
        }

        public HarmonyPatchInsertionTarget[] BuildInsertionTargets(CortexShellState state, IProjectCatalog projectCatalog, HarmonyResolvedMethodTarget resolvedTarget, HarmonyPatchGenerationRequest request)
        {
            var targets = new List<HarmonyPatchInsertionTarget>();
            var project = resolvedTarget != null && resolvedTarget.Project != null
                ? resolvedTarget.Project
                : FindProject(projectCatalog, request != null ? request.TargetDocumentPath : string.Empty, request != null ? request.TargetAssemblyPath : string.Empty);
            var selectedSourcePath = request != null ? request.TargetDocumentPath ?? string.Empty : string.Empty;
            if (!string.IsNullOrEmpty(selectedSourcePath) && File.Exists(selectedSourcePath) && !CortexModuleUtil.IsDecompilerDocumentPath(state, selectedSourcePath))
            {
                targets.Add(new HarmonyPatchInsertionTarget
                {
                    FilePath = selectedSourcePath,
                    DisplayName = Path.GetFileName(selectedSourcePath),
                    IsNewFile = false,
                    IsWritable = true,
                    DefaultAnchorKind = HarmonyPatchInsertionAnchorKind.NamespaceOrClass,
                    SuggestedLine = GetSuggestedLine(selectedSourcePath, 1),
                    Reason = "Current source document"
                });
            }

            if (project != null && !string.IsNullOrEmpty(project.SourceRootPath))
            {
                var patchClassName = request != null ? request.PatchClassName ?? string.Empty : string.Empty;
                var harmonyDirectoryPath = Path.Combine(project.SourceRootPath, "Harmony");
                var newFilePath = Path.Combine(harmonyDirectoryPath, (string.IsNullOrEmpty(patchClassName) ? "GeneratedHarmonyPatch" : patchClassName) + ".cs");
                targets.Add(new HarmonyPatchInsertionTarget
                {
                    FilePath = newFilePath,
                    DisplayName = "New: " + Path.GetFileName(newFilePath),
                    IsNewFile = !File.Exists(newFilePath),
                    IsWritable = true,
                    DefaultAnchorKind = HarmonyPatchInsertionAnchorKind.EndOfFile,
                    SuggestedLine = GetSuggestedLine(newFilePath, 1),
                    Reason = "Dedicated Harmony patch file"
                });

                var existingFiles = SafeGetCsFiles(project.SourceRootPath);
                for (var i = 0; i < existingFiles.Length && targets.Count < 10; i++)
                {
                    var filePath = existingFiles[i];
                    if (string.IsNullOrEmpty(filePath) || string.Equals(filePath, selectedSourcePath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (IndexOfTarget(targets, filePath) >= 0)
                    {
                        continue;
                    }

                    var name = Path.GetFileName(filePath) ?? filePath;
                    if (name.IndexOf("Patch", StringComparison.OrdinalIgnoreCase) < 0 &&
                        name.IndexOf("Harmony", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    targets.Add(new HarmonyPatchInsertionTarget
                    {
                        FilePath = filePath,
                        DisplayName = name,
                        IsNewFile = false,
                        IsWritable = true,
                        DefaultAnchorKind = HarmonyPatchInsertionAnchorKind.EndOfFile,
                        SuggestedLine = GetSuggestedLine(filePath, 1),
                        Reason = "Existing patch-focused source file"
                    });
                }
            }

            return targets.ToArray();
        }

        public HarmonyPatchGenerationPreview BuildPreview(CortexShellState state, HarmonyPatchGenerationRequest request, HarmonyPatchGenerationPreview snippetPreview)
        {
            var preview = snippetPreview ?? new HarmonyPatchGenerationPreview();
            preview.StatusMessage = "Choose a destination file before applying the Harmony patch.";
            preview.CanApply = false;
            if (request == null || string.IsNullOrEmpty(request.DestinationFilePath))
            {
                return preview;
            }

            var destinationPath = NormalizePath(request.DestinationFilePath);
            if (string.IsNullOrEmpty(destinationPath))
            {
                preview.StatusMessage = "Destination file path was invalid.";
                MMLog.WriteWarning("[Cortex.Harmony] Preview build failed because the destination path was invalid.");
                return preview;
            }

            if (CortexModuleUtil.IsDecompilerDocumentPath(state, destinationPath))
            {
                preview.StatusMessage = "Decompiler cache files are read-only. Choose a writable source file instead.";
                MMLog.WriteWarning("[Cortex.Harmony] Preview build rejected a decompiled cache destination: " + destinationPath);
                return preview;
            }

            var currentText = ReadCurrentText(state, destinationPath);
            var insertionSelection = ResolveInsertionSelection(currentText, request);
            var insertionOffset = insertionSelection.Offset;
            var snippetText = PrepareSnippetText(currentText, insertionOffset, snippetPreview != null ? snippetPreview.SnippetText : string.Empty, out insertionOffset);
            var updatedText = (currentText ?? string.Empty).Insert(insertionOffset, snippetText);
            var insertionLine = CalculateLineNumber(updatedText, insertionOffset + CountLeadingNewLineCharacters(snippetText));

            preview.SnippetText = snippetText;
            preview.PreviewText = updatedText;
            preview.InsertionOffset = insertionOffset;
            preview.InsertionLine = insertionLine;
            preview.InsertionContextLabel = insertionSelection.ContextLabel ?? string.Empty;
            preview.Placeholders = OffsetPlaceholders(snippetPreview != null ? snippetPreview.Placeholders : null, insertionOffset);
            preview.StatusMessage = "Preview ready for " + Path.GetFileName(destinationPath) +
                (!string.IsNullOrEmpty(preview.InsertionContextLabel) ? " at " + preview.InsertionContextLabel + "." : ".");
            preview.CanApply = true;
            return preview;
        }

        public bool ApplyPreview(CortexShellState state, IDocumentService documentService, HarmonyPatchGenerationRequest request, HarmonyPatchGenerationPreview preview, out DocumentSession session, out string statusMessage)
        {
            session = null;
            statusMessage = "Harmony patch preview was not ready to apply.";
            if (state == null || documentService == null || request == null || preview == null || !preview.CanApply || string.IsNullOrEmpty(request.DestinationFilePath))
            {
                return false;
            }

            var destinationPath = NormalizePath(request.DestinationFilePath);
            if (string.IsNullOrEmpty(destinationPath) || CortexModuleUtil.IsDecompilerDocumentPath(state, destinationPath))
            {
                statusMessage = "Harmony generation must target a writable source file.";
                MMLog.WriteWarning("[Cortex.Harmony] Apply rejected because the destination was not a writable source file. Destination='" + (request.DestinationFilePath ?? string.Empty) + "'.");
                return false;
            }

            session = ResolveOrCreateSession(state, documentService, destinationPath, preview.InsertionLine);
            if (session == null)
            {
                statusMessage = "Cortex could not open the destination file for insertion.";
                MMLog.WriteWarning("[Cortex.Harmony] Apply failed because Cortex could not open the destination file. Destination='" + destinationPath + "'.");
                return false;
            }

            var originalText = session.Text ?? string.Empty;
            var originalSnapshot = session.OriginalTextSnapshot ?? string.Empty;
            var originalVersion = session.TextVersion;
            var originalDirty = session.IsDirty;
            var originalLastMutationUtc = session.LastTextMutationUtc;
            var originalHasExternalChanges = session.HasExternalChanges;
            session.Kind = DocumentKind.SourceCode;
            session.IsReadOnly = false;
            if (!_editorService.SetText(session, preview.PreviewText ?? string.Empty))
            {
                statusMessage = "The destination document could not be updated in the editor pipeline.";
                MMLog.WriteWarning("[Cortex.Harmony] Apply failed because the editor pipeline rejected the generated text. Destination='" + destinationPath + "'.");
                return false;
            }

            if (!documentService.Save(session))
            {
                RestoreSession(session, originalText, originalSnapshot, originalVersion, originalDirty, originalLastMutationUtc, originalHasExternalChanges);
                statusMessage = "Saving the generated Harmony patch was blocked by a scoped save conflict.";
                MMLog.WriteWarning("[Cortex.Harmony] Apply failed because saving was blocked by a scoped save conflict. Destination='" + destinationPath + "'.");
                return false;
            }

            session.HighlightedLine = preview.InsertionLine > 0 ? preview.InsertionLine : 1;
            state.Documents.ActiveDocument = session;
            state.Documents.ActiveDocumentPath = session.FilePath ?? string.Empty;
            state.Workbench.RequestedContainerId = CortexWorkbenchIds.EditorContainer;
            statusMessage = "Inserted Harmony patch into " + Path.GetFileName(session.FilePath) + ".";
            MMLog.WriteInfo("[Cortex.Harmony] Inserted generated patch into '" + (session.FilePath ?? string.Empty) +
                "'. Line=" + session.HighlightedLine + ", Placeholders=" + (preview.Placeholders != null ? preview.Placeholders.Length : 0) + ".");
            return true;
        }

        private DocumentSession ResolveOrCreateSession(CortexShellState state, IDocumentService documentService, string filePath, int highlightedLine)
        {
            var existing = CortexModuleUtil.FindOpenDocument(state, filePath);
            if (existing != null)
            {
                existing.Kind = DocumentKind.SourceCode;
                existing.IsReadOnly = false;
                return existing;
            }

            if (File.Exists(filePath))
            {
                return CortexModuleUtil.OpenDocument(documentService, state, filePath, highlightedLine, DocumentKind.SourceCode);
            }

            var fullPath = Path.GetFullPath(filePath);
            var directory = Path.GetDirectoryName(fullPath) ?? string.Empty;
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var session = new DocumentSession();
            session.FilePath = fullPath;
            session.Kind = DocumentKind.SourceCode;
            session.IsReadOnly = false;
            session.Text = string.Empty;
            session.OriginalTextSnapshot = string.Empty;
            session.TextVersion = 1;
            session.LastKnownWriteUtc = DateTime.MinValue;
            session.LastTextMutationUtc = DateTime.UtcNow;
            session.EditorState = new EditorDocumentState();
            session.HighlightedLine = highlightedLine;
            state.Documents.OpenDocuments.Add(session);
            state.Documents.ActiveDocument = session;
            state.Documents.ActiveDocumentPath = fullPath;
            return session;
        }

        private static string ReadCurrentText(CortexShellState state, string filePath)
        {
            var session = CortexModuleUtil.FindOpenDocument(state, filePath);
            if (session != null)
            {
                return session.Text ?? string.Empty;
            }

            try
            {
                return File.Exists(filePath) ? File.ReadAllText(filePath) : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static InsertionSelection ResolveInsertionSelection(string text, HarmonyPatchGenerationRequest request)
        {
            var safeText = text ?? string.Empty;
            switch (request != null ? request.InsertionAnchorKind : HarmonyPatchInsertionAnchorKind.EndOfFile)
            {
                case HarmonyPatchInsertionAnchorKind.ExplicitLine:
                    return new InsertionSelection
                    {
                        Offset = GetOffsetForLine(safeText, request != null ? request.InsertionLine : 0),
                        ContextLabel = request != null && request.InsertionLine > 0 ? "line " + request.InsertionLine : string.Empty
                    };
                case HarmonyPatchInsertionAnchorKind.SelectedContext:
                    return ResolveSelectedContextInsertion(safeText, request);
                case HarmonyPatchInsertionAnchorKind.NamespaceOrClass:
                    return new InsertionSelection
                    {
                        Offset = FindNamespaceOrClassInsertionOffset(safeText),
                        ContextLabel = "the nearest namespace/class slot"
                    };
                case HarmonyPatchInsertionAnchorKind.EndOfFile:
                default:
                    return new InsertionSelection
                    {
                        Offset = safeText.Length,
                        ContextLabel = "end of file"
                    };
            }
        }

        private static int GetOffsetForLine(string text, int lineNumber)
        {
            if (string.IsNullOrEmpty(text) || lineNumber <= 1)
            {
                return 0;
            }

            var currentLine = 1;
            for (var i = 0; i < text.Length; i++)
            {
                if (currentLine >= lineNumber)
                {
                    return i;
                }

                if (text[i] == '\n')
                {
                    currentLine++;
                }
            }

            return text.Length;
        }

        private static int FindNamespaceOrClassInsertionOffset(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            var namespaceIndex = text.IndexOf("namespace ", StringComparison.Ordinal);
            if (namespaceIndex >= 0)
            {
                var braceIndex = text.IndexOf('{', namespaceIndex);
                if (braceIndex >= 0)
                {
                    var closingBrace = FindMatchingBrace(text, braceIndex);
                    if (closingBrace > braceIndex)
                    {
                        return closingBrace;
                    }
                }
            }

            var classIndex = text.IndexOf("class ", StringComparison.Ordinal);
            if (classIndex >= 0)
            {
                return text.Length;
            }

            return text.Length;
        }

        private static InsertionSelection ResolveSelectedContextInsertion(string text, HarmonyPatchGenerationRequest request)
        {
            var safeText = text ?? string.Empty;
            if (request == null)
            {
                return new InsertionSelection
                {
                    Offset = safeText.Length,
                    ContextLabel = "end of file"
                };
            }

            var selectedOffset = Math.Max(0, Math.Min(request.InsertionAbsolutePosition, safeText.Length));
            if (selectedOffset <= 0 || string.IsNullOrEmpty(safeText))
            {
                return new InsertionSelection
                {
                    Offset = GetOffsetForLine(safeText, request.InsertionLine),
                    ContextLabel = request.InsertionLine > 0 ? "line " + request.InsertionLine : "selected editor slot"
                };
            }

            var blocks = BuildDeclarationBlocks(safeText);
            var fileScopeTypeIndex = FindOutermostContainingTypeBlock(blocks, selectedOffset);
            if (fileScopeTypeIndex >= 0)
            {
                var typeBlock = blocks[fileScopeTypeIndex];
                return new InsertionSelection
                {
                    Offset = typeBlock.CloseBraceIndex >= 0 ? Math.Min(typeBlock.CloseBraceIndex + 1, safeText.Length) : safeText.Length,
                    ContextLabel = "after " + FormatDeclarationLabel(typeBlock)
                };
            }

            var namespaceIndex = FindContainingBlock(blocks, selectedOffset, "namespace");
            if (namespaceIndex >= 0)
            {
                return new InsertionSelection
                {
                    Offset = GetOffsetForLine(safeText, request.InsertionLine),
                    ContextLabel = "the selected namespace slot"
                };
            }

            return new InsertionSelection
            {
                Offset = GetOffsetForLine(safeText, request.InsertionLine),
                ContextLabel = request.InsertionLine > 0 ? "line " + request.InsertionLine : "selected editor slot"
            };
        }

        private static string PrepareSnippetText(string currentText, int insertionOffset, string snippetText, out int adjustedOffset)
        {
            adjustedOffset = insertionOffset;
            var snippet = snippetText ?? string.Empty;
            var safeText = currentText ?? string.Empty;
            if (safeText.Length > 0 && adjustedOffset > 0)
            {
                var previous = safeText[adjustedOffset - 1];
                if (previous != '\n' && previous != '\r')
                {
                    snippet = Environment.NewLine + Environment.NewLine + snippet;
                }
                else if (!snippet.StartsWith(Environment.NewLine, StringComparison.Ordinal) && !snippet.StartsWith("\n", StringComparison.Ordinal))
                {
                    snippet = Environment.NewLine + snippet;
                }
            }

            if (safeText.Length > adjustedOffset)
            {
                var next = safeText[adjustedOffset];
                if (next != '\n' && next != '\r' && !snippet.EndsWith(Environment.NewLine, StringComparison.Ordinal))
                {
                    snippet += Environment.NewLine + Environment.NewLine;
                }
            }

            return snippet;
        }

        private static int CalculateLineNumber(string text, int insertionOffset)
        {
            if (string.IsNullOrEmpty(text) || insertionOffset <= 0)
            {
                return 1;
            }

            var line = 1;
            for (var i = 0; i < text.Length && i < insertionOffset; i++)
            {
                if (text[i] == '\n')
                {
                    line++;
                }
            }

            return line;
        }

        private static int CountLeadingNewLineCharacters(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            var index = 0;
            while (index < text.Length && (text[index] == '\r' || text[index] == '\n'))
            {
                index++;
            }

            return index;
        }

        private static GeneratedTemplatePlaceholder[] OffsetPlaceholders(GeneratedTemplatePlaceholder[] placeholders, int insertionOffset)
        {
            if (placeholders == null || placeholders.Length == 0)
            {
                return new GeneratedTemplatePlaceholder[0];
            }

            var results = new GeneratedTemplatePlaceholder[placeholders.Length];
            for (var i = 0; i < placeholders.Length; i++)
            {
                var current = placeholders[i];
                results[i] = current == null
                    ? null
                    : new GeneratedTemplatePlaceholder
                    {
                        PlaceholderId = current.PlaceholderId,
                        Start = insertionOffset + current.Start,
                        Length = current.Length,
                        DefaultText = current.DefaultText,
                        Description = current.Description
                    };
            }

            return results;
        }

        private static CortexProjectDefinition FindProject(IProjectCatalog projectCatalog, string documentPath, string assemblyPath)
        {
            if (projectCatalog == null)
            {
                return null;
            }

            var projects = projectCatalog.GetProjects();
            for (var i = 0; i < projects.Count; i++)
            {
                var project = projects[i];
                if (project == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(documentPath) && PathStartsWith(documentPath, project.SourceRootPath))
                {
                    return project;
                }

                if (!string.IsNullOrEmpty(assemblyPath) &&
                    !string.IsNullOrEmpty(project.OutputAssemblyPath) &&
                    string.Equals(Path.GetFullPath(project.OutputAssemblyPath), Path.GetFullPath(assemblyPath), StringComparison.OrdinalIgnoreCase))
                {
                    return project;
                }
            }

            return null;
        }

        private static bool PathStartsWith(string path, string rootPath)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(rootPath))
            {
                return false;
            }

            try
            {
                var normalizedPath = Path.GetFullPath(path);
                var normalizedRoot = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static int IndexOfTarget(List<HarmonyPatchInsertionTarget> targets, string filePath)
        {
            if (targets == null || string.IsNullOrEmpty(filePath))
            {
                return -1;
            }

            for (var i = 0; i < targets.Count; i++)
            {
                var current = targets[i];
                if (current != null && string.Equals(current.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static string[] SafeGetCsFiles(string rootPath)
        {
            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
            {
                return new string[0];
            }

            try
            {
                var files = Directory.GetFiles(rootPath, "*.cs", SearchOption.AllDirectories);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                return files;
            }
            catch
            {
                return new string[0];
            }
        }

        private static int GetSuggestedLine(string filePath, int fallback)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    return fallback;
                }

                return Math.Max(1, File.ReadAllLines(filePath).Length + 1);
            }
            catch
            {
                return fallback;
            }
        }

        private static int FindMatchingBrace(string text, int openBraceIndex)
        {
            if (string.IsNullOrEmpty(text) || openBraceIndex < 0 || openBraceIndex >= text.Length || text[openBraceIndex] != '{')
            {
                return -1;
            }

            var depth = 0;
            for (var i = openBraceIndex; i < text.Length; i++)
            {
                if (text[i] == '{')
                {
                    depth++;
                }
                else if (text[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        private static List<DeclarationBlock> BuildDeclarationBlocks(string text)
        {
            var blocks = new List<DeclarationBlock>();
            if (string.IsNullOrEmpty(text))
            {
                return blocks;
            }

            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] != '{')
                {
                    continue;
                }

                string kind;
                string name;
                if (!TryClassifyDeclaration(text, i, out kind, out name))
                {
                    continue;
                }

                var closeBraceIndex = FindMatchingBrace(text, i);
                if (closeBraceIndex <= i)
                {
                    continue;
                }

                blocks.Add(new DeclarationBlock
                {
                    Kind = kind,
                    Name = name,
                    OpenBraceIndex = i,
                    CloseBraceIndex = closeBraceIndex
                });
            }

            for (var i = 0; i < blocks.Count; i++)
            {
                var current = blocks[i];
                var parentIndex = -1;
                var parentSpan = int.MaxValue;
                for (var candidateIndex = 0; candidateIndex < blocks.Count; candidateIndex++)
                {
                    if (candidateIndex == i)
                    {
                        continue;
                    }

                    var candidate = blocks[candidateIndex];
                    if (candidate.OpenBraceIndex >= current.OpenBraceIndex || candidate.CloseBraceIndex <= current.CloseBraceIndex)
                    {
                        continue;
                    }

                    var span = candidate.CloseBraceIndex - candidate.OpenBraceIndex;
                    if (span < parentSpan)
                    {
                        parentSpan = span;
                        parentIndex = candidateIndex;
                    }
                }

                current.ParentIndex = parentIndex;
            }

            return blocks;
        }

        private static int FindOutermostContainingTypeBlock(List<DeclarationBlock> blocks, int absolutePosition)
        {
            var typeIndex = FindContainingTypeBlock(blocks, absolutePosition);
            if (typeIndex < 0)
            {
                return -1;
            }

            var currentIndex = typeIndex;
            while (currentIndex >= 0)
            {
                var parentIndex = blocks[currentIndex].ParentIndex;
                if (parentIndex < 0 || !IsTypeDeclaration(blocks[parentIndex]))
                {
                    return currentIndex;
                }

                currentIndex = parentIndex;
            }

            return typeIndex;
        }

        private static int FindContainingTypeBlock(List<DeclarationBlock> blocks, int absolutePosition)
        {
            if (blocks == null || blocks.Count == 0)
            {
                return -1;
            }

            var matchIndex = -1;
            var matchSpan = int.MaxValue;
            for (var i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                if (!IsTypeDeclaration(block) ||
                    absolutePosition <= block.OpenBraceIndex ||
                    absolutePosition >= block.CloseBraceIndex)
                {
                    continue;
                }

                var span = block.CloseBraceIndex - block.OpenBraceIndex;
                if (span < matchSpan)
                {
                    matchSpan = span;
                    matchIndex = i;
                }
            }

            return matchIndex;
        }

        private static int FindContainingBlock(List<DeclarationBlock> blocks, int absolutePosition, string kind)
        {
            if (blocks == null || blocks.Count == 0 || string.IsNullOrEmpty(kind))
            {
                return -1;
            }

            var matchIndex = -1;
            var matchSpan = int.MaxValue;
            for (var i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                if (!string.Equals(block.Kind, kind, StringComparison.OrdinalIgnoreCase) ||
                    absolutePosition <= block.OpenBraceIndex ||
                    absolutePosition >= block.CloseBraceIndex)
                {
                    continue;
                }

                var span = block.CloseBraceIndex - block.OpenBraceIndex;
                if (span < matchSpan)
                {
                    matchSpan = span;
                    matchIndex = i;
                }
            }

            return matchIndex;
        }

        private static bool IsTypeDeclaration(DeclarationBlock block)
        {
            if (block == null)
            {
                return false;
            }

            return string.Equals(block.Kind, "class", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(block.Kind, "struct", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(block.Kind, "interface", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(block.Kind, "record", StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatDeclarationLabel(DeclarationBlock block)
        {
            if (block == null)
            {
                return "selected slot";
            }

            if (string.IsNullOrEmpty(block.Name))
            {
                return block.Kind;
            }

            return block.Kind + " " + block.Name;
        }

        private static bool TryClassifyDeclaration(string text, int openBraceIndex, out string kind, out string name)
        {
            kind = string.Empty;
            name = string.Empty;
            if (string.IsNullOrEmpty(text) || openBraceIndex <= 0 || openBraceIndex >= text.Length)
            {
                return false;
            }

            var start = Math.Max(0, openBraceIndex - 320);
            var header = text.Substring(start, openBraceIndex - start);
            var lastDelimiter = FindLastDeclarationDelimiter(header);
            if (lastDelimiter >= 0 && lastDelimiter + 1 < header.Length)
            {
                header = header.Substring(lastDelimiter + 1);
            }

            header = NormalizeWhitespace(header);
            if (string.IsNullOrEmpty(header))
            {
                return false;
            }

            if (TryExtractDeclaration(header, "namespace", out name))
            {
                kind = "namespace";
                return true;
            }

            if (TryExtractDeclaration(header, "class", out name))
            {
                kind = "class";
                return true;
            }

            if (TryExtractDeclaration(header, "struct", out name))
            {
                kind = "struct";
                return true;
            }

            if (TryExtractDeclaration(header, "interface", out name))
            {
                kind = "interface";
                return true;
            }

            if (TryExtractDeclaration(header, "record", out name))
            {
                kind = "record";
                return true;
            }

            return false;
        }

        private static int FindLastDeclarationDelimiter(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return -1;
            }

            for (var i = text.Length - 1; i >= 0; i--)
            {
                var current = text[i];
                if (current == ';' || current == '{' || current == '}')
                {
                    return i;
                }
            }

            return -1;
        }

        private static string NormalizeWhitespace(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var builder = new System.Text.StringBuilder(text.Length);
            var previousWasWhitespace = false;
            for (var i = 0; i < text.Length; i++)
            {
                var current = text[i];
                if (char.IsWhiteSpace(current))
                {
                    if (previousWasWhitespace)
                    {
                        continue;
                    }

                    builder.Append(' ');
                    previousWasWhitespace = true;
                    continue;
                }

                builder.Append(current);
                previousWasWhitespace = false;
            }

            return builder.ToString().Trim();
        }

        private static bool TryExtractDeclaration(string header, string keyword, out string name)
        {
            name = string.Empty;
            if (string.IsNullOrEmpty(header) || string.IsNullOrEmpty(keyword))
            {
                return false;
            }

            var marker = keyword + " ";
            var index = header.LastIndexOf(marker, StringComparison.Ordinal);
            if (index < 0)
            {
                return false;
            }

            var start = index + marker.Length;
            if (start >= header.Length)
            {
                return false;
            }

            if (string.Equals(keyword, "record", StringComparison.Ordinal) &&
                (header.IndexOf("record class ", StringComparison.Ordinal) >= 0 || header.IndexOf("record struct ", StringComparison.Ordinal) >= 0))
            {
                var classIndex = header.LastIndexOf("record class ", StringComparison.Ordinal);
                var structIndex = header.LastIndexOf("record struct ", StringComparison.Ordinal);
                index = Math.Max(classIndex, structIndex);
                if (index >= 0)
                {
                    start = index + "record ".Length;
                    while (start < header.Length && !char.IsWhiteSpace(header[start]))
                    {
                        start++;
                    }

                    while (start < header.Length && char.IsWhiteSpace(header[start]))
                    {
                        start++;
                    }
                }
            }

            var end = start;
            while (end < header.Length)
            {
                var current = header[end];
                if (char.IsWhiteSpace(current) || current == ':' || current == '<' || current == '(')
                {
                    break;
                }

                end++;
            }

            if (end <= start)
            {
                return false;
            }

            name = header.Substring(start, end - start).Trim();
            return !string.IsNullOrEmpty(name);
        }

        private static string NormalizePath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(filePath);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void RestoreSession(DocumentSession session, string originalText, string originalSnapshot, int originalVersion, bool originalDirty, DateTime originalLastMutationUtc, bool originalHasExternalChanges)
        {
            if (session == null)
            {
                return;
            }

            session.Text = originalText ?? string.Empty;
            session.OriginalTextSnapshot = originalSnapshot ?? string.Empty;
            session.TextVersion = originalVersion;
            session.IsDirty = originalDirty;
            session.LastTextMutationUtc = originalLastMutationUtc;
            session.HasExternalChanges = originalHasExternalChanges;
        }
    }
}
