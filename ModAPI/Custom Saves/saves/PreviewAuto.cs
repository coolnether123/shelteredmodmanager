using System.Collections;
using UnityEngine;
using ModAPI.Core;

namespace ModAPI.Saves
{
    internal class PreviewAutoRunner : MonoBehaviour { }

    public static class PreviewAuto
    {
        private static bool _hooked;

        public static void EnsureHooked()
        {
            if (_hooked) return;
            _hooked = true;
            Events.OnAfterSave += OnAfterSave;
        }

        private static void OnAfterSave(SaveEntry entry)
        {
            // Best-effort screen capture end-of-frame
            try
            {
                var go = new GameObject("PreviewAutoRunner");
                Object.DontDestroyOnLoad(go);
                var runner = go.AddComponent<PreviewAutoRunner>();
                runner.StartCoroutine(CaptureCoroutine(entry));
            }
            catch { }
        }

        private static IEnumerator CaptureCoroutine(SaveEntry entry)
        {
            yield return new WaitForEndOfFrame();
            var width = Screen.width; var height = Screen.height;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            PreviewCapture.CapturePNG(entry.scenarioId, entry.id, tex);
            Object.Destroy(tex);
            MMLog.WriteDebug($"Preview captured for scenario={entry.scenarioId} id={entry.id} size={width}x{height}");
        }
    }
}
