using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.Presentation.Abstractions;
using Cortex.Presentation.Models;
using Cortex.Rendering.Abstractions;
using Cortex.Rendering.RuntimeUi;
using System;

namespace Cortex.Host.Unity.Runtime
{
    public sealed class UnityWorkbenchRuntime : IWorkbenchRuntime, IWorkbenchRuntimeUiProvider, IWorkbenchRuntimeUiSwitcher, IDisposable
    {
        public readonly ICommandRegistry CommandRegistry;
        public readonly IContributionRegistry ContributionRegistry;
        public readonly WorkbenchState WorkbenchState;
        public readonly LayoutState LayoutState;
        public readonly StatusState StatusState;
        public readonly ThemeState ThemeState;
        public readonly FocusState FocusState;
        private readonly IUnityWorkbenchContributionRegistrar _contributionRegistrar;
        private IWorkbenchRuntimeUi _runtimeUi;

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
            WorkbenchState = new WorkbenchState();
            LayoutState = new LayoutState();
            StatusState = new StatusState();
            ThemeState = new ThemeState();
            FocusState = new FocusState();
            _contributionRegistrar.RegisterBuiltIns(CommandRegistry, ContributionRegistry, Renderer.DisplayName);
        }

        public IRenderPipeline RenderPipeline
        {
            get { return RuntimeUi.RenderPipeline; }
        }

        public IWorkbenchRenderer Renderer
        {
            get { return RenderPipeline.WorkbenchRenderer; }
        }

        public IWorkbenchRuntimeUi RuntimeUi
        {
            get { return _runtimeUi; }
        }

        public bool SwitchRuntimeUi(IWorkbenchRuntimeUi runtimeUi)
        {
            var nextRuntimeUi = runtimeUi ?? NullWorkbenchRuntimeUi.Instance;
            if (object.ReferenceEquals(_runtimeUi, nextRuntimeUi))
            {
                return false;
            }

            if (AreEquivalent(_runtimeUi, nextRuntimeUi))
            {
                DisposeRuntimeUi(nextRuntimeUi);
                return false;
            }

            var previousRuntimeUi = _runtimeUi;
            _runtimeUi = nextRuntimeUi;
            DisposeRuntimeUi(previousRuntimeUi);
            return true;
        }

        public void Dispose()
        {
            DisposeRuntimeUi(_runtimeUi);
            _runtimeUi = NullWorkbenchRuntimeUi.Instance;
        }

        ICommandRegistry IWorkbenchRuntime.CommandRegistry { get { return CommandRegistry; } }
        IContributionRegistry IWorkbenchRuntime.ContributionRegistry { get { return ContributionRegistry; } }
        WorkbenchState IWorkbenchRuntime.WorkbenchState { get { return WorkbenchState; } }
        LayoutState IWorkbenchRuntime.LayoutState { get { return LayoutState; } }
        StatusState IWorkbenchRuntime.StatusState { get { return StatusState; } }
        ThemeState IWorkbenchRuntime.ThemeState { get { return ThemeState; } }
        FocusState IWorkbenchRuntime.FocusState { get { return FocusState; } }

        private static bool AreEquivalent(IWorkbenchRuntimeUi left, IWorkbenchRuntimeUi right)
        {
            var leftUi = left ?? NullWorkbenchRuntimeUi.Instance;
            var rightUi = right ?? NullWorkbenchRuntimeUi.Instance;
            return
                leftUi.LayoutMode == rightUi.LayoutMode &&
                string.Equals(
                    leftUi.RenderPipeline.WorkbenchRenderer.RendererId,
                    rightUi.RenderPipeline.WorkbenchRenderer.RendererId,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static void DisposeRuntimeUi(IWorkbenchRuntimeUi runtimeUi)
        {
            var disposable = runtimeUi as IDisposable;
            if (disposable != null && !object.ReferenceEquals(runtimeUi, NullWorkbenchRuntimeUi.Instance))
            {
                disposable.Dispose();
            }
        }
    }
}
