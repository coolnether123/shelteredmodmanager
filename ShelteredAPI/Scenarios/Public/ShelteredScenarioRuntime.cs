using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Map;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Infrastructure.Assets;
using ShelteredAPI.Scenarios.Infrastructure.Runtime;
using UnityEngine;

namespace ShelteredAPI.Scenarios.Public{
    /// <summary>
    /// Stable runtime facade for custom scenario interactions.
    /// </summary>
    public static class ShelteredScenarioRuntime
    {
        private static readonly ScenarioSpriteRuntimeResolver RuntimeSpriteTargetResolver =
            new ScenarioSpriteRuntimeResolver();

        /// <summary>Returns the canonical root-to-leaf identity path for a live transform.</summary>
        public static string GetTransformPath(Transform transform)
        {
            return ScenarioTransformPath.Build(transform);
        }

        /// <summary>Resolves a live sprite target by its stable root-to-leaf transform path.</summary>
        public static bool TryResolveRuntimeSpriteTarget(
            string targetPath,
            ScenarioSpriteTargetComponentKind preferredKind,
            out ScenarioRuntimeSpriteTarget target)
        {
            ScenarioSpriteRuntimeResolver.ResolvedTarget resolved;
            bool found = RuntimeSpriteTargetResolver.TryResolve(targetPath, preferredKind, out resolved);
            target = found ? ScenarioRuntimeSpriteTarget.FromResolvedTarget(resolved) : null;
            return target != null;
        }

        /// <summary>Resolves a live sprite target below a runtime transform.</summary>
        public static bool TryResolveRuntimeSpriteTarget(
            Transform transform,
            ScenarioSpriteTargetComponentKind preferredKind,
            out ScenarioRuntimeSpriteTarget target)
        {
            ScenarioSpriteRuntimeResolver.ResolvedTarget resolved;
            bool found = RuntimeSpriteTargetResolver.TryResolve(transform, preferredKind, out resolved);
            target = found ? ScenarioRuntimeSpriteTarget.FromResolvedTarget(resolved) : null;
            return target != null;
        }

        /// <summary>
        /// Applies an in-memory definition to the current shelter as a preview run.
        /// The caller remains responsible for editor pause and UI ownership.
        /// </summary>
        public static IScenarioPreviewSession BeginPreview(ScenarioDefinition definition, string scenarioFilePath)
        {
            return new ScenarioPreviewSession(definition, scenarioFilePath);
        }

        /// <summary>
        /// Returns whether the active shelter world has completed its scene transition and
        /// initialized every vanilla manager required by scenario runtime interactions.
        /// </summary>
        public static bool IsWorldReady(out string blockingReason)
        {
            return ScenarioWorldReady.Evaluate(out blockingReason);
        }

        /// <summary>Returns whether one of the supported shelter scenes is active.</summary>
        public static bool IsShelterSceneActive()
        {
            return ScenarioWorldReady.IsShelterSceneActive();
        }

        /// <summary>Maps a live shelter-world position to its canonical grid cell.</summary>
        public static bool TryGetShelterGridCell(Vector3 worldPosition, out int gridX, out int gridY)
        {
            return ScenarioGridSnapService.TryGetCell(worldPosition, out gridX, out gridY);
        }

        /// <summary>Returns the canonical world-space center of one shelter grid cell.</summary>
        public static Vector3 GetShelterGridCellCenter(int gridX, int gridY)
        {
            return ScenarioGridSnapService.GetCellCenterWorldPosition(gridX, gridY);
        }

        /// <summary>Builds the stable key used to refer to an already-loaded sprite.</summary>
        public static string CreateRuntimeSpriteKey(Sprite sprite)
        {
            return ScenarioSpriteReferenceLibrary.CreateRuntimeSpriteKey(sprite);
        }

        /// <summary>Builds the stable key used to refer to a full-texture runtime sprite.</summary>
        public static string CreateRuntimeSpriteKey(Texture2D texture, string spriteName)
        {
            return ScenarioSpriteReferenceLibrary.CreateRuntimeSpriteKey(texture, spriteName);
        }

        /// <summary>Plans the deterministic map loot roll used by the live scenario runtime.</summary>
        public static ScenarioMapLootEntrySnapshot[] PlanMapLoot(
            ScenarioDefinition definition,
            MapLocationDefinition location,
            MapLootTableDefinition table)
        {
            return ScenarioMapLootSnapshotMapper.Map(
                ScenarioMapProjectionApplyService.PlanLootRolls(definition, location, table));
        }

        /// <summary>Plans deterministic map loot with an explicit seed for tooling and verification.</summary>
        public static ScenarioMapLootEntrySnapshot[] PlanMapLoot(
            ScenarioDefinition definition,
            MapLocationDefinition location,
            MapLootTableDefinition table,
            int masterSeed)
        {
            return ScenarioMapLootSnapshotMapper.Map(
                ScenarioMapProjectionApplyService.PlanLootRolls(definition, location, table, masterSeed));
        }

        /// <summary>Resolves an authored sprite reference through the runtime asset cache.</summary>
        public static Sprite ResolveSpriteAsset(
            ScenarioDefinition definition,
            string packRoot,
            string spriteId,
            string relativePath,
            string runtimeSpriteKey,
            string contextLabel)
        {
            return ScenarioRuntimeAssetFacade.ResolveSprite(
                definition,
                packRoot,
                spriteId,
                relativePath,
                runtimeSpriteKey,
                contextLabel);
        }

        /// <summary>Clears the runtime sprite resolver's cached authored assets.</summary>
        public static void InvalidateSpriteAssets()
        {
            ScenarioRuntimeAssetFacade.InvalidateSpriteAssets();
        }

        /// <summary>Registers a generated sprite under a stable runtime key.</summary>
        public static void RegisterRuntimeSprite(string runtimeSpriteKey, Sprite sprite)
        {
            ScenarioRuntimeAssetFacade.RegisterRuntimeSprite(runtimeSpriteKey, sprite);
        }

        /// <summary>Finds a sprite already loaded into the current Unity runtime.</summary>
        public static bool TryFindRuntimeSprite(string runtimeSpriteKey, out Sprite sprite)
        {
            return ScenarioRuntimeAssetFacade.TryFindRuntimeSprite(runtimeSpriteKey, out sprite);
        }

        /// <summary>Creates a full-texture sprite and registers it under its stable runtime key.</summary>
        public static Sprite CreateAndRegisterRuntimeSprite(Texture2D texture, string spriteName)
        {
            return ScenarioRuntimeAssetFacade.CreateAndRegisterRuntimeSprite(texture, spriteName);
        }

        /// <summary>Applies a sprite to one typed runtime target path.</summary>
        public static bool TryApplyRuntimeSprite(
            string targetPath,
            ScenarioSpriteTargetComponentKind targetKind,
            Sprite sprite)
        {
            return ScenarioRuntimeAssetFacade.TryApplyRuntimeSprite(targetPath, targetKind, sprite);
        }

        /// <summary>Applies one authored appearance to a live family member.</summary>
        public static bool ApplyConfiguredAppearance(
            ScenarioDefinition definition,
            string scenarioFilePath,
            FamilyMemberConfig config,
            FamilyMember member,
            out string message)
        {
            return ScenarioRuntimeAssetFacade.ApplyConfiguredAppearance(
                definition,
                scenarioFilePath,
                config,
                member,
                out message);
        }

        /// <summary>Resolves authored appearance colors using the runtime's canonical defaults and parser.</summary>
        public static void ResolveConfiguredAppearanceColors(
            FamilyMemberAppearanceConfig appearance,
            out Color hair,
            out Color skin,
            out Color shirt,
            out Color pants)
        {
            ScenarioRuntimeAssetFacade.ResolveConfiguredAppearanceColors(
                appearance,
                out hair,
                out skin,
                out shirt,
                out pants);
        }

        /// <summary>
        /// Queues a scenario-owned startup save and begins the managed transition to
        /// the shelter scene for its base game mode.
        /// </summary>
        public static bool TryLaunchScenarioWorld(ScenarioWorldLaunchRequest request, out string message)
        {
            return ScenarioWorldLaunchFacade.TryLaunch(request, out message);
        }

        /// <summary>Completes the owned loading-screen handoff once a shelter scene is ready.</summary>
        public static bool TryCompleteScenarioWorldLaunch(string expectedSceneName, string targetLabel)
        {
            return ScenarioWorldLaunchFacade.TryComplete(expectedSceneName, targetLabel);
        }

        /// <summary>Leaves the active scenario world through the canonical main-menu transition.</summary>
        public static bool TryReturnToMainMenu(out string message)
        {
            return ScenarioWorldLaunchFacade.TryReturnToMainMenu(out message);
        }

        /// <summary>Reads stable authored identity without exposing runtime marker components.</summary>
        public static bool TryGetRuntimeIdentity(GameObject gameObject, out ScenarioRuntimeIdentity identity)
        {
            return ScenarioRuntimeIdentityQuery.TryGet(gameObject, out identity);
        }

        /// <summary>Reads stable authored identity from a component's owning object hierarchy.</summary>
        public static bool TryGetRuntimeIdentity(Component component, out ScenarioRuntimeIdentity identity)
        {
            return ScenarioRuntimeIdentityQuery.TryGet(
                component != null ? component.gameObject : null,
                out identity);
        }

        public static bool FireTrigger(string triggerId)
        {
            return ScenarioTriggerRuntime.Fire(triggerId);
        }

        public static bool FireTrigger(string triggerId, string source, out string message)
        {
            return ScenarioTriggerRuntime.Fire(triggerId, source, out message);
        }

        public static ScenarioScoreSnapshot GetScoreSnapshot()
        {
            try
            {
                IScenarioScoreSnapshotService service = ScenarioRuntimeCompositionRoot.Resolve<IScenarioScoreSnapshotService>();
                return service != null ? service.GetSnapshot() : null;
            }
            catch
            {
                return null;
            }
        }

        public static void SetScoreSnapshot(ScenarioScoreSnapshot snapshot)
        {
            try
            {
                IScenarioScoreSnapshotService service = ScenarioRuntimeCompositionRoot.Resolve<IScenarioScoreSnapshotService>();
                if (service != null)
                    service.SetSnapshot(snapshot);
            }
            catch
            {
            }
        }

        public static void ClearScoreSnapshot()
        {
            try
            {
                IScenarioScoreSnapshotService service = ScenarioRuntimeCompositionRoot.Resolve<IScenarioScoreSnapshotService>();
                if (service != null)
                    service.ClearSnapshot();
            }
            catch
            {
            }
        }
    }

}
