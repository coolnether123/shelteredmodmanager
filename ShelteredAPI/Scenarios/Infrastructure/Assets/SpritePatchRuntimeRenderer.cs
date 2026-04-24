using ModAPI.Scenarios;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class SpritePatchRuntimeRenderer
    {
        public Sprite Render(Sprite baseSprite, SpritePatchDefinition patch)
        {
            if (baseSprite == null || patch == null)
                return null;

            Texture2D sourceTexture = baseSprite.texture;
            if (sourceTexture == null)
                return null;

            Rect sourceRect = baseSprite.textureRect;
            int width = Mathf.Max(1, Mathf.RoundToInt(sourceRect.width));
            int height = Mathf.Max(1, Mathf.RoundToInt(sourceRect.height));
            Texture2D working = new Texture2D(width, height, TextureFormat.ARGB32, false);
            working.filterMode = FilterMode.Point;
            working.wrapMode = TextureWrapMode.Clamp;

            if (!CopySpritePixels(sourceTexture, sourceRect, working))
                return null;

            for (int operationIndex = 0; operationIndex < patch.Operations.Count; operationIndex++)
            {
                SpritePatchOperation operation = patch.Operations[operationIndex];
                if (operation == null)
                    continue;

                if (operation.Kind == SpritePatchOperationKind.Clear)
                {
                    Clear(working);
                    continue;
                }

                for (int runIndex = 0; operation.Runs != null && runIndex < operation.Runs.Count; runIndex++)
                {
                    SpritePatchDeltaRun run = operation.Runs[runIndex];
                    ApplyRun(working, run);
                }
            }

            working.Apply();
            Rect rect = new Rect(0f, 0f, working.width, working.height);
            Vector2 pivot = new Vector2(baseSprite.pivot.x / rect.width, baseSprite.pivot.y / rect.height);
            Sprite sprite = Sprite.Create(working, rect, pivot, baseSprite.pixelsPerUnit);
            if (sprite != null)
                sprite.name = string.IsNullOrEmpty(patch.Id) ? baseSprite.name : patch.Id;
            return sprite;
        }

        private static bool CopySpritePixels(Texture2D sourceTexture, Rect sourceRect, Texture2D target)
        {
            try
            {
                Color[] pixels = sourceTexture.GetPixels(
                    Mathf.RoundToInt(sourceRect.x),
                    Mathf.RoundToInt(sourceRect.y),
                    target.width,
                    target.height);
                target.SetPixels(pixels);
                target.Apply();
                return true;
            }
            catch
            {
                RenderTexture renderTexture = RenderTexture.GetTemporary(target.width, target.height, 0, RenderTextureFormat.ARGB32);
                RenderTexture previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = renderTexture;
                    GL.Clear(true, true, Color.clear);
                    GL.PushMatrix();
                    GL.LoadPixelMatrix(0f, target.width, target.height, 0f);
                    Rect uv = new Rect(
                        sourceRect.x / sourceTexture.width,
                        sourceRect.y / sourceTexture.height,
                        sourceRect.width / sourceTexture.width,
                        sourceRect.height / sourceTexture.height);
                    Graphics.DrawTexture(new Rect(0f, 0f, target.width, target.height), sourceTexture, uv, 0, 0, 0, 0);
                    GL.PopMatrix();
                    target.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
                    target.Apply();
                    return true;
                }
                finally
                {
                    RenderTexture.active = previous;
                    RenderTexture.ReleaseTemporary(renderTexture);
                }
            }
        }

        private static void Clear(Texture2D texture)
        {
            Color[] clear = new Color[texture.width * texture.height];
            for (int i = 0; i < clear.Length; i++)
                clear[i] = new Color32(0, 0, 0, 0);
            texture.SetPixels(clear);
        }

        private static void ApplyRun(Texture2D texture, SpritePatchDeltaRun run)
        {
            if (texture == null || run == null || !run.IsValid())
                return;

            Color color = DecodeColor(run.ColorHex);
            for (int offset = 0; offset < run.Length; offset++)
            {
                int x = run.X + offset;
                if (x >= 0 && x < texture.width && run.Y >= 0 && run.Y < texture.height)
                    texture.SetPixel(x, run.Y, color);
            }
        }

        private static Color DecodeColor(string colorHex)
        {
            if (string.IsNullOrEmpty(colorHex) || colorHex.Length != 8)
                return new Color32(0, 0, 0, 0);

            byte r = byte.Parse(colorHex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
            byte g = byte.Parse(colorHex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
            byte b = byte.Parse(colorHex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
            byte a = byte.Parse(colorHex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
            return new Color32(r, g, b, a);
        }
    }
}
