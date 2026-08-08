using System;
using System.Collections.Generic;
using System.Reflection;
using ModAPI.Scenarios;
using ShelteredAPI.Content;
using UnityEngine;

using ShelteredAPI.Scenarios.Application.Runtime;
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
        private sealed class ResolvedCharacterTarget
        {
            public BaseCharacter Character;
            public CharacterMesh Mesh;
            public CharacterMeshOptions.CharacterMeshType MeshType;
        }

        private static readonly FieldInfo BaseCharacterMeshField = typeof(BaseCharacter).GetField("m_mesh", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo BaseCharacterHeadTextureField = typeof(BaseCharacter).GetField("m_headTexture", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo BaseCharacterTorsoTextureField = typeof(BaseCharacter).GetField("m_torsoTexture", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo BaseCharacterLegTextureField = typeof(BaseCharacter).GetField("m_legTexture", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo BaseCharacterAvatarSpriteField = typeof(BaseCharacter).GetField("m_avatarSprite", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo BaseCharacterMeshIdField = typeof(BaseCharacter).GetField("m_characterMeshId", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo BaseCharacterMaleField = typeof(BaseCharacter).GetField("m_male", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo BaseCharacterChildField = typeof(BaseCharacter).GetField("m_child", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo BaseCharacterHairColorField = typeof(BaseCharacter).GetField("m_hairColor", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo BaseCharacterSkinColorField = typeof(BaseCharacter).GetField("m_skinColor", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo BaseCharacterShirtColorField = typeof(BaseCharacter).GetField("m_shirtColor", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo BaseCharacterPantsColorField = typeof(BaseCharacter).GetField("m_pantsColor", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly Color DefaultHairColor = new Color(0.23f, 0.13f, 0.08f, 1f);
        private static readonly Color DefaultSkinColor = new Color(0.84f, 0.61f, 0.45f, 1f);
        private static readonly Color DefaultShirtColor = new Color(0.37f, 0.45f, 0.58f, 1f);
        private static readonly Color DefaultPantsColor = new Color(0.25f, 0.25f, 0.25f, 1f);

        private readonly IScenarioSpriteAssetResolver _assetResolver;

        internal ScenarioCharacterAppearanceService(IScenarioSpriteAssetResolver assetResolver)
        {
            _assetResolver = assetResolver;
        }

        private static bool TryResolve(FamilyMember familyMember, out ResolvedCharacterTarget resolved, out string message)
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

            resolved = new ResolvedCharacterTarget
            {
                Character = familyMember,
                Mesh = mesh,
                MeshType = meshType
            };
            return true;
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
            if (!TryResolve(member, out target, out message))
                return false;

            ApplyConfiguredPart(definition, scenarioFilePath, target, config.Appearance.HeadTextureId, config.Appearance.HeadTexturePath, ScenarioCharacterTexturePart.Head);
            ApplyConfiguredPart(definition, scenarioFilePath, target, config.Appearance.TorsoTextureId, config.Appearance.TorsoTexturePath, ScenarioCharacterTexturePart.Torso);
            ApplyConfiguredPart(definition, scenarioFilePath, target, config.Appearance.LegTextureId, config.Appearance.LegTexturePath, ScenarioCharacterTexturePart.Legs);
            ApplyConfiguredColors(target, config.Appearance);
            return true;
        }

        public bool AlignLiveMesh(FamilyMemberConfig config, FamilyMember member, out string message)
        {
            message = null;
            if (config == null || member == null)
            {
                message = "Character mesh alignment target was unavailable.";
                return false;
            }

            string meshId = ResolveConfiguredMeshId(config, member);
            CharacterMeshOptions.CharacterMeshType meshType = ResolveMeshType(meshId);
            if (meshType == null || (UnityEngine.Object)meshType.m_meshAsset == (UnityEngine.Object)null)
            {
                message = "Character mesh definition '" + meshId + "' could not be resolved.";
                return false;
            }

            SanitizeAppearanceTextures(config.Appearance);

            if (BaseCharacterMaleField != null)
                BaseCharacterMaleField.SetValue(member, meshType.m_male);
            if (BaseCharacterChildField != null)
                BaseCharacterChildField.SetValue(member, !meshType.m_adult);
            if (BaseCharacterMeshIdField != null)
                BaseCharacterMeshIdField.SetValue(member, meshId);

            CharacterMesh current = BaseCharacterMeshField != null ? BaseCharacterMeshField.GetValue(member) as CharacterMesh : null;
            if (current != null && string.Equals(current.meshId, meshId, StringComparison.OrdinalIgnoreCase))
                return true;

            CharacterMesh replacement = InstantiateCharacterMesh(member.transform, meshType);
            if (replacement == null)
            {
                message = "Character mesh '" + meshId + "' could not be instantiated.";
                return false;
            }

            CharacterMesh old = current;
            member.SetCharacterMesh(replacement);
            member.UpdateSpritesAndAnimators();
            ApplyReplacementDefaults(member, replacement, meshType, config.Appearance);
            if ((UnityEngine.Object)old != (UnityEngine.Object)null && (UnityEngine.Object)old.gameObject != (UnityEngine.Object)null)
                UnityEngine.Object.Destroy(old.gameObject);

            return true;
        }

        private void SanitizeAppearanceTextures(FamilyMemberAppearanceConfig appearance)
        {
            if (appearance == null)
                return;

            CharacterMeshOptions.CharacterMeshType meshType = ResolveMeshType(appearance.MeshId);
            SanitizeAppearanceTexture(appearance, meshType, ScenarioCharacterTexturePart.Head);
            SanitizeAppearanceTexture(appearance, meshType, ScenarioCharacterTexturePart.Torso);
            SanitizeAppearanceTexture(appearance, meshType, ScenarioCharacterTexturePart.Legs);
        }

        public static void ResolveConfiguredColors(
            FamilyMemberAppearanceConfig appearance,
            out Color hair,
            out Color skin,
            out Color shirt,
            out Color pants)
        {
            hair = DefaultHairColor;
            skin = DefaultSkinColor;
            shirt = DefaultShirtColor;
            pants = DefaultPantsColor;
            if (appearance == null)
                return;

            ApplyConfiguredColorValue(appearance.HairColorHex, ref hair);
            ApplyConfiguredColorValue(appearance.SkinColorHex, ref skin);
            ApplyConfiguredColorValue(appearance.ShirtColorHex, ref shirt);
            ApplyConfiguredColorValue(appearance.PantsColorHex, ref pants);
        }

        private static void ApplyConfiguredColorValue(string colorHex, ref Color target)
        {
            Color parsed;
            if (TryParseColorHex(colorHex, out parsed))
                target = parsed;
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

        private static CharacterMesh InstantiateCharacterMesh(Transform parent, CharacterMeshOptions.CharacterMeshType meshType)
        {
            if (parent == null || meshType == null || (UnityEngine.Object)meshType.m_meshAsset == (UnityEngine.Object)null)
                return null;

            GameObject meshObject = UnityEngine.Object.Instantiate(meshType.m_meshAsset) as GameObject;
            if ((UnityEngine.Object)meshObject == (UnityEngine.Object)null)
                return null;

            meshObject.name = "mesh";
            meshObject.transform.parent = parent;
            meshObject.transform.localPosition = meshType.m_meshOffset;
            meshObject.transform.localScale = Vector3.one;
            meshObject.transform.rotation = Quaternion.Euler(new Vector3(0f, 180f, 0f));
            meshObject.AddComponent<CharacterAnimEvents>();

            CharacterMesh mesh = meshObject.AddComponent<CharacterMesh>();
            if ((UnityEngine.Object)mesh == (UnityEngine.Object)null)
            {
                UnityEngine.Object.Destroy(meshObject);
                return null;
            }

            mesh.SetMeshType(meshType, "CharacterMesh");
            DayNightTransition tint = meshObject.AddComponent<DayNightTransition>();
            if ((UnityEngine.Object)tint != (UnityEngine.Object)null)
            {
                tint.TransitionColor1 = new Color(0.3529412f, 0.392156869f, 0.7058824f);
                tint.checkForChild = false;
                tint.checkRecursive = false;
                tint.alphaTransition = false;
                tint.particleTransition = false;
            }

            Animator animator = meshObject.GetComponent<Animator>();
            if ((UnityEngine.Object)animator != (UnityEngine.Object)null)
            {
                animator.runtimeAnimatorController = meshType.m_anims;
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            }

            return mesh;
        }

        private void ApplyReplacementDefaults(
            FamilyMember member,
            CharacterMesh mesh,
            CharacterMeshOptions.CharacterMeshType meshType,
            FamilyMemberAppearanceConfig appearance)
        {
            if (member == null || mesh == null)
                return;

            mesh.SetColor(CharacterMesh.ColorCustomization.HairColor, member.hairColor);
            mesh.SetColor(CharacterMesh.ColorCustomization.SkinColor, member.skinColor);
            mesh.SetColor(CharacterMesh.ColorCustomization.ShirtColor, member.shirtColor);
            mesh.SetColor(CharacterMesh.ColorCustomization.PantsColor, member.pantsColor);

            ResolvedCharacterTarget target = new ResolvedCharacterTarget
            {
                Character = member,
                Mesh = mesh,
                MeshType = meshType
            };

            string ignored;
            ApplyTextureId(target, ScenarioCharacterTexturePart.Head, GetAppearanceTextureId(appearance, ScenarioCharacterTexturePart.Head) ?? "default", null, out ignored);
            ApplyTextureId(target, ScenarioCharacterTexturePart.Torso, GetAppearanceTextureId(appearance, ScenarioCharacterTexturePart.Torso) ?? "default", null, out ignored);
            ApplyTextureId(target, ScenarioCharacterTexturePart.Legs, GetAppearanceTextureId(appearance, ScenarioCharacterTexturePart.Legs) ?? "default", null, out ignored);
        }

        private static string ResolveConfiguredMeshId(FamilyMemberConfig config, FamilyMember member)
        {
            if (config != null && config.Appearance != null && !string.IsNullOrEmpty(config.Appearance.MeshId))
                return config.Appearance.MeshId;

            ScenarioGender gender = config != null ? config.Gender : ScenarioGender.Any;
            if (gender == ScenarioGender.Any && member != null)
                gender = member.isMale ? ScenarioGender.Male : ScenarioGender.Female;

            bool adult = member == null || member.isAdult;
            if (config != null && config.Appearance != null && config.Appearance.IsAdult.HasValue)
                adult = config.Appearance.IsAdult.Value;
            else if (config != null && config.ExactAge.HasValue)
                adult = config.ExactAge.Value >= 18;

            if (gender == ScenarioGender.Female)
                return adult ? "woman" : "girl";
            return adult ? "man" : "boy";
        }

        private static void SanitizeAppearanceTexture(
            FamilyMemberAppearanceConfig appearance,
            CharacterMeshOptions.CharacterMeshType meshType,
            ScenarioCharacterTexturePart part)
        {
            string textureId = GetAppearanceTextureId(appearance, part);
            if (string.IsNullOrEmpty(textureId) || FindTextureEntry(meshType, part, textureId) != null)
                return;

            string texturePath = GetAppearanceTexturePath(appearance, part);
            if (!string.IsNullOrEmpty(texturePath) && !string.IsNullOrEmpty(texturePath.Trim()))
                return;

            UpsertAppearanceTexture(appearance, part, "default", null);
        }

        private static string GetAppearanceTextureId(FamilyMemberAppearanceConfig appearance, ScenarioCharacterTexturePart part)
        {
            if (appearance == null)
                return null;

            switch (part)
            {
                case ScenarioCharacterTexturePart.Head: return appearance.HeadTextureId;
                case ScenarioCharacterTexturePart.Torso: return appearance.TorsoTextureId;
                case ScenarioCharacterTexturePart.Legs: return appearance.LegTextureId;
                default: return null;
            }
        }

        private static string GetAppearanceTexturePath(FamilyMemberAppearanceConfig appearance, ScenarioCharacterTexturePart part)
        {
            if (appearance == null)
                return null;

            switch (part)
            {
                case ScenarioCharacterTexturePart.Head: return appearance.HeadTexturePath;
                case ScenarioCharacterTexturePart.Torso: return appearance.TorsoTexturePath;
                case ScenarioCharacterTexturePart.Legs: return appearance.LegTexturePath;
                default: return null;
            }
        }

        private static void UpsertAppearanceTexture(FamilyMemberAppearanceConfig appearance, ScenarioCharacterTexturePart part, string textureId, string texturePath)
        {
            if (appearance == null)
                return;

            switch (part)
            {
                case ScenarioCharacterTexturePart.Head:
                    appearance.HeadTextureId = textureId;
                    appearance.HeadTexturePath = texturePath;
                    break;
                case ScenarioCharacterTexturePart.Torso:
                    appearance.TorsoTextureId = textureId;
                    appearance.TorsoTexturePath = texturePath;
                    break;
                case ScenarioCharacterTexturePart.Legs:
                    appearance.LegTextureId = textureId;
                    appearance.LegTexturePath = texturePath;
                    break;
            }
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

            CharacterMesh.TextureType meshPart = ToMeshTexturePart(part);
            target.Mesh.SetTexture(meshPart, textureId);
            if (!string.Equals(GetMeshTextureId(target.Mesh, part), textureId, StringComparison.OrdinalIgnoreCase))
            {
                message = "Character mesh rejected texture '" + textureId + "'.";
                return false;
            }

            switch (part)
            {
                case ScenarioCharacterTexturePart.Head:
                    if (BaseCharacterMeshIdField != null && !string.IsNullOrEmpty(target.Mesh.meshId))
                        BaseCharacterMeshIdField.SetValue(target.Character, target.Mesh.meshId);
                    if (BaseCharacterHeadTextureField != null)
                        BaseCharacterHeadTextureField.SetValue(target.Character, textureId);
                    SetAvatarSprite(target, textureId, avatarSprite);
                    break;
                case ScenarioCharacterTexturePart.Torso:
                    if (BaseCharacterTorsoTextureField != null)
                        BaseCharacterTorsoTextureField.SetValue(target.Character, textureId);
                    break;
                case ScenarioCharacterTexturePart.Legs:
                    if (BaseCharacterLegTextureField != null)
                        BaseCharacterLegTextureField.SetValue(target.Character, textureId);
                    break;
            }

            target.Mesh.RefreshTextures();
            return true;
        }

        private static string GetMeshTextureId(CharacterMesh mesh, ScenarioCharacterTexturePart part)
        {
            if (mesh == null)
                return null;

            switch (part)
            {
                case ScenarioCharacterTexturePart.Head:
                    return mesh.headTexture;
                case ScenarioCharacterTexturePart.Torso:
                    return mesh.torsoTexture;
                case ScenarioCharacterTexturePart.Legs:
                    return mesh.legTexture;
                default:
                    return null;
            }
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

    }
}
