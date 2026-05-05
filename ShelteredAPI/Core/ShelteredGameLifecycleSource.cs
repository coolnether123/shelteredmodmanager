using System;
using System.Collections.Generic;
using ModAPI.Core;
using ShelteredAPI.Events;

using ShelteredAPI.Saves;
namespace ShelteredAPI.Core
{
    internal sealed class ShelteredGameLifecycleSource : IGameLifecycleSource
    {
        private readonly object _sync = new object();
        private readonly Dictionary<Action<object>, Action<SaveData>> _beforeSaveHandlers = new Dictionary<Action<object>, Action<SaveData>>();
        private readonly Dictionary<Action<object>, Action<SaveData>> _beforeLoadHandlers = new Dictionary<Action<object>, Action<SaveData>>();
        private readonly Dictionary<Action<object>, Action<SaveData>> _afterLoadHandlers = new Dictionary<Action<object>, Action<SaveData>>();

        public event Action<object> BeforeSave
        {
            add { AddSaveHandler(_beforeSaveHandlers, value, delegate(Action<SaveData> handler) { GameEvents.OnBeforeSave += handler; }); }
            remove { RemoveSaveHandler(_beforeSaveHandlers, value, delegate(Action<SaveData> handler) { GameEvents.OnBeforeSave -= handler; }); }
        }

        public event Action<object> BeforeLoadSceneContents
        {
            add { AddSaveHandler(_beforeLoadHandlers, value, delegate(Action<SaveData> handler) { GameEvents.OnBeforeLoadSceneContents += handler; }); }
            remove { RemoveSaveHandler(_beforeLoadHandlers, value, delegate(Action<SaveData> handler) { GameEvents.OnBeforeLoadSceneContents -= handler; }); }
        }

        public event Action<object> AfterLoad
        {
            add { AddSaveHandler(_afterLoadHandlers, value, delegate(Action<SaveData> handler) { GameEvents.OnAfterLoad += handler; }); }
            remove { RemoveSaveHandler(_afterLoadHandlers, value, delegate(Action<SaveData> handler) { GameEvents.OnAfterLoad -= handler; }); }
        }

        public event Action SessionStarted
        {
            add { GameEvents.OnSessionStarted += value; }
            remove { GameEvents.OnSessionStarted -= value; }
        }

        public event Action NewGame
        {
            add { GameEvents.OnNewGame += value; }
            remove { GameEvents.OnNewGame -= value; }
        }

        private void AddSaveHandler(
            Dictionary<Action<object>, Action<SaveData>> map,
            Action<object> handler,
            Action<Action<SaveData>> subscribe)
        {
            if (handler == null)
                return;

            Action<SaveData> wrapped = delegate(SaveData data) { handler(data); };
            lock (_sync)
            {
                map[handler] = wrapped;
            }

            subscribe(wrapped);
        }

        private void RemoveSaveHandler(
            Dictionary<Action<object>, Action<SaveData>> map,
            Action<object> handler,
            Action<Action<SaveData>> unsubscribe)
        {
            if (handler == null)
                return;

            Action<SaveData> wrapped = null;
            lock (_sync)
            {
                if (!map.TryGetValue(handler, out wrapped))
                    return;
                map.Remove(handler);
            }

            unsubscribe(wrapped);
        }
    }
}
