using System;
using System.Collections;
using System.Collections.Generic;
using ModAPI.Actors;
using ModAPI.Core;
using ShelteredAPI.Actors;
using ShelteredAPI.Persistence;

namespace ShelteredAPI.Storage
{
    internal sealed class CharacterItemAssignmentService : ICharacterItemAssignmentService
    {
        internal static readonly CharacterItemAssignmentService Instance = new CharacterItemAssignmentService();

        private readonly object _sync = new object();
        private readonly List<CharacterItemAssignment> _assignments = new List<CharacterItemAssignment>();
        private readonly Dictionary<string, IItemStore> _knownSources = new Dictionary<string, IItemStore>(StringComparer.OrdinalIgnoreCase);

        private CharacterItemAssignmentService()
        {
        }

        public CharacterItemAssignment Assign(
            ActorId actorId,
            IItemStore source,
            string itemId,
            int quantity,
            CharacterItemAssignmentKind kind,
            CharacterItemSlot slot)
        {
            CharacterItemAssignmentPersistence.EnsureRegistered();
            ActorId resolvedActorId = RequireKnownActor(actorId);
            return AssignResolvedActor(
                resolvedActorId,
                BuildActorDisplayName(resolvedActorId),
                source,
                itemId,
                quantity,
                kind,
                slot);
        }

        public CharacterItemAssignment Assign(
            FamilyMember member,
            IItemStore source,
            string itemId,
            int quantity,
            CharacterItemAssignmentKind kind,
            CharacterItemSlot slot)
        {
            CharacterItemAssignmentPersistence.EnsureRegistered();

            if (member == null)
                throw new ArgumentNullException("member");
            ActorId actorId = ResolveFamilyMemberActorId(member);
            return AssignResolvedActor(
                actorId,
                BuildMemberDisplayName(member),
                source,
                itemId,
                quantity,
                kind,
                slot);
        }

        private CharacterItemAssignment AssignResolvedActor(
            ActorId actorId,
            string displayName,
            IItemStore source,
            string itemId,
            int quantity,
            CharacterItemAssignmentKind kind,
            CharacterItemSlot slot)
        {
            if (actorId == null)
                throw new ArgumentNullException("actorId");
            if (source == null)
                throw new ArgumentNullException("source");
            if (string.IsNullOrEmpty(itemId))
                throw new ArgumentException("Item ID is required.", "itemId");
            if (quantity <= 0)
                throw new ArgumentOutOfRangeException("quantity", "Quantity must be greater than zero.");

            string sourceStoreId = source.StoreId;
            if (string.IsNullOrEmpty(sourceStoreId))
                throw new ArgumentException("Source store must expose a stable StoreId.", "source");

            lock (_sync)
            {
                RememberSource(source);

                int storeCount = GetAssignableCount(source, itemId);
                if (storeCount < quantity)
                    throw new InvalidOperationException("Source store does not contain the requested item quantity.");

                int alreadyAssigned = GetAssignedCountInStore(sourceStoreId, itemId);
                if (storeCount - alreadyAssigned < quantity)
                    throw new InvalidOperationException("Source store does not have enough unassigned quantity for this item.");

                CharacterItemAssignment assignment = new CharacterItemAssignment
                {
                    AssignmentId = "assignment." + Guid.NewGuid().ToString("N"),
                    ActorId = CloneActorId(actorId),
                    MemberDisplayName = string.IsNullOrEmpty(displayName) ? BuildActorKey(actorId) : displayName,
                    SourceStoreId = sourceStoreId,
                    SourceStoreName = source.DisplayName,
                    SourceStoreKind = source.Kind,
                    ItemId = itemId,
                    Quantity = quantity,
                    Kind = kind,
                    Slot = slot
                };

                _assignments.Add(assignment);
                return assignment.Clone();
            }
        }

        public bool Unassign(string assignmentId)
        {
            CharacterItemAssignmentPersistence.EnsureRegistered();

            if (string.IsNullOrEmpty(assignmentId))
                return false;

            lock (_sync)
            {
                for (int i = 0; i < _assignments.Count; i++)
                {
                    CharacterItemAssignment assignment = _assignments[i];
                    if (assignment != null && string.Equals(assignment.AssignmentId, assignmentId, StringComparison.OrdinalIgnoreCase))
                    {
                        _assignments.RemoveAt(i);
                        return true;
                    }
                }
            }

            return false;
        }

        public IList<CharacterItemAssignment> GetAssignments(ActorId actorId)
        {
            CharacterItemAssignmentPersistence.EnsureRegistered();

            ActorId resolvedActorId = RequireKnownActor(actorId);
            List<CharacterItemAssignment> results = new List<CharacterItemAssignment>();

            lock (_sync)
            {
                for (int i = 0; i < _assignments.Count; i++)
                {
                    CharacterItemAssignment assignment = _assignments[i];
                    if (IsActorAssignment(assignment, resolvedActorId))
                        results.Add(assignment.Clone());
                }
            }

            return results;
        }

        public IList<CharacterItemAssignment> GetAssignments(FamilyMember member)
        {
            CharacterItemAssignmentPersistence.EnsureRegistered();

            if (member == null)
                return new List<CharacterItemAssignment>();

            return GetAssignments(ResolveFamilyMemberActorId(member));
        }

        public IList<CharacterItemAssignment> GetAvailableAssignments(ActorId actorId)
        {
            CharacterItemAssignmentPersistence.EnsureRegistered();

            ActorId resolvedActorId = RequireKnownActor(actorId);
            List<CharacterItemAssignment> results = new List<CharacterItemAssignment>();

            lock (_sync)
            {
                for (int i = 0; i < _assignments.Count; i++)
                {
                    CharacterItemAssignment assignment = _assignments[i];
                    if (!IsActorAssignment(assignment, resolvedActorId))
                        continue;

                    if (IsAssignmentAvailable(assignment))
                        results.Add(assignment.Clone());
                }
            }

            return results;
        }

        public IList<CharacterItemAssignment> GetAvailableAssignments(FamilyMember member)
        {
            CharacterItemAssignmentPersistence.EnsureRegistered();

            if (member == null)
                return new List<CharacterItemAssignment>();

            return GetAvailableAssignments(ResolveFamilyMemberActorId(member));
        }

        public int GetAssignedCount(ActorId actorId, string itemId)
        {
            CharacterItemAssignmentPersistence.EnsureRegistered();

            ActorId resolvedActorId = RequireKnownActor(actorId);
            if (string.IsNullOrEmpty(itemId))
                return 0;

            int total = 0;
            lock (_sync)
            {
                for (int i = 0; i < _assignments.Count; i++)
                {
                    CharacterItemAssignment assignment = _assignments[i];
                    if (IsActorAssignment(assignment, resolvedActorId)
                        && string.Equals(assignment.ItemId, itemId, StringComparison.OrdinalIgnoreCase))
                    {
                        total += Math.Max(0, assignment.Quantity);
                    }
                }
            }

            return total;
        }

        public int GetAssignedCount(FamilyMember member, string itemId)
        {
            CharacterItemAssignmentPersistence.EnsureRegistered();

            if (member == null)
                return 0;

            return GetAssignedCount(ResolveFamilyMemberActorId(member), itemId);
        }

        public int ReleaseAssignmentsForActor(ActorId actorId)
        {
            CharacterItemAssignmentPersistence.EnsureRegistered();

            ActorId resolvedActorId = RequireKnownActor(actorId);
            int removed = 0;
            lock (_sync)
            {
                for (int i = _assignments.Count - 1; i >= 0; i--)
                {
                    CharacterItemAssignment assignment = _assignments[i];
                    if (IsActorAssignment(assignment, resolvedActorId))
                    {
                        _assignments.RemoveAt(i);
                        removed++;
                    }
                }
            }

            return removed;
        }

        public int ReleaseAssignmentsForMember(FamilyMember member)
        {
            CharacterItemAssignmentPersistence.EnsureRegistered();

            if (member == null)
                return 0;

            return ReleaseAssignmentsForActor(ResolveFamilyMemberActorId(member));
        }

        internal void EnsureRegistered()
        {
            CharacterItemAssignmentPersistence.EnsureRegistered();
        }

        internal List<CharacterItemAssignment> Snapshot()
        {
            lock (_sync)
            {
                List<CharacterItemAssignment> snapshot = new List<CharacterItemAssignment>();
                for (int i = 0; i < _assignments.Count; i++)
                {
                    if (_assignments[i] != null)
                        snapshot.Add(_assignments[i].Clone());
                }

                return snapshot;
            }
        }

        internal void ReplaceAll(List<CharacterItemAssignment> assignments)
        {
            lock (_sync)
            {
                _assignments.Clear();
                for (int i = 0; assignments != null && i < assignments.Count; i++)
                {
                    CharacterItemAssignment assignment = assignments[i];
                    if (IsValidLoadedAssignment(assignment))
                        _assignments.Add(assignment.Clone());
                }
            }
        }

        private bool IsAssignmentAvailable(CharacterItemAssignment assignment)
        {
            IItemStore source = ResolveSource(assignment);
            if (source == null || string.IsNullOrEmpty(assignment.ItemId) || assignment.Quantity <= 0)
                return false;

            return GetAssignableCount(source, assignment.ItemId) >= GetAssignedCountInStore(assignment.SourceStoreId, assignment.ItemId);
        }

        private IItemStore ResolveSource(CharacterItemAssignment assignment)
        {
            if (assignment == null || string.IsNullOrEmpty(assignment.SourceStoreId))
                return null;

            IItemStore source;
            if (_knownSources.TryGetValue(assignment.SourceStoreId, out source) && source != null)
                return source;

            if (ShelteredStores.TryResolveStore(assignment.SourceStoreId, assignment.SourceStoreKind, out source) && source != null)
            {
                RememberSource(source);
                return source;
            }

            return null;
        }

        private void RememberSource(IItemStore source)
        {
            if (source != null && !string.IsNullOrEmpty(source.StoreId))
                _knownSources[source.StoreId] = source;
        }

        private int GetAssignedCountInStore(string sourceStoreId, string itemId)
        {
            if (string.IsNullOrEmpty(sourceStoreId) || string.IsNullOrEmpty(itemId))
                return 0;

            int total = 0;
            for (int i = 0; i < _assignments.Count; i++)
            {
                CharacterItemAssignment assignment = _assignments[i];
                if (assignment != null
                    && string.Equals(assignment.SourceStoreId, sourceStoreId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(assignment.ItemId, itemId, StringComparison.OrdinalIgnoreCase))
                {
                    total += Math.Max(0, assignment.Quantity);
                }
            }

            return total;
        }

        private static bool IsValidLoadedAssignment(CharacterItemAssignment assignment)
        {
            return assignment != null
                && !string.IsNullOrEmpty(assignment.AssignmentId)
                && assignment.ActorId != null
                && !string.IsNullOrEmpty(assignment.SourceStoreId)
                && !string.IsNullOrEmpty(assignment.ItemId)
                && assignment.Quantity > 0;
        }

        private static int GetAssignableCount(IItemStore source, string itemId)
        {
            if (source == null)
                return 0;

            try
            {
                System.Reflection.MethodInfo method = source.GetType().GetMethod(
                    "GetAvailableCount",
                    new Type[] { typeof(string) });
                if (method != null && method.ReturnType == typeof(int))
                    return (int)method.Invoke(source, new object[] { itemId });
            }
            catch
            {
            }

            return source.GetCount(itemId);
        }

        private static ActorId ResolveFamilyMemberActorId(FamilyMember member)
        {
            if (member == null)
                throw new ArgumentNullException("member");

            int id = member.GetId();
            if (id < 0)
                throw new InvalidOperationException("Family member does not expose a stable actor ID.");

            ActorId actorId = ShelteredActors.FamilyMemberActorId(id);
            EnsureActorRegistered(actorId);
            return actorId;
        }

        private static ActorId RequireKnownActor(ActorId actorId)
        {
            if (actorId == null)
                throw new ArgumentNullException("actorId");

            EnsureActorRegistered(actorId);
            return CloneActorId(actorId);
        }

        private static void EnsureActorRegistered(ActorId actorId)
        {
            if (actorId == null)
                return;

            IActorRecord existing;
            if (ShelteredActors.Instance.TryGet(actorId, out existing))
                return;

            ShelteredActors.Instance.Ensure(new ActorCreateRequest
            {
                Id = CloneActorId(actorId),
                Kind = actorId.Kind,
                Domain = actorId.Domain,
                LifecycleState = ResolveInitialLifecycleState(actorId),
                PresenceState = ActorPresenceState.Unknown,
                Flags = ResolveInitialFlags(actorId),
                Origin = ActorOrigin.Core(ResolveActorOriginKey(actorId))
            });
        }

        private static ActorLifecycleState ResolveInitialLifecycleState(ActorId actorId)
        {
            if (actorId != null && (actorId.Kind == ActorKind.Player || actorId.Kind == ActorKind.Visitor))
                return ActorLifecycleState.Active;

            return ActorLifecycleState.Registered;
        }

        private static ActorFlags ResolveInitialFlags(ActorId actorId)
        {
            ActorFlags flags = ActorFlags.Persistent;
            if (actorId != null && (actorId.Kind == ActorKind.Player || actorId.Kind == ActorKind.Visitor))
                flags |= ActorFlags.Loaded;
            if (actorId != null && actorId.Kind == ActorKind.Synthetic)
                flags |= ActorFlags.Synthetic;
            return flags;
        }

        private static string ResolveActorOriginKey(ActorId actorId)
        {
            if (actorId == null)
                return "character-items";
            if (actorId.Kind == ActorKind.Player)
                return "family";
            if (actorId.Kind == ActorKind.Visitor)
                return "visitor";
            return "character-items";
        }

        private static bool IsActorAssignment(CharacterItemAssignment assignment, ActorId actorId)
        {
            return assignment != null
                && assignment.ActorId != null
                && assignment.ActorId.Equals(actorId);
        }

        private static string BuildActorKey(ActorId actorId)
        {
            return actorId == null ? string.Empty : actorId.ToString();
        }

        private static string BuildActorDisplayName(ActorId actorId)
        {
            return BuildActorKey(actorId);
        }

        private static ActorId CloneActorId(ActorId actorId)
        {
            return actorId == null ? null : new ActorId(actorId.Kind, actorId.LocalId, actorId.Domain);
        }

        private static string BuildMemberDisplayName(FamilyMember member)
        {
            if (member == null)
                return string.Empty;

            string name = (SafeText(member.firstName) + " " + SafeText(member.lastName)).Trim();
            return string.IsNullOrEmpty(name) ? BuildActorKey(ResolveFamilyMemberActorId(member)) : name;
        }

        private static string SafeText(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Trim().Replace(" ", "_");
        }
    }

    internal sealed class CharacterItemAssignmentPersistence : ISaveable
    {
        private const string GroupName = "ShelteredAPI_CharacterItemAssignments";
        private const string AssignmentsKey = "assignments";
        private static readonly CharacterItemAssignmentPersistence Instance = new CharacterItemAssignmentPersistence();
        private static bool _registered;

        private CharacterItemAssignmentPersistence()
        {
        }

        public static void EnsureRegistered()
        {
            if (_registered)
                return;

            ModPersistence.Register(Instance);
            _registered = true;
        }

        public bool IsReadyForLoad() { return true; }
        public bool IsRelocationEnabled() { return true; }

        public bool SaveLoad(SaveData data)
        {
            if (data == null)
                return false;

            data.GroupStart(GroupName);
            try
            {
                List<CharacterItemAssignment> entries = data.isSaving
                    ? CharacterItemAssignmentService.Instance.Snapshot()
                    : new List<CharacterItemAssignment>();

                data.SaveLoadList(AssignmentsKey, (IList)entries,
                    i => SaveLoadEntry(data, entries[i]),
                    i =>
                    {
                        CharacterItemAssignment entry = new CharacterItemAssignment();
                        SaveLoadEntry(data, entry);
                        entries.Add(entry);
                    });

                if (data.isLoading)
                    CharacterItemAssignmentService.Instance.ReplaceAll(entries);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("CharacterItemAssignmentPersistence.SaveLoad", "Character item assignment persistence failed: " + ex.Message);
            }
            finally
            {
                data.GroupEnd();
            }

            return true;
        }

        private static void SaveLoadEntry(SaveData data, CharacterItemAssignment entry)
        {
            string assignmentId = entry.AssignmentId;
            string actorKind = entry.ActorId != null ? entry.ActorId.Kind.ToString() : string.Empty;
            int actorLocalId = entry.ActorId != null ? entry.ActorId.LocalId : -1;
            string actorDomain = entry.ActorId != null ? entry.ActorId.Domain : string.Empty;
            string memberDisplayName = entry.MemberDisplayName;
            string sourceStoreId = entry.SourceStoreId;
            string sourceStoreName = entry.SourceStoreName;
            string sourceStoreKind = entry.SourceStoreKind.ToString();
            string itemId = entry.ItemId;
            int quantity = entry.Quantity;
            string kind = entry.Kind.ToString();
            string slot = entry.Slot.ToString();

            data.GroupStart("assignment");
            data.SaveLoad("assignmentId", ref assignmentId);
            data.SaveLoad("actorKind", ref actorKind);
            data.SaveLoad("actorLocalId", ref actorLocalId);
            data.SaveLoad("actorDomain", ref actorDomain);
            data.SaveLoad("memberDisplayName", ref memberDisplayName);
            data.SaveLoad("sourceStoreId", ref sourceStoreId);
            data.SaveLoad("sourceStoreName", ref sourceStoreName);
            data.SaveLoad("sourceStoreKind", ref sourceStoreKind);
            data.SaveLoad("itemId", ref itemId);
            data.SaveLoad("quantity", ref quantity);
            data.SaveLoad("kind", ref kind);
            data.SaveLoad("slot", ref slot);
            data.GroupEnd();

            entry.AssignmentId = assignmentId;
            ActorKind parsedActorKind = ParseEnum(actorKind, ActorKind.Custom);
            entry.ActorId = actorLocalId >= 0 ? new ActorId(parsedActorKind, actorLocalId, actorDomain) : null;
            entry.MemberDisplayName = memberDisplayName;
            entry.SourceStoreId = sourceStoreId;
            entry.SourceStoreName = sourceStoreName;
            entry.SourceStoreKind = ParseEnum(sourceStoreKind, ItemStoreKind.Unknown);
            entry.ItemId = itemId;
            entry.Quantity = quantity;
            entry.Kind = ParseEnum(kind, CharacterItemAssignmentKind.Assigned);
            entry.Slot = ParseEnum(slot, CharacterItemSlot.None);
        }

        private static T ParseEnum<T>(string value, T fallback)
        {
            if (string.IsNullOrEmpty(value))
                return fallback;

            try
            {
                return (T)Enum.Parse(typeof(T), value, true);
            }
            catch
            {
                return fallback;
            }
        }
    }
}
