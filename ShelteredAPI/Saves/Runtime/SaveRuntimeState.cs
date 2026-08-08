using System.Collections.Generic;
using ShelteredAPI.Saves;

namespace ShelteredAPI.Saves.Runtime
{
    /// <summary>
    /// Owns custom-save session state and exposes synchronized state transitions.
    /// PlatformSaveProxy is limited to adapting the game's platform I/O surface.
    /// </summary>
    internal static class SaveRuntimeState
    {
        internal sealed class Target
        {
            public string ScenarioId;
            public string SaveId;
        }

        internal sealed class PendingOperation
        {
            public SaveManager.SaveType SaveType;
            public Target Target;
            public bool IsSave;
        }

        internal sealed class MirroredVanillaSession
        {
            public SaveManager.SaveType ProxySlot;
            public SaveEntry Entry;
            public VanillaSaveRoute Route;
        }

        private static readonly object Sync = new object();
        private static readonly Dictionary<SaveManager.SaveType, Target> PendingLoads =
            new Dictionary<SaveManager.SaveType, Target>();
        private static readonly Dictionary<SaveManager.SaveType, Target> PendingSaves =
            new Dictionary<SaveManager.SaveType, Target>();
        private static SaveEntry _activeCustomSave;
        private static SaveManager.SaveType _activeCustomProxySlot = SaveManager.SaveType.Invalid;
        private static SaveManager.SaveType _currentSaveOperationProxySlot = SaveManager.SaveType.Invalid;
        private static SaveEntry _currentSaveOperationEntry;
        private static MirroredVanillaSession _activeMirroredVanillaSession;
        private static readonly Dictionary<SaveManager.SaveType, VanillaSaveRoute> _pendingMirroredVanillaLoads =
            new Dictionary<SaveManager.SaveType, VanillaSaveRoute>();

        internal static SaveEntry ActiveCustomSave
        {
            get { lock (Sync) return _activeCustomSave; }
        }

        internal static void ReplaceActiveCustomSave(SaveEntry entry)
        {
            lock (Sync)
                _activeCustomSave = entry;
        }

        internal static bool HasActiveCustomSave
        {
            get { lock (Sync) return _activeCustomSave != null; }
        }

        internal static SaveManager.SaveType ActiveCustomProxySlot
        {
            get { lock (Sync) return _activeCustomProxySlot; }
        }

        internal static void SetActiveCustomSession(SaveManager.SaveType proxySlot, SaveEntry entry)
        {
            lock (Sync)
            {
                _activeCustomSave = entry;
                _activeCustomProxySlot = entry != null ? proxySlot : SaveManager.SaveType.Invalid;
                _activeMirroredVanillaSession = null;
            }
        }

        internal static void SetActiveMirroredVanillaSession(SaveManager.SaveType proxySlot, SaveEntry entry, VanillaSaveRoute route)
        {
            lock (Sync)
            {
                _activeCustomSave = entry;
                _activeCustomProxySlot = entry != null ? proxySlot : SaveManager.SaveType.Invalid;
                _activeMirroredVanillaSession = entry != null
                    ? new MirroredVanillaSession
                    {
                        ProxySlot = proxySlot,
                        Entry = entry,
                        Route = route
                    }
                    : null;
            }

            ModAPI.Core.MMLog.WriteDebug("[SaveRuntime] Active mirrored vanilla session. proxySlot=" + proxySlot
                + ", vanillaSlot=" + route.VanillaSlotNumber
                + ", scenario=" + (entry != null ? entry.scenarioId : "<null>")
                + ", saveId=" + (entry != null ? entry.id : "<null>")
                + ", absoluteSlot=" + (entry != null ? entry.absoluteSlot.ToString() : "<null>") + ".");
        }

        internal static void ClearActiveCustomSession()
        {
            lock (Sync)
            {
                _activeCustomSave = null;
                _activeCustomProxySlot = SaveManager.SaveType.Invalid;
                _activeMirroredVanillaSession = null;
            }
        }

        internal static bool HasActiveCustomSessionFor(SaveManager.SaveType type)
        {
            lock (Sync)
            {
                return _activeCustomSave != null
                    && _activeCustomProxySlot == type
                    && type != SaveManager.SaveType.Invalid;
            }
        }

        internal static bool TryGetActiveMirroredVanillaSessionFor(SaveManager.SaveType type, out MirroredVanillaSession session)
        {
            lock (Sync)
            {
                session = _activeMirroredVanillaSession;
                if (session == null || session.Entry == null || _activeCustomSave == null)
                    return false;

                return session.ProxySlot == type
                    && _activeCustomProxySlot == type
                    && type != SaveManager.SaveType.Invalid
                    && type != SaveManager.SaveType.GlobalData
                    && _activeCustomSave.absoluteSlot == session.Entry.absoluteSlot
                    && string.Equals(_activeCustomSave.id, session.Entry.id, System.StringComparison.OrdinalIgnoreCase);
            }
        }

        internal static bool TryBeginSaveOperation(SaveManager.SaveType type)
        {
            if (type == SaveManager.SaveType.Invalid || type == SaveManager.SaveType.GlobalData)
            {
                ClearCurrentSaveOperation(type);
                return false;
            }

            Target pending;
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
            lock (Sync)
            {
                type = _currentSaveOperationProxySlot;
                entry = _currentSaveOperationEntry;
                return entry != null && type != SaveManager.SaveType.Invalid && type != SaveManager.SaveType.GlobalData;
            }
        }

        internal static void ClearCurrentSaveOperation(SaveManager.SaveType type)
        {
            lock (Sync)
            {
                if (_currentSaveOperationEntry == null)
                    return;

                if (type != SaveManager.SaveType.Invalid && _currentSaveOperationProxySlot != type)
                    return;

                _currentSaveOperationEntry = null;
                _currentSaveOperationProxySlot = SaveManager.SaveType.Invalid;
            }
        }

        private static void SetCurrentSaveOperation(SaveManager.SaveType type, SaveEntry entry, string reason)
        {
            lock (Sync)
            {
                _currentSaveOperationProxySlot = type;
                _currentSaveOperationEntry = entry;
            }
            ModAPI.Core.MMLog.WriteDebug("[SaveRuntime] Scoped save operation context. reason=" + reason
                + ", proxySlot=" + type
                + ", scenario=" + (entry != null ? entry.scenarioId : "<null>")
                + ", saveId=" + (entry != null ? entry.id : "<null>")
                + ", absoluteSlot=" + (entry != null ? entry.absoluteSlot.ToString() : "<null>") + ".");
        }

        private static SaveEntry ResolveEntry(Target target)
        {
            if (target == null || string.IsNullOrEmpty(target.SaveId))
                return null;

            string scopeId = SaveStorageRouter.NormalizeScenarioId(target.ScenarioId);
            return SaveStorageRouter.Get(scopeId, target.SaveId);
        }

        internal static bool HasPendingSave(SaveManager.SaveType type)
        {
            lock (Sync)
            {
                return PendingSaves.ContainsKey(type);
            }
        }

        internal static bool HasAnyPendingSave()
        {
            lock (Sync)
            {
                return PendingSaves.Count > 0;
            }
        }

        internal static bool TryGetPendingSave(SaveManager.SaveType type, out Target target)
        {
            lock (Sync)
            {
                return PendingSaves.TryGetValue(type, out target);
            }
        }

        internal static bool TryGetPendingLoad(SaveManager.SaveType type, out Target target)
        {
            lock (Sync)
            {
                return PendingLoads.TryGetValue(type, out target);
            }
        }

        internal static void QueueLoad(SaveManager.SaveType type, string scenarioId, string saveId)
        {
            EnsureProxyInjected();
            lock (Sync)
            {
                PendingLoads[type] = new Target { ScenarioId = scenarioId, SaveId = saveId };
            }
        }

        internal static void MarkPendingMirroredVanillaLoad(SaveManager.SaveType type, VanillaSaveRoute route)
        {
            lock (Sync)
            {
                _pendingMirroredVanillaLoads[type] = route;
            }
        }

        internal static bool TryConsumePendingMirroredVanillaLoad(SaveManager.SaveType type, out VanillaSaveRoute route)
        {
            lock (Sync)
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

        internal static void QueueSave(SaveManager.SaveType type, string scenarioId, string saveId)
        {
            EnsureProxyInjected();
            lock (Sync)
            {
                PendingSaves[type] = new Target { ScenarioId = scenarioId, SaveId = saveId };
            }
        }

        internal static bool ClearPendingLoad(SaveManager.SaveType type)
        {
            lock (Sync)
            {
                _pendingMirroredVanillaLoads.Remove(type);
                return PendingLoads.Remove(type);
            }
        }

        internal static bool ClearPendingSave(SaveManager.SaveType type)
        {
            lock (Sync)
            {
                return PendingSaves.Remove(type);
            }
        }

        internal static bool ClearPendingSaveIfMatches(SaveManager.SaveType type, Target expectedTarget)
        {
            if (expectedTarget == null)
                return false;

            lock (Sync)
            {
                Target currentTarget;
                if (!PendingSaves.TryGetValue(type, out currentTarget))
                    return false;

                bool matches = object.ReferenceEquals(currentTarget, expectedTarget)
                    || (currentTarget != null
                        && string.Equals(
                            SaveStorageRouter.NormalizeScenarioId(currentTarget.ScenarioId),
                            SaveStorageRouter.NormalizeScenarioId(expectedTarget.ScenarioId),
                            System.StringComparison.OrdinalIgnoreCase)
                        && string.Equals(currentTarget.SaveId, expectedTarget.SaveId, System.StringComparison.OrdinalIgnoreCase));

                if (!matches)
                    return false;

                return PendingSaves.Remove(type);
            }
        }

        internal static bool ClearPendingLoadIfMatches(SaveManager.SaveType type, Target expectedTarget)
        {
            if (expectedTarget == null)
                return false;

            lock (Sync)
            {
                Target currentTarget;
                if (!PendingLoads.TryGetValue(type, out currentTarget))
                    return false;

                bool matches = object.ReferenceEquals(currentTarget, expectedTarget)
                    || (currentTarget != null
                        && string.Equals(
                            SaveStorageRouter.NormalizeScenarioId(currentTarget.ScenarioId),
                            SaveStorageRouter.NormalizeScenarioId(expectedTarget.ScenarioId),
                            System.StringComparison.OrdinalIgnoreCase)
                        && string.Equals(currentTarget.SaveId, expectedTarget.SaveId, System.StringComparison.OrdinalIgnoreCase));

                if (!matches)
                    return false;

                _pendingMirroredVanillaLoads.Remove(type);
                return PendingLoads.Remove(type);
            }
        }

        internal static PendingOperation[] SnapshotPendingOperations()
        {
            lock (Sync)
            {
                List<PendingOperation> result = new List<PendingOperation>(PendingSaves.Count + PendingLoads.Count);
                foreach (KeyValuePair<SaveManager.SaveType, Target> pair in PendingSaves)
                    result.Add(new PendingOperation { SaveType = pair.Key, Target = pair.Value, IsSave = true });
                foreach (KeyValuePair<SaveManager.SaveType, Target> pair in PendingLoads)
                    result.Add(new PendingOperation { SaveType = pair.Key, Target = pair.Value, IsSave = false });
                return result.ToArray();
            }
        }

        internal static void ClearTrackedReferences(SaveManager.SaveType requestedType, string deletedSaveId)
        {
            if (ActiveCustomSave != null && ActiveCustomSave.id == deletedSaveId)
            {
                ClearActiveCustomSession();
            }

            lock (Sync)
            {
                var loadKeys = new List<SaveManager.SaveType>();
                foreach (var pair in PendingLoads)
                {
                    if (pair.Key == requestedType || (pair.Value != null && pair.Value.SaveId == deletedSaveId))
                    {
                        loadKeys.Add(pair.Key);
                    }
                }

                for (int i = 0; i < loadKeys.Count; i++)
                {
                    PendingLoads.Remove(loadKeys[i]);
                    _pendingMirroredVanillaLoads.Remove(loadKeys[i]);
                }
            }

            lock (Sync)
            {
                var saveKeys = new List<SaveManager.SaveType>();
                foreach (var pair in PendingSaves)
                {
                    if (pair.Key == requestedType || (pair.Value != null && pair.Value.SaveId == deletedSaveId))
                    {
                        saveKeys.Add(pair.Key);
                    }
                }

                for (int i = 0; i < saveKeys.Count; i++)
                {
                    PendingSaves.Remove(saveKeys[i]);
                }
            }
        }

        internal static string GetSaveSlotKey(SaveManager.SaveType type)
        {
            if (type == SaveManager.SaveType.GlobalData) return "Global";
            if (type == SaveManager.SaveType.Invalid) return "Invalid";

            Target pendingLoad;
            if (TryGetPendingLoad(type, out pendingLoad) && pendingLoad != null)
            {
                return string.Format("{0}_{1}", pendingLoad.ScenarioId, pendingLoad.SaveId);
            }

            var active = ActiveCustomSave;
            if (active != null)
            {
                string scenario = string.IsNullOrEmpty(active.scenarioId) ? "Standard" : active.scenarioId;
                return string.Format("{0}_{1}", scenario, active.id);
            }

            return type.ToString();
        }

        private static void EnsureProxyInjected()
        {
            try { SaveManager_Injection_Patch.Inject(SaveManager.instance); }
            catch { }
        }
    }
}
