using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModAPI.Actors
{
    public enum ActorConflictPolicy
    {
        Replace = 0,
        Merge = 1,
        Reject = 2
    }

    public enum ActorComponentWriteResult
    {
        Added = 0,
        Updated = 1,
        Replaced = 2,
        Merged = 3,
        Rejected = 4,
        MissingActor = 5
    }

    public interface IActorComponent
    {
        string ComponentId { get; }
        int Version { get; }
        ActorConflictPolicy ConflictPolicy { get; }
    }

    public interface IMergeableActorComponent : IActorComponent
    {
        IActorComponent Merge(IActorComponent existing);
    }

    public interface IActorComponentSerializer
    {
        string ComponentId { get; }
        Type ComponentType { get; }
        int CurrentVersion { get; }

        string Serialize(IActorComponent component);
        IActorComponent Deserialize(string payload, int storedVersion);
    }

    public sealed class ActorJsonComponentSerializer<TComponent> : IActorComponentSerializer
        where TComponent : class, IActorComponent, new()
    {
        private readonly string _componentId;
        private readonly int _currentVersion;

        public ActorJsonComponentSerializer(string componentId, int currentVersion)
        {
            _componentId = componentId ?? string.Empty;
            _currentVersion = currentVersion < 1 ? 1 : currentVersion;
        }

        public string ComponentId { get { return _componentId; } }
        public Type ComponentType { get { return typeof(TComponent); } }
        public int CurrentVersion { get { return _currentVersion; } }

        public string Serialize(IActorComponent component)
        {
            if (component == null) return string.Empty;
            return JsonUtility.ToJson(component);
        }

        public IActorComponent Deserialize(string payload, int storedVersion)
        {
            if (string.IsNullOrEmpty(payload))
                return new TComponent();

            TComponent component = null;
            try { component = JsonUtility.FromJson<TComponent>(payload); }
            catch { component = null; }
            return component ?? new TComponent();
        }
    }

    [Serializable]
    public sealed class ActorProfileComponent : IActorComponent
    {
        public const string DefaultComponentId = "sheltered.actor_profile";

        public string FirstName;
        public string LastName;
        public bool IsMale;
        public string MeshId;
        public Color SkinColor;
        public Color HairColor;
        public int StrengthLevel;
        public int DexterityLevel;
        public int IntelligenceLevel;
        public int CharismaLevel;
        public int PerceptionLevel;
        public int Health;
        public int MaxHealth;

        public string ComponentId { get { return DefaultComponentId; } }
        public int Version { get { return 1; } }
        public ActorConflictPolicy ConflictPolicy { get { return ActorConflictPolicy.Replace; } }
    }

    [Serializable]
    public sealed class ActorAttributeEntry
    {
        public string Name;
        public float Value;
        public string SourceModId;
    }

    [Serializable]
    public sealed class ActorAttributeSetComponent : IMergeableActorComponent
    {
        public const string DefaultComponentId = "sheltered.attribute_set";

        public List<ActorAttributeEntry> Entries = new List<ActorAttributeEntry>();

        public string ComponentId { get { return DefaultComponentId; } }
        public int Version { get { return 1; } }
        public ActorConflictPolicy ConflictPolicy { get { return ActorConflictPolicy.Merge; } }

        public float GetValue(string name)
        {
            if (string.IsNullOrEmpty(name) || Entries == null) return 0f;

            float total = 0f;
            for (int i = 0; i < Entries.Count; i++)
            {
                ActorAttributeEntry entry = Entries[i];
                if (entry == null) continue;
                if (string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase))
                    total += entry.Value;
            }
            return total;
        }

        public void SetValue(string name, float value, string sourceModId)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (Entries == null) Entries = new List<ActorAttributeEntry>();

            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                ActorAttributeEntry entry = Entries[i];
                if (entry == null) continue;
                if (string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(entry.SourceModId ?? string.Empty, sourceModId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    entry.Value = value;
                    return;
                }
            }

            Entries.Add(new ActorAttributeEntry
            {
                Name = name,
                Value = value,
                SourceModId = sourceModId ?? string.Empty
            });
        }

        public bool Remove(string name, string sourceModId)
        {
            if (string.IsNullOrEmpty(name) || Entries == null) return false;

            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                ActorAttributeEntry entry = Entries[i];
                if (entry == null) continue;
                if (!string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.Equals(entry.SourceModId ?? string.Empty, sourceModId ?? string.Empty, StringComparison.OrdinalIgnoreCase)) continue;
                Entries.RemoveAt(i);
                return true;
            }
            return false;
        }

        public IActorComponent Merge(IActorComponent existing)
        {
            ActorAttributeSetComponent merged = new ActorAttributeSetComponent();
            ActorAttributeSetComponent current = existing as ActorAttributeSetComponent;

            if (current != null && current.Entries != null)
            {
                for (int i = 0; i < current.Entries.Count; i++)
                {
                    ActorAttributeEntry entry = current.Entries[i];
                    if (entry == null) continue;
                    merged.SetValue(entry.Name, entry.Value, entry.SourceModId);
                }
            }

            if (Entries != null)
            {
                for (int i = 0; i < Entries.Count; i++)
                {
                    ActorAttributeEntry entry = Entries[i];
                    if (entry == null) continue;
                    merged.SetValue(entry.Name, entry.Value, entry.SourceModId);
                }
            }

            return merged;
        }
    }
}
