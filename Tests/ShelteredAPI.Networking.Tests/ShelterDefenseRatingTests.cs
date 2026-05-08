using System.Collections.Generic;
using ShelteredAPI.Security;
using ShelteredAPI.Storage;

namespace ShelteredAPI.Networking.Tests
{
    internal static class ShelterDefenseRatingTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("ShelterDefenseRating_CalculatesPureContributorScores", CalculatesPureContributorScores));
            tests.Add(new TestCase("ShelterDefenseRating_CountsEquippedWeapons", CountsEquippedWeapons));
        }

        private static void CalculatesPureContributorScores()
        {
            ShelterDefenseInput input = new ShelterDefenseInput();
            input.Contributors.Add(new ShelterDefenseContributor { Kind = ShelterDefenseContributorKind.Door, BaseScore = 10, Quantity = 2, Condition01 = 1f });
            input.Contributors.Add(new ShelterDefenseContributor { Kind = ShelterDefenseContributorKind.Trap, BaseScore = 8, Quantity = 3, Condition01 = 0.5f });
            input.Contributors.Add(new ShelterDefenseContributor { Kind = ShelterDefenseContributorKind.SettlementSupport, BaseScore = 6, Quantity = 1, Condition01 = 1f });

            ShelterDefenseRating rating = new ShelterDefenseRatingCalculator().Calculate(input);

            TestAssert.Equal(38, rating.TotalScore, "Total defense should sum pure contributors.");
            TestAssert.Equal(20, rating.StructureScore, "Door score should land in structure bucket.");
            TestAssert.Equal(12, rating.TrapScore, "Trap score should apply condition.");
            TestAssert.Equal(6, rating.SupportScore, "Settlement support should land in support bucket.");
        }

        private static void CountsEquippedWeapons()
        {
            ShelterDefenseInput input = new ShelterDefenseInput();
            CharacterItemAssignment assignment = new CharacterItemAssignment();
            assignment.AssignmentId = "weapon-1";
            assignment.MemberDisplayName = "Guard";
            assignment.ItemId = "Rifle";
            assignment.Quantity = 1;
            assignment.Kind = CharacterItemAssignmentKind.Equipped;
            assignment.Slot = CharacterItemSlot.MainHand;
            input.CharacterItemAssignments.Add(assignment);

            ShelterDefenseRating rating = new ShelterDefenseRatingCalculator().Calculate(input);

            TestAssert.Equal(25, rating.ArmedSurvivorScore, "Equipped rifle should contribute armed survivor score.");
            TestAssert.Equal(25, rating.TotalScore, "Weapon score should be included in total.");
        }
    }
}
