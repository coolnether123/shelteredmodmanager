using System;
using System.Collections.Generic;
using System.Globalization;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Presentation.Inspector;
using UnityEngine;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal static class ScenarioCastMemberPickerBuilder
    {
        public static ScenarioAuthoringInspectorSection BuildSection(
            string id,
            string title,
            ScenarioDefinition definition,
            bool includeStarting,
            bool includeFuture,
            ScenarioActorRef currentActorRef,
            string actionPrefix,
            string actionKey,
            string emptyText)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            List<ScenarioCastMemberReferenceCandidate> candidates = ScenarioCastMemberReferenceCatalog.Build(definition, includeStarting, includeFuture);
            for (int i = 0; i < candidates.Count; i++)
            {
                ScenarioCastMemberReferenceCandidate candidate = candidates[i];
                if (candidate == null)
                    continue;

                items.Add(BuildCard(candidate, currentActorRef, actionPrefix, actionKey));
            }

            if (items.Count == 0)
                items.Add(ScenarioInspectorItemFactory.Text(emptyText));

            return new ScenarioAuthoringInspectorSection
            {
                Id = id,
                Title = title,
                Expanded = true,
                Layout = items.Count > 0 && items[0].CastCard != null ? ScenarioAuthoringInspectorSectionLayout.CastCardGrid : ScenarioAuthoringInspectorSectionLayout.NoteList,
                Items = items.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorItem BuildCard(ScenarioCastMemberReferenceCandidate candidate, ScenarioActorRef currentActorRef, string actionPrefix, string actionKey)
        {
            bool selected = ScenarioCastMemberReferenceCatalog.SameActorRef(candidate.ActorRef, currentActorRef);
            string encodedToken = Uri.EscapeDataString(candidate.Token ?? string.Empty);
            ScenarioAuthoringInspectorAction primary = ScenarioInspectorItemFactory.Action(
                actionPrefix + actionKey + "." + encodedToken,
                selected ? "Selected" : "Select",
                "Use this cast member as the actor-backed reference.",
                candidate.ActorRef != null,
                selected,
                selected ? "OK" : "SV");

            ScenarioCastCardViewModel card = new ScenarioCastCardViewModel
            {
                Name = candidate.DisplayName,
                RoleLine = candidate.Detail,
                Status = candidate.Badge,
                ArrivalSummary = candidate.Kind == ScenarioCastMemberReferenceCandidate.FutureKind ? candidate.Detail : null,
                CompactReference = true,
                PrimaryAction = primary
            };

            ApplyPortrait(card, candidate.Member);
            return new ScenarioAuthoringInspectorItem
            {
                Kind = ScenarioAuthoringInspectorItemKind.Property,
                CastCard = card
            };
        }

        private static void ApplyPortrait(ScenarioCastCardViewModel card, FamilyMemberConfig member)
        {
            if (card == null || member == null)
                return;

            Color hair;
            Color skin;
            Color shirt;
            Color pants;
            ScenarioCastPortraitResolver.ResolveColors(member, out hair, out skin, out shirt, out pants);
            card.PortraitSprite = ScenarioCastPortraitResolver.Resolve(member);
            card.PortraitTexture = ScenarioCastPortraitResolver.ResolveTexture(member);
            card.HairColor = hair;
            card.SkinColor = skin;
            card.ShirtColor = shirt;
            card.PantsColor = pants;
            card.Stats = BuildStats(member);
            card.Traits = BuildTraits(member);
        }

        private static ScenarioCastStatViewModel[] BuildStats(FamilyMemberConfig member)
        {
            if (member == null || member.Stats == null || member.Stats.Count == 0)
                return new ScenarioCastStatViewModel[0];

            int max = Math.Min(4, member.Stats.Count);
            ScenarioCastStatViewModel[] stats = new ScenarioCastStatViewModel[max];
            for (int i = 0; i < max; i++)
            {
                StatOverride stat = member.Stats[i];
                string id = stat != null && !string.IsNullOrEmpty(stat.StatId) ? stat.StatId : "Stat";
                stats[i] = new ScenarioCastStatViewModel
                {
                    Id = id,
                    Label = id.Length >= 3 ? id.Substring(0, 3) : id,
                    Value = stat != null ? Math.Max(0, Math.Min(20, stat.Value)) : 0,
                    Max = 20
                };
            }
            return stats;
        }

        private static string[] BuildTraits(FamilyMemberConfig member)
        {
            if (member == null || member.Traits == null || member.Traits.Count == 0)
                return new string[0];

            int max = Math.Min(3, member.Traits.Count);
            string[] traits = new string[max];
            for (int i = 0; i < max; i++)
                traits[i] = member.Traits[i];
            return traits;
        }
    }
}
