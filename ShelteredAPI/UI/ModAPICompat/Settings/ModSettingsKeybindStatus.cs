using System;
using ModAPI.Spine;
using UnityEngine;

namespace ModAPI.Internal.UI
{
    internal delegate UILabel ModSettingsLabelFactory(
        Transform parent,
        string name,
        string text,
        Vector3 pos,
        int fontSize,
        Color color,
        UIFont uiFont,
        Font ttfFont,
        int depth);

    internal static class ModSettingsKeybindStatusReporter
    {
        private static Action<string, bool> _report;

        internal static void Attach(Action<string, bool> report)
        {
            _report = report;
        }

        internal static void Detach(Action<string, bool> report)
        {
            if (_report == report)
                _report = null;
        }

        internal static void Report(string message, bool warning)
        {
            if (_report != null)
                _report(message, warning);
        }
    }

    internal sealed class ModSettingsKeybindStatusController
    {
        private const float StatusMessageSeconds = 5f;
        private const string DefaultStatusText = "Click a key slot to rebind it. Escape cancels capture; RESET restores reserved defaults.";

        private static readonly Color SubtextColor = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color OkColor = new Color(0.62f, 0.86f, 0.62f, 1f);
        private static readonly Color WarningColor = new Color(0.95f, 0.72f, 0.38f, 1f);

        private UILabel _label;
        private float _messageUntil = -1f;

        internal void Build(
            Transform root,
            Vector3 position,
            UIFont uiFont,
            Font ttfFont,
            ModSettingsLabelFactory createLabel)
        {
            if (root == null || createLabel == null) return;

            _label = createLabel(root, "Status", DefaultStatusText, position, 15, SubtextColor, uiFont, ttfFont, 610);
            _label.alignment = NGUIText.Alignment.Center;
            _label.pivot = UIWidget.Pivot.Center;
            _label.width = 820;
            _label.height = 24;
            _label.multiLine = false;
            _label.overflowMethod = UILabel.Overflow.ClampContent;
        }

        internal void Report(string message, bool warning)
        {
            if (_label == null) return;

            if (string.IsNullOrEmpty(message))
                message = DefaultStatusText;

            _label.text = message;
            _label.color = warning ? WarningColor : OkColor;
            _messageUntil = Time.realtimeSinceStartup + StatusMessageSeconds;
        }

        internal void Update()
        {
            if (_label == null || _messageUntil < 0f) return;
            if (Time.realtimeSinceStartup < _messageUntil) return;

            _label.text = DefaultStatusText;
            _label.color = SubtextColor;
            _messageUntil = -1f;
        }
    }
}
