using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.Host.Unity.Composition;
using Cortex.Presentation.Abstractions;
using Cortex.Presentation.Models;
using Cortex.Presentation.Services;
using Cortex.Renderers.Imgui;
using Cortex.Rendering.Abstractions;

namespace Cortex.Host.Unity.Runtime
{
    public sealed class UnityWorkbenchRuntime : IWorkbenchRuntime
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

        public UnityWorkbenchRuntime()
        {
            CommandRegistry = new CommandRegistry();
            ContributionRegistry = new ContributionRegistry();
            Presenter = new WorkbenchPresenter();
            RenderPipeline = new ImguiRenderPipeline();
            Renderer = RenderPipeline.WorkbenchRenderer;
            WorkbenchState = new WorkbenchState();
            LayoutState = new LayoutState();
            StatusState = new StatusState();
            ThemeState = new ThemeState();
            FocusState = new FocusState();
            DefaultWorkbenchComposition.RegisterBuiltIns(CommandRegistry, ContributionRegistry, Renderer.DisplayName);
        }

        public WorkbenchPresentationSnapshot CreateSnapshot()
        {
            var snapshot = Presenter.BuildSnapshot(WorkbenchState, LayoutState, StatusState, ThemeState, FocusState, CommandRegistry, ContributionRegistry);
            snapshot.RendererSummary = Renderer.DisplayName + " | Capabilities v" + Renderer.Capabilities.CapabilityVersion;
            return snapshot;
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
