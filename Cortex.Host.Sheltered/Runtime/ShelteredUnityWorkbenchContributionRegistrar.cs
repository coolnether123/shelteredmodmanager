using Cortex.Core.Abstractions;
using Cortex.Host.Sheltered.Composition;
using Cortex.Host.Unity.Runtime;

namespace Cortex.Host.Sheltered.Runtime
{
    public sealed class ShelteredUnityWorkbenchContributionRegistrar : IUnityWorkbenchContributionRegistrar
    {
        private readonly UnityRenderHostCatalog _renderHostCatalog;
        private readonly string _hostStatusSummary;

        public ShelteredUnityWorkbenchContributionRegistrar()
            : this(null, null)
        {
        }

        internal ShelteredUnityWorkbenchContributionRegistrar(
            UnityRenderHostCatalog renderHostCatalog,
            string hostStatusSummary)
        {
            _renderHostCatalog = renderHostCatalog ?? UnityRenderHostCatalog.CreateDefault();
            _hostStatusSummary = hostStatusSummary ?? string.Empty;
        }

        public void RegisterBuiltIns(
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry,
            string rendererDisplayName)
        {
            var statusSummary = !string.IsNullOrEmpty(_hostStatusSummary)
                ? _hostStatusSummary
                : rendererDisplayName;
            ShelteredWorkbenchComposition.RegisterBuiltIns(
                commandRegistry,
                contributionRegistry,
                statusSummary,
                _renderHostCatalog);
        }
    }
}
