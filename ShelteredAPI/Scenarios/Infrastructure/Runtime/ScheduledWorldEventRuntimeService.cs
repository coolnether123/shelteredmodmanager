using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ModAPI.Core;
using ModAPI.Scenarios;

using ShelteredAPI.Content.Compatibility;
using ShelteredAPI.Infrastructure;
using ShelteredAPI.Scenarios.Application.Effects;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Effects;
using ShelteredAPI.Scenarios.Domain.Runtime;
using ShelteredAPI.Scenarios.Shared;
namespace ShelteredAPI.Scenarios.Infrastructure.Runtime{
    internal sealed class ScheduledWorldEventRuntimeService : IScenarioEffectHandler
    {
        private static readonly FieldInfo NpcVisitPendingSpawnsField = typeof(NpcVisitManager).GetField("m_pendingSpawns", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly Type NpcVisitSpawnInfoType = typeof(NpcVisitManager).GetNestedType("SpawnInfo", BindingFlags.NonPublic);
        private static readonly FieldInfo SpawnInfoTypeField = NpcVisitSpawnInfoType != null ? NpcVisitSpawnInfoType.GetField("type", BindingFlags.Public | BindingFlags.Instance) : null;
        private static readonly FieldInfo SpawnInfoAttributesField = NpcVisitSpawnInfoType != null ? NpcVisitSpawnInfoType.GetField("npcAttributes", BindingFlags.Public | BindingFlags.Instance) : null;
        private static readonly FieldInfo SpawnInfoTimerField = NpcVisitSpawnInfoType != null ? NpcVisitSpawnInfoType.GetField("spawnTimer", BindingFlags.Public | BindingFlags.Instance) : null;
        private static readonly FieldInfo SpawnInfoCarriedItemsField = NpcVisitSpawnInfoType != null ? NpcVisitSpawnInfoType.GetField("carriedItems", BindingFlags.Public | BindingFlags.Instance) : null;
        private static readonly MethodInfo BreachCanStartMethod = typeof(BreachMan).GetMethod("CanStartBreach", BindingFlags.NonPublic | BindingFlags.Instance);

        public bool CanHandle(ScenarioEffectKind kind)
        {
            return kind == ScenarioEffectKind.WorldEvent;
        }

        public bool Handle(ScenarioDefinition definition, ScenarioEffectDefinition effect, ScenarioRuntimeState state, out string message)
        {
            message = null;
            string eventType = ScenarioPropertyBag.GetString(effect != null ? effect.Properties : null, "eventType", null);
            if (string.Equals(eventType, "NpcVisit", StringComparison.OrdinalIgnoreCase))
                return ScheduleNpcVisit(definition, effect, out message);
            if (string.Equals(eventType, "Raid", StringComparison.OrdinalIgnoreCase))
                return StartRaid(effect, out message);
            if (string.Equals(eventType, "Broadcast", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventType, "RadioScan", StringComparison.OrdinalIgnoreCase))
                return ApplyBroadcastOutcome(definition, effect, out message);

            message = "Unknown WorldEvent eventType '" + (eventType ?? string.Empty) + "'.";
            return false;
        }

        private static bool ScheduleNpcVisit(ScenarioDefinition definition, ScenarioEffectDefinition effect, out string message)
        {
            message = null;
            if ((UnityEngine.Object)NpcVisitManager.Instance == (UnityEngine.Object)null)
            {
                message = "NpcVisitManager is not ready; world event visitor cannot be queued.";
                return false;
            }

            if (NpcVisitManager.Instance.npcPresent)
            {
                message = "A visitor is already present; world event visitor will retry later.";
                return false;
            }

            NpcVisitor.NpcType npcType;
            string npcTypeText = ScenarioPropertyBag.GetString(effect.Properties, "npcType", "Passerby");
            if (!TryParseNpcType(npcTypeText, out npcType))
            {
                message = "WorldEvent NpcVisit has unsupported npcType '" + (npcTypeText ?? string.Empty) + "'.";
                return false;
            }

            object spawnInfo = null;
            IList attributes = null;
            IList carriedItems = null;
            string createMessage = null;
            string seamMessage;
            if (!SeamGuard.Run(
                "scenario.world.npc-visit",
                SeamRecoveryPolicy.DisableSeamAndDegrade,
                delegate
                {
                    if (!TryCreateSpawnInfo(npcType, Math.Max(0f, ScenarioPropertyBag.GetFloat(effect.Properties, "arrivalDelaySeconds", 0f)), out spawnInfo, out attributes, out carriedItems, out createMessage))
                        throw new InvalidOperationException(createMessage);
                },
                "NPC visit bridge unavailable - scenario still playable.",
                null,
                out seamMessage))
            {
                message = seamMessage;
                return false;
            }

            int count = Math.Max(1, ScenarioPropertyBag.GetInt(effect.Properties, "count", 1));
            string characterId = ScenarioPropertyBag.GetString(effect.Properties, "characterId", null);
            for (int i = 0; i < count; i++)
            {
                FamilySpawner.CharacterAttributes authored = CreateAttributes(definition, characterId);
                if (authored != null)
                    attributes.Add(authored);
            }

            AddItemStacks(carriedItems, ScenarioPropertyBag.GetString(effect.Properties, "tradeItems", null));
            AddItemStacks(carriedItems, ScenarioPropertyBag.GetString(effect.Properties, "lootItems", null));

            IList pendingSpawns = null;
            SeamGuard.Try<IList>(
                "scenario.world.npc-visit.pending-spawns",
                SeamRecoveryPolicy.DisableSeamAndDegrade,
                delegate { return NpcVisitPendingSpawnsField.GetValue(NpcVisitManager.Instance) as IList; },
                null,
                "NPC visit bridge unavailable - scenario still playable.",
                null,
                out pendingSpawns,
                out seamMessage);
            if (pendingSpawns == null)
            {
                message = "Sheltered NPC pending-spawn list is unavailable.";
                return false;
            }

            if (!SeamGuard.Run(
                "scenario.world.npc-visit.pending-spawns",
                SeamRecoveryPolicy.DisableSeamAndDegrade,
                delegate { pendingSpawns.Add(spawnInfo); },
                "NPC visit bridge unavailable - scenario still playable.",
                null,
                out seamMessage))
            {
                message = seamMessage;
                return false;
            }

            message = "Queued WorldEvent " + npcType + " visit.";
            return true;
        }

        private static bool StartRaid(ScenarioEffectDefinition effect, out string message)
        {
            message = null;
            BreachMan breachMan = BreachMan.instance;
            if ((UnityEngine.Object)breachMan == (UnityEngine.Object)null)
            {
                message = "BreachMan is not ready; raid cannot start.";
                return false;
            }
            if (breachMan.inProgress || breachMan.currentStage != BreachMan.BreachStage.Finished)
            {
                message = "A breach is already in progress; raid will retry later.";
                return false;
            }
            if (!CanStartBreach(breachMan))
            {
                message = "The current world state cannot host a breach yet; raid will retry later.";
                return false;
            }

            string seamMessage;
            if (!SeamGuard.Run(
                "scenario.world.raid.start",
                SeamRecoveryPolicy.RetryOnce,
                delegate { breachMan.StartBreach(); },
                "Raid bridge unavailable - scenario still playable.",
                null,
                out seamMessage))
            {
                message = seamMessage;
                return false;
            }

            ApplyRaidOverrides(breachMan.difficulty, effect);
            message = "Started WorldEvent raid through BreachMan.StartBreach.";
            return true;
        }

        private static bool ApplyBroadcastOutcome(ScenarioDefinition definition, ScenarioEffectDefinition effect, out string message)
        {
            string outcome = ScenarioPropertyBag.GetString(effect.Properties, "outcome", ScenarioPropertyBag.GetString(effect.Properties, "broadcastOutcome", "None"));
            if (string.Equals(outcome, "None", StringComparison.OrdinalIgnoreCase))
            {
                StopShelterRadio();
                message = "Forced broadcast outcome: none.";
                return true;
            }

            if (string.Equals(outcome, "Trader", StringComparison.OrdinalIgnoreCase))
                return StartShelterRadioBroadcast(true, out message);

            if (string.Equals(outcome, "Recruit", StringComparison.OrdinalIgnoreCase)
                || string.Equals(outcome, "Joiner", StringComparison.OrdinalIgnoreCase))
                return StartShelterRadioBroadcast(false, out message);

            message = "Unknown broadcast outcome '" + (outcome ?? string.Empty) + "'.";
            return false;
        }

        private static bool TryCreateSpawnInfo(NpcVisitor.NpcType type, float delay, out object spawnInfo, out IList attributes, out IList carriedItems, out string message)
        {
            spawnInfo = null;
            attributes = null;
            carriedItems = null;
            message = null;

            if (NpcVisitPendingSpawnsField == null
                || NpcVisitSpawnInfoType == null
                || SpawnInfoTypeField == null
                || SpawnInfoAttributesField == null
                || SpawnInfoTimerField == null
                || SpawnInfoCarriedItemsField == null)
            {
                message = "Sheltered NPC pending-spawn internals are unavailable.";
                return false;
            }

            spawnInfo = Activator.CreateInstance(NpcVisitSpawnInfoType);
            SpawnInfoTypeField.SetValue(spawnInfo, type);
            SpawnInfoTimerField.SetValue(spawnInfo, delay);
            attributes = SpawnInfoAttributesField.GetValue(spawnInfo) as IList;
            carriedItems = SpawnInfoCarriedItemsField.GetValue(spawnInfo) as IList;
            if (attributes == null || carriedItems == null)
            {
                message = "Sheltered NPC pending-spawn attributes or item list is unavailable.";
                return false;
            }
            return true;
        }

        private static FamilySpawner.CharacterAttributes CreateAttributes(ScenarioDefinition definition, string characterId)
        {
            ScenarioNpcDefinition npc = FindScenarioNpc(definition, characterId);
            if (npc == null)
                return null;

            FamilySpawner.CharacterAttributes attributes = new FamilySpawner.CharacterAttributes();
            attributes.Randomize();
            if (!string.IsNullOrEmpty(npc.DisplayName))
                attributes.m_firstName = npc.DisplayName;
            if (!string.IsNullOrEmpty(npc.PresetId))
                attributes.m_meshId = npc.PresetId;
            if (npc.Stats != null)
            {
                if (npc.Stats.Strength > 0) attributes.m_strengthLevel = npc.Stats.Strength;
                if (npc.Stats.Dexterity > 0) attributes.m_dexterityLevel = npc.Stats.Dexterity;
                if (npc.Stats.Charisma > 0) attributes.m_charismaLevel = npc.Stats.Charisma;
                if (npc.Stats.Perception > 0) attributes.m_perceptionLevel = npc.Stats.Perception;
                if (npc.Stats.Intelligence > 0) attributes.m_intelligenceLevel = npc.Stats.Intelligence;
            }
            ItemManager.ItemType weapon;
            if (InventoryHelper.ResolveItemType(npc.WeaponItemId, out weapon))
                attributes.m_weapon = weapon;
            return attributes;
        }

        private static ScenarioNpcDefinition FindScenarioNpc(ScenarioDefinition definition, string characterId)
        {
            if (definition == null || definition.ScenarioCharacters == null || string.IsNullOrEmpty(characterId))
                return null;
            for (int i = 0; i < definition.ScenarioCharacters.Count; i++)
            {
                ScenarioNpcDefinition npc = definition.ScenarioCharacters[i];
                if (npc != null && string.Equals(npc.CharacterId, characterId, StringComparison.OrdinalIgnoreCase))
                    return npc;
            }
            return null;
        }

        private static bool TryParseNpcType(string value, out NpcVisitor.NpcType type)
        {
            type = NpcVisitor.NpcType.Passerby;
            if (string.Equals(value, "Trader", StringComparison.OrdinalIgnoreCase))
            {
                type = NpcVisitor.NpcType.Trader;
                return true;
            }
            if (string.Equals(value, "Joiner", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Recruit", StringComparison.OrdinalIgnoreCase))
            {
                type = NpcVisitor.NpcType.Joiner;
                return true;
            }
            if (string.Equals(value, "Passerby", StringComparison.OrdinalIgnoreCase))
            {
                type = NpcVisitor.NpcType.Passerby;
                return true;
            }
            return false;
        }

        private static void AddItemStacks(IList target, string spec)
        {
            if (target == null || string.IsNullOrEmpty(spec))
                return;

            string[] entries = spec.Split(',');
            for (int i = 0; i < entries.Length; i++)
            {
                string[] parts = entries[i].Split(':');
                if (parts.Length == 0)
                    continue;
                string itemId = parts[0] != null ? parts[0].Trim() : string.Empty;
                int count = 1;
                if (parts.Length > 1)
                    int.TryParse(parts[1], out count);
                ItemManager.ItemType type;
                if (count > 0 && InventoryHelper.ResolveItemType(itemId, out type))
                    target.Add(new ItemStack(type, count));
            }
        }

        private static bool CanStartBreach(BreachMan breachMan)
        {
            if (BreachCanStartMethod == null)
                return true;

            bool canStart = true;
            string message;
            if (SeamGuard.Try<bool>(
                "scenario.world.raid.can-start",
                SeamRecoveryPolicy.RetryOnce,
                delegate { return (bool)BreachCanStartMethod.Invoke(breachMan, new object[0]); },
                true,
                "Raid readiness bridge unavailable - scenario still playable.",
                null,
                out canStart,
                out message))
            {
                return canStart;
            }

            return true;
        }

        private static void ApplyRaidOverrides(BreachMan.BreachDifficulty difficulty, ScenarioEffectDefinition effect)
        {
            if (difficulty == null || effect == null)
                return;

            int minNpcs = ScenarioPropertyBag.GetInt(effect.Properties, "minNpcs", difficulty.m_minNpcCount);
            int maxNpcs = ScenarioPropertyBag.GetInt(effect.Properties, "maxNpcs", difficulty.m_maxNpcCount);
            int count = ScenarioPropertyBag.GetInt(effect.Properties, "count", 0);
            if (count > 0)
            {
                minNpcs = count;
                maxNpcs = count;
            }
            difficulty.m_minNpcCount = Math.Max(1, minNpcs);
            difficulty.m_maxNpcCount = Math.Max(difficulty.m_minNpcCount, maxNpcs);
            difficulty.m_damagePerSecond = Math.Max(0f, ScenarioPropertyBag.GetFloat(effect.Properties, "damagePerSecond", difficulty.m_damagePerSecond));
            difficulty.m_minTimeLimit = Math.Max(0f, ScenarioPropertyBag.GetFloat(effect.Properties, "minTimeLimit", difficulty.m_minTimeLimit));
            difficulty.m_maxTimeLimit = Math.Max(difficulty.m_minTimeLimit, ScenarioPropertyBag.GetFloat(effect.Properties, "maxTimeLimit", difficulty.m_maxTimeLimit));
            ReplaceBiasList(difficulty.m_Weapons, ScenarioPropertyBag.GetString(effect.Properties, "weapons", null));
            ReplaceBiasList(difficulty.m_Armor, ScenarioPropertyBag.GetString(effect.Properties, "armor", null));
        }

        private static void ReplaceBiasList(List<ItemBias> target, string spec)
        {
            if (target == null || string.IsNullOrEmpty(spec))
                return;
            target.Clear();
            string[] entries = spec.Split(',');
            for (int i = 0; i < entries.Length; i++)
            {
                string[] parts = entries[i].Split(':');
                if (parts.Length == 0)
                    continue;
                ItemManager.ItemType type;
                if (!InventoryHelper.ResolveItemType(parts[0].Trim(), out type))
                    continue;
                int bias = 1;
                if (parts.Length > 1)
                    int.TryParse(parts[1], out bias);
                target.Add(new ItemBias { itemType = type, bias = Math.Max(1, bias) });
            }
        }

        private static void StopShelterRadio()
        {
            Obj_Radio radio = null;
            string message;
            SeamGuard.Run(
                "scenario.world.broadcast.radio",
                SeamRecoveryPolicy.RetryOnce,
                delegate
                {
                    if (ShelteredAPI.GameState.GameUtil.TryGetShelterRadio(out radio) && radio != null)
                        radio.StopBroadcasting();
                },
                "Broadcast bridge unavailable - scenario still playable.",
                null,
                out message);
        }

        private static bool StartShelterRadioBroadcast(bool trader, out string message)
        {
            message = null;
            Obj_Radio radio = null;
            if (!ShelteredAPI.GameState.GameUtil.TryGetShelterRadio(out radio) || radio == null)
            {
                message = "Shelter radio is not ready; world event broadcast will retry later.";
                return false;
            }
            if (radio.broadcasting || radio.scanning)
            {
                message = "Shelter radio is already in use; world event broadcast will retry later.";
                return false;
            }

            string seamMessage;
            if (!SeamGuard.Run(
                "scenario.world.broadcast.radio",
                SeamRecoveryPolicy.RetryOnce,
                delegate
                {
                    ScenarioWorldEventRuntimeState.BeginAuthoredRadioBroadcastDispatch();
                    try
                    {
                        if (trader)
                            radio.StartBroadcastingForTraders();
                        else
                            radio.StartBroadcastingForRecruits();
                    }
                    finally
                    {
                        ScenarioWorldEventRuntimeState.EndAuthoredRadioBroadcastDispatch();
                    }
                },
                "Broadcast bridge unavailable - scenario still playable.",
                null,
                out seamMessage))
            {
                message = seamMessage;
                return false;
            }

            message = "Forced WorldEvent radio broadcast: " + (trader ? "Trader" : "Recruit") + ".";
            return true;
        }
    }
}
