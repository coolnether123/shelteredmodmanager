using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Presentation.Models;

namespace Cortex.Presentation.Abstractions
{
    public interface IWorkbenchPresenter
    {
        WorkbenchPresentationSnapshot BuildSnapshot(
            WorkbenchState workbenchState,
            LayoutState layoutState,
            StatusState statusState,
            ThemeState themeState,
            FocusState focusState,
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry);
    }
}
