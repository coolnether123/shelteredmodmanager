using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.Presentation.Abstractions;
using Cortex.Presentation.Models;
using Cortex.Presentation.Services;
using Cortex.Rendering.Abstractions;
using Cortex.Rendering.RuntimeUi;

namespace Cortex.Host.Unity.Runtime
{
    public sealed class UnityWorkbenchRuntime : IWorkbenchRuntime, IWorkbenchRuntimeUiProvider, System.IDisposable
    {
        public readonly ICommandRegistry CommandRegistry;
        public readonly IContributionRegistry ContributionRegistry;
        public readonly IWorkbenchPresenter Presenter;
        public readonly IRenderPipeline RenderPipeline;
        public readonly IWorkbenchRenderer Renderer;
        public readonly WorkbenchState WorkbenchState;
        public readonly LayoutState LayoutState;
        public readonly StatusState StatusState;
        public readonly ThemeState ThemeState;
        public readonly FocusState FocusState;
        private readonly IUnityWorkbenchContributionRegistrar _contributionRegistrar;
        private readonly IWorkbenchRuntimeUi _runtimeUi;

        public UnityWorkbenchRuntime()
            : this(null, null)
        {
        }

        public UnityWorkbenchRuntime(IUnityWorkbenchContributionRegistrar contributionRegistrar)
            : this(contributionRegistrar, null)
        {
        }

        public UnityWorkbenchRuntime(IUnityWorkbenchContributionRegistrar contributionRegistrar, IWorkbenchRuntimeUi runtimeUi)
        {
            _contributionRegistrar = contributionRegistrar ?? new NullUnityWorkbenchContributionRegistrar();
            _runtimeUi = runtimeUi ?? NullWorkbenchRuntimeUi.Instance;
            CommandRegistry = new CommandRegistry();
            ContributionRegistry = new ContributionRegistry();
            Presenter = new WorkbenchPresenter();
            RenderPipeline = _runtimeUi.RenderPipeline;
            Renderer = RenderPipeline.WorkbenchRenderer;
            WorkbenchState = new WorkbenchState();
            LayoutState = new LayoutState();
            StatusState = new StatusState();
            ThemeState = new ThemeState();
            FocusState = new FocusState();
            _contributionRegistrar.RegisterBuiltIns(CommandRegistry, ContributionRegistry, Renderer.DisplayName);
        }

        public WorkbenchPresentationSnapshot CreateSnapshot()
        {
            var snapshot = Presenter.BuildSnapshot(WorkbenchState, LayoutState, StatusState, ThemeState, FocusState, CommandRegistry, ContributionRegistry);
            snapshot.RendererSummary = Renderer.DisplayName + " | Capabilities v" + Renderer.Capabilities.CapabilityVersion;
            return snapshot;
        }

        public IWorkbenchRuntimeUi RuntimeUi
        {
            get { return _runtimeUi; }
        }

        public void Dispose()
        {
            var disposable = _runtimeUi as System.IDisposable;
            if (disposable != null)
            {
                disposable.Dispose();
            }
        }

        ICommandRegistry IWorkbenchRuntime.CommandRegistry { get { return CommandRegistry; } }
        IContributionRegistry IWorkbenchRuntime.ContributionRegistry { get { return ContributionRegistry; } }
        WorkbenchState IWorkbenchRuntime.WorkbenchState { get { return WorkbenchState; } }
        LayoutState IWorkbenchRuntime.LayoutState { get { return LayoutState; } }
        StatusState IWorkbenchRuntime.StatusState { get { return StatusState; } }
        ThemeState IWorkbenchRuntime.ThemeState { get { return ThemeState; } }
        FocusState IWorkbenchRuntime.FocusState { get { return FocusState; } }
    }
}
