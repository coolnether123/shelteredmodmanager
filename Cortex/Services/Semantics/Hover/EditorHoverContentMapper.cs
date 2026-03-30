using System;
using System.Collections.Generic;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Rendering.Models;

namespace Cortex.Services.Semantics.Hover
{
    internal sealed class EditorHoverContentMapper
    {
        public EditorResolvedHoverContent BuildHoverContent(EditorHoverTarget hoverTarget, LanguageServiceHoverResponse response)
        {
            var target = hoverTarget != null ? hoverTarget.Target : null;
            var tokenClassification = hoverTarget != null ? hoverTarget.TokenClassification ?? string.Empty : string.Empty;
            var overloadSummary = BuildOverloadSummary(response != null ? response.SupplementalSections : null);
            return new EditorResolvedHoverContent
            {
                Key = hoverTarget != null ? hoverTarget.HoverKey ?? string.Empty : string.Empty,
                ContextKey = target != null ? target.ContextKey ?? string.Empty : string.Empty,
                DocumentPath = target != null ? target.DocumentPath ?? string.Empty : string.Empty,
                DocumentVersion = response != null ? response.DocumentVersion : 0,
                QualifiedPath = response != null ? response.QualifiedSymbolDisplay ?? string.Empty : string.Empty,
                SymbolDisplay = response != null && !string.IsNullOrEmpty(response.SymbolDisplay) ? response.SymbolDisplay ?? string.Empty : (target != null ? target.SymbolText ?? string.Empty : string.Empty),
                SummaryText = BuildMetaText(response, null, tokenClassification),
                DocumentationText = response != null ? response.DocumentationText ?? string.Empty : string.Empty,
                SignatureParts = BuildSignatureParts(response, tokenClassification, overloadSummary),
                SupplementalSections = BuildSections(response != null ? response.SupplementalSections : null, tokenClassification),
                OverloadSummary = overloadSummary,
                OverloadCount = 0,
                OverloadIndex = -1,
                PrimaryNavigationTarget = BuildNavigationTarget(response)
            };
        }

        public HoverTooltipRenderModel BuildRenderModel(EditorHoverTarget hoverTarget, EditorResolvedHoverContent hoverContent)
        {
            return new HoverTooltipRenderModel
            {
                Key = hoverContent != null ? hoverContent.Key ?? string.Empty : string.Empty,
                ContextKey = hoverContent != null ? hoverContent.ContextKey ?? string.Empty : string.Empty,
                DocumentPath = hoverContent != null ? hoverContent.DocumentPath ?? string.Empty : string.Empty,
                DocumentVersion = hoverContent != null ? hoverContent.DocumentVersion : 0,
                AnchorRect = hoverTarget != null ? hoverTarget.AnchorRect : new RenderRect(0f, 0f, 0f, 0f),
                QualifiedPath = hoverContent != null ? hoverContent.QualifiedPath ?? string.Empty : string.Empty,
                SymbolDisplay = hoverContent != null ? hoverContent.SymbolDisplay ?? string.Empty : string.Empty,
                SummaryText = hoverContent != null ? hoverContent.SummaryText ?? string.Empty : string.Empty,
                DocumentationText = hoverContent != null ? hoverContent.DocumentationText ?? string.Empty : string.Empty,
                SignatureParts = hoverContent != null ? hoverContent.SignatureParts : new EditorHoverContentPart[0],
                SupplementalSections = hoverContent != null ? hoverContent.SupplementalSections : new EditorHoverSection[0],
                OverloadIndex = hoverContent != null ? hoverContent.OverloadIndex : -1,
                OverloadCount = hoverContent != null ? hoverContent.OverloadCount : 0,
                OverloadSummary = hoverContent != null ? hoverContent.OverloadSummary ?? string.Empty : string.Empty,
                PrimaryNavigationTarget = hoverContent != null ? hoverContent.PrimaryNavigationTarget : null
            };
        }

        public int CountInteractiveParts(EditorResolvedHoverContent hoverContent)
        {
            if (hoverContent == null || hoverContent.SignatureParts == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < hoverContent.SignatureParts.Length; i++)
            {
                if (hoverContent.SignatureParts[i] != null && hoverContent.SignatureParts[i].IsInteractive)
                {
                    count++;
                }
            }

            return count;
        }

        public int CountSections(EditorResolvedHoverContent hoverContent)
        {
            return hoverContent != null && hoverContent.SupplementalSections != null ? hoverContent.SupplementalSections.Length : 0;
        }

        public int CountParts(EditorHoverContentPart[] parts)
        {
            return parts != null ? parts.Length : 0;
        }

        private EditorHoverContentPart[] BuildSignatureParts(LanguageServiceHoverResponse response, string tokenClassification, string overloadSummary)
        {
            if (response == null || response.DisplayParts == null || response.DisplayParts.Length == 0)
            {
                var fallbackParts = new List<EditorHoverContentPart>
                {
                    new EditorHoverContentPart
                    {
                        Text = response != null ? response.SymbolDisplay ?? string.Empty : string.Empty,
                        Classification = response != null ? response.SymbolKind ?? string.Empty : string.Empty,
                        IsInteractive = false,
                        SummaryText = BuildMetaText(response, null, tokenClassification),
                        DocumentationText = response != null ? response.DocumentationText ?? string.Empty : string.Empty,
                        SupplementalSections = BuildSections(response != null ? response.SupplementalSections : null, tokenClassification)
                    }
                };

                AppendOverloadSummaryPart(fallbackParts, overloadSummary);
                return fallbackParts.ToArray();
            }

            var parts = new List<EditorHoverContentPart>(response.DisplayParts.Length + 1);
            for (var i = 0; i < response.DisplayParts.Length; i++)
            {
                var displayPart = response.DisplayParts[i];
                parts.Add(new EditorHoverContentPart
                {
                    Text = displayPart != null ? displayPart.Text ?? string.Empty : string.Empty,
                    Classification = displayPart != null ? displayPart.Classification ?? string.Empty : string.Empty,
                    IsInteractive = displayPart != null && displayPart.IsInteractive && BuildNavigationTarget(displayPart) != null,
                    SummaryText = BuildMetaText(response, displayPart, tokenClassification),
                    DocumentationText = displayPart != null && !string.IsNullOrEmpty(displayPart.DocumentationText)
                        ? displayPart.DocumentationText ?? string.Empty
                        : (response != null ? response.DocumentationText ?? string.Empty : string.Empty),
                    SupplementalSections = BuildSections(displayPart != null ? displayPart.SupplementalSections : null, tokenClassification),
                    NavigationTarget = BuildNavigationTarget(displayPart)
                });
            }

            AppendOverloadSummaryPart(parts, overloadSummary);
            return parts.ToArray();
        }

        private static void AppendOverloadSummaryPart(List<EditorHoverContentPart> parts, string overloadSummary)
        {
            if (parts == null || string.IsNullOrEmpty(overloadSummary))
            {
                return;
            }

            parts.Add(new EditorHoverContentPart
            {
                Text = " " + overloadSummary,
                Classification = "overload",
                IsInteractive = false
            });
        }

        private EditorHoverSection[] BuildSections(LanguageServiceHoverSection[] sections, string tokenClassification)
        {
            if (sections == null || sections.Length == 0)
            {
                return new EditorHoverSection[0];
            }

            var results = new List<EditorHoverSection>();
            for (var i = 0; i < sections.Length; i++)
            {
                var section = sections[i];
                if (section == null)
                {
                    continue;
                }

                var displayParts = section.DisplayParts;
                var mappedDisplayParts = new EditorHoverContentPart[displayParts != null ? displayParts.Length : 0];
                for (var partIndex = 0; displayParts != null && partIndex < displayParts.Length; partIndex++)
                {
                    var displayPart = displayParts[partIndex];
                    mappedDisplayParts[partIndex] = new EditorHoverContentPart
                    {
                        Text = displayPart != null ? displayPart.Text ?? string.Empty : string.Empty,
                        Classification = displayPart != null ? displayPart.Classification ?? string.Empty : string.Empty,
                        IsInteractive = displayPart != null && displayPart.IsInteractive && BuildNavigationTarget(displayPart) != null,
                        SummaryText = BuildMetaText(null, displayPart, tokenClassification),
                        DocumentationText = displayPart != null ? displayPart.DocumentationText ?? string.Empty : string.Empty,
                        SupplementalSections = BuildSections(displayPart != null ? displayPart.SupplementalSections : null, tokenClassification),
                        NavigationTarget = BuildNavigationTarget(displayPart)
                    };
                }

                var text = !string.IsNullOrEmpty(section.Text) ? section.Text ?? string.Empty : FlattenDisplayParts(section.DisplayParts);
                if (string.IsNullOrEmpty(text) && mappedDisplayParts.Length == 0)
                {
                    continue;
                }

                results.Add(new EditorHoverSection
                {
                    Title = section.Title ?? section.Kind ?? string.Empty,
                    Text = text,
                    DisplayParts = mappedDisplayParts
                });
            }

            return results.ToArray();
        }

        private static string FlattenDisplayParts(LanguageServiceHoverDisplayPart[] parts)
        {
            if (parts == null || parts.Length == 0)
            {
                return string.Empty;
            }

            var text = string.Empty;
            for (var i = 0; i < parts.Length; i++)
            {
                if (parts[i] != null && !string.IsNullOrEmpty(parts[i].Text))
                {
                    text += parts[i].Text;
                }
            }

            return text;
        }

        private static string BuildMetaText(LanguageServiceHoverResponse response, LanguageServiceHoverDisplayPart hoveredPart, string tokenClassification)
        {
            var symbolKind = hoveredPart != null && !string.IsNullOrEmpty(hoveredPart.SymbolKind)
                ? hoveredPart.SymbolKind
                : response != null ? response.SymbolKind ?? string.Empty : string.Empty;
            var containingTypeName = hoveredPart != null && !string.IsNullOrEmpty(hoveredPart.ContainingTypeName)
                ? hoveredPart.ContainingTypeName
                : response != null ? response.ContainingTypeName ?? string.Empty : string.Empty;
            var assemblyName = hoveredPart != null && !string.IsNullOrEmpty(hoveredPart.ContainingAssemblyName)
                ? hoveredPart.ContainingAssemblyName
                : response != null ? response.ContainingAssemblyName ?? string.Empty : string.Empty;

            var meta = string.Empty;
            if (!string.IsNullOrEmpty(symbolKind))
            {
                meta = symbolKind;
            }

            if (!string.IsNullOrEmpty(tokenClassification))
            {
                meta = !string.IsNullOrEmpty(meta) ? meta + " | " + tokenClassification : tokenClassification;
            }

            if (!string.IsNullOrEmpty(containingTypeName))
            {
                meta = !string.IsNullOrEmpty(meta) ? meta + " | " + containingTypeName : containingTypeName;
            }

            if (!string.IsNullOrEmpty(assemblyName))
            {
                meta = !string.IsNullOrEmpty(meta) ? meta + " | " + assemblyName : assemblyName;
            }

            return meta;
        }

        private static string BuildOverloadSummary(LanguageServiceHoverSection[] sections)
        {
            if (sections == null || sections.Length == 0)
            {
                return string.Empty;
            }

            for (var i = 0; i < sections.Length; i++)
            {
                var section = sections[i];
                if (section == null)
                {
                    continue;
                }

                var title = section.Title ?? section.Kind ?? string.Empty;
                if (title.IndexOf("overload", StringComparison.OrdinalIgnoreCase) >= 0 && !string.IsNullOrEmpty(section.Text))
                {
                    return section.Text ?? string.Empty;
                }
            }

            return string.Empty;
        }

        private static EditorHoverNavigationTarget BuildNavigationTarget(LanguageServiceHoverResponse response)
        {
            if (response == null)
            {
                return null;
            }

            return CreateNavigationTarget(
                response.SymbolDisplay,
                response.SymbolKind,
                response.MetadataName,
                response.ContainingTypeName,
                response.ContainingAssemblyName,
                response.DocumentationCommentId,
                response.DefinitionDocumentPath,
                response.DefinitionRange);
        }

        private static EditorHoverNavigationTarget BuildNavigationTarget(LanguageServiceHoverDisplayPart displayPart)
        {
            if (displayPart == null || !displayPart.IsInteractive)
            {
                return null;
            }

            return CreateNavigationTarget(
                displayPart.SymbolDisplay,
                displayPart.SymbolKind,
                displayPart.MetadataName,
                displayPart.ContainingTypeName,
                displayPart.ContainingAssemblyName,
                displayPart.DocumentationCommentId,
                displayPart.DefinitionDocumentPath,
                displayPart.DefinitionRange);
        }

        private static EditorHoverNavigationTarget CreateNavigationTarget(string symbolDisplay, string symbolKind, string metadataName, string containingTypeName, string containingAssemblyName, string documentationCommentId, string definitionDocumentPath, LanguageServiceRange definitionRange)
        {
            if (string.IsNullOrEmpty(definitionDocumentPath) &&
                string.IsNullOrEmpty(containingAssemblyName) &&
                string.IsNullOrEmpty(documentationCommentId) &&
                string.IsNullOrEmpty(metadataName))
            {
                return null;
            }

            return new EditorHoverNavigationTarget
            {
                Kind = EditorHoverNavigationKind.Symbol,
                Label = !string.IsNullOrEmpty(symbolDisplay) ? symbolDisplay ?? string.Empty : metadataName ?? string.Empty,
                SymbolDisplay = symbolDisplay ?? string.Empty,
                SymbolKind = symbolKind ?? string.Empty,
                MetadataName = metadataName ?? string.Empty,
                ContainingTypeName = containingTypeName ?? string.Empty,
                ContainingAssemblyName = containingAssemblyName ?? string.Empty,
                DocumentationCommentId = documentationCommentId ?? string.Empty,
                DefinitionDocumentPath = definitionDocumentPath ?? string.Empty,
                DefinitionRange = definitionRange
            };
        }
    }
}
