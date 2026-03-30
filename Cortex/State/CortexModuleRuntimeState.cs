using System;
using System.Collections.Generic;
using Cortex.Core.Models;
using Cortex.Plugins.Abstractions;

namespace Cortex
{
    public sealed class CortexModuleRuntimeState
    {
        private readonly Dictionary<string, CortexModuleStateBucket> _modules = new Dictionary<string, CortexModuleStateBucket>(StringComparer.OrdinalIgnoreCase);

        internal CortexModuleStateBucket GetOrCreateModule(string moduleId)
        {
            if (string.IsNullOrEmpty(moduleId))
            {
                moduleId = string.Empty;
            }

            CortexModuleStateBucket moduleState;
            if (!_modules.TryGetValue(moduleId, out moduleState))
            {
                moduleState = new CortexModuleStateBucket();
                _modules[moduleId] = moduleState;
            }

            return moduleState;
        }

        internal bool TryGetModule(string moduleId, out CortexModuleStateBucket moduleState)
        {
            return _modules.TryGetValue(moduleId ?? string.Empty, out moduleState);
        }

        internal void ImportPersistentEntries(PersistedModuleStateEntry[] entries)
        {
            _modules.Clear();
            if (entries == null)
            {
                return;
            }

            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.ModuleId) || string.IsNullOrEmpty(entry.Key))
                {
                    continue;
                }

                GetOrCreateModule(entry.ModuleId).PersistentValues[entry.Key] = entry.Value ?? string.Empty;
            }
        }

        internal PersistedModuleStateEntry[] ExportPersistentEntries()
        {
            var results = new List<PersistedModuleStateEntry>();
            foreach (var modulePair in _modules)
            {
                var moduleState = modulePair.Value;
                if (moduleState == null)
                {
                    continue;
                }

                foreach (var valuePair in moduleState.PersistentValues)
                {
                    results.Add(new PersistedModuleStateEntry
                    {
                        ModuleId = modulePair.Key ?? string.Empty,
                        Key = valuePair.Key ?? string.Empty,
                        Value = valuePair.Value ?? string.Empty
                    });
                }
            }

            return results.ToArray();
        }

        internal void ClearDocumentScopes(string documentPath)
        {
            if (string.IsNullOrEmpty(documentPath))
            {
                return;
            }

            foreach (var modulePair in _modules)
            {
                var moduleState = modulePair.Value;
                if (moduleState == null)
                {
                    continue;
                }

                var scopeKeys = new List<string>();
                foreach (var scopePair in moduleState.ContextScopes)
                {
                    var scopeState = scopePair.Value;
                    if (scopeState == null || scopeState.Scope == null)
                    {
                        continue;
                    }

                    if (string.Equals(scopeState.Scope.DocumentPath, documentPath, StringComparison.OrdinalIgnoreCase))
                    {
                        scopeKeys.Add(scopePair.Key);
                    }
                }

                for (var i = 0; i < scopeKeys.Count; i++)
                {
                    moduleState.ContextScopes.Remove(scopeKeys[i]);
                }
            }
        }
    }

    internal sealed class CortexModuleStateBucket
    {
        public readonly Dictionary<string, string> PersistentValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, object> WorkflowValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, CortexContextStateBucket> ContextScopes = new Dictionary<string, CortexContextStateBucket>(StringComparer.OrdinalIgnoreCase);
    }

    internal sealed class CortexContextStateBucket
    {
        public WorkbenchContextStateScope Scope;
        public readonly Dictionary<string, object> Values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }
}
