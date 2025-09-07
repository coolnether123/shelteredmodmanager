using System;
using System.IO;
using UnityEngine;

namespace ModAPI.Saves
{
    public static class PreviewCapture
    {
        public static void CapturePNG(string scenarioId, string saveId, Texture2D frame)
        {
            if (frame == null) return;
            try
            {
                var path = DirectoryProvider.PreviewPath(scenarioId, saveId);
                var bytes = frame.EncodeToPNG();
                File.WriteAllBytes(path, bytes);
            }
            catch (System.Exception ex)
            {
                MMLog.Write("PreviewCapture error: " + ex.Message);
            }
        }
    }
}

