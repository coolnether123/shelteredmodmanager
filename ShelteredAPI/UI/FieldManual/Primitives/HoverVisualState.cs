using UnityEngine;

namespace ShelteredAPI.UI.FieldManual.Primitives
{
    internal sealed class HoverVisualState : MonoBehaviour
    {
        public UIWidget[] Widgets;
        public Color[] RestColors;
        public Color[] HoverColors;
        public UITexture TextureTarget;
        public Texture RestTexture;
        public Texture HoverTexture;
        public Transform ScaleTarget;
        public float RestScale = 1f;
        public float HoverScale = 1.02f;

        private void OnHover(bool isOver)
        {
            Apply(isOver);
        }

        private void OnDisable()
        {
            Apply(false);
        }

        private void Apply(bool hovered)
        {
            if (TextureTarget != null)
                TextureTarget.mainTexture = hovered && HoverTexture != null ? HoverTexture : RestTexture;

            if (ScaleTarget != null)
            {
                float scale = hovered ? HoverScale : RestScale;
                ScaleTarget.localScale = new Vector3(scale, scale, 1f);
            }

            if (Widgets == null)
                return;

            for (int i = 0; i < Widgets.Length; i++)
            {
                UIWidget widget = Widgets[i];
                if (widget == null)
                    continue;

                Color[] colors = hovered ? HoverColors : RestColors;
                if (colors != null && i < colors.Length)
                    widget.color = colors[i];
            }
        }
    }
}
