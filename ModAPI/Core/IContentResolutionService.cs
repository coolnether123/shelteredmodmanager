using System;
using System.Collections.Generic;

namespace ModAPI.Core
{
    /// <summary>
    /// Resolves mod-facing content identifiers to opaque host runtime keys.
    /// Implementations are game-owned; ModAPI treats returned keys as host-specific values.
    /// </summary>
    public interface IContentResolutionService
    {
        /// <summary>
        /// Resolves a mod-facing item ID to the host runtime key used by the active game.
        /// The returned key is opaque; pass it back to game-specific services rather than casting it in ModAPI code.
        /// </summary>
        bool TryResolveRuntimeItemKey(string itemId, out object runtimeItemKey);

        /// <summary>
        /// Enumerates runtime item keys registered by the host integration.
        /// Use this for diagnostics or compatibility scans, not as a stable persistence format.
        /// </summary>
        IEnumerable<object> GetRegisteredRuntimeItemKeys();
    }

    internal static class ContentResolutionServices
    {
        private static readonly IContentResolutionService NullService = new NullContentResolutionService();
        private static readonly object[] EmptyRuntimeItemKeys = new object[0];

        internal static bool TryResolveRuntimeItemKey(string itemId, out object runtimeItemKey)
        {
            try
            {
                return Current.TryResolveRuntimeItemKey(itemId, out runtimeItemKey);
            }
            catch (Exception ex)
            {
                runtimeItemKey = null;
                MMLog.WarnOnce("ContentResolutionServices.TryResolveRuntimeItemKey", "Content resolution failed: " + ex.Message);
                return false;
            }
        }

        internal static IEnumerable<object> GetRegisteredRuntimeItemKeys()
        {
            try
            {
                IEnumerable<object> keys = Current.GetRegisteredRuntimeItemKeys();
                return keys ?? EmptyRuntimeItemKeys;
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ContentResolutionServices.GetRegisteredRuntimeItemKeys", "Content enumeration failed: " + ex.Message);
                return EmptyRuntimeItemKeys;
            }
        }

        private static IContentResolutionService Current
        {
            get
            {
                if (!ModAPIRegistry.IsAPIRegistered(GameRuntimeApiIds.ContentResolution))
                    return NullService;

                IContentResolutionService service = ModAPIRegistry.GetAPI<IContentResolutionService>(GameRuntimeApiIds.ContentResolution);
                return service ?? NullService;
            }
        }

        private sealed class NullContentResolutionService : IContentResolutionService
        {
            public bool TryResolveRuntimeItemKey(string itemId, out object runtimeItemKey)
            {
                runtimeItemKey = null;
                return false;
            }

            public IEnumerable<object> GetRegisteredRuntimeItemKeys()
            {
                return EmptyRuntimeItemKeys;
            }
        }
    }
}
