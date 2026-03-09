using Cortex.Core.Abstractions;

namespace Cortex.Plugins.Abstractions
{
    public interface IWorkbenchPlugin
    {
        string PluginId { get; }
        string DisplayName { get; }
        void Register(ICommandRegistry commandRegistry, IContributionRegistry contributionRegistry);
    }
}
