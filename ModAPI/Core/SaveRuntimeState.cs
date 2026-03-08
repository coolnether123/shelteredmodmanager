using System.Collections.Generic;
using ModAPI.Hooks;
using ModAPI.Saves;

namespace ModAPI.Core
{
    /// <summary>
    /// Internal coordinator for custom-save session state.
    /// Keeps ModAPI internals from reaching directly into proxy fields while
    /// preserving the existing PlatformSaveProxy surface for compatibility.
    /// </summary>
    internal static class SaveRuntimeState
    {
        internal static SaveEntry ActiveCustomSave
        {
            get { return PlatformSaveProxy.ActiveCustomSave; }
            set { PlatformSaveProxy.ActiveCustomSave = value; }
        }

        internal static bool HasActiveCustomSave
        {
            get { return ActiveCustomSave != null; }
        }

        internal static bool HasPendingSave(SaveManager.SaveType type)
        {
            lock (PlatformSaveProxy._nextSaveLock)
            {
                return PlatformSaveProxy.NextSave.ContainsKey(type);
            }
        }

        internal static bool HasAnyPendingSave()
        {
            lock (PlatformSaveProxy._nextSaveLock)
            {
                return PlatformSaveProxy.NextSave.Count > 0;
            }
        }

        internal static bool TryGetPendingSave(SaveManager.SaveType type, out PlatformSaveProxy.Target target)
        {
            lock (PlatformSaveProxy._nextSaveLock)
            {
                return PlatformSaveProxy.NextSave.TryGetValue(type, out target);
            }
        }

        internal static bool TryGetPendingLoad(SaveManager.SaveType type, out PlatformSaveProxy.Target target)
        {
            lock (PlatformSaveProxy._nextLoadLock)
            {
                return PlatformSaveProxy.NextLoad.TryGetValue(type, out target);
            }
        }

        internal static void SetPendingLoad(SaveManager.SaveType type, string scenarioId, string saveId)
        {
            lock (PlatformSaveProxy._nextLoadLock)
            {
                PlatformSaveProxy.NextLoad[type] = new PlatformSaveProxy.Target { scenarioId = scenarioId, saveId = saveId };
            }
        }

        internal static void SetPendingSave(SaveManager.SaveType type, string scenarioId, string saveId)
        {
            lock (PlatformSaveProxy._nextSaveLock)
            {
                PlatformSaveProxy.NextSave[type] = new PlatformSaveProxy.Target { scenarioId = scenarioId, saveId = saveId };
            }
        }

        internal static bool ClearPendingLoad(SaveManager.SaveType type)
        {
            lock (PlatformSaveProxy._nextLoadLock)
            {
                return PlatformSaveProxy.NextLoad.Remove(type);
            }
        }

        internal static bool ClearPendingSave(SaveManager.SaveType type)
        {
            lock (PlatformSaveProxy._nextSaveLock)
            {
                return PlatformSaveProxy.NextSave.Remove(type);
            }
        }

        internal static KeyValuePair<SaveManager.SaveType, PlatformSaveProxy.Target> GetNextSaveTargetAndClear()
        {
            lock (PlatformSaveProxy._nextSaveLock)
            {
                if (PlatformSaveProxy.NextSave.Count == 0)
                {
                    throw new System.InvalidOperationException("GetNextSaveTargetAndClear called with no pending save targets.");
                }

                var e = PlatformSaveProxy.NextSave.GetEnumerator();
                e.MoveNext();
                var target = e.Current;
                PlatformSaveProxy.NextSave.Clear();
                return target;
            }
        }

        internal static void ClearTrackedReferences(SaveManager.SaveType requestedType, string deletedSaveId)
        {
            lock (PlatformSaveProxy._nextLoadLock)
            {
                var loadKeys = new List<SaveManager.SaveType>();
                foreach (var pair in PlatformSaveProxy.NextLoad)
                {
                    if (pair.Key == requestedType || (pair.Value != null && pair.Value.saveId == deletedSaveId))
                    {
                        loadKeys.Add(pair.Key);
                    }
                }

                for (int i = 0; i < loadKeys.Count; i++)
                {
                    PlatformSaveProxy.NextLoad.Remove(loadKeys[i]);
                }
            }

            lock (PlatformSaveProxy._nextSaveLock)
            {
                var saveKeys = new List<SaveManager.SaveType>();
                foreach (var pair in PlatformSaveProxy.NextSave)
                {
                    if (pair.Key == requestedType || (pair.Value != null && pair.Value.saveId == deletedSaveId))
                    {
                        saveKeys.Add(pair.Key);
                    }
                }

                for (int i = 0; i < saveKeys.Count; i++)
                {
                    PlatformSaveProxy.NextSave.Remove(saveKeys[i]);
                }
            }
        }

        internal static string GetSaveSlotKey(SaveManager.SaveType type)
        {
            if (type == SaveManager.SaveType.GlobalData) return "Global";
            if (type == SaveManager.SaveType.Invalid) return "Invalid";

            PlatformSaveProxy.Target pendingLoad;
            if (TryGetPendingLoad(type, out pendingLoad) && pendingLoad != null)
            {
                return string.Format("{0}_{1}", pendingLoad.scenarioId, pendingLoad.saveId);
            }

            var active = ActiveCustomSave;
            if (active != null)
            {
                string scenario = string.IsNullOrEmpty(active.scenarioId) ? "Standard" : active.scenarioId;
                return string.Format("{0}_{1}", scenario, active.id);
            }

            return type.ToString();
        }
    }
}
