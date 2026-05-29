using System;
using System.Collections.Generic;
using Setting;
using UnityEngine;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesTogetherCardCategoryDefinition
    {
        public ulong Guid { get; set; }

        public string DisplayName { get; set; }

        public Color BackgroundColor { get; set; }

        public Color TextColor { get; set; }

        public TogetherCardLimitPerCharacter LimitPerCharacter { get; set; }

        public int LimitValue { get; set; }

        public int MaximumPerGroup { get; set; }

        public ulong ConversationTagGuid { get; set; }

        public ulong ConversationTagIfFailedGuid { get; set; }
    }

    public sealed class ParalivesTogetherCardDefinition
    {
        public ulong Guid { get; set; }

        public string DisplayName { get; set; }

        public TogetherCardType CardType { get; set; }

        public ulong CategoryGuid { get; set; }

        public bool TargetsAnotherCharacter { get; set; }

        public bool CanBeOfferedToManyCharactersAtATime { get; set; }

        public bool OverridesCooldown { get; set; }

        public float OverriddenCooldownMinutes { get; set; }

        public bool IgnoreGlobalOutcomes { get; set; }

        public bool ForceAppearing { get; set; }

        public ulong CanBeUsedByLifestageGuid { get; set; }

        public ulong CanAffectLifestageGuid { get; set; }
    }

    public sealed class ParalivesTogetherCardChoicesBuildingEvent
    {
        public global::SocialGroup Group { get; internal set; }

        public ulong GroupGuid { get; internal set; }

        public Dictionary<ulong, List<global::TogetherCardChoice>> Choices { get; internal set; }

        public ulong[] GroupCharacterGuids { get; internal set; }

        public void AddChoice(ulong actorGuid, ulong cardGuid, IEnumerable<ulong> targetGuids)
        {
            AddChoice(actorGuid, cardGuid, targetGuids, 0UL, 0UL);
        }

        public void AddChoice(ulong actorGuid, ulong cardGuid, IEnumerable<ulong> targetGuids, ulong skinGuid, ulong requestGuid)
        {
            if (actorGuid == 0UL || cardGuid == 0UL || Choices == null)
                return;

            if (!Choices.ContainsKey(actorGuid))
                Choices[actorGuid] = new List<global::TogetherCardChoice>();

            List<ulong> targets = new List<ulong>();
            if (targetGuids != null)
            {
                foreach (ulong targetGuid in targetGuids)
                {
                    if (targetGuid != 0UL && !targets.Contains(targetGuid))
                        targets.Add(targetGuid);
                }
            }

            Choices[actorGuid].Add(new global::TogetherCardChoice
            {
                CardGUID = cardGuid,
                Characters = targets,
                SkinGUID = skinGuid,
                RequestGUID = requestGuid
            });
        }
    }

    public sealed class ParalivesTogetherCardUsedEvent
    {
        public bool IsSuccess { get; internal set; }

        public ulong CardGuid { get; internal set; }

        public ulong ActorCharacterGuid { get; internal set; }

        public ulong TargetCharacterGuid { get; internal set; }

        public ulong[] CharactersFromCard { get; internal set; }

        public ulong SkinGuid { get; internal set; }

        public ulong InitiativeReplyGuid { get; internal set; }

        public ulong RequestGuid { get; internal set; }

        public ulong SocialGroupGuid { get; internal set; }

        public global::TogetherCardOutcomeData OutcomeData { get; internal set; }
    }

    public sealed class ParalivesTogetherFacade
    {
        private readonly ParalivesSocialFacade _social;

        public event Action<ParalivesTogetherCardChoicesBuildingEvent> ChoicesBuilding;

        public event Action<ParalivesTogetherCardUsedEvent> CardUsed;

        internal ParalivesTogetherFacade(ParalivesSocialFacade social)
        {
            _social = social;
        }

        public bool EnsureCategory(ParalivesTogetherCardCategoryDefinition definition)
        {
            if (definition == null || definition.Guid == 0UL)
                return false;

            Together together = global::Settings.Get<Together>();
            if (together == null)
                return false;

            TogetherCardCategory category = new TogetherCardCategory
            {
                GUID = definition.Guid,
                DisplayName = definition.DisplayName ?? string.Empty,
                BackgroundColor = definition.BackgroundColor,
                TextColor = definition.TextColor,
                LimitPerCharacter = definition.LimitPerCharacter,
                LimitValue = definition.LimitValue,
                MaximumPerGroup = definition.MaximumPerGroup,
                ConversationTag = definition.ConversationTagGuid,
                ConversationTagIfFailed = definition.ConversationTagIfFailedGuid
            };

            return EnsureCategory(category);
        }

        public bool EnsureCategory(TogetherCardCategory category)
        {
            if (category == null || category.GUID == 0UL)
                return false;

            try
            {
                Together together = global::Settings.Get<Together>();
                if (together == null)
                    return false;
                if (together.AllCategories == null)
                    together.AllCategories = new TogetherCardCategory[0];

                for (int i = 0; i < together.AllCategories.Length; i++)
                {
                    TogetherCardCategory existing = together.AllCategories[i];
                    if (existing != null && existing.GUID == category.GUID)
                    {
                        together.AllCategories[i] = category;
                        return true;
                    }
                }

                List<TogetherCardCategory> categories = new List<TogetherCardCategory>(together.AllCategories);
                categories.Add(category);
                together.AllCategories = categories.ToArray();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool EnsureCard(ParalivesTogetherCardDefinition definition)
        {
            if (definition == null || definition.Guid == 0UL)
                return false;

            TogetherCard card = new TogetherCard
            {
                GUID = definition.Guid,
                DisplayName = definition.DisplayName ?? string.Empty,
                CardType = definition.CardType,
                Category = definition.CategoryGuid,
                TargetsAnotherCharacter = definition.TargetsAnotherCharacter,
                CanBeOfferedToManyCharactersAtATime = definition.CanBeOfferedToManyCharactersAtATime,
                OverridesCooldown = definition.OverridesCooldown,
                OverridenCooldown = definition.OverriddenCooldownMinutes,
                IgnoreGlobalOutcomes = definition.IgnoreGlobalOutcomes,
                ForceAppearing = definition.ForceAppearing,
                CanBeUsedByLifestage = definition.CanBeUsedByLifestageGuid,
                CanAffectLifestage = definition.CanAffectLifestageGuid
            };

            return EnsureCard(card);
        }

        public bool EnsureCard(TogetherCard card)
        {
            if (card == null || card.GUID == 0UL)
                return false;

            try
            {
                Together together = global::Settings.Get<Together>();
                if (together == null)
                    return false;
                if (together.AllTogetherCards == null)
                    together.AllTogetherCards = new TogetherCard[0];

                for (int i = 0; i < together.AllTogetherCards.Length; i++)
                {
                    TogetherCard existing = together.AllTogetherCards[i];
                    if (existing != null && existing.GUID == card.GUID)
                    {
                        together.AllTogetherCards[i] = card;
                        return true;
                    }
                }

                List<TogetherCard> cards = new List<TogetherCard>(together.AllTogetherCards);
                cards.Add(card);
                together.AllTogetherCards = cards.ToArray();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool WasCardUsedRecently(ulong characterGuid, ulong cardGuid, float cooldownMinutes)
        {
            if (characterGuid == 0UL || cardGuid == 0UL || cooldownMinutes <= 0f)
                return false;

            try
            {
                global::AssetCharacter character = global::AssetManager.Instance.GetCharacter(characterGuid);
                if (character == null || character.Data == null || character.Data.MemoryLogSaveData == null)
                    return false;

                float cutoff = global::ParaTime.TotalMinutes - cooldownMinutes;
                for (int i = character.Data.MemoryLogSaveData.Count - 1; i >= 0; i--)
                {
                    global::AssetCharacterMemoryLogSaveData memory = character.Data.MemoryLogSaveData[i];
                    if (memory != null
                        && memory.MemoryLogType == MemoryLogType.TogetherCardChosen
                        && !memory.WasCancelled
                        && memory.StartTime >= cutoff
                        && memory.Data != null
                        && memory.Data.TogetherCardGUID == cardGuid)
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        internal void PublishChoicesBuilding(
            global::SocialGroup group,
            Dictionary<ulong, List<global::TogetherCardChoice>> choices)
        {
            Action<ParalivesTogetherCardChoicesBuildingEvent> handler = ChoicesBuilding;
            if (handler == null || group == null || choices == null)
                return;

            try
            {
                handler(new ParalivesTogetherCardChoicesBuildingEvent
                {
                    Group = group,
                    GroupGuid = group.GUID,
                    Choices = choices,
                    GroupCharacterGuids = _social.GetCharactersInGroup(group)
                });
            }
            catch
            {
            }
        }

        internal void PublishCardUsed(ParalivesTogetherCardUsedEvent evt)
        {
            if (evt == null)
                return;

            Action<ParalivesTogetherCardUsedEvent> handler = CardUsed;
            if (handler == null)
                return;

            try
            {
                handler(evt);
            }
            catch
            {
            }
        }
    }
}
