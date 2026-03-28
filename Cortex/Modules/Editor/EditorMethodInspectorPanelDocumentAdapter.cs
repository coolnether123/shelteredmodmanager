using Cortex.Presentation.Models;
using Cortex.Rendering.Models;

namespace Cortex.Modules.Editor
{
    internal sealed class EditorMethodInspectorPanelDocumentAdapter
    {
        public PanelDocument Build(MethodInspectorViewModel viewModel)
        {
            if (viewModel == null)
            {
                return null;
            }

            var document = new PanelDocument();
            document.Title = viewModel.Title ?? string.Empty;
            document.Subtitle = viewModel.Subtitle ?? string.Empty;
            document.ShowCloseButton = viewModel.ShowCloseButton;
            document.HeaderActions = BuildActions(viewModel.HeaderActions);
            document.Sections = BuildSections(viewModel.Sections);
            return document;
        }

        private static PanelAction[] BuildActions(MethodInspectorActionViewModel[] actions)
        {
            if (actions == null || actions.Length == 0)
            {
                return new PanelAction[0];
            }

            var results = new PanelAction[actions.Length];
            for (var i = 0; i < actions.Length; i++)
            {
                var action = actions[i];
                results[i] = action != null
                    ? new PanelAction
                    {
                        Id = action.Id ?? string.Empty,
                        Label = action.Label ?? string.Empty,
                        Hint = action.Hint ?? string.Empty,
                        Enabled = action.Enabled,
                        Emphasized = action.Emphasized
                    }
                    : new PanelAction();
            }

            return results;
        }

        private static PanelSection[] BuildSections(MethodInspectorSectionViewModel[] sections)
        {
            if (sections == null || sections.Length == 0)
            {
                return new PanelSection[0];
            }

            var results = new PanelSection[sections.Length];
            for (var i = 0; i < sections.Length; i++)
            {
                var section = sections[i];
                results[i] = section != null
                    ? new PanelSection
                    {
                        Id = section.Id ?? string.Empty,
                        Title = section.Title ?? string.Empty,
                        Expanded = section.Expanded,
                        Elements = BuildElements(section.Elements)
                    }
                    : new PanelSection();
            }

            return results;
        }

        private static PanelElement[] BuildElements(MethodInspectorElementViewModel[] elements)
        {
            if (elements == null || elements.Length == 0)
            {
                return new PanelElement[0];
            }

            var results = new PanelElement[elements.Length];
            for (var i = 0; i < elements.Length; i++)
            {
                results[i] = BuildElement(elements[i]);
            }

            return results;
        }

        private static PanelElement BuildElement(MethodInspectorElementViewModel element)
        {
            if (element == null)
            {
                return null;
            }

            switch (element.Kind)
            {
                case MethodInspectorElementKind.Metadata:
                    var metadata = element as MethodInspectorMetadataViewModel;
                    return new PanelMetadataElement
                    {
                        Label = metadata != null ? metadata.Label ?? string.Empty : string.Empty,
                        Value = metadata != null ? metadata.Value ?? string.Empty : string.Empty,
                        DrawDivider = metadata == null || metadata.DrawDivider
                    };
                case MethodInspectorElementKind.Text:
                    var text = element as MethodInspectorTextViewModel;
                    return new PanelTextElement
                    {
                        Label = text != null ? text.Label ?? string.Empty : string.Empty,
                        Value = text != null ? text.Value ?? string.Empty : string.Empty,
                        Monospace = text != null && text.Monospace
                    };
                case MethodInspectorElementKind.Action:
                    var actionElement = element as MethodInspectorActionElementViewModel;
                    return new PanelActionElement
                    {
                        Action = BuildSingleAction(actionElement != null ? actionElement.Action : null),
                        Hint = actionElement != null ? actionElement.Hint ?? string.Empty : string.Empty
                    };
                case MethodInspectorElementKind.Card:
                    var card = element as MethodInspectorCardViewModel;
                    return new PanelCardElement
                    {
                        Title = card != null ? card.Title ?? string.Empty : string.Empty,
                        Body = card != null ? card.Body ?? string.Empty : string.Empty,
                        Rows = BuildRows(card != null ? card.Rows : null),
                        Actions = BuildActions(card != null ? card.Actions : null)
                    };
                case MethodInspectorElementKind.Spacer:
                    var spacer = element as MethodInspectorSpacerViewModel;
                    return new PanelSpacerElement
                    {
                        Height = spacer != null ? spacer.Height : 6f
                    };
                default:
                    return null;
            }
        }

        private static PanelAction BuildSingleAction(MethodInspectorActionViewModel action)
        {
            return action != null
                ? new PanelAction
                {
                    Id = action.Id ?? string.Empty,
                    Label = action.Label ?? string.Empty,
                    Hint = action.Hint ?? string.Empty,
                    Enabled = action.Enabled,
                    Emphasized = action.Emphasized
                }
                : new PanelAction();
        }

        private static PanelMetadataElement[] BuildRows(MethodInspectorMetadataViewModel[] rows)
        {
            if (rows == null || rows.Length == 0)
            {
                return new PanelMetadataElement[0];
            }

            var results = new PanelMetadataElement[rows.Length];
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                results[i] = row != null
                    ? new PanelMetadataElement
                    {
                        Label = row.Label ?? string.Empty,
                        Value = row.Value ?? string.Empty,
                        DrawDivider = row.DrawDivider
                    }
                    : new PanelMetadataElement();
            }

            return results;
        }
    }
}
