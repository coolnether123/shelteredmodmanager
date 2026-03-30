using System;
using System.Collections.Generic;
using Cortex.Core.Models;
using Cortex.Presentation.Models;
using Cortex.Services.Inspector.Actions;
using Cortex.Services.Inspector.Relationships;

namespace Cortex.Services.Inspector
{
    internal sealed class EditorMethodInspectorHostViewComposer
    {
        private const int SnippetContextLineCount = 2;

        private readonly IEditorMethodInspectorNavigationActionFactory _relationshipActionFactory;

        public EditorMethodInspectorHostViewComposer(IEditorMethodInspectorNavigationActionFactory relationshipActionFactory)
        {
            _relationshipActionFactory = relationshipActionFactory;
        }

        public MethodInspectorViewModel Compose(
            CortexMethodInspectorState inspector,
            DocumentSession session,
            EditorCommandTarget target,
            EditorMethodRelationshipsContext relationshipsContext,
            MethodInspectorSectionViewModel[] contributedSections)
        {
            var sections = new List<MethodInspectorSectionViewModel>();
            sections.Add(BuildStructureSection(inspector, target));
            sections.Add(BuildRelationshipsSection(inspector, relationshipsContext));
            if (contributedSections != null)
            {
                for (var i = 0; i < contributedSections.Length; i++)
                {
                    if (contributedSections[i] != null)
                    {
                        sections.Add(contributedSections[i]);
                    }
                }
            }

            sections.Add(BuildSourceSection(inspector, session, target));

            return new MethodInspectorViewModel
            {
                Title = "Method Info: " + (inspector != null ? inspector.Title ?? string.Empty : string.Empty),
                Subtitle = BuildHeaderSubtitle(target),
                Sections = sections.ToArray()
            };
        }

        private static MethodInspectorSectionViewModel BuildStructureSection(CortexMethodInspectorState inspector, EditorCommandTarget target)
        {
            var section = new MethodInspectorSectionViewModel();
            section.Id = "structure";
            section.Title = "Structure";
            section.Expanded = inspector != null ? inspector.OverviewExpanded : true;

            var elements = new List<MethodInspectorElementViewModel>();
            if (target == null)
            {
                elements.Add(CreateTextElement(string.Empty, "No method target is selected.", false));
            }
            else
            {
                elements.Add(CreateMetadataElement("Namespace", ExtractNamespace(target)));
                elements.Add(CreateMetadataElement("Type", target.ContainingTypeName));
                elements.Add(CreateMetadataElement("Member", target.SymbolText));
                elements.Add(CreateMetadataElement("Kind", !string.IsNullOrEmpty(target.SymbolKind) ? target.SymbolKind : "Method"));
                elements.Add(CreateMetadataElement("Classification", inspector != null ? inspector.Classification : string.Empty));
                elements.Add(CreateMetadataElement("Assembly", target.ContainingAssemblyName));
                elements.Add(CreateMetadataElement("Document", target.DocumentPath));
                elements.Add(CreateMetadataElement("Position", "Line " + Math.Max(1, target.Line) + ", Column " + Math.Max(1, target.Column)));

                var signature = BuildSignature(target);
                if (!string.IsNullOrEmpty(signature))
                {
                    elements.Add(new MethodInspectorSpacerViewModel { Height = 2f });
                    elements.Add(CreateTextElement("Signature", signature, true));
                }

                if (!string.IsNullOrEmpty(target.HoverText))
                {
                    elements.Add(CreateTextElement("Inspector Notes", target.HoverText, true));
                }
            }

            section.Elements = elements.ToArray();
            return section;
        }

        private MethodInspectorSectionViewModel BuildRelationshipsSection(CortexMethodInspectorState inspector, EditorMethodRelationshipsContext relationshipsContext)
        {
            var section = new MethodInspectorSectionViewModel();
            section.Id = "relationships";
            section.Title = "Relationships";
            section.Expanded = inspector != null ? inspector.RelationshipsExpanded : false;

            var elements = new List<MethodInspectorElementViewModel>();
            if (relationshipsContext == null)
            {
                elements.Add(CreateTextElement(string.Empty, "Method relationships are not available.", false));
            }
            else if (!relationshipsContext.IsExpanded)
            {
                elements.Add(CreateTextElement(string.Empty, relationshipsContext.StatusMessage, false));
            }
            else if (relationshipsContext.IsLoading || !relationshipsContext.HasResponse)
            {
                elements.Add(CreateTextElement(string.Empty, relationshipsContext.StatusMessage, false));
            }
            else
            {
                elements.Add(CreateMetadataElement("Depends On", relationshipsContext.OutgoingCallCount.ToString()));
                elements.Add(CreateMetadataElement("Used By", relationshipsContext.IncomingCallCount.ToString()));
                AppendRelationshipGroup(elements, "Depends On", relationshipsContext.OutgoingCalls, "This method does not call any resolved symbols.");
                elements.Add(new MethodInspectorSpacerViewModel { Height = 4f });
                AppendRelationshipGroup(elements, "Used By", relationshipsContext.IncomingCalls, "No incoming callers were found for this method.");
            }

            section.Elements = elements.ToArray();
            return section;
        }

        private void AppendRelationshipGroup(
            List<MethodInspectorElementViewModel> elements,
            string title,
            EditorMethodRelationshipItem[] items,
            string emptyMessage)
        {
            elements.Add(CreateTextElement(title, string.Empty, false));
            var safeItems = items ?? new EditorMethodRelationshipItem[0];
            if (safeItems.Length == 0)
            {
                elements.Add(CreateTextElement(string.Empty, emptyMessage, false));
                return;
            }

            var rendered = 0;
            for (var i = 0; i < safeItems.Length && rendered < 4; i++)
            {
                var item = safeItems[i];
                if (item == null)
                {
                    continue;
                }

                rendered++;
                var rows = new List<MethodInspectorMetadataViewModel>();
                if (!string.IsNullOrEmpty(item.Detail))
                {
                    rows.Add(CreateMetadataElement("Context", item.Detail));
                }

                if (!string.IsNullOrEmpty(item.ContainingTypeName))
                {
                    rows.Add(CreateMetadataElement("Type", item.ContainingTypeName));
                }

                rows.Add(CreateMetadataElement("Relationship", (item.Relationship ?? "Call") + " (" + item.CallCount + ")"));
                if (!string.IsNullOrEmpty(item.ContainingAssemblyName))
                {
                    rows.Add(CreateMetadataElement("Assembly", item.ContainingAssemblyName));
                }

                elements.Add(CreateCardElement(
                    item.Title ?? "Unknown",
                    rows.ToArray(),
                    string.Empty,
                    item.Actions != null && item.Actions.Length > 0
                        ? item.Actions
                        : (_relationshipActionFactory != null ? _relationshipActionFactory.CreateRelationshipActions(item) : new MethodInspectorActionViewModel[0])));
            }

            if (safeItems.Length > rendered)
            {
                elements.Add(CreateTextElement(string.Empty, "Additional relationship entries are available beyond this preview.", false));
            }
        }

        private static MethodInspectorSectionViewModel BuildSourceSection(CortexMethodInspectorState inspector, DocumentSession session, EditorCommandTarget target)
        {
            var section = new MethodInspectorSectionViewModel();
            section.Id = "source";
            section.Title = "Source Context";
            section.Expanded = inspector != null ? inspector.NavigationExpanded : true;

            var elements = new List<MethodInspectorElementViewModel>();
            if (session == null || target == null || string.IsNullOrEmpty(session.Text))
            {
                elements.Add(CreateTextElement(string.Empty, "Source context is not available for the selected method.", false));
            }
            else
            {
                elements.Add(CreateMetadataElement("Definition", BuildDefinitionText(target)));
                elements.Add(CreateMetadataElement("Fit", BuildFitText(session, target)));
                var snippet = BuildSnippet(session, target.Line, SnippetContextLineCount);
                if (string.IsNullOrEmpty(snippet))
                {
                    elements.Add(CreateTextElement(string.Empty, "No local source snippet could be built for this method.", false));
                }
                else
                {
                    elements.Add(CreateTextElement("Nearby Code", snippet, true));
                }
            }

            section.Elements = elements.ToArray();
            return section;
        }

        private static MethodInspectorMetadataViewModel CreateMetadataElement(string label, string value)
        {
            return CreateMetadataElement(label, value, true);
        }

        private static MethodInspectorMetadataViewModel CreateMetadataElement(string label, string value, bool drawDivider)
        {
            return new MethodInspectorMetadataViewModel
            {
                Label = label ?? string.Empty,
                Value = !string.IsNullOrEmpty(value) ? value : "Unknown",
                DrawDivider = drawDivider
            };
        }

        private static MethodInspectorTextViewModel CreateTextElement(string label, string value, bool monospace)
        {
            return new MethodInspectorTextViewModel
            {
                Label = label ?? string.Empty,
                Value = value ?? string.Empty,
                Monospace = monospace
            };
        }

        private static MethodInspectorCardViewModel CreateCardElement(string title, MethodInspectorMetadataViewModel[] rows, string body, MethodInspectorActionViewModel[] actions)
        {
            return new MethodInspectorCardViewModel
            {
                Title = title ?? string.Empty,
                Rows = rows ?? new MethodInspectorMetadataViewModel[0],
                Body = body ?? string.Empty,
                Actions = actions ?? new MethodInspectorActionViewModel[0]
            };
        }

        private static string BuildHeaderSubtitle(EditorCommandTarget target)
        {
            if (target == null)
            {
                return string.Empty;
            }

            var namespaceName = ExtractNamespace(target);
            var typeName = target.ContainingTypeName ?? string.Empty;
            if (string.IsNullOrEmpty(namespaceName))
            {
                return typeName;
            }

            return string.IsNullOrEmpty(typeName)
                ? namespaceName
                : namespaceName + "  |  " + typeName;
        }

        private static string BuildSignature(EditorCommandTarget target)
        {
            if (target == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(target.QualifiedSymbolDisplay))
            {
                return target.QualifiedSymbolDisplay;
            }

            return target.SymbolText ?? string.Empty;
        }

        private static string BuildFitText(DocumentSession session, EditorCommandTarget target)
        {
            if (session == null || target == null)
            {
                return string.Empty;
            }

            var lines = SplitLines(session.Text);
            if (target.Line <= 0 || target.Line > lines.Length)
            {
                return "Method line is outside the loaded text snapshot.";
            }

            var previous = target.Line > 1 ? TrimForSummary(lines[target.Line - 2]) : string.Empty;
            var current = TrimForSummary(lines[target.Line - 1]);
            var next = target.Line < lines.Length ? TrimForSummary(lines[target.Line]) : string.Empty;
            var summary = "Declaration line: " + current;
            if (!string.IsNullOrEmpty(previous))
            {
                summary += " | Prev: " + previous;
            }

            if (!string.IsNullOrEmpty(next))
            {
                summary += " | Next: " + next;
            }

            return summary;
        }

        private static string BuildSnippet(DocumentSession session, int lineNumber, int contextLineCount)
        {
            if (session == null || string.IsNullOrEmpty(session.Text))
            {
                return string.Empty;
            }

            var lines = SplitLines(session.Text);
            if (lines.Length == 0)
            {
                return string.Empty;
            }

            var safeLine = Math.Max(1, Math.Min(lineNumber, lines.Length));
            var start = Math.Max(1, safeLine - Math.Max(0, contextLineCount));
            var end = Math.Min(lines.Length, safeLine + Math.Max(0, contextLineCount));
            var snippet = string.Empty;
            for (var i = start; i <= end; i++)
            {
                if (snippet.Length > 0)
                {
                    snippet += Environment.NewLine;
                }

                snippet += i.ToString().PadLeft(4) + "  " + lines[i - 1];
            }

            return snippet;
        }

        private static string BuildDefinitionText(EditorCommandTarget target)
        {
            if (target == null || string.IsNullOrEmpty(target.DefinitionDocumentPath))
            {
                return "Unavailable";
            }

            return target.DefinitionDocumentPath + ":" + Math.Max(1, target.DefinitionLine) + ":" + Math.Max(1, target.DefinitionColumn);
        }

        private static string ExtractNamespace(EditorCommandTarget target)
        {
            var qualified = target != null ? target.QualifiedSymbolDisplay ?? string.Empty : string.Empty;
            var containingType = target != null ? target.ContainingTypeName ?? string.Empty : string.Empty;
            if (string.IsNullOrEmpty(qualified))
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(containingType))
            {
                var marker = "." + containingType;
                var markerIndex = qualified.IndexOf(marker, StringComparison.Ordinal);
                if (markerIndex > 0)
                {
                    return qualified.Substring(0, markerIndex);
                }
            }

            var lastDot = qualified.LastIndexOf('.');
            return lastDot > 0 ? qualified.Substring(0, lastDot) : string.Empty;
        }

        private static string[] SplitLines(string text)
        {
            return (text ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n');
        }

        private static string TrimForSummary(string value)
        {
            var trimmed = (value ?? string.Empty).Trim();
            if (trimmed.Length <= 72)
            {
                return trimmed;
            }

            return trimmed.Substring(0, 72) + "...";
        }
    }
}
