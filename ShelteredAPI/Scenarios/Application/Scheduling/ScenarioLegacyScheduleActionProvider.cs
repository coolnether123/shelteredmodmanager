using System.Collections.Generic;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioLegacyScheduleActionProvider : IScenarioScheduledActionProvider
    {
        public void AddActions(ScenarioDefinition definition, IList<ScenarioScheduledActionDefinition> target)
        {
            if (definition == null || target == null)
                return;

            AddFutureSurvivors(definition, target);
            AddInventory(definition, target);
            AddWeather(definition, target);
            AddQuests(definition, target);
            AddObjectActivations(definition, target);
            AddBunkerExpansionBuilds(definition, target);
        }

        private static void AddFutureSurvivors(ScenarioDefinition definition, IList<ScenarioScheduledActionDefinition> target)
        {
            for (int i = 0; definition.FamilySetup != null && definition.FamilySetup.FutureSurvivors != null && i < definition.FamilySetup.FutureSurvivors.Count; i++)
            {
                FutureSurvivorDefinition survivor = definition.FamilySetup.FutureSurvivors[i];
                if (survivor == null)
                    continue;
                ScenarioScheduledActionDefinition action = NewAction("legacy.survivor." + BuildId(survivor.Id, i), "SpawnFutureSurvivor", survivor.Arrival);
                ScenarioEffectDefinition effect = new ScenarioEffectDefinition();
                effect.Kind = ScenarioEffectKind.SpawnFutureSurvivor;
                effect.SurvivorId = survivor.Id;
                effect.TargetId = survivor.Id;
                effect.Properties.Add(new ScenarioProperty { Key = "askToJoin", Value = survivor.AskToJoin ? "true" : "false" });
                action.Effects.Add(effect);
                target.Add(action);
            }
        }

        private static void AddInventory(ScenarioDefinition definition, IList<ScenarioScheduledActionDefinition> target)
        {
            for (int i = 0; definition.StartingInventory != null && definition.StartingInventory.ScheduledChanges != null && i < definition.StartingInventory.ScheduledChanges.Count; i++)
            {
                TimedInventoryChangeDefinition change = definition.StartingInventory.ScheduledChanges[i];
                if (change == null)
                    continue;
                ScenarioScheduledActionDefinition action = NewAction("legacy.inventory." + BuildId(change.Id, i), change.Kind == ScenarioInventoryChangeKind.Remove ? "RemoveInventory" : "AddInventory", change.When);
                action.Effects.Add(new ScenarioEffectDefinition
                {
                    Kind = change.Kind == ScenarioInventoryChangeKind.Remove ? ScenarioEffectKind.RemoveInventory : ScenarioEffectKind.AddInventory,
                    ItemId = change.ItemId,
                    Quantity = change.Quantity
                });
                target.Add(action);
            }
        }

        private static void AddWeather(ScenarioDefinition definition, IList<ScenarioScheduledActionDefinition> target)
        {
            for (int i = 0; definition.TriggersAndEvents != null && definition.TriggersAndEvents.WeatherEvents != null && i < definition.TriggersAndEvents.WeatherEvents.Count; i++)
            {
                WeatherEventDefinition weather = definition.TriggersAndEvents.WeatherEvents[i];
                if (weather == null)
                    continue;
                string id = BuildId(weather.Id, i);
                ScenarioScheduledActionDefinition action = NewAction("legacy.weather." + id, "SetWeather", weather.When);
                action.Effects.Add(new ScenarioEffectDefinition
                {
                    Kind = ScenarioEffectKind.SetWeather,
                    WeatherState = weather.WeatherState,
                    DurationHours = weather.DurationHours
                });
                target.Add(action);

                if (weather.DurationHours > 0)
                {
                    ScenarioScheduledActionDefinition restore = NewAction("legacy.weather." + id + ".restore", "RestoreWeather", AddHours(weather.When, weather.DurationHours));
                    restore.Effects.Add(new ScenarioEffectDefinition { Kind = ScenarioEffectKind.RestoreWeather, WeatherState = "None" });
                    target.Add(restore);
                }
            }
        }

        private static void AddQuests(ScenarioDefinition definition, IList<ScenarioScheduledActionDefinition> target)
        {
            for (int i = 0; definition.Quests != null && definition.Quests.Quests != null && i < definition.Quests.Quests.Count; i++)
            {
                QuestDefinition quest = definition.Quests.Quests[i];
                if (quest == null || !string.IsNullOrEmpty(quest.StartTriggerId))
                    continue;
                ScenarioScheduledActionDefinition action = NewAction("legacy.quest." + BuildId(quest.Id, i), "StartQuest", quest.ScheduledStart);
                action.Effects.Add(new ScenarioEffectDefinition
                {
                    Kind = ScenarioEffectKind.StartQuest,
                    QuestId = quest.Id,
                    TargetId = quest.Id
                });
                target.Add(action);
            }
        }

        private static void AddObjectActivations(ScenarioDefinition definition, IList<ScenarioScheduledActionDefinition> target)
        {
            for (int i = 0; definition.BunkerEdits != null && definition.BunkerEdits.ObjectPlacements != null && i < definition.BunkerEdits.ObjectPlacements.Count; i++)
            {
                ObjectPlacement placement = definition.BunkerEdits.ObjectPlacements[i];
                if (placement == null || string.IsNullOrEmpty(placement.ScheduledActivationId))
                    continue;
                ScenarioScheduledActionDefinition action = NewAction(placement.ScheduledActivationId, "ActivateObject", null);
                action.GateId = placement.UnlockGateId;
                action.Effects.Add(new ScenarioEffectDefinition
                {
                    Kind = ScenarioEffectKind.ActivateObject,
                    ObjectId = !string.IsNullOrEmpty(placement.ScenarioObjectId) ? placement.ScenarioObjectId : placement.ScheduledActivationId,
                    TargetId = !string.IsNullOrEmpty(placement.ScenarioObjectId) ? placement.ScenarioObjectId : placement.ScheduledActivationId
                });
                target.Add(action);
            }

            for (int i = 0; definition.AssetReferences != null && definition.AssetReferences.SceneSpritePlacements != null && i < definition.AssetReferences.SceneSpritePlacements.Count; i++)
            {
                SceneSpritePlacement placement = definition.AssetReferences.SceneSpritePlacements[i];
                if (placement == null || string.IsNullOrEmpty(placement.ScheduledActivationId))
                    continue;
                ScenarioScheduledActionDefinition action = NewAction(placement.ScheduledActivationId, "ActivateSceneSprite", null);
                action.GateId = placement.UnlockGateId;
                string id = !string.IsNullOrEmpty(placement.ScenarioObjectId) ? placement.ScenarioObjectId : placement.Id;
                action.Effects.Add(new ScenarioEffectDefinition
                {
                    Kind = ScenarioEffectKind.ActivateObject,
                    ObjectId = !string.IsNullOrEmpty(id) ? id : placement.ScheduledActivationId,
                    TargetId = !string.IsNullOrEmpty(id) ? id : placement.ScheduledActivationId
                });
                target.Add(action);
            }
        }

        private static void AddBunkerExpansionBuilds(ScenarioDefinition definition, IList<ScenarioScheduledActionDefinition> target)
        {
            for (int i = 0; definition.BunkerGrid != null && definition.BunkerGrid.Expansions != null && i < definition.BunkerGrid.Expansions.Count; i++)
            {
                ScenarioBunkerExpansionDefinition expansion = definition.BunkerGrid.Expansions[i];
                if (expansion == null || expansion.RequiredTime == null || string.IsNullOrEmpty(expansion.Id))
                    continue;
                ScenarioScheduledActionDefinition action = NewAction("legacy.bunker.expansion." + expansion.Id, "UnlockBunkerExpansion", expansion.RequiredTime);
                action.GateId = expansion.UnlockGateId;
                action.Effects.Add(new ScenarioEffectDefinition
                {
                    Kind = ScenarioEffectKind.UnlockBunkerExpansion,
                    BunkerExpansionId = expansion.Id,
                    TargetId = expansion.Id
                });
                target.Add(action);
            }
        }

        private static ScenarioScheduledActionDefinition NewAction(string id, string type, ScenarioScheduleTime due)
        {
            ScenarioScheduledActionDefinition action = new ScenarioScheduledActionDefinition();
            action.Id = id;
            action.ActionType = type;
            action.DueTime = due != null ? due : new ScenarioScheduleTime();
            return action;
        }

        private static ScenarioScheduleTime AddHours(ScenarioScheduleTime time, int hours)
        {
            ScenarioScheduleTime result = new ScenarioScheduleTime();
            result.Day = time != null ? time.Day : 1;
            result.Hour = time != null ? time.Hour : 0;
            result.Minute = time != null ? time.Minute : 0;
            int totalHours = result.Hour + hours;
            result.Day += totalHours / 24;
            result.Hour = totalHours % 24;
            return result;
        }

        private static string BuildId(string id, int index)
        {
            return !string.IsNullOrEmpty(id) ? id : index.ToString();
        }
    }
}
