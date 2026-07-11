using System;
using System.Collections.Generic;
using System.Globalization;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Conditions;
using ShelteredAPI.Scenarios.Shared;

namespace ShelteredAPI.Scenarios.Application.Commands{
    internal static class ScenarioWinLossAuthoringActionIds
    {
        public const string AddWin = "win_loss.win.add";
        public const string AddLoss = "win_loss.loss.add";
        public const string DeleteWinPrefix = "win_loss.win.delete.";
        public const string DeleteLossPrefix = "win_loss.loss.delete.";
        public const string TypeWinPrefix = "win_loss.win.type.";
        public const string TypeLossPrefix = "win_loss.loss.type.";
        public const string DayWinPrefix = "win_loss.win.day.";
        public const string DayLossPrefix = "win_loss.loss.day.";
        public const string HourWinPrefix = "win_loss.win.hour.";
        public const string HourLossPrefix = "win_loss.loss.hour.";
        public const string MinuteWinPrefix = "win_loss.win.minute.";
        public const string MinuteLossPrefix = "win_loss.loss.minute.";
        public const string QuantityWinPrefix = "win_loss.win.quantity.";
        public const string QuantityLossPrefix = "win_loss.loss.quantity.";
        public const string TargetWinPrefix = "win_loss.win.target.";
        public const string TargetLossPrefix = "win_loss.loss.target.";
    }

    internal sealed class ScenarioWinLossCommandHandler : IScenarioCommandHandler
    {
        private readonly IScenarioEditorService _editorService;

        public ScenarioWinLossCommandHandler(IScenarioEditorService editorService)
        {
            _editorService = editorService;
        }

        public bool TryHandle(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = IsWinLossAction(actionId);
            message = null;
            if (!handled)
                return false;

            ScenarioEditorSession session = _editorService != null ? _editorService.CurrentSession : null;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null)
            {
                message = "No active scenario definition.";
                return false;
            }

            if (definition.WinLossConditions == null)
                definition.WinLossConditions = new WinLossConditionsDefinition();

            bool changed = Apply(definition.WinLossConditions, actionId, out message);
            if (changed && session != null)
                session.MarkDraftChanged(ScenarioDirtySection.WinLoss, ScenarioEditCategory.WinLoss);
            return changed;
        }

        private static bool IsWinLossAction(string actionId)
        {
            return !string.IsNullOrEmpty(actionId) && actionId.StartsWith("win_loss.", StringComparison.Ordinal);
        }

        private static bool Apply(WinLossConditionsDefinition winLoss, string actionId, out string message)
        {
            message = null;
            if (string.Equals(actionId, ScenarioWinLossAuthoringActionIds.AddWin, StringComparison.Ordinal))
            {
                AddCondition(winLoss.WinConditions, "win");
                message = "Added victory condition.";
                return true;
            }

            if (string.Equals(actionId, ScenarioWinLossAuthoringActionIds.AddLoss, StringComparison.Ordinal))
            {
                AddCondition(winLoss.LossConditions, "loss");
                message = "Added failure condition.";
                return true;
            }

            if (TryApplyToCondition(winLoss.WinConditions, actionId, ScenarioWinLossAuthoringActionIds.DeleteWinPrefix, ScenarioWinLossAuthoringActionIds.TypeWinPrefix, ScenarioWinLossAuthoringActionIds.DayWinPrefix, ScenarioWinLossAuthoringActionIds.HourWinPrefix, ScenarioWinLossAuthoringActionIds.MinuteWinPrefix, ScenarioWinLossAuthoringActionIds.QuantityWinPrefix, ScenarioWinLossAuthoringActionIds.TargetWinPrefix, "victory", out message))
                return true;

            if (TryApplyToCondition(winLoss.LossConditions, actionId, ScenarioWinLossAuthoringActionIds.DeleteLossPrefix, ScenarioWinLossAuthoringActionIds.TypeLossPrefix, ScenarioWinLossAuthoringActionIds.DayLossPrefix, ScenarioWinLossAuthoringActionIds.HourLossPrefix, ScenarioWinLossAuthoringActionIds.MinuteLossPrefix, ScenarioWinLossAuthoringActionIds.QuantityLossPrefix, ScenarioWinLossAuthoringActionIds.TargetLossPrefix, "failure", out message))
                return true;

            message = "Win/loss action was not recognized.";
            return false;
        }

        private static void AddCondition(List<ConditionDef> conditions, string prefix)
        {
            ScenarioWinLossConditionDescriptor descriptor = ScenarioWinLossConditionSupport.GetDefaultDescriptor();
            ConditionDef condition = new ConditionDef();
            condition.Id = BuildUniqueId(conditions, prefix);
            condition.Type = descriptor.CanonicalType;
            ScenarioPropertyBag.Set(condition.Properties, "day", "7");
            ScenarioPropertyBag.Set(condition.Properties, "hour", "0");
            ScenarioPropertyBag.Set(condition.Properties, "minute", "0");
            conditions.Add(condition);
        }

        private static bool TryApplyToCondition(
            List<ConditionDef> conditions,
            string actionId,
            string deletePrefix,
            string typePrefix,
            string dayPrefix,
            string hourPrefix,
            string minutePrefix,
            string quantityPrefix,
            string targetPrefix,
            string label,
            out string message)
        {
            message = null;
            int index;
            if (TryParseIndex(actionId, deletePrefix, out index))
            {
                if (!IsValidIndex(conditions, index))
                {
                    message = "That " + label + " condition no longer exists.";
                    return false;
                }

                conditions.RemoveAt(index);
                message = "Deleted " + label + " condition.";
                return true;
            }

            if (TryParseIndex(actionId, typePrefix, out index))
            {
                ConditionDef condition = Get(conditions, index);
                if (condition == null)
                {
                    message = "That " + label + " condition no longer exists.";
                    return false;
                }

                ScenarioWinLossConditionDescriptor next = ScenarioWinLossConditionSupport.NextDescriptor(condition.Type);
                condition.Type = next.CanonicalType;
                EnsureDefaultProperties(condition, next);
                message = "Changed " + label + " condition type to " + next.Label + ".";
                return true;
            }

            int deltaDay;
            if (TryParseDelta(actionId, dayPrefix, out index, out deltaDay))
                return AdjustIntProperty(conditions, index, "day", deltaDay, 1, 999, label, out message);
            int deltaHour;
            if (TryParseDelta(actionId, hourPrefix, out index, out deltaHour))
                return AdjustIntProperty(conditions, index, "hour", deltaHour, 0, 23, label, out message);
            int deltaMinute;
            if (TryParseDelta(actionId, minutePrefix, out index, out deltaMinute))
                return AdjustIntProperty(conditions, index, "minute", deltaMinute, 0, 59, label, out message);
            int deltaQuantity;
            if (TryParseDelta(actionId, quantityPrefix, out index, out deltaQuantity))
                return AdjustIntProperty(conditions, index, "quantity", deltaQuantity, 1, 9999, label, out message);

            string targetId;
            if (TryParseToken(actionId, targetPrefix, out index, out targetId))
            {
                ConditionDef condition = Get(conditions, index);
                if (condition == null || string.IsNullOrEmpty(targetId))
                {
                    message = condition == null
                        ? "That " + label + " condition no longer exists."
                        : "Choose a valid authored quest.";
                    return false;
                }

                ScenarioPropertyBag.Set(condition.Properties, "questId", targetId);
                ScenarioPropertyBag.Set(condition.Properties, "targetId", targetId);
                message = "Updated " + label + " condition quest target to " + targetId + ".";
                return true;
            }

            return false;
        }

        private static void EnsureDefaultProperties(ConditionDef condition, ScenarioWinLossConditionDescriptor descriptor)
        {
            if (condition == null || descriptor == null)
                return;

            if (descriptor.FieldKind == ScenarioWinLossConditionFieldKind.Time)
            {
                if (ScenarioPropertyBag.GetInt(condition.Properties, "day", 0) <= 0)
                    ScenarioPropertyBag.Set(condition.Properties, "day", "7");
                if (ScenarioPropertyBag.GetInt(condition.Properties, "hour", -1) < 0)
                    ScenarioPropertyBag.Set(condition.Properties, "hour", "0");
                if (ScenarioPropertyBag.GetInt(condition.Properties, "minute", -1) < 0)
                    ScenarioPropertyBag.Set(condition.Properties, "minute", "0");
            }
            else if (descriptor.FieldKind == ScenarioWinLossConditionFieldKind.Quantity)
            {
                if (ScenarioPropertyBag.GetInt(condition.Properties, "quantity", 0) <= 0)
                    ScenarioPropertyBag.Set(condition.Properties, "quantity", "1");
            }
            else if (descriptor.FieldKind == ScenarioWinLossConditionFieldKind.Flag)
            {
                if (string.IsNullOrEmpty(ScenarioPropertyBag.GetString(condition.Properties, "flagValue")))
                    ScenarioPropertyBag.Set(condition.Properties, "flagValue", "true");
            }
        }

        private static bool AdjustIntProperty(
            List<ConditionDef> conditions,
            int index,
            string property,
            int delta,
            int min,
            int max,
            string label,
            out string message)
        {
            ConditionDef condition = Get(conditions, index);
            if (condition == null)
            {
                message = "That " + label + " condition no longer exists.";
                return false;
            }

            int current = ScenarioPropertyBag.GetInt(condition.Properties, property, min);
            int next = Math.Max(min, Math.Min(max, current + delta));
            ScenarioPropertyBag.Set(condition.Properties, property, next.ToString(CultureInfo.InvariantCulture));
            message = "Updated " + label + " condition " + property + ".";
            return true;
        }

        private static string BuildUniqueId(List<ConditionDef> conditions, string prefix)
        {
            for (int i = 1; i < 1000; i++)
            {
                string id = prefix + "_" + i.ToString(CultureInfo.InvariantCulture);
                bool found = false;
                for (int c = 0; conditions != null && c < conditions.Count; c++)
                {
                    if (conditions[c] != null && string.Equals(conditions[c].Id, id, StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                    return id;
            }

            return prefix + "_" + Environment.TickCount.ToString(CultureInfo.InvariantCulture);
        }

        private static bool TryParseIndex(string actionId, string prefix, out int index)
        {
            index = -1;
            if (string.IsNullOrEmpty(actionId) || string.IsNullOrEmpty(prefix) || !actionId.StartsWith(prefix, StringComparison.Ordinal))
                return false;

            return int.TryParse(actionId.Substring(prefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out index);
        }

        private static bool TryParseDelta(string actionId, string prefix, out int index, out int delta)
        {
            index = -1;
            delta = 0;
            if (string.IsNullOrEmpty(actionId) || string.IsNullOrEmpty(prefix) || !actionId.StartsWith(prefix, StringComparison.Ordinal))
                return false;

            string payload = actionId.Substring(prefix.Length);
            int separator = payload.IndexOf('.');
            if (separator <= 0)
                return false;

            return int.TryParse(payload.Substring(0, separator), NumberStyles.Integer, CultureInfo.InvariantCulture, out index)
                && int.TryParse(payload.Substring(separator + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out delta);
        }

        private static bool TryParseToken(string actionId, string prefix, out int index, out string value)
        {
            index = -1;
            value = null;
            if (string.IsNullOrEmpty(actionId) || string.IsNullOrEmpty(prefix) || !actionId.StartsWith(prefix, StringComparison.Ordinal))
                return false;

            string payload = actionId.Substring(prefix.Length);
            int separator = payload.IndexOf('.');
            if (separator <= 0 || !int.TryParse(payload.Substring(0, separator), NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
                return false;

            value = ScenarioAuthoringActionCodec.DecodeToken(payload.Substring(separator + 1));
            return value != null;
        }

        private static bool IsValidIndex(List<ConditionDef> conditions, int index)
        {
            return conditions != null && index >= 0 && index < conditions.Count;
        }

        private static ConditionDef Get(List<ConditionDef> conditions, int index)
        {
            return IsValidIndex(conditions, index) ? conditions[index] : null;
        }
    }
}
