using Cortex.Presentation.Abstractions;
using Cortex.Rendering.RuntimeUi;

namespace Cortex.Host.Unity.Runtime
{
    public sealed class UnityWorkbenchRuntimeFactory : IWorkbenchRuntimeFactory
    {
        private readonly IUnityWorkbenchContributionRegistrar _contributionRegistrar;
        private readonly IWorkbenchRuntimeUiFactory _runtimeUiFactory;

        public UnityWorkbenchRuntimeFactory()
            : this(null, null)
        {
        }

        public UnityWorkbenchRuntimeFactory(IUnityWorkbenchContributionRegistrar contributionRegistrar)
            : this(contributionRegistrar, null)
        {
        }

        public UnityWorkbenchRuntimeFactory(IUnityWorkbenchContributionRegistrar contributionRegistrar, IWorkbenchRuntimeUiFactory runtimeUiFactory)
        {
            _contributionRegistrar = contributionRegistrar;
            _runtimeUiFactory = runtimeUiFactory ?? NullWorkbenchRuntimeUiFactory.Instance;
        }

        public IWorkbenchRuntime Create()
        {
            return new UnityWorkbenchRuntime(_contributionRegistrar, _runtimeUiFactory.Create());
        }
    }
}
