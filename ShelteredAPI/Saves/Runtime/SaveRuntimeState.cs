using System.Collections.Generic;
using ShelteredAPI.Saves;

namespace ShelteredAPI.Saves.Runtime
{
    /// <summary>
    /// Internal coordinator for custom-save session state.
    /// Keeps save internals from reaching directly into proxy fields while
    /// preserving the existing PlatformSaveProxy surface for compatibility.
    /// </summary>
    internal static class SaveRuntimeState
    {
        internal sealed class MirroredVanillaSession
        {
            public SaveManager.SaveType ProxySlot;
            public SaveEntry Entry;
            public VanillaSaveRoute Route;
        }

        private static SaveManager.SaveType _activeCustomProxySlot = SaveManager.SaveType.Invalid;
        private static SaveManager.SaveType _currentSaveOperationProxySlot = SaveManager.SaveType.Invalid;
        private static SaveEntry _currentSaveOperationEntry;
        private static MirroredVanillaSession _activeMirroredVanillaSession;
        private static readonly Dictionary<SaveManager.SaveType, VanillaSaveRoute> _pendingMirroredVanillaLoads =
            new Dictionary<SaveManager.SaveType, VanillaSaveRoute>();

        internal static SaveEntry ActiveCustomSave
        {
            get { return PlatformSaveProxy.ActiveCustomSave; }
            set { PlatformSaveProxy.ActiveCustomSave = value; }
        }

        internal static bool HasActiveCustomSave
        {
            get { return ActiveCustomSave != null; }
        }

        internal static SaveManager.SaveType ActiveCustomProxySlot
        {
            get { return _activeCustomProxySlot; }
        }

        internal static void SetActiveCustomSession(SaveManager.SaveType proxySlot, SaveEntry entry)
        {
            ActiveCustomSave = entry;
            _activeCustomProxySlot = entry != null ? proxySlot : SaveManager.SaveType.Invalid;
            _activeMirroredVanillaSession = null;
        }

        internal static void SetActiveMirroredVanillaSession(SaveManager.SaveType proxySlot, SaveEntry entry, VanillaSaveRoute route)
        {
            ActiveCustomSave = entry;
            _activeCustomProxySlot = entry != null ? proxySlot : SaveManager.SaveType.Invalid;
            _activeMirroredVanillaSession = entry != null
                ? new MirroredVanillaSession
                {
                    ProxySlot = proxySlot,
                    Entry = entry,
                    Route = route
                }
                : null;

            ModAPI.Core.MMLog.WriteDebug("[SaveRuntime] Active mirrored vanilla session. proxySlot=" + proxySlot
                + ", vanillaSlot=" + route.VanillaSlotNumber
                + ", scenario=" + (entry != null ? entry.scenarioId : "<null>")
                + ", saveId=" + (entry != null ? entry.id : "<null>")
                + ", absoluteSlot=" + (entry != null ? entry.absoluteSlot.ToString() : "<null>") + ".");
        }

        internal static void ClearActiveCustomSession()
        {
            ActiveCustomSave = null;
            _activeCustomProxySlot = SaveManager.SaveType.Invalid;
            _activeMirroredVanillaSession = null;
        }

        internal static bool HasActiveCustomSessionFor(SaveManager.SaveType type)
        {
            return ActiveCustomSave != null && _activeCustomProxySlot == type && type != SaveManager.SaveType.Invalid;
        }

        internal static bool TryGetActiveMirroredVanillaSessionFor(SaveManager.SaveType type, out MirroredVanillaSession session)
        {
            session = _activeMirroredVanillaSession;
            if (session == null || session.Entry == null || ActiveCustomSave == null)
                return false;

            return session.ProxySlot == type
                && _activeCustomProxySlot == type
                && type != SaveManager.SaveType.Invalid
                && type != SaveManager.SaveType.GlobalData
                && ActiveCustomSave.absoluteSlot == session.Entry.absoluteSlot
                && string.Equals(ActiveCustomSave.id, session.Entry.id, System.StringComparison.OrdinalIgnoreCase);
        }

        internal static bool TryBeginSaveOperation(SaveManager.SaveType type)
        {
            if (type == SaveManager.SaveType.Invalid || type == SaveManager.SaveType.GlobalData)
            {
                ClearCurrentSaveOperation(type);
                return false;
            }

            PlatformSaveProxy.Target pending;
            if (TryGetPendingSave(type, out pending) && pending != null)
            {
                SaveEntry pendingEntry = ResolveEntry(pending);
                if (pendingEntry != null)
                {
                    SetCurrentSaveOperation(type, pendingEntry, "pending-custom-save");
                    return true;
                }
            }

            if (HasActiveCustomSessionFor(type))
            {
                SetCurrentSaveOperation(type, ActiveCustomSave, "active-custom-session");
                return true;
            }

            ClearCurrentSaveOperation(type);
            return false;
        }

        internal static bool TryGetCurrentSaveOperation(out SaveManager.SaveType type, out SaveEntry entry)
        {
            type = _currentSaveOperationProxySlot;
            entry = _currentSaveOperationEntry;
            return entry != null && type != SaveManager.SaveType.Invalid && type != SaveManager.SaveType.GlobalData;
        }

        internal static void ClearCurrentSaveOperation(SaveManager.SaveType type)
        {
            if (_currentSaveOperationEntry == null)
                return;

            if (type != SaveManager.SaveType.Invalid && _currentSaveOperationProxySlot != type)
                return;

            _currentSaveOperationEntry = null;
            _currentSaveOperationProxySlot = SaveManager.SaveType.Invalid;
        }

        private static void SetCurrentSaveOperation(SaveManager.SaveType type, SaveEntry entry, string reason)
        {
            _currentSaveOperationProxySlot = type;
            _currentSaveOperationEntry = entry;
            ModAPI.Core.MMLog.WriteDebug("[SaveRuntime] Scoped save operation context. reason=" + reason
                + ", proxySlot=" + type
                + ", scenario=" + (entry != null ? entry.scenarioId : "<null>")
                + ", saveId=" + (entry != null ? entry.id : "<null>")
                + ", absoluteSlot=" + (entry != null ? entry.absoluteSlot.ToString() : "<null>") + ".");
        }

        private static SaveEntry ResolveEntry(PlatformSaveProxy.Target target)
        {
            if (target == null || string.IsNullOrEmpty(target.saveId))
                return null;

            string scopeId = SaveStorageRouter.NormalizeScenarioId(target.scenarioId);
            return SaveStorageRouter.Get(scopeId, target.saveId);
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

        internal static void MarkPendingMirroredVanillaLoad(SaveManager.SaveType type, VanillaSaveRoute route)
        {
            lock (PlatformSaveProxy._nextLoadLock)
            {
                _pendingMirroredVanillaLoads[type] = route;
            }
        }

        internal static bool TryConsumePendingMirroredVanillaLoad(SaveManager.SaveType type, out VanillaSaveRoute route)
        {
            lock (PlatformSaveProxy._nextLoadLock)
            {
                if (_pendingMirroredVanillaLoads.TryGetValue(type, out route))
                {
                    _pendingMirroredVanillaLoads.Remove(type);
                    return true;
                }
            }

            route = new VanillaSaveRoute();
            return false;
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
                _pendingMirroredVanillaLoads.Remove(type);
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

        internal static bool ClearPendingSaveIfMatches(SaveManager.SaveType type, PlatformSaveProxy.Target expectedTarget)
        {
            if (expectedTarget == null)
                return false;

            lock (PlatformSaveProxy._nextSaveLock)
            {
                PlatformSaveProxy.Target currentTarget;
                if (!PlatformSaveProxy.NextSave.TryGetValue(type, out currentTarget))
                    return false;

                bool matches = object.ReferenceEquals(currentTarget, expectedTarget)
                    || (currentTarget != null
                        && string.Equals(
                            SaveStorageRouter.NormalizeScenarioId(currentTarget.scenarioId),
                            SaveStorageRouter.NormalizeScenarioId(expectedTarget.scenarioId),
                            System.StringComparison.OrdinalIgnoreCase)
                        && string.Equals(currentTarget.saveId, expectedTarget.saveId, System.StringComparison.OrdinalIgnoreCase));

                if (!matches)
                    return false;

                return PlatformSaveProxy.NextSave.Remove(type);
            }
        }

        internal static bool ClearPendingLoadIfMatches(SaveManager.SaveType type, PlatformSaveProxy.Target expectedTarget)
        {
            if (expectedTarget == null)
                return false;

            lock (PlatformSaveProxy._nextLoadLock)
            {
                PlatformSaveProxy.Target currentTarget;
                if (!PlatformSaveProxy.NextLoad.TryGetValue(type, out currentTarget))
                    return false;

                bool matches = object.ReferenceEquals(currentTarget, expectedTarget)
                    || (currentTarget != null
                        && string.Equals(
                            SaveStorageRouter.NormalizeScenarioId(currentTarget.scenarioId),
                            SaveStorageRouter.NormalizeScenarioId(expectedTarget.scenarioId),
                            System.StringComparison.OrdinalIgnoreCase)
                        && string.Equals(currentTarget.saveId, expectedTarget.saveId, System.StringComparison.OrdinalIgnoreCase));

                if (!matches)
                    return false;

                _pendingMirroredVanillaLoads.Remove(type);
                return PlatformSaveProxy.NextLoad.Remove(type);
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
            if (ActiveCustomSave != null && ActiveCustomSave.id == deletedSaveId)
            {
                ClearActiveCustomSession();
            }

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
                    _pendingMirroredVanillaLoads.Remove(loadKeys[i]);
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
