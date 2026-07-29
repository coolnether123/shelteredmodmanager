using System;
using System.Collections.Generic;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesCapabilityRegistry
    {
        private readonly object _sync = new object();
        private readonly List<ParalivesCapability> _capabilities = new List<ParalivesCapability>();

        public ParalivesCapabilityRegistry(ParalivesApiVersion version)
        {
            if (version == null)
                throw new ArgumentNullException("version");

            Version = version;
        }

        public static ParalivesCapabilityRegistry CreateDefault()
        {
            var registry = new ParalivesCapabilityRegistry(ParalivesApiVersion.Current);
            registry.Register(new ParalivesCapability(
                ParalivesCapability.RuntimeV1,
                "Stable runtime metadata and readiness checks.",
                false));
            registry.Register(new ParalivesCapability(
                ParalivesCapability.CompatibilitySafeV1,
                "Non-throwing compatibility helpers for probing renamed or missing runtime members.",
                false));
            registry.Register(new ParalivesCapability(
                ParalivesCapability.InteractionsContentV1,
                "Interaction content registration for actions, groups, and interaction units.",
                false));
            registry.Register(new ParalivesCapability(
                ParalivesCapability.ActionsCompletionV1,
                "Action completion events raised from the native action pipeline.",
                false));
            registry.Register(new ParalivesCapability(
                ParalivesCapability.CharactersNativeV1,
                "Character helpers that intentionally expose native Paralives character objects.",
                true));
            registry.Register(new ParalivesCapability(
                ParalivesCapability.OccupationsV1,
                "Top-level stable occupation contract and read helpers.",
                false));
            registry.Register(new ParalivesCapability(
                ParalivesCapability.OccupationsRegistryV1,
                "Generic occupation definition registration contract.",
                false));
            registry.Register(new ParalivesCapability(
                ParalivesCapability.OccupationsEnrollmentV1,
                "Generic occupation enrollment, swap, and restore contract.",
                false));
            registry.Register(new ParalivesCapability(
                ParalivesCapability.OccupationsSchedulesV1,
                "Generic occupation schedule registration and read contract.",
                false));
            registry.Register(new ParalivesCapability(
                ParalivesCapability.OccupationsTasksV1,
                "Generic occupation task assignment and completion contract.",
                false));
            registry.Register(new ParalivesCapability(
                ParalivesCapability.OccupationsUnlockablesV1,
                "Generic occupation unlockable read and mutation contract.",
                false));
            registry.Register(new ParalivesCapability(
                ParalivesCapability.OccupationsAttendancePoliciesV1,
                "Generic occupation attendance decision policies.",
                false));
            registry.Register(new ParalivesCapability(
                ParalivesCapability.OccupationsPanelProvidersV1,
                "Generic occupation UI panel provider contract.",
                false));
            registry.Register(new ParalivesCapability(
                ParalivesCapability.OccupationsAttendancePolicyV1,
                "Occupation attendance policy overrides.",
                false));
            registry.Register(new ParalivesCapability(
                ParalivesCapability.UiWindowsV1,
                "Window lookup and show/hide helpers for Paralives UI windows.",
                true));
            return registry;
        }

        public ParalivesApiVersion Version { get; private set; }

        public string ApiVersion
        {
            get { return Version.ApiVersion; }
        }

        public string AdapterVersion
        {
            get { return Version.AdapterVersion; }
        }

        public string GameId
        {
            get { return Version.GameId; }
        }

        public string DisplayName
        {
            get { return Version.DisplayName; }
        }

        public ParalivesCapability[] Capabilities
        {
            get
            {
                lock (_sync)
                    return _capabilities.ToArray();
            }
        }

        public string[] CapabilityStrings
        {
            get
            {
                lock (_sync)
                {
                    string[] capabilities = new string[_capabilities.Count];
                    for (int i = 0; i < _capabilities.Count; i++)
                        capabilities[i] = _capabilities[i].Id;
                    return capabilities;
                }
            }
        }

        public bool HasCapability(string capability)
        {
            if (string.IsNullOrWhiteSpace(capability))
                return false;

            string normalized = capability.Trim();
            lock (_sync)
            {
                for (int i = 0; i < _capabilities.Count; i++)
                {
                    if (string.Equals(_capabilities[i].Id, normalized, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        internal void Register(ParalivesCapability capability)
        {
            if (capability == null)
                throw new ArgumentNullException("capability");

            lock (_sync)
            {
                for (int i = 0; i < _capabilities.Count; i++)
                {
                    if (string.Equals(_capabilities[i].Id, capability.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        _capabilities[i] = capability;
                        return;
                    }
                }

                _capabilities.Add(capability);
            }
        }
    }
}
