using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using ModAPI.Util;


using ShelteredAPI.Characters;
using ShelteredAPI.Characters.Abstractions;
using ShelteredAPI.Characters.Models;
using ShelteredAPI.Content;
using ShelteredAPI.Hooks;
namespace ShelteredAPI.Characters.Internal
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
        // Game values as of the last read or write. A data field that differs from this was edited by a mod
        // and is pending writeback, so a refresh from the game must not overwrite it.
        private readonly CharacterDataModel _synced = new CharacterDataModel();

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
        internal FamilyMember UnderlyingMember { get { return _member; } }
        internal NpcVisitor UnderlyingNpc { get { return _npc; } }
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
            _data.CustomDataChanged = NotifyCustomDataChanged;
            CharacterFactory.FillFromFamilyMember(_synced, member);
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
            _data.CustomDataChanged = NotifyCustomDataChanged;
            CharacterFactory.FillFromVisitor(_synced, npc);
            _effects = new BasicCharacterEffects(this);
            _attributes = new BasicCharacterAttributes();
            IsActive = true;
            IsLoadedOnShelterEntry = false;
            RefreshState();
        }

        internal void Bind(FamilyMember member)
        {
            bool sameObject = ReferenceEquals(_member, member);
            _member = member;
            if (member != null)
            {
                CharacterDataModel game = new CharacterDataModel();
                CharacterFactory.FillFromFamilyMember(game, member);
                PullFromGame(game, sameObject);
                IsActive = true;
            }
            RefreshState();
        }

        internal void Bind(NpcVisitor npc)
        {
            bool sameObject = ReferenceEquals(_npc, npc);
            _npc = npc;
            if (npc != null)
            {
                CharacterDataModel game = new CharacterDataModel();
                CharacterFactory.FillFromVisitor(game, npc);
                PullFromGame(game, sameObject);
                IsActive = true;
            }
            RefreshState();
        }

        // Takes the game's current values but keeps mod edits that have not been written back yet.
        // Pending edits only survive a refresh of the same game object; a new object starts from the game.
        private void PullFromGame(CharacterDataModel game, bool keepPendingEdits)
        {
            _data.UniqueId = game.UniqueId;
            _data.FirstName = Pick(_data.FirstName, _synced.FirstName, game.FirstName, keepPendingEdits);
            _data.LastName = Pick(_data.LastName, _synced.LastName, game.LastName, keepPendingEdits);
            _data.Health = Pick(_data.Health, _synced.Health, game.Health, keepPendingEdits);
            _data.MaxHealth = Pick(_data.MaxHealth, _synced.MaxHealth, game.MaxHealth, keepPendingEdits);
            _data.StrengthLevel = Pick(_data.StrengthLevel, _synced.StrengthLevel, game.StrengthLevel, keepPendingEdits);
            _data.DexterityLevel = Pick(_data.DexterityLevel, _synced.DexterityLevel, game.DexterityLevel, keepPendingEdits);
            _data.IntelligenceLevel = Pick(_data.IntelligenceLevel, _synced.IntelligenceLevel, game.IntelligenceLevel, keepPendingEdits);
            _data.CharismaLevel = Pick(_data.CharismaLevel, _synced.CharismaLevel, game.CharismaLevel, keepPendingEdits);
            _data.PerceptionLevel = Pick(_data.PerceptionLevel, _synced.PerceptionLevel, game.PerceptionLevel, keepPendingEdits);

            _synced.UniqueId = game.UniqueId;
            _synced.FirstName = game.FirstName;
            _synced.LastName = game.LastName;
            _synced.Health = game.Health;
            _synced.MaxHealth = game.MaxHealth;
            _synced.StrengthLevel = game.StrengthLevel;
            _synced.DexterityLevel = game.DexterityLevel;
            _synced.IntelligenceLevel = game.IntelligenceLevel;
            _synced.CharismaLevel = game.CharismaLevel;
            _synced.PerceptionLevel = game.PerceptionLevel;
        }

        private static T Pick<T>(T current, T synced, T game, bool keepPendingEdits)
        {
            return keepPendingEdits && !EqualityComparer<T>.Default.Equals(current, synced) ? current : game;
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
            _effects.Tick(dt);
            _attributes.Tick(dt);
            SyncToGameObject();
            RefreshState();
        }

        private void NotifyCustomDataChanged(string key, object value)
        {
            if (_dataChanged != null) _dataChanged(this, key, value);
        }

        internal void SyncToGameObject()
        {
            if (_member != null)
            {
                WriteNamesAndStats(_member, _member.BaseStats);
                SyncHealth(_member);
                CharacterDataModel game = new CharacterDataModel();
                CharacterFactory.FillFromFamilyMember(game, _member);
                PullFromGame(game, false);
            }

            if (_npc != null)
            {
                WriteNamesAndStats(_npc, _npc.BaseStats);
                SyncHealth(_npc);
                CharacterDataModel game = new CharacterDataModel();
                CharacterFactory.FillFromVisitor(game, _npc);
                PullFromGame(game, false);
            }
        }

        private void WriteNamesAndStats(object character, BaseStats stats)
        {
            if (!string.Equals(_data.FirstName, _synced.FirstName, StringComparison.Ordinal))
                TrySetField(character, "m_firstName", _data.FirstName);
            if (!string.Equals(_data.LastName, _synced.LastName, StringComparison.Ordinal))
                TrySetField(character, "m_lastName", _data.LastName);
            if (stats == null) return;
            WriteStatLevel(stats.Strength, _data.StrengthLevel, _synced.StrengthLevel);
            WriteStatLevel(stats.Dexterity, _data.DexterityLevel, _synced.DexterityLevel);
            WriteStatLevel(stats.Intelligence, _data.IntelligenceLevel, _synced.IntelligenceLevel);
            WriteStatLevel(stats.Charisma, _data.CharismaLevel, _synced.CharismaLevel);
            WriteStatLevel(stats.Perception, _data.PerceptionLevel, _synced.PerceptionLevel);
        }

        // Data levels are effective levels (base plus modifier), so the modifier is kept and only the base level moves.
        private static void WriteStatLevel(BaseStat stat, int desired, int synced)
        {
            if (stat == null || desired == synced) return;
            int baseLevel = Math.Max(0, Math.Min(desired - stat.LevelModifier, stat.LevelCap));
            stat.SetInitialLevel(baseLevel, stat.LevelCap);
        }

        private void SyncHealth(BaseCharacter character)
        {
            if (_data.Health == _synced.Health && _data.MaxHealth == _synced.MaxHealth) return;
            int maxHealth = Math.Max(1, _data.MaxHealth);
            int health = Math.Max(0, Math.Min(_data.Health, maxHealth));
            character.SetMaxHealth(maxHealth);
            if (health < character.health)
                character.Damage(character.health - health);
            else if (health > character.health && !character.isDead)
                character.Heal(health - character.health);
            // Fatal damage can leave a family member at one health while becoming unconscious.
            _data.Health = character.health;
            _data.MaxHealth = character.maxHealth;
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
            if (existing != null && effect.StackBehavior == StackBehavior.Ignore) return existing;

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
            if (existing != null)
            {
                if (effect.StackBehavior == StackBehavior.Refresh)
                {
                    existing.Duration = instance.Duration;
                    existing.TimeRemaining = instance.TimeRemaining;
                    existing.StackCount++;
                    return existing;
                }
                if (effect.StackBehavior == StackBehavior.Extend)
                {
                    if (existing.IsPermanent || instance.IsPermanent)
                    {
                        existing.Duration = 0f;
                        existing.TimeRemaining = 0f;
                    }
                    else existing.TimeRemaining += instance.Duration;
                    existing.StackCount++;
                    return existing;
                }
                if (effect.StackBehavior == StackBehavior.Replace) Remove(existing);
            }
            _effects.Add(instance);
            effect.OnApplied(ctx);
            SyncLiveCharacter();
            CharacterEffectSystem.InternalInstance.OnEffectApplied(_character, instance);
            return instance;
        }

        public bool Remove(EffectInstance effect, RemovalReason reason = RemovalReason.Manually)
        {
            if (effect == null) return false;
            if (!_effects.Remove(effect)) return false;
            EffectContext ctx = new EffectContext(_character, effect);
            effect.Effect.OnRemoved(ctx, reason);
            SyncLiveCharacter();
            CharacterEffectSystem.InternalInstance.OnEffectRemoved(_character, effect, reason);
            return true;
        }

        private void SyncLiveCharacter()
        {
            LiveCharacterProxy live = _character as LiveCharacterProxy;
            if (live != null) live.SyncToGameObject();
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
            EffectInstance[] pending = _effects.ToArray();
            for (int i = pending.Length - 1; i >= 0; i--)
            {
                EffectInstance e = pending[i];
                if (!_effects.Contains(e)) continue;
                if (e == null || e.Effect == null) { _effects.Remove(e); continue; }
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
            return _mods.Where(x => !x.IsExpired && !x.IsMultiplicative && string.Equals(x.AttributeName, attributeName, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Value);
        }

        internal float ApplyTo(string attributeName, float baseValue)
        {
            float added = 0f, factor = 1f, minimum = 0f;
            bool contributed = false;
            foreach (AttributeModifier modifier in _mods)
            {
                if (modifier.IsExpired || !string.Equals(modifier.AttributeName, attributeName, StringComparison.OrdinalIgnoreCase)) continue;
                if (float.IsNaN(modifier.Value) || float.IsInfinity(modifier.Value)) continue;
                contributed = true;
                if (modifier.IsMultiplicative) factor *= modifier.Value;
                else added += modifier.Value;
                if (modifier.MinimumResult.HasValue && !float.IsNaN(modifier.MinimumResult.Value)
                    && !float.IsInfinity(modifier.MinimumResult.Value)) minimum = Math.Max(minimum, modifier.MinimumResult.Value);
            }
            if (!contributed) return baseValue;
            float result = (baseValue + added) * factor;
            return float.IsNaN(result) || float.IsInfinity(result) ? baseValue : Math.Max(minimum, result);
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
            AttributeModifier[] pending = _mods.ToArray();
            for (int i = pending.Length - 1; i >= 0; i--)
            {
                AttributeModifier m = pending[i];
                if (!_mods.Contains(m)) continue;
                if (!m.IsPermanent) m.TimeRemaining -= dt;
                if (m.IsExpired) Remove(m);
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
                    IsMultiplicative = m.IsMultiplicative,
                    MinimumResult = m.MinimumResult,
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
                    IsMultiplicative = m.IsMultiplicative,
                    MinimumResult = m.MinimumResult,
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
            d.StrengthLevel = member.BaseStats.Strength.Level;
            d.DexterityLevel = member.BaseStats.Dexterity.Level;
            d.IntelligenceLevel = member.BaseStats.Intelligence.Level;
            d.CharismaLevel = member.BaseStats.Charisma.Level;
            d.PerceptionLevel = member.BaseStats.Perception.Level;
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
            d.StrengthLevel = npc.BaseStats.Strength.Level;
            d.DexterityLevel = npc.BaseStats.Dexterity.Level;
            d.IntelligenceLevel = npc.BaseStats.Intelligence.Level;
            d.CharismaLevel = npc.BaseStats.Charisma.Level;
            d.PerceptionLevel = npc.BaseStats.Perception.Level;
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
