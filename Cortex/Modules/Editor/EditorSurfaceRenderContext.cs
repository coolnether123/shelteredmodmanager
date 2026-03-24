using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Presentation.Abstractions;
using Cortex.Services;

namespace Cortex.Modules.Editor
{
    internal sealed class EditorSurfaceRenderContext
    {
        public IDocumentService DocumentService { get; set; }
        public ICommandRegistry CommandRegistry { get; set; }
        public IContributionRegistry ContributionRegistry { get; set; }
        public CortexShellState State { get; set; }
        public string ThemeKey { get; set; }
        public UnityEngine.GUIStyle CodeStyle { get; set; }
        public UnityEngine.GUIStyle GutterStyle { get; set; }
        public UnityEngine.GUIStyle TooltipStyle { get; set; }
        public UnityEngine.GUIStyle ContextMenuStyle { get; set; }
        public UnityEngine.GUIStyle ContextMenuButtonStyle { get; set; }
        public UnityEngine.GUIStyle ContextMenuHeaderStyle { get; set; }
        public UnityEngine.Rect BlockedRect { get; set; }
        public float GutterWidth { get; set; }
        public HarmonyPatchGenerationService HarmonyPatchGenerationService { get; set; }
        public GeneratedTemplateNavigationService GeneratedTemplateNavigationService { get; set; }
        public IProjectCatalog ProjectCatalog { get; set; }
        public ILoadedModCatalog LoadedModCatalog { get; set; }
        public ISourceLookupIndex SourceLookupIndex { get; set; }
        public HarmonyPatchInspectionService HarmonyPatchInspectionService { get; set; }
        public HarmonyPatchResolutionService HarmonyPatchResolutionService { get; set; }
        public HarmonyPatchDisplayService HarmonyPatchDisplayService { get; set; }
    }
}
