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
            UITexture bg = _ui.CreateQuad(parent, name + "Bg", _textures.Keycap(width, height, KeycapState.Rest),
                position, width, height, Color.white, _ui.NextDepth());
            UILabel label = _ui.CreateLabel(parent, name + "Label", text,
                position, fontSize, _palette.Ink,
                width - 20, height - 6, NGUIText.Alignment.Center, UIWidget.Pivot.Center, _ui.NextDepth());
            label.overflowMethod = UILabel.Overflow.ShrinkContent;
            _ui.AddClickCollider(bg.gameObject, width, height, onClick);
            return bg.gameObject;
        }
    }
}
