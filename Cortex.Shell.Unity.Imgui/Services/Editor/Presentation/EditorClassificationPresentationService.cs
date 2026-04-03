using System;
using UnityEngine;
using Cortex.Shell.Unity.Imgui;

namespace Cortex.Services.Editor.Presentation
{
    internal sealed class EditorClassificationPresentationService
    {
        private readonly EditorClassificationService _classificationService = new EditorClassificationService();

        public string GetHexColor(string classification)
        {
            return GetHexColor(classification, string.Empty);
        }

        public string GetHexColor(string classification, string semanticTokenType)
        {
            return _classificationService.GetHexColor(classification, semanticTokenType);
        }

        public Color GetColor(string classification)
        {
            return GetColor(classification, string.Empty);
        }

        public Color GetColor(string classification, string semanticTokenType)
        {
            return ImguiWorkbenchLayout.ParseColor(GetHexColor(classification, semanticTokenType), ImguiWorkbenchLayout.GetTextColor());
        }

        public bool IsHoverCandidate(string classification, string rawText)
        {
            return IsHoverCandidate(classification, string.Empty, rawText);
        }

        public bool IsHoverCandidate(string classification, string semanticTokenType, string rawText)
        {
            return _classificationService.IsHoverCandidate(classification, semanticTokenType, rawText);
        }

        public bool CanNavigateToDefinition(string classification, string rawText)
        {
            return CanNavigateToDefinition(classification, string.Empty, rawText);
        }

        public bool CanNavigateToDefinition(string classification, string semanticTokenType, string rawText)
        {
            return _classificationService.CanNavigateToDefinition(classification, semanticTokenType, rawText);
        }

        public string NormalizeClassification(string classification)
        {
            return _classificationService.NormalizeClassification(classification);
        }

        public string ResolvePresentationClassification(string classification, string semanticTokenType)
        {
            return _classificationService.ResolvePresentationClassification(classification, semanticTokenType);
        }
    }
}
