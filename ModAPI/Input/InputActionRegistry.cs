using System;
using System.Collections.Generic;

namespace ModAPI.InputActions
{
    /// <summary>
    /// Global registry for rebindable actions and their active bindings.
    /// </summary>
    public static class InputActionRegistry
    {
        private static readonly Dictionary<string, ModInputAction> _actions = new Dictionary<string, ModInputAction>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, InputBinding> _bindings = new Dictionary<string, InputBinding>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _sync = new object();

        public static event Action<string, InputBinding> OnBindingChanged;

        public static bool Register(ModInputAction action)
        {
            if (action == null || string.IsNullOrEmpty(action.Id)) return false;

            lock (_sync)
            {
                if (_actions.ContainsKey(action.Id)) return false;
                _actions[action.Id] = action;
                _bindings[action.Id] = action.DefaultBinding;
                return true;
            }
        }

        public static bool IsRegistered(string actionId)
        {
            if (string.IsNullOrEmpty(actionId)) return false;
            lock (_sync) return _actions.ContainsKey(actionId);
        }

        public static List<ModInputAction> GetAllActions()
        {
            lock (_sync)
            {
                return new List<ModInputAction>(_actions.Values);
            }
        }

        public static bool TryGetAction(string actionId, out ModInputAction action)
        {
            action = null;
            if (string.IsNullOrEmpty(actionId)) return false;
            lock (_sync) return _actions.TryGetValue(actionId, out action);
        }

        public static InputBinding GetBinding(string actionId)
        {
            InputBinding binding;
            if (TryGetBinding(actionId, out binding)) return binding;
            return new InputBinding(UnityEngine.KeyCode.None, UnityEngine.KeyCode.None);
        }

        public static bool TryGetBinding(string actionId, out InputBinding binding)
        {
            binding = new InputBinding(UnityEngine.KeyCode.None, UnityEngine.KeyCode.None);
            if (string.IsNullOrEmpty(actionId)) return false;

            lock (_sync) return _bindings.TryGetValue(actionId, out binding);
        }

        public static bool SetBinding(string actionId, InputBinding binding)
        {
            if (string.IsNullOrEmpty(actionId)) return false;
            lock (_sync)
            {
                if (!_actions.ContainsKey(actionId)) return false;
                _bindings[actionId] = binding;
            }

            var handler = OnBindingChanged;
            if (handler != null) handler(actionId, binding);
            return true;
        }

        public static bool ResetBinding(string actionId)
        {
            if (string.IsNullOrEmpty(actionId)) return false;
            lock (_sync)
            {
                ModInputAction action;
                if (!_actions.TryGetValue(actionId, out action)) return false;
                _bindings[actionId] = action.DefaultBinding;
            }

            var handler = OnBindingChanged;
            if (handler != null) handler(actionId, GetBinding(actionId));
            return true;
        }

        public static void ResetAllBindings()
        {
            List<string> actionIds;
            lock (_sync)
            {
                actionIds = new List<string>(_actions.Keys);
            }

            for (int i = 0; i < actionIds.Count; i++) ResetBinding(actionIds[i]);
        }

        public static List<ModInputAction> FindConflicts(string actionId, InputBinding candidate)
        {
            var conflicts = new List<ModInputAction>();
            if (string.IsNullOrEmpty(actionId) || candidate.IsUnbound) return conflicts;

            lock (_sync)
            {
                foreach (var kvp in _actions)
                {
                    if (string.Equals(kvp.Key, actionId, StringComparison.OrdinalIgnoreCase)) continue;

                    InputBinding existing;
                    if (!_bindings.TryGetValue(kvp.Key, out existing)) continue;
                    if (existing.Overlaps(candidate)) conflicts.Add(kvp.Value);
                }
            }

            return conflicts;
        }

        public static bool IsDown(string actionId)
        {
            InputBinding binding;
            return TryGetBinding(actionId, out binding) && binding.IsDown();
        }

        public static bool IsHeld(string actionId)
        {
            InputBinding binding;
            return TryGetBinding(actionId, out binding) && binding.IsHeld();
        }

        public static bool IsUp(string actionId)
        {
            InputBinding binding;
            return TryGetBinding(actionId, out binding) && binding.IsUp();
        }
    }
}
