using System;
using System.Collections.Generic;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioDefinitionRegistrationSync
    {
        private readonly IScenarioDefinitionSerializer _definitionSerializer;
        private readonly IScenarioDefinitionCatalog _definitionCatalog;
        private readonly IScenarioDefinitionValidator _definitionValidator;
        private readonly ScenarioAuthoringDraftRepository _draftRepository;
        private readonly IScenarioRegistrationStore _store;
        private readonly ScenarioRecordFactory _recordFactory;
        private readonly ScenarioSaveDescriptorMirror _saveDescriptorMirror;
        private readonly ScenarioDependencyService _dependencyService;
        private readonly ScenarioDefinitionService _definitionService;

        public ScenarioDefinitionRegistrationSync(
            IScenarioDefinitionSerializer definitionSerializer,
            IScenarioDefinitionCatalog definitionCatalog,
            IScenarioDefinitionValidator definitionValidator,
            ScenarioAuthoringDraftRepository draftRepository,
            IScenarioRegistrationStore store,
            ScenarioRecordFactory recordFactory,
            ScenarioSaveDescriptorMirror saveDescriptorMirror,
            ScenarioDependencyService dependencyService,
            ScenarioDefinitionService definitionService)
        {
            _definitionSerializer = definitionSerializer;
            _definitionCatalog = definitionCatalog;
            _definitionValidator = definitionValidator;
            _draftRepository = draftRepository;
            _store = store;
            _recordFactory = recordFactory;
            _saveDescriptorMirror = saveDescriptorMirror;
            _dependencyService = dependencyService;
            _definitionService = definitionService;
        }

        public void RefreshDefinitionCatalog()
        {
            _definitionCatalog.Refresh();
            SyncDefinitionRegistrations();
        }

        public ScenarioInfo[] ListDefinitions()
        {
            return GetAllDefinitionInfos();
        }

        public ScenarioValidationResult ValidateDefinition(string scenarioId)
        {
            ScenarioInfo info;
            if (!TryGetDefinitionInfo(scenarioId, out info) || info == null)
            {
                ScenarioValidationResult missing = new ScenarioValidationResult();
                missing.AddError("Scenario is not indexed: " + scenarioId);
                return missing;
            }

            try
            {
                ScenarioDefinition definition = _definitionSerializer.Load(info.FilePath);
                return _definitionValidator.Validate(definition, info.FilePath);
            }
            catch (Exception ex)
            {
                ScenarioValidationResult failed = new ScenarioValidationResult();
                failed.AddError("Scenario XML could not be loaded: " + ex.Message);
                return failed;
            }
        }

        public bool TryLoadDefinition(string scenarioId, out ScenarioDefinition definition, out string scenarioFilePath, out ScenarioValidationResult validation)
        {
            definition = null;
            scenarioFilePath = null;
            validation = new ScenarioValidationResult();

            ScenarioInfo info;
            if (!TryGetDefinitionInfo(scenarioId, out info) || info == null)
            {
                validation.AddError("Scenario is not indexed: " + scenarioId);
                return false;
            }

            scenarioFilePath = info.FilePath;
            try
            {
                definition = _definitionSerializer.Load(info.FilePath);
                validation = _definitionValidator.Validate(definition, info.FilePath);
                return validation.IsValid;
            }
            catch (Exception ex)
            {
                validation.AddError("Scenario XML could not be loaded: " + ex.Message);
                return false;
            }
        }

        private void SyncDefinitionRegistrations()
        {
            ScenarioInfo[] definitions = GetAllDefinitionInfos();
            Dictionary<string, ScenarioInfo> current = new Dictionary<string, ScenarioInfo>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < definitions.Length; i++)
            {
                if (definitions[i] != null && !string.IsNullOrEmpty(definitions[i].Id))
                    current[definitions[i].Id] = definitions[i];
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

                ScenarioRecord existing;
                if (_store.TryGet(definition.Id, out existing) && existing != null && !existing.IsDefinitionBacked)
                    continue;

                ScenarioRecord record = CreateDefinitionRecord(definition);
                ScenarioRecord previous;
                _store.Upsert(record, out previous);
                _saveDescriptorMirror.Mirror(record.Info);
            }
        }

        private ScenarioRecord CreateDefinitionRecord(ScenarioInfo definition)
        {
            CustomScenarioRegistration registration = new CustomScenarioRegistration
            {
                Id = definition.Id,
                DisplayName = TrimToNull(definition.DisplayName) ?? definition.Id,
                Description = "XML scenario pack from " + (TrimToNull(definition.OwnerModId) ?? "loaded mod") + ".",
                Version = TrimToNull(definition.Version) ?? "1.0",
                OwnerModId = TrimToNull(definition.OwnerModId),
                RequiredMods = ScenarioDependencyManifest.CloneRequiredMods(_dependencyService.LoadDefinitionDependencies(definition.Id)),
                DefinitionFactory = new CustomScenarioDefinitionFactory(
                    delegate(CustomScenarioBuildContext context) { return _definitionService.BuildScenarioDefFromDefinition(definition.Id); })
            };

            ScenarioRecord record = _recordFactory.CreateRecord(registration);
            record.IsDefinitionBacked = true;
            return record;
        }

        private bool TryGetDefinitionInfo(string scenarioId, out ScenarioInfo info)
        {
            info = null;
            if (string.IsNullOrEmpty(scenarioId))
                return false;

            if (_definitionCatalog.TryGet(scenarioId, out info) && info != null)
                return true;

            return _draftRepository.TryGet(scenarioId, out info) && info != null;
        }

        private ScenarioInfo[] GetAllDefinitionInfos()
        {
            List<ScenarioInfo> combined = new List<ScenarioInfo>();
            Dictionary<string, ScenarioInfo> byId = new Dictionary<string, ScenarioInfo>(StringComparer.OrdinalIgnoreCase);
            AddDefinitionInfos(byId, _definitionCatalog.ListAll());
            AddDefinitionInfos(byId, _draftRepository.ListAll());

            foreach (KeyValuePair<string, ScenarioInfo> pair in byId)
                combined.Add(pair.Value);

            combined.Sort(CompareScenarioDefinitionInfo);
            return combined.ToArray();
        }

        private static void AddDefinitionInfos(Dictionary<string, ScenarioInfo> target, ScenarioInfo[] source)
        {
            if (target == null || source == null)
                return;

            for (int i = 0; i < source.Length; i++)
            {
                ScenarioInfo info = source[i];
                if (info == null || string.IsNullOrEmpty(info.Id) || target.ContainsKey(info.Id))
                    continue;

                target[info.Id] = info;
            }
        }

        private static int CompareScenarioDefinitionInfo(ScenarioInfo left, ScenarioInfo right)
        {
            if (object.ReferenceEquals(left, right)) return 0;
            if (left == null) return 1;
            if (right == null) return -1;

            int name = string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
            if (name != 0) return name;

            return string.Compare(left.Id, right.Id, StringComparison.OrdinalIgnoreCase);
        }

        private static string TrimToNull(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            string trimmed = value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }
    }
}
