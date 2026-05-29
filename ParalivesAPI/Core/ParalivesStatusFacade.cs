using System.Collections.Generic;
using Setting;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesStatusEffectSnapshot
    {
        public ulong CharacterGuid { get; internal set; }

        public int Index { get; internal set; }

        public ulong StatusEffectGuid { get; internal set; }

        public string DisplayName { get; internal set; }

        public bool IsKnownStatusEffect { get; internal set; }

        public bool Active { get; internal set; }

        public bool RemoveNextFrame { get; internal set; }

        public float RemainingTimeInMinutes { get; internal set; }

        public ulong BrainLogicWhoGaveIt { get; internal set; }

        public ulong TogetherCardWhoGaveIt { get; internal set; }

        public ulong CharacterWhoGaveIt { get; internal set; }

        public ulong TogetherCardSocialGroup { get; internal set; }

        public ulong StoryCardWhoGaveIt { get; internal set; }

        public ulong OccupationGuid { get; internal set; }

        public int OccupationIndex { get; internal set; }

        public ulong OccupationUnlockableWhoGaveIt { get; internal set; }

        public ulong SkillGuid { get; internal set; }

        public bool HasOverrideValue { get; internal set; }

        public float OverrideValue { get; internal set; }
    }

    public sealed class ParalivesStatusFacade
    {
        private readonly ParalivesCharacterFacade _characters;
        private readonly ParalivesSettingsFacade _settings;

        public event System.Action<ParalivesStatusEffectChangedEvent> StatusEffectChanged;

        internal ParalivesStatusFacade(ParalivesCharacterFacade characters, ParalivesSettingsFacade settings)
        {
            _characters = characters;
            _settings = settings;
        }

        public ParalivesStatusEffectSnapshot[] ReadStatusEffects(ulong characterGuid)
        {
            global::AssetCharacter character;
            return _characters.TryGet(characterGuid, out character)
                ? ReadStatusEffects(character)
                : new ParalivesStatusEffectSnapshot[0];
        }

        public ParalivesStatusEffectSnapshot[] ReadStatusEffects(global::AssetCharacter character)
        {
            List<ParalivesStatusEffectSnapshot> snapshots = new List<ParalivesStatusEffectSnapshot>();
            if (character == null || character.Data == null || character.Data.StatusEffectSaveData == null)
                return snapshots.ToArray();

            for (int i = 0; i < character.Data.StatusEffectSaveData.Count; i++)
            {
                global::AssetCharacterStatusEffectSaveData data = character.Data.StatusEffectSaveData[i];
                if (data != null)
                    snapshots.Add(CreateSnapshot(character.GUID, i, data));
            }

            return snapshots.ToArray();
        }

        public bool HasStatusEffect(ulong characterGuid, ulong statusEffectGuid)
        {
            global::AssetCharacter character;
            if (statusEffectGuid == 0UL || !_characters.TryGet(characterGuid, out character))
                return false;

            try
            {
                return global::StatusEffectManager.Instance.HasStatusEffect(statusEffectGuid, character);
            }
            catch
            {
                return false;
            }
        }

        public bool TryAddStatusEffect(ulong characterGuid, ulong statusEffectGuid, global::AddStatusEffectData data)
        {
            global::AssetCharacter character;
            if (statusEffectGuid == 0UL || !_characters.TryGet(characterGuid, out character))
                return false;

            try
            {
                global::StatusEffectManager.Instance.AddStatusEffectToCharacter(character, statusEffectGuid, data ?? new global::AddStatusEffectData());
                character.IsSaveDirty = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool TryRemoveStatusEffect(ulong characterGuid, ulong statusEffectGuid)
        {
            global::AssetCharacter character;
            if (statusEffectGuid == 0UL || !_characters.TryGet(characterGuid, out character))
                return false;

            try
            {
                global::StatusEffectManager.Instance.RemoveStatusEffectFromCharacter(character, statusEffectGuid);
                character.IsSaveDirty = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private ParalivesStatusEffectSnapshot CreateSnapshot(
            ulong characterGuid,
            int index,
            global::AssetCharacterStatusEffectSaveData data)
        {
            StatusEffect statusEffect;
            bool known = _settings.TryGetStatusEffect(data.StatusEffectGUID, out statusEffect);

            return new ParalivesStatusEffectSnapshot
            {
                CharacterGuid = characterGuid,
                Index = index,
                StatusEffectGuid = data.StatusEffectGUID,
                DisplayName = known && statusEffect != null ? (statusEffect.DisplayName ?? string.Empty) : string.Empty,
                IsKnownStatusEffect = known,
                Active = data.Active,
                RemoveNextFrame = data.RemoveNextFrame,
                RemainingTimeInMinutes = data.RemainingTimeInMinutes,
                BrainLogicWhoGaveIt = data.BrainLogicWhoGaveIt,
                TogetherCardWhoGaveIt = data.TogetherCardWhoGaveIt,
                CharacterWhoGaveIt = data.CharacterWhoGaveIt,
                TogetherCardSocialGroup = data.TogetherCardSocialGroup,
                StoryCardWhoGaveIt = data.StorycardWhoGaveIt,
                OccupationGuid = data.OccupationGUID,
                OccupationIndex = data.OccupationIndex,
                OccupationUnlockableWhoGaveIt = data.OccupationUnlockableWhoGaveIt,
                SkillGuid = data.SkillGUID,
                HasOverrideValue = data.HasOverrideValue,
                OverrideValue = data.OverrideValue
            };
        }

        internal void PublishChanged(ParalivesStatusEffectChangedEvent evt)
        {
            if (evt == null)
                return;

            System.Action<ParalivesStatusEffectChangedEvent> handler = StatusEffectChanged;
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
