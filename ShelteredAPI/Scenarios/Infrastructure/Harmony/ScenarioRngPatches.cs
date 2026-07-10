using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;

namespace ShelteredAPI.Scenarios.Infrastructure.Harmony
{
    /// <summary>Auditable first RNG batch. Missing Epic/Steam members are skipped, never fatal.</summary>
    internal static class ScenarioRngPatches
    {
        private static bool _installed;
        // Generated from phase6_rngsweep.md Tier 1.  Each entry is a catalogued declaring
        // method, never a broad type scan; the IL precheck below is a second drift guard.
        private static readonly string[] TargetMethodManifest = new string[]
        {
            "BreachMan|ResetSpawnTime,EnterStage_FamilySheltering,EnterStage_MainPhase,EnterStage_Breached",
            "BreachMan_Stasis|EnterStage_MainPhase", "BreachMan_Surrounded|EnterStage_MainPhase",
            "CharacterMeshOptions|GetRandomStasisMutantCharacterPreset,GetRandomTexture,Randomize_DontCallThisOutsideOfCharacterMeshOptions",
            "CombatAI_Worm|GetNextAction", "CombatAIAggressive|GetNextAction", "CombatAIBear|GetNextAction", "CombatAIDebug|GetNextAction", "CombatAIDog|GetNextAction", "CombatAIGeneric|GetNextAction", "CombatAIMutant|GetNextAction", "CombatAISurroundedBoss|GetNextAction", "CombatAIWolf|GetNextAction",
            "Companion_Scientist|SetRandomState", "CompanionAnimal|GetRandomState", "DialogueStageOpening|State_WaitFade", "DialogueStageQuest|QuestStage_Randomizer,QuestStage_EndEncounter",
            "DiceRoll|d2,d3,d4,d6,d8,d10,d12,d20,d100", "EncounterCharacter|SetupFromInspector,SetAppearance,RandomiseCurrentAnimation,MeleeAttack,Subdue,UpdateState_Throwing,UpdateState_Shooting,GetRandomBackpackItem", "EncounterDialoguePanel|OnShow",
            "EncounterGenerator|GenerateNPCs,GetRandomStasisEncounter,SetupCharacter", "EncounterLogic|AttackRoll_Melee,AttackRoll_Special,DamageRoll", "EncounterManager|OverrideTradeItems,StartEncounter_EditorDebug", "EncounterTypeBias|GetRandomEncounterType,GetUnbiasedRandomEncounterType",
            "ExpeditionMap|CreateMap,CreateStasisMap,FindClearSpace,FindMapRegionForQuest,FindRandomEmptyPlaceForMutantSwarm,FindSpaceContainingRegions,FindSuitablePlaceForMutantSwarm,GenerateMountains,GenerateRandomRegions,GenerateSettlements,GenerateWoodland,GetRandomCommonItemType,PlaceBuildingsNearToShelter,PlaceBuildingsNearToShelter_Remaining,PlaceHousingEstates,PlaceItemsAtStartLocations,PlaceLargeLocations,PlaceLoneBuildings,PlaceMediumLocations,PlaceRecyclingBuildings,PlaceReservoirsAndShacks,PlaceRestaurants,PlaceSmallLocations,PlaceSpecialLocationItems,PlaceStasisHiddenMapItems,PlaceStasisMapItems,PlaceStasisSpecialMapItems,StampIntoScratchpad,StampMutantSwarmOnToStasisMap",
            "ExplorationParty|Begin_Finished,CreateRadioDialogParametersForQuery,NpcEncounter_Premature_AutoResolve,OpenGroundEncounterCheck,Update_EncounteredNPCs_Start,Update_OpenGroundNpcEncounter_Start,Update_ReportingDiversions_Start,Update_SearchingLocation", "FactionMan|GetFactionCharacterInfo", "FamilyManager|GetRandomLeaveSpeech", "FamilySpawner|SetUpStasisMutant", "Illness_Radiation|UpdateIllness", "InventoryManager|AddRandomStartingItems", "ItemDefinition_Combat|GetAnimInfo", "Job_LeaveShelter|DoFakeInteraction", "JournalInterpreter_Combat|CreateJournalEntry", "MapRegion|AttemptToDiscoverItems,GenerateRandomItems", "NameGenerator|GetFirstName,GetPetName,GetSurname",
            "NpcDialogueScenario|QuestStage_Randomizer", "NpcVisitManager|CreateNpcVisitor,GetRandomExitPosition,GetRandomNpcColor,GetRandomPersonality,GetRandomStasisSpawnNPCType,ResetBinManSpawnTimer,ResetSpawnTimer,ResetWormsSpawnTimer,SpawnNpc,SpawnNpcs,SpawnStasisWorms,StartShelterBreach,UpdateStasis,UpdateSurvivial", "NpcVisitor|BreachAction_TryStealFood,BreachAction_TryStealItems,BreachAction_TryStealWater,BreachAction_TryToDrainPower,ChooseBreachAction,FindFoodToEat,FindPowerToDrain,Initialize,PickNextBreachState,SetUpBreachDifficultyValues,SpawnLootItems,SpawnTraderItems,Start,UpdateBreacher_MovingToNextDoor,UpdateBreacher_MovingToTarget,UpdateIntercom_StartPunish,UpdateMutantLurker_Moving,UpdateMutantLurker_StartDamagingFilter,UpdateMutantLurker_Vomiting,UpdateMutantLurker_Waiting,UpdatePasserby,UpdateWorm_ChangingFloor,UpdateWorm_DrainingPower,UpdateWorm_EatingFood,UpdateWorm_Entering,UpdateWorm_Loitering,UpdateWorm_WaitForCombat",
            "Obj_AntiBreachTrap|OnTrapTriggered", "Obj_CryoBank|AddEmbryo", "Obj_ItemBin|RemoveRandomItems", "Obj_OxygenFilter|Start", "Obj_RatTrap|UseTrap", "Obj_SnareTrap|UseTrap", "Obj_WaterFilter|Start", "ObjectManager|RandomlyBreakSomething,SetNextRandomBreakageTime", "PartyMember|DropCarriedItems", "PestManager|NibbleStuff,SpawnPest,StartManager,UpdateManager", "PsychoState|Initialize,TriggerPsycho", "QuestLibrary|GetRandomAvailableQuest,GetRandomAvailableScenario", "QuestManager|AddPendingScenarioStage,SetMissingFamilyMemberSpawnDate,UpdateNextQuestSpawnTime", "RandomStatGenerator|GetRandomCharisma,GetRandomDexterity,GetRandomIntelligence,GetRandomPerception,GetRandomStrength", "RelocationManager|SpawnNewFamilyMembers,SpawnNewLayout,SpawnNewPet", "SettlementNameGenerator|GenerateName", "WeatherManager|ChooseNewRandomWeather,StartManager", "WildlifeManager|Awake,SpawnRandomWildlife,SpawnSurroundedWildlife,UpdateManager"
        };
        private static readonly MethodInfo RangeII = AccessTools.Method(typeof(UnityEngine.Random), "Range", new Type[] { typeof(int), typeof(int) });
        private static readonly MethodInfo RangeFF = AccessTools.Method(typeof(UnityEngine.Random), "Range", new Type[] { typeof(float), typeof(float) });
        private static readonly MethodInfo Value = AccessTools.PropertyGetter(typeof(UnityEngine.Random), "value");
        private static readonly MethodInfo InitState = AccessTools.Method(typeof(UnityEngine.Random), "InitState", new Type[] { typeof(int) });
        private static readonly MethodInfo BridgeDomainII = AccessTools.Method(typeof(ModRandomBridge), "Range", new Type[] { typeof(int), typeof(int), typeof(string) });
        private static readonly MethodInfo BridgeDomainFF = AccessTools.Method(typeof(ModRandomBridge), "Range", new Type[] { typeof(float), typeof(float), typeof(string) });
        private static readonly MethodInfo BridgeDomainValue = AccessTools.Method(typeof(ModRandomBridge), "Value", new Type[] { typeof(string) });
        private static readonly MethodInfo BridgeInitState = AccessTools.Method(typeof(ModRandomBridge), "InitScenarioState", new Type[] { typeof(int) });
        private static readonly MethodInfo ExtensionShuffle = ResolveGenericMethod(AccessTools.TypeByName("ExtensionMethods"), "Shuffle", 1);
        private static readonly MethodInfo BridgeDomainShuffle = ResolveGenericMethod(typeof(ModRandomBridge), "Shuffle", 2);

        public static void Install()
        {
            if (_installed) return;
            _installed = true;
            HarmonyLib.Harmony harmony = new HarmonyLib.Harmony("ShelteredModManager.ScenarioRngPatch");
            int patched = 0;
            for (int i = 0; i < TargetMethodManifest.Length; i++)
            {
                string[] manifest = TargetMethodManifest[i].Split('|');
                Type type = AccessTools.TypeByName(manifest[0]);
                if (type == null)
                {
                    MMLog.WriteWarning("[ScenarioRngPatch] SKIP type mismatch: " + manifest[0]);
                    continue;
                }
                string[] names = manifest[1].Split(',');
                for (int j = 0; j < names.Length; j++)
                {
                    MethodInfo[] methods = type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    bool found = false;
                    for (int k = 0; k < methods.Length; k++)
                    {
                        MethodInfo target = methods[k];
                        if (target == null || target.Name != names[j] || target.IsAbstract || target.ContainsGenericParameters) continue;
                        found = true;
                        if (!ContainsRedirectableRngCall(target))
                        {
                            MMLog.WriteWarning("[ScenarioRngPatch] SKIP catalog drift/no RNG call: " + type.FullName + "." + target.Name);
                            continue;
                        }
                        try { harmony.Patch(target, transpiler: new HarmonyMethod(typeof(ScenarioRngPatches), "RngTranspiler")); patched++; }
                        catch (Exception ex) { MMLog.WriteWarning("[ScenarioRngPatch] SKIP method mismatch: " + type.FullName + "." + target.Name + " :: " + ex.Message); }
                    }
                    if (!found) MMLog.WriteWarning("[ScenarioRngPatch] SKIP method missing: " + type.FullName + "." + names[j]);
                }
            }
            MMLog.WriteInfo("[ScenarioRngPatch] Installed first tier-1 batches; methods=" + patched + ".");
        }

        public static IEnumerable<CodeInstruction> RngTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase original, ILGenerator generator)
        {
            string domain = GetDomainName(original == null ? null : original.DeclaringType);
            return FluentTranspiler.Execute(instructions, original, generator, FluentTranspiler.BuildProfile.Runtime, delegate(FluentTranspiler t)
            {
                RedirectDomainCalls(t, RangeII, BridgeDomainII, domain);
                RedirectDomainCalls(t, RangeFF, BridgeDomainFF, domain);
                RedirectDomainCalls(t, Value, BridgeDomainValue, domain);
                RedirectDomainShuffleCalls(t, domain);
                if (InitState != null)
                    t.ReplaceCalls(InitState).Optional().WithCall(BridgeInitState, "RNG scenario-owned InitState redirect");
            });
        }

        private static void RedirectDomainCalls(FluentTranspiler transpiler, MethodInfo source, MethodInfo replacement, string domain)
        {
            if (source == null || replacement == null) return;
            List<int> callIndices = transpiler.Instructions()
                .Select((instruction, index) => new { instruction, index })
                .Where(entry => entry.instruction != null && entry.instruction.Calls(source))
                .Select(entry => entry.index)
                .ToList();

            // Work backwards so each insertion leaves all remaining absolute indices stable.
            for (int i = callIndices.Count - 1; i >= 0; i--)
            {
                int callIndex = callIndices[i];
                transpiler.MoveTo(callIndex).InsertBefore(OpCodes.Ldstr, domain);
                transpiler.ReplaceAtWithCall(callIndex + 1, replacement);
            }
        }

        private static string GetDomainName(Type declaringType)
        {
            string typeName = declaringType == null ? string.Empty : declaringType.Name;
            if (typeName == "ExpeditionMap" || typeName == "RelocationManager" || typeName == "SettlementNameGenerator") return "map";

            if (typeName == "CharacterMeshOptions" || typeName == "Companion_Scientist" || typeName == "CompanionAnimal"
                || typeName == "FactionMan" || typeName == "FamilyManager" || typeName == "FamilySpawner"
                || typeName == "NameGenerator" || typeName == "RandomStatGenerator") return "characters";

            if (typeName == "DialogueStageOpening" || typeName == "DialogueStageQuest" || typeName == "EncounterCharacter"
                || typeName == "EncounterGenerator" || typeName == "EncounterManager" || typeName == "EncounterTypeBias"
                || typeName == "ExplorationParty" || typeName == "NpcDialogueScenario" || typeName == "QuestLibrary"
                || typeName == "QuestManager") return "encounters";

            if (typeName == "WeatherManager") return "weather";

            if (typeName.StartsWith("BreachMan", StringComparison.Ordinal) || typeName == "Job_LeaveShelter"
                || typeName == "NpcVisitManager" || typeName == "NpcVisitor") return "visits";

            if (typeName.StartsWith("CombatAI", StringComparison.Ordinal) || typeName == "DiceRoll"
                || typeName == "EncounterLogic" || typeName == "JournalInterpreter_Combat") return "combat";

            if (typeName == "InventoryManager" || typeName == "ItemDefinition_Combat" || typeName == "MapRegion"
                || typeName.StartsWith("Obj_", StringComparison.Ordinal) || typeName == "ObjectManager"
                || typeName == "PartyMember") return "items";

            return "misc";
        }

        private static void RedirectDomainShuffleCalls(FluentTranspiler transpiler, string domain)
        {
            if (ExtensionShuffle == null || BridgeDomainShuffle == null) return;
            List<int> callIndices = transpiler.Instructions()
                .Select((instruction, index) => new { instruction, index })
                .Where(entry => entry.instruction != null && IsGenericCall(entry.instruction, ExtensionShuffle))
                .Select(entry => entry.index)
                .ToList();

            for (int i = callIndices.Count - 1; i >= 0; i--)
            {
                int callIndex = callIndices[i];
                MethodInfo sourceCall = transpiler.Instructions().ElementAt(callIndex).operand as MethodInfo;
                if (sourceCall == null || !sourceCall.IsGenericMethod) continue;
                MethodInfo replacement = BridgeDomainShuffle.MakeGenericMethod(sourceCall.GetGenericArguments());
                transpiler.MoveTo(callIndex).InsertBefore(OpCodes.Ldstr, domain);
                transpiler.ReplaceAtWithCall(callIndex + 1, replacement);
            }
        }

        private static bool IsGenericCall(CodeInstruction instruction, MethodInfo genericDefinition)
        {
            MethodInfo call = instruction.operand as MethodInfo;
            return call != null && call.IsGenericMethod && call.GetGenericMethodDefinition() == genericDefinition;
        }

        private static MethodInfo ResolveGenericMethod(Type type, string name, int parameterCount)
        {
            if (type == null) return null;
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method.Name == name && method.IsGenericMethodDefinition && method.GetParameters().Length == parameterCount)
                    return method;
            }
            return null;
        }

        private static bool ContainsRedirectableRngCall(MethodBase target)
        {
            try
            {
                foreach (CodeInstruction instruction in PatchProcessor.GetOriginalInstructions(target))
                {
                    MethodInfo call = instruction.operand as MethodInfo;
                    if (call == RangeII || call == RangeFF || call == Value || call == InitState)
                        return true;
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioRngPatch] SKIP IL inspection failure: " + target.DeclaringType.FullName + "." + target.Name + " :: " + ex.Message);
            }
            return false;
        }
    }
}
