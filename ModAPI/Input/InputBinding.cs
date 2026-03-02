using UnityEngine;

namespace ModAPI.InputActions
{
    /// <summary>
    /// Two-slot keyboard binding model for an action.
    /// </summary>
    public struct InputBinding
    {
        public KeyCode Primary;
        public KeyCode Secondary;

        public InputBinding(KeyCode primary, KeyCode secondary)
        {
            Primary = primary;
            Secondary = secondary;
        }

        public bool IsUnbound
        {
            get { return Primary == KeyCode.None && Secondary == KeyCode.None; }
        }

        public bool ContainsKey(KeyCode key)
        {
            return key != KeyCode.None && (Primary == key || Secondary == key);
        }

        public bool Overlaps(InputBinding other)
        {
            if (Primary != KeyCode.None && (other.Primary == Primary || other.Secondary == Primary)) return true;
            if (Secondary != KeyCode.None && (other.Primary == Secondary || other.Secondary == Secondary)) return true;
            return false;
        }

        public bool IsDown()
        {
            return (Primary != KeyCode.None && UnityEngine.Input.GetKeyDown(Primary))
                   || (Secondary != KeyCode.None && UnityEngine.Input.GetKeyDown(Secondary));
        }

        public bool IsHeld()
        {
            return (Primary != KeyCode.None && UnityEngine.Input.GetKey(Primary))
                   || (Secondary != KeyCode.None && UnityEngine.Input.GetKey(Secondary));
        }

        public bool IsUp()
        {
            return (Primary != KeyCode.None && UnityEngine.Input.GetKeyUp(Primary))
                   || (Secondary != KeyCode.None && UnityEngine.Input.GetKeyUp(Secondary));
        }
    }
}
