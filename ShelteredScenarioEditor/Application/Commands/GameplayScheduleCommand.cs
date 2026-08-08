using System;
using System.Globalization;

using ShelteredScenarioEditor.Application.Authoring;

namespace ShelteredScenarioEditor.Application.Commands
{
    internal enum GameplayScheduleCommandKind
    {
        AddFutureSurvivor, RemoveFutureSurvivor, ToggleFutureAsk, StepFutureDay, StepFutureHour,
        AddStartingItem, ApplySuppliesPreset, MergeStartingDuplicates, ToggleStartingOverride,
        RemoveStartingItem, StepStartingQuantity, CycleStartingItem, SetStartingItem,
        AddTimedItem, DeleteTimedItem, ToggleTimedKind, StepTimedQuantity, CycleTimedItem, SetTimedItem,
        StepTimedDay, StepTimedHour, StepTimedMinute,
        AddWeather, DeleteWeather, SetWeatherState, StepWeatherDuration, StepWeatherDay, StepWeatherHour, StepWeatherMinute,
        CaptureActiveQuests, AddQuest, AddCatalogQuest, DeleteQuest, MoveQuest, DuplicateQuest, CycleQuestId,
        SetQuestStartMode, ToggleQuestStartMode, CycleQuestTrigger, CycleQuestCompletion, SyncQuestTitle,
        SyncQuestDescription, SpawnQuestNow, StepQuestDay, StepQuestHour, StepQuestMinute,
        AddStartingItemAndPick, AddTimedItemAndPick, OpenStartingPicker, OpenTimedPicker, PreviewSuppliesPreset,
        OpenFutureSurvivor, OpenWeatherEditor, OpenQuestDocument
    }

    internal sealed class GameplayScheduleCommand : ScenarioAuthoringCommand
    {
        private GameplayScheduleCommand(GameplayScheduleCommandKind kind, int index, int delta, string value, bool remove)
            : base(BuildAutomationId(kind, index, delta, value, remove),
                kind == GameplayScheduleCommandKind.SpawnQuestNow
                    ? ScenarioAuthoringCommandPolicy.World
                    : (IsDestructive(kind) ? ScenarioAuthoringCommandPolicy.SafetySnapshot : ScenarioAuthoringCommandPolicy.Default))
        {
            Kind = kind;
            Index = index;
            Delta = delta;
            Value = value;
            Remove = remove;
        }

        internal GameplayScheduleCommandKind Kind { get; private set; }
        internal int Index { get; private set; }
        internal int Delta { get; private set; }
        internal string Value { get; private set; }
        internal bool Remove { get; private set; }

        internal static GameplayScheduleCommand New(GameplayScheduleCommandKind kind, int index = -1, int delta = 0, string value = null, bool remove = false)
        {
            return new GameplayScheduleCommand(kind, index, delta, value, remove);
        }

        internal bool ValidateStructure(out string reason)
        {
            reason = null;
            if (NeedsIndex(Kind) && Index < 0)
                reason = "Gameplay schedule index is invalid.";
            else if (NeedsDelta(Kind) && Delta == 0)
                reason = "Gameplay schedule step is invalid.";
            else if (NeedsValue(Kind) && Value == null)
                reason = "Gameplay schedule value is invalid.";
            return reason == null;
        }

        private static bool NeedsIndex(GameplayScheduleCommandKind kind)
        {
            switch (kind)
            {
                case GameplayScheduleCommandKind.AddFutureSurvivor:
                case GameplayScheduleCommandKind.AddStartingItem:
                case GameplayScheduleCommandKind.MergeStartingDuplicates:
                case GameplayScheduleCommandKind.ToggleStartingOverride:
                case GameplayScheduleCommandKind.AddTimedItem:
                case GameplayScheduleCommandKind.AddWeather:
                case GameplayScheduleCommandKind.CaptureActiveQuests:
                case GameplayScheduleCommandKind.AddQuest:
                case GameplayScheduleCommandKind.AddStartingItemAndPick:
                case GameplayScheduleCommandKind.AddTimedItemAndPick:
                    return false;
                default:
                    return true;
            }
        }

        private static bool NeedsDelta(GameplayScheduleCommandKind kind)
        {
            string name = kind.ToString();
            return name.StartsWith("Step", StringComparison.Ordinal) || name.StartsWith("Cycle", StringComparison.Ordinal) || kind == GameplayScheduleCommandKind.MoveQuest;
        }

        private static bool NeedsValue(GameplayScheduleCommandKind kind)
        {
            return kind == GameplayScheduleCommandKind.SetStartingItem
                || kind == GameplayScheduleCommandKind.SetTimedItem
                || kind == GameplayScheduleCommandKind.SetWeatherState
                || kind == GameplayScheduleCommandKind.SetQuestStartMode;
        }

        private static bool IsDestructive(GameplayScheduleCommandKind kind)
        {
            return kind == GameplayScheduleCommandKind.RemoveFutureSurvivor
                || kind == GameplayScheduleCommandKind.RemoveStartingItem
                || kind == GameplayScheduleCommandKind.DeleteTimedItem
                || kind == GameplayScheduleCommandKind.DeleteWeather
                || kind == GameplayScheduleCommandKind.DeleteQuest
                || kind == GameplayScheduleCommandKind.ApplySuppliesPreset;
        }

        private static string BuildAutomationId(GameplayScheduleCommandKind kind, int index, int delta, string value, bool remove)
        {
            string id = "scenario.gameplay_schedule.command." + kind.ToString().ToLowerInvariant();
            if (index >= 0) id += "." + index.ToString(CultureInfo.InvariantCulture);
            if (delta != 0) id += ".step." + delta.ToString(CultureInfo.InvariantCulture);
            if (value != null) id += ".value." + ScenarioAutomationIdCodec.EncodeToken(value);
            if (remove) id += ".remove";
            return id;
        }
    }

    internal static class GameplayScheduleCommands
    {
        internal static GameplayScheduleCommand AddFutureSurvivor() { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.AddFutureSurvivor); }
        internal static GameplayScheduleCommand RemoveFutureSurvivor(int index) { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.RemoveFutureSurvivor, index); }
        internal static GameplayScheduleCommand ToggleFutureAsk(int index) { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.ToggleFutureAsk, index); }
        internal static GameplayScheduleCommand StepFutureDay(int index, int delta) { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.StepFutureDay, index, delta); }
        internal static GameplayScheduleCommand StepFutureHour(int index, int delta) { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.StepFutureHour, index, delta); }
        internal static GameplayScheduleCommand AddStartingItem() { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.AddStartingItem); }
        internal static GameplayScheduleCommand ApplySuppliesPreset(int index) { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.ApplySuppliesPreset, index); }
        internal static GameplayScheduleCommand MergeStartingDuplicates() { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.MergeStartingDuplicates); }
        internal static GameplayScheduleCommand ToggleStartingOverride() { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.ToggleStartingOverride); }
        internal static GameplayScheduleCommand RemoveStartingItem(int index) { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.RemoveStartingItem, index); }
        internal static GameplayScheduleCommand StepStartingQuantity(int index, int delta) { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.StepStartingQuantity, index, delta); }
        internal static GameplayScheduleCommand CycleStartingItem(int index, int delta) { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.CycleStartingItem, index, delta); }
        internal static GameplayScheduleCommand SetStartingItem(int index, string value) { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.SetStartingItem, index, value: value); }
        internal static GameplayScheduleCommand AddTimedItem(bool remove) { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.AddTimedItem, remove: remove); }
        internal static GameplayScheduleCommand DeleteTimedItem(int index) { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.DeleteTimedItem, index); }
        internal static GameplayScheduleCommand ToggleTimedKind(int index) { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.ToggleTimedKind, index); }
        internal static GameplayScheduleCommand StepTimedQuantity(int index, int delta) { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.StepTimedQuantity, index, delta); }
        internal static GameplayScheduleCommand CycleTimedItem(int index, int delta) { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.CycleTimedItem, index, delta); }
        internal static GameplayScheduleCommand SetTimedItem(int index, string value) { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.SetTimedItem, index, value: value); }
        internal static GameplayScheduleCommand StepTimedDay(int index, int delta) { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.StepTimedDay, index, delta); }
        internal static GameplayScheduleCommand StepTimedHour(int index, int delta) { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.StepTimedHour, index, delta); }
        internal static GameplayScheduleCommand StepTimedMinute(int index, int delta) { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.StepTimedMinute, index, delta); }
        internal static GameplayScheduleCommand AddWeather() { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.AddWeather); }
        internal static GameplayScheduleCommand DeleteWeather(int index) { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.DeleteWeather, index); }
        internal static GameplayScheduleCommand SetWeatherState(int index, string value) { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.SetWeatherState, index, value: value); }
        internal static GameplayScheduleCommand StepWeatherDuration(int index, int delta) { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.StepWeatherDuration, index, delta); }
        internal static GameplayScheduleCommand StepWeatherDay(int index, int delta) { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.StepWeatherDay, index, delta); }
        internal static GameplayScheduleCommand StepWeatherHour(int index, int delta) { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.StepWeatherHour, index, delta); }
        internal static GameplayScheduleCommand StepWeatherMinute(int index, int delta) { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.StepWeatherMinute, index, delta); }
        internal static GameplayScheduleCommand CaptureActiveQuests() { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.CaptureActiveQuests); }
        internal static GameplayScheduleCommand AddQuest() { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.AddQuest); }
        internal static GameplayScheduleCommand AddCatalogQuest(int index) { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.AddCatalogQuest, index); }
        internal static GameplayScheduleCommand Quest(GameplayScheduleCommandKind kind, int index, int delta = 0, string value = null) { return GameplayScheduleCommand.New(kind, index, delta, value); }
        internal static GameplayScheduleCommand AddStartingItemAndPick() { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.AddStartingItemAndPick); }
        internal static GameplayScheduleCommand AddTimedItemAndPick(bool remove) { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.AddTimedItemAndPick, remove: remove); }
        internal static GameplayScheduleCommand OpenStartingPicker(int index) { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.OpenStartingPicker, index); }
        internal static GameplayScheduleCommand OpenTimedPicker(int index) { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.OpenTimedPicker, index); }
        internal static GameplayScheduleCommand PreviewSuppliesPreset(int index) { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.PreviewSuppliesPreset, index); }
        internal static GameplayScheduleCommand OpenFutureSurvivor(int index) { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.OpenFutureSurvivor, index); }
        internal static GameplayScheduleCommand OpenWeatherEditor(int index) { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.OpenWeatherEditor, index); }
        internal static GameplayScheduleCommand OpenQuestDocument(int index) { return GameplayScheduleCommand.New(GameplayScheduleCommandKind.OpenQuestDocument, index); }
    }
}
