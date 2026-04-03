using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Presentation.Models;

namespace Cortex.Presentation.Abstractions
{
    public interface IWorkbenchPresenter
    {
        WorkbenchPresentationSnapshot BuildSnapshot(IWorkbenchRuntime runtime, WorkbenchPresentationMetadata metadata);
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
    }

    public interface IWorkbenchRuntimeFactory
    {
        IWorkbenchRuntime Create();
    }
}
