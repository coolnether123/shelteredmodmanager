using ModAPI.Core;
using ShelteredAPI.Input;
using UnityEngine;

namespace ShelteredAPI.Core
{
    /// <summary>
    /// Flushes keybind changes to ModPrefs during quit/unload to avoid edge-case data loss.
    /// </summary>
    public sealed class ShelteredKeybindPersistenceGuard : MonoBehaviour
    {
        private static bool _flushed;

        private void OnApplicationQuit()
        {
            Flush("OnApplicationQuit");
        }

        private void OnDisable()
        {
            // Unity 5.3 shutdown paths can skip OnApplicationQuit in some edge cases.
            Flush("OnDisable");
        }

        private void Flush(string source)
        {
            if (_flushed)
                return;

            try
            {
                ShelteredKeybindsProvider.Instance.Save();
                _flushed = true;
                MMLog.WriteInfo("[ShelteredKeybindPersistenceGuard] Keybinds flushed from " + source + ".");
            }
            catch (System.Exception ex)
            {
                MMLog.WriteWarning("[ShelteredKeybindPersistenceGuard] Failed to flush keybinds from " + source + ": " + ex.Message);
            }
        }
    }
}
