using System;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesCapability
    {
        public const string RuntimeV1 = "paralives.runtime.v1";
        public const string CompatibilitySafeV1 = "paralives.compatibility.safe.v1";
        public const string InteractionsContentV1 = "paralives.interactions.content.v1";
        public const string ActionsCompletionV1 = "paralives.actions.completion.v1";
        public const string CharactersNativeV1 = "paralives.characters.native.v1";
        public const string OccupationsV1 = "paralives.occupations.v1";
        public const string OccupationsRegistryV1 = "paralives.occupations.registry.v1";
        public const string OccupationsEnrollmentV1 = "paralives.occupations.enrollment.v1";
        public const string OccupationsSchedulesV1 = "paralives.occupations.schedules.v1";
        public const string OccupationsTasksV1 = "paralives.occupations.tasks.v1";
        public const string OccupationsUnlockablesV1 = "paralives.occupations.unlockables.v1";
        public const string OccupationsAttendancePoliciesV1 = "paralives.occupations.attendancePolicies.v1";
        public const string OccupationsPanelProvidersV1 = "paralives.occupations.panelProviders.v1";
        public const string OccupationsAttendancePolicyV1 = "paralives.occupations.attendancePolicy.v1";
        public const string UiWindowsV1 = "paralives.ui.windows.v1";

        public ParalivesCapability(string id)
            : this(id, string.Empty, false)
        {
        }

        public ParalivesCapability(string id, string description, bool exposesNativeTypes)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A capability id is required.", "id");

            Id = id.Trim();
            Description = description == null ? string.Empty : description.Trim();
            ExposesNativeTypes = exposesNativeTypes;
        }

        public string Id { get; private set; }

        public string Description { get; private set; }

        public bool ExposesNativeTypes { get; private set; }

        public override string ToString()
        {
            return Id;
        }
    }
}
