using System;
using Setting;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesSettingsFacade
    {
        internal ParalivesSettingsFacade()
        {
            Content = new ParalivesContentFacade(this);
        }

        public ParalivesContentFacade Content
        {
            get;
            private set;
        }

        public bool IsReady
        {
            get { return global::Settings.Instance != null; }
        }

        public bool Exists<T>() where T : class
        {
            try
            {
                return global::Settings.Instance != null && global::Settings.Exists(typeof(T));
            }
            catch
            {
                return false;
            }
        }

        public bool TryGet<T>(out T setting) where T : class
        {
            setting = null;
            try
            {
                if (global::Settings.Instance == null)
                    return false;

                setting = global::Settings.Get<T>();
                return setting != null;
            }
            catch
            {
                setting = null;
                return false;
            }
        }

        public T GetOrNull<T>() where T : class
        {
            T setting;
            return TryGet<T>(out setting) ? setting : null;
        }

        public bool TryGetAction(ulong actionGuid, out ActionUnit action)
        {
            action = null;
            if (actionGuid == 0UL)
                return false;

            Actions actions;
            if (!TryGet<Actions>(out actions))
                return false;

            try
            {
                action = actions.GetActionByGUID(actionGuid);
                return action != null;
            }
            catch
            {
                action = null;
                return false;
            }
        }

        public bool TryGetInteraction(ulong interactionGuid, out InteractionUnit interaction)
        {
            interaction = null;
            if (interactionGuid == 0UL)
                return false;

            Interactions interactions;
            if (!TryGet<Interactions>(out interactions))
                return false;

            try
            {
                interaction = interactions.GetInteractionByGUID(interactionGuid);
                return interaction != null;
            }
            catch
            {
                interaction = null;
                return false;
            }
        }

        public bool TryGetInteractionGroup(ulong groupGuid, out InteractionGroup group)
        {
            group = null;
            if (groupGuid == 0UL)
                return false;

            Interactions interactions;
            if (!TryGet<Interactions>(out interactions))
                return false;

            try
            {
                group = interactions.GetInteractionGroupByGUID(groupGuid);
                return group != null;
            }
            catch
            {
                group = null;
                return false;
            }
        }

        public bool TryGetCharacterRequirement(ulong requirementGuid, out CharacterRequirement requirement)
        {
            requirement = null;
            if (requirementGuid == 0UL)
                return false;

            CharacterRequirements requirements;
            if (!TryGet<CharacterRequirements>(out requirements))
                return false;

            try
            {
                requirement = requirements.GetCharacterRequirementByGUID(requirementGuid);
                return requirement != null;
            }
            catch
            {
                requirement = null;
                return false;
            }
        }

        public bool TryGetNotification(ulong notificationGuid, out Notification notification)
        {
            notification = null;
            if (notificationGuid == 0UL)
                return false;

            Notifications notifications;
            if (!TryGet<Notifications>(out notifications))
                return false;

            try
            {
                notification = notifications.GetNotificationByGUID(notificationGuid);
                return notification != null;
            }
            catch
            {
                notification = null;
                return false;
            }
        }

        public bool TryGetOccupation(ulong occupationGuid, out Occupation occupation)
        {
            occupation = null;
            if (occupationGuid == 0UL)
                return false;

            Occupations occupations;
            if (!TryGet<Occupations>(out occupations))
                return false;

            try
            {
                occupation = occupations.GetOccupationByGUID(occupationGuid);
                return occupation != null;
            }
            catch
            {
                occupation = null;
                return false;
            }
        }

        public bool TryGetSkill(ulong skillGuid, out Skill skill)
        {
            skill = null;
            if (skillGuid == 0UL)
                return false;

            Skills skills;
            if (!TryGet<Skills>(out skills))
                return false;

            try
            {
                skill = skills.GetSkillByGUID(skillGuid);
                return skill != null;
            }
            catch
            {
                skill = null;
                return false;
            }
        }

        public bool TryGetWant(ulong wantGuid, out Want want)
        {
            want = null;
            if (wantGuid == 0UL)
                return false;

            Wants wants;
            if (!TryGet<Wants>(out wants))
                return false;

            try
            {
                want = wants.GetWantByGUID(wantGuid);
                return want != null;
            }
            catch
            {
                want = null;
                return false;
            }
        }

        public bool TryGetNeed(ulong needGuid, out Need need)
        {
            need = null;
            if (needGuid == 0UL)
                return false;

            Needs needs;
            if (!TryGet<Needs>(out needs))
                return false;

            try
            {
                need = needs.GetNeedByGUID(needGuid);
                return need != null;
            }
            catch
            {
                need = null;
                return false;
            }
        }

        public bool TryGetStatusEffect(ulong statusEffectGuid, out StatusEffect statusEffect)
        {
            statusEffect = null;
            if (statusEffectGuid == 0UL)
                return false;

            StatusEffects statusEffects;
            if (!TryGet<StatusEffects>(out statusEffects))
                return false;

            try
            {
                statusEffect = statusEffects.GetStatusEffectByGUID(statusEffectGuid);
                return statusEffect != null;
            }
            catch
            {
                statusEffect = null;
                return false;
            }
        }

        public bool TryGetRelationshipLabel(ulong labelGuid, out RelationshipLabel label)
        {
            label = null;
            if (labelGuid == 0UL)
                return false;

            RelationshipLabels labels;
            if (!TryGet<RelationshipLabels>(out labels))
                return false;

            try
            {
                label = labels.GetLabelByGUID(labelGuid);
                return label != null;
            }
            catch
            {
                label = null;
                return false;
            }
        }

        public bool TryGetPersonalityTrait(ulong traitGuid, out PersonalityTrait trait)
        {
            trait = null;
            if (traitGuid == 0UL)
                return false;

            Personalities personalities;
            if (!TryGet<Personalities>(out personalities))
                return false;

            try
            {
                trait = personalities.GetPersonalityTraitByGUID(traitGuid);
                return trait != null;
            }
            catch
            {
                trait = null;
                return false;
            }
        }

        public bool TryGetOccupationUnlockable(ulong unlockableGuid, out OccupationUnlockable unlockable)
        {
            unlockable = null;
            if (unlockableGuid == 0UL)
                return false;

            Occupations occupations;
            if (!TryGet<Occupations>(out occupations))
                return false;

            try
            {
                unlockable = occupations.GetOccupationUnlockableByGUID(unlockableGuid);
                return unlockable != null;
            }
            catch
            {
                unlockable = null;
                return false;
            }
        }

        public object GetByClassName(string className)
        {
            if (string.IsNullOrEmpty(className))
                return null;

            try
            {
                return global::Settings.Instance == null ? null : global::Settings.GetByClassName(className);
            }
            catch
            {
                return null;
            }
        }

        public SettingBase GetByType(Type type)
        {
            if (type == null)
                return null;

            try
            {
                return global::Settings.Instance == null ? null : global::Settings.Get(type);
            }
            catch
            {
                return null;
            }
        }
    }
}
