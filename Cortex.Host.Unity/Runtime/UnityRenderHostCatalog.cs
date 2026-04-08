using System;
using System.Collections.Generic;
using System.IO;
using Cortex.Core.Models;
using Cortex.Presentation.Abstractions;

namespace Cortex.Host.Unity.Runtime
{
    public sealed class UnityRenderHostCatalog
    {
        public UnityRenderHostCatalog()
        {
            SelectedRenderHostId = UnityRenderHostSettings.ImguiRenderHostId;
            EffectiveRenderHostId = UnityRenderHostSettings.ImguiRenderHostId;
            SettingsHelpText = string.Empty;
            StatusSummary = string.Empty;
            StartupStatusMessage = string.Empty;
            UnavailableReasons = new List<string>();
            AvailableOptions = new List<SettingChoiceOption>();
            AvaloniaLaunchRequest = new UnityExternalHostLaunchRequest();
        }

        public string SelectedRenderHostId { get; set; }

        public string EffectiveRenderHostId { get; set; }

        public string SettingsHelpText { get; set; }

        public string StatusSummary { get; set; }

        public string StartupStatusMessage { get; set; }

        public IList<string> UnavailableReasons { get; private set; }

        public IList<SettingChoiceOption> AvailableOptions { get; private set; }

        public UnityExternalHostLaunchRequest AvaloniaLaunchRequest { get; set; }

        public bool ShouldLaunchAvaloniaExternalHost
        {
            get
            {
                return string.Equals(
                    EffectiveRenderHostId,
                    UnityRenderHostSettings.AvaloniaExternalRenderHostId,
                    StringComparison.OrdinalIgnoreCase) &&
                    AvaloniaLaunchRequest != null &&
                    AvaloniaLaunchRequest.CanLaunch;
            }
        }

        public SettingChoiceOption[] BuildOptions()
        {
            var options = new SettingChoiceOption[AvailableOptions.Count];
            AvailableOptions.CopyTo(options, 0);
            return options;
        }

        public static UnityRenderHostCatalog CreateDefault()
        {
            var catalog = new UnityRenderHostCatalog();
            catalog.AvailableOptions.Add(new SettingChoiceOption
            {
                Value = UnityRenderHostSettings.ImguiRenderHostId,
                DisplayName = "IMGUI",
                Description = "Run Cortex directly inside the active game host with the current IMGUI shell."
            });
            catalog.SettingsHelpText = "Select how Cortex presents its workbench for the current host. Saving applies the new presentation live without restarting the game.";
            catalog.StatusSummary = "Presentation: IMGUI (in-game)";
            return catalog;
        }
    }

    public sealed class UnityExternalHostLaunchRequest
    {
        public UnityExternalHostLaunchRequest()
        {
            CommandPath = string.Empty;
            Arguments = string.Empty;
            WorkingDirectory = string.Empty;
            SuccessStatusMessage = string.Empty;
            FailureReason = string.Empty;
            LaunchToken = string.Empty;
        }

        public string CommandPath { get; set; }

        public string Arguments { get; set; }

        public string WorkingDirectory { get; set; }

        public string SuccessStatusMessage { get; set; }

        public string FailureReason { get; set; }

        public string LaunchToken { get; set; }

        public bool CanLaunch
        {
            get { return !string.IsNullOrEmpty(CommandPath); }
        }
    }

    public sealed class UnityRenderHostCatalogBuilder
    {
        private const string DesktopBridgePipeNameEnvironmentVariable = "CORTEX_DESKTOP_BRIDGE_PIPE_NAME";
        private const string DesktopBridgeDefaultPipeName = "cortex.desktop.bridge";
        private const string DesktopBundleRootEnvironmentVariable = "CORTEX_DESKTOP_BUNDLE_ROOT";
        private readonly Func<string, bool> _fileExists;
        private readonly Func<string, string> _readEnvironmentVariable;

        public UnityRenderHostCatalogBuilder()
            : this(File.Exists, Environment.GetEnvironmentVariable)
        {
        }

        public UnityRenderHostCatalogBuilder(
            Func<string, bool> fileExists,
            Func<string, string> readEnvironmentVariable)
        {
            _fileExists = fileExists ?? File.Exists;
            _readEnvironmentVariable = readEnvironmentVariable ?? Environment.GetEnvironmentVariable;
        }

        public UnityRenderHostCatalog Build(ICortexHostEnvironment environment, string selectedRenderHostId)
        {
            var catalog = UnityRenderHostCatalog.CreateDefault();
            catalog.SelectedRenderHostId = UnityRenderHostSettings.NormalizeRenderHostId(selectedRenderHostId);
            catalog.AvaloniaLaunchRequest = CreateAvaloniaLaunchRequest(environment);

            if (catalog.AvaloniaLaunchRequest.CanLaunch)
            {
                catalog.AvailableOptions.Add(new SettingChoiceOption
                {
                    Value = UnityRenderHostSettings.AvaloniaExternalRenderHostId,
                    DisplayName = "Avalonia",
                    Description = "Launch the external Avalonia desktop host live over the runtime bridge. IMGUI remains the in-game bootstrap and fallback surface."
                });
            }
            else if (!string.IsNullOrEmpty(catalog.AvaloniaLaunchRequest.FailureReason))
            {
                catalog.UnavailableReasons.Add("Avalonia (external): " + catalog.AvaloniaLaunchRequest.FailureReason);
            }

            catalog.EffectiveRenderHostId =
                string.Equals(catalog.SelectedRenderHostId, UnityRenderHostSettings.AvaloniaExternalRenderHostId, StringComparison.OrdinalIgnoreCase) &&
                catalog.AvaloniaLaunchRequest.CanLaunch
                    ? UnityRenderHostSettings.AvaloniaExternalRenderHostId
                    : UnityRenderHostSettings.ImguiRenderHostId;

            catalog.SettingsHelpText = BuildSettingsHelpText(catalog);
            catalog.StatusSummary = BuildStatusSummary(catalog);
            catalog.StartupStatusMessage = BuildStartupStatusMessage(catalog);
            return catalog;
        }

        public UnityExternalHostLaunchRequest CreateAvaloniaLaunchRequest(ICortexHostEnvironment environment)
        {
            var request = new UnityExternalHostLaunchRequest();
            var runtimeCandidates = BuildRuntimeCandidates(environment);
            var dotNetPath = ResolveDotNetPath();
            string dllOnlyRoot = null;

            for (var i = 0; i < runtimeCandidates.Count; i++)
            {
                var candidate = runtimeCandidates[i];
                var exePath = Path.Combine(candidate.HostRuntimeRootPath, "Cortex.Host.Avalonia.exe");
                if (_fileExists(exePath))
                {
                    request.CommandPath = exePath;
                    request.Arguments = BuildLaunchArguments(candidate.BundleRootPath);
                    request.WorkingDirectory = Path.GetDirectoryName(exePath) ?? string.Empty;
                    request.SuccessStatusMessage = "Launched external Avalonia host.";
                    return request;
                }

                var dllPath = Path.Combine(candidate.HostRuntimeRootPath, "Cortex.Host.Avalonia.dll");
                if (_fileExists(dllPath))
                {
                    if (!string.IsNullOrEmpty(dotNetPath))
                    {
                        request.CommandPath = dotNetPath;
                        request.Arguments = QuoteArgument(dllPath) + BuildLaunchArguments(candidate.BundleRootPath);
                        request.WorkingDirectory = Path.GetDirectoryName(dllPath) ?? string.Empty;
                        request.SuccessStatusMessage = "Launched external Avalonia host via dotnet.";
                        return request;
                    }

                    dllOnlyRoot = candidate.HostRuntimeRootPath;
                }
            }

            if (!string.IsNullOrEmpty(dllOnlyRoot))
            {
                request.FailureReason =
                    "Cortex.Host.Avalonia.dll was found under " + dllOnlyRoot +
                    ", but dotnet.exe was not found on PATH for the DLL fallback launch path.";
                return request;
            }

            var searchedRoots = new List<string>();
            for (var i = 0; i < runtimeCandidates.Count; i++)
            {
                searchedRoots.Add(runtimeCandidates[i].HostRuntimeRootPath);
            }

            request.FailureReason =
                "Cortex.Host.Avalonia.exe or Cortex.Host.Avalonia.dll was not found under " +
                string.Join(" or ", searchedRoots.ToArray()) + ".";
            return request;
        }

        private string BuildSettingsHelpText(UnityRenderHostCatalog catalog)
        {
            var helpText =
                "Select how Cortex presents its workbench for the current host. " +
                "IMGUI runs inside the game with the current Unity shell. " +
                "Avalonia launches the external desktop host live over the runtime bridge while the game remains interactive.";
            if (catalog != null && catalog.UnavailableReasons.Count > 0)
            {
                helpText += " Unavailable right now: " + string.Join(" ", CopyUnavailableReasons(catalog.UnavailableReasons));
            }

            return helpText;
        }

        private static string BuildStatusSummary(UnityRenderHostCatalog catalog)
        {
            if (catalog == null)
            {
                return "Presentation: IMGUI (in-game)";
            }

            if (string.Equals(catalog.EffectiveRenderHostId, UnityRenderHostSettings.AvaloniaExternalRenderHostId, StringComparison.OrdinalIgnoreCase))
            {
                return "Presentation: Avalonia (external overlay)";
            }

            if (string.Equals(catalog.SelectedRenderHostId, UnityRenderHostSettings.AvaloniaExternalRenderHostId, StringComparison.OrdinalIgnoreCase) &&
                catalog.UnavailableReasons.Count > 0)
            {
                return "Presentation: IMGUI (Avalonia unavailable)";
            }

            return "Presentation: IMGUI (in-game)";
        }

        private static string BuildStartupStatusMessage(UnityRenderHostCatalog catalog)
        {
            if (catalog == null)
            {
                return string.Empty;
            }

            if (string.Equals(catalog.SelectedRenderHostId, UnityRenderHostSettings.AvaloniaExternalRenderHostId, StringComparison.OrdinalIgnoreCase))
            {
                return catalog.AvaloniaLaunchRequest != null && catalog.AvaloniaLaunchRequest.CanLaunch
                    ? "Launching external Avalonia host."
                    : "Avalonia external host unavailable. Using IMGUI: " +
                        (catalog.AvaloniaLaunchRequest != null ? catalog.AvaloniaLaunchRequest.FailureReason ?? string.Empty : string.Empty);
            }

            return string.Empty;
        }

        private List<RuntimeRootCandidate> BuildRuntimeCandidates(ICortexHostEnvironment environment)
        {
            var candidates = new List<RuntimeRootCandidate>();
            if (environment != null)
            {
                var bundledToolRootPath = NormalizePath(environment.BundledToolRootPath);
                if (!string.IsNullOrEmpty(bundledToolRootPath))
                {
                    var nestedDesktopHostBundleRoot = Path.Combine(bundledToolRootPath, "desktop-host");
                    AddRuntimeCandidate(
                        candidates,
                        Path.Combine(Path.Combine(nestedDesktopHostBundleRoot, "host"), "lib"),
                        nestedDesktopHostBundleRoot);
                    AddRuntimeCandidate(
                        candidates,
                        Path.Combine(Path.Combine(bundledToolRootPath, "host"), "lib"),
                        bundledToolRootPath);
                }

                var hostBinPath = NormalizePath(environment.HostBinPath);
                if (!string.IsNullOrEmpty(hostBinPath))
                {
                    var embeddedDesktopHostBundleRoot = Path.Combine(Path.Combine(hostBinPath, "tools"), "desktop-host");
                    AddRuntimeCandidate(
                        candidates,
                        Path.Combine(Path.Combine(embeddedDesktopHostBundleRoot, "host"), "lib"),
                        embeddedDesktopHostBundleRoot);
                    AddRuntimeCandidate(candidates, Path.Combine(hostBinPath, "decompiler"), string.Empty);
                }
            }

            var configuredBundleRoot = NormalizePath(_readEnvironmentVariable(DesktopBundleRootEnvironmentVariable));
            if (!string.IsNullOrEmpty(configuredBundleRoot))
            {
                AddRuntimeCandidate(
                    candidates,
                    Path.Combine(Path.Combine(configuredBundleRoot, "host"), "lib"),
                    configuredBundleRoot);
            }

            var repoRoot = TryFindRepositoryRoot(environment != null ? environment.ApplicationRootPath : string.Empty);
            if (!string.IsNullOrEmpty(repoRoot))
            {
                var desktopBundleRoot = Path.Combine(
                    Path.Combine(Path.Combine(repoRoot, "artifacts"), "bundles"),
                    "Desktop");
                AddRuntimeCandidate(
                    candidates,
                    Path.Combine(Path.Combine(desktopBundleRoot, "host"), "lib"),
                    desktopBundleRoot);
            }

            return candidates;
        }

        private static void AddRuntimeCandidate(IList<RuntimeRootCandidate> candidates, string hostRuntimeRootPath, string bundleRootPath)
        {
            var normalizedRuntimeRootPath = NormalizePath(hostRuntimeRootPath);
            if (string.IsNullOrEmpty(normalizedRuntimeRootPath))
            {
                return;
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                if (string.Equals(candidates[i].HostRuntimeRootPath, normalizedRuntimeRootPath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            candidates.Add(new RuntimeRootCandidate
            {
                HostRuntimeRootPath = normalizedRuntimeRootPath,
                BundleRootPath = NormalizePath(bundleRootPath)
            });
        }

        private string ResolveDotNetPath()
        {
            var pathValue = _readEnvironmentVariable("PATH") ?? string.Empty;
            if (string.IsNullOrEmpty(pathValue))
            {
                return string.Empty;
            }

            var parts = pathValue.Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < parts.Length; i++)
            {
                var directory = NormalizePath(parts[i]);
                if (string.IsNullOrEmpty(directory))
                {
                    continue;
                }

                var dotNetPath = Path.Combine(directory, "dotnet.exe");
                if (_fileExists(dotNetPath))
                {
                    return dotNetPath;
                }
            }

            return string.Empty;
        }

        private string BuildLaunchArguments(string bundleRootPath)
        {
            var arguments = " --pipe-name " + QuoteArgument(ResolveDesktopBridgePipeName());
            if (!string.IsNullOrEmpty(bundleRootPath))
            {
                arguments += " --bundle-root " + QuoteArgument(bundleRootPath);
            }

            return arguments;
        }

        private string ResolveDesktopBridgePipeName()
        {
            var configuredPipeName = _readEnvironmentVariable(DesktopBridgePipeNameEnvironmentVariable);
            return string.IsNullOrEmpty(configuredPipeName)
                ? DesktopBridgeDefaultPipeName
                : configuredPipeName;
        }

        private string TryFindRepositoryRoot(string applicationRootPath)
        {
            var current = NormalizePath(applicationRootPath);
            while (!string.IsNullOrEmpty(current))
            {
                if (_fileExists(Path.Combine(current, "Cortex.sln")) &&
                    _fileExists(Path.Combine(current, "Directory.Build.props")))
                {
                    return current;
                }

                var parent = Directory.GetParent(current);
                current = parent != null ? parent.FullName : string.Empty;
            }

            return string.Empty;
        }

        private static string[] CopyUnavailableReasons(IList<string> reasons)
        {
            var copy = new string[reasons != null ? reasons.Count : 0];
            if (reasons == null)
            {
                return copy;
            }

            for (var i = 0; i < reasons.Count; i++)
            {
                copy[i] = reasons[i] ?? string.Empty;
            }

            return copy;
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(path.Trim());
            }
            catch
            {
                return string.Empty;
            }
        }

        private sealed class RuntimeRootCandidate
        {
            public string HostRuntimeRootPath;
            public string BundleRootPath;
        }
    }
}
