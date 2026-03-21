using Cortex.Core.Abstractions;
using Cortex.Plugins.Abstractions;

namespace Cortex.Host.Unity.Composition
{
    internal static class DefaultWorkbenchComposition
    {
        public static void RegisterBuiltIns(
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry,
            string rendererDisplayName)
        {
            var context = new WorkbenchPluginContext(commandRegistry, contributionRegistry, null);
            new DefaultWorkbenchViewContributions().Register(context);
            new DefaultWorkbenchCommandContributions().Register(context);
            new DefaultWorkbenchAppearanceContributions().Register(context, rendererDisplayName);
            new DefaultWorkbenchOnboardingContributions().Register(context);
            new DefaultWorkbenchSettingContributions().Register(context);
        }
    }
}
