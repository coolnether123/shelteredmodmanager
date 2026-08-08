using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Definitions;
namespace ShelteredScenarioEditor.Application.Authoring{
    // Snapshot-based undo/redo for asset-reference edits. The authoring service
    // asks for a snapshot before any mutation; the history service records it and
    // exposes Undo/Redo that restore the SpriteSwaps list on the working definition.
    //
    // Scope is intentionally limited to sprite swaps -- extending to other dirty
    // sections is a question of adding more SectionSnapshot kinds here.
    internal sealed class ScenarioAuthoringHistoryService
    {
        private const int MaxDepth = 64;

        private readonly Stack<DefinitionSnapshot> _undo = new Stack<DefinitionSnapshot>();
        private readonly Stack<DefinitionSnapshot> _redo = new Stack<DefinitionSnapshot>();
        private string _boundDraftId;

        internal ScenarioAuthoringHistoryService()
        {
        }

        public bool CanUndo
        {
            get { return _undo.Count > 0; }
        }

        public bool CanRedo
        {
            get { return _redo.Count > 0; }
        }

        public int UndoDepth
        {
            get { return _undo.Count; }
        }

        public int RedoDepth
        {
            get { return _redo.Count; }
        }

        public bool TryPeekUndo(out string description, out ScenarioEditCategory editCategory)
        {
            description = null;
            editCategory = ScenarioEditCategory.Bunker;
            if (_undo.Count == 0)
                return false;

            DefinitionSnapshot snapshot = _undo.Peek();
            description = snapshot.Description;
            editCategory = snapshot.EditCategory;
            return true;
        }

        public bool TryPeekRedo(out string description, out ScenarioEditCategory editCategory)
        {
            description = null;
            editCategory = ScenarioEditCategory.Bunker;
            if (_redo.Count == 0)
                return false;

            DefinitionSnapshot snapshot = _redo.Peek();
            description = snapshot.Description;
            editCategory = snapshot.EditCategory;
            return true;
        }

        // Call when the active authoring draft changes so stale snapshots don't leak
        // across sessions.
        public void BindSession(string draftId)
        {
            if (string.Equals(_boundDraftId, draftId, StringComparison.Ordinal))
                return;

            _boundDraftId = draftId;
            _undo.Clear();
            _redo.Clear();
        }

        public void Reset()
        {
            _boundDraftId = null;
            _undo.Clear();
            _redo.Clear();
        }

        // Capture before mutating. A new user action invalidates the redo stack.
        public void RecordVisualChange(ScenarioDefinition definition, string description)
        {
            RecordAuthoringChange(definition, description, ScenarioDirtySection.Assets, ScenarioEditCategory.Assets);
        }

        public void RecordBunkerChange(ScenarioDefinition definition, string description)
        {
            RecordAuthoringChange(definition, description, ScenarioDirtySection.Bunker, ScenarioEditCategory.Bunker);
        }

        public void RecordAuthoringChange(
            ScenarioDefinition definition,
            string description,
            ScenarioDirtySection dirtySection,
            ScenarioEditCategory editCategory)
        {
            if (definition == null)
                return;

            DefinitionSnapshot snapshot = new DefinitionSnapshot
            {
                Description = description,
                Definition = ShelteredScenarioEditor.Application.Runtime.ScenarioEditorDefinitionCloner.Clone(definition),
                DirtySection = dirtySection,
                EditCategory = editCategory
            };
            PushUndo(snapshot);
            _redo.Clear();
        }

        public bool Undo(ScenarioDefinition definition, out string description)
        {
            ScenarioDirtySection dirtySection;
            ScenarioEditCategory editCategory;
            return Undo(definition, out description, out dirtySection, out editCategory);
        }

        public bool Undo(
            ScenarioDefinition definition,
            out string description,
            out ScenarioDirtySection dirtySection,
            out ScenarioEditCategory editCategory)
        {
            return Undo(definition, null, out description, out dirtySection, out editCategory);
        }

        public bool Undo(
            ScenarioDefinition definition,
            ScenarioEditCategory[] allowedCategories,
            out string description,
            out ScenarioDirtySection dirtySection,
            out ScenarioEditCategory editCategory)
        {
            description = null;
            dirtySection = ScenarioDirtySection.None;
            editCategory = ScenarioEditCategory.Bunker;
            if (definition == null || _undo.Count == 0)
                return false;

            if (!IsCategoryAllowed(_undo.Peek().EditCategory, allowedCategories))
                return false;

            DefinitionSnapshot redoPoint = new DefinitionSnapshot
            {
                Description = "Redo",
                Definition = ShelteredScenarioEditor.Application.Runtime.ScenarioEditorDefinitionCloner.Clone(definition)
            };

            DefinitionSnapshot snapshot = _undo.Pop();
            RestoreDefinition(definition, snapshot.Definition);
            redoPoint.Description = snapshot.Description;
            redoPoint.DirtySection = snapshot.DirtySection;
            redoPoint.EditCategory = snapshot.EditCategory;
            _redo.Push(redoPoint);
            description = snapshot.Description;
            dirtySection = snapshot.DirtySection;
            editCategory = snapshot.EditCategory;
            MMLog.WriteInfo("[ScenarioAuthoringHistory] Undo: " + (description ?? "<unnamed>")
                + " | undoDepth=" + _undo.Count + " redoDepth=" + _redo.Count);
            return true;
        }

        public bool Redo(ScenarioDefinition definition, out string description)
        {
            ScenarioDirtySection dirtySection;
            ScenarioEditCategory editCategory;
            return Redo(definition, out description, out dirtySection, out editCategory);
        }

        public bool Redo(
            ScenarioDefinition definition,
            out string description,
            out ScenarioDirtySection dirtySection,
            out ScenarioEditCategory editCategory)
        {
            return Redo(definition, null, out description, out dirtySection, out editCategory);
        }

        public bool Redo(
            ScenarioDefinition definition,
            ScenarioEditCategory[] allowedCategories,
            out string description,
            out ScenarioDirtySection dirtySection,
            out ScenarioEditCategory editCategory)
        {
            description = null;
            dirtySection = ScenarioDirtySection.None;
            editCategory = ScenarioEditCategory.Bunker;
            if (definition == null || _redo.Count == 0)
                return false;

            if (!IsCategoryAllowed(_redo.Peek().EditCategory, allowedCategories))
                return false;

            DefinitionSnapshot undoPoint = new DefinitionSnapshot
            {
                Description = "Undo",
                Definition = ShelteredScenarioEditor.Application.Runtime.ScenarioEditorDefinitionCloner.Clone(definition)
            };

            DefinitionSnapshot snapshot = _redo.Pop();
            RestoreDefinition(definition, snapshot.Definition);
            undoPoint.Description = snapshot.Description;
            undoPoint.DirtySection = snapshot.DirtySection;
            undoPoint.EditCategory = snapshot.EditCategory;
            PushUndo(undoPoint);
            description = snapshot.Description;
            dirtySection = snapshot.DirtySection;
            editCategory = snapshot.EditCategory;
            MMLog.WriteInfo("[ScenarioAuthoringHistory] Redo: " + (description ?? "<unnamed>")
                + " | undoDepth=" + _undo.Count + " redoDepth=" + _redo.Count);
            return true;
        }

        private static bool IsCategoryAllowed(ScenarioEditCategory editCategory, ScenarioEditCategory[] allowedCategories)
        {
            if (allowedCategories == null || allowedCategories.Length == 0)
                return true;

            for (int i = 0; i < allowedCategories.Length; i++)
            {
                if (allowedCategories[i] == editCategory)
                    return true;
            }

            return false;
        }

        private void PushUndo(DefinitionSnapshot snapshot)
        {
            _undo.Push(snapshot);
            if (_undo.Count <= MaxDepth)
                return;

            DefinitionSnapshot[] keep = _undo.ToArray();
            _undo.Clear();
            for (int i = keep.Length - 2; i >= 0; i--)
                _undo.Push(keep[i]);
        }

        private static void RestoreDefinition(ScenarioDefinition destination, ScenarioDefinition snapshot)
        {
            if (destination == null || snapshot == null)
                return;

            ScenarioDefinition restored = ShelteredScenarioEditor.Application.Runtime.ScenarioEditorDefinitionCloner.Clone(snapshot);
            if (restored == null)
                return;

            destination.Id = restored.Id;
            destination.DisplayName = restored.DisplayName;
            destination.Description = restored.Description;
            destination.Goal = restored.Goal;
            destination.Author = restored.Author;
            destination.Version = restored.Version;
            destination.Credits = restored.Credits;
            destination.Tags.Clear();
            if (restored.Tags != null)
            {
                for (int i = 0; i < restored.Tags.Count; i++)
                    destination.Tags.Add(restored.Tags[i]);
            }
            destination.BaseGameMode = restored.BaseGameMode;
            destination.BaseFamilyChoice = restored.BaseFamilyChoice;
            destination.SeedOverride = restored.SeedOverride;
            destination.SelectionRules = restored.SelectionRules;
            destination.ScenarioCharacters.Clear();
            if (restored.ScenarioCharacters != null)
            {
                for (int i = 0; i < restored.ScenarioCharacters.Count; i++)
                    destination.ScenarioCharacters.Add(restored.ScenarioCharacters[i]);
            }
            destination.ScenarioFlow = restored.ScenarioFlow;
            destination.Dependencies.Clear();
            if (restored.Dependencies != null)
            {
                for (int i = 0; i < restored.Dependencies.Count; i++)
                    destination.Dependencies.Add(restored.Dependencies[i]);
            }
            destination.ModDependencies.Clear();
            if (restored.ModDependencies != null)
            {
                for (int i = 0; i < restored.ModDependencies.Count; i++)
                    destination.ModDependencies.Add(restored.ModDependencies[i]);
            }
            destination.FamilySetup = restored.FamilySetup;
            destination.LaunchSetup = restored.LaunchSetup;
            destination.StartingInventory = restored.StartingInventory;
            destination.BunkerEdits = restored.BunkerEdits;
            destination.BunkerGrid = restored.BunkerGrid;
            destination.BackendWorlds = restored.BackendWorlds;
            destination.TriggersAndEvents = restored.TriggersAndEvents;
            destination.Quests = restored.Quests;
            destination.Map = restored.Map;
            destination.WinLossConditions = restored.WinLossConditions;
            destination.Scoring = restored.Scoring;
            destination.AssetReferences = restored.AssetReferences;
            destination.Gates.Clear();
            if (restored.Gates != null)
            {
                for (int i = 0; i < restored.Gates.Count; i++)
                    destination.Gates.Add(restored.Gates[i]);
            }
            destination.ScheduledActions.Clear();
            if (restored.ScheduledActions != null)
            {
                for (int i = 0; i < restored.ScheduledActions.Count; i++)
                    destination.ScheduledActions.Add(restored.ScheduledActions[i]);
            }
            destination.Journal = restored.Journal;
            destination.Conversations = restored.Conversations;
            destination.VanillaSuppression = restored.VanillaSuppression;
        }

        private sealed class DefinitionSnapshot
        {
            public string Description;
            public ScenarioDefinition Definition;
            public ScenarioDirtySection DirtySection;
            public ScenarioEditCategory EditCategory;
        }
    }
}
