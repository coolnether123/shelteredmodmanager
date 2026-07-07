using ModAPI.Scenarios;
using UnityEngine;

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

        public static Sprite Resolve(FamilyMember member)
        {
            return member != null ? member.avatarSprite : null;
        }

        public static Sprite Resolve(FamilyMemberConfig config)
        {
            CharacterMeshOptions.CharacterTexture texture = ResolveHeadTexture(config);
            return texture != null ? texture.m_avatar : null;
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
    }
}
