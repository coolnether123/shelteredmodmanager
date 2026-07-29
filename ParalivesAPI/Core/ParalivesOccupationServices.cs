using System;
using ParalivesAPI.Stable;
using Setting;

namespace ParalivesAPI.Core
{
    internal sealed class ParalivesOccupationRegistryContract : IParalivesOccupationRegistry
    {
        private readonly ParalivesOccupationRegistry _registry;

        public ParalivesOccupationRegistryContract(ParalivesOccupationRegistry registry)
        {
            _registry = registry;
        }

        public int RegisteredOccupationCount
        {
            get { return _registry == null ? 0 : _registry.RegisteredOccupationCount; }
        }

        public ParalivesOccupationRegistrationResult RegisterOccupation(
            ParalivesOccupationDefinition definition)
        {
            return _registry.RegisterOccupation(definition);
        }

        public ParalivesOccupationRegistrationResult ApplyWhenReady()
        {
            return _registry.ApplyWhenReady();
        }
    }

    internal sealed class ParalivesOccupationTaskContract : IParalivesOccupationTasks
    {
        private readonly ParalivesOccupationFacade _occupations;

        public ParalivesOccupationTaskContract(ParalivesOccupationFacade occupations)
        {
            _occupations = occupations;
        }

        public ParalivesOccupationTaskEntry[] ReadActiveTasks(ulong characterGuid)
        {
            return _occupations.Tasks.ReadActiveTasks(characterGuid);
        }

        public ParalivesOccupationTaskEntry[] ReadActiveTasks(
            ulong characterGuid,
            ulong occupationGuid)
        {
            return _occupations.Tasks.ReadActiveTasks(characterGuid, occupationGuid);
        }

        public ParalivesOccupationTaskAssignmentResult AssignTask(
            ulong characterGuid,
            ulong occupationGuid,
            ulong taskGuid)
        {
            return _occupations.Tasks.AssignTask(characterGuid, occupationGuid, taskGuid);
        }

        public ParalivesOccupationTaskAssignmentResult AssignTask(
            ulong characterGuid,
            ulong occupationGuid,
            ulong taskGuid,
            ulong skillGuid)
        {
            return _occupations.Tasks.AssignTask(characterGuid, occupationGuid, taskGuid, skillGuid);
        }

        public ParalivesOccupationTaskAssignmentResult AssignTask(
            ulong characterGuid,
            ParalivesOccupationTaskDefinition definition)
        {
            return _occupations.Tasks.AssignTask(characterGuid, definition);
        }

        public ParalivesOccupationTaskCompletionResult CompleteTask(
            ulong characterGuid,
            ulong occupationGuid,
            ulong taskGuid)
        {
            return _occupations.Tasks.CompleteTask(characterGuid, occupationGuid, taskGuid);
        }

        public ParalivesOccupationTaskCompletionResult CompleteMatchingTask(
            ulong characterGuid,
            ulong occupationGuid)
        {
            return _occupations.Tasks.CompleteMatchingTask(characterGuid, occupationGuid);
        }

        public ParalivesOccupationTaskCompletionResult CompleteMatchingTask(
            ulong characterGuid,
            ulong occupationGuid,
            ulong skillGuid)
        {
            return _occupations.Tasks.CompleteMatchingTask(characterGuid, occupationGuid, skillGuid);
        }

        public ParalivesOccupationTaskCompletionResult CompleteMatchingTask(
            ulong characterGuid,
            ulong occupationGuid,
            ulong skillGuid,
            ulong characterTargetGuid)
        {
            return _occupations.Tasks.CompleteMatchingTask(
                characterGuid,
                occupationGuid,
                skillGuid,
                characterTargetGuid);
        }

        public ParalivesOccupationTaskCompletionResult CompleteMatchingTask(
            ulong characterGuid,
            ulong occupationGuid,
            Predicate<ParalivesOccupationTaskEntry> predicate)
        {
            return _occupations.Tasks.CompleteMatchingTask(
                characterGuid,
                occupationGuid,
                predicate);
        }
    }

    internal sealed class ParalivesOccupationPanelProviderService :
        IParalivesOccupationPanelProviders
    {
        private ParalivesUiFacade _ui;

        public int RegisteredProviderCount
        {
            get
            {
                return _ui == null || _ui.Extensions == null
                    ? 0
                    : _ui.Extensions.RegisteredOccupationPanelProviderCount;
            }
        }

        public void Attach(ParalivesUiFacade ui)
        {
            _ui = ui;
        }

        public IDisposable Register(IParalivesOccupationPanelProvider provider)
        {
            return RequireUi().RegisterOccupationPanelProvider(provider);
        }

        public IDisposable Register(
            Func<ulong, int, bool> canProvide,
            Func<ulong, int, ParalivesOccupationPanel> buildPanel)
        {
            return RequireUi().Extensions.RegisterOccupationPanelProvider(canProvide, buildPanel);
        }

        public bool Unregister(IParalivesOccupationPanelProvider provider)
        {
            return RequireUi().UnregisterOccupationPanelProvider(provider);
        }

        private ParalivesUiFacade RequireUi()
        {
            if (_ui == null)
                throw new InvalidOperationException("Occupation panel providers are not wired yet.");

            return _ui;
        }
    }

    internal static class ParalivesOccupationContractMapper
    {
        public static ParalivesOccupationKind ToOccupationKind(SchoolJobTypes type)
        {
            return type == SchoolJobTypes.School
                ? ParalivesOccupationKind.School
                : ParalivesOccupationKind.Job;
        }
    }
}
