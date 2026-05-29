using Setting;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesInteractionGroupChildRegistration
    {
        private ParalivesInteractionGroupChildRegistration()
        {
        }

        public ulong ParentGroupGuid { get; private set; }

        public bool UsesParentRootGroup { get; private set; }

        public ParalivesInteractionRootGroup ParentRootGroup { get; private set; }

        public ulong ChildItemGuid { get; private set; }

        public InteractionItemType Type { get; private set; }

        public ulong InteractionGuid { get; private set; }

        public ulong GroupGuid { get; private set; }

        public string NestedInteractionDisplayName { get; private set; }

        public static ParalivesInteractionGroupChildRegistration ForInteraction(
            ulong parentGroupGuid,
            ulong interactionGuid,
            string nestedInteractionDisplayName = null,
            ulong childItemGuid = 0UL)
        {
            ulong stableChildGuid = childItemGuid != 0UL
                ? childItemGuid
                : ParalivesGuid.FromStableName("ParalivesAPI.InteractionGroupChild", parentGroupGuid + ":interaction:" + interactionGuid);

            return new ParalivesInteractionGroupChildRegistration
            {
                ParentGroupGuid = parentGroupGuid,
                UsesParentRootGroup = false,
                ChildItemGuid = stableChildGuid,
                Type = InteractionItemType.Interaction,
                InteractionGuid = interactionGuid,
                GroupGuid = 0UL,
                NestedInteractionDisplayName = nestedInteractionDisplayName
            };
        }

        public static ParalivesInteractionGroupChildRegistration ForGroup(
            ulong parentGroupGuid,
            ulong groupGuid,
            ulong childItemGuid = 0UL)
        {
            ulong stableChildGuid = childItemGuid != 0UL
                ? childItemGuid
                : ParalivesGuid.FromStableName("ParalivesAPI.InteractionGroupChild", parentGroupGuid + ":group:" + groupGuid);

            return new ParalivesInteractionGroupChildRegistration
            {
                ParentGroupGuid = parentGroupGuid,
                UsesParentRootGroup = false,
                ChildItemGuid = stableChildGuid,
                Type = InteractionItemType.Group,
                InteractionGuid = 0UL,
                GroupGuid = groupGuid,
                NestedInteractionDisplayName = null
            };
        }

        public static ParalivesInteractionGroupChildRegistration ForInteractionInRootGroup(
            ParalivesInteractionRootGroup parentRootGroup,
            ulong interactionGuid,
            string nestedInteractionDisplayName = null,
            ulong childItemGuid = 0UL)
        {
            ulong stableChildGuid = childItemGuid != 0UL
                ? childItemGuid
                : ParalivesGuid.FromStableName("ParalivesAPI.InteractionGroupChild", parentRootGroup + ":interaction:" + interactionGuid);

            return new ParalivesInteractionGroupChildRegistration
            {
                ParentGroupGuid = 0UL,
                UsesParentRootGroup = true,
                ParentRootGroup = parentRootGroup,
                ChildItemGuid = stableChildGuid,
                Type = InteractionItemType.Interaction,
                InteractionGuid = interactionGuid,
                GroupGuid = 0UL,
                NestedInteractionDisplayName = nestedInteractionDisplayName
            };
        }

        public static ParalivesInteractionGroupChildRegistration ForGroupInRootGroup(
            ParalivesInteractionRootGroup parentRootGroup,
            ulong groupGuid,
            ulong childItemGuid = 0UL)
        {
            ulong stableChildGuid = childItemGuid != 0UL
                ? childItemGuid
                : ParalivesGuid.FromStableName("ParalivesAPI.InteractionGroupChild", parentRootGroup + ":group:" + groupGuid);

            return new ParalivesInteractionGroupChildRegistration
            {
                ParentGroupGuid = 0UL,
                UsesParentRootGroup = true,
                ParentRootGroup = parentRootGroup,
                ChildItemGuid = stableChildGuid,
                Type = InteractionItemType.Group,
                InteractionGuid = 0UL,
                GroupGuid = groupGuid,
                NestedInteractionDisplayName = null
            };
        }

        public static ParalivesInteractionGroupChildRegistration FromSettingItem(
            ulong parentGroupGuid,
            InteractionGroupItem childItem)
        {
            if (childItem == null)
                throw new System.ArgumentNullException("childItem");

            if (childItem.Type == InteractionItemType.Group)
                return ForGroup(parentGroupGuid, childItem.Group, childItem.GUID);

            return ForInteraction(
                parentGroupGuid,
                childItem.Interaction,
                childItem.IsNestedNameDifferentThanInteractionName ? childItem.DisplayNameOfNestedInteraction : null,
                childItem.GUID);
        }

        internal InteractionGroupItem ToSettingItem()
        {
            return new InteractionGroupItem
            {
                GUID = ChildItemGuid,
                Type = Type,
                Interaction = InteractionGuid,
                Group = GroupGuid,
                IsNestedNameDifferentThanInteractionName = !string.IsNullOrEmpty(NestedInteractionDisplayName),
                DisplayNameOfNestedInteraction = NestedInteractionDisplayName
            };
        }
    }
}
