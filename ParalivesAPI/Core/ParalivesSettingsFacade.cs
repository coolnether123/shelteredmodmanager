using System;
using Setting;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesSettingsFacade
    {
        internal ParalivesSettingsFacade()
        {
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

            action = actions.GetActionByGUID(actionGuid);
            return action != null;
        }

        public bool TryGetInteraction(ulong interactionGuid, out InteractionUnit interaction)
        {
            interaction = null;
            if (interactionGuid == 0UL)
                return false;

            Interactions interactions;
            if (!TryGet<Interactions>(out interactions))
                return false;

            interaction = interactions.GetInteractionByGUID(interactionGuid);
            return interaction != null;
        }

        public bool TryGetInteractionGroup(ulong groupGuid, out InteractionGroup group)
        {
            group = null;
            if (groupGuid == 0UL)
                return false;

            Interactions interactions;
            if (!TryGet<Interactions>(out interactions))
                return false;

            group = interactions.GetInteractionGroupByGUID(groupGuid);
            return group != null;
        }

        public bool TryGetNotification(ulong notificationGuid, out Notification notification)
        {
            notification = null;
            if (notificationGuid == 0UL)
                return false;

            Notifications notifications;
            if (!TryGet<Notifications>(out notifications))
                return false;

            notification = notifications.GetNotificationByGUID(notificationGuid);
            return notification != null;
        }

        public bool TryGetOccupation(ulong occupationGuid, out Occupation occupation)
        {
            occupation = null;
            if (occupationGuid == 0UL)
                return false;

            Occupations occupations;
            if (!TryGet<Occupations>(out occupations))
                return false;

            occupation = occupations.GetOccupationByGUID(occupationGuid);
            return occupation != null;
        }

        public bool TryGetSkill(ulong skillGuid, out Skill skill)
        {
            skill = null;
            if (skillGuid == 0UL)
                return false;

            Skills skills;
            if (!TryGet<Skills>(out skills))
                return false;

            skill = skills.GetSkillByGUID(skillGuid);
            return skill != null;
        }

        public bool TryGetWant(ulong wantGuid, out Want want)
        {
            want = null;
            if (wantGuid == 0UL)
                return false;

            Wants wants;
            if (!TryGet<Wants>(out wants))
                return false;

            want = wants.GetWantByGUID(wantGuid);
            return want != null;
        }

        public bool TryGetNeed(ulong needGuid, out Need need)
        {
            need = null;
            if (needGuid == 0UL)
                return false;

            Needs needs;
            if (!TryGet<Needs>(out needs))
                return false;

            need = needs.GetNeedByGUID(needGuid);
            return need != null;
        }

        public bool TryGetStatusEffect(ulong statusEffectGuid, out StatusEffect statusEffect)
        {
            statusEffect = null;
            if (statusEffectGuid == 0UL)
                return false;

            StatusEffects statusEffects;
            if (!TryGet<StatusEffects>(out statusEffects))
                return false;

            statusEffect = statusEffects.GetStatusEffectByGUID(statusEffectGuid);
            return statusEffect != null;
        }

        public bool TryGetRelationshipLabel(ulong labelGuid, out RelationshipLabel label)
        {
            label = null;
            if (labelGuid == 0UL)
                return false;

            RelationshipLabels labels;
            if (!TryGet<RelationshipLabels>(out labels))
                return false;

            label = labels.GetLabelByGUID(labelGuid);
            return label != null;
        }

        public bool TryGetPersonalityTrait(ulong traitGuid, out PersonalityTrait trait)
        {
            trait = null;
            if (traitGuid == 0UL)
                return false;

            Personalities personalities;
            if (!TryGet<Personalities>(out personalities))
                return false;

            trait = personalities.GetPersonalityTraitByGUID(traitGuid);
            return trait != null;
        }

        public bool TryGetOccupationUnlockable(ulong unlockableGuid, out OccupationUnlockable unlockable)
        {
            unlockable = null;
            if (unlockableGuid == 0UL)
                return false;

            Occupations occupations;
            if (!TryGet<Occupations>(out occupations))
                return false;

            unlockable = occupations.GetOccupationUnlockableByGUID(unlockableGuid);
            return unlockable != null;
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
