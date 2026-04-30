using System.Collections.Generic;
using UnityEngine;

namespace ShelteredAPI.UI.FieldManual.Primitives
{
    /// <summary>
    /// Reveals child GameObjects (and adjusts their alpha) only while the pointer hovers
    /// the host collider. Used to hide clear/reset icons until a keybind row is hovered.
    /// </summary>
    internal sealed class HoverReveal : MonoBehaviour
    {
        private readonly List<UIWidget> _widgets = new List<UIWidget>();
        private readonly List<float> _shownAlpha = new List<float>();
        private bool _hovered;

        public void Register(UIWidget widget, float shownAlpha = 1f)
        {
            if (widget == null) return;
            _widgets.Add(widget);
            _shownAlpha.Add(shownAlpha);
            widget.alpha = 0f;
        }

        public void ForceHover(bool value)
        {
            _hovered = value;
            ApplyAlpha();
        }

        private void OnHover(bool isOver)
        {
            _hovered = isOver;
            ApplyAlpha();
        }

        private void ApplyAlpha()
        {
            for (int i = 0; i < _widgets.Count; i++)
            {
                if (_widgets[i] == null) continue;
                _widgets[i].alpha = _hovered ? _shownAlpha[i] : 0f;
            }
        }
    }
}
