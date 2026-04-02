using Cortex.Presentation.Abstractions;

namespace Cortex.Host.Unity.Runtime
{
    public sealed class UnityWorkbenchRuntimeFactory : IWorkbenchRuntimeFactory
    {
        private readonly IUnityWorkbenchContributionRegistrar _contributionRegistrar;

        public UnityWorkbenchRuntimeFactory()
            : this(null)
        {
        }

        public UnityWorkbenchRuntimeFactory(IUnityWorkbenchContributionRegistrar contributionRegistrar)
        {
            _contributionRegistrar = contributionRegistrar;
        }

        public IWorkbenchRuntime Create()
        {
            return new UnityWorkbenchRuntime(_contributionRegistrar);
        }
    }
}
