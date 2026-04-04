using Cortex.Core.Abstractions;
using Cortex.Host.Sheltered.Runtime;
using Cortex.Host.Unity.Runtime;
using Cortex.Plugins.Abstractions;

namespace Cortex.Host.Sheltered.Composition
{
    internal static class ShelteredWorkbenchComposition
    {
        public static void RegisterBuiltIns(
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry,
            string hostStatusSummary,
            UnityRenderHostCatalog renderHostCatalog)
        {
            var context = new WorkbenchPluginContext(commandRegistry, contributionRegistry, null, null, null);
            new ShelteredWorkbenchViewContributions().Register(context);
            new ShelteredWorkbenchCommandContributions().Register(context);
            new ShelteredWorkbenchAppearanceContributions().Register(context, hostStatusSummary);
            new ShelteredWorkbenchOnboardingContributions().Register(context);
            new ShelteredWorkbenchSettingContributions().Register(context, renderHostCatalog);
        }
    }
}
