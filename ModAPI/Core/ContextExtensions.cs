using System;
using System.Collections;
using UnityEngine;

namespace ModAPI.Core
{
    /// <summary>
    /// Scheduling helpers for common plugin-context timing needs.
    /// Use these when code must wait for a scene or a few Unity frames before touching scene objects.
    /// </summary>
    public static class ContextExtensions
    {
        /// <summary>
        /// Runs the action after the named scene is loaded, or next frame when that scene is already active.
        /// The helper is hosted under <see cref="IPluginContext.LoaderRoot"/> so callers do not need their own MonoBehaviour.
        /// </summary>
        public static void RunWhenSceneReady(this IPluginContext ctx, string sceneName, Action action)
        {
            if (ctx == null || ctx.LoaderRoot == null || string.IsNullOrEmpty(sceneName) || action == null) return;
            var host = EnsureHelper(ctx.LoaderRoot);
            host.RunWhenSceneReady(sceneName, action);
        }

        /// <summary>
        /// Runs an action on the Unity main thread after the requested number of frames.
        /// Use this to wait for legacy UI or scene objects to finish one-frame initialization.
        /// </summary>
        public static void RunAfterFrames(this IPluginContext ctx, int frames, Action action)
        {
            if (ctx == null || action == null || frames < 0) return;
            IEnumerator Routine()
            {
                for (int i = 0; i < frames; i++) yield return null;
                try { action(); } catch (Exception ex) { MMLog.Write("RunAfterFrames failed: " + ex.Message); }
            }
            ctx.StartCoroutine(Routine());
        }

        private static ContextHelper EnsureHelper(GameObject root)
        {
            var existing = root.GetComponent<ContextHelper>();
            if (existing != null) return existing;
            return root.AddComponent<ContextHelper>();
        }

        private class ContextHelper : MonoBehaviour
        {
            private readonly System.Collections.Generic.List<Item> _items = new System.Collections.Generic.List<Item>();
            private PluginRunner _runner;

            private void Awake()
            {
                _runner = GetComponent<PluginRunner>();
                if (_runner == null)
                {
                    MMLog.WriteError("ContextHelper requires a PluginRunner on the same GameObject.");
                    return;
                }
                _runner.SceneLoaded += OnSceneLoaded;
            }

            private void OnDestroy()
            {
                if (_runner != null)
                {
                    _runner.SceneLoaded -= OnSceneLoaded;
                }
            }

            public void RunWhenSceneReady(string sceneName, Action action)
            {
                // Already loaded?
                try
                {
                    if (ModAPI.SceneUtil.GetCurrentSceneName() == sceneName)
                    {
                        // If the target scene is already loaded and active, run the action immediately (or next frame)
                        // without waiting for a scene load event, optimizing execution.
                        StartCoroutine(RunNextFrame(action));
                        return;
                    }
                }
                catch (Exception ex) { MMLog.WarnOnce("ContextExtensions.RunWhenSceneReady.Check", "Failed to check current scene: " + ex.Message); }

                _items.Add(new Item { SceneName = sceneName, Action = action, Deadline = Time.realtimeSinceStartup + 60f });
            }

            private void OnSceneLoaded(string sceneName)
            {
                if (_items.Count == 0) return;
                for (int i = _items.Count - 1; i >= 0; i--)
                {
                    var it = _items[i];
                    if (sceneName == it.SceneName)
                    {
                        _items.RemoveAt(i);
                        StartCoroutine(RunNextFrame(it.Action));
                    }
                    else if (Time.realtimeSinceStartup > it.Deadline)
                    {
                        _items.RemoveAt(i);
                        MMLog.WriteDebug("RunWhenSceneReady timed out for " + it.SceneName);
                    }
                }
            }

            private IEnumerator RunNextFrame(Action a)
            {            yield return null; // ensure scene objects are initialized
                try { a(); } catch (Exception ex) { MMLog.Write("RunWhenSceneReady failed: " + ex.Message); }
            }

            private struct Item { public string SceneName; public Action Action; public float Deadline; }
        }
    }
}
