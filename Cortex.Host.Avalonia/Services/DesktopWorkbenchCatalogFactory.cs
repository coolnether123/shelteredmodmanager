using Cortex.Shell.Shared.Models;
using Cortex.Shell.Shared.Services;

namespace Cortex.Host.Avalonia.Services
{
    internal static class DesktopWorkbenchCatalogFactory
    {
        public static WorkbenchCatalogSnapshot CreateDefaultCatalog()
        {
            return WorkbenchCatalogFactory.CreateDefaultCatalog();
        }
    }
}
