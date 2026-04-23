using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Scenarios;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioAuthoringCaptureService
    {
        private static readonly ScenarioAuthoringCaptureService _instance = new ScenarioAuthoringCaptureService();

        public static ScenarioAuthoringCaptureService Instance
        {
            get { return _instance; }
        }

        private ScenarioAuthoringCaptureService()
        {
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
                config.Name = member.firstName;
                config.Gender = member.isMale ? ScenarioGender.Male : ScenarioGender.Female;

                CaptureStats(member, config);
                CaptureTraits(member, config);
                ScenarioCharacterAppearanceService.CaptureAppearance(member, config);

                familySetup.Members.Add(config);
                captured++;
            }

            session.WorkingDefinition.FamilySetup = familySetup;
            MarkCaptured(session, ScenarioDirtySection.Family, ScenarioEditCategory.Family);
            message = "Captured current family snapshot: " + captured + " member(s).";
            MMLog.WriteInfo("[ScenarioAuthoringCapture] " + message);
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
            StartingInventoryDefinition inventory = session.WorkingDefinition.StartingInventory ?? new StartingInventoryDefinition();
            inventory.OverrideRandomStart = true;
            inventory.Items.Clear();

            int totalItems = 0;
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
            for (int i = 0; i < capturedItems.Count; i++)
                inventory.Items.Add(capturedItems[i]);

            session.WorkingDefinition.StartingInventory = inventory;
            MarkCaptured(session, ScenarioDirtySection.Inventory, ScenarioEditCategory.Inventory);
            message = "Captured current inventory snapshot: " + capturedItems.Count + " stack(s), " + totalItems + " total item(s).";
            MMLog.WriteInfo("[ScenarioAuthoringCapture] " + message);
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

            BunkerEditsDefinition bunkerEdits = ScenarioBunkerDraftService.EnsureBunkerEdits(session);
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
            ScenarioBunkerDraftService.MarkBunkerDirty(session);
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
            Obj_Base obj;
            string blockingReason;
            if (!TryResolveCapturableObject(target, out obj, out blockingReason))
            {
                message = blockingReason;
                return false;
            }

            BunkerEditsDefinition bunkerEdits = ScenarioBunkerDraftService.EnsureBunkerEdits(session);
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
            ScenarioBunkerDraftService.MarkBunkerDirty(session);
            MMLog.WriteInfo("[ScenarioAuthoringCapture] " + message);
            return true;
        }

        public bool RemoveSelectedObjectPlacement(ScenarioEditorSession session, ScenarioAuthoringTarget target, out string message)
        {
            message = null;
            if (session == null || session.WorkingDefinition == null || session.WorkingDefinition.BunkerEdits == null)
            {
                message = "No captured shelter object placements are available.";
                return false;
            }

            Obj_Base obj;
            string blockingReason;
            if (!TryResolveCapturableObject(target, out obj, out blockingReason))
            {
                message = blockingReason;
                return false;
            }

            int index = ScenarioBunkerDraftService.FindPlacementIndex(session.WorkingDefinition.BunkerEdits.ObjectPlacements, obj);
            if (index < 0)
            {
                message = "The selected object does not have a captured scenario placement.";
                return false;
            }

            session.WorkingDefinition.BunkerEdits.ObjectPlacements.RemoveAt(index);
            ScenarioBunkerDraftService.MarkBunkerDirty(session);
            message = "Removed captured placement for '" + SafeObjectName(obj) + "'.";
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

            Obj_Base obj;
            string ignored;
            if (!TryResolveCapturableObject(target, out obj, out ignored))
                return false;

            return ScenarioBunkerDraftService.FindPlacementIndex(session.WorkingDefinition.BunkerEdits.ObjectPlacements, obj) >= 0;
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

            if (!session.DirtyFlags.Contains(dirtySection))
                session.DirtyFlags.Add(dirtySection);

            session.CurrentEditCategory = category;
            session.HasAppliedToCurrentWorld = true;
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
