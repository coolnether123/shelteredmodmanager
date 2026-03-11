using System;
using Cortex.Core.Models;

namespace Cortex.Tabby
{
    internal static class TabbyRuntimeSettings
    {
        private const int MinimumBundledTimeoutMs = 30000;

        public static int GetEffectiveTimeoutMs(CortexSettings settings)
        {
            var tabbyTimeout = settings != null ? settings.TabbyRequestTimeoutMs : 0;
            var ollamaTimeout = settings != null ? settings.OllamaRequestTimeoutMs : 0;
            return Math.Max(MinimumBundledTimeoutMs, Math.Max(tabbyTimeout, ollamaTimeout));
        }
    }
}
