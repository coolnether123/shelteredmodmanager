using System;
using System.Collections.Generic;
using ModAPI.Core;
using Setting;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesInteractionRegistry
    {
        public const ulong OtherCharacterInteractionsGroupGuid = 1802949143211095722UL;

        private readonly object _sync = new object();
        private readonly List<ActionUnit> _actions = new List<ActionUnit>();
        private readonly List<InteractionGroup> _groups = new List<InteractionGroup>();
        private readonly List<InteractionUnit> _interactions = new List<InteractionUnit>();
        private readonly List<ParalivesInteractionGroupChildRegistration> _groupChildren =
            new List<ParalivesInteractionGroupChildRegistration>();

        public int RegisteredActionCount
        {
            get { lock (_sync) return _actions.Count; }
        }

        public int RegisteredGroupCount
        {
            get { lock (_sync) return _groups.Count; }
        }

        public int RegisteredInteractionCount
        {
            get { lock (_sync) return _interactions.Count; }
        }

        public int RegisteredGroupChildCount
        {
            get { lock (_sync) return _groupChildren.Count; }
        }

        public void Register(ParalivesInteractionContent content)
        {
            if (content == null)
                throw new ArgumentNullException("content");

            for (int i = 0; i < content.Actions.Count; i++)
                RegisterAction(content.Actions[i]);
            for (int i = 0; i < content.Groups.Count; i++)
                RegisterGroup(content.Groups[i]);
            for (int i = 0; i < content.Interactions.Count; i++)
                RegisterInteraction(content.Interactions[i]);
            for (int i = 0; i < content.GroupChildren.Count; i++)
                RegisterGroupChild(content.GroupChildren[i]);
        }

        public ulong GetOtherCharacterInteractionsGroupGuid()
        {
            return GetRootGroupGuid(ParalivesInteractionRootGroup.OtherCharacter);
        }

        public ulong GetRootGroupGuid(ParalivesInteractionRootGroup rootGroup)
        {
            try
            {
                Interactions interactions = Settings.Get<Interactions>();
                if (interactions == null)
                    return 0UL;

                switch (rootGroup)
                {
                    case ParalivesInteractionRootGroup.Floor:
                        return interactions.FloorInteractions;
                    case ParalivesInteractionRootGroup.SelfCharacter:
                        return interactions.SelfCharacterInteractions;
                    case ParalivesInteractionRootGroup.OtherCharacter:
                        return interactions.OtherCharacterInteractions;
                    case ParalivesInteractionRootGroup.GroupOf2InsideCharacter:
                        return interactions.GroupOf2InsideCharacterInteractions;
                    case ParalivesInteractionRootGroup.GroupInsideCharacter:
                        return interactions.GroupInsideCharacterInteractions;
                    case ParalivesInteractionRootGroup.GroupOutsideCharacter:
                        return interactions.GroupOutsideCharacterInteractions;
                    default:
                        return 0UL;
                }
            }
            catch
            {
                return 0UL;
            }
        }

        public void RegisterAction(ActionUnit action)
        {
            if (action == null)
                throw new ArgumentNullException("action");
            if (action.GUID == 0UL)
                throw new ArgumentException("Registered actions must have a non-zero GUID.", "action");

            lock (_sync)
                Upsert(_actions, action, delegate(ActionUnit value) { return value.GUID; });
        }

        public void RegisterGroup(InteractionGroup group)
        {
            if (group == null)
                throw new ArgumentNullException("group");
            if (group.GUID == 0UL)
                throw new ArgumentException("Registered interaction groups must have a non-zero GUID.", "group");

            lock (_sync)
                Upsert(_groups, group, delegate(InteractionGroup value) { return value.GUID; });
        }

        public void RegisterInteraction(InteractionUnit interaction)
        {
            if (interaction == null)
                throw new ArgumentNullException("interaction");
            if (interaction.GUID == 0UL)
                throw new ArgumentException("Registered interactions must have a non-zero GUID.", "interaction");

            lock (_sync)
                Upsert(_interactions, interaction, delegate(InteractionUnit value) { return value.GUID; });
        }

        public void RegisterGroupChild(ParalivesInteractionGroupChildRegistration child)
        {
            if (child == null)
                throw new ArgumentNullException("child");
            if (!child.UsesParentRootGroup && child.ParentGroupGuid == 0UL)
                throw new ArgumentException("Group children must target a non-zero parent group GUID.", "child");
            if (child.Type == InteractionItemType.Interaction && child.InteractionGuid == 0UL)
                throw new ArgumentException("Interaction group children must target a non-zero interaction GUID.", "child");
            if (child.Type == InteractionItemType.Group && child.GroupGuid == 0UL)
                throw new ArgumentException("Interaction group children must target a non-zero child group GUID.", "child");

            lock (_sync)
                Upsert(_groupChildren, child, GetGroupChildKey);
        }

        public void AddInteractionToGroup(ulong parentGroupGuid, ulong interactionGuid, string nestedInteractionDisplayName = null)
        {
            RegisterGroupChild(ParalivesInteractionGroupChildRegistration.ForInteraction(
                parentGroupGuid,
                interactionGuid,
                nestedInteractionDisplayName));
        }

        public void AddInteractionToRootGroup(
            ParalivesInteractionRootGroup parentRootGroup,
            ulong interactionGuid,
            string nestedInteractionDisplayName = null)
        {
            RegisterGroupChild(ParalivesInteractionGroupChildRegistration.ForInteractionInRootGroup(
                parentRootGroup,
                interactionGuid,
                nestedInteractionDisplayName));
        }

        public void AddInteractionToOtherCharacterInteractions(ulong interactionGuid, string nestedInteractionDisplayName = null)
        {
            AddInteractionToRootGroup(
                ParalivesInteractionRootGroup.OtherCharacter,
                interactionGuid,
                nestedInteractionDisplayName);
        }

        public void AddChildToGroup(ulong parentGroupGuid, InteractionGroupItem childItem)
        {
            RegisterGroupChild(ParalivesInteractionGroupChildRegistration.FromSettingItem(parentGroupGuid, childItem));
        }

        public void AddItemToGroup(ulong parentGroupGuid, InteractionGroupItem childItem)
        {
            AddChildToGroup(parentGroupGuid, childItem);
        }

        public void AddGroupToGroup(ulong parentGroupGuid, ulong groupGuid)
        {
            RegisterGroupChild(ParalivesInteractionGroupChildRegistration.ForGroup(parentGroupGuid, groupGuid));
        }

        public void AddGroupToRootGroup(ParalivesInteractionRootGroup parentRootGroup, ulong groupGuid)
        {
            RegisterGroupChild(ParalivesInteractionGroupChildRegistration.ForGroupInRootGroup(
                parentRootGroup,
                groupGuid));
        }

        public void AddGroupToOtherCharacterInteractions(ulong groupGuid)
        {
            AddGroupToRootGroup(ParalivesInteractionRootGroup.OtherCharacter, groupGuid);
        }

        public bool EnsureRegistered()
        {
            return ApplyWhenReady();
        }

        public bool ApplyWhenReady()
        {
            try
            {
                return ApplyWhenReadyCore();
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ParalivesInteractionRegistry.ApplyWhenReady", "Failed to apply Paralives interaction registrations: " + ex.Message);
                return false;
            }
        }

        private bool ApplyWhenReadyCore()
        {
            if (Settings.Instance == null)
                return false;

            Actions actionsSetting = Settings.Get<Actions>();
            Interactions interactionsSetting = Settings.Get<Interactions>();
            if (actionsSetting == null || interactionsSetting == null)
                return false;

            ActionUnit[] actions;
            InteractionGroup[] groups;
            InteractionUnit[] interactions;
            ParalivesInteractionGroupChildRegistration[] children;

            lock (_sync)
            {
                actions = _actions.ToArray();
                groups = _groups.ToArray();
                interactions = _interactions.ToArray();
                children = _groupChildren.ToArray();
            }

            bool changed = false;

            for (int i = 0; i < actions.Length; i++)
                changed |= EnsureAction(actionsSetting, actions[i]);
            for (int i = 0; i < groups.Length; i++)
                changed |= EnsureGroup(interactionsSetting, groups[i]);
            for (int i = 0; i < interactions.Length; i++)
                changed |= EnsureInteraction(interactionsSetting, interactions[i]);
            for (int i = 0; i < children.Length; i++)
                changed |= EnsureGroupChild(interactionsSetting, children[i]);

            return changed;
        }

        private static bool EnsureAction(Actions actionsSetting, ActionUnit action)
        {
            if (action == null || action.GUID == 0UL)
                return false;

            if (ContainsAction(actionsSetting.AllActions, action.GUID))
                return false;

            actionsSetting.AllActions = Append(actionsSetting.AllActions, action);
            return true;
        }

        private static bool EnsureGroup(Interactions interactionsSetting, InteractionGroup group)
        {
            if (group == null || group.GUID == 0UL)
                return false;

            if (ContainsGroup(interactionsSetting.InteractionGroups, group.GUID))
                return false;

            interactionsSetting.InteractionGroups = Append(interactionsSetting.InteractionGroups, group);
            return true;
        }

        private static bool EnsureInteraction(Interactions interactionsSetting, InteractionUnit interaction)
        {
            if (interaction == null || interaction.GUID == 0UL)
                return false;

            if (ContainsInteraction(interactionsSetting.AllInteractions, interaction.GUID))
                return false;

            interactionsSetting.AllInteractions = Append(interactionsSetting.AllInteractions, interaction);
            return true;
        }

        private static bool EnsureGroupChild(Interactions interactionsSetting, ParalivesInteractionGroupChildRegistration child)
        {
            if (child == null)
                return false;

            ulong parentGroupGuid = ResolveParentGroupGuid(interactionsSetting, child);
            if (parentGroupGuid == 0UL)
                return false;

            InteractionGroup parent = interactionsSetting.GetInteractionGroupByGUID(parentGroupGuid);
            if (parent == null)
                return false;

            InteractionGroupItem childItem = child.ToSettingItem();
            if (ContainsGroupChild(parent.ChildrenInteractionAndGroups, childItem))
                return false;

            parent.ChildrenInteractionAndGroups = Append(parent.ChildrenInteractionAndGroups, childItem);
            return true;
        }

        private static bool ContainsAction(ActionUnit[] actions, ulong guid)
        {
            if (actions == null)
                return false;
            for (int i = 0; i < actions.Length; i++)
            {
                if (actions[i] != null && actions[i].GUID == guid)
                    return true;
            }
            return false;
        }

        private static bool ContainsGroup(InteractionGroup[] groups, ulong guid)
        {
            if (groups == null)
                return false;
            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i] != null && groups[i].GUID == guid)
                    return true;
            }
            return false;
        }

        private static bool ContainsInteraction(InteractionUnit[] interactions, ulong guid)
        {
            if (interactions == null)
                return false;
            for (int i = 0; i < interactions.Length; i++)
            {
                if (interactions[i] != null && interactions[i].GUID == guid)
                    return true;
            }
            return false;
        }

        private static bool ContainsGroupChild(InteractionGroupItem[] children, InteractionGroupItem child)
        {
            if (children == null || child == null)
                return false;

            for (int i = 0; i < children.Length; i++)
            {
                InteractionGroupItem existing = children[i];
                if (existing == null)
                    continue;

                if (existing.GUID != 0UL && child.GUID != 0UL && existing.GUID == child.GUID)
                    return true;
                if (existing.Type != child.Type)
                    continue;
                if (child.Type == InteractionItemType.Group && existing.Group == child.Group)
                    return true;
                if (child.Type == InteractionItemType.Interaction && existing.Interaction == child.Interaction)
                    return true;
            }

            return false;
        }

        private static void Upsert<T>(List<T> list, T item, Func<T, ulong> getGuid)
        {
            ulong guid = getGuid(item);
            for (int i = 0; i < list.Count; i++)
            {
                if (getGuid(list[i]) == guid)
                {
                    list[i] = item;
                    return;
                }
            }

            list.Add(item);
        }

        private static ulong GetGroupChildKey(ParalivesInteractionGroupChildRegistration child)
        {
            return child.ChildItemGuid != 0UL
                ? child.ChildItemGuid
                : ParalivesGuid.FromStableName(
                    "ParalivesAPI.InteractionGroupChild",
                    GetParentKey(child) + ":" + child.Type + ":" + child.GroupGuid + ":" + child.InteractionGuid);
        }

        private static string GetParentKey(ParalivesInteractionGroupChildRegistration child)
        {
            return child.UsesParentRootGroup
                ? "root:" + child.ParentRootGroup
                : child.ParentGroupGuid.ToString();
        }

        private static ulong ResolveParentGroupGuid(
            Interactions interactionsSetting,
            ParalivesInteractionGroupChildRegistration child)
        {
            if (child == null)
                return 0UL;
            if (!child.UsesParentRootGroup)
                return child.ParentGroupGuid;
            if (interactionsSetting == null)
                return 0UL;

            switch (child.ParentRootGroup)
            {
                case ParalivesInteractionRootGroup.Floor:
                    return interactionsSetting.FloorInteractions;
                case ParalivesInteractionRootGroup.SelfCharacter:
                    return interactionsSetting.SelfCharacterInteractions;
                case ParalivesInteractionRootGroup.OtherCharacter:
                    return interactionsSetting.OtherCharacterInteractions;
                case ParalivesInteractionRootGroup.GroupOf2InsideCharacter:
                    return interactionsSetting.GroupOf2InsideCharacterInteractions;
                case ParalivesInteractionRootGroup.GroupInsideCharacter:
                    return interactionsSetting.GroupInsideCharacterInteractions;
                case ParalivesInteractionRootGroup.GroupOutsideCharacter:
                    return interactionsSetting.GroupOutsideCharacterInteractions;
                default:
                    return 0UL;
            }
        }

        private static T[] Append<T>(T[] source, T item)
        {
            int length = source != null ? source.Length : 0;
            T[] result = new T[length + 1];
            if (length > 0)
                Array.Copy(source, result, length);

            result[length] = item;
            return result;
        }
    }
}
