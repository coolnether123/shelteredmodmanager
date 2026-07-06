using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ShelteredAPI.Scenarios.Presentation.UiKit.Textures
{
    internal static class ScenarioUiAtlasSkin
    {
        public const int CornerRadiusPixels = 2;
        public const float CornerInsetPixels = 6f;
        public const int CornerTextureSize = 8;
        public const float ShadowOffset = 2f;

        private static readonly Dictionary<string, AtlasSprite> Cache = new Dictionary<string, AtlasSprite>(StringComparer.OrdinalIgnoreCase);
        private static bool _scanned;

        public static bool DrawPanel(Rect rect)
        {
            return DrawRole(rect, "panel", true);
        }

        public static bool DrawHeader(Rect rect)
        {
            return DrawRole(rect, "header", true);
        }

        public static bool DrawStatus(Rect rect)
        {
            return DrawRole(rect, "rule", false);
        }

        public static bool DrawButton(Rect rect, bool active, bool enabled, bool pressed, bool tab)
        {
            if (tab && active)
                return false;

            string role = tab
                ? (active ? "tabActive" : "tab")
                : (!enabled ? "buttonDisabled" : (pressed ? "buttonPressed" : (active ? "buttonHover" : "button")));
            return DrawRole(rect, role, true);
        }

        public static bool DrawIcon(Rect rect, string role)
        {
            return DrawRole(rect, role, false);
        }

        public static void DrawCornerCutTexture(Rect rect, Texture texture)
        {
            if (texture == null || rect.width <= 0f || rect.height <= 0f)
                return;

            float cut = ResolveCornerCut(rect);
            if (cut <= 0f)
            {
                GUI.DrawTexture(rect, texture);
                return;
            }

            DrawTextureIfVisible(new Rect(rect.x + cut, rect.y, rect.width - (cut * 2f), cut), texture);
            DrawTextureIfVisible(new Rect(rect.x, rect.y + cut, rect.width, rect.height - (cut * 2f)), texture);
            DrawTextureIfVisible(new Rect(rect.x + cut, rect.yMax - cut, rect.width - (cut * 2f), cut), texture);
        }

        public static void DrawCornerCutShadow(Rect rect)
        {
            Color oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.42f);
            DrawCornerCutTexture(
                new Rect(rect.x + ShadowOffset, rect.y + ShadowOffset, rect.width, rect.height),
                Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        public static void DrawCornerCutBorder(Rect rect, Texture strong, Texture subtle)
        {
            float cut = ResolveCornerCut(rect);
            if (cut <= 0f)
                return;

            Texture topLeft = strong != null ? strong : Texture2D.whiteTexture;
            Texture bottomRight = subtle != null ? subtle : topLeft;
            DrawTextureIfVisible(new Rect(rect.x + cut, rect.y + 1f, rect.width - (cut * 2f), 1f), topLeft);
            DrawTextureIfVisible(new Rect(rect.x + cut, rect.yMax - 2f, rect.width - (cut * 2f), 1f), bottomRight);
            DrawTextureIfVisible(new Rect(rect.x + 1f, rect.y + cut, 1f, rect.height - (cut * 2f)), topLeft);
            DrawTextureIfVisible(new Rect(rect.xMax - 2f, rect.y + cut, 1f, rect.height - (cut * 2f)), bottomRight);
        }

        public static string WriteDump(string outputPath)
        {
            string json = BuildDumpJson();
            if (!string.IsNullOrEmpty(outputPath))
            {
                string directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(outputPath, json);
            }

            return json;
        }

        public static string BuildDumpJson()
        {
            StringBuilder builder = new StringBuilder(32768);
            UIAtlas[] atlases = Resources.FindObjectsOfTypeAll<UIAtlas>();
            builder.Append("{\"atlases\":[");
            for (int i = 0; atlases != null && i < atlases.Length; i++)
            {
                UIAtlas atlas = atlases[i];
                if (atlas == null)
                    continue;

                if (i > 0)
                    builder.Append(',');

                Texture texture = atlas.texture;
                builder.Append("{\"name\":\"").Append(Escape(atlas.name)).Append("\",");
                builder.Append("\"textureWidth\":").Append(texture != null ? texture.width : 0).Append(',');
                builder.Append("\"textureHeight\":").Append(texture != null ? texture.height : 0).Append(',');
                builder.Append("\"premultipliedAlpha\":").Append(atlas.premultipliedAlpha ? "true" : "false").Append(',');
                builder.Append("\"sprites\":[");
                List<UISpriteData> sprites = atlas.spriteList;
                for (int s = 0; sprites != null && s < sprites.Count; s++)
                {
                    UISpriteData sprite = sprites[s];
                    if (sprite == null)
                        continue;
                    if (s > 0)
                        builder.Append(',');
                    AppendSprite(builder, atlas, sprite);
                }
                builder.Append("]}");
            }
            builder.Append("],\"roleChoices\":");
            AppendRoleChoices(builder);
            builder.Append('}');
            return builder.ToString();
        }

        private static bool DrawRole(Rect rect, string role, bool nineSlice)
        {
            AtlasSprite sprite = ResolveRole(role);
            if (sprite == null || sprite.PremultipliedAlpha || sprite.Texture == null)
                return false;

            Color oldColor = GUI.color;
            GUI.color = Color.white;
            if (nineSlice && sprite.HasBorder)
                DrawNineSlice(rect, sprite);
            else
                GUI.DrawTextureWithTexCoords(rect, sprite.Texture, sprite.Uv, true);
            GUI.color = oldColor;
            return true;
        }

        private static float ResolveCornerCut(Rect rect)
        {
            return Mathf.Min(CornerRadiusPixels, Mathf.Floor(Mathf.Min(rect.width, rect.height) * 0.5f));
        }

        private static void DrawTextureIfVisible(Rect rect, Texture texture)
        {
            if (texture != null && rect.width > 0f && rect.height > 0f)
                GUI.DrawTexture(rect, texture);
        }

        private static AtlasSprite ResolveRole(string role)
        {
            EnsureScanned();
            AtlasSprite sprite;
            return Cache.TryGetValue(role ?? string.Empty, out sprite) ? sprite : null;
        }

        private static void EnsureScanned()
        {
            if (_scanned)
                return;

            _scanned = true;
            UIAtlas[] atlases = Resources.FindObjectsOfTypeAll<UIAtlas>();
            RegisterRole(atlases, "panel", "UI_Panel", "BluePaper", "UI_ClipboardNew_1", "Paper");
            RegisterRole(atlases, "header", "PaperStripLong", "ClipboardStatusBarBackground");
            RegisterRole(atlases, "button", "UI_Button", "PCButton_Normal", "OM_Stasis_Button");
            RegisterRole(atlases, "buttonHover", "UI_Button_Hover", "PC_Button_Selected", "OM_Stasis_Button_Selected");
            RegisterRole(atlases, "buttonPressed", "UI_Button_Pressed", "PC_Button_Selected", "OM_Stasis_Button_Selected");
            RegisterRole(atlases, "buttonDisabled", "Button_Disabled", "button_disabled", "btn_disabled", "button_grey");
            RegisterRole(atlases, "tab", "Tab_Background", "Tab_Normal", "tab_normal");
            RegisterRole(atlases, "tabActive", "Tab_Active", "Tab_Selected", "tab_selected");
            RegisterRole(atlases, "rule", "ClipboardStatusBarBackground", "ToolTipsBox", "Divider", "divider", "rule");
            RegisterRole(atlases, "close", "Close", "close", "x", "cross");
            RegisterRole(atlases, "check", "Check", "check", "tick", "ok");
            RegisterRole(atlases, "pin", "Pin", "pin", "clip", "paperclip");
        }

        private static void RegisterRole(UIAtlas[] atlases, string role, params string[] candidates)
        {
            AtlasSprite sprite = FindSprite(atlases, true, candidates);
            if (sprite == null)
                sprite = FindSprite(atlases, false, candidates);
            if (sprite != null && !Cache.ContainsKey(role))
                Cache.Add(role, sprite);
        }

        private static AtlasSprite FindSprite(UIAtlas[] atlases, bool exact, string[] candidates)
        {
            for (int c = 0; candidates != null && c < candidates.Length; c++)
            {
                string candidate = candidates[c];
                for (int i = 0; atlases != null && i < atlases.Length; i++)
                {
                    UIAtlas atlas = atlases[i];
                    if (atlas == null || atlas.spriteList == null)
                        continue;
                    for (int s = 0; s < atlas.spriteList.Count; s++)
                    {
                        UISpriteData data = atlas.spriteList[s];
                        if (data == null || string.IsNullOrEmpty(data.name))
                            continue;

                        bool match = exact
                            ? string.Equals(data.name, candidate, StringComparison.OrdinalIgnoreCase)
                            : data.name.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) >= 0;
                        if (match)
                            return CreateSprite(atlas, data);
                    }
                }
            }

            return null;
        }

        private static AtlasSprite CreateSprite(UIAtlas atlas, UISpriteData data)
        {
            Texture texture = atlas != null ? atlas.texture : null;
            if (texture == null || data == null || data.width <= 0 || data.height <= 0)
                return null;

            return new AtlasSprite(
                atlas.name,
                data.name,
                texture,
                atlas.premultipliedAlpha,
                data.x,
                data.y,
                data.width,
                data.height,
                data.borderLeft,
                data.borderRight,
                data.borderTop,
                data.borderBottom);
        }

        private static void DrawNineSlice(Rect rect, AtlasSprite sprite)
        {
            float left = Mathf.Clamp(sprite.BorderLeft, 0f, rect.width * 0.5f);
            float right = Mathf.Clamp(sprite.BorderRight, 0f, rect.width * 0.5f);
            float top = Mathf.Clamp(sprite.BorderTop, 0f, rect.height * 0.5f);
            float bottom = Mathf.Clamp(sprite.BorderBottom, 0f, rect.height * 0.5f);

            DrawSlice(new Rect(rect.x, rect.y, left, top), sprite, 0f, 0f, sprite.BorderLeft, sprite.BorderTop);
            DrawSlice(new Rect(rect.x + left, rect.y, rect.width - left - right, top), sprite, sprite.BorderLeft, 0f, sprite.Width - sprite.BorderLeft - sprite.BorderRight, sprite.BorderTop);
            DrawSlice(new Rect(rect.xMax - right, rect.y, right, top), sprite, sprite.Width - sprite.BorderRight, 0f, sprite.BorderRight, sprite.BorderTop);

            DrawSlice(new Rect(rect.x, rect.y + top, left, rect.height - top - bottom), sprite, 0f, sprite.BorderTop, sprite.BorderLeft, sprite.Height - sprite.BorderTop - sprite.BorderBottom);
            DrawSlice(new Rect(rect.x + left, rect.y + top, rect.width - left - right, rect.height - top - bottom), sprite, sprite.BorderLeft, sprite.BorderTop, sprite.Width - sprite.BorderLeft - sprite.BorderRight, sprite.Height - sprite.BorderTop - sprite.BorderBottom);
            DrawSlice(new Rect(rect.xMax - right, rect.y + top, right, rect.height - top - bottom), sprite, sprite.Width - sprite.BorderRight, sprite.BorderTop, sprite.BorderRight, sprite.Height - sprite.BorderTop - sprite.BorderBottom);

            DrawSlice(new Rect(rect.x, rect.yMax - bottom, left, bottom), sprite, 0f, sprite.Height - sprite.BorderBottom, sprite.BorderLeft, sprite.BorderBottom);
            DrawSlice(new Rect(rect.x + left, rect.yMax - bottom, rect.width - left - right, bottom), sprite, sprite.BorderLeft, sprite.Height - sprite.BorderBottom, sprite.Width - sprite.BorderLeft - sprite.BorderRight, sprite.BorderBottom);
            DrawSlice(new Rect(rect.xMax - right, rect.yMax - bottom, right, bottom), sprite, sprite.Width - sprite.BorderRight, sprite.Height - sprite.BorderBottom, sprite.BorderRight, sprite.BorderBottom);
        }

        private static void DrawSlice(Rect target, AtlasSprite sprite, float localX, float localY, float width, float height)
        {
            if (target.width <= 0f || target.height <= 0f || width <= 0f || height <= 0f)
                return;

            const float atlasInset = 0.5f;
            float insetX = width > atlasInset * 2f ? atlasInset : 0f;
            float insetY = height > atlasInset * 2f ? atlasInset : 0f;
            Rect uv = ToUv(
                sprite.X + localX + insetX,
                sprite.Y + localY + insetY,
                Math.Max(0.001f, width - (insetX * 2f)),
                Math.Max(0.001f, height - (insetY * 2f)),
                sprite.Texture.width,
                sprite.Texture.height);
            GUI.DrawTextureWithTexCoords(target, sprite.Texture, uv, true);
        }

        private static Rect ToUv(float x, float y, float width, float height, float textureWidth, float textureHeight)
        {
            return new Rect(
                x / textureWidth,
                1f - ((y + height) / textureHeight),
                width / textureWidth,
                height / textureHeight);
        }

        private static void AppendRoleChoices(StringBuilder builder)
        {
            EnsureScanned();
            builder.Append('{');
            int index = 0;
            foreach (KeyValuePair<string, AtlasSprite> pair in Cache)
            {
                if (index > 0)
                    builder.Append(',');
                builder.Append('"').Append(Escape(pair.Key)).Append("\":");
                if (pair.Value == null)
                    builder.Append("null");
                else
                {
                    builder.Append("{\"atlas\":\"").Append(Escape(pair.Value.AtlasName)).Append("\",");
                    builder.Append("\"sprite\":\"").Append(Escape(pair.Value.SpriteName)).Append("\",");
                    builder.Append("\"premultipliedAlpha\":").Append(pair.Value.PremultipliedAlpha ? "true" : "false").Append('}');
                }
                index++;
            }
            builder.Append('}');
        }

        private static void AppendSprite(StringBuilder builder, UIAtlas atlas, UISpriteData sprite)
        {
            builder.Append("{\"name\":\"").Append(Escape(sprite.name)).Append("\",");
            builder.Append("\"x\":").Append(sprite.x).Append(',');
            builder.Append("\"y\":").Append(sprite.y).Append(',');
            builder.Append("\"width\":").Append(sprite.width).Append(',');
            builder.Append("\"height\":").Append(sprite.height).Append(',');
            builder.Append("\"borderLeft\":").Append(sprite.borderLeft).Append(',');
            builder.Append("\"borderRight\":").Append(sprite.borderRight).Append(',');
            builder.Append("\"borderTop\":").Append(sprite.borderTop).Append(',');
            builder.Append("\"borderBottom\":").Append(sprite.borderBottom).Append('}');
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private sealed class AtlasSprite
        {
            public readonly string AtlasName;
            public readonly string SpriteName;
            public readonly Texture Texture;
            public readonly bool PremultipliedAlpha;
            public readonly float X;
            public readonly float Y;
            public readonly float Width;
            public readonly float Height;
            public readonly float BorderLeft;
            public readonly float BorderRight;
            public readonly float BorderTop;
            public readonly float BorderBottom;
            public readonly Rect Uv;

            public AtlasSprite(
                string atlasName,
                string spriteName,
                Texture texture,
                bool premultipliedAlpha,
                float x,
                float y,
                float width,
                float height,
                float borderLeft,
                float borderRight,
                float borderTop,
                float borderBottom)
            {
                AtlasName = atlasName;
                SpriteName = spriteName;
                Texture = texture;
                PremultipliedAlpha = premultipliedAlpha;
                X = x;
                Y = y;
                Width = width;
                Height = height;
                BorderLeft = borderLeft;
                BorderRight = borderRight;
                BorderTop = borderTop;
                BorderBottom = borderBottom;
                Uv = ToUv(x, y, width, height, texture.width, texture.height);
            }

            public bool HasBorder
            {
                get { return (BorderLeft + BorderRight + BorderTop + BorderBottom) > 0f; }
            }
        }
    }
}
