using System;

namespace ModAPI.Core
{
    /// <summary>
    /// Neutral lifecycle notifications supplied by the active game runtime.
    /// Payloads are opaque to ModAPI; game-specific assemblies own their concrete types.
    /// </summary>
    public interface IGameLifecycleSource
    {
        event Action<object> BeforeSave;
        event Action<object> BeforeLoadSceneContents;
        event Action<object> AfterLoad;
        event Action SessionStarted;
        event Action NewGame;
    }

    internal static class GameLifecycleSources
    {
        private static readonly IGameLifecycleSource NullSource = new NullGameLifecycleSource();

        internal static void AddBeforeSave(Action<object> handler)
        {
            Current.BeforeSave += handler;
        }

        internal static void RemoveBeforeSave(Action<object> handler)
        {
            Current.BeforeSave -= handler;
        }

        internal static void AddBeforeLoadSceneContents(Action<object> handler)
        {
            Current.BeforeLoadSceneContents += handler;
        }

        internal static void RemoveBeforeLoadSceneContents(Action<object> handler)
        {
            Current.BeforeLoadSceneContents -= handler;
        }

        internal static void AddAfterLoad(Action<object> handler)
        {
            Current.AfterLoad += handler;
        }

        internal static void RemoveAfterLoad(Action<object> handler)
        {
            Current.AfterLoad -= handler;
        }

        internal static void AddSessionStarted(Action handler)
        {
            Current.SessionStarted += handler;
        }

        internal static void RemoveSessionStarted(Action handler)
        {
            Current.SessionStarted -= handler;
        }

        internal static void AddNewGame(Action handler)
        {
            Current.NewGame += handler;
        }

        internal static void RemoveNewGame(Action handler)
        {
            Current.NewGame -= handler;
        }

        private static IGameLifecycleSource Current
        {
            get
            {
                if (!ModAPIRegistry.IsAPIRegistered(GameRuntimeApiIds.GameLifecycle))
                    return NullSource;

                IGameLifecycleSource source = ModAPIRegistry.GetAPI<IGameLifecycleSource>(GameRuntimeApiIds.GameLifecycle);
                return source ?? NullSource;
            }
        }

        private sealed class NullGameLifecycleSource : IGameLifecycleSource
        {
            public event Action<object> BeforeSave { add { } remove { } }
            public event Action<object> BeforeLoadSceneContents { add { } remove { } }
            public event Action<object> AfterLoad { add { } remove { } }
            public event Action SessionStarted { add { } remove { } }
            public event Action NewGame { add { } remove { } }
        }
    }
}
