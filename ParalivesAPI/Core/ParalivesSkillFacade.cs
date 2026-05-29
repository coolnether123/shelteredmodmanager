namespace ParalivesAPI.Core
{
    public sealed class ParalivesSkillSnapshot
    {
        public ulong CharacterGuid { get; internal set; }

        public ulong SkillGuid { get; internal set; }

        public string DisplayName { get; internal set; }

        public bool IsKnownSkill { get; internal set; }

        public bool IsAppropriateForCharacter { get; internal set; }

        public bool HasSkill { get; internal set; }

        public int Level { get; internal set; }

        public int MaxLevel { get; internal set; }

        public float CurrentLevelExperience { get; internal set; }

        public float TotalExperience { get; internal set; }

        public float ExperienceNeededForNextLevel { get; internal set; }

        public bool IsAtMaxLevel { get; internal set; }
    }

    public sealed class ParalivesSkillGrantResult
    {
        public bool Succeeded { get; internal set; }

        public ulong CharacterGuid { get; internal set; }

        public ulong SkillGuid { get; internal set; }

        public float RequestedAmount { get; internal set; }

        public float GrantedAmount { get; internal set; }

        public int PreviousLevel { get; internal set; }

        public int CurrentLevel { get; internal set; }

        public float PreviousCurrentLevelExperience { get; internal set; }

        public string Message { get; internal set; }
    }

    public sealed class ParalivesSkillFacade
    {
        private readonly ParalivesCharacterFacade _characters;

        public event System.Action<ParalivesSkillChangedEvent> SkillChanged;

        internal ParalivesSkillFacade(ParalivesCharacterFacade characters)
        {
            _characters = characters;
        }

        public int GetLevel(ulong characterGuid, ulong skillGuid)
        {
            global::AssetCharacter character;
            return _characters.TryGet(characterGuid, out character)
                ? GetLevel(character, skillGuid)
                : 0;
        }

        public int GetLevel(global::AssetCharacter character, ulong skillGuid)
        {
            if (character == null || skillGuid == 0UL || global::SkillManager.Instance == null)
                return 0;

            try
            {
                return global::SkillManager.Instance.GetCharacterSkillLevel(character, skillGuid);
            }
            catch
            {
                return 0;
            }
        }

        public bool HasSkill(global::AssetCharacter character, ulong skillGuid)
        {
            if (character == null || skillGuid == 0UL || global::SkillManager.Instance == null)
                return false;

            try
            {
                return global::SkillManager.Instance.HasSkill(character, skillGuid);
            }
            catch
            {
                return false;
            }
        }

        public bool IsAppropriateForCharacter(ulong characterGuid, ulong skillGuid)
        {
            global::AssetCharacter character;
            return _characters.TryGet(characterGuid, out character)
                && IsAppropriateForCharacter(character, skillGuid);
        }

        public bool IsAppropriateForCharacter(global::AssetCharacter character, ulong skillGuid)
        {
            if (character == null || skillGuid == 0UL || global::SkillManager.Instance == null)
                return false;

            try
            {
                return global::SkillManager.Instance.IsSkillAppropriateForCharacter(character, skillGuid);
            }
            catch
            {
                return false;
            }
        }

        public int GetMaxLevel(ulong skillGuid)
        {
            if (skillGuid == 0UL || global::SkillManager.Instance == null)
                return 0;

            try
            {
                return global::SkillManager.Instance.GetMaxLevel(skillGuid);
            }
            catch
            {
                return 0;
            }
        }

        public float GetExperienceInCurrentLevel(global::AssetCharacter character, ulong skillGuid)
        {
            if (character == null || skillGuid == 0UL || global::SkillManager.Instance == null)
                return 0f;

            try
            {
                return global::SkillManager.Instance.GetCharacterExperienceInCurrentLevel(character, skillGuid);
            }
            catch
            {
                return 0f;
            }
        }

        public float GetExperienceNeededToLevelUp(int currentLevel)
        {
            if (currentLevel <= 0 || global::SkillManager.Instance == null)
                return 0f;

            try
            {
                return global::SkillManager.Instance.GetExperienceNeededToLevelUp(currentLevel);
            }
            catch
            {
                return 0f;
            }
        }

        public bool TryGetTotalExperience(global::AssetCharacter character, ulong skillGuid, out float totalExperience)
        {
            totalExperience = 0f;
            global::AssetCharacterSkillData data = FindSkillSaveData(character, skillGuid);
            if (data == null)
                return false;

            totalExperience = data.TotalExperience;
            return true;
        }

        public bool TryRead(ulong characterGuid, ulong skillGuid, out ParalivesSkillSnapshot snapshot)
        {
            snapshot = null;
            global::AssetCharacter character;
            if (!_characters.TryGet(characterGuid, out character))
                return false;

            snapshot = Read(character, skillGuid);
            return snapshot.IsKnownSkill;
        }

        public ParalivesSkillSnapshot Read(ulong characterGuid, ulong skillGuid)
        {
            global::AssetCharacter character;
            return _characters.TryGet(characterGuid, out character)
                ? Read(character, skillGuid)
                : new ParalivesSkillSnapshot { CharacterGuid = characterGuid, SkillGuid = skillGuid };
        }

        public ParalivesSkillSnapshot Read(global::AssetCharacter character, ulong skillGuid)
        {
            ParalivesSkillSnapshot snapshot = new ParalivesSkillSnapshot
            {
                CharacterGuid = character == null ? 0UL : character.GUID,
                SkillGuid = skillGuid,
                DisplayName = string.Empty
            };

            if (character == null || skillGuid == 0UL)
                return snapshot;

            try
            {
                Setting.Skills skills = global::Settings.Get<Setting.Skills>();
                Setting.Skill skill = skills == null ? null : skills.GetSkillByGUID(skillGuid);
                snapshot.IsKnownSkill = skill != null;
                snapshot.DisplayName = skill == null ? string.Empty : (skill.DisplayName ?? string.Empty);
            }
            catch
            {
            }

            snapshot.IsAppropriateForCharacter = IsAppropriateForCharacter(character, skillGuid);
            snapshot.MaxLevel = GetMaxLevel(skillGuid);
            snapshot.Level = GetLevel(character, skillGuid);
            snapshot.HasSkill = HasSkill(character, skillGuid);
            snapshot.CurrentLevelExperience = GetExperienceInCurrentLevel(character, skillGuid);
            snapshot.ExperienceNeededForNextLevel = snapshot.Level >= snapshot.MaxLevel
                ? 0f
                : GetExperienceNeededToLevelUp(snapshot.Level <= 0 ? 1 : snapshot.Level);
            snapshot.IsAtMaxLevel = snapshot.MaxLevel > 0 && snapshot.Level >= snapshot.MaxLevel;

            float totalExperience;
            if (TryGetTotalExperience(character, skillGuid, out totalExperience))
                snapshot.TotalExperience = totalExperience;

            return snapshot;
        }

        public bool SetLevel(global::AssetCharacter character, ulong skillGuid, int level)
        {
            if (character == null || skillGuid == 0UL || global::SkillManager.Instance == null)
                return false;

            try
            {
                global::SkillManager.Instance.SetCharacterSkillLevel(character, skillGuid, level);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool IncrementBurst(global::AssetCharacter character, ulong skillGuid, float amount)
        {
            if (character == null || skillGuid == 0UL || amount <= 0f || global::SkillManager.Instance == null)
                return false;

            try
            {
                global::SkillManager.Instance.IncrementCharacterSkillBurst(character, skillGuid, amount);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public ParalivesSkillGrantResult IncrementBurstDetailed(global::AssetCharacter character, ulong skillGuid, float amount)
        {
            ParalivesSkillGrantResult result = new ParalivesSkillGrantResult
            {
                CharacterGuid = character == null ? 0UL : character.GUID,
                SkillGuid = skillGuid,
                RequestedAmount = amount,
                Message = string.Empty
            };

            if (character == null)
            {
                result.Message = "Character not found.";
                return result;
            }

            if (skillGuid == 0UL)
            {
                result.Message = "Skill GUID is empty.";
                return result;
            }

            if (amount <= 0f)
            {
                result.Succeeded = true;
                result.Message = "No skill experience requested.";
                return result;
            }

            if (global::SkillManager.Instance == null)
            {
                result.Message = "Skill manager is not ready.";
                return result;
            }

            try
            {
                result.PreviousLevel = global::SkillManager.Instance.GetCharacterSkillLevel(character, skillGuid);
                result.PreviousCurrentLevelExperience =
                    global::SkillManager.Instance.GetCharacterExperienceInCurrentLevel(character, skillGuid);
                var increment = global::SkillManager.Instance.IncrementCharacterSkillBurst(character, skillGuid, amount);
                result.CurrentLevel = global::SkillManager.Instance.GetCharacterSkillLevel(character, skillGuid);
                result.GrantedAmount = increment.Item3;
                result.Succeeded = result.GrantedAmount > 0f;
                result.Message = result.Succeeded ? "Granted." : "Skill is capped or unavailable.";
                return result;
            }
            catch (System.Exception ex)
            {
                result.Message = ex.Message;
                return result;
            }
        }

        public ParalivesSkillGrantResult IncrementBurstDetailed(ulong characterGuid, ulong skillGuid, float amount)
        {
            global::AssetCharacter character;
            return _characters.TryGet(characterGuid, out character)
                ? IncrementBurstDetailed(character, skillGuid, amount)
                : new ParalivesSkillGrantResult
                {
                    CharacterGuid = characterGuid,
                    SkillGuid = skillGuid,
                    RequestedAmount = amount,
                    Message = "Character not found."
                };
        }

        public bool IncrementOverTime(global::AssetCharacter character, ulong skillGuid)
        {
            return IncrementOverTime(character, skillGuid, false);
        }

        public bool IncrementOverTime(global::AssetCharacter character, ulong skillGuid, bool isSlowLearning)
        {
            if (character == null || skillGuid == 0UL || global::SkillManager.Instance == null)
                return false;

            try
            {
                global::SkillManager.Instance.IncrementCharacterSkillOverTime(character, skillGuid, isSlowLearning);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static global::AssetCharacterSkillData FindSkillSaveData(global::AssetCharacter character, ulong skillGuid)
        {
            if (character == null || character.Data == null || character.Data.Skills == null || skillGuid == 0UL)
                return null;

            for (int i = 0; i < character.Data.Skills.Count; i++)
            {
                global::AssetCharacterSkillData data = character.Data.Skills[i];
                if (data != null && data.Skill == skillGuid)
                    return data;
            }

            return null;
        }

        internal void PublishChanged(ParalivesSkillChangedEvent evt)
        {
            if (evt == null)
                return;

            System.Action<ParalivesSkillChangedEvent> handler = SkillChanged;
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
