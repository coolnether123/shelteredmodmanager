using System;
using ShelteredAPI.UI.Compatibility;
using ShelteredAPI.UI.FieldManual.Primitives;
using ShelteredAPI.UI.FieldManual.Textures;
using ShelteredAPI.UI.FieldManual.Theme;
using ShelteredAPI.UI.Internal;
using UnityEngine;

namespace ShelteredAPI.UI.FieldManual.Widgets
{
    /// <summary>
    /// Builds Sheltered book-style command buttons by cloning the scenario template when
    /// available and falling back to the local procedural button texture otherwise.
    /// </summary>
    internal sealed class BookButtonWidget
    {
        private static readonly Color DefaultColor = new Color(0.88f, 0.76f, 0.63f, 1f);
        private static readonly Color HoverColor = new Color(0.97f, 0.85f, 0.70f, 1f);
        private static readonly Color PressedColor = new Color(0.74f, 0.61f, 0.49f, 1f);
        private static readonly Color DisabledColor = new Color(0.52f, 0.45f, 0.39f, 0.95f);

        private readonly IThemePalette _palette;
        private readonly ITextureLibrary _textures;
        private readonly UIPrimitiveFactory _ui;

        public BookButtonWidget(IThemePalette palette, ITextureLibrary textures, UIPrimitiveFactory ui)
        {
            _palette = palette;
            _textures = textures;
            _ui = ui;
        }

        public GameObject Build(GameObject parent, string name, string text, Vector3 position, int width, int height, int fontSize, Action onClick)
        {
            GameObject cloned = TryBuildFromScenarioTemplate(parent, name, text, position, width, height, fontSize, onClick);
            if (cloned != null)
                return cloned;

            return BuildFallback(parent, name, text, position, width, height, fontSize, onClick);
        }

        private GameObject TryBuildFromScenarioTemplate(GameObject parent, string name, string text, Vector3 position, int width, int height, int fontSize, Action onClick)
        {
            UIButton template = ModManagerPanelScaffolding.FindScenarioButtonTemplate();
            if (template == null)
                return null;

            UIButton cloned = UIUtil.CloneButton(template, parent.transform, text);
            if (cloned == null || cloned.gameObject == null)
                return null;

            GameObject go = cloned.gameObject;
            go.name = name;
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            go.SetActive(true);

            ConfigureClonedButton(go, text, width, height, fontSize, onClick);
            return go;
        }

        private GameObject BuildFallback(GameObject parent, string name, string text, Vector3 position, int width, int height, int fontSize, Action onClick)
        {
            UITexture bg = _ui.CreateQuad(parent, name + "Bg", _textures.Keycap(width, height, KeycapState.Rest),
                position, width, height, Color.white, _ui.NextDepth());
            UILabel label = _ui.CreateLabel(parent, name + "Label", text,
                position, fontSize, _palette.Ink,
                width - 20, height - 6, NGUIText.Alignment.Center, UIWidget.Pivot.Center, _ui.NextDepth());
            label.overflowMethod = UILabel.Overflow.ShrinkContent;
            _ui.AddClickCollider(bg.gameObject, width, height, onClick);
            return bg.gameObject;
        }

        private void ConfigureClonedButton(GameObject go, string text, int width, int height, int fontSize, Action onClick)
        {
            SetButtonWidgetDepths(go);
            ResizeBackground(go, width, height);
            ConfigureLabels(go, text, width, height, fontSize);
            ConfigureButtonColors(go);
            ConfigureCollider(go, width, height, onClick);
        }

        private void SetButtonWidgetDepths(GameObject go)
        {
            int bgDepth = _ui.NextDepth();
            int labelDepth = _ui.NextDepth();

            UIWidget[] widgets = go.GetComponentsInChildren<UIWidget>(true);
            for (int i = 0; i < widgets.Length; i++)
            {
                UIWidget widget = widgets[i];
                if (widget == null) continue;
                widget.depth = widget is UILabel ? labelDepth : bgDepth;
            }
        }

        private static void ResizeBackground(GameObject go, int width, int height)
        {
            UIWidget backgroundWidget = FindLargestNonLabelWidget(go);
            if (backgroundWidget == null)
                return;

            backgroundWidget.width = width;
            backgroundWidget.height = height;
        }

        private void ConfigureLabels(GameObject go, string text, int width, int height, int fontSize)
        {
            UILabel primaryLabel = FindPrimaryLabel(go);
            UILabel[] labels = go.GetComponentsInChildren<UILabel>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                UILabel label = labels[i];
                if (label == null) continue;

                bool primary = label == primaryLabel;
                label.enabled = primary;
                label.text = primary ? text ?? string.Empty : string.Empty;
                label.fontSize = fontSize;
                label.width = width - 20;
                label.height = height - 8;
                label.color = _palette.Ink;
                label.alignment = NGUIText.Alignment.Center;
                label.pivot = UIWidget.Pivot.Center;
                label.overflowMethod = UILabel.Overflow.ShrinkContent;
                label.effectStyle = UILabel.Effect.None;
                label.transform.localPosition = Vector3.zero;
            }
        }

        private static void ConfigureButtonColors(GameObject go)
        {
            UIButton button = go.GetComponent<UIButton>();
            if (button == null)
                return;

            if (button.onClick != null) button.onClick.Clear();
            button.tweenTarget = go;
            button.defaultColor = DefaultColor;
            button.hover = HoverColor;
            button.pressed = PressedColor;
            button.disabledColor = DisabledColor;
            button.duration = 0.08f;
            button.SetState(UIButtonColor.State.Normal, true);
        }

        private static void ConfigureCollider(GameObject go, int width, int height, Action onClick)
        {
            BoxCollider collider = go.GetComponent<BoxCollider>();
            if (collider == null) collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(width, height, 1f);
            collider.center = Vector3.zero;

            UIEventListener listener = UIEventListener.Get(go);
            listener.onClick = delegate(GameObject clicked)
            {
                if (onClick != null) onClick();
            };

            NGUITools.UpdateWidgetCollider(go, true);
        }

        private static UIWidget FindLargestNonLabelWidget(GameObject go)
        {
            UIWidget[] widgets = go.GetComponentsInChildren<UIWidget>(true);
            UIWidget best = null;
            int bestArea = int.MinValue;
            for (int i = 0; i < widgets.Length; i++)
            {
                UIWidget widget = widgets[i];
                if (widget == null || widget is UILabel) continue;

                int area = widget.width * widget.height;
                if (best == null || area > bestArea)
                {
                    best = widget;
                    bestArea = area;
                }
            }

            return best;
        }

        private static UILabel FindPrimaryLabel(GameObject go)
        {
            UILabel[] labels = go.GetComponentsInChildren<UILabel>(true);
            UILabel best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < labels.Length; i++)
            {
                UILabel label = labels[i];
                if (label == null) continue;

                int score = Math.Max(label.width, label.fontSize);
                if (best == null || score > bestScore)
                {
                    best = label;
                    bestScore = score;
                }
            }

            return best;
        }
    }
}
