using HarmonyLib;
using ModAPI.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using ModAPI.Util;

namespace ModAPI.Characters.Internal
{
    internal sealed class CharacterEffectSystemImpl : ICharacterEffectSystem, ICharacterFactory, ISaveable
    {
        private readonly object _sync = new object();
        private readonly Dictionary<string, Type> _effectTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, LiveCharacterProxy> _liveFamily = new Dictionary<int, LiveCharacterProxy>();
        private readonly Dictionary<int, LiveCharacterProxy> _liveVisitors = new Dictionary<int, LiveCharacterProxy>();
        private readonly Dictionary<int, SyntheticCharacterProxy> _syntheticById = new Dictionary<int, SyntheticCharacterProxy>();
        private readonly Dictionary<string, SyntheticCharacterProxy> _syntheticByKey = new Dictionary<string, SyntheticCharacterProxy>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<int> _temporarySyntheticIds = new HashSet<int>();
        private int _nextSyntheticId = 1000000;
        private bool _registered;

        public event Action<ICharacterProxy, EffectInstance> EffectApplied;
        public event Action<ICharacterProxy, EffectInstance, RemovalReason> EffectRemoved;
        public event Action<ICharacterProxy, string, object> DataChanged;
        public event Action<ICharacterProxy> SyntheticCharacterCreated;
        public event Action<ICharacterProxy> SyntheticCharacterUnloaded;

        internal CharacterEffectSystemImpl()
        {
            try { GameEvents.OnSessionStarted += HandleSessionStarted; } catch { }
            try { GameEvents.OnNewGame += HandleSessionStarted; } catch { }
        }

        public void RegisterEffectType<T>(string effectId) where T : ICharacterEffect, new()
        {
            if (string.IsNullOrEmpty(effectId)) return;
            lock (_sync)
            {
                if (_effectTypes.ContainsKey(effectId)) return;
                _effectTypes[effectId] = typeof(T);
            }
        }

        public ICharacterProxy GetCharacter(FamilyMember member)
        {
            if (member == null) return null;
            EnsureRegistered();
            lock (_sync)
            {
                int id = member.GetId();
                LiveCharacterProxy p;
                if (_liveFamily.TryGetValue(id, out p))
                {
                    p.Bind(member);
                    return p;
                }
                p = new LiveCharacterProxy(member, OnDataChangedInternal);
                _liveFamily[id] = p;
                return p;
            }
        }

        public ICharacterProxy GetCharacter(NpcVisitor npc)
        {
            if (npc == null) return null;
            EnsureRegistered();
            lock (_sync)
            {
                int id = npc.npcId;
                LiveCharacterProxy p;
                if (_liveVisitors.TryGetValue(id, out p))
                {
                    p.Bind(npc);
                    return p;
                }
                p = new LiveCharacterProxy(npc, OnDataChangedInternal);
                _liveVisitors[id] = p;
                return p;
            }
        }

        public ICharacterProxy GetCharacterById(int uniqueMemberId)
        {
            EnsureRegistered();
            lock (_sync)
            {
                if (_liveFamily.ContainsKey(uniqueMemberId)) return _liveFamily[uniqueMemberId];
                if (_liveVisitors.ContainsKey(uniqueMemberId)) return _liveVisitors[uniqueMemberId];
                if (_syntheticById.ContainsKey(uniqueMemberId)) return _syntheticById[uniqueMemberId];
            }
            return null;
        }

        public CharacterQuery Query()
        {
            return new CharacterQuery(GetAllCharacters);
        }

        public IReadOnlyList<ICharacterProxy> GetAllCharacters()
        {
            lock (_sync)
            {
                var all = new List<ICharacterProxy>();
                all.AddRange(_liveFamily.Values.Cast<ICharacterProxy>());
                all.AddRange(_liveVisitors.Values.Cast<ICharacterProxy>());
                all.AddRange(_syntheticById.Values.Cast<ICharacterProxy>());
                return all.ToReadOnlyList();
            }
        }

        public IReadOnlyList<ICharacterProxy> GetPersistentCharacters()
        {
            return GetAllCharacters().Where(x => x != null && x.IsPersistent).ToList().ToReadOnlyList();
        }

        public IReadOnlyList<ICharacterProxy> GetTemporaryCharacters()
        {
            return GetAllCharacters().Where(x => x != null && !x.IsPersistent).ToList().ToReadOnlyList();
        }

        public void UnregisterCharacter(ICharacterProxy character)
        {
            if (character == null) return;
            lock (_sync)
            {
                if (character.Source == CharacterSource.Synthetic)
                {
                    SyntheticCharacterProxy synthetic = character as SyntheticCharacterProxy;
                    if (synthetic != null)
                    {
                        _syntheticById.Remove(synthetic.UniqueId);
                        if (!string.IsNullOrEmpty(synthetic.PersistenceKey)) _syntheticByKey.Remove(synthetic.PersistenceKey);
                        _temporarySyntheticIds.Remove(synthetic.UniqueId);
                        synthetic.Unregister();
                        var evt = SyntheticCharacterUnloaded;
                        if (evt != null) evt(synthetic);
                    }
                    return;
                }

                LiveCharacterProxy live = character as LiveCharacterProxy;
                if (live == null) return;
                _liveFamily.Remove(live.UniqueId);
                _liveVisitors.Remove(live.UniqueId);
                live.Unregister();
            }
        }

        public ICharacterProxy CreateSyntheticCharacter(string firstName, string lastName, string persistenceKey, string sourceModId, bool isPersistent = true)
        {
            EnsureRegistered();
            lock (_sync)
            {
                if (!string.IsNullOrEmpty(persistenceKey))
                {
                    if (_syntheticByKey.ContainsKey(persistenceKey))
                    {
                        WriteError("Persistence key '" + persistenceKey + "' already exists. Use a unique namespaced key.");
                        return null;
                    }

                    if (!persistenceKey.Contains("."))
                    {
                        WriteWarning("Persistence key '" + persistenceKey + "' is not namespaced. Prefer '" + (sourceModId ?? "mod") + "." + persistenceKey + "'.");
                    }
                }

                int id = NextSyntheticId();
                SyntheticCharacterProxy p = BuildSynthetic(id, firstName, lastName, persistenceKey, sourceModId, isPersistent);
                _syntheticById[id] = p;
                if (!string.IsNullOrEmpty(p.PersistenceKey)) _syntheticByKey[p.PersistenceKey] = p;
                if (!isPersistent) _temporarySyntheticIds.Add(id);
                var evt = SyntheticCharacterCreated;
                if (evt != null) evt(p);
                return p;
            }
        }

        public ICharacterProxy CreateTemporaryCharacter(string firstName, string lastName, string sourceModId)
        {
            string key = "temp." + sourceModId + "." + Guid.NewGuid().ToString("N");
            return CreateSyntheticCharacter(firstName, lastName, key, sourceModId, false);
        }

        public void SwapEncounterCharacter(EncounterCharacter encounterActor, ICharacterProxy newCharacter, Action<EncounterCharacter> onSwapComplete = null)
        {
            if (encounterActor == null || newCharacter == null)
            {
                if (onSwapComplete != null) onSwapComplete(encounterActor);
                return;
            }

            try
            {
                var first = encounterActor.GetType().GetField("m_firstName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (first != null) first.SetValue(encounterActor, newCharacter.Data.FirstName);
                var last = encounterActor.GetType().GetField("m_lastName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (last != null) last.SetValue(encounterActor, newCharacter.Data.LastName);
            }
            catch { }
            SwapEncounterActorStats(encounterActor, newCharacter);

            if (onSwapComplete != null) onSwapComplete(encounterActor);
        }

        internal void SwapEncounterActorStats(EncounterCharacter actor, ICharacterProxy character)
        {
            if (actor == null || character == null || character.Data == null) return;

            TrySetMember(actor, "health", character.Data.Health);
            TrySetMember(actor, "maxHealth", character.Data.MaxHealth);
            TrySetMember(actor, "strength", character.Data.StrengthLevel);
            TrySetMember(actor, "dexterity", character.Data.DexterityLevel);
            TrySetMember(actor, "intelligence", character.Data.IntelligenceLevel);
            TrySetMember(actor, "charisma", character.Data.CharismaLevel);
            TrySetMember(actor, "perception", character.Data.PerceptionLevel);
        }

        public int UnloadTemporaryCharacters(string sourceModId)
        {
            List<SyntheticCharacterProxy> unload = new List<SyntheticCharacterProxy>();
            lock (_sync)
            {
                foreach (var id in _temporarySyntheticIds.ToList())
                {
                    SyntheticCharacterProxy p;
                    if (!_syntheticById.TryGetValue(id, out p) || p == null) continue;
                    if (!string.IsNullOrEmpty(sourceModId) && !string.Equals(p.SourceMod, sourceModId, StringComparison.OrdinalIgnoreCase)) continue;
                    unload.Add(p);
                }
            }

            for (int i = 0; i < unload.Count; i++) UnregisterCharacter(unload[i]);
            return unload.Count;
        }

        public ICharacterProxy GetSyntheticCharacter(string persistenceKey)
        {
            if (string.IsNullOrEmpty(persistenceKey)) return null;
            lock (_sync)
            {
                SyntheticCharacterProxy p;
                return _syntheticByKey.TryGetValue(persistenceKey, out p) ? p : null;
            }
        }

        ICharacterProxy ICharacterFactory.CreateSyntheticCharacter(string name, int? baseId, string persistenceKey, string sourceModId)
        {
            string first = name ?? "Synthetic";
            string last = string.Empty;
            if (!string.IsNullOrEmpty(name))
            {
                var split = name.Split(new[] { ' ' }, 2);
                first = split[0];
                if (split.Length > 1) last = split[1];
            }
            lock (_sync)
            {
                int id = baseId.HasValue ? baseId.Value : NextSyntheticId();
                string key = string.IsNullOrEmpty(persistenceKey) ? "synthetic." + sourceModId + "." + id : persistenceKey;
                if (_syntheticByKey.ContainsKey(key))
                {
                    WriteError("Persistence key '" + key + "' already exists. Use a unique namespaced key.");
                    return null;
                }
                SyntheticCharacterProxy p = BuildSynthetic(id, first, last, key, sourceModId, true);
                _syntheticById[id] = p;
                _syntheticByKey[key] = p;
                var evt = SyntheticCharacterCreated;
                if (evt != null) evt(p);
                return p;
            }
        }

        ICharacterProxy ICharacterFactory.CreateTemporaryCharacter(string name, string sourceModId)
        {
            return CreateTemporaryCharacter(name, string.Empty, sourceModId);
        }

        public ICharacterProxy RestoreSyntheticCharacter(CharacterSaveData data)
        {
            if (data == null) return null;
            lock (_sync)
            {
                SyntheticCharacterProxy existing;
                if (!string.IsNullOrEmpty(data.PersistenceKey) && _syntheticByKey.TryGetValue(data.PersistenceKey, out existing))
                    return existing;

                SyntheticCharacterProxy p = BuildSynthetic(data.UniqueId, data.FirstName, data.LastName, data.PersistenceKey, data.SourceMod, data.IsPersistent);
                p.Data.MeshId = data.MeshId;
                p.Data.IsMale = data.IsMale;
                p.Data.Health = data.Health > 0 ? data.Health : 100;
                p.Data.MaxHealth = data.MaxHealth > 0 ? data.MaxHealth : Math.Max(100, p.Data.Health);
                if (data.CustomData != null && data.CustomData.Count > 0)
                {
                    foreach (var kv in data.CustomData)
                    {
                        p.Data.SetCustomData(kv.Key, RuntimeSerialization.DeserializeFromString(kv.Value));
                    }
                }
                var effects = p.Effects as BasicCharacterEffects;
                if (effects != null) effects.RestoreFromSaveData(data.Effects);
                var attrs = p.Attributes as BasicCharacterAttributes;
                if (attrs != null) attrs.RestoreFromSaveData(data.Attributes);
                if (data.Source == CharacterSource.Synthetic) p.State = CharacterState.SyntheticIdle;
                _syntheticById[p.UniqueId] = p;
                if (!string.IsNullOrEmpty(p.PersistenceKey)) _syntheticByKey[p.PersistenceKey] = p;
                if (!data.IsPersistent) _temporarySyntheticIds.Add(p.UniqueId);
                return p;
            }
        }

        internal bool TryCreateEffect(string effectId, out ICharacterEffect effect)
        {
            effect = null;
            if (string.IsNullOrEmpty(effectId)) return false;
            lock (_sync)
            {
                Type t;
                if (!_effectTypes.TryGetValue(effectId, out t)) return false;
                effect = (ICharacterEffect)Activator.CreateInstance(t);
                return effect != null;
            }
        }

        internal void OnEffectApplied(ICharacterProxy character, EffectInstance effect)
        {
            var evt = EffectApplied;
            if (evt != null) evt(character, effect);
        }

        internal void OnEffectRemoved(ICharacterProxy character, EffectInstance effect, RemovalReason reason)
        {
            var evt = EffectRemoved;
            if (evt != null) evt(character, effect, reason);
        }

        private SyntheticCharacterProxy BuildSynthetic(int id, string firstName, string lastName, string key, string sourceModId, bool isPersistent)
        {
            SyntheticCharacterProxy p = new SyntheticCharacterProxy(id);
            CharacterDataModel data = new CharacterDataModel
            {
                UniqueId = id,
                PersistenceKey = string.IsNullOrEmpty(key) ? "synthetic." + id : key,
                FirstName = string.IsNullOrEmpty(firstName) ? "Synthetic" : firstName,
                LastName = lastName ?? string.Empty,
                Source = CharacterSource.Synthetic,
                SourceMod = sourceModId ?? "unknown",
                IsPersistent = isPersistent,
                CreatedAtTime = NowSeconds(),
                Health = 100,
                MaxHealth = 100,
                MeshId = "synthetic"
            };
            p.Data = data;
            p.Effects = new BasicCharacterEffects(p);
            p.Attributes = new BasicCharacterAttributes();
            p.State = isPersistent ? CharacterState.SyntheticIdle : CharacterState.TemporarilyAbsent;
            p.IsLoadedOnShelterEntry = true;
            return p;
        }

        private int NextSyntheticId()
        {
            while (_liveFamily.ContainsKey(_nextSyntheticId) || _liveVisitors.ContainsKey(_nextSyntheticId) || _syntheticById.ContainsKey(_nextSyntheticId))
                _nextSyntheticId++;
            return _nextSyntheticId++;
        }

        private void HandleSessionStarted()
        {
            lock (_sync)
            {
                _liveFamily.Clear();
                _liveVisitors.Clear();
                _temporarySyntheticIds.Clear();
                var keep = _syntheticById.Values.Where(x => x.IsPersistent).ToList();
                _syntheticById.Clear();
                _syntheticByKey.Clear();
                for (int i = 0; i < keep.Count; i++)
                {
                    _syntheticById[keep[i].UniqueId] = keep[i];
                    if (!string.IsNullOrEmpty(keep[i].PersistenceKey)) _syntheticByKey[keep[i].PersistenceKey] = keep[i];
                }
            }
        }

        private void EnsureRegistered()
        {
            if (_registered) return;
            var sm = SaveManager.instance;
            if (sm == null) return;
            sm.RegisterSaveable(this);
            _registered = true;
        }

        internal static float NowSeconds()
        {
            try
            {
                return (((GameTime.Day - 1) * 1440) + (GameTime.Hour * 60) + GameTime.Minute) * 60f;
            }
            catch { return Time.time; }
        }

        private void OnDataChangedInternal(ICharacterProxy c, string key, object value)
        {
            var evt = DataChanged;
            if (evt != null) evt(c, key, value);
        }

        public bool IsRelocationEnabled() { return true; }
        public bool IsReadyForLoad() { return true; }

        public bool SaveLoad(SaveData data)
        {
            if (data == null) return false;
            try { data.GroupStart("ModAPI_Characters_V2"); }
            catch (SaveData.MissingGroupException) { return true; }

            List<SyntheticCharacterProxy> persistent = null;
            if (data.isSaving)
            {
                lock (_sync)
                {
                    persistent = _syntheticById.Values.Where(x => x.IsPersistent).ToList();
                }
            }

            var loadBuffer = new List<string>();
            data.SaveLoadList("synthetic", persistent ?? new List<SyntheticCharacterProxy>(),
                i =>
                {
                    CharacterSaveData dto = ToSaveData(persistent[i]);
                    string blob = Serialize(dto);
                    data.SaveLoad("blob", ref blob);
                },
                i =>
                {
                    string blob = string.Empty;
                    data.SaveLoad("blob", ref blob);
                    if (!string.IsNullOrEmpty(blob)) loadBuffer.Add(blob);
                });

            if (data.isLoading)
            {
                lock (_sync)
                {
                    var temp = _syntheticById.Values.Where(x => !x.IsPersistent).ToList();
                    for (int i = 0; i < temp.Count; i++) UnregisterCharacter(temp[i]);
                }
                for (int i = 0; i < loadBuffer.Count; i++)
                {
                    CharacterSaveData dto = Deserialize<CharacterSaveData>(loadBuffer[i]);
                    if (dto != null) RestoreSyntheticCharacter(dto);
                }
            }

            data.GroupEnd();
            return true;
        }

        private static CharacterSaveData ToSaveData(SyntheticCharacterProxy p)
        {
            var data = p.Data as CharacterDataModel;
            Dictionary<string, string> custom = new Dictionary<string, string>(StringComparer.Ordinal);
            if (data != null)
            {
                var raw = data.SnapshotCustomData();
                foreach (var kv in raw)
                {
                    custom[kv.Key] = RuntimeSerialization.SerializeToString(kv.Value);
                }
            }

            var effects = p.Effects as BasicCharacterEffects;
            var attrs = p.Attributes as BasicCharacterAttributes;

            return new CharacterSaveData
            {
                UniqueId = p.UniqueId,
                PersistenceKey = p.PersistenceKey,
                Source = p.Source,
                SourceMod = p.SourceMod,
                IsPersistent = p.IsPersistent,
                FirstName = p.Data.FirstName,
                LastName = p.Data.LastName,
                IsMale = p.Data.IsMale,
                MeshId = p.Data.MeshId,
                Health = p.Data.Health,
                MaxHealth = p.Data.MaxHealth,
                Effects = effects != null ? effects.ToSaveData() : new List<EffectSaveData>(),
                Attributes = attrs != null ? attrs.ToSaveData() : new List<AttributeSaveData>(),
                CustomData = custom
            };
        }

        private static string Serialize(object value)
        {
            try
            {
                using (var ms = new MemoryStream())
                {
                    new BinaryFormatter().Serialize(ms, value);
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
            catch { return string.Empty; }
        }

        private static T Deserialize<T>(string blob) where T : class
        {
            try
            {
                var bytes = Convert.FromBase64String(blob);
                using (var ms = new MemoryStream(bytes))
                {
                    return new BinaryFormatter().Deserialize(ms) as T;
                }
            }
            catch { return null; }
        }

        [HarmonyPatch(typeof(GameTime), "Awake")]
        private static class GameTime_Awake
        {
            private static void Postfix()
            {
                CharacterEffectSystem.InternalInstance.EnsureRegistered();
            }
        }

        [HarmonyPatch(typeof(GameTime), "Update")]
        private static class GameTime_Update
        {
            private static void Postfix()
            {
                CharacterEffectSystem.InternalInstance.Update();
            }
        }

        private void Update()
        {
            EnsureRegistered();
            RefreshLiveCharacters();
            float dt = Time.deltaTime;
            List<LiveCharacterProxy> proxies;
            lock (_sync)
            {
                proxies = _liveFamily.Values.Concat(_liveVisitors.Values).ToList();
            }
            for (int i = 0; i < proxies.Count; i++) proxies[i].Tick(dt);
        }

        private void RefreshLiveCharacters()
        {
            try
            {
                var fm = FamilyManager.Instance;
                if (fm != null)
                {
                    var members = fm.GetAllFamilyMembers();
                    if (members != null)
                    {
                        for (int i = 0; i < members.Count; i++)
                        {
                            var m = members[i];
                            if (m != null) GetCharacter(m);
                        }
                    }
                }
            }
            catch { }

            try
            {
                var nvm = NpcVisitManager.Instance;
                if (nvm != null && nvm.Visitors != null)
                {
                    for (int i = 0; i < nvm.Visitors.Count; i++)
                    {
                        var v = nvm.Visitors[i];
                        if (v != null) GetCharacter(v);
                    }
                }
            }
            catch { }
        }

        private static bool TrySetMember(object target, string name, object value)
        {
            if (target == null || string.IsNullOrEmpty(name)) return false;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type cursor = target.GetType();

            while (cursor != null)
            {
                FieldInfo field = cursor.GetField(name, flags);
                if (field != null)
                {
                    try
                    {
                        object converted = value;
                        if (value != null && field.FieldType != value.GetType())
                        {
                            converted = Convert.ChangeType(value, field.FieldType);
                        }
                        field.SetValue(target, converted);
                        return true;
                    }
                    catch { return false; }
                }

                PropertyInfo prop = cursor.GetProperty(name, flags);
                if (prop != null && prop.CanWrite)
                {
                    try
                    {
                        object converted = value;
                        if (value != null && prop.PropertyType != value.GetType())
                        {
                            converted = Convert.ChangeType(value, prop.PropertyType);
                        }
                        prop.SetValue(target, converted, null);
                        return true;
                    }
                    catch { return false; }
                }

                cursor = cursor.BaseType;
            }
            return false;
        }

        private static void WriteWarning(string message)
        {
            try
            {
                Type mmLogType = Type.GetType("ModAPI.Core.MMLog, ModAPI", false);
                if (mmLogType != null)
                {
                    MethodInfo writeWarning = mmLogType.GetMethod("WriteWarning", BindingFlags.Public | BindingFlags.Static);
                    if (writeWarning != null)
                    {
                        writeWarning.Invoke(null, new object[] { message });
                        return;
                    }
                }
            }
            catch { }
            Debug.LogWarning("[CharacterEffectSystem] " + message);
        }

        private static void WriteError(string message)
        {
            try
            {
                Type mmLogType = Type.GetType("ModAPI.Core.MMLog, ModAPI", false);
                if (mmLogType != null)
                {
                    MethodInfo writeError = mmLogType.GetMethod("WriteError", BindingFlags.Public | BindingFlags.Static);
                    if (writeError != null)
                    {
                        writeError.Invoke(null, new object[] { message });
                        return;
                    }
                }
            }
            catch { }
            Debug.LogError("[CharacterEffectSystem] " + message);
        }
    }
}
