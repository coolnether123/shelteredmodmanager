using System;
using System.Collections;
using UnityEngine;

namespace ModAPI.UI
{
    public static class ContextUIExtensions
    {
        /// <summary>
        /// Waits until a GameObject at name/path exists and is activeInHierarchy,
        /// then invokes 'action(go)'. Times out after 'timeoutSec' seconds.
        /// </summary>
        public static void RunWhenPanelVisible(this IPluginContext ctx,
            string nameOrPath, Action<GameObject> action, float timeoutSec = 60f)
        {
            if (ctx == null || string.IsNullOrEmpty(nameOrPath) || action == null)
                return;

            IEnumerator Routine()
            {
                float deadline = Time.realtimeSinceStartup + timeoutSec;
                while (Time.realtimeSinceStartup < deadline)
                {
                    GameObject go = null;
                    try { go = ModAPI.Util.SceneUtil.Find(nameOrPath); } catch (Exception ex) { MMLog.WarnOnce("ContextUIExtensions.RunWhenPanelVisible.Find", "SceneUtil.Find failed: " + ex.Message); }
                    if (go != null && go.activeInHierarchy)
                    {
                        try { action(go); }
                        catch (Exception ex)
                        {
                            MMLog.Write("RunWhenPanelVisible action failed: " + ex.Message);
                        }
                        yield break;
                    }
                    yield return null; // next frame
                }
                MMLog.WriteDebug("RunWhenPanelVisible timeout for: " + nameOrPath);
            }

            ctx.StartCoroutine(Routine());
        }

        /// <summary>
        /// Convenience: waits for panel, then creates label under it using UIUtil.
        /// Returns immediately; work is done asynchronously.
        /// </summary>
        public static void AddLabelToPanelWhenVisible(this IPluginContext ctx,
            string nameOrPath, string text, UIUtil.UILabelOptions opts = null)
        {
            if (opts == null) opts = new UIUtil.UILabelOptions();
            opts.text = text;

            ctx.RunWhenPanelVisible(nameOrPath, go =>
            {
                UIPanel used;
                var label = UIUtil.CreateLabel(go, opts, out used);
                if (label == null)
                    MMLog.Write("AddLabelToPanelWhenVisible: failed to create label.");
            });
        }
    }
}