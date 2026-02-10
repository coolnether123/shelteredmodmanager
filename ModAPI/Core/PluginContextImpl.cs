using System;
using System.Collections;
using UnityEngine;

namespace ModAPI.Core
{
    internal class PluginContextImpl : IPluginContext
    {
        public GameObject LoaderRoot { get; set; }
        public GameObject PluginRoot { get; set; }
        public ModEntry Mod { get; set; }
        public ModAPI.Spine.ISettingsProvider Settings { get; set; }
        public IModLogger Log { get; set; }
        public IGameHelper Game { get; set; }
        public ISaveSystem SaveSystem { get; set; }
        public string GameRoot { get; set; }
        public string ModsRoot { get; set; }
        public bool IsModernUnity { get { return PluginRunner.IsModernUnity; } }

        public Action<Action> Scheduler;

        public FamilyMember FindMember(string characterId)
        {
            return Game != null ? Game.FindMember(characterId) : null;
        }

        public void RunNextFrame(Action action)
        {
            if (Scheduler != null) Scheduler(action);
        }

        public Coroutine StartCoroutine(IEnumerator routine)
        {
            return LoaderRoot != null ? LoaderRoot.GetComponent<PluginRunner>().StartCoroutine(routine) : null;
        }

        public GameObject FindPanel(string nameOrPath)
        {
            try { return ModAPI.SceneUtil.Find(nameOrPath); }
            catch (Exception ex) { MMLog.WarnOnce("PluginContextImpl.FindPanel", "Error finding panel: " + ex.Message); return null; }
        }

        public T AddComponentToPanel<T>(string nameOrPath) where T : Component
        {
            var go = FindPanel(nameOrPath);
            if (go == null)
            {
                if (Log != null) Log.Warn("FindPanel failed for '" + nameOrPath + "'");
                return null;
            }

            var existing = go.GetComponent<T>();
            if (existing != null) return existing;
            try { return go.AddComponent<T>(); }
            catch (Exception ex)
            {
                if (Log != null) Log.Error("AddComponentToPanel<" + typeof(T).Name + "> failed: " + ex.Message);
                return null;
            }
        }
    }
}
