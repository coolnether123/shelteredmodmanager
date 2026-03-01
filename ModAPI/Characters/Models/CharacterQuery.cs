using System;
using System.Collections.Generic;
using System.Linq;
using ModAPI.Util;

namespace ModAPI.Characters
{
    public class CharacterQuery
    {
        private readonly Func<IReadOnlyList<ICharacterProxy>> _source;
        private readonly List<Func<ICharacterProxy, bool>> _predicates = new List<Func<ICharacterProxy, bool>>();

        public CharacterQuery(Func<IReadOnlyList<ICharacterProxy>> source)
        {
            _source = source;
        }

        public CharacterQuery WithEffect<T>() where T : ICharacterEffect
        {
            _predicates.Add(delegate(ICharacterProxy c) { return c != null && c.Effects.Has<T>(); });
            return this;
        }

        public CharacterQuery WithEffect(string effectId)
        {
            _predicates.Add(delegate(ICharacterProxy c) { return c != null && c.Effects.Has(effectId); });
            return this;
        }

        public CharacterQuery WithoutEffect<T>() where T : ICharacterEffect
        {
            _predicates.Add(delegate(ICharacterProxy c) { return c != null && !c.Effects.Has<T>(); });
            return this;
        }

        public CharacterQuery WithoutEffect(string effectId)
        {
            _predicates.Add(delegate(ICharacterProxy c) { return c != null && !c.Effects.Has(effectId); });
            return this;
        }

        public CharacterQuery WithAttribute(string attributeName)
        {
            _predicates.Add(delegate(ICharacterProxy c) { return c != null && c.Attributes.GetModifier(attributeName) != 0f; });
            return this;
        }

        public CharacterQuery InState(CharacterState state)
        {
            _predicates.Add(delegate(ICharacterProxy c) { return c != null && c.State == state; });
            return this;
        }

        public CharacterQuery FromMod(string modId)
        {
            _predicates.Add(delegate(ICharacterProxy c) { return c != null && c.Effects.GetAllFromMod(modId).Count > 0; });
            return this;
        }

        public CharacterQuery Where(Func<ICharacterProxy, bool> predicate)
        {
            if (predicate != null) _predicates.Add(predicate);
            return this;
        }

        public CharacterQuery WithData<T>(string key)
        {
            _predicates.Add(delegate(ICharacterProxy c) { return c != null && string.Equals(c.Data.PersistenceKey, key, StringComparison.OrdinalIgnoreCase); });
            return this;
        }

        public CharacterQuery FromSource(CharacterSource source)
        {
            _predicates.Add(delegate(ICharacterProxy c) { return c != null && c.Source == source; });
            return this;
        }

        public CharacterQuery OnlyPersistent()
        {
            _predicates.Add(delegate(ICharacterProxy c) { return c != null && c.IsPersistent; });
            return this;
        }

        public CharacterQuery OnlyTemporary()
        {
            _predicates.Add(delegate(ICharacterProxy c) { return c != null && !c.IsPersistent; });
            return this;
        }

        public CharacterQuery WithPersistenceKey(string key)
        {
            _predicates.Add(delegate(ICharacterProxy c) { return c != null && string.Equals(c.PersistenceKey, key, StringComparison.OrdinalIgnoreCase); });
            return this;
        }

        public CharacterQuery CreatedByMod(string modId)
        {
            _predicates.Add(delegate(ICharacterProxy c) { return c != null && string.Equals(c.SourceMod, modId, StringComparison.OrdinalIgnoreCase); });
            return this;
        }

        public List<ICharacterProxy> ToList()
        {
            IReadOnlyList<ICharacterProxy> source = _source != null ? _source() : new List<ICharacterProxy>().ToReadOnlyList();
            return source.Where(delegate(ICharacterProxy c) { return _predicates.All(delegate(Func<ICharacterProxy, bool> p) { return p(c); }); }).ToList();
        }

        public ICharacterProxy FirstOrDefault()
        {
            var list = ToList();
            return list.Count > 0 ? list[0] : null;
        }

        public int Count()
        {
            return ToList().Count;
        }

        public void ForEach(Action<ICharacterProxy> action)
        {
            if (action == null) return;
            var list = ToList();
            for (int i = 0; i < list.Count; i++)
            {
                action(list[i]);
            }
        }
    }
}
