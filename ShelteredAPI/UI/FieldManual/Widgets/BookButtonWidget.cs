using System;
using ShelteredAPI.UI.FieldManual.Primitives;
using ShelteredAPI.UI.FieldManual.Textures;
using ShelteredAPI.UI.FieldManual.Theme;
using UnityEngine;

namespace ShelteredAPI.UI.FieldManual.Widgets
{
    /// <summary>
    /// Builds Sheltered book-style command buttons from local procedural assets.
    /// Field Manual windows must not clone live front-end controls because they can
    /// carry scene-specific state into unrelated screens.
    /// </summary>
    internal sealed class BookButtonWidget
    {
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
            Texture2D restTexture = _textures.Keycap(width, height, KeycapState.Rest);
            Texture2D hoverTexture = _textures.Keycap(width, height, KeycapState.Hover);
            GameObject root = _ui.CreateChild(parent, name, position);
            UITexture bg = _ui.CreateQuad(root, name + "Bg", restTexture,
                Vector3.zero, width, height, Color.white, _ui.NextDepth());
            UILabel label = _ui.CreateLabel(root, name + "Label", text,
                Vector3.zero, fontSize, _palette.Ink,
                width - 20, height - 6, NGUIText.Alignment.Center, UIWidget.Pivot.Center, _ui.NextDepth());
            label.overflowMethod = UILabel.Overflow.ShrinkContent;
            _ui.AddClickCollider(root, width, height, onClick);
            HoverVisualState hover = root.AddComponent<HoverVisualState>();
            hover.TextureTarget = bg;
            hover.RestTexture = restTexture;
            hover.HoverTexture = hoverTexture;
            hover.Widgets = new UIWidget[] { label };
            hover.RestColors = new Color[] { _palette.Ink };
            hover.HoverColors = new Color[] { _palette.KeycapInk };
            hover.ScaleTarget = root.transform;
            hover.RestScale = 1f;
            hover.HoverScale = 1.035f;
            return root;
        }
    }
}
