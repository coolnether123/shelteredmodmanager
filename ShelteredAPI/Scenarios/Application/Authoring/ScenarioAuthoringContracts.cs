using System;
using System.Collections.Generic;
using ModAPI.Scenarios;
using UnityEngine;

using ShelteredAPI.Content;
using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Application.Assets;
using ShelteredAPI.Scenarios.Application.Map;
using ShelteredAPI.Scenarios.Domain.Stages;
using ShelteredAPI.Scenarios.Domain.Story;
namespace ShelteredAPI.Scenarios.Application.Authoring{
    internal interface IScenarioAuthoringBackend
    {
        event Action<ScenarioAuthoringState> StateChanged;

        ScenarioAuthoringState CurrentState { get; }
        ScenarioAuthoringShellViewModel GetShellViewModel();
        ScenarioAuthoringInspectorDocument GetShellDocument();
        ScenarioAuthoringInspectorDocument GetInspectorDocument();
        ScenarioAuthoringInspectorDocument GetHoverDocument();
        bool ExecuteAction(string actionId);
        ScenarioAuthoringActionExecutionResult ExecuteActionWithResult(string actionId);
        void Refresh();
    }

    internal static class ScenarioAuthoringActionIds
    {
        public const string ToggleShell = "sheltered.scenario_authoring.toggle_shell";
        public const string SelectionModifier = "sheltered.scenario_authoring.selection_modifier";
        public const string ConfirmSelection = "sheltered.scenario_authoring.confirm_selection";
        public const string ClearSelection = "sheltered.scenario_authoring.clear_selection";
        public const string SaveDraft = "sheltered.scenario_authoring.save_draft";
        public const string TogglePlaytest = "sheltered.scenario_authoring.toggle_playtest";
        public const string UndoKey = "sheltered.scenario_authoring.undo";
        public const string RedoKey = "sheltered.scenario_authoring.redo";
        public const string CopyKey = "sheltered.scenario_authoring.copy";
        public const string PasteKey = "sheltered.scenario_authoring.paste";
        public const string RevertKey = "sheltered.scenario_authoring.revert";

        public const string ActionShellToggle = "shell.toggle";
        public const string ActionShellShow = "shell.show";
        public const string ActionShellHideAll = "shell.hide_all";
        public const string ActionShellResetLayout = "shell.layout.reset";
        public const string ActionShellMinimalMode = "shell.layout.minimal_mode";
        public const string ActionShellFocusSelection = "shell.layout.focus_selection";
        public const string ActionShellOpenSettings = "shell.settings.open";
        public const string ActionShellOpenTimeline = "shell.timeline.open";
        // Legacy alias retained for older mods; routes to the Timeline window.
        public const string ActionShellOpenCalendar = "shell.calendar.open";
        public const string ActionShellCloseSettings = "shell.settings.close";
        public const string ActionShellOpenHelp = "shell.help.open";
        public const string ActionShellCloseHelp = "shell.help.close";
        public const string ActionShellToggleGlobalSearch = "shell.search.toggle";
        public const string ActionShellCloseGlobalSearch = "shell.search.close";
        public const string ActionShellOpenShortcuts = "shell.shortcuts.open";
        public const string ActionShellHelpShowPages = "shell.help.view.pages";
        public const string ActionShellSettingsReset = "shell.settings.reset";
        public const string ActionTutorialNext = "tutorial.next";
        public const string ActionTutorialBack = "tutorial.back";
        public const string ActionTutorialSkip = "tutorial.skip";
        public const string ActionTutorialSkipPrompt = "tutorial.skip.prompt";
        public const string ActionTutorialSkipCancel = "tutorial.skip.cancel";
        public const string ActionTutorialReset = "tutorial.reset";
        public const string ActionTutorialOpenTarget = "tutorial.open_target";
        public const string ActionHelpPagePrevious = "tutorial.help_page.previous";
        public const string ActionHelpPageNext = "tutorial.help_page.next";
        public const string ActionHelpOpenTopicPrefix = "help.open.";
        public const string ActionTourStartPrefix = "tour.start.";
        public const string ActionTourNext = "tour.next";
        public const string ActionTourBack = "tour.back";
        public const string ActionTourExit = "tour.exit";
        public const string ActionSetupDismiss = "setup.dismiss";
        public const string ActionShellToggleWindowMenu = "shell.menu.windows";
        public const string ActionWindowTogglePrefix = "shell.window.toggle.";
        public const string ActionWindowCollapsePrefix = "shell.window.collapse.";
        public const string ActionWindowRestorePrefix = "shell.window.restore.";
        public const string ActionSettingTogglePrefix = "shell.setting.toggle.";
        public const string ActionSettingIncreasePrefix = "shell.setting.increase.";
        public const string ActionSettingDecreasePrefix = "shell.setting.decrease.";
        public const string ActionSettingSelectPrefix = "shell.setting.select.";
        public const string ActionInspectorPinTogglePrefix = "shell.inspector.pin.toggle.";
        public const string ActionRendererMapFilterTogglePrefix = "shell.renderer.map_filter.toggle.";
        public const string ActionRendererPixelGroupTogglePrefix = "shell.renderer.pixel_group.toggle.";
        public const string ActionRendererHomeGroupTogglePrefix = "shell.renderer.home_group.toggle.";
        public const string ActionRendererAssetFavoriteTogglePrefix = "shell.renderer.asset_favorite.toggle.";
        public const string ActionRendererAssetCategorySelectPrefix = "shell.renderer.asset_category.select.";
        public const string ActionRendererAssetSearchPrefix = "shell.renderer.asset_search.";
        public const string ActionRendererAssetSearchClear = "shell.renderer.asset_search.clear";
        public const string ActionRendererAssetInventoryFilterPrefix = "shell.renderer.asset_inventory_filter.select.";
        public const string ActionRendererCandidateSearchPrefix = "shell.renderer.candidate_search.set.";
        public const string ActionRendererCandidateFilterPrefix = "shell.renderer.candidate_filter.select.";
        public const string ActionRendererGlobalSearchQueryPrefix = "shell.renderer.global_search.query.";
        public const string ActionRendererTopBarMoreToggle = "shell.renderer.top_bar_more.toggle";
        public const string ActionRendererPlacementBack = "shell.renderer.placement.back";
        public const string ActionRendererPlacementDone = "shell.renderer.placement.done";
        public const string ActionInspectorTabPrefix = "inspector.tab.";
        public const string ActionStageSelectPrefix = "stage.select.";
        public const string ActionShellTabShelter = "shell.tab.shelter";
        public const string ActionShellTabBuild = "shell.tab.build";
        public const string ActionShellTabSurvivors = "shell.tab.survivors";
        public const string ActionShellTabStockpile = "shell.tab.stockpile";
        public const string ActionShellTabTriggers = "shell.tab.triggers";
        public const string ActionShellTabJobs = "shell.tab.jobs";
        public const string ActionShellTabQuests = "shell.tab.quests";
        public const string ActionShellTabArt = "shell.tab.art";
        public const string ActionShellTabMap = "shell.tab.map";
        public const string ActionShellTabTest = "shell.tab.test";
        public const string ActionShellTabPublish = "shell.tab.publish";
        public const string ActionShellTabShell = "shell.tab.shell";
        public const string ActionSave = "editor.save";
        public const string ActionHistoryShow = "editor.history.show";
        public const string ActionHistorySaveVersion = "editor.history.save_version";
        public const string ActionHistoryClose = "editor.history.close";
        public const string ActionHistoryConfirmRestore = "editor.history.confirm_restore";
        public const string ActionHistoryConfirmDelete = "editor.history.confirm_delete";
        public const string ActionHistoryCancelRestore = "editor.history.cancel_restore";
        public const string ActionHistoryCancelDelete = "editor.history.cancel_delete";
        public const string ActionHistoryRestorePrefix = "editor.history.restore.";
        public const string ActionHistoryDeletePrefix = "editor.history.delete.";
        public const string ActionDraftCopyPath = "editor.draft.copy_path";
        public const string ActionDraftTitlePrefix = "editor.draft.title.";
        public const string ActionDraftDescriptionPrefix = "editor.draft.description.";
        public const string ActionDraftGoalPrefix = "editor.draft.goal.";
        public const string ActionDraftAuthorPrefix = "editor.draft.author.";
        public const string ActionDraftVersionPrefix = "editor.draft.version.";
        public const string ActionDraftCreditsPrefix = "editor.draft.credits.";
        public const string ActionDraftTagsPrefix = "editor.draft.tags.";
        public const string ActionDraftIdPrefix = "editor.draft.id.";
        public const string ActionDraftVersionBumpPatch = "editor.draft.version.bump_patch";
        public const string ActionDraftVersionBumpMinor = "editor.draft.version.bump_minor";
        public const string ActionPlaytest = "editor.playtest.toggle";
        public const string ActionPlaytestRestart = "editor.playtest.restart";
        public const string ActionTestConsoleHour = "editor.test_console.time.hour";
        public const string ActionTestConsoleDay = "editor.test_console.time.day";
        public const string ActionTestConsoleNextEvent = "editor.test_console.time.next_event";
        public const string ActionTestConsoleFirePrefix = "editor.test_console.fire.";
        public const string ActionTestConsoleStoryStagePrefix = "editor.test_console.story_stage.";
        public const string ActionScenarioSeedRandom = "editor.seed.random";
        public const string ActionScenarioSeedFixed = "editor.seed.fixed";
        public const string ActionScenarioSeedReroll = "editor.seed.reroll";
        public const string ActionScenarioSeedValuePrefix = "editor.seed.value.";
        public const string ActionPublishExport = "publish.export";
        public const string ActionOpenPauseMenu = "editor.pause_menu.open";
        public const string ActionCloseEditor = "editor.close";
        public const string ActionConvertToNormal = "editor.convert_to_normal";
        public const string ActionSelectionClear = "selection.clear";
        public const string ActionSelectionStackCycle = "selection.stack.cycle";
        public const string ActionSelectionStackToggleExpanded = "selection.stack.toggle_expanded";
        public const string ActionSelectionStackSelectPrefix = "selection.stack.select.";
        public const string ActionHierarchySelectPrefix = "hierarchy.select.";
        public const string ActionCaptureFamily = "capture.family.current";
        public const string ActionCaptureShelterObjects = "capture.shelter.objects";
        public const string ActionCaptureSelectedObject = "capture.shelter.selected_object";
        public const string ActionRemoveSelectedObjectPlacement = "capture.shelter.remove_selected_object";
        public const string ActionStationLevelPrefix = "station.level.";
        public const string ActionStationUpgradePrefix = "station.upgrade.";
        public const string ActionStationStatPrefix = "station.stat.";
        public const string ActionStationStatClearPrefix = "station.stat_clear.";
        public const string ActionLiveSurvivorAddToStartingPrefix = "scenario.live_survivor.add_to_start.";
        public const string ActionStartingSurvivorAdd = "scenario.start_survivor.add";
        public const string ActionStartingSurvivorPrefix = "scenario.start_survivor.";
        public const string ActionFutureSurvivorAdd = "scenario.future_survivor.add";
        public const string ActionFutureSurvivorEditPrefix = "scenario.future_survivor.edit.";
        public const string ActionFutureSurvivorRemovePrefix = "scenario.future_survivor.remove.";
        public const string ActionFutureSurvivorToggleAskPrefix = "scenario.future_survivor.ask.";
        public const string ActionFutureSurvivorDayPrefix = "scenario.future_survivor.day.";
        public const string ActionFutureSurvivorHourPrefix = "scenario.future_survivor.hour.";
        public const string ActionInventoryScheduleAdd = "scenario.inventory.schedule.add";
        public const string ActionInventoryScheduleRemove = "scenario.inventory.schedule.remove";
        public const string ActionInventoryScheduleDeletePrefix = "scenario.inventory.schedule.delete.";
        public const string ActionInventoryScheduleDayPrefix = "scenario.inventory.schedule.day.";
        public const string ActionInventoryScheduleHourPrefix = "scenario.inventory.schedule.hour.";
        public const string ActionInventoryScheduleMinutePrefix = "scenario.inventory.schedule.minute.";
        public const string ActionInventoryScheduleQuantityPrefix = "scenario.inventory.schedule.quantity.";
        public const string ActionInventoryScheduleItemPrefix = "scenario.inventory.schedule.item.";
        public const string ActionInventoryScheduleItemSelectPrefix = "scenario.inventory.schedule.item_select.";
        public const string ActionInventoryScheduleKindPrefix = "scenario.inventory.schedule.kind.";
        public const string ActionInventoryStartingAdd = "scenario.inventory.start.add";
        public const string ActionInventoryStartingRemovePrefix = "scenario.inventory.start.remove.";
        public const string ActionInventoryStartingQuantityPrefix = "scenario.inventory.start.quantity.";
        public const string ActionInventoryStartingItemPrefix = "scenario.inventory.start.item.";
        public const string ActionInventoryStartingItemSelectPrefix = "scenario.inventory.start.item_select.";
        public const string ActionInventoryStartingOverrideToggle = "scenario.inventory.start.override_random";
        public const string ActionWeatherScheduleAdd = "scenario.weather.schedule.add";
        public const string ActionWeatherScheduleDeletePrefix = "scenario.weather.schedule.delete.";
        public const string ActionWeatherScheduleDayPrefix = "scenario.weather.schedule.day.";
        public const string ActionWeatherScheduleHourPrefix = "scenario.weather.schedule.hour.";
        public const string ActionWeatherScheduleMinutePrefix = "scenario.weather.schedule.minute.";
        public const string ActionWeatherScheduleStatePrefix = "scenario.weather.schedule.state.";
        public const string ActionWeatherScheduleDurationPrefix = "scenario.weather.schedule.duration.";
        public const string ActionStoryStageAdd = "scenario.story.stage.add";
        public const string ActionStoryStageDeletePrefix = "scenario.story.stage.delete.";
        public const string ActionStoryStageDuplicatePrefix = "scenario.story.stage.duplicate.";
        public const string ActionStoryStageMovePrefix = "scenario.story.stage.move.";
        public const string ActionStoryStageIdPrefix = "scenario.story.stage.id.";
        public const string ActionStoryStageCharacterTogglePrefix = "scenario.story.stage.character.";
        public const string ActionStoryStageUnansweredPrefix = "scenario.story.stage.unanswered.";
        public const string ActionStoryStageUnansweredDelayPrefix = "scenario.story.stage.unanswered_delay.";
        public const string ActionStoryStagePunishPrefix = "scenario.story.stage.punish.";
        public const string ActionStoryIntercomAddPrefix = "scenario.story.intercom.add.";
        public const string ActionStoryIntercomDeletePrefix = "scenario.story.intercom.delete.";
        public const string ActionStoryIntercomDuplicatePrefix = "scenario.story.intercom.duplicate.";
        public const string ActionStoryIntercomMovePrefix = "scenario.story.intercom.move.";
        public const string ActionStoryIntercomIdPrefix = "scenario.story.intercom.id.";
        public const string ActionStoryIntercomTypePrefix = "scenario.story.intercom.type.";
        public const string ActionStoryIntercomNextPrefix = "scenario.story.intercom.next.";
        public const string ActionStoryIntercomAlternatePrefix = "scenario.story.intercom.alternate.";
        public const string ActionStoryIntercomRandomAddPrefix = "scenario.story.intercom.random.add.";
        public const string ActionStoryIntercomRandomDeletePrefix = "scenario.story.intercom.random.delete.";
        public const string ActionStoryIntercomRandomTargetPrefix = "scenario.story.intercom.random.target.";
        public const string ActionStoryDialogueAddPrefix = "scenario.story.dialogue.add.";
        public const string ActionStoryDialogueDeletePrefix = "scenario.story.dialogue.delete.";
        public const string ActionStoryDialogueSpeakerPrefix = "scenario.story.dialogue.speaker.";
        public const string ActionStoryDialogueKeyPrefix = "scenario.story.dialogue.key.";
        public const string ActionStoryOptionAddPrefix = "scenario.story.option.add.";
        public const string ActionStoryOptionDeletePrefix = "scenario.story.option.delete.";
        public const string ActionStoryOptionKeyPrefix = "scenario.story.option.key.";
        public const string ActionStoryOptionNextPrefix = "scenario.story.option.next.";
        public const string ActionStoryRewardAddPrefix = "scenario.story.reward.add.";
        public const string ActionStoryRewardDeletePrefix = "scenario.story.reward.delete.";
        public const string ActionStoryRewardItemPrefix = "scenario.story.reward.item.";
        public const string ActionStoryRewardQuantityPrefix = "scenario.story.reward.quantity.";
        public const string ActionStoryRemovalAddPrefix = "scenario.story.removal.add.";
        public const string ActionStoryRemovalDeletePrefix = "scenario.story.removal.delete.";
        public const string ActionStoryRemovalItemPrefix = "scenario.story.removal.item.";
        public const string ActionStoryRemovalQuantityPrefix = "scenario.story.removal.quantity.";
        public const string ActionStoryMilestoneAddPrefix = "scenario.story.milestone.add.";
        public const string ActionStoryMilestoneDeletePrefix = "scenario.story.milestone.delete.";
        public const string ActionStoryMilestoneNamePrefix = "scenario.story.milestone.name.";
        public const string ActionStoryStageChangeTargetPrefix = "scenario.story.stage_change.target.";
        public const string ActionStoryStageChangeDelayPrefix = "scenario.story.stage_change.delay.";
        public const string ActionStoryRecruitTogglePrefix = "scenario.story.recruit.";
        public const string ActionStoryRecruitFamilyPrefix = "scenario.story.recruit_family.";
        public const string ActionStoryCharacterAdd = "scenario.story.character.add";
        public const string ActionStoryCharacterEditPrefix = "scenario.story.character.edit.";
        public const string ActionStoryCharacterDeletePrefix = "scenario.story.character.delete.";
        public const string ActionStoryCharacterActorPrefix = "scenario.story.character.actor.";
        public const string ActionStoryCharacterActorClearPrefix = "scenario.story.character.actor.clear.";
        public const string ActionStoryConversationAdd = "scenario.story.conversation.add";
        public const string ActionStoryConversationDeletePrefix = "scenario.story.conversation.delete.";
        public const string ActionStoryConversationDuplicatePrefix = "scenario.story.conversation.duplicate.";
        public const string ActionStoryConversationMovePrefix = "scenario.story.conversation.move.";
        public const string ActionStoryConversationIdPrefix = "scenario.story.conversation.id.";
        public const string ActionStoryConversationTriggerSourcePrefix = "scenario.story.conversation.trigger.source.";
        public const string ActionStoryConversationTriggerIdPrefix = "scenario.story.conversation.trigger.id.";
        public const string ActionStoryConversationTriggerWeightPrefix = "scenario.story.conversation.trigger.weight.";
        public const string ActionStoryConversationTriggerCooldownPrefix = "scenario.story.conversation.trigger.cooldown.";
        public const string ActionStoryConversationTriggerOncePrefix = "scenario.story.conversation.trigger.once.";
        public const string ActionStoryConversationTriggerDayPrefix = "scenario.story.conversation.trigger.day.";
        public const string ActionStoryConversationTriggerHourPrefix = "scenario.story.conversation.trigger.hour.";
        public const string ActionStoryConversationTriggerMinutePrefix = "scenario.story.conversation.trigger.minute.";
        public const string ActionStoryConversationParticipantAddPrefix = "scenario.story.conversation.participant.add.";
        public const string ActionStoryConversationParticipantDeletePrefix = "scenario.story.conversation.participant.delete.";
        public const string ActionStoryConversationParticipantSlotPrefix = "scenario.story.conversation.participant.slot.";
        public const string ActionStoryConversationParticipantStoryPrefix = "scenario.story.conversation.participant.story.";
        public const string ActionStoryConversationParticipantActorPrefix = "scenario.story.conversation.participant.actor.";
        public const string ActionStoryConversationParticipantFallbackPrefix = "scenario.story.conversation.participant.fallback.";
        public const string ActionStoryConversationParticipantRequiredPrefix = "scenario.story.conversation.participant.required.";
        public const string ActionStoryConversationLineAddPrefix = "scenario.story.conversation.line.add.";
        public const string ActionStoryConversationLineDeletePrefix = "scenario.story.conversation.line.delete.";
        public const string ActionStoryConversationLineSpeakerPrefix = "scenario.story.conversation.line.speaker.";
        public const string ActionStoryConversationLineTextPrefix = "scenario.story.conversation.line.text.";
        public const string ActionStoryConversationLineDelayPrefix = "scenario.story.conversation.line.delay.";
        public const string ActionStoryConversationSuppressionToggle = "scenario.story.conversation.suppression.toggle";
        public const string ActionStoryConversationSuppressionCategoryPrefix = "scenario.story.conversation.suppression.category.";
        public const string ActionStoryConversationSuppressionTopicPrefix = "scenario.story.conversation.suppression.topic.";
        public const string ActionStoryConversationPreviewPrefix = "scenario.story.conversation.preview.";
        public const string ActionStoryEndTypePrefix = "scenario.story.end.type.";
        public const string ActionStoryEndCompleteQuestPrefix = "scenario.story.end.complete_quest.";
        public const string ActionStoryEndCompleteScenarioPrefix = "scenario.story.end.complete_scenario.";
        public const string ActionQuestCaptureActive = "scenario.quest.capture_active";
        public const string ActionQuestScheduleAdd = "scenario.quest.schedule.add";
        public const string ActionQuestCatalogAddPrefix = "scenario.quest.catalog.add.";
        public const string ActionQuestScheduleDeletePrefix = "scenario.quest.schedule.delete.";
        public const string ActionQuestScheduleDayPrefix = "scenario.quest.schedule.day.";
        public const string ActionQuestScheduleHourPrefix = "scenario.quest.schedule.hour.";
        public const string ActionQuestScheduleMinutePrefix = "scenario.quest.schedule.minute.";
        public const string ActionQuestIdCyclePrefix = "scenario.quest.id.";
        public const string ActionQuestStartModePrefix = "scenario.quest.start_mode.";
        public const string ActionQuestTriggerCyclePrefix = "scenario.quest.trigger.";
        public const string ActionQuestCompletionCyclePrefix = "scenario.quest.completion.";
        public const string ActionQuestTitleSyncPrefix = "scenario.quest.title_sync.";
        public const string ActionQuestDescriptionSyncPrefix = "scenario.quest.description_sync.";
        public const string ActionQuestDuplicatePrefix = "scenario.quest.duplicate.";
        public const string ActionQuestMovePrefix = "scenario.quest.move.";
        public const string ActionQuestSpawnNowPrefix = "scenario.quest.spawn_now.";
        public const string ActionTriggerAddManual = "scenario.trigger.add.manual";
        public const string ActionTriggerAddScheduled = "scenario.trigger.add.scheduled";
        public const string ActionTriggerDeletePrefix = "scenario.trigger.delete.";
        public const string ActionTriggerTypePrefix = "scenario.trigger.type.";
        public const string ActionTriggerDayPrefix = "scenario.trigger.day.";
        public const string ActionTriggerHourPrefix = "scenario.trigger.hour.";
        public const string ActionTriggerMinutePrefix = "scenario.trigger.minute.";
        public const string ActionTriggerTargetPrefix = "scenario.trigger.target.";
        public const string ActionGateAdd = "scenario.gate.add";
        public const string ActionGateDeletePrefix = "scenario.gate.delete.";
        public const string ActionGateModePrefix = "scenario.gate.mode.";
        public const string ActionGateConditionAddPrefix = "scenario.gate.condition.add.";
        public const string ActionGateConditionDeletePrefix = "scenario.gate.condition.delete.";
        public const string ActionGateConditionKindPrefix = "scenario.gate.condition.kind.";
        public const string ActionGateConditionTargetPrefix = "scenario.gate.condition.target.";
        public const string ActionGateConditionActorPrefix = "scenario.gate.condition.actor.";
        public const string ActionGateConditionQuantityPrefix = "scenario.gate.condition.quantity.";
        public const string ActionGateConditionFlagValuePrefix = "scenario.gate.condition.flag_value.";
        public const string ActionScheduledActionAdd = "scenario.action.add";
        public const string ActionScheduledActionDeletePrefix = "scenario.action.delete.";
        public const string ActionScheduledActionDayPrefix = "scenario.action.day.";
        public const string ActionScheduledActionHourPrefix = "scenario.action.hour.";
        public const string ActionScheduledActionMinutePrefix = "scenario.action.minute.";
        public const string ActionScheduledActionTypePrefix = "scenario.action.type.";
        public const string ActionScheduledActionGatePrefix = "scenario.action.gate.";
        public const string ActionScheduledActionRepeatPrefix = "scenario.action.repeat.";
        public const string ActionScheduledActionCooldownPrefix = "scenario.action.cooldown.";
        public const string ActionScheduledActionWindowEndDayPrefix = "scenario.action.window_end_day.";
        public const string ActionScheduledActionChancePrefix = "scenario.action.chance.";
        public const string ActionScheduledActionJitterPrefix = "scenario.action.jitter.";
        public const string ActionScheduledActionMaxRunsPrefix = "scenario.action.max_runs.";
        public const string ActionScheduledActionEffectAddPrefix = "scenario.action.effect.add.";
        public const string ActionScheduledActionEffectDeletePrefix = "scenario.action.effect.delete.";
        public const string ActionScheduledActionEffectKindPrefix = "scenario.action.effect.kind.";
        public const string ActionScheduledActionEffectTargetPrefix = "scenario.action.effect.target.";
        public const string ActionScheduledActionEffectActorPrefix = "scenario.action.effect.actor.";
        public const string ActionScheduledActionEffectQuantityPrefix = "scenario.action.effect.quantity.";
        public const string ActionScheduledActionEffectWeatherDurationPrefix = "scenario.action.effect.weather_duration.";
        public const string ActionScheduledActionEffectFlagValuePrefix = "scenario.action.effect.flag_value.";
        public const string ActionWorldEventAdd = "scenario.world_event.add";
        public const string ActionWorldEventEventTypePrefix = "scenario.world_event.type.";
        public const string ActionWorldEventNpcTypePrefix = "scenario.world_event.npc_type.";
        public const string ActionWorldEventOutcomePrefix = "scenario.world_event.outcome.";
        public const string ActionWorldEventTradeAddPrefix = "scenario.world_event.trade.add.";
        public const string ActionWorldEventTradeDeletePrefix = "scenario.world_event.trade.delete.";
        public const string ActionWorldEventTradeItemPrefix = "scenario.world_event.trade.item.";
        public const string ActionWorldEventTradeQuantityPrefix = "scenario.world_event.trade.quantity.";
        public const string ActionWorldEventWeaponAddPrefix = "scenario.world_event.weapon.add.";
        public const string ActionWorldEventWeaponDeletePrefix = "scenario.world_event.weapon.delete.";
        public const string ActionWorldEventWeaponItemPrefix = "scenario.world_event.weapon.item.";
        public const string ActionWorldEventWeaponQuantityPrefix = "scenario.world_event.weapon.quantity.";
        public const string ActionWorldEventArmorAddPrefix = "scenario.world_event.armor.add.";
        public const string ActionWorldEventArmorDeletePrefix = "scenario.world_event.armor.delete.";
        public const string ActionWorldEventArmorItemPrefix = "scenario.world_event.armor.item.";
        public const string ActionWorldEventArmorQuantityPrefix = "scenario.world_event.armor.quantity.";
        public const string ActionWorldEventRaidMinPrefix = "scenario.world_event.raid.min.";
        public const string ActionWorldEventRaidMaxPrefix = "scenario.world_event.raid.max.";
        public const string ActionWorldEventSuppressionPrefix = "scenario.world_event.suppression.";
        public const string ActionJournalEntryAdd = "scenario.journal.entry.add";
        public const string ActionJournalEntryDeletePrefix = "scenario.journal.entry.delete.";
        public const string ActionJournalEntryIdPrefix = "scenario.journal.entry.id.";
        public const string ActionJournalEntryTextPrefix = "scenario.journal.entry.text.";
        public const string ActionJournalEntryDayPrefix = "scenario.journal.entry.day.";
        public const string ActionJournalEntryHourPrefix = "scenario.journal.entry.hour.";
        public const string ActionJournalEntryMinutePrefix = "scenario.journal.entry.minute.";
        public const string ActionJournalEntryGatePrefix = "scenario.journal.entry.gate.";
        public const string ActionJournalEntryRepeatPrefix = "scenario.journal.entry.repeat.";
        public const string ActionJournalEntryWriterPrefix = "scenario.journal.entry.writer.";
        public const string ActionJournalEntryWriterAnyPrefix = "scenario.journal.entry.writer_any.";
        public const string ActionJournalVanillaSuppressFirst = "scenario.journal.vanilla.suppress_first";
        public const string ActionJournalVanillaCategoryPrefix = "scenario.journal.vanilla.category.";
        public const string ActionTimelinePresetPrefix = "scenario.timeline.preset.";
        public const string ActionTimelineDayPrefix = "scenario.timeline.day.";
        public const string ActionTimelineEntryPrefix = "scenario.timeline.entry.";
        public const string ActionSpriteSwapClear = "sprite_swap.clear";
        public const string ActionSpriteSwapRevert = "sprite_swap.revert";
        public const string ActionSpriteSwapCopy = "sprite_swap.copy";
        public const string ActionSpriteSwapPaste = "sprite_swap.paste";
        public const string ActionSpriteSwapApplyPrefix = "sprite_swap.apply.";
        public const string ActionSpriteSwapPickerOpen = "sprite_swap.picker.open";
        public const string ActionSpriteSwapPickerSave = "sprite_swap.picker.save";
        public const string ActionSpriteSwapPickerCancel = "sprite_swap.picker.cancel";
        public const string ActionSpriteSwapPreviewPrefix = "sprite_swap.preview.";
        public const string ActionSpriteSwapImportPng = "sprite_swap.import_png";
        public const string ActionSpriteSwapCustomEditStart = "sprite_swap.custom.start";
        public const string ActionSpriteSwapCustomEditDiscard = "sprite_swap.custom.discard";
        public const string ActionSpriteSwapCustomBrushPrefix = "sprite_swap.custom.brush.";
        public const string ActionSpriteSwapCustomPaintPrefix = "sprite_swap.custom.paint.";
        public const string ActionSpriteSwapCustomPickPrefix = "sprite_swap.custom.pick.";
        public const string ActionSpriteSwapCustomToolPaint = "sprite_swap.custom.tool.paint";
        public const string ActionSpriteSwapCustomToolPick = "sprite_swap.custom.tool.pick";
        public const string ActionSpriteSwapCustomToolSelect = "sprite_swap.custom.tool.select";
        public const string ActionSpriteSwapCustomSelectionClear = "sprite_swap.custom.selection.clear";
        public const string ActionSpriteSwapCustomCopy = "sprite_swap.custom.copy";
        public const string ActionSpriteSwapCustomPaste = "sprite_swap.custom.paste";
        public const string ActionSpriteSwapCustomStrokeBegin = "sprite_swap.custom.stroke.begin";
        public const string ActionSpriteSwapCustomPresetPrefix = "sprite_swap.custom.preset.";
        public const string ActionSpriteSwapCustomColorPrefix = "sprite_swap.custom.color.";
        public const string ActionSpriteSwapCustomZoomIn = "sprite_swap.custom.zoom_in";
        public const string ActionSpriteSwapCustomZoomOut = "sprite_swap.custom.zoom_out";
        public const string ActionSpriteSwapCustomZoomReset = "sprite_swap.custom.zoom_reset";
        public const string ActionSpriteSwapAnimationFramePrefix = "sprite_swap.anim.frame.";
        public const string ActionSpriteSwapAnimationCopyPrefix = "sprite_swap.anim.copy.";
        public const string ActionSpriteSwapAnimationPlayPause = "sprite_swap.anim.play_pause";
        public const string ActionSpriteSwapAnimationStepPrevious = "sprite_swap.anim.step_prev";
        public const string ActionSpriteSwapAnimationStepNext = "sprite_swap.anim.step_next";
        public const string ActionSpriteSwapAnimationOnionToggle = "sprite_swap.anim.onion";
        public const string ActionSpriteSwapAnimationCompareToggle = "sprite_swap.anim.compare";
        public const string ActionSpriteSwapAnimationRevertFrame = "sprite_swap.anim.revert_frame";
        public const string ActionSpriteSwapAnimationRevertAll = "sprite_swap.anim.revert_all";
        public const string ActionSpriteSwapAnimationPlayInWorld = "sprite_swap.anim.play_world";
        public const string ActionSpriteSwapAnimationSpeedPrefix = "sprite_swap.anim.speed.";
        public const string ActionSpriteSwapCharacterPartHead = "sprite_swap.character.part.head";
        public const string ActionSpriteSwapCharacterPartTorso = "sprite_swap.character.part.torso";
        public const string ActionSpriteSwapCharacterPartLegs = "sprite_swap.character.part.legs";
        public const string ActionSpriteSwapCustomSelectStartPrefix = "sprite_swap.custom.select.start.";
        public const string ActionSpriteSwapCustomSelectDragPrefix = "sprite_swap.custom.select.drag.";
        public const string ActionSpriteSwapCustomSelectEndPrefix = "sprite_swap.custom.select.end.";
        public const string ActionHistoryUndo = "history.undo";
        public const string ActionHistoryRedo = "history.redo";
        public const string ActionSceneSpritePlacementRemove = "scene_sprite.remove";
        public const string ActionSceneSpritePlacementCancel = "scene_sprite.cancel";
        public const string ActionSceneSpritePlacementApplyPrefix = "scene_sprite.apply.";
        public const string ActionWeatherEffectSpriteSelectPrefix = "weather_effect.select.";
        public const string ActionBuildPlacementCancel = "build.place.cancel";
        public const string ActionBuildPlacementCommitGridPrefix = "build.place.commit.grid.";
        public const string ActionBuildObjectPlacePrefix = "build.place.object.";
        public const string ActionBuildStructureRoom = "build.place.room";
        public const string ActionBuildStructureLadder = "build.place.ladder";
        public const string ActionBuildStructureLight = "build.place.light";
        public const string ActionBuildDeleteObject = "build.delete.object";
        public const string ActionBuildDeleteRoom = "build.delete.room";
        public const string ActionBuildDeleteLadder = "build.delete.ladder";
        public const string ActionBuildDeleteLight = "build.delete.light";
        public const string ActionBuildResetWall = "build.reset.wall";
        public const string ActionBuildResetWire = "build.reset.wire";
        public const string ActionBuildWallApplyPrefix = "build.wall.apply.";
        public const string ActionBuildWireApplyPrefix = "build.wire.apply.";
        public const string ActionAssetBrowserSelectPrefix = "asset_browser.select.";
        public const string ActionAssetBrowserPlaceSelected = "asset_browser.place_selected";
        public const string ActionAssetBrowserEditSelected = "asset_browser.edit_selected";
        public const string ActionToolSelect = "tool.select";
        public const string ActionToolFamily = "tool.family";
        public const string ActionToolInventory = "tool.inventory";
        public const string ActionToolShelter = "tool.shelter";
        public const string ActionToolAssets = "tool.assets";
        public const string ActionToolObjects = "tool.objects";
        public const string ActionToolWiring = "tool.wiring";
        public const string ActionToolPeople = "tool.people";
        public const string ActionToolVehicle = "tool.vehicle";
        public const string ActionToolWinLoss = "tool.win_loss";
        public const string ActionScenarioModePrevious = "scenario.mode.previous";
        public const string ActionScenarioModeNext = "scenario.mode.next";
        public const string ActionFocusedEditorSave = "scenario.focused_editor.save";
        public const string ActionFocusedEditorCancel = "scenario.focused_editor.cancel";
        public const string ActionMapAuthoringOpen = "scenario.map.open_real";
        public const string ActionMapAuthoringClose = "scenario.map.close_real";
        public const string ActionMapAuthoringCaptureSelection = "scenario.map.capture_selection";
        public const string ActionMapAuthoringSelectWorldPrefix = "scenario.map.select_world.";
        public const string ActionMapAuthoringClickWorldPrefix = "scenario.map.click_world.";
        public const string ActionMapAuthoringModeSelect = "scenario.map.mode.select";
        public const string ActionMapAuthoringModePlace = "scenario.map.mode.place";
        public const string ActionMapAuthoringModeMove = "scenario.map.mode.move";
        public const string ActionMapAuthoringSelectLocationPrefix = "scenario.map.select_location.";
        public const string ActionMapLocationEditPrefix = "scenario.map.location.edit.";
        public const string ActionMapLocationTogglePrefix = "scenario.map.location.toggle.";
        public const string ActionMapLocationCycleIconPrefix = "scenario.map.location.icon_next.";
        public const string ActionMapLocationDuplicatePrefix = "scenario.map.location.duplicate.";
        public const string ActionInventoryStorageOpen = "scenario.inventory.storage.open_real";
        public const string ActionInventoryStorageClose = "scenario.inventory.storage.close_real";
        public const string ActionVanillaInteractionReturnEditor = "shell.vanilla_interaction.return_editor";
    }

    internal enum ScenarioAuthoringTool
    {
        Select = 0,
        Family = 1,
        Inventory = 2,
        Shelter = 3,
        Objects = 4,
        Wiring = 5,
        People = 6,
        Vehicle = 7,
        WinLoss = 8,
        Assets = 9
    }

    internal enum ScenarioAssetAuthoringMode
    {
        ReplaceExisting = 0,
        PlaceNew = 1
    }

    internal enum ScenarioAuthoringShellTab
    {
        Shelter = 0,
        Build = 1,
        Survivors = 2,
        Stockpile = 3,
        Triggers = 4,
        Jobs = 5,
        Quests = 6,
        Art = 7,
        Map = 8,
        Test = 9,
        Publish = 10,
        Shell = 11
    }

    internal enum ScenarioAuthoringInspectorTab
    {
        Properties = 0,
        Interactions = 1,
        Visuals = 2,
        Runtime = 3,
        Notes = 4
    }

    internal enum ScenarioAuthoringShellDock
    {
        Top = 0,
        Left = 1,
        Right = 2,
        Bottom = 3,
        Overlay = 4,
        Floating = 5,
        Status = 6
    }

    internal enum ScenarioAuthoringShellRendererKind
    {
        Standard = 0,
        Inspector = 1,
        BottomTray = 2
    }

    internal enum ScenarioAuthoringWindowContentKind
    {
        Empty = 0,
        Scenario = 1,
        TilesPalette = 3,
        Inspector = 4,
        BuildTools = 5,
        Triggers = 6,
        Survivors = 7,
        Stockpile = 8,
        Quests = 9,
        Map = 10,
        Publish = 11,
        AssetBrowser = 12,
        Hierarchy = 13,
        SelectionStack = 14,
        PixelEditor = 15
    }

    internal enum ScenarioAuthoringSettingKind
    {
        Toggle = 0,
        Float = 1,
        Integer = 2,
        Choice = 3,
        ReadOnly = 4
    }

    internal enum ScenarioAuthoringTargetKind
    {
        None = 0,
        Unknown = 1,
        Character = 2,
        PlaceableObject = 3,
        Wall = 4,
        Wire = 5,
        Light = 6,
        Vehicle = 7,
        Room = 8,
        Tile = 9,
        Background = 10,
        SceneSprite = 11
    }

    internal sealed class ScenarioAuthoringActionExecutionResult
    {
        public bool Ok { get; set; }
        public string ActionId { get; set; }
        public bool Result { get; set; }
        public string Reason { get; set; }
        public string StatusMessage { get; set; }

        public static ScenarioAuthoringActionExecutionResult Success(string actionId, bool result, string statusMessage)
        {
            return new ScenarioAuthoringActionExecutionResult
            {
                Ok = true,
                ActionId = actionId ?? string.Empty,
                Result = result,
                Reason = string.Empty,
                StatusMessage = statusMessage ?? string.Empty
            };
        }

        public static ScenarioAuthoringActionExecutionResult Failure(string actionId, string reason, string statusMessage)
        {
            string safeReason = string.IsNullOrEmpty(reason) ? "Action did not complete." : reason;
            return new ScenarioAuthoringActionExecutionResult
            {
                Ok = true,
                ActionId = actionId ?? string.Empty,
                Result = false,
                Reason = safeReason,
                StatusMessage = statusMessage ?? safeReason
            };
        }

        public static ScenarioAuthoringActionExecutionResult Unavailable(string actionId, string reason)
        {
            string safeReason = string.IsNullOrEmpty(reason) ? "Scenario authoring is not active." : reason;
            return new ScenarioAuthoringActionExecutionResult
            {
                Ok = false,
                ActionId = actionId ?? string.Empty,
                Result = false,
                Reason = safeReason,
                StatusMessage = safeReason
            };
        }
    }

    internal sealed class ScenarioAuthoringTarget
    {
        public string Id { get; set; }
        public ScenarioAuthoringTargetKind Kind { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string AdapterId { get; set; }
        public string GameObjectName { get; set; }
        public string TransformPath { get; set; }
        public string ScenarioReferenceId { get; set; }
        public UnityEngine.Object RuntimeObject { get; set; }
        public UnityEngine.Object HighlightObject { get; set; }
        public Vector3 WorldPosition { get; set; }
        public int? GridX { get; set; }
        public int? GridY { get; set; }
        public bool SupportsInspect { get; set; }
        public bool SupportsReplace { get; set; }

        public ScenarioAuthoringTarget Copy()
        {
            return new ScenarioAuthoringTarget
            {
                Id = Id,
                Kind = Kind,
                DisplayName = DisplayName,
                Description = Description,
                AdapterId = AdapterId,
                GameObjectName = GameObjectName,
                TransformPath = TransformPath,
                ScenarioReferenceId = ScenarioReferenceId,
                RuntimeObject = RuntimeObject,
                HighlightObject = HighlightObject,
                WorldPosition = WorldPosition,
                GridX = GridX,
                GridY = GridY,
                SupportsInspect = SupportsInspect,
                SupportsReplace = SupportsReplace
            };
        }
    }

    internal sealed class ScenarioAuthoringState
    {
        public ScenarioAuthoringState()
        {
            WindowStates = new List<ScenarioAuthoringWindowState>();
            MultiSelection = new List<ScenarioAuthoringTarget>();
            SelectionStack = new List<ScenarioAuthoringTarget>();
            ScrollStates = new List<ScenarioAuthoringPanelScrollState>();
            Settings = new ScenarioAuthoringSettingsSnapshot();
            SetupState = new ScenarioAuthoringSetupState();
            HistoryRestoreCandidateIndex = -1;
            HistoryDeleteCandidateIndex = -1;
        }

        public bool IsActive { get; set; }
        public bool ReloadPending { get; set; }
        public string ReloadPendingReason { get; set; }
        public bool WorldLoading { get; set; }
        public string WorldLoadingStatus { get; set; }
        public bool ShellVisible { get; set; }
        public bool SelectionModeActive { get; set; }
        public ScenarioStageKind ActiveStage { get; set; }
        public ScenarioStageKind ActiveBunkerStage { get; set; }
        public ScenarioAuthoringTool ActiveTool { get; set; }
        public ScenarioAuthoringShellTab ActiveShellTab { get; set; }
        public ScenarioAssetAuthoringMode AssetMode { get; set; }
        public string ActiveLayoutPreset { get; set; }
        public bool MinimalMode { get; set; }
        public bool FocusSelectionMode { get; set; }
        public bool PixelEditorChromeSuppressed { get; set; }
        public bool PixelEditorRestoreBuildToolsVisible { get; set; }
        public bool PixelEditorRestoreInspectorVisible { get; set; }
        public bool PixelEditorRestoreBuildToolsCollapsed { get; set; }
        public bool PixelEditorRestoreInspectorCollapsed { get; set; }
        public string ActiveDraftId { get; set; }
        public string ActiveScenarioFilePath { get; set; }
        public string StatusMessage { get; set; }
        public ScenarioAuthoringTarget HoveredTarget { get; set; }
        public ScenarioAuthoringTarget SelectedTarget { get; set; }
        public List<ScenarioAuthoringTarget> MultiSelection { get; private set; }
        public List<ScenarioAuthoringTarget> SelectionStack { get; private set; }
        public int ActiveSelectionStackIndex { get; set; }
        public bool SelectionStackExpanded { get; set; }
        public string SelectionStackSignature { get; set; }
        public string TimelineSelectionId { get; set; }
        public string TimelineSelectedDayId { get; set; }
        public string TimelineSelectedEntryId { get; set; }
        public string AssetBrowserSelectedActionId { get; set; }
        public bool MapAuthoringActive { get; set; }
        public bool MapAuthoringPreviousShellVisible { get; set; }
        public string MapAuthoringMode { get; set; }
        public string MapSelectedLocationId { get; set; }
        public ScenarioMapRegionSelection MapSelection { get; set; }
        public bool StorageAuthoringActive { get; set; }
        public bool StorageAuthoringPreviousShellVisible { get; set; }
        public bool VanillaInteractionActive { get; set; }
        public bool VanillaInteractionPreviousShellVisible { get; set; }
        public string VanillaInteractionKind { get; set; }
        public string VanillaInteractionAssistNote { get; set; }
        public string FocusedEditorKind { get; set; }
        public int FocusedEditorIndex { get; set; }
        public bool FocusedEditorIsNew { get; set; }
        public string SurvivorColorPickerChannel { get; set; }
        public int SurvivorColorPickerRequestId { get; set; }
        public ScenarioAuthoringInspectorTab InspectorTab { get; set; }
        public string FilterText { get; set; }
        public string SearchText { get; set; }
        public bool SettingsWindowOpen { get; set; }
        public bool HelpWindowOpen { get; set; }
        public bool HelpShortcutsView { get; set; }
        public bool WindowMenuOpen { get; set; }
        public bool GlobalSearchOpen { get; set; }
        public bool HistoryWindowOpen { get; set; }
        public int HistoryRestoreCandidateIndex { get; set; }
        public int HistoryDeleteCandidateIndex { get; set; }
        public ScenarioSpriteSwapPickerState SpriteSwapPicker { get; set; }
        public List<ScenarioAuthoringWindowState> WindowStates { get; private set; }
        public List<ScenarioAuthoringPanelScrollState> ScrollStates { get; private set; }
        public ScenarioAuthoringSettingsSnapshot Settings { get; set; }
        public ScenarioAuthoringSetupState SetupState { get; set; }

        public ScenarioAuthoringState Copy()
        {
            ScenarioAuthoringState copy = new ScenarioAuthoringState
            {
                IsActive = IsActive,
                ReloadPending = ReloadPending,
                ReloadPendingReason = ReloadPendingReason,
                WorldLoading = WorldLoading,
                WorldLoadingStatus = WorldLoadingStatus,
                ShellVisible = ShellVisible,
                SelectionModeActive = SelectionModeActive,
                ActiveStage = ActiveStage,
                ActiveBunkerStage = ActiveBunkerStage,
                ActiveTool = ActiveTool,
                ActiveShellTab = ActiveShellTab,
                AssetMode = AssetMode,
                ActiveLayoutPreset = ActiveLayoutPreset,
                MinimalMode = MinimalMode,
                FocusSelectionMode = FocusSelectionMode,
                PixelEditorChromeSuppressed = PixelEditorChromeSuppressed,
                PixelEditorRestoreBuildToolsVisible = PixelEditorRestoreBuildToolsVisible,
                PixelEditorRestoreInspectorVisible = PixelEditorRestoreInspectorVisible,
                PixelEditorRestoreBuildToolsCollapsed = PixelEditorRestoreBuildToolsCollapsed,
                PixelEditorRestoreInspectorCollapsed = PixelEditorRestoreInspectorCollapsed,
                ActiveDraftId = ActiveDraftId,
                ActiveScenarioFilePath = ActiveScenarioFilePath,
                StatusMessage = StatusMessage,
                HoveredTarget = HoveredTarget != null ? HoveredTarget.Copy() : null,
                SelectedTarget = SelectedTarget != null ? SelectedTarget.Copy() : null,
                ActiveSelectionStackIndex = ActiveSelectionStackIndex,
                SelectionStackExpanded = SelectionStackExpanded,
                SelectionStackSignature = SelectionStackSignature,
                TimelineSelectionId = TimelineSelectionId,
                TimelineSelectedDayId = TimelineSelectedDayId,
                TimelineSelectedEntryId = TimelineSelectedEntryId,
                AssetBrowserSelectedActionId = AssetBrowserSelectedActionId,
                MapAuthoringActive = MapAuthoringActive,
                MapAuthoringPreviousShellVisible = MapAuthoringPreviousShellVisible,
                MapAuthoringMode = MapAuthoringMode,
                MapSelectedLocationId = MapSelectedLocationId,
                MapSelection = MapSelection != null ? MapSelection.Copy() : null,
                StorageAuthoringActive = StorageAuthoringActive,
                StorageAuthoringPreviousShellVisible = StorageAuthoringPreviousShellVisible,
                VanillaInteractionActive = VanillaInteractionActive,
                VanillaInteractionPreviousShellVisible = VanillaInteractionPreviousShellVisible,
                VanillaInteractionKind = VanillaInteractionKind,
                VanillaInteractionAssistNote = VanillaInteractionAssistNote,
                FocusedEditorKind = FocusedEditorKind,
                FocusedEditorIndex = FocusedEditorIndex,
                FocusedEditorIsNew = FocusedEditorIsNew,
                SurvivorColorPickerChannel = SurvivorColorPickerChannel,
                SurvivorColorPickerRequestId = SurvivorColorPickerRequestId,
                InspectorTab = InspectorTab,
                FilterText = FilterText,
                SearchText = SearchText,
                SettingsWindowOpen = SettingsWindowOpen,
                HelpWindowOpen = HelpWindowOpen,
                HelpShortcutsView = HelpShortcutsView,
                WindowMenuOpen = WindowMenuOpen,
                GlobalSearchOpen = GlobalSearchOpen,
                HistoryWindowOpen = HistoryWindowOpen,
                HistoryRestoreCandidateIndex = HistoryRestoreCandidateIndex,
                HistoryDeleteCandidateIndex = HistoryDeleteCandidateIndex,
                SpriteSwapPicker = SpriteSwapPicker != null ? SpriteSwapPicker.Copy() : null,
                Settings = Settings != null ? Settings.Copy() : new ScenarioAuthoringSettingsSnapshot(),
                SetupState = SetupState != null ? SetupState.Copy() : new ScenarioAuthoringSetupState()
            };

            for (int i = 0; MultiSelection != null && i < MultiSelection.Count; i++)
            {
                ScenarioAuthoringTarget target = MultiSelection[i];
                if (target != null)
                    copy.MultiSelection.Add(target.Copy());
            }

            for (int i = 0; SelectionStack != null && i < SelectionStack.Count; i++)
            {
                ScenarioAuthoringTarget target = SelectionStack[i];
                if (target != null)
                    copy.SelectionStack.Add(target.Copy());
            }

            for (int i = 0; WindowStates != null && i < WindowStates.Count; i++)
            {
                ScenarioAuthoringWindowState state = WindowStates[i];
                if (state != null)
                    copy.WindowStates.Add(state.Copy());
            }

            for (int i = 0; ScrollStates != null && i < ScrollStates.Count; i++)
            {
                ScenarioAuthoringPanelScrollState scroll = ScrollStates[i];
                if (scroll != null)
                    copy.ScrollStates.Add(scroll.Copy());
            }

            return copy;
        }
    }

    internal sealed class ScenarioSpriteSwapPickerState
    {
        public bool IsOpen { get; set; }
        public ScenarioAuthoringTarget Target { get; set; }
        public string TargetPath { get; set; }
        public string SavedCandidateToken { get; set; }
        public string SavedCandidateLabel { get; set; }
        public string PreviewCandidateToken { get; set; }
        public string PreviewCandidateLabel { get; set; }

        public ScenarioSpriteSwapPickerState Copy()
        {
            return new ScenarioSpriteSwapPickerState
            {
                IsOpen = IsOpen,
                Target = Target != null ? Target.Copy() : null,
                TargetPath = TargetPath,
                SavedCandidateToken = SavedCandidateToken,
                SavedCandidateLabel = SavedCandidateLabel,
                PreviewCandidateToken = PreviewCandidateToken,
                PreviewCandidateLabel = PreviewCandidateLabel
            };
        }
    }

    internal sealed class ScenarioAuthoringWindowDefinition
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public ScenarioAuthoringShellDock Dock { get; set; }
        public ScenarioStageKind WorkspaceStage { get; set; }
        public ScenarioAuthoringShellRendererKind RendererKind { get; set; }
        public ScenarioAuthoringWindowContentKind ContentKind { get; set; }
        public bool MenuVisible { get; set; }
        public bool WorkspaceTabVisible { get; set; }
        public bool DefaultVisible { get; set; }
        public bool DefaultCollapsed { get; set; }
        public bool DefaultPinned { get; set; }
        public int Order { get; set; }
        public float DefaultWidth { get; set; }
        public float DefaultHeight { get; set; }
        public float MinWidth { get; set; }
        public float MinHeight { get; set; }

        public bool IsWorkspaceStageWindow
        {
            get { return WorkspaceStage != ScenarioStageKind.None; }
        }
    }

    internal sealed class ScenarioAuthoringWindowState
    {
        public string Id { get; set; }
        public bool Visible { get; set; }
        public bool Collapsed { get; set; }
        public bool Pinned { get; set; }
        public int Order { get; set; }
        public bool HasCustomBounds { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public int ZIndex { get; set; }

        public ScenarioAuthoringWindowState Copy()
        {
            return new ScenarioAuthoringWindowState
            {
                Id = Id,
                Visible = Visible,
                Collapsed = Collapsed,
                Pinned = Pinned,
                Order = Order,
                HasCustomBounds = HasCustomBounds,
                X = X,
                Y = Y,
                Width = Width,
                Height = Height,
                ZIndex = ZIndex
            };
        }
    }

    internal sealed class ScenarioAuthoringPanelScrollState
    {
        public string PanelId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }

        public ScenarioAuthoringPanelScrollState Copy()
        {
            return new ScenarioAuthoringPanelScrollState
            {
                PanelId = PanelId,
                X = X,
                Y = Y
            };
        }
    }

    internal sealed class ScenarioAuthoringSettingDefinition
    {
        public string Id { get; set; }
        public string Section { get; set; }
        public string Label { get; set; }
        public string Description { get; set; }
        public ScenarioAuthoringSettingKind Kind { get; set; }
        public string DefaultValue { get; set; }
        public float MinValue { get; set; }
        public float MaxValue { get; set; }
        public float Step { get; set; }
        public string[] ChoiceValues { get; set; }
        public string[] ChoiceLabels { get; set; }
    }

    internal sealed class ScenarioAuthoringSettingValue
    {
        public string Id { get; set; }
        public string Value { get; set; }

        public ScenarioAuthoringSettingValue Copy()
        {
            return new ScenarioAuthoringSettingValue
            {
                Id = Id,
                Value = Value
            };
        }
    }

    internal sealed class ScenarioAuthoringSettingsSnapshot
    {
        private readonly List<ScenarioAuthoringSettingValue> _values = new List<ScenarioAuthoringSettingValue>();

        public List<ScenarioAuthoringSettingValue> Values
        {
            get { return _values; }
        }

        public string Get(string id, string fallback)
        {
            for (int i = 0; i < _values.Count; i++)
            {
                ScenarioAuthoringSettingValue value = _values[i];
                if (value != null && string.Equals(value.Id, id, StringComparison.OrdinalIgnoreCase))
                    return value.Value ?? fallback;
            }

            return fallback;
        }

        public bool GetBool(string id, bool fallback)
        {
            string value = Get(id, fallback ? "true" : "false");
            bool parsed;
            return bool.TryParse(value, out parsed) ? parsed : fallback;
        }

        public int GetInt(string id, int fallback)
        {
            string value = Get(id, fallback.ToString());
            int parsed;
            return int.TryParse(value, out parsed) ? parsed : fallback;
        }

        public float GetFloat(string id, float fallback)
        {
            string value = Get(id, fallback.ToString(System.Globalization.CultureInfo.InvariantCulture));
            float parsed;
            return float.TryParse(
                value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out parsed)
                ? parsed
                : fallback;
        }

        public void Set(string id, string value)
        {
            if (string.IsNullOrEmpty(id))
                return;

            for (int i = 0; i < _values.Count; i++)
            {
                ScenarioAuthoringSettingValue entry = _values[i];
                if (entry != null && string.Equals(entry.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    entry.Value = value;
                    return;
                }
            }

            _values.Add(new ScenarioAuthoringSettingValue
            {
                Id = id,
                Value = value
            });
        }

        public ScenarioAuthoringSettingsSnapshot Copy()
        {
            ScenarioAuthoringSettingsSnapshot copy = new ScenarioAuthoringSettingsSnapshot();
            for (int i = 0; i < _values.Count; i++)
            {
                ScenarioAuthoringSettingValue entry = _values[i];
                if (entry != null)
                    copy.Values.Add(entry.Copy());
            }

            return copy;
        }
    }

    internal sealed class ScenarioAuthoringShellWindowViewModel
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public ScenarioAuthoringShellDock Dock { get; set; }
        public ScenarioStageKind WorkspaceStage { get; set; }
        public ScenarioAuthoringShellRendererKind RendererKind { get; set; }
        public bool WorkspaceTabVisible { get; set; }
        public bool Visible { get; set; }
        public bool Collapsed { get; set; }
        public bool HasCustomBounds { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public float MinWidth { get; set; }
        public float MinHeight { get; set; }
        public int ZIndex { get; set; }
        public ScenarioAuthoringInspectorAction[] HeaderActions { get; set; }
        public ScenarioAuthoringInspectorSection[] Sections { get; set; }
    }

    internal sealed class ScenarioAuthoringSettingsItemViewModel
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public string Description { get; set; }
        public string ValueText { get; set; }
        public ScenarioAuthoringSettingKind Kind { get; set; }
        public bool BoolValue { get; set; }
        public bool Enabled { get; set; }
        public bool CanIncrease { get; set; }
        public bool CanDecrease { get; set; }
        public string[] ChoiceLabels { get; set; }
        public string[] ChoiceValues { get; set; }
        public int SelectedChoiceIndex { get; set; }
    }

    internal sealed class ScenarioAuthoringSettingsSectionViewModel
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public ScenarioAuthoringSettingsItemViewModel[] Items { get; set; }
    }

    internal sealed class ScenarioAuthoringSettingsViewModel
    {
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public ScenarioAuthoringInspectorAction[] HeaderActions { get; set; }
        public ScenarioAuthoringSettingsSectionViewModel[] Sections { get; set; }
    }

    internal sealed class ScenarioAuthoringGraphNodeViewModel
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public string Kind { get; set; }
        public string Detail { get; set; }
        public string Status { get; set; }
        public ScenarioAuthoringInspectorAction PrimaryAction { get; set; }
    }

    internal sealed class ScenarioAuthoringGraphEdgeViewModel
    {
        public string FromNodeId { get; set; }
        public string ToNodeId { get; set; }
        public string Label { get; set; }
        public string Status { get; set; }
    }

    internal sealed class ScenarioAuthoringToolButtonViewModel
    {
        public ScenarioAuthoringTool Tool { get; set; }
        public string Label { get; set; }
        public string IconText { get; set; }
        public ScenarioAuthoringInspectorAction Action { get; set; }
    }

    internal sealed class ScenarioAuthoringContextMenuModel
    {
        public bool Visible { get; set; }
        public string Title { get; set; }
        public string Detail { get; set; }
        public float AnchorX { get; set; }
        public float AnchorY { get; set; }
        public bool CenterOnScreen { get; set; }
        public ScenarioAuthoringInspectorAction[] Actions { get; set; }

        public ScenarioAuthoringContextMenuModel Copy()
        {
            ScenarioAuthoringContextMenuModel copy = new ScenarioAuthoringContextMenuModel
            {
                Visible = Visible,
                Title = Title,
                Detail = Detail,
                AnchorX = AnchorX,
                AnchorY = AnchorY,
                CenterOnScreen = CenterOnScreen
            };

            if (Actions != null)
            {
                copy.Actions = new ScenarioAuthoringInspectorAction[Actions.Length];
                Array.Copy(Actions, copy.Actions, Actions.Length);
            }

            return copy;
        }
    }

    internal sealed class ScenarioAuthoringShellViewModel
    {
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string TimeLabel { get; set; }
        public ScenarioAuthoringInspectorAction[] Tabs { get; set; }
        public ScenarioAuthoringInspectorAction[] ToolbarActions { get; set; }
        public ScenarioAuthoringInspectorAction[] LayoutActions { get; set; }
        public ScenarioAuthoringInspectorAction[] WorldSubstageActions { get; set; }
        public ScenarioAuthoringToolButtonViewModel[] ToolButtons { get; set; }
        public ScenarioAuthoringInspectorAction[] WindowMenuActions { get; set; }
        public ScenarioAuthoringInspectorAction[] RendererActions { get; set; }
        public ScenarioAuthoringShellWindowViewModel[] Windows { get; set; }
        public ScenarioAuthoringInspectorDocument SpritePickerDocument { get; set; }
        public ScenarioAuthoringInspectorDocument FocusedEditorDocument { get; set; }
        internal ScenarioSpriteSwapAuthoringService.CustomEditorModel CustomSpriteEditor { get; set; }
        public ScenarioAuthoringSettingsViewModel Settings { get; set; }
        public ScenarioAuthoringHelpViewModel Help { get; set; }
        public ScenarioAuthoringTutorialViewModel Tutorial { get; set; }
        public ScenarioAuthoringTourViewModel Tour { get; set; }
        public ScenarioAuthoringContextMenuModel ContextMenu { get; set; }
        public ScenarioDayTimelineRibbonViewModel TimelineRibbon { get; set; }
        public string[] StatusEntries { get; set; }
    }

    /// <summary>
    /// Cached, presentation-only projection for the persistent workshop day ribbon.
    /// It deliberately contains semantic actions so renderers and automation use the
    /// same navigation routes as the full timeline page.
    /// </summary>
    internal sealed class ScenarioDayTimelineRibbonViewModel
    {
        public int FirstDay { get; set; }
        public int LastDay { get; set; }
        public int EntryCount { get; set; }
        public int ChapterCount { get; set; }
        public string EmptyMessage { get; set; }
        public ScenarioDayTimelineRibbonMarkerViewModel[] Markers { get; set; }
    }

    internal sealed class ScenarioDayTimelineRibbonMarkerViewModel
    {
        public int Day { get; set; }
        public string Domain { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public bool IsChapter { get; set; }
        public ScenarioAuthoringInspectorAction Action { get; set; }
    }

    internal sealed class ScenarioAuthoringHelpViewModel
    {
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public int PageIndex { get; set; }
        public int PageCount { get; set; }
        public string PageTitle { get; set; }
        public string TopicId { get; set; }
        public string Body { get; set; }
        public ScenarioAuthoringInspectorAction[] HeaderActions { get; set; }
        public ScenarioAuthoringInspectorAction[] ViewTabs { get; set; }
        public ScenarioAuthoringInspectorAction[] TopicActions { get; set; }
        public ScenarioAuthoringInspectorAction PreviousAction { get; set; }
        public ScenarioAuthoringInspectorAction NextAction { get; set; }
        public ScenarioAuthoringInspectorAction ReplayAction { get; set; }
        // Non-null when the help window is showing the Keyboard Shortcuts view.
        public ScenarioAuthoringShortcutOverlayViewModel Shortcuts { get; set; }
    }

    internal sealed class ScenarioAuthoringShortcutOverlayViewModel
    {
        public string ActiveContextTitle { get; set; }
        public ScenarioAuthoringShortcutGroupViewModel[] Groups { get; set; }
    }

    internal sealed class ScenarioAuthoringShortcutGroupViewModel
    {
        public string Title { get; set; }
        public bool IsActiveContext { get; set; }
        public ScenarioAuthoringShortcutRowViewModel[] Rows { get; set; }
    }

    internal sealed class ScenarioAuthoringShortcutRowViewModel
    {
        public string KeyChord { get; set; }
        public string Description { get; set; }
    }

    internal sealed class ScenarioAuthoringTourViewModel
    {
        public bool Visible { get; set; }
        public string TourId { get; set; }
        public int StepIndex { get; set; }
        public int StepCount { get; set; }
        public string TargetId { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public ScenarioAuthoringInspectorAction BackAction { get; set; }
        public ScenarioAuthoringInspectorAction NextAction { get; set; }
        public ScenarioAuthoringInspectorAction ExitAction { get; set; }
    }

    internal sealed class ScenarioAuthoringTutorialViewModel
    {
        public bool Visible { get; set; }
        public int StepIndex { get; set; }
        public int StepCount { get; set; }
        public string StepId { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public string PrimaryCallout { get; set; }
        public bool WaitingForAction { get; set; }
        public string TargetWindowId { get; set; }
        public string TargetActionId { get; set; }
        public string TargetId { get; set; }
        public ScenarioStageKind TargetStage { get; set; }
        public bool SkipPromptVisible { get; set; }
        public ScenarioAuthoringInspectorAction PrimaryAction { get; set; }
        public ScenarioAuthoringInspectorAction BackAction { get; set; }
        public ScenarioAuthoringInspectorAction NextAction { get; set; }
        public ScenarioAuthoringInspectorAction SkipAction { get; set; }
        public ScenarioAuthoringInspectorAction SkipPromptAction { get; set; }
        public ScenarioAuthoringInspectorAction SkipCancelAction { get; set; }
        public ScenarioAuthoringInspectorAction HelpAction { get; set; }
    }

    internal sealed class ScenarioAuthoringInspectorDocument
    {
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public ScenarioAuthoringInspectorAction[] HeaderActions { get; set; }
        public ScenarioAuthoringInspectorSection[] Sections { get; set; }
    }

    internal sealed class ScenarioAuthoringInspectorAction
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public string Hint { get; set; }
        public string Detail { get; set; }
        public string Badge { get; set; }
        public string IconText { get; set; }
        public Sprite PreviewSprite { get; set; }
        public Color PreviewTint { get; set; }
        public bool HasPreviewTint { get; set; }
        public bool Enabled { get; set; }
        public bool Emphasized { get; set; }
        public string DisabledReason { get; set; }
    }

    internal enum ScenarioAuthoringInspectorSectionLayout
    {
        Default = 0,
        MetricGrid = 1,
        PropertyList = 2,
        NoteList = 3,
        ActionStrip = 4,
        TabStrip = 5,
        Summary = 6,
        CandidateGrid = 7,
        FactGrid = 8,
        CastCardGrid = 9,
        SurvivorEditor = 10,
        InventorySlotGrid = 11,
        ModFieldList = 12
    }

    internal sealed class ScenarioAuthoringInspectorSection
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public bool Expanded { get; set; }
        public ScenarioAuthoringInspectorSectionLayout Layout { get; set; }
        public ScenarioAuthoringInspectorItem[] Items { get; set; }
        public ScenarioSurvivorEditorViewModel SurvivorEditor { get; set; }
        public ScenarioSurvivorModFieldRowViewModel[] ModFieldRows { get; set; }
        public ScenarioInventorySlotGridViewModel InventorySlotGrid { get; set; }
        // Non-null on the Story Map section: the visual story graph the Story Map renders.
        public ScenarioStoryGraphModel StoryMap { get; set; }
    }

    internal enum ScenarioAuthoringInspectorItemKind
    {
        Text = 0,
        Property = 1,
        Action = 2
    }

    internal sealed class ScenarioAuthoringInspectorItem
    {
        public ScenarioAuthoringInspectorItemKind Kind { get; set; }
        public string Label { get; set; }
        public string Value { get; set; }
        public string Detail { get; set; }
        public string Badge { get; set; }
        public string IconText { get; set; }
        public Sprite PreviewSprite { get; set; }
        public Color PreviewTint { get; set; }
        public bool HasPreviewTint { get; set; }
        public bool Emphasized { get; set; }
        public bool Editable { get; set; }
        public string HoverHint { get; set; }
        public string PulseKey { get; set; }
        public string PulseSignature { get; set; }
        public ScenarioCastCardViewModel CastCard { get; set; }
        public ScenarioAuthoringInspectorAction Action { get; set; }
    }

    internal sealed class ScenarioInventorySlotGridViewModel
    {
        public string EmptyMessage { get; set; }
        public bool ReadOnly { get; set; }
        public ScenarioInventorySlotViewModel[] Slots { get; set; }
    }

    internal sealed class ScenarioInventorySlotViewModel
    {
        public string Id { get; set; }
        public string ItemId { get; set; }
        public string DisplayName { get; set; }
        public string Detail { get; set; }
        public string QuantityText { get; set; }
        public string Badge { get; set; }
        public string ScheduleText { get; set; }
        public bool Empty { get; set; }
        public bool ReadOnly { get; set; }
        public bool Emphasized { get; set; }
        public Sprite PreviewSprite { get; set; }
        public ScenarioAuthoringInspectorAction PrimaryAction { get; set; }
        public ScenarioAuthoringInspectorAction QuantityIncreaseAction { get; set; }
        public ScenarioAuthoringInspectorAction QuantityDecreaseAction { get; set; }
        public ScenarioAuthoringInspectorAction RemoveAction { get; set; }
        public ScenarioAuthoringInspectorAction KindAction { get; set; }
        public ScenarioAuthoringInspectorAction[] TimeActions { get; set; }
    }

    internal sealed class ScenarioCastCardViewModel
    {
        public string Name { get; set; }
        public string RoleLine { get; set; }
        public string Status { get; set; }
        public string ArrivalSummary { get; set; }
        public bool CompactReference { get; set; }
        public Sprite PortraitSprite { get; set; }
        public Texture2D PortraitTexture { get; set; }
        public Color HairColor { get; set; }
        public Color SkinColor { get; set; }
        public Color ShirtColor { get; set; }
        public Color PantsColor { get; set; }
        public ScenarioCastStatViewModel[] Stats { get; set; }
        public string[] Traits { get; set; }
        public ScenarioAuthoringInspectorAction PrimaryAction { get; set; }
        public ScenarioAuthoringInspectorAction[] SecondaryActions { get; set; }
    }

    internal sealed class ScenarioCastStatViewModel
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public int Value { get; set; }
        public int Max { get; set; }
    }

    internal sealed class ScenarioSurvivorEditorViewModel
    {
        public ScenarioCastCardViewModel Portrait { get; set; }
        public ScenarioAuthoringInspectorAction NameAction { get; set; }
        public ScenarioAuthoringInspectorAction GenderAction { get; set; }
        public ScenarioAuthoringInspectorAction BodyAction { get; set; }
        public ScenarioSurvivorTextureRowViewModel[] TextureRows { get; set; }
        public ScenarioSurvivorColorRowViewModel[] ColorRows { get; set; }
        public ScenarioSurvivorStatRowViewModel[] StatRows { get; set; }
        public string SkillsLimitationText { get; set; }
        public ScenarioSurvivorTraitRowViewModel[] TraitRows { get; set; }
        public ScenarioSurvivorConditionRowViewModel[] ConditionRows { get; set; }
        public string[] UtilityDisclosureLines { get; set; }
        public ScenarioAuthoringInspectorAction[] UtilityActions { get; set; }
        public ScenarioAuthoringInspectorAction[] CloseActions { get; set; }
    }

    internal sealed class ScenarioSurvivorTextureRowViewModel
    {
        public string Label { get; set; }
        public string Detail { get; set; }
        public ScenarioAuthoringInspectorAction PreviousAction { get; set; }
        public ScenarioAuthoringInspectorAction NextAction { get; set; }
    }

    internal sealed class ScenarioSurvivorColorRowViewModel
    {
        public string Channel { get; set; }
        public string Label { get; set; }
        public string Hex { get; set; }
        public Color Color { get; set; }
        public ScenarioAuthoringInspectorAction PreviousAction { get; set; }
        public ScenarioAuthoringInspectorAction NextAction { get; set; }
        public string OpenColorPickerActionId { get; set; }
        public string ApplyColorActionPrefix { get; set; }
    }

    internal sealed class ScenarioSurvivorStatRowViewModel
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public int Value { get; set; }
        public int Min { get; set; }
        public int Max { get; set; }
        public string RangeText { get; set; }
        public ScenarioAuthoringInspectorAction DecreaseAction { get; set; }
        public ScenarioAuthoringInspectorAction IncreaseAction { get; set; }
        public ScenarioAuthoringInspectorAction TextAction { get; set; }
    }

    internal sealed class ScenarioSurvivorTraitRowViewModel
    {
        public string Kind { get; set; }
        public string Label { get; set; }
        public string Value { get; set; }
        public string PickerKey { get; set; }
        public ScenarioAuthoringInspectorAction PreviousAction { get; set; }
        public ScenarioAuthoringInspectorAction NextAction { get; set; }
        public ScenarioAuthoringInspectorAction PickerAction { get; set; }
        public ScenarioSurvivorTraitOptionViewModel[] Options { get; set; }
    }

    internal sealed class ScenarioSurvivorTraitOptionViewModel
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public string Description { get; set; }
        public ScenarioAuthoringInspectorAction SelectAction { get; set; }
    }

    internal sealed class ScenarioSurvivorConditionRowViewModel
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public int Value { get; set; }
        public int Min { get; set; }
        public int Max { get; set; }
        public string RangeText { get; set; }
        public string HelpText { get; set; }
        public ScenarioAuthoringInspectorAction DecreaseAction { get; set; }
        public ScenarioAuthoringInspectorAction IncreaseAction { get; set; }
        public ScenarioAuthoringInspectorAction TextAction { get; set; }
    }

    internal enum ScenarioSurvivorModFieldControlKind
    {
        Notice = 0,
        Toggle = 1,
        Stepper = 2,
        Text = 3,
        Enum = 4,
        Color = 5
    }

    internal sealed class ScenarioSurvivorModFieldRowViewModel
    {
        public ScenarioSurvivorModFieldControlKind Kind { get; set; }
        public string Label { get; set; }
        public string ValueText { get; set; }
        public string HelpText { get; set; }
        public string Badge { get; set; }
        public bool Emphasized { get; set; }
        public ScenarioAuthoringInspectorAction ToggleAction { get; set; }
        public ScenarioAuthoringInspectorAction DecreaseAction { get; set; }
        public ScenarioAuthoringInspectorAction IncreaseAction { get; set; }
        public ScenarioAuthoringInspectorAction CycleAction { get; set; }
        public ScenarioAuthoringInspectorAction TextAction { get; set; }
        public ScenarioSurvivorColorRowViewModel ColorRow { get; set; }
    }

    internal sealed class ScenarioAuthoringTargetContext
    {
        public Camera Camera { get; set; }
        public Ray Ray { get; set; }
        public RaycastHit Hit { get; set; }
        public Collider Collider { get; set; }
        public GameObject GameObject { get; set; }
        public Vector3 WorldPoint { get; set; }
    }

    internal interface IScenarioAuthoringTargetAdapter
    {
        string AdapterId { get; }
        int Priority { get; }
        bool TryCreateTarget(ScenarioAuthoringTargetContext context, out ScenarioAuthoringTarget target);
    }

    internal sealed class ScenarioAuthoringPresentationSnapshot
    {
        public ScenarioAuthoringState State { get; set; }
        public ScenarioAuthoringShellViewModel ShellViewModel { get; set; }
        public ScenarioAuthoringInspectorDocument ShellDocument { get; set; }
        public ScenarioAuthoringInspectorDocument InspectorDocument { get; set; }
        public ScenarioAuthoringInspectorDocument HoverDocument { get; set; }
    }

    internal interface IScenarioAuthoringRenderModule
    {
        string ModuleId { get; }
        int Priority { get; }
        bool CanRender();
        void Render(ScenarioAuthoringPresentationSnapshot snapshot);
        void Hide();
    }
}
