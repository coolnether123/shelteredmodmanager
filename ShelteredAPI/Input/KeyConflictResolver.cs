using System;
using System.Collections.Generic;
using ModAPI.InputActions;
using ModAPI.Core;
using UnityEngine;

namespace ShelteredAPI.Input
{
    public enum KeyConflictUserChoice
    {
        Cancel = 0,
        Override = 1,
        Swap = 2
    }

    public enum KeyBindingSlot
    {
        Primary = 0,
        Secondary = 1
    }

    public sealed class KeyConflictEntry
    {
        public string ActionId;
        public string ActionLabel;
        public InputContext Context;
        public KeyBindingSlot Slot;
        public KeyCode Key;
    }

    public sealed class KeyConflictDetection
    {
        public bool Conflicted;
        public List<KeyConflictEntry> ActionList = new List<KeyConflictEntry>();
        public string Recommendation;
        public KeyConflictUserChoice RecommendedChoice = KeyConflictUserChoice.Cancel;
    }

    public sealed class KeyConflictResolution
    {
        public bool Applied;
        public bool Cancelled;
        public string Message;
        public List<string> AffectedActionIds = new List<string>();
    }

    /// <summary>
    /// Centralized conflict detection and deterministic resolution strategies.
    /// </summary>
    public static class KeyConflictResolver
    {
        public static KeyConflictDetection DetectConflicts(
            KeyCode proposedKey,
            string targetActionId,
            InputContext targetContext)
        {
            var result = new KeyConflictDetection();
            if (proposedKey == KeyCode.None || string.IsNullOrEmpty(targetActionId)) return result;
            if (!KeyValidationPolicy.IsValidForContext(proposedKey, targetContext)) return result;

            List<ModInputAction> actions = InputActionRegistry.GetAllActions();
            for (int i = 0; i < actions.Count; i++)
            {
                ModInputAction action = actions[i];
                if (action == null || string.IsNullOrEmpty(action.Id)) continue;
                if (string.Equals(action.Id, targetActionId, StringComparison.OrdinalIgnoreCase)) continue;

                InputBinding binding;
                if (!InputActionRegistry.TryGetBinding(action.Id, out binding)) continue;
                if (!binding.ContainsKey(proposedKey)) continue;
                if (ShouldAllowSharedDefault(targetActionId, action.Id, proposedKey)) continue;

                InputContext otherContext = ShelteredVanillaInputActions.GetContextForActionId(action.Id);
                if (otherContext == InputContext.Unknown)
                    otherContext = InferContextFromAction(action.Id);

                if (binding.Primary == proposedKey)
                {
                    result.ActionList.Add(new KeyConflictEntry
                    {
                        ActionId = action.Id,
                        ActionLabel = action.Label,
                        Context = otherContext,
                        Slot = KeyBindingSlot.Primary,
                        Key = proposedKey
                    });
                }

                if (binding.Secondary == proposedKey)
                {
                    result.ActionList.Add(new KeyConflictEntry
                    {
                        ActionId = action.Id,
                        ActionLabel = action.Label,
                        Context = otherContext,
                        Slot = KeyBindingSlot.Secondary,
                        Key = proposedKey
                    });
                }
            }

            if (result.ActionList.Count > 0)
            {
                result.Conflicted = true;
                bool hasCrossContext = result.ActionList.Exists(c => c.Context != targetContext && c.Context != InputContext.Unknown);
                if (hasCrossContext)
                {
                    result.RecommendedChoice = KeyConflictUserChoice.Cancel;
                    result.Recommendation = "Cross-context conflict detected. Recommended: cancel or explicit override.";
                }
                else
                {
                    result.RecommendedChoice = KeyConflictUserChoice.Override;
                    result.Recommendation = "Same-context conflict detected. Recommended: override.";
                }

                MMLog.WriteInfo("[KeyConflictResolver] Conflict detected for action=" + targetActionId
                    + " key=" + proposedKey + " context=" + targetContext
                    + " count=" + result.ActionList.Count + " recommendation=" + result.RecommendedChoice + ".");
            }

            return result;
        }

        public static KeyConflictResolution ResolveConflict(
            string targetActionId,
            KeyBindingSlot targetSlot,
            KeyCode targetOldKey,
            KeyCode proposedKey,
            KeyConflictDetection detected,
            KeyConflictUserChoice userChoice)
        {
            var resolution = new KeyConflictResolution();
            if (detected == null || !detected.Conflicted)
            {
                resolution.Applied = true;
                resolution.Message = "No conflicts.";
                return resolution;
            }

            if (userChoice == KeyConflictUserChoice.Cancel)
            {
                resolution.Cancelled = true;
                resolution.Message = "Conflict resolution cancelled by user.";
                MMLog.WriteInfo("[KeyConflictResolver] Resolution cancelled by user for action=" + targetActionId + ".");
                return resolution;
            }

            if (userChoice == KeyConflictUserChoice.Override)
            {
                for (int i = 0; i < detected.ActionList.Count; i++)
                {
                    KeyConflictEntry conflict = detected.ActionList[i];
                    InputBinding binding;
                    if (!InputActionRegistry.TryGetBinding(conflict.ActionId, out binding)) continue;

                    if (conflict.Slot == KeyBindingSlot.Primary && binding.Primary == proposedKey)
                        binding.Primary = KeyCode.None;
                    if (conflict.Slot == KeyBindingSlot.Secondary && binding.Secondary == proposedKey)
                        binding.Secondary = KeyCode.None;

                    InputActionRegistry.SetBinding(conflict.ActionId, binding);
                    AddAffected(resolution.AffectedActionIds, conflict.ActionId);
                }

                resolution.Applied = true;
                resolution.Message = "Conflicts resolved by override.";
                MMLog.WriteInfo("[KeyConflictResolver] Override applied for action=" + targetActionId
                    + ". Affected=" + resolution.AffectedActionIds.Count + ".");
                return resolution;
            }

            if (userChoice == KeyConflictUserChoice.Swap)
            {
                if (detected.ActionList.Count != 1)
                {
                    resolution.Cancelled = true;
                    resolution.Message = "Swap requires exactly one conflicting slot.";
                    return resolution;
                }

                KeyConflictEntry single = detected.ActionList[0];
                InputBinding otherBinding;
                if (!InputActionRegistry.TryGetBinding(single.ActionId, out otherBinding))
                {
                    resolution.Cancelled = true;
                    resolution.Message = "Could not load conflicting binding for swap.";
                    return resolution;
                }

                if (single.Slot == KeyBindingSlot.Primary)
                    otherBinding.Primary = targetOldKey;
                else
                    otherBinding.Secondary = targetOldKey;

                InputActionRegistry.SetBinding(single.ActionId, otherBinding);
                AddAffected(resolution.AffectedActionIds, single.ActionId);

                resolution.Applied = true;
                resolution.Message = "Conflicts resolved by swap.";
                MMLog.WriteInfo("[KeyConflictResolver] Swap applied for action=" + targetActionId
                    + " with action=" + single.ActionId + ".");
                return resolution;
            }

            resolution.Cancelled = true;
            resolution.Message = "Unsupported conflict choice.";
            return resolution;
        }

        private static InputContext InferContextFromAction(string actionId)
        {
            if (string.IsNullOrEmpty(actionId)) return InputContext.Unknown;
            if (actionId.StartsWith("sheltered.vanilla.menu.", StringComparison.OrdinalIgnoreCase)) return InputContext.Menu;
            if (actionId.StartsWith("sheltered.vanilla.input.", StringComparison.OrdinalIgnoreCase)) return InputContext.Gameplay;
            if (actionId.StartsWith("sheltered.", StringComparison.OrdinalIgnoreCase)) return InputContext.Gameplay;
            return InputContext.Unknown;
        }

        private static void AddAffected(List<string> affectedIds, string actionId)
        {
            if (affectedIds == null || string.IsNullOrEmpty(actionId)) return;
            if (!affectedIds.Contains(actionId))
                affectedIds.Add(actionId);
        }

        private static bool ShouldAllowSharedDefault(string targetActionId, string otherActionId, KeyCode key)
        {
            if (key == KeyCode.None || string.IsNullOrEmpty(targetActionId) || string.IsNullOrEmpty(otherActionId))
                return false;

            ModInputAction targetAction;
            if (!InputActionRegistry.TryGetAction(targetActionId, out targetAction) || targetAction == null)
                return false;

            ModInputAction otherAction;
            if (!InputActionRegistry.TryGetAction(otherActionId, out otherAction) || otherAction == null)
                return false;

            return targetAction.DefaultBinding.ContainsKey(key) && otherAction.DefaultBinding.ContainsKey(key);
        }
    }
}
