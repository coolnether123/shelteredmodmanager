using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using ModAPI.Util;

namespace ModAPI.Characters.Internal
{
    internal sealed class LiveCharacterProxy : ICharacterProxy
    {
        private FamilyMember _member;
        private NpcVisitor _npc;
        private readonly CharacterSource _source;
        private readonly CharacterDataModel _data;
        private readonly BasicCharacterEffects _effects;
        private readonly BasicCharacterAttributes _attributes;
        private readonly Action<ICharacterProxy, string, object> _dataChanged;

        public int UniqueId { get; private set; }
        public string Name
        {
            get
            {
                string full = (_data.FirstName + " " + _data.LastName).Trim();
                return string.IsNullOrEmpty(full) ? _data.PersistenceKey : full;
            }
        }
        public CharacterState State { get; private set; }
        public CharacterLocation Location
        {
            get
            {
                if (_source == CharacterSource.RealFamily)
                {
                    if (_member == null) return CharacterLocation.Unknown;
                    if (_member.isMissing) return CharacterLocation.Missing;
                    if (_member.isAway) return CharacterLocation.Expedition;
                    return CharacterLocation.Shelter;
                }

                if (_source == CharacterSource.Visitor)
                {
                    if (_npc == null) return CharacterLocation.Unknown;
                    return CharacterLocation.Shelter;
                }

                return CharacterLocation.Unknown;
            }
        }
        public FamilyMember UnderlyingMember { get { return _member; } }
        public NpcVisitor UnderlyingNpc { get { return _npc; } }
        public bool IsActive { get; private set; }
        public bool IsPersistent { get { return _data.IsPersistent; } }
        public bool IsLoadedOnShelterEntry { get; private set; }
        public string PersistenceKey { get { return _data.PersistenceKey; } }
        public CharacterSource Source { get { return _source; } }
        public string SourceMod { get { return _data.SourceMod; } }
        public ICharacterEffects Effects { get { return _effects; } }
        public ICharacterAttributes Attributes { get { return _attributes; } }
        public ICharacterData Data { get { return _data; } }

        public event Action<ICharacterProxy> OnUnregistered;

        internal LiveCharacterProxy(FamilyMember member, Action<ICharacterProxy, string, object> dataChanged)
        {
            _member = member;
            _source = CharacterSource.RealFamily;
            _dataChanged = dataChanged;
            UniqueId = member != null ? member.GetId() : -1;
            _data = CharacterFactory.CreateFromFamilyMember(member);
            _effects = new BasicCharacterEffects(this);
            _attributes = new BasicCharacterAttributes();
            IsActive = true;
            IsLoadedOnShelterEntry = true;
            RefreshState();
        }

        internal LiveCharacterProxy(NpcVisitor npc, Action<ICharacterProxy, string, object> dataChanged)
        {
            _npc = npc;
            _source = CharacterSource.Visitor;
            _dataChanged = dataChanged;
            UniqueId = npc != null ? npc.npcId : -1;
            _data = CharacterFactory.CreateFromVisitor(npc);
            _effects = new BasicCharacterEffects(this);
            _attributes = new BasicCharacterAttributes();
            IsActive = true;
            IsLoadedOnShelterEntry = false;
            RefreshState();
        }

        internal void Bind(FamilyMember member)
        {
            _member = member;
            if (member != null)
            {
                CharacterFactory.FillFromFamilyMember(_data, member);
                IsActive = true;
            }
            RefreshState();
        }

        internal void Bind(NpcVisitor npc)
        {
            _npc = npc;
            if (npc != null)
            {
                CharacterFactory.FillFromVisitor(_data, npc);
                IsActive = true;
            }
            RefreshState();
        }

        internal void ClearBinding()
        {
            _member = null;
            _npc = null;
            IsActive = false;
            RefreshState();
        }

        internal void RefreshState()
        {
            if (_source == CharacterSource.RealFamily)
            {
                if (_member == null)
                {
                    State = CharacterState.TemporarilyAbsent;
                    return;
                }
                if (_member.isDead) { State = CharacterState.Dead; return; }
                if (_member.isCatatonic) { State = CharacterState.CatatonicGhost; return; }
                if (_member.IsUnconscious) { State = CharacterState.Unconscious; return; }
                if (_member.isAway) { State = CharacterState.OnExpedition; return; }
                State = CharacterState.InShelter;
                return;
            }

            if (_npc == null) { State = CharacterState.TemporarilyAbsent; return; }
            if (_npc.isDead) { State = CharacterState.Dead; return; }
            State = CharacterState.InShelter;
        }

        internal void Tick(float dt)
        {
            RefreshState();
            SyncToGameObject();
            _effects.Tick(dt);
            _attributes.Tick(dt);
            _dataChanged(this, "state", State);
        }

        internal void SyncToGameObject()
        {
            if (_member != null)
            {
                TrySetField(_member, "m_firstName", _data.FirstName);
                TrySetField(_member, "m_lastName", _data.LastName);
                TrySetField(_member, "m_health", _data.Health);
                TrySetField(_member, "m_maxHealth", _data.MaxHealth);
            }

            if (_npc != null)
            {
                TrySetField(_npc, "m_firstName", _data.FirstName);
                TrySetField(_npc, "m_lastName", _data.LastName);
                TrySetField(_npc, "m_health", _data.Health);
                TrySetField(_npc, "m_maxHealth", _data.MaxHealth);
            }
        }

        internal void Unregister()
        {
            if (!IsActive) return;
            IsActive = false;
            _member = null;
            _npc = null;
            var evt = OnUnregistered;
            if (evt != null) evt(this);
        }

        private static bool TrySetField(object target, string fieldName, object value)
        {
            if (target == null || string.IsNullOrEmpty(fieldName)) return false;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type cursor = target.GetType();
            while (cursor != null)
            {
                FieldInfo field = cursor.GetField(fieldName, flags);
                if (field != null)
                {
                    try
                    {
                        field.SetValue(target, value);
                        return true;
                    }
                    catch { return false; }
                }
                cursor = cursor.BaseType;
            }

            return false;
        }
    }

    internal sealed class BasicCharacterEffects : ICharacterEffects
    {
        private readonly ICharacterProxy _character;
        private readonly List<EffectInstance> _effects = new List<EffectInstance>();

        public int Count { get { return _effects.Count; } }

        internal BasicCharacterEffects(ICharacterProxy character)
        {
            _character = character;
        }

        public EffectInstance Apply<T>(float? duration = null, string sourceModId = null) where T : ICharacterEffect, new()
        {
            ICharacterEffect effect = new T();
            return ApplyInternal(effect, duration ?? effect.Duration, sourceModId);
        }

        public EffectInstance Apply(string effectId, float? duration = null, string sourceModId = null)
        {
            ICharacterEffect effect;
            if (!CharacterEffectSystem.InternalInstance.TryCreateEffect(effectId, out effect) || effect == null)
                return null;
            return ApplyInternal(effect, duration ?? effect.Duration, sourceModId);
        }

        private EffectInstance ApplyInternal(ICharacterEffect effect, float duration, string sourceModId)
        {
            if (effect == null) return null;
            EffectInstance existing = Get(effect.EffectId);
            if (existing != null)
            {
                if (effect.StackBehavior == StackBehavior.Ignore) return existing;
                if (effect.StackBehavior == StackBehavior.Refresh) { existing.TimeRemaining = duration; existing.StackCount++; return existing; }
                if (effect.StackBehavior == StackBehavior.Extend) { existing.TimeRemaining += duration; existing.StackCount++; return existing; }
                if (effect.StackBehavior == StackBehavior.Replace) Remove(existing);
            }

            EffectInstance instance = new EffectInstance
            {
                Effect = effect,
                Duration = duration < 0f ? 0f : duration,
                TimeRemaining = duration < 0f ? 0f : duration,
                TimeApplied = CharacterEffectSystemImpl.NowSeconds(),
                SourceModId = sourceModId ?? string.Empty
            };
            EffectContext ctx = new EffectContext(_character, instance);
            if (!effect.CanApply(ctx)) return null;
            _effects.Add(instance);
            effect.OnApplied(ctx);
            CharacterEffectSystem.InternalInstance.OnEffectApplied(_character, instance);
            return instance;
        }

        public bool Remove(EffectInstance effect, RemovalReason reason = RemovalReason.Manually)
        {
            if (effect == null) return false;
            if (!_effects.Remove(effect)) return false;
            EffectContext ctx = new EffectContext(_character, effect);
            effect.Effect.OnRemoved(ctx, reason);
            CharacterEffectSystem.InternalInstance.OnEffectRemoved(_character, effect, reason);
            return true;
        }

        public int RemoveAllOfType<T>(RemovalReason reason = RemovalReason.Manually) where T : ICharacterEffect
        {
            var toRemove = _effects.Where(x => x.Effect is T).ToList();
            for (int i = 0; i < toRemove.Count; i++) Remove(toRemove[i], reason);
            return toRemove.Count;
        }

        public int RemoveAllOfType(string effectId, RemovalReason reason = RemovalReason.Manually)
        {
            var toRemove = _effects.Where(x => x.Effect != null && string.Equals(x.Effect.EffectId, effectId, StringComparison.OrdinalIgnoreCase)).ToList();
            for (int i = 0; i < toRemove.Count; i++) Remove(toRemove[i], reason);
            return toRemove.Count;
        }

        public int RemoveAllFromMod(string modId, RemovalReason reason = RemovalReason.Manually)
        {
            var toRemove = _effects.Where(x => string.Equals(x.SourceModId, modId, StringComparison.OrdinalIgnoreCase)).ToList();
            for (int i = 0; i < toRemove.Count; i++) Remove(toRemove[i], reason);
            return toRemove.Count;
        }

        public bool Has<T>() where T : ICharacterEffect { return _effects.Any(x => x.Effect is T); }
        public bool Has(string effectId) { return _effects.Any(x => x.Effect != null && string.Equals(x.Effect.EffectId, effectId, StringComparison.OrdinalIgnoreCase)); }
        public EffectInstance Get<T>() where T : ICharacterEffect { return _effects.FirstOrDefault(x => x.Effect is T); }
        public EffectInstance Get(string effectId) { return _effects.FirstOrDefault(x => x.Effect != null && string.Equals(x.Effect.EffectId, effectId, StringComparison.OrdinalIgnoreCase)); }
        public IReadOnlyList<EffectInstance> GetAll<T>() where T : ICharacterEffect { return _effects.Where(x => x.Effect is T).ToList().ToReadOnlyList(); }
        public IReadOnlyList<EffectInstance> GetAll(string effectId) { return _effects.Where(x => x.Effect != null && string.Equals(x.Effect.EffectId, effectId, StringComparison.OrdinalIgnoreCase)).ToList().ToReadOnlyList(); }
        public IReadOnlyList<EffectInstance> GetAll() { return _effects.ToReadOnlyList(); }
        public IReadOnlyList<EffectInstance> GetAllFromMod(string modId) { return _effects.Where(x => string.Equals(x.SourceModId, modId, StringComparison.OrdinalIgnoreCase)).ToList().ToReadOnlyList(); }

        internal void Tick(float dt)
        {
            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                EffectInstance e = _effects[i];
                if (e == null || e.Effect == null) { _effects.RemoveAt(i); continue; }
                if (!e.IsPermanent) e.TimeRemaining -= dt;
                EffectContext ctx = new EffectContext(_character, e);
                e.Effect.OnTick(ctx, dt);
                if (e.IsExpired) Remove(e, RemovalReason.Expired);
            }
        }

        internal List<EffectSaveData> ToSaveData()
        {
            List<EffectSaveData> result = new List<EffectSaveData>(_effects.Count);
            for (int i = 0; i < _effects.Count; i++)
            {
                EffectInstance effect = _effects[i];
                if (effect == null || effect.Effect == null) continue;

                Dictionary<string, string> blob = new Dictionary<string, string>(StringComparer.Ordinal);
                if (effect.CustomData != null)
                {
                    foreach (var kv in effect.CustomData)
                    {
                        blob[kv.Key] = RuntimeSerialization.SerializeToString(kv.Value);
                    }
                }

                result.Add(new EffectSaveData
                {
                    EffectId = effect.Effect.EffectId ?? string.Empty,
                    Duration = effect.Duration,
                    TimeRemaining = effect.TimeRemaining,
                    TimeApplied = effect.TimeApplied,
                    StackCount = effect.StackCount,
                    SourceModId = effect.SourceModId ?? string.Empty,
                    SerializedData = effect.Effect.SerializeData() ?? string.Empty,
                    CustomDataBlob = blob
                });
            }
            return result;
        }

        internal void RestoreFromSaveData(List<EffectSaveData> source)
        {
            _effects.Clear();
            if (source == null) return;

            for (int i = 0; i < source.Count; i++)
            {
                EffectSaveData entry = source[i];
                if (entry == null || string.IsNullOrEmpty(entry.EffectId)) continue;

                ICharacterEffect effect;
                if (!CharacterEffectSystem.InternalInstance.TryCreateEffect(entry.EffectId, out effect) || effect == null) continue;

                try { effect.DeserializeData(entry.SerializedData ?? string.Empty); } catch { }

                EffectInstance instance = new EffectInstance
                {
                    Effect = effect,
                    Duration = entry.Duration,
                    TimeRemaining = entry.TimeRemaining,
                    TimeApplied = entry.TimeApplied,
                    StackCount = entry.StackCount < 1 ? 1 : entry.StackCount,
                    SourceModId = entry.SourceModId ?? string.Empty
                };

                if (entry.CustomDataBlob != null && entry.CustomDataBlob.Count > 0)
                {
                    if (instance.CustomData == null) instance.CustomData = new Dictionary<string, object>();
                    foreach (var kv in entry.CustomDataBlob)
                    {
                        instance.CustomData[kv.Key] = RuntimeSerialization.DeserializeFromString(kv.Value);
                    }
                }

                _effects.Add(instance);
            }
        }
    }

    internal sealed class BasicCharacterAttributes : ICharacterAttributes
    {
        private readonly List<AttributeModifier> _mods = new List<AttributeModifier>();
        public event Action<string> ModifierChanged;

        public AttributeModifier Apply(string attributeName, float value, float duration, string sourceModId)
        {
            AttributeModifier m = new AttributeModifier
            {
                AttributeName = attributeName,
                Value = value,
                Duration = duration < 0f ? 0f : duration,
                TimeRemaining = duration < 0f ? 0f : duration,
                SourceModId = sourceModId ?? string.Empty,
                TimeApplied = CharacterEffectSystemImpl.NowSeconds()
            };
            _mods.Add(m);
            Raise(attributeName);
            return m;
        }

        public bool Remove(AttributeModifier modifier)
        {
            bool removed = _mods.Remove(modifier);
            if (removed) Raise(modifier != null ? modifier.AttributeName : string.Empty);
            return removed;
        }

        public float GetModifier(string attributeName)
        {
            return _mods.Where(x => string.Equals(x.AttributeName, attributeName, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Value);
        }

        public IReadOnlyList<AttributeModifier> GetModifiers(string attributeName)
        {
            return _mods.Where(x => string.Equals(x.AttributeName, attributeName, StringComparison.OrdinalIgnoreCase)).ToList().ToReadOnlyList();
        }

        public int RemoveAllFromMod(string modId)
        {
            var toRemove = _mods.Where(x => string.Equals(x.SourceModId, modId, StringComparison.OrdinalIgnoreCase)).ToList();
            for (int i = 0; i < toRemove.Count; i++) Remove(toRemove[i]);
            return toRemove.Count;
        }

        internal void Tick(float dt)
        {
            for (int i = _mods.Count - 1; i >= 0; i--)
            {
                AttributeModifier m = _mods[i];
                if (!m.IsPermanent) m.TimeRemaining -= dt;
                if (m.IsExpired) { _mods.RemoveAt(i); Raise(m.AttributeName); }
            }
        }

        internal List<AttributeSaveData> ToSaveData()
        {
            List<AttributeSaveData> result = new List<AttributeSaveData>(_mods.Count);
            for (int i = 0; i < _mods.Count; i++)
            {
                AttributeModifier m = _mods[i];
                if (m == null) continue;
                result.Add(new AttributeSaveData
                {
                    AttributeName = m.AttributeName,
                    Value = m.Value,
                    Duration = m.Duration,
                    TimeRemaining = m.TimeRemaining,
                    SourceModId = m.SourceModId
                });
            }
            return result;
        }

        internal void RestoreFromSaveData(List<AttributeSaveData> source)
        {
            _mods.Clear();
            if (source == null) return;
            for (int i = 0; i < source.Count; i++)
            {
                AttributeSaveData m = source[i];
                if (m == null) continue;
                _mods.Add(new AttributeModifier
                {
                    AttributeName = m.AttributeName,
                    Value = m.Value,
                    Duration = m.Duration,
                    TimeRemaining = m.TimeRemaining,
                    SourceModId = m.SourceModId ?? string.Empty,
                    TimeApplied = CharacterEffectSystemImpl.NowSeconds()
                });
            }
        }

        private void Raise(string attr)
        {
            var evt = ModifierChanged;
            if (evt != null) evt(attr);
        }
    }

    internal sealed class EffectContext : ICharacterEffectContext
    {
        private readonly ICharacterProxy _character;
        private readonly EffectInstance _effect;

        internal EffectContext(ICharacterProxy character, EffectInstance effect)
        {
            _character = character;
            _effect = effect;
        }

        public ICharacterProxy Character { get { return _character; } }
        public EffectInstance Effect { get { return _effect; } }
        public float TimeApplied { get { return _effect.TimeApplied; } }
        public float TimeRemaining { get { return _effect.TimeRemaining; } }
        public float Elapsed { get { return CharacterEffectSystemImpl.NowSeconds() - _effect.TimeApplied; } }

        public T GetData<T>(string key)
        {
            if (_effect.CustomData == null || !_effect.CustomData.ContainsKey(key)) return default(T);
            object raw = _effect.CustomData[key];
            if (raw is T) return (T)raw;
            return default(T);
        }

        public void SetData<T>(string key, T value)
        {
            if (_effect.CustomData == null) _effect.CustomData = new Dictionary<string, object>();
            _effect.CustomData[key] = value;
        }
    }

    internal static class CharacterFactory
    {
        internal static CharacterDataModel CreateFromFamilyMember(FamilyMember member)
        {
            CharacterDataModel d = new CharacterDataModel();
            FillFromFamilyMember(d, member);
            d.Source = CharacterSource.RealFamily;
            d.SourceMod = "core";
            d.IsPersistent = true;
            d.CreatedAtTime = CharacterEffectSystemImpl.NowSeconds();
            d.PersistenceKey = "family." + d.UniqueId;
            return d;
        }

        internal static void FillFromFamilyMember(CharacterDataModel d, FamilyMember member)
        {
            if (d == null || member == null) return;
            d.UniqueId = member.GetId();
            d.FirstName = member.firstName;
            d.LastName = member.lastName;
            d.Health = member.health;
            d.MaxHealth = member.maxHealth;
        }

        internal static CharacterDataModel CreateFromVisitor(NpcVisitor npc)
        {
            CharacterDataModel d = new CharacterDataModel();
            FillFromVisitor(d, npc);
            d.Source = CharacterSource.Visitor;
            d.SourceMod = "core";
            d.IsPersistent = false;
            d.CreatedAtTime = CharacterEffectSystemImpl.NowSeconds();
            d.PersistenceKey = "visitor." + d.UniqueId;
            return d;
        }

        internal static void FillFromVisitor(CharacterDataModel d, NpcVisitor npc)
        {
            if (d == null || npc == null) return;
            d.UniqueId = npc.npcId;
            d.FirstName = npc.firstName;
            d.LastName = npc.lastName;
            d.Health = npc.health;
            d.MaxHealth = npc.maxHealth;
        }
    }

    internal static class RuntimeSerialization
    {
        internal static string SerializeToString(object value)
        {
            if (value == null) return string.Empty;
            try
            {
                using (var ms = new MemoryStream())
                {
                    new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter().Serialize(ms, value);
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        internal static object DeserializeFromString(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            try
            {
                byte[] data = Convert.FromBase64String(value);
                using (var ms = new MemoryStream(data))
                {
                    return new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter().Deserialize(ms);
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
