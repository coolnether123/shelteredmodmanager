using System;
using ShelteredAPI.UI.Compatibility;
using UnityEngine;

namespace ShelteredAPI.UI
{
    internal static class NguiModalPrimitives
    {
        public static GameObject CreateRoot(UIPanel panel, string name)
        {
            if (panel == null)
                return null;

            GameObject root = new GameObject(name);
            root.transform.SetParent(panel.transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localScale = Vector3.one;
            root.layer = panel.gameObject.layer;
            return root;
        }

        public static GameObject CreateBox(Transform parent, string name, Vector3 position, int width, int height, Color color, int depth, bool addCollider)
        {
            GameObject box = new GameObject(name);
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.layer = parent.gameObject.layer;

            UITexture texture = box.AddComponent<UITexture>();
            texture.mainTexture = UIUtil.WhiteTexture;
            texture.width = width;
            texture.height = height;
            texture.depth = depth;
            texture.color = color;

            if (addCollider)
            {
                BoxCollider collider = box.AddComponent<BoxCollider>();
                collider.size = new Vector3(width, height, 1f);
            }

            return box;
        }

        public static UILabel CreateLabel(
            Transform parent,
            string name,
            string text,
            Vector3 position,
            int fontSize,
            Color color,
            UIFont bitmapFont,
            Font trueTypeFont,
            int depth)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.layer = parent.gameObject.layer;

            UILabel label = go.AddComponent<UILabel>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.depth = depth;
            label.bitmapFont = bitmapFont;
            label.trueTypeFont = trueTypeFont;
            label.overflowMethod = UILabel.Overflow.ResizeFreely;
            return label;
        }

        public static GameObject CreateButton(
            Transform parent,
            string name,
            string text,
            Vector3 position,
            int width,
            int height,
            UIFont bitmapFont,
            Font trueTypeFont,
            Color backgroundColor,
            Color textColor,
            Action onClick)
        {
            return CreateButton(
                parent,
                name,
                text,
                position,
                width,
                height,
                16,
                bitmapFont,
                trueTypeFont,
                backgroundColor,
                textColor,
                onClick);
        }

        public static GameObject CreateButton(
            Transform parent,
            string name,
            string text,
            Vector3 position,
            int width,
            int height,
            int fontSize,
            UIFont bitmapFont,
            Font trueTypeFont,
            Color backgroundColor,
            Color textColor,
            Action onClick)
        {
            GameObject button = new GameObject(name);
            button.transform.SetParent(parent, false);
            button.transform.localPosition = position;
            button.layer = parent.gameObject.layer;

            UITexture background = button.AddComponent<UITexture>();
            background.mainTexture = UIUtil.WhiteTexture;
            background.width = width;
            background.height = height;
            background.depth = 100;
            // Treat the supplied position as the visual center. NGUI defaults
            // can vary across the game's bundled versions, which otherwise
            // shifts modal buttons toward their bottom-right corner.
            background.pivot = UIWidget.Pivot.Center;
            background.color = backgroundColor;

            UILabel label = CreateLabel(button.transform, "Label", text, Vector3.zero, fontSize, textColor, bitmapFont, trueTypeFont, 101);
            label.alignment = NGUIText.Alignment.Center;
            label.pivot = UIWidget.Pivot.Center;
            label.width = width - 20;
            label.height = height - 8;
            label.multiLine = false;
            label.overflowMethod = UILabel.Overflow.ShrinkContent;
            // UILabel adjusts its transform while recalculating glyph bounds.
            // Restore the requested visual centre after every sizing option has
            // been applied so bitmap-font metrics cannot move the caption.
            label.transform.localPosition = Vector3.zero;

            BoxCollider collider = button.AddComponent<BoxCollider>();
            collider.size = new Vector3(width, height, 1f);

            UIEventListener listener = UIEventListener.Get(button);
            listener.onClick = _ =>
            {
                if (onClick != null)
                    onClick();
            };

            UIButton uiButton = button.AddComponent<UIButton>();
            uiButton.tweenTarget = button;
            uiButton.isEnabled = onClick != null;
            return button;
        }
    }
}
