using System.Collections.Generic;
using ShelteredAPI.Storage;

namespace ShelteredAPI.Security
{
    public enum ShelterDefenseContributorKind
    {
        ArmedSurvivor,
        Door,
        Trap,
        GuardPost,
        Alarm,
        Pet,
        SettlementSupport,
        ModdedDefense
    }

    public sealed class ShelterDefenseContributor
    {
        public ShelterDefenseContributor()
        {
            ContributorId = string.Empty;
            DisplayName = string.Empty;
            Kind = ShelterDefenseContributorKind.ModdedDefense;
        }

        public string ContributorId { get; set; }
        public string DisplayName { get; set; }
        public ShelterDefenseContributorKind Kind { get; set; }
        public int BaseScore { get; set; }
        public int Quantity { get; set; }
        public float Condition01 { get; set; }

        public ShelterDefenseContributor Copy()
        {
            return new ShelterDefenseContributor
            {
                ContributorId = ContributorId ?? string.Empty,
                DisplayName = DisplayName ?? string.Empty,
                Kind = Kind,
                BaseScore = BaseScore,
                Quantity = Quantity,
                Condition01 = Condition01
            };
        }
    }

    public sealed class ShelterDefenseInput
    {
        public ShelterDefenseInput()
        {
            Contributors = new List<ShelterDefenseContributor>();
            CharacterItemAssignments = new List<CharacterItemAssignment>();
        }

        public IList<ShelterDefenseContributor> Contributors { get; private set; }
        public IList<CharacterItemAssignment> CharacterItemAssignments { get; private set; }
    }

    public sealed class ShelterDefenseRating
    {
        public ShelterDefenseRating()
        {
            Contributors = new List<ShelterDefenseContributor>();
        }

        public int TotalScore { get; set; }
        public int ArmedSurvivorScore { get; set; }
        public int StructureScore { get; set; }
        public int TrapScore { get; set; }
        public int SupportScore { get; set; }
        public int ModdedScore { get; set; }
        public IList<ShelterDefenseContributor> Contributors { get; private set; }
    }
}
