using Cortex.Core.Abstractions;

namespace Cortex.Host.Unity.Runtime
{
    public interface IUnityWorkbenchContributionRegistrar
    {
        void RegisterBuiltIns(
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry,
            string rendererDisplayName);
    }

    internal sealed class NullUnityWorkbenchContributionRegistrar : IUnityWorkbenchContributionRegistrar
    {
        public void RegisterBuiltIns(
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry,
            string rendererDisplayName)
        {
        }
    }
}
