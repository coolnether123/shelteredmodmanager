using ModAPI.Scenarios;
using ModAPI.Actors;
using UnityEngine;
using System.Collections.Generic;

using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Infrastructure.Assets;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell
{
    internal static class ScenarioCastPortraitResolver
    {
        private static readonly Color DefaultHairColor = new Color(0.23f, 0.13f, 0.08f, 1f);
        private static readonly Color DefaultSkinColor = new Color(0.84f, 0.61f, 0.45f, 1f);
        private static readonly Color DefaultShirtColor = new Color(0.37f, 0.45f, 0.58f, 1f);
        private static readonly Color DefaultPantsColor = new Color(0.25f, 0.25f, 0.25f, 1f);
        private static readonly Dictionary<string, CachedPortrait> ColorizedPortraitCache = new Dictionary<string, CachedPortrait>();
        private static Material AvatarMaterialTemplate;
        private static int LastAvatarMaterialLookupFrame = -120;
        private const float PortraitAlphaThreshold = 0.02f;
        private const int MinimumCropPadding = 1;

        public static Sprite Resolve(FamilyMember member)
        {
            return member != null ? member.avatarSprite : null;
        }

        public static Sprite Resolve(FamilyMemberConfig config)
        {
            CharacterMeshOptions.CharacterTexture texture = ResolveHeadTexture(config);
            return texture != null ? texture.m_avatar : null;
        }

        public static Sprite Resolve(ActorProfileComponent profile)
        {
            return Resolve(ToFamilyMemberConfig(profile));
        }

        public static Texture2D ResolveTexture(FamilyMember member)
        {
            if (member == null)
                return null;

            Color hair;
            Color skin;
            Color shirt;
            Color pants;
            ResolveColors(member, out hair, out skin, out shirt, out pants);
            return ResolveTexture(Resolve(member), hair, skin, shirt, pants);
        }

        public static Texture2D ResolveTexture(FamilyMemberConfig config)
        {
            Color hair;
            Color skin;
            Color shirt;
            Color pants;
            ResolveColors(config, out hair, out skin, out shirt, out pants);
            return ResolveTexture(Resolve(config), hair, skin, shirt, pants);
        }

        public static Texture2D ResolveTexture(ActorProfileComponent profile)
        {
            Color hair;
            Color skin;
            Color shirt;
            Color pants;
            ResolveColors(profile, out hair, out skin, out shirt, out pants);
            return ResolveTexture(Resolve(profile), hair, skin, shirt, pants);
        }

        public static void ResolveColors(
            FamilyMember member,
            out Color hair,
            out Color skin,
            out Color shirt,
            out Color pants)
        {
            if (member != null)
            {
                hair = member.hairColor;
                skin = member.skinColor;
                shirt = member.shirtColor;
                pants = member.pantsColor;
                return;
            }

            ResolveDefaultColors(out hair, out skin, out shirt, out pants);
        }

        public static void ResolveColors(
            FamilyMemberConfig config,
            out Color hair,
            out Color skin,
            out Color shirt,
            out Color pants)
        {
            ResolveDefaultColors(out hair, out skin, out shirt, out pants);
            FamilyMemberAppearanceConfig appearance = config != null ? config.Appearance : null;
            if (appearance == null)
                return;

            ApplyColor(appearance.HairColorHex, ref hair);
            ApplyColor(appearance.SkinColorHex, ref skin);
            ApplyColor(appearance.ShirtColorHex, ref shirt);
            ApplyColor(appearance.PantsColorHex, ref pants);
        }

        public static void ResolveColors(
            ActorProfileComponent profile,
            out Color hair,
            out Color skin,
            out Color shirt,
            out Color pants)
        {
            ResolveDefaultColors(out hair, out skin, out shirt, out pants);
            if (profile == null)
                return;

            if (profile.HairColor.a > 0f)
                hair = profile.HairColor;
            if (profile.SkinColor.a > 0f)
                skin = profile.SkinColor;
            if (profile.ShirtColor.a > 0f)
                shirt = profile.ShirtColor;
            if (profile.PantsColor.a > 0f)
                pants = profile.PantsColor;
        }

        private static FamilyMemberConfig ToFamilyMemberConfig(ActorProfileComponent profile)
        {
            if (profile == null)
                return null;

            FamilyMemberConfig config = new FamilyMemberConfig();
            config.Name = profile.FirstName;
            config.Gender = profile.IsMale ? ScenarioGender.Male : ScenarioGender.Female;
            config.Appearance.MeshId = profile.MeshId;
            return config;
        }

        private static CharacterMeshOptions.CharacterTexture ResolveHeadTexture(FamilyMemberConfig config)
        {
            CharacterMeshOptions.CharacterMeshType mesh = ResolveMeshType(config);
            if (mesh == null || mesh.m_headTextures == null || mesh.m_headTextures.Count == 0)
                return null;

            string headTextureId = config != null && config.Appearance != null ? config.Appearance.HeadTextureId : null;
            if (string.IsNullOrEmpty(headTextureId))
                headTextureId = "default";

            for (int i = 0; i < mesh.m_headTextures.Count; i++)
            {
                CharacterMeshOptions.CharacterTexture texture = mesh.m_headTextures[i];
                if (texture != null && string.Equals(texture.m_id, headTextureId, System.StringComparison.OrdinalIgnoreCase))
                    return texture;
            }

            for (int i = 0; i < mesh.m_headTextures.Count; i++)
            {
                CharacterMeshOptions.CharacterTexture texture = mesh.m_headTextures[i];
                if (texture != null && string.Equals(texture.m_id, "default", System.StringComparison.OrdinalIgnoreCase))
                    return texture;
            }

            return mesh.m_headTextures[0];
        }

        private static CharacterMeshOptions.CharacterMeshType ResolveMeshType(FamilyMemberConfig config)
        {
            CharacterMeshOptions options = CharacterMeshOptions.instance;
            if ((Object)options == (Object)null)
                return null;

            string meshId = ResolveMeshId(config);
            return options.FindCharacterMesh(meshId);
        }

        private static string ResolveMeshId(FamilyMemberConfig config)
        {
            if (config != null && config.Appearance != null && !string.IsNullOrEmpty(config.Appearance.MeshId))
                return config.Appearance.MeshId;

            ScenarioGender gender = config != null ? config.Gender : ScenarioGender.Any;
            bool adult = true;
            if (config != null && config.Appearance != null && config.Appearance.IsAdult.HasValue)
                adult = config.Appearance.IsAdult.Value;
            else if (config != null && config.ExactAge.HasValue)
                adult = config.ExactAge.Value >= 18;

            if (gender == ScenarioGender.Female)
                return adult ? "woman" : "girl";
            return adult ? "man" : "boy";
        }

        private static void ResolveDefaultColors(out Color hair, out Color skin, out Color shirt, out Color pants)
        {
            hair = DefaultHairColor;
            skin = DefaultSkinColor;
            shirt = DefaultShirtColor;
            pants = DefaultPantsColor;
        }

        private static void ApplyColor(string hex, ref Color target)
        {
            Color parsed;
            if (ScenarioCharacterAppearanceService.TryParseColorHex(hex, out parsed))
                target = parsed;
        }

        private static Texture2D ResolveTexture(Sprite sprite, Color hair, Color skin, Color shirt, Color pants)
        {
            if (sprite == null || sprite.texture == null)
                return null;

            string key = BuildCacheKey(sprite, hair, skin, shirt, pants);
            CachedPortrait cached;
            if (ColorizedPortraitCache.TryGetValue(key, out cached) && cached != null && cached.Texture != null && cached.Exact)
                return cached.Texture;

            Texture2D source = ExtractSpriteTexture(sprite);
            if (source == null)
                return cached != null ? cached.Texture : null;

            Texture2D portraitSource = CropToDominantPortraitRegion(source);
            bool usedAvatarMaterial = false;
            Texture2D rendered = RenderWithAvatarMaterial(portraitSource, hair, skin, shirt, pants, out usedAvatarMaterial);
            if (rendered == null && cached != null && cached.Texture != null)
                rendered = cached.Texture;
            else if (rendered == null)
                rendered = RenderFallbackTint(portraitSource, hair, skin, shirt, pants);

            if (rendered != null)
            {
                rendered.name = "SMM_CastPortrait_" + key.GetHashCode().ToString();
                if (usedAvatarMaterial || cached == null || cached.Texture == null)
                {
                    ColorizedPortraitCache[key] = new CachedPortrait
                    {
                        Texture = rendered,
                        Exact = usedAvatarMaterial
                    };
                }
            }

            if (source != sprite.texture)
                UnityEngine.Object.Destroy(source);
            if (portraitSource != null && portraitSource != source)
                UnityEngine.Object.Destroy(portraitSource);

            return rendered;
        }

        private static string BuildCacheKey(Sprite sprite, Color hair, Color skin, Color shirt, Color pants)
        {
            Rect rect = sprite.textureRect;
            return sprite.texture.GetInstanceID().ToString()
                + ":" + Mathf.RoundToInt(rect.x).ToString()
                + ":" + Mathf.RoundToInt(rect.y).ToString()
                + ":" + Mathf.RoundToInt(rect.width).ToString()
                + ":" + Mathf.RoundToInt(rect.height).ToString()
                + ":" + EncodeColor(hair)
                + ":" + EncodeColor(skin)
                + ":" + EncodeColor(shirt)
                + ":" + EncodeColor(pants);
        }

        private static string EncodeColor(Color color)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(color.r) * 255f).ToString("X2")
                + Mathf.RoundToInt(Mathf.Clamp01(color.g) * 255f).ToString("X2")
                + Mathf.RoundToInt(Mathf.Clamp01(color.b) * 255f).ToString("X2")
                + Mathf.RoundToInt(Mathf.Clamp01(color.a) * 255f).ToString("X2");
        }

        private static Texture2D ExtractSpriteTexture(Sprite sprite)
        {
            Texture2D texture = sprite != null ? sprite.texture : null;
            if (texture == null)
                return null;

            Rect rect = sprite.textureRect;
            int x = Mathf.RoundToInt(rect.x);
            int y = Mathf.RoundToInt(rect.y);
            int width = Mathf.Max(1, Mathf.RoundToInt(rect.width));
            int height = Mathf.Max(1, Mathf.RoundToInt(rect.height));
            if (x == 0 && y == 0 && width == texture.width && height == texture.height)
                return texture;

            Texture2D copy = new Texture2D(width, height, TextureFormat.ARGB32, false);
            copy.filterMode = FilterMode.Point;
            copy.wrapMode = TextureWrapMode.Clamp;
            try
            {
                copy.SetPixels(texture.GetPixels(x, y, width, height));
                copy.Apply();
                return copy;
            }
            catch
            {
                RenderTexture renderTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
                RenderTexture previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = renderTexture;
                    GL.PushMatrix();
                    GL.LoadPixelMatrix(0f, width, height, 0f);
                    Rect uv = new Rect(rect.x / texture.width, rect.y / texture.height, rect.width / texture.width, rect.height / texture.height);
                    Graphics.DrawTexture(new Rect(0f, 0f, width, height), texture, uv, 0, 0, 0, 0);
                    GL.PopMatrix();
                    copy.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                    copy.Apply();
                    return copy;
                }
                finally
                {
                    RenderTexture.active = previous;
                    RenderTexture.ReleaseTemporary(renderTexture);
                }
            }
        }

        private static Texture2D CropToDominantPortraitRegion(Texture2D source)
        {
            if (source == null || source.width <= 1 || source.height <= 1)
                return source;

            Color[] pixels;
            try
            {
                pixels = source.GetPixels();
            }
            catch
            {
                return source;
            }

            RectInt crop;
            if (!TryFindDominantAlphaCrop(pixels, source.width, source.height, out crop))
                return source;

            if (crop.X <= 0
                && crop.Y <= 0
                && crop.Width >= source.width
                && crop.Height >= source.height)
            {
                return source;
            }

            Texture2D copy = new Texture2D(crop.Width, crop.Height, TextureFormat.ARGB32, false);
            copy.filterMode = FilterMode.Point;
            copy.wrapMode = TextureWrapMode.Clamp;
            Color[] cropped = new Color[crop.Width * crop.Height];
            for (int y = 0; y < crop.Height; y++)
            {
                for (int x = 0; x < crop.Width; x++)
                {
                    cropped[x + (y * crop.Width)] = pixels[(crop.X + x) + ((crop.Y + y) * source.width)];
                }
            }

            copy.SetPixels(cropped);
            copy.Apply();
            return copy;
        }

        private static bool TryFindDominantAlphaCrop(Color[] pixels, int width, int height, out RectInt crop)
        {
            crop = new RectInt(0, 0, width, height);
            if (pixels == null || pixels.Length != width * height)
                return false;

            bool[] visited = new bool[pixels.Length];
            int[] queue = new int[pixels.Length];
            AlphaComponent largest = new AlphaComponent();
            AlphaComponent retained = new AlphaComponent();
            for (int index = 0; index < pixels.Length; index++)
            {
                if (visited[index] || pixels[index].a <= PortraitAlphaThreshold)
                    continue;

                AlphaComponent component = FloodAlphaComponent(pixels, visited, queue, width, height, index);
                if (component.Count > largest.Count)
                    largest = component;
            }

            if (largest.Count <= 0)
                return false;

            int minimumArea = Mathf.Max(4, largest.Count / 5);
            for (int index = 0; index < pixels.Length; index++)
                visited[index] = false;

            for (int index = 0; index < pixels.Length; index++)
            {
                if (visited[index] || pixels[index].a <= PortraitAlphaThreshold)
                    continue;

                AlphaComponent component = FloodAlphaComponent(pixels, visited, queue, width, height, index);
                if (component.Count >= minimumArea || Overlaps(component, largest))
                    retained.Include(component);
            }

            if (retained.Count <= 0)
                retained = largest;

            int minX = Mathf.Max(0, retained.MinX - MinimumCropPadding);
            int minY = Mathf.Max(0, retained.MinY - MinimumCropPadding);
            int maxX = Mathf.Min(width - 1, retained.MaxX + MinimumCropPadding);
            int maxY = Mathf.Min(height - 1, retained.MaxY + MinimumCropPadding);
            crop = new RectInt(minX, minY, Mathf.Max(1, maxX - minX + 1), Mathf.Max(1, maxY - minY + 1));
            return true;
        }

        private static AlphaComponent FloodAlphaComponent(
            Color[] pixels,
            bool[] visited,
            int[] queue,
            int width,
            int height,
            int startIndex)
        {
            AlphaComponent component = new AlphaComponent();
            int head = 0;
            int tail = 0;
            queue[tail++] = startIndex;
            visited[startIndex] = true;

            while (head < tail)
            {
                int index = queue[head++];
                int x = index % width;
                int y = index / width;
                component.Include(x, y);

                EnqueueAlphaNeighbor(pixels, visited, queue, width, height, x - 1, y, ref tail);
                EnqueueAlphaNeighbor(pixels, visited, queue, width, height, x + 1, y, ref tail);
                EnqueueAlphaNeighbor(pixels, visited, queue, width, height, x, y - 1, ref tail);
                EnqueueAlphaNeighbor(pixels, visited, queue, width, height, x, y + 1, ref tail);
            }

            return component;
        }

        private static void EnqueueAlphaNeighbor(
            Color[] pixels,
            bool[] visited,
            int[] queue,
            int width,
            int height,
            int x,
            int y,
            ref int tail)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
                return;

            int index = x + (y * width);
            if (visited[index] || pixels[index].a <= PortraitAlphaThreshold)
                return;

            visited[index] = true;
            queue[tail++] = index;
        }

        private static bool Overlaps(AlphaComponent left, AlphaComponent right)
        {
            return left.MinX <= right.MaxX
                && left.MaxX >= right.MinX
                && left.MinY <= right.MaxY
                && left.MaxY >= right.MinY;
        }

        private static Texture2D RenderWithAvatarMaterial(Texture2D source, Color hair, Color skin, Color shirt, Color pants, out bool usedAvatarMaterial)
        {
            usedAvatarMaterial = false;
            Material template = FindAvatarMaterialTemplate();
            if (source == null || template == null)
                return null;

            Material material = new Material(template);
            material.mainTexture = source;
            material.SetColor("_HairColour", hair);
            material.SetColor("_SkinColour", skin);
            material.SetColor("_ShirtColour", shirt);
            material.SetColor("_PantsColour", pants);

            RenderTexture renderTexture = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            Texture2D output = new Texture2D(source.width, source.height, TextureFormat.ARGB32, false);
            output.filterMode = FilterMode.Point;
            output.wrapMode = TextureWrapMode.Clamp;
            try
            {
                Graphics.Blit(source, renderTexture, material);
                RenderTexture.active = renderTexture;
                output.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
                output.Apply();
                usedAvatarMaterial = true;
                return output;
            }
            catch
            {
                UnityEngine.Object.Destroy(output);
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(renderTexture);
                UnityEngine.Object.Destroy(material);
            }
        }

        private static Texture2D RenderFallbackTint(Texture2D source, Color hair, Color skin, Color shirt, Color pants)
        {
            if (source == null)
                return null;

            Texture2D output = new Texture2D(source.width, source.height, TextureFormat.ARGB32, false);
            output.filterMode = FilterMode.Point;
            output.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels;
            try
            {
                pixels = source.GetPixels();
            }
            catch
            {
                UnityEngine.Object.Destroy(output);
                return null;
            }

            Color[] tinted = new Color[pixels.Length];
            for (int y = 0; y < source.height; y++)
            {
                float vertical = source.height > 1 ? (float)y / (float)(source.height - 1) : 0f;
                for (int x = 0; x < source.width; x++)
                {
                    int index = x + (y * source.width);
                    Color pixel = pixels[index];
                    if (pixel.a <= 0.01f)
                    {
                        tinted[index] = Color.clear;
                        continue;
                    }

                    Color region = vertical > 0.70f
                        ? hair
                        : vertical > 0.42f ? skin : shirt;
                    float shade = Mathf.Clamp01((pixel.r + pixel.g + pixel.b) / 3f);
                    tinted[index] = new Color(region.r * Mathf.Lerp(0.55f, 1.15f, shade), region.g * Mathf.Lerp(0.55f, 1.15f, shade), region.b * Mathf.Lerp(0.55f, 1.15f, shade), pixel.a);
                }
            }

            output.SetPixels(tinted);
            output.Apply();
            return output;
        }

        private static Material FindAvatarMaterialTemplate()
        {
            if (AvatarMaterialTemplate != null)
                return AvatarMaterialTemplate;
            int frame = Time.frameCount;
            if (frame - LastAvatarMaterialLookupFrame < 60)
                return null;

            LastAvatarMaterialLookupFrame = frame;
            UI2DSprite[] sprites = Resources.FindObjectsOfTypeAll<UI2DSprite>();
            for (int i = 0; sprites != null && i < sprites.Length; i++)
            {
                UI2DSprite sprite = sprites[i];
                Material material = sprite != null ? sprite.material : null;
                if (IsAvatarMaterial(material))
                {
                    AvatarMaterialTemplate = material;
                    return AvatarMaterialTemplate;
                }
            }

            Material[] materials = Resources.FindObjectsOfTypeAll<Material>();
            for (int i = 0; materials != null && i < materials.Length; i++)
            {
                if (IsAvatarMaterial(materials[i]))
                {
                    AvatarMaterialTemplate = materials[i];
                    return AvatarMaterialTemplate;
                }
            }

            return null;
        }

        private static bool IsAvatarMaterial(Material material)
        {
            return material != null
                && material.shader != null
                && material.HasProperty("_HairColour")
                && material.HasProperty("_SkinColour")
                && material.HasProperty("_ShirtColour")
                && material.HasProperty("_PantsColour");
        }

        private sealed class CachedPortrait
        {
            public Texture2D Texture;
            public bool Exact;
        }

        private struct RectInt
        {
            public RectInt(int x, int y, int width, int height)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
            }

            public int X;
            public int Y;
            public int Width;
            public int Height;
        }

        private struct AlphaComponent
        {
            public int Count;
            public int MinX;
            public int MinY;
            public int MaxX;
            public int MaxY;

            public void Include(int x, int y)
            {
                if (Count == 0)
                {
                    MinX = x;
                    MaxX = x;
                    MinY = y;
                    MaxY = y;
                }
                else
                {
                    if (x < MinX) MinX = x;
                    if (x > MaxX) MaxX = x;
                    if (y < MinY) MinY = y;
                    if (y > MaxY) MaxY = y;
                }

                Count++;
            }

            public void Include(AlphaComponent component)
            {
                if (component.Count <= 0)
                    return;

                if (Count == 0)
                {
                    MinX = component.MinX;
                    MaxX = component.MaxX;
                    MinY = component.MinY;
                    MaxY = component.MaxY;
                    Count = component.Count;
                    return;
                }

                if (component.MinX < MinX) MinX = component.MinX;
                if (component.MaxX > MaxX) MaxX = component.MaxX;
                if (component.MinY < MinY) MinY = component.MinY;
                if (component.MaxY > MaxY) MaxY = component.MaxY;
                Count += component.Count;
            }
        }
    }
}
