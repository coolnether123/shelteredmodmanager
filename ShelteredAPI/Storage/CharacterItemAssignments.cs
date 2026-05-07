using System;
using System.Collections;
using System.Collections.Generic;
using ModAPI.Core;
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
            if (source == null)
                throw new ArgumentNullException("source");
            if (string.IsNullOrEmpty(itemId))
                throw new ArgumentException("Item ID is required.", "itemId");
            if (quantity <= 0)
                throw new ArgumentOutOfRangeException("quantity", "Quantity must be greater than zero.");

            string memberKey = BuildMemberKey(member);
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
                    MemberKey = memberKey,
                    MemberDisplayName = BuildMemberDisplayName(member),
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

        public IList<CharacterItemAssignment> GetAssignments(FamilyMember member)
        {
            CharacterItemAssignmentPersistence.EnsureRegistered();

            string memberKey = BuildMemberKey(member);
            List<CharacterItemAssignment> results = new List<CharacterItemAssignment>();
            if (string.IsNullOrEmpty(memberKey))
                return results;

            lock (_sync)
            {
                for (int i = 0; i < _assignments.Count; i++)
                {
                    CharacterItemAssignment assignment = _assignments[i];
                    if (assignment != null && string.Equals(assignment.MemberKey, memberKey, StringComparison.OrdinalIgnoreCase))
                        results.Add(assignment.Clone());
                }
            }

            return results;
        }

        public IList<CharacterItemAssignment> GetAvailableAssignments(FamilyMember member)
        {
            CharacterItemAssignmentPersistence.EnsureRegistered();

            string memberKey = BuildMemberKey(member);
            List<CharacterItemAssignment> results = new List<CharacterItemAssignment>();
            if (string.IsNullOrEmpty(memberKey))
                return results;

            lock (_sync)
            {
                for (int i = 0; i < _assignments.Count; i++)
                {
                    CharacterItemAssignment assignment = _assignments[i];
                    if (assignment == null || !string.Equals(assignment.MemberKey, memberKey, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (IsAssignmentAvailable(assignment))
                        results.Add(assignment.Clone());
                }
            }

            return results;
        }

        public int GetAssignedCount(FamilyMember member, string itemId)
        {
            CharacterItemAssignmentPersistence.EnsureRegistered();

            string memberKey = BuildMemberKey(member);
            if (string.IsNullOrEmpty(memberKey) || string.IsNullOrEmpty(itemId))
                return 0;

            int total = 0;
            lock (_sync)
            {
                for (int i = 0; i < _assignments.Count; i++)
                {
                    CharacterItemAssignment assignment = _assignments[i];
                    if (assignment != null
                        && string.Equals(assignment.MemberKey, memberKey, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(assignment.ItemId, itemId, StringComparison.OrdinalIgnoreCase))
                    {
                        total += Math.Max(0, assignment.Quantity);
                    }
                }
            }

            return total;
        }

        public int ReleaseAssignmentsForMember(FamilyMember member)
        {
            CharacterItemAssignmentPersistence.EnsureRegistered();

            string memberKey = BuildMemberKey(member);
            if (string.IsNullOrEmpty(memberKey))
                return 0;

            int removed = 0;
            lock (_sync)
            {
                for (int i = _assignments.Count - 1; i >= 0; i--)
                {
                    CharacterItemAssignment assignment = _assignments[i];
                    if (assignment != null && string.Equals(assignment.MemberKey, memberKey, StringComparison.OrdinalIgnoreCase))
                    {
                        _assignments.RemoveAt(i);
                        removed++;
                    }
                }
            }

            return removed;
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
                && !string.IsNullOrEmpty(assignment.MemberKey)
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

        internal static string BuildMemberKey(FamilyMember member)
        {
            if (member == null)
                return string.Empty;

            try
            {
                int id = member.GetId();
                if (id >= 0)
                    return "family." + id.ToString();
            }
            catch
            {
            }

            string firstName = SafeText(member.firstName);
            string lastName = SafeText(member.lastName);
            if (!string.IsNullOrEmpty(firstName) || !string.IsNullOrEmpty(lastName))
                return "family.name." + firstName + "." + lastName;

            try
            {
                return "family.instance." + member.GetInstanceID().ToString();
            }
            catch
            {
                return "family.unknown";
            }
        }

        private static string BuildMemberDisplayName(FamilyMember member)
        {
            if (member == null)
                return string.Empty;

            string name = (SafeText(member.firstName) + " " + SafeText(member.lastName)).Trim();
            return string.IsNullOrEmpty(name) ? BuildMemberKey(member) : name;
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
            string sourceStoreKind = entry.SourceStoreKind.ToString();
            string kind = entry.Kind.ToString();
            string slot = entry.Slot.ToString();

            data.GroupStart("assignment");
            data.SaveLoad("assignmentId", ref entry.AssignmentId);
            data.SaveLoad("memberKey", ref entry.MemberKey);
            data.SaveLoad("memberDisplayName", ref entry.MemberDisplayName);
            data.SaveLoad("sourceStoreId", ref entry.SourceStoreId);
            data.SaveLoad("sourceStoreName", ref entry.SourceStoreName);
            data.SaveLoad("sourceStoreKind", ref sourceStoreKind);
            data.SaveLoad("itemId", ref entry.ItemId);
            data.SaveLoad("quantity", ref entry.Quantity);
            data.SaveLoad("kind", ref kind);
            data.SaveLoad("slot", ref slot);
            data.GroupEnd();

            entry.SourceStoreKind = ParseEnum(sourceStoreKind, ItemStoreKind.Unknown);
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
