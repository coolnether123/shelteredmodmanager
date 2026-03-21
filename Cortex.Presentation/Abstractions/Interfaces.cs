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

    public interface IWorkbenchRuntime
    {
        ICommandRegistry CommandRegistry { get; }
        IContributionRegistry ContributionRegistry { get; }
        WorkbenchState WorkbenchState { get; }
        LayoutState LayoutState { get; }
        StatusState StatusState { get; }
        ThemeState ThemeState { get; }
        FocusState FocusState { get; }

        WorkbenchPresentationSnapshot CreateSnapshot();
    }

    public interface IWorkbenchRuntimeFactory
    {
        IWorkbenchRuntime Create();
    }
}
