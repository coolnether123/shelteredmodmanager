using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Infrastructure.Serialization;
namespace ShelteredAPI.Scenarios.Application.Authoring{
    // Snapshot-based undo/redo for asset-reference edits. The authoring service
    // asks for a snapshot before any mutation; the history service records it and
    // exposes Undo/Redo that restore the SpriteSwaps list on the working definition.
    //
    // Scope is intentionally limited to sprite swaps Ã¢â‚¬â€ extending to other dirty
    // sections is a question of adding more SectionSnapshot kinds here.
    internal sealed class ScenarioAuthoringHistoryService
    {
        private const int MaxDepth = 64;

        private readonly Stack<DefinitionSnapshot> _undo = new Stack<DefinitionSnapshot>();
        private readonly Stack<DefinitionSnapshot> _redo = new Stack<DefinitionSnapshot>();
        private string _boundDraftId;

        public static ScenarioAuthoringHistoryService Instance
        {
            get { return ScenarioCompositionRoot.Resolve<ScenarioAuthoringHistoryService>(); }
        }

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
                Definition = ScenarioDefinitionCloner.Clone(definition),
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
            description = null;
            dirtySection = ScenarioDirtySection.None;
            editCategory = ScenarioEditCategory.Bunker;
            if (definition == null || _undo.Count == 0)
                return false;

            DefinitionSnapshot redoPoint = new DefinitionSnapshot
            {
                Description = "Redo",
                Definition = ScenarioDefinitionCloner.Clone(definition)
            };

            DefinitionSnapshot snapshot = _undo.Pop();
            RestoreDefinition(definition, snapshot.Definition);
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
            description = null;
            dirtySection = ScenarioDirtySection.None;
            editCategory = ScenarioEditCategory.Bunker;
            if (definition == null || _redo.Count == 0)
                return false;

            DefinitionSnapshot undoPoint = new DefinitionSnapshot
            {
                Description = "Undo",
                Definition = ScenarioDefinitionCloner.Clone(definition)
            };

            DefinitionSnapshot snapshot = _redo.Pop();
            RestoreDefinition(definition, snapshot.Definition);
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

            ScenarioDefinition restored = ScenarioDefinitionCloner.Clone(snapshot);
            if (restored == null)
                return;

            destination.Id = restored.Id;
            destination.DisplayName = restored.DisplayName;
            destination.Description = restored.Description;
            destination.Author = restored.Author;
            destination.Version = restored.Version;
            destination.BaseGameMode = restored.BaseGameMode;
            destination.SeedOverride = restored.SeedOverride;
            destination.Dependencies.Clear();
            if (restored.Dependencies != null)
            {
                for (int i = 0; i < restored.Dependencies.Count; i++)
                    destination.Dependencies.Add(restored.Dependencies[i]);
            }
            destination.FamilySetup = restored.FamilySetup;
            destination.StartingInventory = restored.StartingInventory;
            destination.BunkerEdits = restored.BunkerEdits;
            destination.TriggersAndEvents = restored.TriggersAndEvents;
            destination.Quests = restored.Quests;
            destination.Map = restored.Map;
            destination.WinLossConditions = restored.WinLossConditions;
            destination.AssetReferences = restored.AssetReferences;
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
