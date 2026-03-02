using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Reflection;

namespace ShelteredAPI.Debugging
{
    /// <summary>
    /// Fluent helper for forcing or mutating runtime values for debug/testing.
    /// Intended for use inside Harmony prefixes/postfixes or event callbacks.
    /// </summary>
    public static class DebugValueTakeover
    {
        public static DebugValueTakeoverSession For(object target)
        {
            return new DebugValueTakeoverSession(target);
        }
    }

    public sealed class DebugValueTakeoverSession
    {
        private readonly object _target;
        private Func<bool> _enabledPredicate;
        private readonly List<Action> _restoreActions = new List<Action>();

        internal DebugValueTakeoverSession(object target)
        {
            _target = target;
            _enabledPredicate = delegate { return true; };
        }

        public object Target { get { return _target; } }

        public DebugValueTakeoverSession When(Func<bool> predicate)
        {
            _enabledPredicate = predicate ?? delegate { return true; };
            return this;
        }

        public DebugValueTakeoverSession If(bool enabled)
        {
            _enabledPredicate = delegate { return enabled; };
            return this;
        }

        public DebugValueTakeoverSession SetField<T>(string fieldName, T value)
        {
            return SetField(fieldName, value, false);
        }

        public DebugValueTakeoverSession SetField<T>(string fieldName, T value, bool rememberOriginal)
        {
            if (!CanApply()) return this;
            if (string.IsNullOrEmpty(fieldName) || _target == null) return this;

            if (rememberOriginal)
            {
                T oldValue;
                if (Safe.TryGetField<T>(_target, fieldName, out oldValue))
                {
                    _restoreActions.Add(delegate
                    {
                        Safe.SetField(_target, fieldName, oldValue);
                    });
                }
            }

            Safe.SetField(_target, fieldName, value);
            return this;
        }

        public DebugValueTakeoverSession SetProperty<T>(string propertyName, T value)
        {
            return SetProperty(propertyName, value, false);
        }

        public DebugValueTakeoverSession SetProperty<T>(string propertyName, T value, bool rememberOriginal)
        {
            if (!CanApply()) return this;
            if (string.IsNullOrEmpty(propertyName) || _target == null) return this;

            if (rememberOriginal)
            {
                T oldValue;
                if (Safe.TryGetProperty<T>(_target, propertyName, out oldValue))
                {
                    _restoreActions.Add(delegate
                    {
                        Safe.SetProperty(_target, propertyName, oldValue);
                    });
                }
            }

            Safe.SetProperty(_target, propertyName, value);
            return this;
        }

        public DebugValueTakeoverSession MutateField<T>(string fieldName, Func<T, T> mutator)
        {
            return MutateField(fieldName, mutator, false);
        }

        public DebugValueTakeoverSession MutateField<T>(string fieldName, Func<T, T> mutator, bool rememberOriginal)
        {
            if (!CanApply()) return this;
            if (string.IsNullOrEmpty(fieldName) || _target == null || mutator == null) return this;

            T current;
            if (!Safe.TryGetField<T>(_target, fieldName, out current))
                return this;

            if (rememberOriginal)
            {
                T oldValue = current;
                _restoreActions.Add(delegate
                {
                    Safe.SetField(_target, fieldName, oldValue);
                });
            }

            T next = mutator(current);
            Safe.SetField(_target, fieldName, next);
            return this;
        }

        public DebugValueTakeoverSession MutateProperty<T>(string propertyName, Func<T, T> mutator)
        {
            return MutateProperty(propertyName, mutator, false);
        }

        public DebugValueTakeoverSession MutateProperty<T>(string propertyName, Func<T, T> mutator, bool rememberOriginal)
        {
            if (!CanApply()) return this;
            if (string.IsNullOrEmpty(propertyName) || _target == null || mutator == null) return this;

            T current;
            if (!Safe.TryGetProperty<T>(_target, propertyName, out current))
                return this;

            if (rememberOriginal)
            {
                T oldValue = current;
                _restoreActions.Add(delegate
                {
                    Safe.SetProperty(_target, propertyName, oldValue);
                });
            }

            T next = mutator(current);
            Safe.SetProperty(_target, propertyName, next);
            return this;
        }

        public bool TryGetField<T>(string fieldName, out T value)
        {
            value = default(T);
            if (string.IsNullOrEmpty(fieldName) || _target == null) return false;
            return Safe.TryGetField<T>(_target, fieldName, out value);
        }

        public bool TryGetProperty<T>(string propertyName, out T value)
        {
            value = default(T);
            if (string.IsNullOrEmpty(propertyName) || _target == null) return false;
            return Safe.TryGetProperty<T>(_target, propertyName, out value);
        }

        public void Restore()
        {
            for (int i = _restoreActions.Count - 1; i >= 0; i--)
            {
                try
                {
                    _restoreActions[i]();
                }
                catch { }
            }
            _restoreActions.Clear();
        }

        private bool CanApply()
        {
            if (_target == null) return false;

            try
            {
                return _enabledPredicate == null || _enabledPredicate();
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("DebugValueTakeover.PredicateError", "[DebugValueTakeover] Predicate error: " + ex.Message);
                return false;
            }
        }
    }
}
