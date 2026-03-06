using System;
using System.Collections.Generic;
using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using GameModding.Shared.Serialization;

namespace Cortex.Core.Services
{
    public sealed class JsonProjectConfigurationStore : IProjectConfigurationStore
    {
        // Uses the shared manual serializer so Cortex config stays engine-agnostic and Mono-safe.
        [Serializable]
        private sealed class ProjectFileModel
        {
            public CortexProjectDefinition[] Projects;
        }

        private readonly string _path;

        public JsonProjectConfigurationStore(string path)
        {
            _path = path;
        }

        public IList<CortexProjectDefinition> LoadProjects()
        {
            if (string.IsNullOrEmpty(_path) || !File.Exists(_path))
            {
                return new List<CortexProjectDefinition>();
            }

            try
            {
                var json = File.ReadAllText(_path);
                var model = ManualJson.Deserialize<ProjectFileModel>(json);
                return model != null && model.Projects != null
                    ? new List<CortexProjectDefinition>(model.Projects)
                    : new List<CortexProjectDefinition>();
            }
            catch
            {
                return new List<CortexProjectDefinition>();
            }
        }

        public void SaveProjects(IList<CortexProjectDefinition> projects)
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var model = new ProjectFileModel
            {
                Projects = projects != null ? new List<CortexProjectDefinition>(projects).ToArray() : new CortexProjectDefinition[0]
            };

            var json = ManualJson.Serialize(model);
            File.WriteAllText(_path, json);
        }
    }
}
