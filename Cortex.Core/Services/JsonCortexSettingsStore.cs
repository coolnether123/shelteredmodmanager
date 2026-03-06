using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using GameModding.Shared.Serialization;

namespace Cortex.Core.Services
{
    public sealed class JsonCortexSettingsStore : ICortexSettingsStore
    {
        // Uses the shared manual serializer so runtime tooling does not depend on Unity or System.Web JSON.
        private readonly string _path;

        public JsonCortexSettingsStore(string path)
        {
            _path = path;
        }

        public CortexSettings Load()
        {
            if (string.IsNullOrEmpty(_path) || !File.Exists(_path))
            {
                return new CortexSettings();
            }

            try
            {
                var json = File.ReadAllText(_path);
                var settings = ManualJson.Deserialize<CortexSettings>(json);
                return settings ?? new CortexSettings();
            }
            catch
            {
                return new CortexSettings();
            }
        }

        public void Save(CortexSettings settings)
        {
            var effective = settings ?? new CortexSettings();
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = ManualJson.Serialize(effective);
            File.WriteAllText(_path, json);
        }
    }
}
