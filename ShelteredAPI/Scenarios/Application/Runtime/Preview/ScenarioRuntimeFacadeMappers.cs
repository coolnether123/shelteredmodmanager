using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Application.Selection;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Map;
using ShelteredAPI.Scenarios.Infrastructure.Assets;
using ShelteredAPI.Scenarios.Infrastructure.Runtime;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Core;
using ShelteredAPI.Scenarios.Public;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShelteredAPI.Scenarios.Application.Runtime
{
    internal static class ScenarioMapLootSnapshotMapper
    {
        public static ScenarioMapLootEntrySnapshot[] Map(List<MapLootProjectionEntry> entries)
        {
            ScenarioMapLootEntrySnapshot[] snapshots =
                new ScenarioMapLootEntrySnapshot[entries != null ? entries.Count : 0];
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                MapLootProjectionEntry entry = entries[i];
                snapshots[i] = entry == null
                    ? null
                    : new ScenarioMapLootEntrySnapshot
                    {
                        ItemId = entry.ItemId,
                        Quantity = entry.Quantity,
                        Hidden = entry.Hidden,
                        HiddenUnlockItemId = entry.HiddenUnlockItemId
                    };
            }
            return snapshots;
        }
    }

    internal static class ScenarioRuntimeAssetFacade
    {
        public static Sprite ResolveSprite(
            ScenarioDefinition definition,
            string packRoot,
            string spriteId,
            string relativePath,
            string runtimeSpriteKey,
            string contextLabel)
        {
            return ScenarioRuntimeCompositionRoot.Resolve<IScenarioSpriteAssetResolver>().ResolveSprite(
                definition,
                packRoot,
                spriteId,
                relativePath,
                runtimeSpriteKey,
                contextLabel);
        }

        public static void InvalidateSpriteAssets()
        {
            ScenarioRuntimeCompositionRoot.Resolve<IScenarioSpriteAssetResolver>().Invalidate();
        }

        public static void RegisterRuntimeSprite(string runtimeSpriteKey, Sprite sprite)
        {
            ScenarioSpriteReferenceLibrary.RegisterGeneratedSprite(runtimeSpriteKey, sprite);
        }

        public static bool TryFindRuntimeSprite(string runtimeSpriteKey, out Sprite sprite)
        {
            return ScenarioSpriteReferenceLibrary.TryFindLoadedSprite(runtimeSpriteKey, out sprite);
        }

        public static Sprite CreateAndRegisterRuntimeSprite(Texture2D texture, string spriteName)
        {
            Sprite sprite = ScenarioSpriteReferenceLibrary.GetOrCreateFullTextureSprite(texture, spriteName);
            ScenarioSpriteReferenceLibrary.RegisterGeneratedSprite(
                ScenarioSpriteReferenceLibrary.CreateRuntimeSpriteKey(sprite),
                sprite);
            return sprite;
        }

        public static bool TryApplyRuntimeSprite(
            string targetPath,
            ScenarioSpriteTargetComponentKind targetKind,
            Sprite sprite)
        {
            ScenarioSpriteRuntimeResolver.ResolvedTarget target;
            return TryResolveRuntimeTarget(targetPath, targetKind, out target)
                && ScenarioSpriteRuntimeMutationService.TryApply(target, sprite);
        }

        public static bool TryPreviewRuntimeSpriteFrame(
            string targetPath,
            ScenarioSpriteTargetComponentKind targetKind,
            Sprite sprite)
        {
            ScenarioSpriteRuntimeResolver.ResolvedTarget target;
            return TryResolveRuntimeTarget(targetPath, targetKind, out target)
                && ScenarioSpriteRuntimeMutationService.TryPreviewEditedFrame(target, sprite);
        }

        public static bool TryPlayRuntimeSpriteAnimation(
            string targetPath,
            ScenarioSpriteTargetComponentKind targetKind,
            Sprite[] frames,
            float[] durations,
            float speed)
        {
            ScenarioSpriteRuntimeResolver.ResolvedTarget target;
            return TryResolveRuntimeTarget(targetPath, targetKind, out target)
                && ScenarioSpriteRuntimeMutationService.TryPlayEditedAnimation(target, frames, durations, speed);
        }

        public static void StopRuntimeSpriteAnimation(
            string targetPath,
            ScenarioSpriteTargetComponentKind targetKind)
        {
            ScenarioSpriteRuntimeResolver.ResolvedTarget target;
            if (TryResolveRuntimeTarget(targetPath, targetKind, out target))
                ScenarioSpriteRuntimeMutationService.StopEditedAnimation(target);
        }

        private static bool TryResolveRuntimeTarget(
            string targetPath,
            ScenarioSpriteTargetComponentKind targetKind,
            out ScenarioSpriteRuntimeResolver.ResolvedTarget target)
        {
            return ScenarioRuntimeCompositionRoot.Resolve<ScenarioSpriteRuntimeResolver>()
                .TryResolve(targetPath, targetKind, out target);
        }

        public static bool ApplyConfiguredAppearance(
            ScenarioDefinition definition,
            string scenarioFilePath,
            FamilyMemberConfig config,
            FamilyMember member,
            out string message)
        {
            return ScenarioRuntimeCompositionRoot.Resolve<ScenarioCharacterAppearanceService>()
                .ApplyConfiguredAppearance(definition, scenarioFilePath, config, member, out message);
        }

        public static void ResolveConfiguredAppearanceColors(
            FamilyMemberAppearanceConfig appearance,
            out Color hair,
            out Color skin,
            out Color shirt,
            out Color pants)
        {
            ScenarioCharacterAppearanceService.ResolveConfiguredColors(
                appearance,
                out hair,
                out skin,
                out shirt,
                out pants);
        }
    }

    internal static class ScenarioWorldLaunchFacade
    {
        public static bool TryLaunch(ScenarioWorldLaunchRequest request, out string message)
        {
            if (request == null)
            {
                message = "Scenario world launch request was null.";
                return false;
            }

            string seedMessage;
            ScenarioSeedPolicy.TryApplyForScenario(
                request.Definition,
                "scenario-world-launch",
                out seedMessage);

            bool started = ScenarioRuntimeCompositionRoot.Resolve<ScenarioLaunchCoordinator>()
                .QueuePreviewSceneReload(
                    request.StorageScenarioId,
                    request.StartupSave,
                    request.SaveType,
                    request.TargetLabel,
                    request.BaseGameMode,
                    out message);
            if (!started)
                ModRandomBridge.SetScenarioFixedSeedActive(false);
            else if (!string.IsNullOrEmpty(seedMessage))
                message = seedMessage;
            return started;
        }

        public static bool TryComplete(string expectedSceneName, string targetLabel)
        {
            return ScenarioLoadingTransitionGuard.TryCompleteManagedTransition(
                expectedSceneName,
                targetLabel);
        }

        public static bool TryReturnToMainMenu(out string message)
        {
            try
            {
                PauseManager.Resume();
                if (LoadingScreen.Instance != null)
                {
                    ScenarioLoadingTransitionGuard.PrepareForManagedTransition(
                        "MenuScene after scenario world close");
                    LoadingScreen.Instance.ShowLoadingScreen("MenuScene");
                    message = "Main-menu loading transition started.";
                    return true;
                }

                if (LoadingTransitionRuntime.TryReturnToMainMenu())
                {
                    message = "Returned to the main menu panel.";
                    return true;
                }

                SceneManager.LoadScene("MenuScene");
                message = "Main-menu scene load started.";
                return true;
            }
            catch (System.Exception ex)
            {
                message = "Could not return to the main menu: " + ex.Message;
                return false;
            }
        }
    }

    internal static class ScenarioRuntimeIdentityQuery
    {
        public static bool TryGet(GameObject gameObject, out ScenarioRuntimeIdentity identity)
        {
            identity = null;
            if (gameObject == null)
                return false;

            ScenarioSceneSpritePlacementMarker sceneSprite =
                gameObject.GetComponentInParent<ScenarioSceneSpritePlacementMarker>();
            if (sceneSprite != null)
            {
                identity = new ScenarioRuntimeIdentity
                {
                    Kind = ScenarioRuntimeIdentityKind.SceneSpritePlacement,
                    PlacementId = sceneSprite.PlacementId,
                    ScenarioObjectId = sceneSprite.ScenarioObjectId,
                    RuntimeBindingKey = sceneSprite.RuntimeBindingKey,
                    GridX = sceneSprite.GridX,
                    GridY = sceneSprite.GridY
                };
                return true;
            }

            ScenarioObjectPlacementRuntimeBinding objectPlacement =
                gameObject.GetComponentInParent<ScenarioObjectPlacementRuntimeBinding>();
            if (objectPlacement == null)
                return false;

            identity = new ScenarioRuntimeIdentity
            {
                Kind = ScenarioRuntimeIdentityKind.ObjectPlacement,
                ScenarioObjectId = objectPlacement.ScenarioObjectId,
                RuntimeBindingKey = objectPlacement.RuntimeBindingKey,
                GridX = objectPlacement.GridX,
                GridY = objectPlacement.GridY
            };
            return true;
        }
    }
}
