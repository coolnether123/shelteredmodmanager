using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Presentation.Abstractions;
using Cortex.Services.Harmony.Generation;
using Cortex.Services.Harmony.Inspection;
using Cortex.Services.Harmony.Presentation;
using Cortex.Services.Harmony.Resolution;
using Cortex.Services.Navigation;
using Cortex.Services.Harmony.Workflow;

namespace Cortex.Modules.Editor
{
    internal sealed class EditorSurfaceServices
    {
        public IDocumentService DocumentService { get; set; }
        public ICortexNavigationService NavigationService { get; set; }
        public ICommandRegistry CommandRegistry { get; set; }
        public IContributionRegistry ContributionRegistry { get; set; }
        public CortexShellState State { get; set; }
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
