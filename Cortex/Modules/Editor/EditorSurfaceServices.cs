using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Presentation.Abstractions;
using Cortex.Services.Navigation;

namespace Cortex.Modules.Editor
{
    internal sealed class EditorSurfaceServices
    {
        public IDocumentService DocumentService { get; set; }
        public ICortexNavigationService NavigationService { get; set; }
        public ICommandRegistry CommandRegistry { get; set; }
        public IContributionRegistry ContributionRegistry { get; set; }
        public CortexShellState State { get; set; }
        public IProjectCatalog ProjectCatalog { get; set; }
        public ILoadedModCatalog LoadedModCatalog { get; set; }
        public ISourceLookupIndex SourceLookupIndex { get; set; }
        public IEditorContributionRuntime ExtensionRuntime { get; set; }
    }
}
