using System;
using System.Collections.Generic;
using System.Reflection;
using ModAPI.Core;
using ModAPI.Hooks.Paging;
using ModAPI.Saves;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    /// <summary>
    /// Sheltered runtime implementation of the neutral custom scenario service contract.
    /// </summary>
    public sealed class ShelteredCustomScenarioService : ICustomScenarioService, IShelteredCustomScenarioService
    {
        private sealed class ScenarioRecord
        {
            public CustomScenarioRegistration Registration;
            public CustomScenarioInfo Info;
            public bool IsDefinitionBacked;
        }

        private readonly Dictionary<string, ScenarioRecord> _registrations = new Dictionary<string, ScenarioRecord>(StringComparer.OrdinalIgnoreCase);
        private readonly object _sync = new object();
        private readonly IScenarioDefinitionSerializer _definitionSerializer;
        private readonly IScenarioDefinitionCatalog _definitionCatalog;
        private readonly IScenarioDefinitionValidator _definitionValidator;
        private readonly ScenarioAuthoringDraftRepository _draftRepository;
        private readonly IScenarioStateManager _stateManager;
        private readonly IScenarioRuntimeBindingService _runtimeBindingService;

        public static ShelteredCustomScenarioService Instance
        {
            get { return ScenarioCompositionRoot.Resolve<ShelteredCustomScenarioService>(); }
        }

        public event Action<CustomScenarioEventArgs> ScenarioRegistered;
        public event Action<CustomScenarioEventArgs> ScenarioUnregistered;
        public event Action<CustomScenarioEventArgs> ScenarioSelected;
        public event Action<CustomScenarioEventArgs> ScenarioSpawned;
        public event Action<CustomScenarioEventArgs> StateChanged;

        public CustomScenarioState CurrentState
        {
            get
            {
                return _stateManager.GetCustomScenarioState();
            }
        }

        internal ShelteredCustomScenarioService(
            IScenarioDefinitionSerializer definitionSerializer,
            IScenarioDefinitionCatalog definitionCatalog,
            IScenarioDefinitionValidator definitionValidator,
            ScenarioAuthoringDraftRepository draftRepository,
            IScenarioStateManager stateManager,
            IScenarioRuntimeBindingService runtimeBindingService)
        {
            _definitionSerializer = definitionSerializer;
            _definitionCatalog = definitionCatalog;
            _definitionValidator = definitionValidator;
            _draftRepository = draftRepository;
            _stateManager = stateManager;
            _runtimeBindingService = runtimeBindingService;
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

        public CustomScenarioRegistrationResult Register(CustomScenarioRegistration registration)
        {
            string error;
            Assembly callerAssembly = null;
            try { callerAssembly = Assembly.GetCallingAssembly(); } catch { }

            CustomScenarioRegistration normalized = NormalizeRegistration(registration, callerAssembly, out error);
            if (normalized == null)
                return CustomScenarioRegistrationResult.Failed(error);

            ScenarioRecord record = CreateRecord(normalized);
            bool replacedExisting;
            lock (_sync)
            {
                replacedExisting = _registrations.ContainsKey(record.Info.Id);
                _registrations[record.Info.Id] = record;
            }

            MirrorSaveScenarioDescriptor(record.Info);
            Raise(ScenarioRegistered, CustomScenarioEventType.Registered, record.Info);
            return CustomScenarioRegistrationResult.Ok(record.Info.Id, replacedExisting);
        }

        public bool Unregister(string scenarioId)
        {
            if (string.IsNullOrEmpty(scenarioId))
                return false;

            ScenarioRecord removed = null;
            bool clearedState = false;
            lock (_sync)
            {
                if (!_registrations.TryGetValue(scenarioId, out removed))
                    return false;

                _registrations.Remove(scenarioId);
                CustomScenarioState currentState = _stateManager.GetCustomScenarioState();
                if (currentState != null && string.Equals(currentState.ScenarioId, scenarioId, StringComparison.OrdinalIgnoreCase))
                    clearedState = true;
            }

            if (clearedState)
                _stateManager.SetCustomScenarioState(CustomScenarioState.None(), "custom-scenario", "Scenario unregistered.");

            Raise(ScenarioUnregistered, CustomScenarioEventType.Unregistered, removed.Info);
            if (clearedState)
                Raise(StateChanged, CustomScenarioEventType.Cleared, removed.Info);
            return true;
        }

        public bool TryGet(string scenarioId, out CustomScenarioInfo scenario)
        {
            scenario = null;
            if (string.IsNullOrEmpty(scenarioId))
                return false;

            lock (_sync)
            {
                ScenarioRecord record;
                if (!_registrations.TryGetValue(scenarioId, out record))
                    return false;

                scenario = record.Info;
                return true;
            }
        }

        public CustomScenarioInfo[] List()
        {
            List<CustomScenarioInfo> items = new List<CustomScenarioInfo>();
            lock (_sync)
            {
                foreach (KeyValuePair<string, ScenarioRecord> pair in _registrations)
                    items.Add(pair.Value.Info);
            }

            items.Sort(CompareScenarioInfo);
            return items.ToArray();
        }

        public bool TryCreateDefinition(string scenarioId, CustomScenarioBuildContext context, out object definition, out string errorMessage)
        {
            ScenarioDef scenarioDef;
            bool result = TryCreateScenarioDef(scenarioId, context, out scenarioDef, out errorMessage);
            definition = scenarioDef;
            return result;
        }

        public bool TryCreateScenarioDef(string scenarioId, CustomScenarioBuildContext context, out ScenarioDef definition, out string errorMessage)
        {
            definition = null;
            errorMessage = null;

            ScenarioRecord record;
            lock (_sync)
            {
                if (!_registrations.TryGetValue(scenarioId, out record))
                {
                    errorMessage = "Custom scenario is not registered: " + scenarioId;
                    return false;
                }
            }

            CustomScenarioRegistration registration = record.Registration;
            if (registration.Definition != null)
            {
                definition = registration.Definition as ScenarioDef;
                if (definition == null)
                {
                    errorMessage = "Registered definition for '" + record.Info.Id + "' is not a Sheltered ScenarioDef.";
                    return false;
                }

                return true;
            }

            if (registration.DefinitionFactory == null)
            {
                errorMessage = "Custom scenario has no ScenarioDef or definition factory: " + record.Info.Id;
                return false;
            }

            try
            {
                CustomScenarioBuildContext buildContext = PrepareBuildContext(record, context);
                object built = registration.DefinitionFactory(buildContext);
                definition = built as ScenarioDef;
                if (definition == null)
                {
                    errorMessage = "Definition factory for '" + record.Info.Id + "' did not return a Sheltered ScenarioDef.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Definition factory for '" + record.Info.Id + "' failed: " + ex.Message;
                MMLog.WriteError("[ShelteredCustomScenarioService] " + errorMessage);
                return false;
            }
        }

        public bool MarkSelected(string scenarioId)
        {
            ScenarioRecord record;
            lock (_sync)
            {
                if (!_registrations.TryGetValue(scenarioId, out record))
                    return false;
            }

            if (VerifyDependencies(record.Info) != ScenarioDependencyVerificationState.Match)
            {
                MMLog.WriteWarning("[ShelteredCustomScenarioService] Custom scenario dependencies are not satisfied: " + scenarioId);
                return false;
            }

            _stateManager.SetCustomScenarioState(new CustomScenarioState
            {
                ScenarioId = record.Info.Id,
                LifecycleState = CustomScenarioLifecycleState.Pending
            }, "custom-scenario", "Scenario selected.");

            CustomScenarioEventArgs args = CreateArgs(CustomScenarioEventType.Selected, record.Info);
            InvokeRegistrationCallback(record.Registration.OnSelected, args, record.Info.Id, "OnSelected");
            Raise(ScenarioSelected, args);
            Raise(StateChanged, args);
            return true;
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

            lock (_sync)
            {
                List<string> stale = new List<string>();
                foreach (KeyValuePair<string, ScenarioRecord> pair in _registrations)
                {
                    if (pair.Value != null && pair.Value.IsDefinitionBacked && !current.ContainsKey(pair.Key))
                        stale.Add(pair.Key);
                }

                for (int i = 0; i < stale.Count; i++)
                    _registrations.Remove(stale[i]);

                for (int i = 0; i < definitions.Length; i++)
                {
                    ScenarioInfo definition = definitions[i];
                    if (definition == null || string.IsNullOrEmpty(definition.Id))
                        continue;

                    ScenarioRecord existing;
                    if (_registrations.TryGetValue(definition.Id, out existing) && existing != null && !existing.IsDefinitionBacked)
                        continue;

                    ScenarioRecord record = CreateDefinitionRecord(definition);
                    _registrations[definition.Id] = record;
                    MirrorSaveScenarioDescriptor(record.Info);
                }
            }
        }

        private ScenarioRecord CreateDefinitionRecord(ScenarioInfo definition)
        {
            LoadedModInfo[] requiredMods = LoadDefinitionDependencies(definition.Id);
            CustomScenarioRegistration registration = new CustomScenarioRegistration
            {
                Id = definition.Id,
                DisplayName = TrimToNull(definition.DisplayName) ?? definition.Id,
                Description = "XML scenario pack from " + (TrimToNull(definition.OwnerModId) ?? "loaded mod") + ".",
                Version = TrimToNull(definition.Version) ?? "1.0",
                OwnerModId = TrimToNull(definition.OwnerModId),
                RequiredMods = ScenarioDependencyManifest.CloneRequiredMods(requiredMods),
                DefinitionFactory = new CustomScenarioDefinitionFactory(
                    delegate(CustomScenarioBuildContext context) { return BuildScenarioDefFromDefinition(definition.Id); })
            };

            ScenarioRecord record = CreateRecord(registration);
            record.IsDefinitionBacked = true;
            return record;
        }

        private ScenarioDef BuildScenarioDefFromDefinition(string scenarioId)
        {
            ScenarioDefinition definition;
            string scenarioFilePath;
            ScenarioValidationResult validation;
            if (!TryLoadDefinition(scenarioId, out definition, out scenarioFilePath, out validation))
                throw new InvalidOperationException("Scenario XML failed validation: " + FormatValidationIssues(validation));

            ShelteredScenarioDefBuilder builder = new ShelteredScenarioDefBuilder()
                .SetId(definition.Id)
                .SetNameKey(!string.IsNullOrEmpty(definition.DisplayName) ? definition.DisplayName : definition.Id)
                .SetDescriptionKey(definition.Description ?? string.Empty)
                .UseInModes(
                    definition.BaseGameMode == ScenarioBaseGameMode.Survival,
                    definition.BaseGameMode == ScenarioBaseGameMode.Surrounded,
                    definition.BaseGameMode == ScenarioBaseGameMode.Stasis)
                .OnceOnly(false);

            string stageId = definition.Id + ".main";
            if (definition.TriggersAndEvents != null && definition.TriggersAndEvents.Triggers.Count > 0
                && !string.IsNullOrEmpty(definition.TriggersAndEvents.Triggers[0].Id))
            {
                stageId = definition.TriggersAndEvents.Triggers[0].Id;
            }

            builder.AddSimpleStage(stageId);
            return builder.Build();
        }

        private static string FormatValidationIssues(ScenarioValidationResult validation)
        {
            if (validation == null || validation.Issues.Length == 0)
                return "no details were provided.";

            List<string> parts = new List<string>();
            ScenarioValidationIssue[] issues = validation.Issues;
            for (int i = 0; i < issues.Length; i++)
            {
                if (issues[i] != null)
                    parts.Add(issues[i].Severity + ": " + issues[i].Message);
            }

            return string.Join("; ", parts.ToArray());
        }

        public bool MarkSpawned(string scenarioId)
        {
            ScenarioRecord record;
            lock (_sync)
            {
                if (!_registrations.TryGetValue(scenarioId, out record))
                    return false;
            }

            _stateManager.SetCustomScenarioState(new CustomScenarioState
            {
                ScenarioId = record.Info.Id,
                LifecycleState = CustomScenarioLifecycleState.Active
            }, "custom-scenario", "Scenario spawned.");

            _runtimeBindingService.SetBinding(CreateRuntimeBinding(record.Info));
            CustomScenarioEventArgs args = CreateArgs(CustomScenarioEventType.Spawned, record.Info);
            InvokeRegistrationCallback(record.Registration.OnSpawned, args, record.Info.Id, "OnSpawned");
            Raise(ScenarioSpawned, args);
            Raise(StateChanged, args);
            return true;
        }

        public void ClearState()
        {
            CustomScenarioInfo previousInfo = null;
            bool hadState = false;
            lock (_sync)
            {
                CustomScenarioState currentState = _stateManager.GetCustomScenarioState();
                if (currentState != null && !string.IsNullOrEmpty(currentState.ScenarioId))
                {
                    hadState = true;
                    ScenarioRecord record;
                    if (_registrations.TryGetValue(currentState.ScenarioId, out record))
                        previousInfo = record.Info;
                }
            }

            _stateManager.SetCustomScenarioState(CustomScenarioState.None(), "custom-scenario", "State cleared.");
            if (hadState)
                Raise(StateChanged, CustomScenarioEventType.Cleared, previousInfo);
        }

        private static CustomScenarioRegistration NormalizeRegistration(
            CustomScenarioRegistration registration,
            Assembly callerAssembly,
            out string error)
        {
            error = null;
            if (registration == null)
            {
                error = "Custom scenario registration cannot be null.";
                return null;
            }

            string id = TrimToNull(registration.Id);
            if (id == null)
            {
                error = "Custom scenario id is required.";
                return null;
            }

            string displayName = TrimToNull(registration.DisplayName);
            if (displayName == null)
            {
                error = "Custom scenario display name is required for '" + id + "'.";
                return null;
            }

            if (registration.Definition == null && registration.DefinitionFactory == null)
            {
                error = "Custom scenario '" + id + "' requires a Sheltered ScenarioDef or a definition factory.";
                return null;
            }

            if (registration.Definition != null && !(registration.Definition is ScenarioDef))
            {
                error = "Custom scenario '" + id + "' definition must be a Sheltered ScenarioDef.";
                return null;
            }

            Assembly ownerAssembly = registration.OwnerAssembly ?? callerAssembly;
            string ownerModId = TrimToNull(registration.OwnerModId) ?? ResolveOwnerModId(ownerAssembly);

            return new CustomScenarioRegistration
            {
                Id = id,
                DisplayName = displayName,
                Description = registration.Description ?? string.Empty,
                Version = TrimToNull(registration.Version) ?? "1.0",
                Order = registration.Order,
                OwnerModId = ownerModId,
                OwnerAssembly = ownerAssembly,
                RequiredMods = ScenarioDependencyManifest.CloneRequiredMods(registration.RequiredMods),
                Definition = registration.Definition,
                DefinitionFactory = registration.DefinitionFactory,
                OnSelected = registration.OnSelected,
                OnSpawned = registration.OnSpawned,
                UserData = registration.UserData
            };
        }

        private static ScenarioRecord CreateRecord(CustomScenarioRegistration registration)
        {
            CustomScenarioInfo info = new CustomScenarioInfo(
                registration.Id,
                registration.DisplayName,
                registration.Description,
                registration.Version,
                registration.Order,
                registration.OwnerModId,
                ScenarioDependencyManifest.CloneRequiredMods(registration.RequiredMods),
                registration.Definition != null,
                registration.DefinitionFactory != null);

            return new ScenarioRecord
            {
                Registration = registration,
                Info = info,
                IsDefinitionBacked = false
            };
        }

        private static ScenarioRuntimeBinding CreateRuntimeBinding(CustomScenarioInfo info)
        {
            return new ScenarioRuntimeBinding
            {
                ScenarioId = info != null ? info.Id : null,
                VersionApplied = info != null ? info.Version : null,
                IsActive = true,
                IsConvertedToNormalSave = false,
                DayCreated = GetCurrentDay()
            };
        }

        private static int GetCurrentDay()
        {
            try { return GameTime.Day; }
            catch { return 0; }
        }

        private CustomScenarioBuildContext PrepareBuildContext(ScenarioRecord record, CustomScenarioBuildContext context)
        {
            CustomScenarioBuildContext result = context ?? new CustomScenarioBuildContext();
            if (string.IsNullOrEmpty(result.ScenarioId))
                result.ScenarioId = record.Info.Id;
            if (string.IsNullOrEmpty(result.OwnerModId))
                result.OwnerModId = record.Info.OwnerModId;
            if (result.State == null)
                result.State = CurrentState;
            if (result.UserData == null)
                result.UserData = record.Registration.UserData;
            return result;
        }

        public SlotManifest CreateDependencyManifest(CustomScenarioInfo info)
        {
            if (info == null)
                return ScenarioDependencyManifest.Create("Custom Scenario", new LoadedModInfo[0]);

            LoadedModInfo[] required = ScenarioDependencyManifest.Merge(
                info.RequiredMods,
                LoadDefinitionDependencies(info.Id));

            return ScenarioDependencyManifest.Create(info.DisplayName, required);
        }

        public ScenarioDependencyVerificationState VerifyDependencies(CustomScenarioInfo info)
        {
            return MapVerificationState(SaveVerification.VerifyRequired(CreateDependencyManifest(info)));
        }

        private LoadedModInfo[] LoadDefinitionDependencies(string scenarioId)
        {
            if (string.IsNullOrEmpty(scenarioId))
                return new LoadedModInfo[0];

            try
            {
                ScenarioInfo info;
                if (!TryGetDefinitionInfo(scenarioId, out info) || info == null)
                    return new LoadedModInfo[0];

                ScenarioDefinition definition = _definitionSerializer.Load(info.FilePath);
                return ScenarioDependencyManifest.FromDependencyStrings(definition.Dependencies);
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ShelteredCustomScenarioService] Failed to load dependency manifest for '" + scenarioId + "': " + ex.Message);
                return new LoadedModInfo[0];
            }
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
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return 1;
            if (right == null) return -1;

            int name = string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
            if (name != 0) return name;

            return string.Compare(left.Id, right.Id, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareScenarioInfo(CustomScenarioInfo left, CustomScenarioInfo right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return 1;
            if (right == null) return -1;

            int order = left.Order.CompareTo(right.Order);
            if (order != 0) return order;

            int name = string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
            if (name != 0) return name;

            return string.Compare(left.Id, right.Id, StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveOwnerModId(Assembly ownerAssembly)
        {
            if (ownerAssembly == null)
                return null;

            try
            {
                ModEntry entry;
                if (ModRegistry.TryGetModByAssembly(ownerAssembly, out entry) && entry != null && !string.IsNullOrEmpty(entry.Id))
                    return entry.Id;
            }
            catch
            {
            }

            try { return ownerAssembly.GetName().Name; }
            catch { return null; }
        }

        private static void MirrorSaveScenarioDescriptor(CustomScenarioInfo info)
        {
            if (info == null || string.IsNullOrEmpty(info.Id))
                return;

            try
            {
                ScenarioRegistry.RegisterScenario(new ScenarioDescriptor
                {
                    id = info.Id,
                    displayName = info.DisplayName,
                    description = info.Description,
                    version = info.Version
                });
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredCustomScenarioService.MirrorSaveScenarioDescriptor." + info.Id, ex.Message);
            }
        }

        private CustomScenarioEventArgs CreateArgs(CustomScenarioEventType eventType, CustomScenarioInfo info)
        {
            return new CustomScenarioEventArgs(eventType, info, CurrentState);
        }

        private void Raise(Action<CustomScenarioEventArgs> handler, CustomScenarioEventType eventType, CustomScenarioInfo info)
        {
            Raise(handler, CreateArgs(eventType, info));
        }

        private static void Raise(Action<CustomScenarioEventArgs> handler, CustomScenarioEventArgs args)
        {
            if (handler == null)
                return;

            try
            {
                handler(args);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredCustomScenarioService.Event." + args.EventType, ex.Message);
            }
        }

        private static void InvokeRegistrationCallback(
            Action<CustomScenarioEventArgs> callback,
            CustomScenarioEventArgs args,
            string scenarioId,
            string callbackName)
        {
            if (callback == null)
                return;

            try
            {
                callback(args);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredCustomScenarioService." + callbackName + "." + scenarioId, ex.Message);
            }
        }

        private static string TrimToNull(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            string trimmed = value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }

        private static ScenarioDependencyVerificationState MapVerificationState(SaveVerification.VerificationState state)
        {
            switch (state)
            {
                case SaveVerification.VerificationState.Match:
                    return ScenarioDependencyVerificationState.Match;
                case SaveVerification.VerificationState.VersionMismatch:
                    return ScenarioDependencyVerificationState.VersionMismatch;
                case SaveVerification.VerificationState.Warning:
                    return ScenarioDependencyVerificationState.Warning;
                case SaveVerification.VerificationState.Missing:
                    return ScenarioDependencyVerificationState.Missing;
                default:
                    return ScenarioDependencyVerificationState.Unknown;
            }
        }
    }
}
