using System;
using System.Collections.Generic;

using ModAPI.Actors;

using ShelteredAPI.Actors;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Infrastructure.Runtime
{
    internal sealed class ScenarioActorResolver
    {
        private const string ScenarioCastBindingType = "sheltered.scenario.cast";
        private const string ScenarioFutureSurvivorBindingType = "sheltered.scenario.future_survivor";
        private const string ScenarioNpcBindingType = "sheltered.scenario.npc";
        private const string BuiltInOwner = "shelteredapi";

        private readonly IActorSystem _actors;

        public ScenarioActorResolver()
            : this(ShelteredActors.Instance)
        {
        }

        public ScenarioActorResolver(IActorSystem actors)
        {
            _actors = actors;
        }

        public IActorRecord Resolve(ScenarioDefinition definition, ScenarioActorRef actorRef)
        {
            return Resolve(definition, actorRef, null, null, null);
        }

        public IActorRecord ResolveStartingMember(ScenarioDefinition definition, FamilyMemberConfig member, int memberIndex)
        {
            ScenarioActorRef actorRef = member != null && member.ActorRef != null
                ? member.ActorRef
                : BuildLegacyStartingMemberRef(definition, member, memberIndex);

            return Resolve(definition, actorRef, member, member != null ? member.ActorComponents : null, null);
        }

        public IActorRecord ResolveFutureSurvivor(ScenarioDefinition definition, FutureSurvivorDefinition survivor, int survivorIndex)
        {
            ScenarioActorRef actorRef = null;
            if (survivor != null)
                actorRef = survivor.ActorRef ?? (survivor.Survivor != null ? survivor.Survivor.ActorRef : null);
            if (actorRef == null)
                actorRef = BuildLegacyFutureSurvivorRef(definition, survivor, survivorIndex);

            List<ScenarioActorComponentDefinition> components = survivor != null && survivor.ActorComponents != null
                ? survivor.ActorComponents
                : null;
            FamilyMemberConfig member = survivor != null ? survivor.Survivor : null;
            return Resolve(definition, actorRef, member, components, null);
        }

        public IActorRecord ResolveNpc(ScenarioDefinition definition, ScenarioNpcDefinition npc, int npcIndex)
        {
            ScenarioActorRef actorRef = npc != null && npc.ActorRef != null
                ? npc.ActorRef
                : BuildLegacyNpcRef(definition, npc, npcIndex);

            return Resolve(definition, actorRef, null, npc != null ? npc.ActorComponents : null, npc);
        }

        public void EnsureScenarioActors(ScenarioDefinition definition)
        {
            if (definition == null)
                return;

            if (definition.FamilySetup != null)
            {
                for (int i = 0; definition.FamilySetup.Members != null && i < definition.FamilySetup.Members.Count; i++)
                    ResolveStartingMember(definition, definition.FamilySetup.Members[i], i);

                for (int i = 0; definition.FamilySetup.FutureSurvivors != null && i < definition.FamilySetup.FutureSurvivors.Count; i++)
                    ResolveFutureSurvivor(definition, definition.FamilySetup.FutureSurvivors[i], i);
            }

            for (int i = 0; definition.ScenarioCharacters != null && i < definition.ScenarioCharacters.Count; i++)
                ResolveNpc(definition, definition.ScenarioCharacters[i], i);
        }

        public string GetDisplayName(ActorId actorId, string fallback)
        {
            if (_actors != null && actorId != null)
            {
                ActorProfileComponent profile;
                if (_actors.TryGet<ActorProfileComponent>(actorId, out profile) && profile != null)
                {
                    string fullName = JoinName(profile.FirstName, profile.LastName);
                    if (!string.IsNullOrEmpty(fullName))
                        return fullName;
                }
            }

            return fallback ?? string.Empty;
        }

        private IActorRecord Resolve(
            ScenarioDefinition definition,
            ScenarioActorRef actorRef,
            FamilyMemberConfig familyMember,
            List<ScenarioActorComponentDefinition> components,
            ScenarioNpcDefinition npc)
        {
            if (_actors == null || actorRef == null)
                return null;

            ActorId requestedId;
            if (TryBuildActorId(actorRef, out requestedId))
            {
                IActorRecord exact;
                if (_actors.TryGet(requestedId, out exact) && exact != null)
                {
                    BindActor(exact.Id, actorRef, false);
                    HydrateActor(definition, exact.Id, actorRef, familyMember, components, npc);
                    return exact;
                }
            }

            ActorId boundId;
            if (!string.IsNullOrEmpty(actorRef.BindingType)
                && !string.IsNullOrEmpty(actorRef.BindingKey)
                && _actors.TryResolve(actorRef.BindingType, actorRef.BindingKey, out boundId)
                && boundId != null)
            {
                IActorRecord bound;
                if (_actors.TryGet(boundId, out bound) && bound != null)
                {
                    HydrateActor(definition, bound.Id, actorRef, familyMember, components, npc);
                    return bound;
                }
            }

            if (requestedId != null && IsScenarioOwnedSynthetic(definition, actorRef, requestedId))
            {
                IActorRecord ensured = _actors.Ensure(new ActorCreateRequest
                {
                    Id = requestedId,
                    Kind = requestedId.Kind,
                    Domain = requestedId.Domain,
                    LifecycleState = ActorLifecycleState.Registered,
                    PresenceState = ActorPresenceState.Offscreen,
                    Flags = ActorFlags.Persistent | ActorFlags.Synthetic,
                    Origin = new ActorOrigin
                    {
                        SourceModId = ResolveScenarioDomain(definition),
                        SourceKey = !string.IsNullOrEmpty(actorRef.BindingKey) ? actorRef.BindingKey : requestedId.ToString(),
                        Generator = "scenario-actor-resolver"
                    }
                });

                if (ensured != null)
                {
                    BindActor(ensured.Id, actorRef, false);
                    HydrateActor(definition, ensured.Id, actorRef, familyMember, components, npc);
                }
                return ensured;
            }

            return null;
        }

        private void HydrateActor(
            ScenarioDefinition definition,
            ActorId actorId,
            ScenarioActorRef actorRef,
            FamilyMemberConfig familyMember,
            List<ScenarioActorComponentDefinition> components,
            ScenarioNpcDefinition npc)
        {
            if (_actors == null || actorId == null)
                return;

            ActorProfileComponent profile = BuildProfile(actorRef, familyMember, npc);
            if (profile != null)
                _actors.Set(actorId, profile, BuiltInOwner);

            ActorAttributeSetComponent attributes = BuildAttributes(familyMember, npc);
            if (attributes != null && attributes.Entries != null && attributes.Entries.Count > 0)
                _actors.Set(actorId, attributes, BuiltInOwner);

            HydrateSerializedComponents(actorId, components);
        }

        private void HydrateSerializedComponents(ActorId actorId, List<ScenarioActorComponentDefinition> components)
        {
            for (int i = 0; components != null && i < components.Count; i++)
            {
                ScenarioActorComponentDefinition entry = components[i];
                if (entry == null || string.IsNullOrEmpty(entry.ComponentId))
                    continue;

                IActorComponentSerializer serializer;
                if (!_actors.TryGetSerializer(entry.ComponentId, out serializer) || serializer == null)
                    continue;

                IActorComponent component = serializer.Deserialize(entry.PayloadJson, entry.Version);
                if (component != null)
                    _actors.Set(actorId, component, entry.OwnerModId);
            }
        }

        private void BindActor(ActorId actorId, ScenarioActorRef actorRef, bool replaceExisting)
        {
            if (_actors == null || actorId == null || actorRef == null)
                return;
            if (string.IsNullOrEmpty(actorRef.BindingType) || string.IsNullOrEmpty(actorRef.BindingKey))
                return;

            _actors.Bind(actorId, new ActorBinding
            {
                BindingType = actorRef.BindingType,
                BindingKey = actorRef.BindingKey,
                SourceModId = BuiltInOwner,
                Persistent = true
            }, replaceExisting);
        }

        private static ActorProfileComponent BuildProfile(ScenarioActorRef actorRef, FamilyMemberConfig familyMember, ScenarioNpcDefinition npc)
        {
            ActorProfileComponent profile = new ActorProfileComponent();
            string displayName = null;

            if (familyMember != null)
            {
                displayName = familyMember.Name;
                profile.FirstName = familyMember.Name;
                profile.IsMale = familyMember.Gender == ScenarioGender.Male;
                if (familyMember.Appearance != null)
                    profile.MeshId = familyMember.Appearance.MeshId;
                ApplyFamilyStats(profile, familyMember);
            }
            else if (npc != null)
            {
                displayName = npc.CharacterId;
                profile.FirstName = npc.CharacterId;
                profile.MeshId = npc.PresetId;
                if (npc.Stats != null)
                {
                    profile.StrengthLevel = npc.Stats.Strength;
                    profile.DexterityLevel = npc.Stats.Dexterity;
                    profile.IntelligenceLevel = npc.Stats.Intelligence;
                    profile.CharismaLevel = npc.Stats.Charisma;
                    profile.PerceptionLevel = npc.Stats.Perception;
                }
            }

            if (string.IsNullOrEmpty(profile.FirstName) && actorRef != null)
                profile.FirstName = actorRef.DisplayNameFallback;
            if (string.IsNullOrEmpty(profile.FirstName))
                profile.FirstName = displayName;

            return string.IsNullOrEmpty(profile.FirstName) && string.IsNullOrEmpty(profile.MeshId) ? null : profile;
        }

        private static ActorAttributeSetComponent BuildAttributes(FamilyMemberConfig familyMember, ScenarioNpcDefinition npc)
        {
            ActorAttributeSetComponent attributes = new ActorAttributeSetComponent();
            if (familyMember != null)
            {
                for (int i = 0; familyMember.Stats != null && i < familyMember.Stats.Count; i++)
                {
                    StatOverride stat = familyMember.Stats[i];
                    if (stat != null && !string.IsNullOrEmpty(stat.StatId))
                        attributes.SetValue(stat.StatId, stat.Value, BuiltInOwner);
                }
            }
            else if (npc != null && npc.Stats != null)
            {
                AddNpcAttribute(attributes, "Strength", npc.Stats.Strength);
                AddNpcAttribute(attributes, "Dexterity", npc.Stats.Dexterity);
                AddNpcAttribute(attributes, "Intelligence", npc.Stats.Intelligence);
                AddNpcAttribute(attributes, "Charisma", npc.Stats.Charisma);
                AddNpcAttribute(attributes, "Perception", npc.Stats.Perception);
            }

            return attributes;
        }

        private static void AddNpcAttribute(ActorAttributeSetComponent attributes, string name, int value)
        {
            if (attributes != null && value != 0)
                attributes.SetValue(name, value, BuiltInOwner);
        }

        private static void ApplyFamilyStats(ActorProfileComponent profile, FamilyMemberConfig familyMember)
        {
            for (int i = 0; profile != null && familyMember != null && familyMember.Stats != null && i < familyMember.Stats.Count; i++)
            {
                StatOverride stat = familyMember.Stats[i];
                if (stat == null || string.IsNullOrEmpty(stat.StatId))
                    continue;

                if (string.Equals(stat.StatId, "Strength", StringComparison.OrdinalIgnoreCase))
                    profile.StrengthLevel = stat.Value;
                else if (string.Equals(stat.StatId, "Dexterity", StringComparison.OrdinalIgnoreCase))
                    profile.DexterityLevel = stat.Value;
                else if (string.Equals(stat.StatId, "Intelligence", StringComparison.OrdinalIgnoreCase))
                    profile.IntelligenceLevel = stat.Value;
                else if (string.Equals(stat.StatId, "Charisma", StringComparison.OrdinalIgnoreCase))
                    profile.CharismaLevel = stat.Value;
                else if (string.Equals(stat.StatId, "Perception", StringComparison.OrdinalIgnoreCase))
                    profile.PerceptionLevel = stat.Value;
            }
        }

        private static bool TryBuildActorId(ScenarioActorRef actorRef, out ActorId actorId)
        {
            actorId = null;
            if (actorRef == null || string.IsNullOrEmpty(actorRef.Kind))
                return false;

            ActorKind kind;
            try
            {
                kind = (ActorKind)Enum.Parse(typeof(ActorKind), actorRef.Kind, true);
            }
            catch
            {
                return false;
            }

            actorId = new ActorId(kind, actorRef.LocalId, actorRef.Domain ?? string.Empty);
            return true;
        }

        private static bool IsScenarioOwnedSynthetic(ScenarioDefinition definition, ScenarioActorRef actorRef, ActorId actorId)
        {
            if (actorId == null || actorId.Kind != ActorKind.Synthetic)
                return false;

            string scenarioDomain = ResolveScenarioDomain(definition);
            return string.Equals(actorId.Domain ?? string.Empty, scenarioDomain, StringComparison.OrdinalIgnoreCase)
                || (actorRef != null
                    && actorRef.BindingType != null
                    && actorRef.BindingType.StartsWith("sheltered.scenario.", StringComparison.OrdinalIgnoreCase));
        }

        private static ScenarioActorRef BuildLegacyStartingMemberRef(ScenarioDefinition definition, FamilyMemberConfig member, int memberIndex)
        {
            string domain = ResolveScenarioDomain(definition);
            string key = domain + ":start:" + Math.Max(0, memberIndex).ToString();
            return new ScenarioActorRef
            {
                Kind = ActorKind.Synthetic.ToString(),
                LocalId = DeterministicLocalId(domain + "|member|" + Math.Max(0, memberIndex).ToString()),
                Domain = domain,
                BindingType = ScenarioCastBindingType,
                BindingKey = key,
                DisplayNameFallback = member != null ? member.Name : null
            };
        }

        private static ScenarioActorRef BuildLegacyFutureSurvivorRef(ScenarioDefinition definition, FutureSurvivorDefinition survivor, int survivorIndex)
        {
            string domain = ResolveScenarioDomain(definition);
            string id = survivor != null && !string.IsNullOrEmpty(survivor.Id)
                ? survivor.Id
                : "future_" + Math.Max(0, survivorIndex).ToString();
            return new ScenarioActorRef
            {
                Kind = ActorKind.Synthetic.ToString(),
                LocalId = DeterministicLocalId(domain + "|future|" + id),
                Domain = domain,
                BindingType = ScenarioFutureSurvivorBindingType,
                BindingKey = domain + ":" + id,
                DisplayNameFallback = survivor != null && survivor.Survivor != null ? survivor.Survivor.Name : id
            };
        }

        private static ScenarioActorRef BuildLegacyNpcRef(ScenarioDefinition definition, ScenarioNpcDefinition npc, int npcIndex)
        {
            string domain = ResolveScenarioDomain(definition);
            string id = npc != null && !string.IsNullOrEmpty(npc.CharacterId)
                ? npc.CharacterId
                : "npc_" + Math.Max(0, npcIndex).ToString();
            return new ScenarioActorRef
            {
                Kind = ActorKind.Synthetic.ToString(),
                LocalId = DeterministicLocalId(domain + "|npc|" + id),
                Domain = domain,
                BindingType = ScenarioNpcBindingType,
                BindingKey = domain + ":" + id,
                DisplayNameFallback = id
            };
        }

        private static int DeterministicLocalId(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                string text = value ?? string.Empty;
                for (int i = 0; i < text.Length; i++)
                {
                    hash ^= text[i];
                    hash *= 16777619u;
                }

                int result = (int)(hash & 0x7fffffff);
                return result == 0 ? 1 : result;
            }
        }

        private static string ResolveScenarioDomain(ScenarioDefinition definition)
        {
            return definition != null && !string.IsNullOrEmpty(definition.Id)
                ? definition.Id
                : "scenario";
        }

        private static string JoinName(string firstName, string lastName)
        {
            if (string.IsNullOrEmpty(firstName))
                return lastName ?? string.Empty;
            if (string.IsNullOrEmpty(lastName))
                return firstName;
            return firstName + " " + lastName;
        }
    }
}
