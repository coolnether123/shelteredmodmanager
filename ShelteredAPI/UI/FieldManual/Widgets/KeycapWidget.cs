using System;
using UnityEngine;
using ShelteredAPI.UI.FieldManual.Primitives;
using ShelteredAPI.UI.FieldManual.Textures;
using ShelteredAPI.UI.FieldManual.Theme;

namespace ShelteredAPI.UI.FieldManual.Widgets
{
    /// <summary>
    /// A Sheltered-style binding slot. Owns its background texture and label, swaps
    /// textures based on hover/capture state, and pulses while capturing.
    /// </summary>
    internal sealed class KeycapWidget : MonoBehaviour
    {
        public GameObject GameObjectRef { get { return gameObject; } }
        public UILabel ValueLabel { get; private set; }

        private UITexture _bg;
        private ITextureLibrary _textures;
        private IThemePalette _palette;
        private int _width;
        private int _height;

        private KeycapState _state = KeycapState.Rest;
        private bool _hovered;
        private bool _pulsing;
        private float _pulseT;

        public static KeycapWidget Create(
            GameObject parent,
            string name,
            Vector3 localPosition,
            int width,
            int height,
            string initialText,
            ITextureLibrary textures,
            IThemePalette palette,
            UIPrimitiveFactory ui,
            Action onClick)
        {
            GameObject host = ui.CreateChild(parent, name, localPosition);

            int bgDepth = ui.NextDepth();
            UITexture bg = ui.CreateQuad(host, "Cap", textures.Keycap(width, height, KeycapState.Rest), Vector3.zero,
                width, height, Color.white, bgDepth);

            int labelDepth = ui.NextDepth();
            UILabel label = ui.CreateLabel(host, "Value", initialText ?? string.Empty,
                Vector3.zero, 14, palette.KeycapInk,
                Mathf.Max(40, width - 12), Mathf.Max(20, height - 6),
                NGUIText.Alignment.Center, UIWidget.Pivot.Center, labelDepth);
            label.overflowMethod = UILabel.Overflow.ShrinkContent;

            ui.AddClickCollider(host, width, height, onClick);

            KeycapWidget cap = host.AddComponent<KeycapWidget>();
            cap._bg = bg;
            cap.ValueLabel = label;
            cap._textures = textures;
            cap._palette = palette;
            cap._width = width;
            cap._height = height;
            cap.SetText(initialText);
            return cap;
        }

        public void SetText(string text)
        {
            if (ValueLabel == null) return;
            bool empty = string.IsNullOrEmpty(text) || string.Equals(text, "UNBOUND", StringComparison.OrdinalIgnoreCase);
            ValueLabel.text = empty ? "--" : text;
            ApplyVisualState(empty ? KeycapState.Empty : (_pulsing ? KeycapState.Pulse : (_hovered ? KeycapState.Hover : KeycapState.Rest)));
        }

        public void StartPulse()
        {
            _pulsing = true;
            _pulseT = 0f;
            ApplyVisualState(KeycapState.Pulse);
        }

        public void StopPulse()
        {
            _pulsing = false;
            if (_bg != null) _bg.color = Color.white;
            ApplyVisualState(_hovered ? KeycapState.Hover : KeycapState.Rest);
        }

        private void OnHover(bool isOver)
        {
            _hovered = isOver;
            if (_pulsing) return;
            ApplyVisualState(string.IsNullOrEmpty(ValueLabel.text) || ValueLabel.text == "--"
                ? KeycapState.Empty
                : (isOver ? KeycapState.Hover : KeycapState.Rest));
        }

        private void Update()
        {
            if (!_pulsing || _bg == null) return;
            _pulseT += Time.unscaledDeltaTime * 4f;
            float a = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(_pulseT));
            _bg.color = new Color(1f, 1f, 1f, a);
        }

        private void ApplyVisualState(KeycapState newState)
        {
            if (_bg == null) return;
            if (newState == _state && newState != KeycapState.Pulse) return;
            _state = newState;
            _bg.mainTexture = _textures.Keycap(_width, _height, newState);
            ValueLabel.color = newState == KeycapState.Pulse ? _palette.Paper : _palette.KeycapInk;
        }
    }
}
