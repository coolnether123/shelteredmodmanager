using ShelteredAPI.UI.Compatibility;
using UnityEngine;
using ShelteredAPI.Content;
using ShelteredAPI.UI.Runtime;
namespace ShelteredAPI.UI.Internal.Runtime.Widgets{
    internal static class RuntimeWidgetUtil
    {
        public static GameObject CreateChild(GameObject parent, string name, Vector3 localPosition)
        {
            if (parent == null)
                return null;

            GameObject child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            child.transform.localPosition = localPosition;
            child.transform.localScale = Vector3.one;
            child.layer = parent.layer;
            return child;
        }

        public static UITexture CreateBox(GameObject parent, string name, int width, int height, Color color, Vector3 localPosition, int depth)
        {
            GameObject child = CreateChild(parent, name, localPosition);
            if (child == null)
                return null;

            UITexture texture = child.AddComponent<UITexture>();
            texture.mainTexture = UIUtil.WhiteTexture;
            Shader shader = Shader.Find("Unlit/Transparent Colored");
            if (shader != null)
                texture.shader = shader;
            texture.width = width;
            texture.height = height;
            texture.color = color;
            texture.depth = depth;
            return texture;
        }

        public static UILabel CreateLabel(GameObject parent, string text, int width, int height, int fontSize, Vector3 localPosition, NGUIText.Alignment alignment, int depth)
        {
            return CreateLabel(parent, text, width, height, fontSize, localPosition, alignment, depth, null);
        }

        public static UILabel CreateLabel(GameObject parent, string text, int width, int height, int fontSize, Vector3 localPosition, NGUIText.Alignment alignment, int depth, Color? color)
        {
            UIPanel usedPanel;
            UILabel label = UIUtil.CreateLabel(parent, new UIUtil.UILabelOptions
            {
                text = text ?? string.Empty,
                fontSize = fontSize,
                localPosition = localPosition,
                alignment = alignment,
                depth = depth,
                absoluteDepth = depth,
                resizeFreely = false
            }, out usedPanel);

            if (label != null)
            {
                label.width = width;
                label.height = height;
                label.overflowMethod = UILabel.Overflow.ShrinkContent;
                if (color.HasValue)
                    label.color = color.Value;
            }

            return label;
        }

        public static UI2DSprite CreateSprite(GameObject parent, string name, Sprite sprite, int width, int height, Vector3 localPosition, int depth)
        {
            if (sprite == null)
                return null;

            GameObject child = CreateChild(parent, name, localPosition);
            if (child == null)
                return null;

            UI2DSprite uiSprite = child.AddComponent<UI2DSprite>();
            uiSprite.sprite2D = sprite;
            uiSprite.width = width;
            uiSprite.height = height;
            uiSprite.depth = depth;
            uiSprite.MakePixelPerfect();
            uiSprite.width = width;
            uiSprite.height = height;
            return uiSprite;
        }

        public static void EnsureCollider(GameObject go, int width, int height)
        {
            if (go == null)
                return;

            BoxCollider collider = go.GetComponent<BoxCollider>();
            if (collider == null)
                collider = go.AddComponent<BoxCollider>();

            collider.size = new Vector3(width, height, 1f);
            collider.center = Vector3.zero;
            collider.isTrigger = true;
        }

        public static void DestroyChildren(GameObject parent)
        {
            if (parent == null)
                return;

            for (int i = parent.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.transform.GetChild(i);
                if (child != null)
                    Object.Destroy(child.gameObject);
            }
        }
    }
}
