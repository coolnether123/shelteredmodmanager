using Cortex.Core.Abstractions;
using Cortex.Host.Sheltered.Composition;
using Cortex.Host.Unity.Runtime;

namespace Cortex.Host.Sheltered.Runtime
{
    public sealed class ShelteredUnityWorkbenchContributionRegistrar : IUnityWorkbenchContributionRegistrar
    {
        public void RegisterBuiltIns(
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry,
            string rendererDisplayName)
        {
            ShelteredWorkbenchComposition.RegisterBuiltIns(commandRegistry, contributionRegistry, rendererDisplayName);
        }
    }
}
