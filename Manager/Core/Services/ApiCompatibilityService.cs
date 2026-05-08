using System;
using System.Collections.Generic;
using System.Text;
using Manager.Core.Models;

namespace Manager.Core.Services
{
    /// <summary>
    /// Evaluates mod API references against installed API assemblies.
    /// </summary>
    public class ApiCompatibilityService
    {
        private const string ModApiName = "ModAPI";
        private const string ShelteredApiName = "ShelteredAPI";

        private readonly Dictionary<string, string> _installedVersions;
        private readonly HashSet<string> _knownApiNames;

        public ApiCompatibilityService(Dictionary<string, string> installedVersions)
            : this(installedVersions, new string[] { ModApiName, ShelteredApiName, "ModAPI.Networking" })
        {
        }

        public ApiCompatibilityService(Dictionary<string, string> installedVersions, IEnumerable<string> knownApiNames)
        {
            _installedVersions = installedVersions != null
                ? new Dictionary<string, string>(installedVersions, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _knownApiNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (knownApiNames != null)
            {
                foreach (string apiName in knownApiNames)
                {
                    if (!string.IsNullOrEmpty(apiName))
                        _knownApiNames.Add(apiName);
                }
            }

            if (_knownApiNames.Count == 0)
                _knownApiNames.Add(ModApiName);
        }

        public ApiCompatibilityReport Evaluate(IEnumerable<AssemblyVersionChecker.ModAssemblyVersion> assemblyReferences, string declaredModApiVersion, string declaredShelteredApiVersion)
        {
            var report = new ApiCompatibilityReport();
            var distinct = new Dictionary<string, ApiRequirement>(StringComparer.OrdinalIgnoreCase);

            if (assemblyReferences != null)
            {
                foreach (var reference in assemblyReferences)
                {
                    if (string.IsNullOrEmpty(reference.ApiName) || string.IsNullOrEmpty(reference.ApiVersion))
                        continue;

                    string key = reference.ApiName + "|" + reference.ApiVersion + "|" + reference.DllName;
                    if (distinct.ContainsKey(key))
                        continue;

                    var requirement = EvaluateRequirement(reference.ApiName, reference.ApiVersion, reference.DllName);
                    distinct[key] = requirement;
                    report.Requirements.Add(requirement);
                    if (!string.IsNullOrEmpty(requirement.Message))
                        report.AddMessage(requirement.Severity, requirement.Message);
                }
            }

            if (UsesApi(ModApiName))
                AddDeclaredRequirement(report, ModApiName, declaredModApiVersion);
            if (UsesApi(ShelteredApiName))
                AddDeclaredRequirement(report, ShelteredApiName, declaredShelteredApiVersion);

            PopulateSummaries(report);
            return report;
        }

        private void AddDeclaredRequirement(ApiCompatibilityReport report, string apiName, string declaredVersion)
        {
            if (report == null || string.IsNullOrEmpty(declaredVersion))
                return;

            for (int i = 0; i < report.Requirements.Count; i++)
            {
                ApiRequirement existing = report.Requirements[i];
                if (existing != null &&
                    string.Equals(existing.ApiName, apiName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(existing.RequiredVersion, declaredVersion, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            var requirement = EvaluateRequirement(apiName, declaredVersion, "About.json");
            report.Requirements.Add(requirement);
            if (!string.IsNullOrEmpty(requirement.Message))
                report.AddMessage(requirement.Severity, requirement.Message);
        }

        public string GetInstalledVersion(string apiName)
        {
            if (string.IsNullOrEmpty(apiName))
                return string.Empty;

            string version;
            return _installedVersions.TryGetValue(apiName, out version) ? version : string.Empty;
        }

        private ApiRequirement EvaluateRequirement(string apiName, string requiredVersion, string sourceAssembly)
        {
            var requirement = new ApiRequirement();
            requirement.ApiName = apiName ?? string.Empty;
            requirement.RequiredVersion = NormalizeVersion(requiredVersion);
            requirement.SourceAssembly = sourceAssembly ?? string.Empty;

            string installed = GetInstalledVersion(requirement.ApiName);
            if (string.IsNullOrEmpty(installed))
            {
                requirement.Severity = ApiCompatibilitySeverity.Error;
                requirement.Message = "The manager runtime is missing a required compatibility file. Reinstall or update the manager, then try again.";
                return requirement;
            }

            if (!AssemblyVersionChecker.IsCompatible(installed, requirement.RequiredVersion))
            {
                requirement.Severity = ApiCompatibilitySeverity.Error;
                requirement.Message = "This mod requires a newer manager API version. Update the manager before using this mod.";
                return requirement;
            }

            if (IsOlderThan(requirement.RequiredVersion, installed))
            {
                requirement.Severity = ApiCompatibilitySeverity.Info;
                requirement.Message = string.Empty;
                return requirement;
            }

            requirement.Severity = ApiCompatibilitySeverity.None;
            requirement.Message = string.Empty;
            return requirement;
        }

        private bool UsesApi(string apiName)
        {
            return !string.IsNullOrEmpty(apiName) && _knownApiNames.Contains(apiName);
        }

        private void PopulateSummaries(ApiCompatibilityReport report)
        {
            if (report == null)
                return;

            report.RequirementSummary = BuildRequirementSummary(report.Requirements);

            if (report.Requirements.Count == 0)
            {
                report.Summary = "Compatibility not declared";
                return;
            }

            if (report.Messages.Count == 0)
            {
                report.Summary = "Compatible";
                return;
            }

            report.Summary = report.Messages[0];
        }

        private static string BuildRequirementSummary(IList<ApiRequirement> requirements)
        {
            if (requirements == null || requirements.Count == 0)
                return string.Empty;

            var parts = new List<string>();
            for (int i = 0; i < requirements.Count; i++)
            {
                ApiRequirement requirement = requirements[i];
                if (requirement == null || string.IsNullOrEmpty(requirement.ApiName) || string.IsNullOrEmpty(requirement.RequiredVersion))
                    continue;

                string part = requirement.ApiName + " " + requirement.RequiredVersion;
                if (!ContainsPart(parts, part))
                    parts.Add(part);
            }

            return Join(parts, ", ");
        }

        private static bool ContainsPart(List<string> parts, string value)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                if (string.Equals(parts[i], value, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string Join(List<string> values, string separator)
        {
            if (values == null || values.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0)
                    sb.Append(separator);
                sb.Append(values[i]);
            }

            return sb.ToString();
        }

        private static string NormalizeVersion(string version)
        {
            return (version ?? string.Empty).Trim();
        }

        private static bool IsOlderThan(string left, string right)
        {
            try
            {
                return new Version(left).CompareTo(new Version(right)) < 0;
            }
            catch
            {
                return string.Compare(left, right, StringComparison.OrdinalIgnoreCase) < 0;
            }
        }
    }
}
