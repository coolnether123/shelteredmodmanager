using System;
using System.IO;
using ModAPI.Scenarios;
using ShelteredAPI.Content;

namespace ShelteredAPI.Scenarios
{
    public sealed class ScenarioValidatorImpl
    {
        private readonly ScenarioValidator _neutralValidator;

        public ScenarioValidatorImpl()
            : this(new ScenarioValidator())
        {
        }

        public ScenarioValidatorImpl(ScenarioValidator neutralValidator)
        {
            _neutralValidator = neutralValidator ?? new ScenarioValidator();
        }

        public ScenarioValidationResult Validate(ScenarioDefinition definition, string scenarioFilePath)
        {
            ScenarioValidationResult result = _neutralValidator.Validate(definition, scenarioFilePath);
            ValidateLoadableAssets(definition, scenarioFilePath, result);
            return result;
        }

        private static void ValidateLoadableAssets(ScenarioDefinition definition, string scenarioFilePath, ScenarioValidationResult result)
        {
            if (definition == null || definition.AssetReferences == null || string.IsNullOrEmpty(scenarioFilePath))
                return;

            string packRoot = Path.GetDirectoryName(scenarioFilePath);
            if (string.IsNullOrEmpty(packRoot))
                return;

            for (int i = 0; i < definition.AssetReferences.CustomSprites.Count; i++)
            {
                SpriteRef sprite = definition.AssetReferences.CustomSprites[i];
                ValidateSprite(packRoot, sprite != null ? sprite.RelativePath : null, "sprite", result);
            }

            for (int i = 0; i < definition.AssetReferences.CustomIcons.Count; i++)
            {
                IconRef icon = definition.AssetReferences.CustomIcons[i];
                ValidateSprite(packRoot, icon != null ? icon.RelativePath : null, "icon", result);
            }

            for (int i = 0; i < definition.AssetReferences.SpriteSwaps.Count; i++)
            {
                SpriteSwapRule swap = definition.AssetReferences.SpriteSwaps[i];
                if (swap != null && !string.IsNullOrEmpty(swap.RelativePath))
                    ValidateSprite(packRoot, swap.RelativePath, "sprite swap", result);
            }

            if (definition.FamilySetup != null && definition.FamilySetup.Members != null)
            {
                for (int i = 0; i < definition.FamilySetup.Members.Count; i++)
                {
                    FamilyMemberConfig member = definition.FamilySetup.Members[i];
                    FamilyMemberAppearanceConfig appearance = member != null ? member.Appearance : null;
                    if (appearance == null)
                        continue;

                    if (!string.IsNullOrEmpty(appearance.HeadTexturePath))
                        ValidateSprite(packRoot, appearance.HeadTexturePath, "family head texture", result);
                    if (!string.IsNullOrEmpty(appearance.TorsoTexturePath))
                        ValidateSprite(packRoot, appearance.TorsoTexturePath, "family torso texture", result);
                    if (!string.IsNullOrEmpty(appearance.LegTexturePath))
                        ValidateSprite(packRoot, appearance.LegTexturePath, "family leg texture", result);
                }
            }
        }

        private static void ValidateSprite(string packRoot, string relativePath, string kind, ScenarioValidationResult result)
        {
            if (string.IsNullOrEmpty(relativePath))
                return;

            try
            {
                if (AssetLoader.LoadSprite(packRoot, relativePath, 100f) == null)
                    result.AddError("Scenario " + kind + " could not be loaded as a sprite through AssetLoader: " + relativePath);
            }
            catch (Exception ex)
            {
                result.AddError("Scenario " + kind + " could not be loaded through AssetLoader: " + relativePath + " (" + ex.Message + ")");
            }
        }
    }
}
