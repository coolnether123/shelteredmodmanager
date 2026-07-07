using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Scenarios;
using UnityEngine;

using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Application.Bunker;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Infrastructure.Assets;
using ShelteredAPI.Scenarios.Infrastructure.Runtime;
using ShelteredAPI.Scenarios.Shared;
namespace ShelteredAPI.Scenarios.Application.Authoring{
    internal sealed class ScenarioAuthoringCaptureService
    {
        private readonly IScenarioDraftMutationService _draftMutationService;
        private readonly ScenarioActorResolver _actorResolver;

        public static ScenarioAuthoringCaptureService Instance
        {
            get { return ScenarioCompositionRoot.Resolve<ScenarioAuthoringCaptureService>(); }
        }

        internal ScenarioAuthoringCaptureService(
            IScenarioDraftMutationService draftMutationService,
            ScenarioActorResolver actorResolver)
        {
            _draftMutationService = draftMutationService;
            _actorResolver = actorResolver;
        }

        public bool CaptureCurrentFamily(ScenarioEditorSession session, out string message)
        {
            message = null;
            if (session == null || session.WorkingDefinition == null)
            {
                message = "No active authoring session is available.";
                return false;
            }

            FamilyManager familyManager = FamilyManager.Instance;
            if (familyManager == null)
            {
                message = "FamilyManager is not ready; family capture skipped.";
                return false;
            }

            List<FamilyMember> liveMembers = familyManager.GetAllFamilyMembers();
            if (liveMembers == null || liveMembers.Count == 0)
            {
                message = "No live family members were available to capture.";
                return false;
            }

            RecordUndo(session, "Capture survivors from world");
            FamilySetupDefinition familySetup = session.WorkingDefinition.FamilySetup ?? new FamilySetupDefinition();
            familySetup.OverrideVanillaFamily = true;
            familySetup.Members.Clear();

            int captured = 0;
            for (int i = 0; i < liveMembers.Count; i++)
            {
                FamilyMember member = liveMembers[i];
                if (member == null)
                    continue;

                FamilyMemberConfig config = new FamilyMemberConfig();
                CaptureLiveFamilyMember(member, config);
                if (_actorResolver != null)
                    config.ActorRef = _actorResolver.CreateLiveFamilyMemberRef(member);

                familySetup.Members.Add(config);
                captured++;
            }

            session.WorkingDefinition.FamilySetup = familySetup;
            MarkCaptured(session, ScenarioDirtySection.Family, ScenarioEditCategory.Family);
            message = "Captured " + captured + " survivors from the world.";
            MMLog.WriteInfo("[ScenarioAuthoringCapture] " + message);
            return true;
        }

        public bool BuildFamilyCapturePreview(ScenarioEditorSession session, out ScenarioCapturePreview preview, out string message)
        {
            preview = null;
            message = null;
            if (session == null || session.WorkingDefinition == null)
            {
                message = "No active authoring session is available.";
                return false;
            }

            FamilyManager familyManager = FamilyManager.Instance;
            List<FamilyMember> liveMembers = familyManager != null ? familyManager.GetAllFamilyMembers() : null;
            if (liveMembers == null || liveMembers.Count == 0)
            {
                message = familyManager == null
                    ? "FamilyManager is not ready; family capture preview is unavailable."
                    : "No live family members were available to capture.";
                return false;
            }

            List<FamilyMemberConfig> captured = new List<FamilyMemberConfig>();
            for (int i = 0; i < liveMembers.Count; i++)
            {
                FamilyMember member = liveMembers[i];
                if (member == null)
                    continue;

                FamilyMemberConfig config = new FamilyMemberConfig();
                CaptureLiveFamilyMember(member, config);
                if (_actorResolver != null)
                    config.ActorRef = _actorResolver.CreateLiveFamilyMemberRef(member);
                captured.Add(config);
            }

            FamilySetupDefinition family = session.WorkingDefinition.FamilySetup;
            List<FamilyMemberConfig> current = family != null ? family.Members : null;
            preview = ScenarioCapturePreview.Create("family", "Starting Survivors", captured.Count, 0);
            AddFamilyDiffLines(preview, current, captured);
            return true;
        }

        public bool CaptureCurrentInventory(ScenarioEditorSession session, out string message)
        {
            message = null;
            if (session == null || session.WorkingDefinition == null)
            {
                message = "No active authoring session is available.";
                return false;
            }

            InventoryManager inventoryManager = InventoryManager.Instance;
            if (inventoryManager == null)
            {
                message = "InventoryManager is not ready; inventory capture skipped.";
                return false;
            }

            List<ItemStack> liveStacks = inventoryManager.GetItems();
            RecordUndo(session, "Capture stockpile from world");
            StartingInventoryDefinition inventory = session.WorkingDefinition.StartingInventory ?? new StartingInventoryDefinition();
            inventory.OverrideRandomStart = true;
            inventory.Items.Clear();

            int totalItems = 0;
            List<ItemEntry> capturedItems = BuildLiveInventoryEntries(liveStacks, out totalItems);
            for (int i = 0; i < capturedItems.Count; i++)
                inventory.Items.Add(capturedItems[i]);

            session.WorkingDefinition.StartingInventory = inventory;
            MarkCaptured(session, ScenarioDirtySection.Inventory, ScenarioEditCategory.Inventory);
            message = "Captured " + capturedItems.Count + " stockpile stack(s) from the world (" + totalItems + " total item(s)).";
            MMLog.WriteInfo("[ScenarioAuthoringCapture] " + message);
            return true;
        }

        public bool BuildInventoryCapturePreview(ScenarioEditorSession session, out ScenarioCapturePreview preview, out string message)
        {
            preview = null;
            message = null;
            if (session == null || session.WorkingDefinition == null)
            {
                message = "No active authoring session is available.";
                return false;
            }

            InventoryManager inventoryManager = InventoryManager.Instance;
            if (inventoryManager == null)
            {
                message = "InventoryManager is not ready; inventory capture preview is unavailable.";
                return false;
            }

            int totalItems = 0;
            List<ItemEntry> captured = BuildLiveInventoryEntries(inventoryManager.GetItems(), out totalItems);
            StartingInventoryDefinition inventory = session.WorkingDefinition.StartingInventory;
            List<ItemEntry> current = inventory != null ? inventory.Items : null;
            preview = ScenarioCapturePreview.Create("inventory", "Starting Stockpile", captured.Count, totalItems);
            AddInventoryDiffLines(preview, current, captured);
            return true;
        }

        public bool CaptureCurrentShelterObjects(ScenarioEditorSession session, out string message)
        {
            message = null;
            if (session == null || session.WorkingDefinition == null)
            {
                message = "No active authoring session is available.";
                return false;
            }

            ObjectManager objectManager = ObjectManager.Instance;
            if (objectManager == null)
            {
                message = "ObjectManager is not ready; shelter object capture skipped.";
                return false;
            }

            BunkerEditsDefinition bunkerEdits;
            if (!_draftMutationService.TryEnsureBunkerEdits(out bunkerEdits))
            {
                message = "No active scenario draft is available for shelter object capture.";
                return false;
            }

            List<ObjectPlacement> preserved = new List<ObjectPlacement>();
            for (int i = 0; i < bunkerEdits.ObjectPlacements.Count; i++)
            {
                ObjectPlacement existing = bunkerEdits.ObjectPlacements[i];
                if (ScenarioBunkerDraftService.ShouldPreserveDuringLiveCapture(existing))
                    preserved.Add(existing);
            }

            bunkerEdits.ObjectPlacements.Clear();

            List<Obj_Base> liveObjects = objectManager.GetAllObjects();
            List<ObjectPlacement> captured = new List<ObjectPlacement>(preserved);
            for (int i = 0; liveObjects != null && i < liveObjects.Count; i++)
            {
                Obj_Base obj = liveObjects[i];
                if (!ShouldCaptureObject(obj))
                    continue;

                captured.Add(ScenarioBunkerDraftService.CreatePlacement(obj));
            }

            captured.Sort(ComparePlacements);
            for (int i = 0; i < captured.Count; i++)
                bunkerEdits.ObjectPlacements.Add(captured[i]);

            session.WorkingDefinition.BunkerEdits = bunkerEdits;
            _draftMutationService.MarkDirty(ScenarioDirtySection.Bunker, ScenarioEditCategory.Bunker);
            int liveCapturedCount = Math.Max(0, captured.Count - preserved.Count);
            message = captured.Count > 0
                ? "Captured " + liveCapturedCount + " live spawned shelter object placement(s)."
                : "No eligible spawned shelter objects were found; captured placement list cleared.";
            MMLog.WriteInfo("[ScenarioAuthoringCapture] " + message);
            return true;
        }

        public bool CaptureSelectedObject(ScenarioEditorSession session, ScenarioAuthoringTarget target, out string message)
        {
            message = null;
            if (session == null || session.WorkingDefinition == null)
            {
                message = "No active authoring session is available.";
                return false;
            }

            Obj_Base obj;
            string blockingReason;
            if (!TryResolveCapturableObject(target, out obj, out blockingReason))
            {
                message = blockingReason;
                return false;
            }

            BunkerEditsDefinition bunkerEdits;
            if (!_draftMutationService.TryEnsureBunkerEdits(out bunkerEdits))
            {
                message = "No active scenario draft is available for selected-object capture.";
                return false;
            }

            ObjectPlacement placement = ScenarioBunkerDraftService.CreatePlacement(obj);
            int existingIndex = ScenarioBunkerDraftService.FindPlacementIndex(bunkerEdits.ObjectPlacements, obj);
            if (existingIndex >= 0)
            {
                bunkerEdits.ObjectPlacements[existingIndex] = placement;
                message = "Updated captured placement for '" + SafeObjectName(obj) + "'.";
            }
            else
            {
                bunkerEdits.ObjectPlacements.Add(placement);
                message = "Captured selected shelter object '" + SafeObjectName(obj) + "'.";
            }

            bunkerEdits.ObjectPlacements.Sort(ComparePlacements);
            _draftMutationService.MarkDirty(ScenarioDirtySection.Bunker, ScenarioEditCategory.Bunker);
            MMLog.WriteInfo("[ScenarioAuthoringCapture] " + message);
            return true;
        }

        public bool RemoveSelectedObjectPlacement(ScenarioEditorSession session, ScenarioAuthoringTarget target, out string message)
        {
            message = null;
            if (session == null || session.WorkingDefinition == null)
            {
                message = "No captured shelter object placements are available.";
                return false;
            }

            BunkerEditsDefinition bunkerEdits;
            if (!_draftMutationService.TryEnsureBunkerEdits(out bunkerEdits) || bunkerEdits == null || bunkerEdits.ObjectPlacements == null)
            {
                message = "No active scenario draft is available for selected-object placement removal.";
                return false;
            }

            string displayName;
            int beforeCount = bunkerEdits.ObjectPlacements.Count;
            int index = FindPlacementIndexForTarget(bunkerEdits.ObjectPlacements, target, out displayName);
            if (index < 0)
            {
                message = "The selected object does not have a captured scenario placement.";
                return false;
            }

            ObjectPlacement matchedPlacement = bunkerEdits.ObjectPlacements[index];
            string placementName = FormatPlacementName(matchedPlacement, displayName);
            bool removed = _draftMutationService.TryRemovePlacement(delegate(ObjectPlacement placement)
            {
                return ReferenceEquals(placement, matchedPlacement) || PlacementMatchesTarget(placement, target);
            });

            int afterCount = bunkerEdits.ObjectPlacements.Count;
            if (!removed || afterCount >= beforeCount)
            {
                message = "No matching draft placement was removed for '" + Safe(placementName) + "'.";
                MMLog.WriteWarning("[ScenarioAuthoringCapture] " + message + " before=" + beforeCount + " after=" + afterCount + ".");
                return false;
            }

            message = "Removed placement '" + Safe(placementName) + "' from the scenario draft.";
            MMLog.WriteInfo("[ScenarioAuthoringCapture] " + message);
            return true;
        }

        public bool CanCaptureTarget(ScenarioAuthoringTarget target, out string reason)
        {
            Obj_Base obj;
            return TryResolveCapturableObject(target, out obj, out reason);
        }

        public bool HasCapturedPlacementForTarget(ScenarioEditorSession session, ScenarioAuthoringTarget target)
        {
            if (session == null || session.WorkingDefinition == null || session.WorkingDefinition.BunkerEdits == null)
                return false;

            string ignored;
            return FindPlacementIndexForTarget(session.WorkingDefinition.BunkerEdits.ObjectPlacements, target, out ignored) >= 0;
        }

        private static void CaptureStats(FamilyMember member, FamilyMemberConfig config)
        {
            if (member == null || config == null || member.BaseStats == null)
                return;

            for (int statIndex = 0; statIndex < (int)BaseStats.StatType.Max; statIndex++)
            {
                BaseStats.StatType statType = (BaseStats.StatType)statIndex;
                BaseStat stat = member.BaseStats.GetStatByEnum(statType);
                if (stat == null)
                    continue;

                config.Stats.Add(new StatOverride
                {
                    StatId = statType.ToString(),
                    Value = stat.Level
                });
            }
        }

        private static void CaptureTraits(FamilyMember member, FamilyMemberConfig config)
        {
            if (member == null || config == null || member.traits == null)
                return;

            List<Traits.Strength> strengths = member.traits.GetStrengths(false);
            for (int i = 0; strengths != null && i < strengths.Count; i++)
                config.Traits.Add("Strength:" + strengths[i]);

            List<Traits.Weakness> weaknesses = member.traits.GetWeaknesses(false);
            for (int i = 0; weaknesses != null && i < weaknesses.Count; i++)
                config.Traits.Add("Weakness:" + weaknesses[i]);
        }

        private static void CaptureLiveFamilyMember(FamilyMember member, FamilyMemberConfig config)
        {
            if (member == null || config == null)
                return;

            config.Name = member.firstName;
            config.Gender = member.isMale ? ScenarioGender.Male : ScenarioGender.Female;
            CaptureStats(member, config);
            CaptureTraits(member, config);
            ScenarioCharacterAppearanceService.CaptureAppearance(member, config);
        }

        private static List<ItemEntry> BuildLiveInventoryEntries(List<ItemStack> liveStacks, out int totalItems)
        {
            totalItems = 0;
            List<ItemEntry> capturedItems = new List<ItemEntry>();
            for (int i = 0; liveStacks != null && i < liveStacks.Count; i++)
            {
                ItemStack stack = liveStacks[i];
                if (stack == null || stack.m_type == ItemManager.ItemType.Undefined || stack.m_count <= 0)
                    continue;

                capturedItems.Add(new ItemEntry
                {
                    ItemId = stack.m_type.ToString(),
                    Quantity = stack.m_count
                });
                totalItems += stack.m_count;
            }

            capturedItems.Sort(CompareItemEntries);
            return capturedItems;
        }

        private static void AddFamilyDiffLines(ScenarioCapturePreview preview, List<FamilyMemberConfig> current, List<FamilyMemberConfig> captured)
        {
            bool[] matchedCurrent = new bool[current != null ? current.Count : 0];
            for (int i = 0; captured != null && i < captured.Count; i++)
            {
                FamilyMemberConfig next = captured[i];
                int existing = FindFamilyByActorRef(current, next != null ? next.ActorRef : null, matchedCurrent);
                if (existing < 0)
                    existing = FindFamilyByName(current, next != null ? next.Name : null, matchedCurrent);
                if (existing < 0)
                {
                    preview.AddAdd("Add " + Safe(next != null ? next.Name : null) + " (" + FormatFamilyPreview(next) + ")");
                    continue;
                }

                matchedCurrent[existing] = true;
                FamilyMemberConfig previous = current[existing];
                if (!string.Equals(FormatFamilyPreview(previous), FormatFamilyPreview(next), StringComparison.Ordinal))
                    preview.AddChange("Change " + Safe(next != null ? next.Name : null) + " from " + FormatFamilyPreview(previous) + " to " + FormatFamilyPreview(next));
            }

            for (int i = 0; current != null && i < current.Count; i++)
            {
                if (!matchedCurrent[i])
                    preview.AddRemoval("Remove authored survivor " + Safe(current[i] != null ? current[i].Name : null));
            }
        }

        private static void AddInventoryDiffLines(ScenarioCapturePreview preview, List<ItemEntry> current, List<ItemEntry> captured)
        {
            bool[] matchedCurrent = new bool[current != null ? current.Count : 0];
            for (int i = 0; captured != null && i < captured.Count; i++)
            {
                ItemEntry next = captured[i];
                int existing = FindItem(current, next != null ? next.ItemId : null, matchedCurrent);
                if (existing < 0)
                {
                    preview.AddAdd("Add " + Safe(next != null ? next.ItemId : null) + " x" + (next != null ? next.Quantity.ToString() : "0"));
                    continue;
                }

                matchedCurrent[existing] = true;
                ItemEntry previous = current[existing];
                if (previous != null && next != null && previous.Quantity != next.Quantity)
                    preview.AddChange("Change " + Safe(next.ItemId) + " from x" + previous.Quantity.ToString() + " to x" + next.Quantity.ToString());
            }

            for (int i = 0; current != null && i < current.Count; i++)
            {
                if (!matchedCurrent[i])
                    preview.AddRemoval("Remove authored stockpile item " + Safe(current[i] != null ? current[i].ItemId : null));
            }
        }

        private static int FindFamilyByName(List<FamilyMemberConfig> members, string name, bool[] excluded)
        {
            for (int i = 0; members != null && i < members.Count; i++)
            {
                if (excluded != null && i < excluded.Length && excluded[i])
                    continue;
                FamilyMemberConfig member = members[i];
                if (member != null && string.Equals(member.Name ?? string.Empty, name ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        private static int FindFamilyByActorRef(List<FamilyMemberConfig> members, ScenarioActorRef actorRef, bool[] excluded)
        {
            if (actorRef == null)
                return -1;

            for (int i = 0; members != null && i < members.Count; i++)
            {
                if (excluded != null && i < excluded.Length && excluded[i])
                    continue;
                FamilyMemberConfig member = members[i];
                if (member != null && SameActorRef(member.ActorRef, actorRef))
                    return i;
            }
            return -1;
        }

        private static bool SameActorRef(ScenarioActorRef left, ScenarioActorRef right)
        {
            if (left == null || right == null)
                return false;
            if (!string.IsNullOrEmpty(left.BindingType)
                && !string.IsNullOrEmpty(left.BindingKey)
                && string.Equals(left.BindingType, right.BindingType, StringComparison.OrdinalIgnoreCase)
                && string.Equals(left.BindingKey, right.BindingKey, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(left.Kind, right.Kind, StringComparison.OrdinalIgnoreCase)
                && left.LocalId == right.LocalId
                && string.Equals(left.Domain ?? string.Empty, right.Domain ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static int FindItem(List<ItemEntry> items, string itemId, bool[] excluded)
        {
            for (int i = 0; items != null && i < items.Count; i++)
            {
                if (excluded != null && i < excluded.Length && excluded[i])
                    continue;
                ItemEntry item = items[i];
                if (item != null && string.Equals(item.ItemId ?? string.Empty, itemId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        private static string FormatFamilyPreview(FamilyMemberConfig member)
        {
            if (member == null)
                return "empty";

            string body = member.Gender.ToString();
            int strength = FindStat(member, "Strength");
            int dexterity = FindStat(member, "Dexterity");
            int intelligence = FindStat(member, "Intelligence");
            return body + ", Str " + strength.ToString() + ", Dex " + dexterity.ToString() + ", Int " + intelligence.ToString()
                + ", " + FindTrait(member, "Strength:") + "/" + FindTrait(member, "Weakness:");
        }

        private static int FindStat(FamilyMemberConfig member, string statId)
        {
            for (int i = 0; member != null && member.Stats != null && i < member.Stats.Count; i++)
            {
                StatOverride stat = member.Stats[i];
                if (stat != null && string.Equals(stat.StatId, statId, StringComparison.OrdinalIgnoreCase))
                    return stat.Value;
            }
            return 0;
        }

        private static string FindTrait(FamilyMemberConfig member, string prefix)
        {
            for (int i = 0; member != null && member.Traits != null && i < member.Traits.Count; i++)
            {
                string trait = member.Traits[i];
                if (!string.IsNullOrEmpty(trait) && trait.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return trait.Substring(prefix.Length);
            }
            return "none";
        }

        private static void RecordUndo(ScenarioEditorSession session, string description)
        {
            ScenarioAuthoringHistoryService history = ScenarioAuthoringHistoryService.Instance;
            if (history != null && session != null)
                history.RecordVisualChange(session.WorkingDefinition, description);
        }

        private static string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "<none>" : value;
        }

        internal sealed class ScenarioCapturePreview
        {
            private readonly List<string> _lines = new List<string>();

            public string Kind { get; private set; }
            public string Title { get; private set; }
            public int SourceCount { get; private set; }
            public int TotalQuantity { get; private set; }
            public int Additions { get; private set; }
            public int Changes { get; private set; }
            public int Removals { get; private set; }
            public IList<string> Lines { get { return _lines; } }
            public bool HasChanges { get { return Additions > 0 || Changes > 0 || Removals > 0; } }

            public static ScenarioCapturePreview Create(string kind, string title, int sourceCount, int totalQuantity)
            {
                return new ScenarioCapturePreview
                {
                    Kind = kind,
                    Title = title,
                    SourceCount = sourceCount,
                    TotalQuantity = totalQuantity
                };
            }

            public void AddAdd(string line)
            {
                Additions++;
                _lines.Add(line);
            }

            public void AddChange(string line)
            {
                Changes++;
                _lines.Add(line);
            }

            public void AddRemoval(string line)
            {
                Removals++;
                _lines.Add(line);
            }
        }

        private static bool TryResolveCapturableObject(ScenarioAuthoringTarget target, out Obj_Base obj, out string reason)
        {
            obj = null;
            reason = null;
            if (target == null)
            {
                reason = "Select a live shelter object to capture it into the scenario.";
                return false;
            }

            GameObject gameObject = target.RuntimeObject as GameObject;
            if (gameObject == null)
            {
                Component component = target.RuntimeObject as Component;
                gameObject = component != null ? component.gameObject : null;
            }

            obj = gameObject != null ? gameObject.GetComponent<Obj_Base>() : null;
            if (obj == null)
            {
                reason = "The selected target is not a spawned shelter object.";
                return false;
            }

            if (obj.initialObject)
            {
                reason = "The selected object belongs to the bunker's initial layout. This first-pass editor only captures spawned shelter objects.";
                return false;
            }

            if (!ShouldCaptureObject(obj))
            {
                reason = "The selected object is not eligible for scenario shelter-object capture.";
                return false;
            }

            return true;
        }

        private static int FindPlacementIndexForTarget(List<ObjectPlacement> placements, ScenarioAuthoringTarget target, out string displayName)
        {
            displayName = target != null ? target.DisplayName : null;
            if (placements == null || target == null)
                return -1;

            Obj_Base obj = ResolveShelterObject(target);
            if (obj != null)
            {
                displayName = SafeObjectName(obj);
                int objIndex = ScenarioBunkerDraftService.FindPlacementIndex(placements, obj);
                if (objIndex >= 0)
                    return objIndex;
            }

            string reference = FirstNonEmpty(target.ScenarioReferenceId, target.Id);
            if (string.IsNullOrEmpty(reference))
                return -1;

            for (int i = 0; i < placements.Count; i++)
            {
                ObjectPlacement placement = placements[i];
                if (placement == null)
                    continue;

                if (PlacementMatchesReference(placement, reference))
                {
                    displayName = FormatPlacementName(placement, displayName);
                    return i;
                }
            }

            return -1;
        }

        private static bool PlacementMatchesTarget(ObjectPlacement placement, ScenarioAuthoringTarget target)
        {
            if (placement == null || target == null)
                return false;

            Obj_Base obj = ResolveShelterObject(target);
            if (obj != null)
                return ScenarioBunkerDraftService.MatchesPlacement(placement, obj);

            string reference = FirstNonEmpty(target.ScenarioReferenceId, target.Id);
            return PlacementMatchesReference(placement, reference);
        }

        private static bool PlacementMatchesReference(ObjectPlacement placement, string reference)
        {
            if (placement == null || string.IsNullOrEmpty(reference))
                return false;

            return StringEquals(placement.ScenarioObjectId, reference)
                || StringEquals(placement.RuntimeBindingKey, reference)
                || StringEquals(ScenarioPropertyBag.GetString(placement.CustomProperties, ScenarioPlacementDefinitions.PropertyAuthoringIdentity), reference);
        }

        private static string FormatPlacementName(ObjectPlacement placement, string fallback)
        {
            if (placement == null)
                return fallback;

            return FirstNonEmpty(
                fallback,
                ScenarioPropertyBag.GetString(placement.CustomProperties, ScenarioPlacementDefinitions.PropertyCapturedName),
                placement.ScenarioObjectId,
                placement.DefinitionReference,
                placement.PrefabReference);
        }

        private static Obj_Base ResolveShelterObject(ScenarioAuthoringTarget target)
        {
            if (target == null)
                return null;

            GameObject gameObject = target.RuntimeObject as GameObject;
            if (gameObject == null)
            {
                Component component = target.RuntimeObject as Component;
                gameObject = component != null ? component.gameObject : null;
            }

            return gameObject != null ? gameObject.GetComponent<Obj_Base>() : null;
        }

        private static bool StringEquals(string left, string right)
        {
            return !string.IsNullOrEmpty(left)
                && !string.IsNullOrEmpty(right)
                && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            for (int i = 0; values != null && i < values.Length; i++)
            {
                if (!string.IsNullOrEmpty(values[i]))
                    return values[i];
            }

            return null;
        }

        private static bool ShouldCaptureObject(Obj_Base obj)
        {
            if (obj == null || obj.initialObject || obj.gameObject == null || !obj.gameObject.activeInHierarchy)
                return false;

            ObjectManager.ObjectType objectType = obj.GetObjectType();
            if (objectType == ObjectManager.ObjectType.Undefined
                || objectType == ObjectManager.ObjectType.Max
                || objectType == ObjectManager.ObjectType.CatatonicGhost)
            {
                return false;
            }

            string typeName = objectType.ToString();
            if (ContainsAny(typeName, "Corpse", "Worm", "Ghost", "Fire", "Breach", "Raider", "Warning", "Smoke", "Explosion"))
                return false;

            return true;
        }

        private static bool ContainsAny(string value, params string[] parts)
        {
            if (string.IsNullOrEmpty(value) || parts == null)
                return false;

            for (int i = 0; i < parts.Length; i++)
            {
                if (!string.IsNullOrEmpty(parts[i]) && value.IndexOf(parts[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static void MarkCaptured(
            ScenarioEditorSession session,
            ScenarioDirtySection dirtySection,
            ScenarioEditCategory category)
        {
            if (session == null)
                return;

            session.MarkDraftChanged(dirtySection, category);
        }

        private static int ComparePlacements(ObjectPlacement left, ObjectPlacement right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return 1;
            if (right == null)
                return -1;

            bool leftPreserved = ScenarioBunkerDraftService.ShouldPreserveDuringLiveCapture(left);
            bool rightPreserved = ScenarioBunkerDraftService.ShouldPreserveDuringLiveCapture(right);
            if (leftPreserved != rightPreserved)
                return leftPreserved ? -1 : 1;

            int typeCompare = string.Compare(left.DefinitionReference ?? string.Empty, right.DefinitionReference ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            if (typeCompare != 0)
                return typeCompare;

            float leftY = left.Position != null ? left.Position.Y : 0f;
            float rightY = right.Position != null ? right.Position.Y : 0f;
            int yCompare = leftY.CompareTo(rightY);
            if (yCompare != 0)
                return yCompare;

            float leftX = left.Position != null ? left.Position.X : 0f;
            float rightX = right.Position != null ? right.Position.X : 0f;
            return leftX.CompareTo(rightX);
        }

        private static int CompareItemEntries(ItemEntry left, ItemEntry right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return 1;
            if (right == null)
                return -1;
            return string.Compare(left.ItemId ?? string.Empty, right.ItemId ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static string SafeObjectName(Obj_Base obj)
        {
            return ScenarioBunkerDraftService.SafeObjectName(obj);
        }
    }
}
