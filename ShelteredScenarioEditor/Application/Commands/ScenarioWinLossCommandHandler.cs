using ShelteredScenarioEditor.Domain.Conditions;
using System;
using System.Collections.Generic;
using System.Globalization;
using ModAPI.Scenarios;
using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Conditions;
using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredAPI.Scenarios.Shared;

namespace ShelteredScenarioEditor.Application.Commands{
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

    internal enum WinLossCommandKind
    {
        Add,
        Delete,
        CycleType,
        AdjustDay,
        AdjustHour,
        AdjustMinute,
        AdjustQuantity,
        SetTarget
    }

    internal sealed class WinLossCommand : ScenarioAuthoringCommand
    {
        private WinLossCommand(WinLossCommandKind kind, bool isWinCondition, int index, int delta, string targetId, string automationId)
            : base(automationId, ScenarioAuthoringCommandPolicy.Default)
        {
            Kind = kind;
            IsWinCondition = isWinCondition;
            Index = index;
            Delta = delta;
            TargetId = targetId;
        }

        public WinLossCommandKind Kind { get; private set; }
        public bool IsWinCondition { get; private set; }
        public int Index { get; private set; }
        public int Delta { get; private set; }
        public string TargetId { get; private set; }

        public static WinLossCommand Add(bool win)
        {
            return new WinLossCommand(WinLossCommandKind.Add, win, -1, 0, null, win ? ScenarioWinLossAuthoringActionIds.AddWin : ScenarioWinLossAuthoringActionIds.AddLoss);
        }

        public static WinLossCommand Delete(bool win, int index)
        {
            return Indexed(WinLossCommandKind.Delete, win, index, win ? ScenarioWinLossAuthoringActionIds.DeleteWinPrefix : ScenarioWinLossAuthoringActionIds.DeleteLossPrefix);
        }

        public static WinLossCommand CycleType(bool win, int index)
        {
            return Indexed(WinLossCommandKind.CycleType, win, index, win ? ScenarioWinLossAuthoringActionIds.TypeWinPrefix : ScenarioWinLossAuthoringActionIds.TypeLossPrefix);
        }

        public static WinLossCommand Adjust(WinLossCommandKind kind, bool win, int index, int delta)
        {
            string prefix;
            switch (kind)
            {
                case WinLossCommandKind.AdjustDay: prefix = win ? ScenarioWinLossAuthoringActionIds.DayWinPrefix : ScenarioWinLossAuthoringActionIds.DayLossPrefix; break;
                case WinLossCommandKind.AdjustHour: prefix = win ? ScenarioWinLossAuthoringActionIds.HourWinPrefix : ScenarioWinLossAuthoringActionIds.HourLossPrefix; break;
                case WinLossCommandKind.AdjustMinute: prefix = win ? ScenarioWinLossAuthoringActionIds.MinuteWinPrefix : ScenarioWinLossAuthoringActionIds.MinuteLossPrefix; break;
                case WinLossCommandKind.AdjustQuantity: prefix = win ? ScenarioWinLossAuthoringActionIds.QuantityWinPrefix : ScenarioWinLossAuthoringActionIds.QuantityLossPrefix; break;
                default: throw new ArgumentOutOfRangeException("kind");
            }

            string automationId = prefix + index.ToString(CultureInfo.InvariantCulture) + "." + delta.ToString(CultureInfo.InvariantCulture);
            return new WinLossCommand(kind, win, index, delta, null, automationId);
        }

        public static WinLossCommand SetTarget(bool win, int index, string targetId)
        {
            string prefix = win ? ScenarioWinLossAuthoringActionIds.TargetWinPrefix : ScenarioWinLossAuthoringActionIds.TargetLossPrefix;
            string automationId = prefix + index.ToString(CultureInfo.InvariantCulture) + "." + ScenarioAutomationIdCodec.EncodeToken(targetId);
            return new WinLossCommand(WinLossCommandKind.SetTarget, win, index, 0, targetId, automationId);
        }

        private static WinLossCommand Indexed(WinLossCommandKind kind, bool win, int index, string prefix)
        {
            return new WinLossCommand(kind, win, index, 0, null, prefix + index.ToString(CultureInfo.InvariantCulture));
        }
    }

    internal sealed class ScenarioWinLossCommandHandler : IScenarioCommandHandler
    {
        private readonly IScenarioEditorService _editorService;

        public ScenarioWinLossCommandHandler(IScenarioEditorService editorService)
        {
            _editorService = editorService;
        }

        public bool CanHandle(ScenarioAuthoringCommand command)
        {
            return command is WinLossCommand;
        }

        public ScenarioCommandDispatchResult Handle(ScenarioAuthoringState state, ScenarioAuthoringCommand command)
        {
            string message = null;
            WinLossCommand winLossCommand = command as WinLossCommand;
            ScenarioEditorSession session = _editorService != null ? _editorService.CurrentSession : null;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null)
            {
                message = "No active scenario definition.";
                return Result(false, message);
            }

            if (definition.WinLossConditions == null)
                definition.WinLossConditions = new WinLossConditionsDefinition();

            bool changed = Apply(definition.WinLossConditions, winLossCommand, out message);
            if (changed && session != null)
                session.MarkDraftChanged(ScenarioDirtySection.WinLoss, ScenarioEditCategory.WinLoss);
            return Result(changed, message);
        }

        private static ScenarioCommandDispatchResult Result(bool changed, string message)
        {
            return new ScenarioCommandDispatchResult { Handled = true, Changed = changed, Message = message };
        }

        private static bool Apply(WinLossConditionsDefinition winLoss, WinLossCommand command, out string message)
        {
            message = null;
            if (command == null)
                return false;

            if (command.Kind == WinLossCommandKind.Add)
            {
                AddCondition(command.IsWinCondition ? winLoss.WinConditions : winLoss.LossConditions, command.IsWinCondition ? "win" : "loss");
                message = command.IsWinCondition ? "Added victory condition." : "Added failure condition.";
                return true;
            }

            List<ScenarioConditionRef> conditions = command.IsWinCondition ? winLoss.WinConditions : winLoss.LossConditions;
            string label = command.IsWinCondition ? "victory" : "failure";
            if (!IsValidIndex(conditions, command.Index))
            {
                message = "That " + label + " condition no longer exists.";
                return false;
            }

            if (command.Kind == WinLossCommandKind.Delete)
            {
                conditions.RemoveAt(command.Index);
                message = "Deleted " + label + " condition.";
                return true;
            }

            ScenarioConditionRef condition = conditions[command.Index];
            if (command.Kind == WinLossCommandKind.CycleType)
            {
                ScenarioWinLossConditionDescriptor next = ScenarioWinLossConditionSupport.NextDescriptor(condition.Kind);
                condition.Kind = next.Kind;
                EnsureDefaultProperties(condition, next);
                message = "Changed " + label + " condition type to " + next.Label + ".";
                return true;
            }

            if (command.Kind == WinLossCommandKind.SetTarget)
            {
                if (string.IsNullOrEmpty(command.TargetId))
                {
                    message = "Choose a valid authored quest.";
                    return false;
                }
                condition.TargetId = command.TargetId;
                message = "Updated " + label + " condition quest target to " + command.TargetId + ".";
                return true;
            }

            string property = command.Kind == WinLossCommandKind.AdjustDay ? "day"
                : command.Kind == WinLossCommandKind.AdjustHour ? "hour"
                : command.Kind == WinLossCommandKind.AdjustMinute ? "minute"
                : "quantity";
            int min = property == "day" || property == "quantity" ? 1 : 0;
            int max = property == "day" ? 999 : property == "hour" ? 23 : property == "minute" ? 59 : 9999;
            return AdjustNumericField(conditions, command.Index, property, command.Delta, min, max, label, out message);
        }

        private static void AddCondition(List<ScenarioConditionRef> conditions, string prefix)
        {
            ScenarioWinLossConditionDescriptor descriptor = ScenarioWinLossConditionSupport.GetDefaultDescriptor();
            ScenarioConditionRef condition = new ScenarioConditionRef();
            condition.Id = BuildUniqueId(conditions, prefix);
            condition.Kind = descriptor.Kind;
            condition.Time = new ScenarioScheduleTime { Day = 7, Hour = 0, Minute = 0 };
            conditions.Add(condition);
        }

        private static void EnsureDefaultProperties(ScenarioConditionRef condition, ScenarioWinLossConditionDescriptor descriptor)
        {
            if (condition == null || descriptor == null)
                return;

            if (descriptor.FieldKind == ScenarioWinLossConditionFieldKind.Time)
            {
                if (condition.Time == null)
                    condition.Time = new ScenarioScheduleTime();
                if (condition.Kind == ScenarioConditionKind.SurviveDays)
                    condition.Quantity = condition.Quantity > 0 ? condition.Quantity : 7;
                else if (condition.Time.Day <= 0)
                    condition.Time.Day = 7;
            }
            else if (descriptor.FieldKind == ScenarioWinLossConditionFieldKind.Quantity)
            {
                if (condition.Quantity <= 0)
                    condition.Quantity = 1;
            }
            else if (descriptor.FieldKind == ScenarioWinLossConditionFieldKind.Flag)
            {
                if (string.IsNullOrEmpty(condition.FlagValue))
                    condition.FlagValue = "true";
            }
        }

        private static bool AdjustNumericField(
            List<ScenarioConditionRef> conditions,
            int index,
            string property,
            int delta,
            int min,
            int max,
            string label,
            out string message)
        {
            ScenarioConditionRef condition = IsValidIndex(conditions, index) ? conditions[index] : null;
            if (condition == null)
            {
                message = "That " + label + " condition no longer exists.";
                return false;
            }

            if (condition.Time == null)
                condition.Time = new ScenarioScheduleTime();

            int current;
            if (property == "quantity" || (property == "day" && condition.Kind == ScenarioConditionKind.SurviveDays))
                current = condition.Quantity;
            else if (property == "day")
                current = condition.Time.Day;
            else if (property == "hour")
                current = condition.Time.Hour;
            else
                current = condition.Time.Minute;
            int next = Math.Max(min, Math.Min(max, current + delta));
            if (property == "quantity" || (property == "day" && condition.Kind == ScenarioConditionKind.SurviveDays))
                condition.Quantity = next;
            else if (property == "day")
                condition.Time.Day = next;
            else if (property == "hour")
                condition.Time.Hour = next;
            else
                condition.Time.Minute = next;
            message = "Updated " + label + " condition " + property + ".";
            return true;
        }

        private static string BuildUniqueId(List<ScenarioConditionRef> conditions, string prefix)
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

        private static bool IsValidIndex(List<ScenarioConditionRef> conditions, int index)
        {
            return conditions != null && index >= 0 && index < conditions.Count;
        }

    }
}
