using System;
using System.Collections.Generic;
using System.Reflection;
using ModAPI.Scenarios;
using ShelteredAPI.Content;
using UnityEngine;

using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Definitions;
namespace ShelteredAPI.Scenarios.Infrastructure.Assets{
    internal enum ScenarioCharacterTexturePart
    {
        Head = 0,
        Torso = 1,
        Legs = 2
    }

    internal enum ScenarioCharacterColorPart
    {
        Hair = 0,
        Skin = 1,
        Shirt = 2,
        Pants = 3
    }

    internal sealed class ScenarioCharacterAppearanceService
    {
        internal sealed class ResolvedCharacterTarget
        {
            public FamilyMember FamilyMember;
            public BaseCharacter Character;
            public CharacterMesh Mesh;
            public CharacterMeshOptions.CharacterMeshType MeshType;
            public int FamilyIndex;
            public string TargetPath;
            public string DisplayName;
        }

        internal sealed class PreviewSession
        {
            public ResolvedCharacterTarget Target;
            public string HeadTextureId;
            public string TorsoTextureId;
            public string LegTextureId;
            public Sprite AvatarSprite;
        }

        private static readonly FieldInfo BaseCharacterMeshField = typeof(BaseCharacter).GetField("m_mesh", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo BaseCharacterHeadTextureField = typeof(BaseCharacter).GetField("m_headTexture", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo BaseCharacterTorsoTextureField = typeof(BaseCharacter).GetField("m_torsoTexture", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo BaseCharacterLegTextureField = typeof(BaseCharacter).GetField("m_legTexture", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo BaseCharacterAvatarSpriteField = typeof(BaseCharacter).GetField("m_avatarSprite", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo BaseCharacterMeshIdField = typeof(BaseCharacter).GetField("m_characterMeshId", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo BaseCharacterHairColorField = typeof(BaseCharacter).GetField("m_hairColor", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo BaseCharacterSkinColorField = typeof(BaseCharacter).GetField("m_skinColor", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo BaseCharacterShirtColorField = typeof(BaseCharacter).GetField("m_shirtColor", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo BaseCharacterPantsColorField = typeof(BaseCharacter).GetField("m_pantsColor", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo NpcVisitHairColorsField = typeof(NpcVisitManager).GetField("m_hairColors", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo NpcVisitSkinColorsField = typeof(NpcVisitManager).GetField("m_skinColors", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo NpcVisitShirtColorsField = typeof(NpcVisitManager).GetField("m_shirtColors", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo NpcVisitPantsColorsField = typeof(NpcVisitManager).GetField("m_pantsColors", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly string[] FallbackHairColors = new[] { "#3B2116", "#6B3F1D", "#A36B35", "#D1B07A", "#101010", "#808080" };
        private static readonly string[] FallbackSkinColors = new[] { "#F2C49B", "#D79B72", "#A96E4B", "#70452F", "#3A2419" };
        private static readonly string[] FallbackShirtColors = new[] { "#5F7EA6", "#7D8B55", "#9A4B4B", "#6B5D8F", "#C1A34F", "#535353" };
        private static readonly string[] FallbackPantsColors = new[] { "#2D3B4F", "#4F4A42", "#5E5A35", "#3A3A3A", "#6C513A" };

        private readonly IScenarioSpriteAssetResolver _assetResolver;

        public static ScenarioCharacterAppearanceService Instance
        {
            get { return ScenarioCompositionRoot.Resolve<ScenarioCharacterAppearanceService>(); }
        }

        internal ScenarioCharacterAppearanceService(IScenarioSpriteAssetResolver assetResolver)
        {
            _assetResolver = assetResolver;
        }

        public bool CanEdit(ScenarioAuthoringTarget target)
        {
            ResolvedCharacterTarget resolved;
            string message;
            return TryResolve(target, out resolved, out message);
        }

        public bool TryResolve(ScenarioAuthoringTarget target, out ResolvedCharacterTarget resolved, out string message)
        {
            resolved = null;
            message = null;

            GameObject gameObject = ResolveGameObject(target);
            if (gameObject == null)
            {
                message = "Character editor requires a live character target.";
                return false;
            }

            FamilyMember familyMember = gameObject.GetComponentInParent<FamilyMember>();
            if (familyMember == null)
            {
                message = "Character editing currently supports family members only.";
                return false;
            }

            return TryResolve(familyMember, target != null ? target.TransformPath : null, target != null ? target.DisplayName : null, out resolved, out message);
        }

        public bool TryResolve(FamilyMember familyMember, string targetPath, string displayName, out ResolvedCharacterTarget resolved, out string message)
        {
            resolved = null;
            message = null;
            if (familyMember == null)
            {
                message = "Family member was not available.";
                return false;
            }

            CharacterMesh mesh = BaseCharacterMeshField != null ? BaseCharacterMeshField.GetValue(familyMember) as CharacterMesh : null;
            if (mesh == null)
            {
                message = "Selected family member does not expose an editable character mesh.";
                return false;
            }

            CharacterMeshOptions options = CharacterMeshOptions.instance;
            if ((UnityEngine.Object)options == (UnityEngine.Object)null)
            {
                message = "Character mesh options are not loaded.";
                return false;
            }

            CharacterMeshOptions.CharacterMeshType meshType = options.FindCharacterMesh(mesh.meshId);
            if (meshType == null)
            {
                message = "Character mesh definition '" + mesh.meshId + "' could not be resolved.";
                return false;
            }

            int familyIndex = FindFamilyIndex(familyMember);
            if (familyIndex < 0)
            {
                message = "Selected family member could not be matched to the current family roster.";
                return false;
            }

            resolved = new ResolvedCharacterTarget
            {
                FamilyMember = familyMember,
                Character = familyMember,
                Mesh = mesh,
                MeshType = meshType,
                FamilyIndex = familyIndex,
                TargetPath = targetPath,
                DisplayName = !string.IsNullOrEmpty(displayName) ? displayName : familyMember.firstName
            };
            return true;
        }

        public PreviewSession CapturePreview(ResolvedCharacterTarget target)
        {
            if (target == null || target.Character == null)
                return null;

            return new PreviewSession
            {
                Target = target,
                HeadTextureId = target.Character.headTexture,
                TorsoTextureId = target.Character.torsoTexture,
                LegTextureId = target.Character.legTexture,
                AvatarSprite = target.Character.avatarSprite
            };
        }

        public void RestorePreview(PreviewSession preview)
        {
            if (preview == null || preview.Target == null || preview.Target.Character == null)
                return;

            string ignored;
            ApplyTextureId(preview.Target, ScenarioCharacterTexturePart.Head, preview.HeadTextureId, preview.AvatarSprite, out ignored);
            ApplyTextureId(preview.Target, ScenarioCharacterTexturePart.Torso, preview.TorsoTextureId, null, out ignored);
            ApplyTextureId(preview.Target, ScenarioCharacterTexturePart.Legs, preview.LegTextureId, null, out ignored);
        }

        public bool TryCreateEditableTexture(
            ResolvedCharacterTarget target,
            ScenarioCharacterTexturePart part,
            out Texture2D texture,
            out string sourceId,
            out string sourceLabel)
        {
            texture = null;
            sourceId = null;
            sourceLabel = null;
            if (target == null || target.Character == null || target.MeshType == null)
                return false;

            sourceId = GetCurrentTextureId(target.Character, part);
            CharacterMeshOptions.CharacterTexture entry = FindTextureEntry(target.MeshType, part, sourceId);
            if (entry == null || (UnityEngine.Object)entry.m_texture == (UnityEngine.Object)null)
                return false;

            texture = CopyTexture(entry.m_texture);
            sourceLabel = BuildPartLabel(part) + " Texture";
            return texture != null;
        }

        public bool ApplyPreviewTexture(
            ResolvedCharacterTarget target,
            ScenarioCharacterTexturePart part,
            string textureId,
            Texture2D texture,
            out string message)
        {
            message = null;
            if (target == null || string.IsNullOrEmpty(textureId) || texture == null)
            {
                message = "Character preview data was incomplete.";
                return false;
            }

            Sprite avatarSprite = part == ScenarioCharacterTexturePart.Head ? CreateTextureSprite(texture) : null;
            EnsureCustomTextureRegistered(target, part, textureId, texture, avatarSprite);
            return ApplyTextureId(target, part, textureId, avatarSprite, out message);
        }

        public bool ApplyConfiguredAppearance(
            ScenarioDefinition definition,
            string scenarioFilePath,
            FamilyMemberConfig config,
            FamilyMember member,
            out string message)
        {
            message = null;
            if (config == null || config.Appearance == null || member == null)
                return false;

            ResolvedCharacterTarget target;
            if (!TryResolve(member, null, null, out target, out message))
                return false;

            ApplyConfiguredPart(definition, scenarioFilePath, target, config.Appearance.HeadTextureId, config.Appearance.HeadTexturePath, ScenarioCharacterTexturePart.Head);
            ApplyConfiguredPart(definition, scenarioFilePath, target, config.Appearance.TorsoTextureId, config.Appearance.TorsoTexturePath, ScenarioCharacterTexturePart.Torso);
            ApplyConfiguredPart(definition, scenarioFilePath, target, config.Appearance.LegTextureId, config.Appearance.LegTexturePath, ScenarioCharacterTexturePart.Legs);
            ApplyConfiguredColors(target, config.Appearance);
            return true;
        }

        public string GetCurrentTextureId(ResolvedCharacterTarget target, ScenarioCharacterTexturePart part)
        {
            return GetCurrentTextureId(target != null ? target.Character : null, part);
        }

        public string GetCurrentTextureId(BaseCharacter character, ScenarioCharacterTexturePart part)
        {
            if (character == null)
                return null;

            switch (part)
            {
                case ScenarioCharacterTexturePart.Head:
                    return character.headTexture;
                case ScenarioCharacterTexturePart.Torso:
                    return character.torsoTexture;
                case ScenarioCharacterTexturePart.Legs:
                    return character.legTexture;
                default:
                    return null;
            }
        }

        public static string BuildPartLabel(ScenarioCharacterTexturePart part)
        {
            switch (part)
            {
                case ScenarioCharacterTexturePart.Head: return "Head";
                case ScenarioCharacterTexturePart.Torso: return "Torso";
                case ScenarioCharacterTexturePart.Legs: return "Legs";
                default: return "Part";
            }
        }

        public static string BuildColorLabel(ScenarioCharacterColorPart part)
        {
            switch (part)
            {
                case ScenarioCharacterColorPart.Hair: return "Hair";
                case ScenarioCharacterColorPart.Skin: return "Skin";
                case ScenarioCharacterColorPart.Shirt: return "Shirt";
                case ScenarioCharacterColorPart.Pants: return "Pants";
                default: return "Color";
            }
        }

        public static void UpsertAppearance(FamilyMemberConfig config, ScenarioCharacterTexturePart part, string textureId, string texturePath)
        {
            if (config == null)
                return;

            if (config.Appearance == null)
                config.Appearance = new FamilyMemberAppearanceConfig();

            switch (part)
            {
                case ScenarioCharacterTexturePart.Head:
                    config.Appearance.HeadTextureId = textureId;
                    config.Appearance.HeadTexturePath = texturePath;
                    break;
                case ScenarioCharacterTexturePart.Torso:
                    config.Appearance.TorsoTextureId = textureId;
                    config.Appearance.TorsoTexturePath = texturePath;
                    break;
                case ScenarioCharacterTexturePart.Legs:
                    config.Appearance.LegTextureId = textureId;
                    config.Appearance.LegTexturePath = texturePath;
                    break;
            }
        }

        public static void UpsertColor(FamilyMemberConfig config, ScenarioCharacterColorPart part, string colorHex)
        {
            if (config == null)
                return;

            if (config.Appearance == null)
                config.Appearance = new FamilyMemberAppearanceConfig();

            switch (part)
            {
                case ScenarioCharacterColorPart.Hair:
                    config.Appearance.HairColorHex = colorHex;
                    break;
                case ScenarioCharacterColorPart.Skin:
                    config.Appearance.SkinColorHex = colorHex;
                    break;
                case ScenarioCharacterColorPart.Shirt:
                    config.Appearance.ShirtColorHex = colorHex;
                    break;
                case ScenarioCharacterColorPart.Pants:
                    config.Appearance.PantsColorHex = colorHex;
                    break;
            }
        }

        public static void CaptureAppearance(FamilyMember member, FamilyMemberConfig config)
        {
            if (member == null || config == null)
                return;

            if (config.Appearance == null)
                config.Appearance = new FamilyMemberAppearanceConfig();

            config.Appearance.MeshId = member.meshId;
            config.Appearance.IsAdult = member.isAdult;
            config.Appearance.HeadTextureId = member.headTexture;
            config.Appearance.HeadTexturePath = null;
            config.Appearance.TorsoTextureId = member.torsoTexture;
            config.Appearance.TorsoTexturePath = null;
            config.Appearance.LegTextureId = member.legTexture;
            config.Appearance.LegTexturePath = null;
            config.Appearance.HairColorHex = ToColorHex(member.hairColor);
            config.Appearance.SkinColorHex = ToColorHex(member.skinColor);
            config.Appearance.ShirtColorHex = ToColorHex(member.shirtColor);
            config.Appearance.PantsColorHex = ToColorHex(member.pantsColor);
        }

        public string CycleTextureId(string meshId, ScenarioCharacterTexturePart part, string currentTextureId, int delta)
        {
            CharacterMeshOptions.CharacterMeshType meshType = ResolveMeshType(meshId);
            List<CharacterMeshOptions.CharacterTexture> textures = GetTextureList(meshType, part);
            if (textures == null || textures.Count == 0)
                return string.IsNullOrEmpty(currentTextureId) ? "default" : currentTextureId;

            int currentIndex = FindTextureIndex(textures, currentTextureId);
            int direction = delta < 0 ? -1 : 1;
            for (int offset = 1; offset <= textures.Count; offset++)
            {
                int nextIndex = currentIndex + (offset * direction);
                while (nextIndex < 0)
                    nextIndex += textures.Count;
                nextIndex = nextIndex % textures.Count;

                CharacterMeshOptions.CharacterTexture next = textures[nextIndex];
                if (IsCustomizationTexture(next))
                    return next.m_id;
            }

            return string.IsNullOrEmpty(currentTextureId) ? "default" : currentTextureId;
        }

        public string RandomTextureId(string meshId, ScenarioCharacterTexturePart part)
        {
            CharacterMeshOptions.CharacterMeshType meshType = ResolveMeshType(meshId);
            if (meshType == null)
                return "default";

            string id = meshType.GetRandomTexture(ToMeshTexturePart(part));
            return string.IsNullOrEmpty(id) ? "default" : id;
        }

        public string CycleColorHex(ScenarioCharacterColorPart part, string currentHex, int delta)
        {
            List<Color> colors = GetVanillaColorList(part);
            if (colors != null && colors.Count > 0)
            {
                int currentIndex = FindColorIndex(colors, currentHex);
                int direction = delta < 0 ? -1 : 1;
                int nextIndex = currentIndex + direction;
                while (nextIndex < 0)
                    nextIndex += colors.Count;
                nextIndex = nextIndex % colors.Count;
                return ToColorHex(colors[nextIndex]);
            }

            string[] fallback = GetFallbackColors(part);
            if (fallback == null || fallback.Length == 0)
                return currentHex;

            int index = FindColorIndex(fallback, currentHex);
            int fallbackDirection = delta < 0 ? -1 : 1;
            int fallbackNext = index + fallbackDirection;
            while (fallbackNext < 0)
                fallbackNext += fallback.Length;
            fallbackNext = fallbackNext % fallback.Length;
            return fallback[fallbackNext];
        }

        public string RandomColorHex(ScenarioCharacterColorPart part)
        {
            NpcVisitManager manager = NpcVisitManager.Instance;
            if ((UnityEngine.Object)manager != (UnityEngine.Object)null)
                return ToColorHex(manager.GetRandomNpcColor(ToMeshColorPart(part)));

            string[] fallback = GetFallbackColors(part);
            if (fallback == null || fallback.Length == 0)
                return "#FFFFFF";

            return fallback[UnityEngine.Random.Range(0, fallback.Length)];
        }

        public static string ToColorHex(Color color)
        {
            return "#"
                + ToByte(color.r).ToString("X2")
                + ToByte(color.g).ToString("X2")
                + ToByte(color.b).ToString("X2")
                + ToByte(color.a).ToString("X2");
        }

        public static bool TryParseColorHex(string value, out Color color)
        {
            color = Color.white;
            if (string.IsNullOrEmpty(value))
                return false;

            string hex = value.Trim();
            if (hex.StartsWith("#", StringComparison.Ordinal))
                hex = hex.Substring(1);

            if (hex.Length != 6 && hex.Length != 8)
                return false;

            try
            {
                int r = Convert.ToInt32(hex.Substring(0, 2), 16);
                int g = Convert.ToInt32(hex.Substring(2, 2), 16);
                int b = Convert.ToInt32(hex.Substring(4, 2), 16);
                int a = hex.Length == 8 ? Convert.ToInt32(hex.Substring(6, 2), 16) : 255;
                color = new Color(r / 255f, g / 255f, b / 255f, a / 255f);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void ApplyConfiguredPart(
            ScenarioDefinition definition,
            string scenarioFilePath,
            ResolvedCharacterTarget target,
            string configuredId,
            string configuredPath,
            ScenarioCharacterTexturePart part)
        {
            if (target == null || string.IsNullOrEmpty(configuredId))
                return;

            string message;
            Texture2D texture;
            Sprite avatarSprite;
            if (TryLoadConfiguredTexture(definition, scenarioFilePath, target, part, configuredId, configuredPath, out texture, out avatarSprite))
            {
                ApplyTextureId(target, part, configuredId, avatarSprite, out message);
                return;
            }

            ApplyTextureId(target, part, configuredId, null, out message);
        }

        private void ApplyConfiguredColors(ResolvedCharacterTarget target, FamilyMemberAppearanceConfig appearance)
        {
            if (target == null || appearance == null)
                return;

            ApplyConfiguredColor(target, ScenarioCharacterColorPart.Hair, appearance.HairColorHex);
            ApplyConfiguredColor(target, ScenarioCharacterColorPart.Skin, appearance.SkinColorHex);
            ApplyConfiguredColor(target, ScenarioCharacterColorPart.Shirt, appearance.ShirtColorHex);
            ApplyConfiguredColor(target, ScenarioCharacterColorPart.Pants, appearance.PantsColorHex);
        }

        private void ApplyConfiguredColor(ResolvedCharacterTarget target, ScenarioCharacterColorPart part, string colorHex)
        {
            Color color;
            if (!TryParseColorHex(colorHex, out color) || target == null || target.Mesh == null)
                return;

            switch (part)
            {
                case ScenarioCharacterColorPart.Hair:
                    SetCharacterColorField(target.Character, BaseCharacterHairColorField, color);
                    target.Mesh.SetColor(CharacterMesh.ColorCustomization.HairColor, color);
                    break;
                case ScenarioCharacterColorPart.Skin:
                    SetCharacterColorField(target.Character, BaseCharacterSkinColorField, color);
                    target.Mesh.SetColor(CharacterMesh.ColorCustomization.SkinColor, color);
                    break;
                case ScenarioCharacterColorPart.Shirt:
                    SetCharacterColorField(target.Character, BaseCharacterShirtColorField, color);
                    target.Mesh.SetColor(CharacterMesh.ColorCustomization.ShirtColor, color);
                    break;
                case ScenarioCharacterColorPart.Pants:
                    SetCharacterColorField(target.Character, BaseCharacterPantsColorField, color);
                    target.Mesh.SetColor(CharacterMesh.ColorCustomization.PantsColor, color);
                    break;
            }
        }

        private bool TryLoadConfiguredTexture(
            ScenarioDefinition definition,
            string scenarioFilePath,
            ResolvedCharacterTarget target,
            ScenarioCharacterTexturePart part,
            string configuredId,
            string configuredPath,
            out Texture2D texture,
            out Sprite avatarSprite)
        {
            texture = null;
            avatarSprite = null;
            if (definition == null || string.IsNullOrEmpty(scenarioFilePath) || target == null)
                return false;

            string packRoot = System.IO.Path.GetDirectoryName(scenarioFilePath);
            if (string.IsNullOrEmpty(packRoot))
                return false;

            Sprite sprite = null;
            try
            {
                sprite = _assetResolver.ResolveSprite(
                    definition,
                    packRoot,
                    configuredId,
                    configuredPath,
                    null,
                    "character appearance '" + configuredId + "'");
            }
            catch
            {
                sprite = null;
            }

            if (sprite == null || sprite.texture == null)
                return false;

            texture = sprite.texture;
            avatarSprite = part == ScenarioCharacterTexturePart.Head ? sprite : null;
            EnsureCustomTextureRegistered(target, part, configuredId, texture, avatarSprite);
            return true;
        }

        private bool ApplyTextureId(
            ResolvedCharacterTarget target,
            ScenarioCharacterTexturePart part,
            string textureId,
            Sprite avatarSprite,
            out string message)
        {
            message = null;
            if (target == null || target.Character == null || target.Mesh == null || string.IsNullOrEmpty(textureId))
            {
                message = "Character texture target was unavailable.";
                return false;
            }

            switch (part)
            {
                case ScenarioCharacterTexturePart.Head:
                    if (BaseCharacterMeshIdField != null && !string.IsNullOrEmpty(target.Mesh.meshId))
                        BaseCharacterMeshIdField.SetValue(target.Character, target.Mesh.meshId);
                    if (BaseCharacterHeadTextureField != null)
                        BaseCharacterHeadTextureField.SetValue(target.Character, textureId);
                    target.Mesh.SetTexture(CharacterMesh.TextureType.Head, textureId);
                    SetAvatarSprite(target, textureId, avatarSprite);
                    break;
                case ScenarioCharacterTexturePart.Torso:
                    if (BaseCharacterTorsoTextureField != null)
                        BaseCharacterTorsoTextureField.SetValue(target.Character, textureId);
                    target.Mesh.SetTexture(CharacterMesh.TextureType.Torso, textureId);
                    break;
                case ScenarioCharacterTexturePart.Legs:
                    if (BaseCharacterLegTextureField != null)
                        BaseCharacterLegTextureField.SetValue(target.Character, textureId);
                    target.Mesh.SetTexture(CharacterMesh.TextureType.Legs, textureId);
                    break;
            }

            target.Mesh.RefreshTextures();
            return true;
        }

        private void SetAvatarSprite(ResolvedCharacterTarget target, string textureId, Sprite fallback)
        {
            if (BaseCharacterAvatarSpriteField == null || target == null || target.Character == null)
                return;

            Sprite avatar = fallback;
            if (avatar == null)
            {
                CharacterMeshOptions.CharacterTexture entry = FindTextureEntry(target.MeshType, ScenarioCharacterTexturePart.Head, textureId);
                avatar = entry != null ? entry.m_avatar : null;
            }

            if (avatar != null)
                BaseCharacterAvatarSpriteField.SetValue(target.Character, avatar);
        }

        private void EnsureCustomTextureRegistered(
            ResolvedCharacterTarget target,
            ScenarioCharacterTexturePart part,
            string textureId,
            Texture2D texture,
            Sprite avatarSprite)
        {
            if (target == null || target.MeshType == null || string.IsNullOrEmpty(textureId) || texture == null)
                return;

            List<CharacterMeshOptions.CharacterTexture> textures = GetTextureList(target.MeshType, part);
            if (textures == null)
                return;

            CharacterMeshOptions.CharacterTexture entry = FindTextureEntry(target.MeshType, part, textureId);
            if (entry == null)
            {
                entry = new CharacterMeshOptions.CharacterTexture();
                textures.Add(entry);
            }

            entry.m_id = textureId;
            entry.m_texture = texture;
            entry.m_availableForCustomization = true;
            if (part == ScenarioCharacterTexturePart.Head)
                entry.m_avatar = avatarSprite != null ? avatarSprite : CreateTextureSprite(texture);
        }

        private static CharacterMeshOptions.CharacterTexture FindTextureEntry(
            CharacterMeshOptions.CharacterMeshType meshType,
            ScenarioCharacterTexturePart part,
            string textureId)
        {
            List<CharacterMeshOptions.CharacterTexture> textures = GetTextureList(meshType, part);
            if (textures == null || string.IsNullOrEmpty(textureId))
                return null;

            for (int i = 0; i < textures.Count; i++)
            {
                CharacterMeshOptions.CharacterTexture entry = textures[i];
                if (entry != null && string.Equals(entry.m_id, textureId, StringComparison.OrdinalIgnoreCase))
                    return entry;
            }

            return null;
        }

        private CharacterMeshOptions.CharacterMeshType ResolveMeshType(string meshId)
        {
            CharacterMeshOptions options = CharacterMeshOptions.instance;
            if ((UnityEngine.Object)options == (UnityEngine.Object)null)
                return null;

            CharacterMeshOptions.CharacterMeshType meshType = !string.IsNullOrEmpty(meshId)
                ? options.FindCharacterMesh(meshId)
                : null;
            return meshType ?? options.FindCharacterMesh("man");
        }

        private static int FindTextureIndex(List<CharacterMeshOptions.CharacterTexture> textures, string textureId)
        {
            for (int i = 0; textures != null && i < textures.Count; i++)
            {
                CharacterMeshOptions.CharacterTexture texture = textures[i];
                if (texture != null && string.Equals(texture.m_id, textureId, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private static bool IsCustomizationTexture(CharacterMeshOptions.CharacterTexture texture)
        {
            if (texture == null || string.IsNullOrEmpty(texture.m_id) || !texture.m_availableForCustomization)
                return false;

            string id = texture.m_id.ToLowerInvariant();
            return id != "gasmask" && id != "hazmat";
        }

        private static CharacterMesh.TextureType ToMeshTexturePart(ScenarioCharacterTexturePart part)
        {
            switch (part)
            {
                case ScenarioCharacterTexturePart.Head: return CharacterMesh.TextureType.Head;
                case ScenarioCharacterTexturePart.Torso: return CharacterMesh.TextureType.Torso;
                case ScenarioCharacterTexturePart.Legs: return CharacterMesh.TextureType.Legs;
                default: return CharacterMesh.TextureType.Head;
            }
        }

        private static CharacterMesh.ColorCustomization ToMeshColorPart(ScenarioCharacterColorPart part)
        {
            switch (part)
            {
                case ScenarioCharacterColorPart.Hair: return CharacterMesh.ColorCustomization.HairColor;
                case ScenarioCharacterColorPart.Skin: return CharacterMesh.ColorCustomization.SkinColor;
                case ScenarioCharacterColorPart.Shirt: return CharacterMesh.ColorCustomization.ShirtColor;
                case ScenarioCharacterColorPart.Pants: return CharacterMesh.ColorCustomization.PantsColor;
                default: return CharacterMesh.ColorCustomization.HairColor;
            }
        }

        private static List<Color> GetVanillaColorList(ScenarioCharacterColorPart part)
        {
            NpcVisitManager manager = NpcVisitManager.Instance;
            if ((UnityEngine.Object)manager == (UnityEngine.Object)null)
                return null;

            FieldInfo field = null;
            switch (part)
            {
                case ScenarioCharacterColorPart.Hair:
                    field = NpcVisitHairColorsField;
                    break;
                case ScenarioCharacterColorPart.Skin:
                    field = NpcVisitSkinColorsField;
                    break;
                case ScenarioCharacterColorPart.Shirt:
                    field = NpcVisitShirtColorsField;
                    break;
                case ScenarioCharacterColorPart.Pants:
                    field = NpcVisitPantsColorsField;
                    break;
            }

            return field != null ? field.GetValue(manager) as List<Color> : null;
        }

        private static string[] GetFallbackColors(ScenarioCharacterColorPart part)
        {
            switch (part)
            {
                case ScenarioCharacterColorPart.Hair: return FallbackHairColors;
                case ScenarioCharacterColorPart.Skin: return FallbackSkinColors;
                case ScenarioCharacterColorPart.Shirt: return FallbackShirtColors;
                case ScenarioCharacterColorPart.Pants: return FallbackPantsColors;
                default: return FallbackHairColors;
            }
        }

        private static int FindColorIndex(List<Color> colors, string currentHex)
        {
            for (int i = 0; colors != null && i < colors.Count; i++)
            {
                if (string.Equals(ToColorHex(colors[i]), currentHex, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private static int FindColorIndex(string[] colors, string currentHex)
        {
            for (int i = 0; colors != null && i < colors.Length; i++)
            {
                if (string.Equals(colors[i], currentHex, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private static int ToByte(float channel)
        {
            return Mathf.Clamp(Mathf.RoundToInt(channel * 255f), 0, 255);
        }

        private static void SetCharacterColorField(BaseCharacter character, FieldInfo field, Color color)
        {
            if (character != null && field != null)
                field.SetValue(character, color);
        }

        private static List<CharacterMeshOptions.CharacterTexture> GetTextureList(
            CharacterMeshOptions.CharacterMeshType meshType,
            ScenarioCharacterTexturePart part)
        {
            if (meshType == null)
                return null;

            switch (part)
            {
                case ScenarioCharacterTexturePart.Head:
                    return meshType.m_headTextures;
                case ScenarioCharacterTexturePart.Torso:
                    return meshType.m_torsoTextures;
                case ScenarioCharacterTexturePart.Legs:
                    return meshType.m_legTextures;
                default:
                    return null;
            }
        }

        internal static Texture2D CopyTexture(Texture2D source)
        {
            if (source == null)
                return null;

            Texture2D copy = new Texture2D(source.width, source.height, TextureFormat.ARGB32, false);
            copy.name = source.name;
            copy.filterMode = FilterMode.Point;
            copy.wrapMode = TextureWrapMode.Clamp;
            try
            {
                copy.SetPixels(source.GetPixels());
                copy.Apply();
                return copy;
            }
            catch
            {
                RenderTexture renderTexture = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
                RenderTexture previous = RenderTexture.active;
                Graphics.Blit(source, renderTexture);
                RenderTexture.active = renderTexture;
                copy.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
                copy.Apply();
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(renderTexture);
                return copy;
            }
        }

        private static Sprite CreateTextureSprite(Texture2D texture)
        {
            if (texture == null)
                return null;

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            if (sprite != null && sprite.texture != null)
                sprite.texture.filterMode = FilterMode.Point;
            return sprite;
        }

        private static GameObject ResolveGameObject(ScenarioAuthoringTarget target)
        {
            if (target == null || target.RuntimeObject == null)
                return null;

            GameObject gameObject = target.RuntimeObject as GameObject;
            if (gameObject != null)
                return gameObject;

            Component component = target.RuntimeObject as Component;
            return component != null ? component.gameObject : null;
        }

        private static int FindFamilyIndex(FamilyMember familyMember)
        {
            FamilyManager familyManager = FamilyManager.Instance;
            List<FamilyMember> members = familyManager != null ? familyManager.GetAllFamilyMembers() : null;
            for (int i = 0; members != null && i < members.Count; i++)
            {
                if ((UnityEngine.Object)members[i] == (UnityEngine.Object)familyMember)
                    return i;
            }

            return -1;
        }
    }
}
