using System.Collections.Generic;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesSocialGroupSnapshot
    {
        public ulong GroupGuid { get; internal set; }

        public float TimestampCreated { get; internal set; }

        public float TogetherEnergy { get; internal set; }

        public float TogetherBarSpeed { get; internal set; }

        public int TogetherCardsUsed { get; internal set; }

        public ulong TalkerCharacterGuid { get; internal set; }

        public ulong MainListenerCharacterGuid { get; internal set; }

        public ulong CurrentConversationTagGuid { get; internal set; }

        public ulong[] CharacterGuids { get; internal set; }

        public int ClusterCount { get; internal set; }

        public int InteractionCount { get; internal set; }
    }

    public sealed class ParalivesSocialFacade
    {
        internal ParalivesSocialFacade()
        {
        }

        public bool TryGetGroup(ulong socialGroupGuid, out global::SocialGroup group)
        {
            group = null;
            if (socialGroupGuid == 0UL || global::SocialGroupManager.Instance == null)
                return false;

            try
            {
                group = global::SocialGroupManager.Instance.GetSocialGroupByGUID(socialGroupGuid);
                return group != null;
            }
            catch
            {
                group = null;
                return false;
            }
        }

        public bool TryGetCurrentGroup(ulong characterGuid, out global::SocialGroup group)
        {
            group = null;
            if (characterGuid == 0UL || global::SocialGroupManager.Instance == null)
                return false;

            try
            {
                group = global::SocialGroupManager.Instance.GetCharacterCurrentSocialGroup(characterGuid);
                return group != null;
            }
            catch
            {
                group = null;
                return false;
            }
        }

        public global::SocialGroup[] GetAllGroups()
        {
            try
            {
                List<global::SocialGroup> groups = global::SocialGroupManager.Instance.GetAllSocialGroups();
                return groups == null ? new global::SocialGroup[0] : groups.ToArray();
            }
            catch
            {
                return new global::SocialGroup[0];
            }
        }

        public ulong[] GetCharactersInGroup(global::SocialGroup group)
        {
            return group == null || group.CharactersInGroup == null
                ? new ulong[0]
                : group.CharactersInGroup.ToArray();
        }

        public ulong[] GetCharactersInGroup(ulong socialGroupGuid)
        {
            global::SocialGroup group;
            return TryGetGroup(socialGroupGuid, out group) ? GetCharactersInGroup(group) : new ulong[0];
        }

        public bool IsCharacterInGroup(ulong socialGroupGuid, ulong characterGuid)
        {
            if (characterGuid == 0UL)
                return false;

            ulong[] characters = GetCharactersInGroup(socialGroupGuid);
            for (int i = 0; i < characters.Length; i++)
            {
                if (characters[i] == characterGuid)
                    return true;
            }

            return false;
        }

        public bool TryReadGroup(ulong socialGroupGuid, out ParalivesSocialGroupSnapshot snapshot)
        {
            snapshot = null;
            global::SocialGroup group;
            if (!TryGetGroup(socialGroupGuid, out group))
                return false;

            snapshot = ReadGroup(group);
            return true;
        }

        public ParalivesSocialGroupSnapshot ReadGroup(global::SocialGroup group)
        {
            if (group == null)
            {
                return new ParalivesSocialGroupSnapshot
                {
                    CharacterGuids = new ulong[0]
                };
            }

            return new ParalivesSocialGroupSnapshot
            {
                GroupGuid = group.GUID,
                TimestampCreated = group.TimestampCreated,
                TogetherEnergy = group.TogetherEnergy,
                TogetherBarSpeed = group.TogetherBarSpeed,
                TogetherCardsUsed = group.TogetherCardsUsed,
                TalkerCharacterGuid = group.TalkerCharacter,
                MainListenerCharacterGuid = group.MainListenerCharacter,
                CurrentConversationTagGuid = group.CurrentConversationTag,
                CharacterGuids = GetCharactersInGroup(group),
                ClusterCount = group.Clusters == null ? 0 : group.Clusters.Count,
                InteractionCount = group.Interactions == null ? 0 : group.Interactions.Count
            };
        }
    }
}
