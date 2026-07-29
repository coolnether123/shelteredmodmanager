using System;
using System.Collections.Generic;
using ModAPI.Core;
using Setting;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesOccupationRegistry
    {
        private readonly object _sync = new object();
        private readonly List<Occupation> _occupations = new List<Occupation>();

        public int RegisteredOccupationCount
        {
            get { lock (_sync) return _occupations.Count; }
        }

        public ParalivesOccupationRegistrationResult RegisterOccupation(ParalivesOccupationDefinition definition)
        {
            string validationMessage;
            if (!ValidateDefinition(definition, out validationMessage))
                return Invalid(definition == null ? 0UL : definition.Guid, validationMessage);

            Occupation occupation;
            try
            {
                occupation = CreateOccupation(definition);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce(
                    "ParalivesOccupationRegistry.RegisterOccupation." + definition.Guid,
                    "Failed to convert Paralives occupation definition: " + ex.Message);
                return Error(definition.Guid, "Failed to convert occupation definition: " + ex.Message);
            }

            lock (_sync)
                Upsert(_occupations, occupation);

            ParalivesOccupationRegistrationResult applyResult = ApplyOccupationWhenReady(occupation);
            applyResult.Accepted = true;
            applyResult.RegisteredCount = 1;
            return applyResult;
        }

        public ParalivesOccupationRegistrationResult ApplyWhenReady()
        {
            try
            {
                return ApplyWhenReadyCore();
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce(
                    "ParalivesOccupationRegistry.ApplyWhenReady",
                    "Failed to apply Paralives occupation registrations: " + ex.Message);
                return Error(0UL, "Failed to apply occupation registrations: " + ex.Message);
            }
        }

        private ParalivesOccupationRegistrationResult ApplyWhenReadyCore()
        {
            Occupations occupationsSetting;
            ParalivesOccupationRegistrationResult readiness = TryGetOccupationsSetting(out occupationsSetting);
            if (readiness != null)
                return readiness;

            Occupation[] occupations;
            lock (_sync)
                occupations = _occupations.ToArray();

            int applied = 0;
            int duplicates = 0;
            int invalid = 0;
            int errors = 0;

            for (int i = 0; i < occupations.Length; i++)
            {
                Occupation occupation = occupations[i];
                if (occupation == null || occupation.GUID == 0UL)
                {
                    invalid++;
                    continue;
                }

                try
                {
                    if (EnsureOccupation(occupationsSetting, occupation))
                        applied++;
                    else
                        duplicates++;
                }
                catch (Exception ex)
                {
                    errors++;
                    MMLog.WarnOnce(
                        "ParalivesOccupationRegistry.ApplyWhenReady." + occupation.GUID,
                        "Failed to apply Paralives occupation registration " + occupation.GUID + ": " + ex.Message);
                }
            }

            if (errors > 0)
            {
                return new ParalivesOccupationRegistrationResult
                {
                    Status = ParalivesOccupationRegistrationStatus.Error,
                    Succeeded = false,
                    SettingsReady = true,
                    Applied = applied > 0,
                    AppliedCount = applied,
                    DuplicateCount = duplicates,
                    InvalidCount = invalid,
                    ErrorCount = errors,
                    Message = "One or more occupation registrations failed."
                };
            }

            if (invalid > 0 && applied == 0)
            {
                return new ParalivesOccupationRegistrationResult
                {
                    Status = ParalivesOccupationRegistrationStatus.InvalidDefinition,
                    Succeeded = false,
                    SettingsReady = true,
                    InvalidCount = invalid,
                    DuplicateCount = duplicates,
                    Message = "One or more registered occupation definitions are invalid."
                };
            }

            if (applied == 0 && duplicates > 0)
            {
                return new ParalivesOccupationRegistrationResult
                {
                    Status = ParalivesOccupationRegistrationStatus.Duplicate,
                    Succeeded = true,
                    SettingsReady = true,
                    IsDuplicate = true,
                    DuplicateCount = duplicates,
                    InvalidCount = invalid,
                    Message = "Occupation registrations are already present."
                };
            }

            return new ParalivesOccupationRegistrationResult
            {
                Status = ParalivesOccupationRegistrationStatus.Success,
                Succeeded = true,
                SettingsReady = true,
                Applied = applied > 0,
                AppliedCount = applied,
                DuplicateCount = duplicates,
                InvalidCount = invalid,
                Message = applied > 0
                    ? "Occupation registrations applied."
                    : "No occupation registrations are pending."
            };
        }

        private static ParalivesOccupationRegistrationResult ApplyOccupationWhenReady(Occupation occupation)
        {
            Occupations occupationsSetting;
            ParalivesOccupationRegistrationResult readiness = TryGetOccupationsSetting(out occupationsSetting);
            if (readiness != null)
            {
                readiness.OccupationGuid = occupation == null ? 0UL : occupation.GUID;
                return readiness;
            }

            try
            {
                if (EnsureOccupation(occupationsSetting, occupation))
                {
                    return new ParalivesOccupationRegistrationResult
                    {
                        Status = ParalivesOccupationRegistrationStatus.Success,
                        Succeeded = true,
                        Applied = true,
                        SettingsReady = true,
                        OccupationGuid = occupation.GUID,
                        AppliedCount = 1,
                        Message = "Occupation registration applied."
                    };
                }

                return new ParalivesOccupationRegistrationResult
                {
                    Status = ParalivesOccupationRegistrationStatus.Duplicate,
                    Succeeded = true,
                    SettingsReady = true,
                    IsDuplicate = true,
                    OccupationGuid = occupation == null ? 0UL : occupation.GUID,
                    DuplicateCount = 1,
                    Message = "Occupation registration is already present."
                };
            }
            catch (Exception ex)
            {
                ulong guid = occupation == null ? 0UL : occupation.GUID;
                MMLog.WarnOnce(
                    "ParalivesOccupationRegistry.ApplyOccupationWhenReady." + guid,
                    "Failed to apply Paralives occupation registration " + guid + ": " + ex.Message);
                return Error(guid, "Failed to apply occupation registration: " + ex.Message);
            }
        }

        private static ParalivesOccupationRegistrationResult TryGetOccupationsSetting(out Occupations occupations)
        {
            occupations = null;
            try
            {
                if (global::Settings.Instance == null)
                    return SettingsNotReady("Settings.Instance is not ready.");

                occupations = global::Settings.Get<Occupations>();
                if (occupations == null)
                    return SettingsNotReady("Occupation settings are not ready.");

                return null;
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce(
                    "ParalivesOccupationRegistry.GetOccupations",
                    "Failed to read Paralives occupation settings: " + ex.Message);
                return Error(0UL, "Failed to read occupation settings: " + ex.Message);
            }
        }

        private static bool EnsureOccupation(Occupations occupationsSetting, Occupation occupation)
        {
            if (occupationsSetting == null || occupation == null || occupation.GUID == 0UL)
                return false;

            if (ContainsOccupation(occupationsSetting.AllOccupations, occupation.GUID))
                return false;

            occupationsSetting.AllOccupations = Append(occupationsSetting.AllOccupations, occupation);
            AddToOccupationDictionary(occupationsSetting, occupation);
            return true;
        }

        private static bool ContainsOccupation(Occupation[] occupations, ulong guid)
        {
            if (occupations == null || guid == 0UL)
                return false;

            for (int i = 0; i < occupations.Length; i++)
            {
                if (occupations[i] != null && occupations[i].GUID == guid)
                    return true;
            }

            return false;
        }

        private static void AddToOccupationDictionary(Occupations occupationsSetting, Occupation occupation)
        {
            if (occupation.Type == SchoolJobTypes.Job)
            {
                if (occupationsSetting.Jobs == null)
                    occupationsSetting.Jobs = new Dictionary<ulong, Occupation>();
                if (!occupationsSetting.Jobs.ContainsKey(occupation.GUID))
                    occupationsSetting.Jobs.Add(occupation.GUID, occupation);
                return;
            }

            if (occupationsSetting.Schools == null)
                occupationsSetting.Schools = new Dictionary<ulong, Occupation>();
            if (!occupationsSetting.Schools.ContainsKey(occupation.GUID))
                occupationsSetting.Schools.Add(occupation.GUID, occupation);
        }

        private static Occupation CreateOccupation(ParalivesOccupationDefinition definition)
        {
            return new Occupation
            {
                GUID = definition.Guid,
                DisplayName = definition.DisplayName == null ? string.Empty : definition.DisplayName.Trim(),
                Type = ToNativeKind(definition.Kind),
                Company = definition.CompanyGuid,
                ProgressionLevel = definition.ProgressionLevelGuid,
                Schedule = definition.ScheduleGuid,
                Domains = ToReferences(definition.DomainGuids),
                AppropriateLifestages = ToReferences(definition.AppropriateLifeStageGuids),
                AutonomyTags = ToReferences(definition.AutonomyTagGuids),
                OverridesCompanyRabbithole = definition.OverridesCompanyRabbitHole,
                IsRabbithole = definition.IsRabbitHole,
                TravelDuration = definition.TravelDurationMinutes,
                MaxNumberOfExtraSlots = definition.MaxNumberOfExtraSlots,
                RarityWeight = definition.RarityWeight < 1 ? 1 : definition.RarityWeight,
                OutfitType = definition.OutfitTypeGuid,
                WorkOutfit = definition.WorkOutfitGuid,
                ForcedToAppearEveryday = definition.ForcedToAppearEveryday,
                Unlockables = new PossibleUnlockable[0],
                UsefulSkills = new UsefulSkill[0],
                GenerateTaskType = GenerateOccupationTask.Never
            };
        }

        private static SchoolJobTypes ToNativeKind(ParalivesOccupationKind kind)
        {
            return kind == ParalivesOccupationKind.School
                ? SchoolJobTypes.School
                : SchoolJobTypes.Job;
        }

        private static UlongAndGuid[] ToReferences(ulong[] values)
        {
            if (values == null || values.Length == 0)
                return new UlongAndGuid[0];

            List<UlongAndGuid> references = new List<UlongAndGuid>();
            HashSet<ulong> seen = new HashSet<ulong>();
            for (int i = 0; i < values.Length; i++)
            {
                ulong value = values[i];
                if (value == 0UL || seen.Contains(value))
                    continue;

                references.Add(new UlongAndGuid { Value = value });
                seen.Add(value);
            }

            return references.ToArray();
        }

        private static bool ValidateDefinition(ParalivesOccupationDefinition definition, out string message)
        {
            if (definition == null)
            {
                message = "Occupation definition is required.";
                return false;
            }

            if (definition.Guid == 0UL)
            {
                message = "Occupation definitions must have a non-zero GUID.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(definition.DisplayName))
            {
                message = "Occupation definitions must have a non-empty display name.";
                return false;
            }

            if (definition.Kind != ParalivesOccupationKind.Job && definition.Kind != ParalivesOccupationKind.School)
            {
                message = "Occupation kind is invalid.";
                return false;
            }

            if (definition.ScheduleGuid == 0UL)
            {
                message = "Occupation definitions must reference a non-zero schedule GUID.";
                return false;
            }

            if (definition.Kind == ParalivesOccupationKind.Job && definition.ProgressionLevelGuid == 0UL)
            {
                message = "Job occupation definitions must reference a non-zero progression level GUID.";
                return false;
            }

            if (definition.TravelDurationMinutes < 0f)
            {
                message = "Occupation travel duration cannot be negative.";
                return false;
            }

            if (definition.MaxNumberOfExtraSlots < 0)
            {
                message = "Occupation max extra slots cannot be negative.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private static void Upsert(List<Occupation> occupations, Occupation occupation)
        {
            for (int i = 0; i < occupations.Count; i++)
            {
                if (occupations[i] != null && occupations[i].GUID == occupation.GUID)
                {
                    occupations[i] = occupation;
                    return;
                }
            }

            occupations.Add(occupation);
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

        private static ParalivesOccupationRegistrationResult SettingsNotReady(string message)
        {
            return new ParalivesOccupationRegistrationResult
            {
                Status = ParalivesOccupationRegistrationStatus.SettingsNotReady,
                Succeeded = false,
                SettingsReady = false,
                Message = message
            };
        }

        private static ParalivesOccupationRegistrationResult Invalid(ulong guid, string message)
        {
            return new ParalivesOccupationRegistrationResult
            {
                Status = ParalivesOccupationRegistrationStatus.InvalidDefinition,
                Succeeded = false,
                OccupationGuid = guid,
                InvalidCount = 1,
                Message = message ?? string.Empty
            };
        }

        private static ParalivesOccupationRegistrationResult Error(ulong guid, string message)
        {
            return new ParalivesOccupationRegistrationResult
            {
                Status = ParalivesOccupationRegistrationStatus.Error,
                Succeeded = false,
                OccupationGuid = guid,
                ErrorCount = 1,
                Message = message ?? string.Empty
            };
        }
    }
}
