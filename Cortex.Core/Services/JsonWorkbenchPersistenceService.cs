using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using GameModding.Shared.Serialization;

namespace Cortex.Core.Services
{
    public sealed class JsonWorkbenchPersistenceService : IWorkbenchPersistenceService
    {
        private readonly string _path;

        public JsonWorkbenchPersistenceService(string path)
        {
            _path = path;
        }

        public PersistedWorkbenchState Load(string workspaceId)
        {
            if (string.IsNullOrEmpty(_path) || !File.Exists(_path))
            {
                return new PersistedWorkbenchState();
            }

            try
            {
                var json = File.ReadAllText(_path);
                var state = ManualJson.Deserialize<PersistedWorkbenchState>(json);
                return state ?? new PersistedWorkbenchState();
            }
            catch
            {
                return new PersistedWorkbenchState();
            }
        }

        public void Save(string workspaceId, PersistedWorkbenchState state)
        {
            var effective = state ?? new PersistedWorkbenchState();
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_path, ManualJson.Serialize(effective));
        }
    }
}
