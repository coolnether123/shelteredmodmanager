using System;
using UnityEngine;

using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Commands;
using ShelteredScenarioEditor.Presentation.UiKit;

namespace ShelteredScenarioEditor.Presentation.Authoring.Shell{
    internal sealed partial class ScenarioAuthoringShellImguiRenderModule
    {
        private void DrawVanillaInteractionAssistStripCore(
            float scaledWidth,
            float scaledHeight,
            ScenarioAuthoringInputCaptureService inputCapture)
        {
            ScenarioAuthoringState state = _snapshot != null ? _snapshot.State : null;
            Rect strip = BuildVanillaInteractionAssistStripRect(scaledWidth, scaledHeight);
            RegisterVisualSurface("vanilla_interaction.assist", strip);

            using (EnterVisualSurface("vanilla_interaction.assist"))
            {
                DrawChromePanel(strip, _statusStyle);
                Rect inner = new Rect(strip.x + 12f, strip.y + 7f, strip.width - 24f, strip.height - 14f);
                float buttonWidth = 92f;
                Rect buttonRect = new Rect(inner.xMax - buttonWidth, inner.y, buttonWidth, inner.height);
                Rect textRect = new Rect(inner.x, inner.y, Math.Max(0f, buttonRect.x - inner.x - 12f), inner.height);

                string title = FormatVanillaInteractionKind(state) + " Editor";
                string note = ResolveVanillaInteractionAssistNote(state);
                float titleWidth = Math.Min(textRect.width * 0.36f, ScenarioUiMeasuredLabel.Width(title, _textStyle, 18f));
                if (titleWidth > 40f)
                {
                    GUI.Label(new Rect(textRect.x, textRect.y + 2f, titleWidth, textRect.height - 2f), ShortenToFit(title, titleWidth, _textStyle), _textStyle);
                    textRect = new Rect(textRect.x + titleWidth + 14f, textRect.y, Math.Max(0f, textRect.width - titleWidth - 14f), textRect.height);
                }

                if (textRect.width > 40f)
                    GUI.Label(new Rect(textRect.x, textRect.y + 4f, textRect.width, textRect.height - 4f), ShortenToFit(note, textRect.width, _mutedTextStyle), _mutedTextStyle);

                DrawButton(buttonRect, new ScenarioAuthoringInspectorAction
                {
                    Id = ScenarioAuthoringActionIds.ActionVanillaInteractionReturnEditor,
                    Command = ShellUxCommand.Simple(ShellUxCommandKind.ReturnFromVanilla, ScenarioAuthoringActionIds.ActionVanillaInteractionReturnEditor),
                    Label = "Done",
                    Hint = "Close this window and return to the scenario editor.",
                    Enabled = true,
                    Emphasized = false,
                    IconText = "ED"
                }, false);
            }

            if (inputCapture != null)
                inputCapture.RegisterInteractiveRect(strip);
        }

        private static Rect BuildVanillaInteractionAssistStripRect(float scaledWidth, float scaledHeight)
        {
            float width = Mathf.Min(Mathf.Max(520f, scaledWidth * 0.52f), scaledWidth - (Margin * 2f));
            float height = 44f;
            return new Rect(Mathf.Max(Margin, (scaledWidth - width) * 0.5f), Margin, width, height);
        }

        private static string FormatVanillaInteractionKind(ScenarioAuthoringState state)
        {
            string kind = state != null ? state.VanillaInteractionKind : null;
            if (string.IsNullOrEmpty(kind))
                return "Game";
            return kind;
        }

        private static string ResolveVanillaInteractionAssistNote(ScenarioAuthoringState state)
        {
            if (state != null && !string.IsNullOrEmpty(state.VanillaInteractionAssistNote))
                return state.VanillaInteractionAssistNote;
            return "Make your changes here. They will be saved to this scenario.";
        }
    }
}
