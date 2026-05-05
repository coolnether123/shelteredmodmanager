using System;
using System.Reflection;
using ModAPI.Core;
using ModAPI.Scenarios;
using ShelteredAPI.Persistence;
using ShelteredAPI.UI.FieldManual.Textures;
namespace ShelteredAPI.Scenarios.Registration{
    internal sealed class ScenarioRegistrationValidator
    {
        public CustomScenarioRegistration Normalize(CustomScenarioRegistration registration, Assembly callerAssembly, out string error)
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

        private static string TrimToNull(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            string trimmed = value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }
    }
}
