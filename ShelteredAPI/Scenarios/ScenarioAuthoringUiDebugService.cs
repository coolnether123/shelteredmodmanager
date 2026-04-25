using System;
using System.Collections.Generic;
using System.Text;
using ModAPI.Core;
using ModAPI.UI;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioAuthoringUiDebugService
    {
        internal struct LayoutRect
        {
            public string Name;
            public Rect Rect;
            public string Detail;
        }

        private static readonly ScenarioAuthoringUiDebugService _instance = new ScenarioAuthoringUiDebugService();
        private string _lastSignature;

        public static ScenarioAuthoringUiDebugService Instance
        {
            get { return _instance; }
        }

        private ScenarioAuthoringUiDebugService()
        {
        }

        public void LogLayout(string signature, IList<LayoutRect> rects)
        {
            if (string.IsNullOrEmpty(signature)
                || string.Equals(_lastSignature, signature, StringComparison.Ordinal)
                || rects == null
                || rects.Count == 0)
            {
                return;
            }

            _lastSignature = signature;

            StringBuilder builder = new StringBuilder();
            builder.Append("[ScenarioAuthoringUIDebug] Layout updated");
            for (int i = 0; i < rects.Count; i++)
            {
                LayoutRect entry = rects[i];
                builder.Append(" | ")
                    .Append(entry.Name ?? "rect")
                    .Append("=")
                    .Append(FormatRect(entry.Rect));
                if (!string.IsNullOrEmpty(entry.Detail))
                    builder.Append(" ").Append(entry.Detail);
            }

            MMLog.WriteInfo(builder.ToString());

            if (!UIDebug.Enabled)
                return;

            UIDebug.ResetTiming();
            for (int i = 0; i < rects.Count; i++)
            {
                LayoutRect entry = rects[i];
                UIDebug.LogTimed((entry.Name ?? "rect") + " " + FormatRect(entry.Rect)
                    + (string.IsNullOrEmpty(entry.Detail) ? string.Empty : " | " + entry.Detail));
            }
        }

        public static LayoutRect Capture(string name, Rect rect, string detail)
        {
            return new LayoutRect
            {
                Name = name,
                Rect = rect,
                Detail = detail
            };
        }

        private static string FormatRect(Rect rect)
        {
            return "("
                + Mathf.RoundToInt(rect.x) + ","
                + Mathf.RoundToInt(rect.y) + " "
                + Mathf.RoundToInt(rect.width) + "x"
                + Mathf.RoundToInt(rect.height) + ")";
        }
    }
}
