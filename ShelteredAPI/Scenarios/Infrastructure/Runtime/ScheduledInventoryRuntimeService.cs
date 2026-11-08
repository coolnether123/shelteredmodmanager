using System;
using ShelteredAPI.Content;
using ShelteredAPI.Storage;
using ModAPI.Scenarios;

using ShelteredAPI.Content.Compatibility;
using ShelteredAPI.Scenarios.Application.Conditions;
using ShelteredAPI.Scenarios.Application.Effects;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Conditions;
using ShelteredAPI.Scenarios.Domain.Effects;
using ShelteredAPI.Scenarios.Domain.Runtime;
namespace ShelteredAPI.Scenarios.Infrastructure.Runtime{
    internal sealed class ScheduledInventoryRuntimeService : IScenarioEffectHandler, IScenarioConditionEvaluator
    {
        public bool CanHandle(ScenarioEffectKind kind)
        {
            return kind == ScenarioEffectKind.AddInventory || kind == ScenarioEffectKind.RemoveInventory;
        }

        public bool Handle(ScenarioDefinition definition, ScenarioEffectDefinition effect, ScenarioRuntimeState state, out string message)
        {
            message = null;
            if (effect == null || InventoryManager.Instance == null)
            {
                message = "InventoryManager is not ready.";
                return false;
            }

            ItemManager.ItemType type;
            if (!InventoryHelper.ResolveItemType(effect.ItemId, out type) || effect.Quantity <= 0)
            {
                message = "Invalid inventory effect item or quantity.";
                return false;
            }

            if (effect.Kind == ScenarioEffectKind.RemoveInventory)
            {
                ItemTransferResult removal = ShelteredStores.Inventory.Remove(type.ToString(), effect.Quantity);
                message = removal.ErrorMessage;
                return removal.Success && removal.Moved == effect.Quantity;
            }
            return InventoryManager.Instance.AddNewItems(type, effect.Quantity);
        }

        public bool CanEvaluate(ScenarioConditionKind kind)
        {
            return kind == ScenarioConditionKind.ItemQuantityAvailable;
        }

        public bool IsSatisfied(ScenarioDefinition definition, ScenarioConditionRef condition, ScenarioRuntimeState state, out string reason)
        {
            reason = null;
            ItemManager.ItemType type;
            if (condition == null || !InventoryHelper.ResolveItemType(condition.TargetId, out type) || InventoryManager.Instance == null)
            {
                reason = "Inventory condition could not resolve item or manager.";
                return false;
            }

            int count = ShelteredStores.Inventory.GetCount(type.ToString());
            return count >= condition.Quantity;
        }
    }
}
