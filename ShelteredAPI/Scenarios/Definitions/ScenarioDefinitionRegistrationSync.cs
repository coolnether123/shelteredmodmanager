using System;
using System.Collections.Generic;
using System.IO;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Registration;
namespace ShelteredAPI.Scenarios.Definitions{
    internal sealed class ScenarioDefinitionRegistrationSync : IScenarioDefinitionCatalogService
    {
        private readonly IScenarioDefinitionCatalog _definitionCatalog;
        private readonly IScenarioDefinitionReader _definitionReader;
        private readonly IScenarioRegistrationStore _store;
        private readonly ScenarioRecordFactory _recordFactory;
        private readonly ScenarioSaveDescriptorMirror _saveDescriptorMirror;
        private readonly IScenarioDefinitionDependencyReader _dependencyReader;
        private readonly IScenarioDefinitionFactory _definitionFactory;
        private readonly object _refreshSync = new object();
        private readonly Dictionary<string, ScenarioCatalogPathStamp> _publishedDefinitions = new Dictionary<string, ScenarioCatalogPathStamp>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ScenarioCatalogPathStamp> _draftFiles = new Dictionary<string, ScenarioCatalogPathStamp>(StringComparer.OrdinalIgnoreCase);
        private ScenarioCatalogPathStamp _draftRootStamp;
        private int _catalogRevision;

        public ScenarioDefinitionRegistrationSync(
            IScenarioDefinitionCatalog definitionCatalog,
            IScenarioDefinitionReader definitionReader,
            IScenarioRegistrationStore store,
            ScenarioRecordFactory recordFactory,
            ScenarioSaveDescriptorMirror saveDescriptorMirror,
            IScenarioDefinitionDependencyReader dependencyReader,
            IScenarioDefinitionFactory definitionFactory)
        {
            _definitionCatalog = definitionCatalog;
            _definitionReader = definitionReader;
            _store = store;
            _recordFactory = recordFactory;
            _saveDescriptorMirror = saveDescriptorMirror;
            _dependencyReader = dependencyReader;
            _definitionFactory = definitionFactory;
        }

        public void RefreshDefinitionCatalog()
        {
            lock (_refreshSync)
            {
                _definitionCatalog.Refresh();
                ScenarioInfo[] publishedDefinitions = _definitionCatalog.ListAll();
                bool definitionsChanged = HavePublishedDefinitionsChanged(publishedDefinitions);
                if (!definitionsChanged && !HaveDraftDefinitionsChanged())
                    return;

                ScenarioInfo[] definitions = _definitionReader.ListAll();
                SyncDefinitionRegistrations(definitions);
                CapturePublishedDefinitionSnapshot(publishedDefinitions);
                CaptureDraftSnapshot(definitions);
                _catalogRevision++;
            }
        }

        public int CatalogRevision
        {
            get { return _catalogRevision; }
        }

        public ScenarioInfo[] ListDefinitions()
        {
            return _definitionReader.ListAll();
        }

        public ScenarioValidationResult ValidateDefinition(string scenarioId)
        {
            return _definitionReader.Validate(scenarioId);
        }

        public bool TryLoadDefinition(string scenarioId, out ScenarioDefinition definition, out string scenarioFilePath, out ScenarioValidationResult validation)
        {
            return _definitionReader.TryLoad(scenarioId, out definition, out scenarioFilePath, out validation);
        }

        private void SyncDefinitionRegistrations(ScenarioInfo[] definitions)
        {
            Dictionary<string, ScenarioInfo> current = new Dictionary<string, ScenarioInfo>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < definitions.Length; i++)
            {
                if (definitions[i] != null
                    && !string.IsNullOrEmpty(definitions[i].Id)
                    && !IsAuthoringDraftDefinition(definitions[i]))
                {
                    current[definitions[i].Id] = definitions[i];
                }
            }

            ScenarioRecord[] records = _store.ListRecords();
            for (int i = 0; i < records.Length; i++)
            {
                ScenarioRecord record = records[i];
                if (record != null && record.Info != null && record.IsDefinitionBacked && !current.ContainsKey(record.Info.Id))
                {
                    ScenarioRecord removed;
                    _store.Remove(record.Info.Id, out removed);
                }
            }

            for (int i = 0; i < definitions.Length; i++)
            {
                ScenarioInfo definition = definitions[i];
                if (definition == null || string.IsNullOrEmpty(definition.Id))
                    continue;
                if (IsAuthoringDraftDefinition(definition))
                    continue;

                ScenarioRecord existing;
                if (_store.TryGet(definition.Id, out existing) && existing != null && !existing.IsDefinitionBacked)
                    continue;

                ScenarioRecord record = CreateDefinitionRecord(definition);
                ScenarioRecord previous;
                _store.Upsert(record, out previous);
                _saveDescriptorMirror.Mirror(record.Info);
            }
        }

        private bool HaveDraftDefinitionsChanged()
        {
            ScenarioCatalogPathStamp currentRoot = ScenarioCatalogDiskStamp.ReadDirectory(ResolveDraftsRootPath());
            if (!ScenarioCatalogDiskStamp.Equal(_draftRootStamp, currentRoot))
                return true;

            foreach (KeyValuePair<string, ScenarioCatalogPathStamp> pair in _draftFiles)
            {
                if (!ScenarioCatalogDiskStamp.Equal(pair.Value, ScenarioCatalogDiskStamp.ReadFile(pair.Key)))
                    return true;
            }

            return false;
        }

        private bool HavePublishedDefinitionsChanged(ScenarioInfo[] definitions)
        {
            int definitionCount = definitions != null ? definitions.Length : 0;
            if (definitionCount != _publishedDefinitions.Count)
                return true;

            for (int i = 0; definitions != null && i < definitions.Length; i++)
            {
                ScenarioInfo definition = definitions[i];
                string key = BuildDefinitionSnapshotKey(definition);
                ScenarioCatalogPathStamp previous;
                if (string.IsNullOrEmpty(key)
                    || !_publishedDefinitions.TryGetValue(key, out previous)
                    || !ScenarioCatalogDiskStamp.Equal(previous, ScenarioCatalogDiskStamp.ReadFile(definition.FilePath)))
                {
                    return true;
                }
            }

            return false;
        }

        private void CapturePublishedDefinitionSnapshot(ScenarioInfo[] definitions)
        {
            _publishedDefinitions.Clear();
            for (int i = 0; definitions != null && i < definitions.Length; i++)
            {
                ScenarioInfo definition = definitions[i];
                string key = BuildDefinitionSnapshotKey(definition);
                if (!string.IsNullOrEmpty(key))
                    _publishedDefinitions[key] = ScenarioCatalogDiskStamp.ReadFile(definition.FilePath);
            }
        }

        private static string BuildDefinitionSnapshotKey(ScenarioInfo definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.FilePath))
                return null;

            return ScenarioCatalogDiskStamp.NormalizePath(definition.FilePath)
                + "|" + (definition.Id ?? string.Empty)
                + "|" + (definition.OwnerModId ?? string.Empty);
        }

        private void CaptureDraftSnapshot(ScenarioInfo[] definitions)
        {
            _draftFiles.Clear();
            for (int i = 0; definitions != null && i < definitions.Length; i++)
            {
                ScenarioInfo definition = definitions[i];
                if (definition == null || !IsAuthoringDraftDefinition(definition) || string.IsNullOrEmpty(definition.FilePath))
                    continue;

                string path = ScenarioCatalogDiskStamp.NormalizePath(definition.FilePath);
                _draftFiles[path] = ScenarioCatalogDiskStamp.ReadFile(path);
            }

            _draftRootStamp = ScenarioCatalogDiskStamp.ReadDirectory(ResolveDraftsRootPath());
        }

        private static string ResolveDraftsRootPath()
        {
            string gameRoot;
            try { gameRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..")); }
            catch { gameRoot = Directory.GetCurrentDirectory(); }

            string modsRoot = Path.Combine(gameRoot, "mods");
            if (!Directory.Exists(modsRoot))
            {
                string legacyModsRoot = Path.Combine(gameRoot, "Mods");
                modsRoot = Directory.Exists(legacyModsRoot) ? legacyModsRoot : modsRoot;
            }

            return Path.Combine(Path.Combine(Path.Combine(Path.Combine(modsRoot, "ModAPI"), "User"), "Saves"), ScenarioAuthoringDraftRepository.DraftStorageScenarioId);
        }

        private ScenarioRecord CreateDefinitionRecord(ScenarioInfo definition)
        {
            ScenarioDefinition scenarioDefinition;
            string scenarioFilePath;
            string loadError;
            string description = null;
            if (_definitionReader.TryLoadUnchecked(definition.Id, out scenarioDefinition, out scenarioFilePath, out loadError)
                && scenarioDefinition != null)
                description = TrimToNull(scenarioDefinition.Description);

            CustomScenarioRegistration registration = new CustomScenarioRegistration
            {
                Id = definition.Id,
                DisplayName = TrimToNull(definition.DisplayName) ?? definition.Id,
                Description = description ?? "A custom scenario for Sheltered.",
                Version = TrimToNull(definition.Version) ?? "1.0",
                OwnerModId = TrimToNull(definition.OwnerModId),
                RequiredMods = ScenarioDependencyManifest.CloneRequiredMods(_dependencyReader.LoadDefinitionDependencies(definition.Id)),
                DefinitionFactory = new CustomScenarioDefinitionFactory(
                    delegate(CustomScenarioBuildContext context) { return _definitionFactory.BuildScenarioDefFromDefinition(definition.Id); })
            };

            ScenarioRecord record = _recordFactory.CreateRecord(registration);
            record.IsDefinitionBacked = true;
            return record;
        }

        private static string TrimToNull(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            string trimmed = value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }

        private static bool IsAuthoringDraftDefinition(ScenarioInfo definition)
        {
            if (definition == null)
                return false;

            if (string.Equals(definition.OwnerModId, ScenarioAuthoringDraftRepository.DraftOwnerId, StringComparison.OrdinalIgnoreCase))
                return true;

            return !string.IsNullOrEmpty(definition.FilePath)
                && definition.FilePath.IndexOf(ScenarioAuthoringDraftRepository.DraftStorageScenarioId, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
