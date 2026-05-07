using System;
using ModAPI.Core;
using ShelteredAPI.Core;

namespace ShelteredAPI.Networking
{
    /// <summary>
    /// Sheltered-specific multiplayer integration facade.
    /// This is the game-facing hook layer; transport and protocol code stay in ModAPI.Networking.
    /// </summary>
    public static class ShelteredMultiplayer
    {
        public static IShelteredMultiplayerHooks Hooks
        {
            get { return ShelteredMultiplayerHookService.Instance; }
        }

        public static bool TryGetHooks(out IShelteredMultiplayerHooks hooks)
        {
            if (ModAPIRegistry.TryGetAPI(ShelteredApiAliasIds.ShelteredMultiplayerHooks, out hooks))
                return true;

            hooks = Hooks;
            return hooks != null;
        }

        public static ShelteredMultiplayerSessionState SessionState
        {
            get { return Hooks.SessionState; }
        }

        public static bool IsActive
        {
            get { return Hooks.IsMultiplayerActive; }
        }

        public static void ActivateHost(byte localPlayerId, string sessionId, int tickRate)
        {
            Hooks.ActivateHost(localPlayerId, sessionId, tickRate);
        }

        public static void ActivateClient(byte localPlayerId, string sessionId, int tickRate)
        {
            Hooks.ActivateClient(localPlayerId, sessionId, tickRate);
        }

        public static void Deactivate(string reason)
        {
            Hooks.Deactivate(reason);
        }

        public static void EnqueueMainThread(Action action)
        {
            Hooks.EnqueueMainThread(action);
        }
    }
}
