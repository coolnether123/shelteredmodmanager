using System;
using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Core.Services
{
    public sealed class ProjectCatalog : IProjectCatalog
    {
        private readonly IProjectConfigurationStore _store;
        private readonly List<CortexProjectDefinition> _projects;

        public ProjectCatalog(IProjectConfigurationStore store)
        {
            _store = store;
            _projects = new List<CortexProjectDefinition>(_store.LoadProjects());
        }

        public IList<CortexProjectDefinition> GetProjects()
        {
            return new List<CortexProjectDefinition>(_projects);
        }

        public CortexProjectDefinition GetProject(string modId)
        {
            if (string.IsNullOrEmpty(modId))
            {
                return null;
            }

            for (var i = 0; i < _projects.Count; i++)
            {
                if (string.Equals(_projects[i].ModId, modId, StringComparison.OrdinalIgnoreCase))
                {
                    return _projects[i];
                }
            }

            return null;
        }

        public void Upsert(CortexProjectDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.ModId))
            {
                return;
            }

            for (var i = 0; i < _projects.Count; i++)
            {
                if (string.Equals(_projects[i].ModId, definition.ModId, StringComparison.OrdinalIgnoreCase))
                {
                    _projects[i] = definition;
                    _store.SaveProjects(_projects);
                    return;
                }
            }

            _projects.Add(definition);
            _store.SaveProjects(_projects);
        }

        public void Remove(string modId)
        {
            if (string.IsNullOrEmpty(modId))
            {
                return;
            }

            for (var i = _projects.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_projects[i].ModId, modId, StringComparison.OrdinalIgnoreCase))
                {
                    _projects.RemoveAt(i);
                }
            }

            _store.SaveProjects(_projects);
        }
    }
}
