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
        private const int BreakingTwoPointZeroMajor = 2;

        private readonly Dictionary<string, string> _installedVersions;
        public ApiCompatibilityService(Dictionary<string, string> installedVersions)
        {
            _installedVersions = installedVersions != null
                ? new Dictionary<string, string>(installedVersions, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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

            AddDeclaredRequirement(report, ModApiName, declaredModApiVersion);
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

        internal static bool TargetsLegacyLine(ModItem mod)
        {
            if (IsLegacyVersion(mod.DeclaredModApiVersion) || IsLegacyVersion(mod.DeclaredShelteredApiVersion))
                return true;

            foreach (ApiRequirement requirement in mod.ApiCompatibility.Requirements)
                if (IsLegacyVersion(requirement.RequiredVersion))
                    return true;

            return false;
        }

        internal static bool ValidateNexusPackage(string root, ModTypes.ModAboutInfo about, bool migrating, out string error)
        {
            var mod = ModItem.FromAbout(about, root, null);
            var references = AssemblyVersionChecker.ScanModAssemblies(root);
            if (IsLegacyVersion(mod.DeclaredModApiVersion) || IsLegacyVersion(mod.DeclaredShelteredApiVersion) ||
                references.Exists(reference => IsLegacyVersion(reference.ApiVersion)))
            {
                error = "This file was built for an older version of Sheltered Mod Manager. Choose a file built for SMM 2.0.";
                return false;
            }

            if (IsFutureVersion(mod.DeclaredModApiVersion) || IsFutureVersion(mod.DeclaredShelteredApiVersion) ||
                references.Exists(reference => IsFutureVersion(reference.ApiVersion)))
            {
                error = "This file requires a newer version of Sheltered Mod Manager. Choose a file built for SMM 2.0.";
                return false;
            }

            if (migrating && !IsCurrentVersion(mod.DeclaredModApiVersion) && !IsCurrentVersion(mod.DeclaredShelteredApiVersion))
            {
                error = "This file does not declare SMM 2.0 support. Choose a file built for SMM 2.0.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool IsLegacyVersion(string version)
        {
            int major;
            return TryGetMajorVersion(version, out major) && major < BreakingTwoPointZeroMajor;
        }

        private static bool IsCurrentVersion(string version)
        {
            int major;
            return TryGetMajorVersion(version, out major) && major == BreakingTwoPointZeroMajor;
        }

        private static bool IsFutureVersion(string version)
        {
            int major;
            return TryGetMajorVersion(version, out major) && major > BreakingTwoPointZeroMajor;
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
                requirement.Message = "SMM is missing a required compatibility file. Reinstall or update SMM, then try again.";
                return requirement;
            }

            if (!AssemblyVersionChecker.IsCompatible(installed, requirement.RequiredVersion))
            {
                requirement.Severity = ApiCompatibilitySeverity.Error;
                requirement.Message = "This mod requires a newer SMM API version. Update SMM before using this mod.";
                return requirement;
            }

            if (IsOlderBreakingApiLine(requirement.RequiredVersion, installed))
            {
                requirement.Severity = ApiCompatibilitySeverity.Warning;
                requirement.Message = "This mod was built for the older SMM API line. SMM 2.0 moved Sheltered APIs into ShelteredAPI.dll; use a 2.0 version of the mod before loading important saves.";
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

        private static bool IsOlderBreakingApiLine(string requiredVersion, string installedVersion)
        {
            int requiredMajor;
            int installedMajor;
            if (!TryGetMajorVersion(requiredVersion, out requiredMajor) || !TryGetMajorVersion(installedVersion, out installedMajor))
                return false;

            return installedMajor >= BreakingTwoPointZeroMajor && requiredMajor < installedMajor;
        }

        private static bool TryGetMajorVersion(string version, out int major)
        {
            major = 0;
            string text = (version ?? string.Empty).Trim();
            if (text.Length == 0)
                return false;

            int index = 0;
            while (index < text.Length && char.IsDigit(text[index]))
                index++;

            if (index == 0)
                return false;

            return int.TryParse(text.Substring(0, index), out major);
        }
    }
}
