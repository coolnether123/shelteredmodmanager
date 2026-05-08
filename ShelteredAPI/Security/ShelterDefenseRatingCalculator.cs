using System;
using System.Collections.Generic;
using ShelteredAPI.Storage;

namespace ShelteredAPI.Security
{
    public sealed class ShelterDefenseRatingCalculator
    {
        public ShelterDefenseRating Calculate(ShelterDefenseInput input)
        {
            ShelterDefenseRating rating = new ShelterDefenseRating();
            if (input == null)
                return rating;

            AddContributors(rating, input.Contributors);
            AddAssignedWeapons(rating, input.CharacterItemAssignments);
            rating.TotalScore = rating.ArmedSurvivorScore + rating.StructureScore + rating.TrapScore + rating.SupportScore + rating.ModdedScore;
            return rating;
        }

        private static void AddContributors(ShelterDefenseRating rating, IList<ShelterDefenseContributor> contributors)
        {
            for (int i = 0; contributors != null && i < contributors.Count; i++)
            {
                ShelterDefenseContributor contributor = contributors[i];
                if (contributor == null)
                    continue;

                ShelterDefenseContributor copy = contributor.Copy();
                int quantity = Math.Max(1, copy.Quantity);
                float condition = copy.Condition01 <= 0f ? 1f : Math.Min(1f, copy.Condition01);
                int score = (int)Math.Round(copy.BaseScore * quantity * condition);
                AddScore(rating, copy.Kind, score);
                rating.Contributors.Add(copy);
            }
        }

        private static void AddAssignedWeapons(ShelterDefenseRating rating, IList<CharacterItemAssignment> assignments)
        {
            for (int i = 0; assignments != null && i < assignments.Count; i++)
            {
                CharacterItemAssignment assignment = assignments[i];
                if (assignment == null || assignment.Quantity <= 0)
                    continue;
                if (assignment.Kind != CharacterItemAssignmentKind.Equipped && assignment.Slot != CharacterItemSlot.MainHand)
                    continue;

                int score = ResolveWeaponScore(assignment.ItemId) * assignment.Quantity;
                if (score <= 0)
                    continue;

                ShelterDefenseContributor contributor = new ShelterDefenseContributor();
                contributor.ContributorId = "assignment:" + (assignment.AssignmentId ?? string.Empty);
                contributor.DisplayName = assignment.MemberDisplayName ?? string.Empty;
                contributor.Kind = ShelterDefenseContributorKind.ArmedSurvivor;
                contributor.BaseScore = score;
                contributor.Quantity = 1;
                contributor.Condition01 = 1f;
                rating.Contributors.Add(contributor);
                rating.ArmedSurvivorScore += score;
            }
        }

        private static void AddScore(ShelterDefenseRating rating, ShelterDefenseContributorKind kind, int score)
        {
            if (score <= 0)
                return;

            if (kind == ShelterDefenseContributorKind.ArmedSurvivor)
                rating.ArmedSurvivorScore += score;
            else if (kind == ShelterDefenseContributorKind.Door || kind == ShelterDefenseContributorKind.GuardPost || kind == ShelterDefenseContributorKind.Alarm)
                rating.StructureScore += score;
            else if (kind == ShelterDefenseContributorKind.Trap)
                rating.TrapScore += score;
            else if (kind == ShelterDefenseContributorKind.Pet || kind == ShelterDefenseContributorKind.SettlementSupport)
                rating.SupportScore += score;
            else
                rating.ModdedScore += score;
        }

        private static int ResolveWeaponScore(string itemId)
        {
            string id = (itemId ?? string.Empty).ToLowerInvariant();
            if (id.IndexOf("gun") >= 0 || id.IndexOf("rifle") >= 0 || id.IndexOf("shotgun") >= 0 || id.IndexOf("pistol") >= 0)
                return 25;
            if (id.IndexOf("knife") >= 0 || id.IndexOf("axe") >= 0 || id.IndexOf("bat") >= 0 || id.IndexOf("pipe") >= 0)
                return 12;
            if (id.IndexOf("weapon") >= 0)
                return 10;
            return 0;
        }
    }
}
