using System;
using System.Collections.Generic;
using System.IO;
using Cortex.Core.Models;

namespace Cortex.Host.Sheltered.Runtime
{
    internal sealed class ShelteredRenderHostCatalog
    {
        public string SelectedRenderHostId = ShelteredRenderHostSettings.ImguiRenderHostId;
        public string EffectiveRenderHostId = ShelteredRenderHostSettings.ImguiRenderHostId;
        public string SettingsHelpText = string.Empty;
        public string StatusSummary = string.Empty;
        public string StartupStatusMessage = string.Empty;
        public readonly List<string> UnavailableReasons = new List<string>();
        public readonly List<SettingChoiceOption> AvailableOptions = new List<SettingChoiceOption>();
        public ShelteredExternalHostLaunchRequest AvaloniaLaunchRequest = new ShelteredExternalHostLaunchRequest();

        public SettingChoiceOption[] BuildOptions()
        {
            return AvailableOptions.ToArray();
        }

        public bool ShouldLaunchAvaloniaExternalHost
        {
            get
            {
                return string.Equals(
                    EffectiveRenderHostId,
                    ShelteredRenderHostSettings.AvaloniaExternalRenderHostId,
                    StringComparison.OrdinalIgnoreCase) &&
                    AvaloniaLaunchRequest != null &&
                    AvaloniaLaunchRequest.CanLaunch;
            }
        }

        public static ShelteredRenderHostCatalog CreateDefault()
        {
            var catalog = new ShelteredRenderHostCatalog();
            catalog.AvailableOptions.Add(new SettingChoiceOption
            {
                Value = ShelteredRenderHostSettings.ImguiRenderHostId,
                DisplayName = "IMGUI",
                Description = "Run Cortex directly inside the game with the current IMGUI shell."
            });
            catalog.SettingsHelpText = "Select how Cortex presents its workbench in the Sheltered host.";
            catalog.StatusSummary = "Host: IMGUI (in-game)";
            return catalog;
        }
    }

    internal sealed class ShelteredExternalHostLaunchRequest
    {
        public string CommandPath = string.Empty;
        public string Arguments = string.Empty;
        public string WorkingDirectory = string.Empty;
        public string SuccessStatusMessage = string.Empty;
        public string FailureReason = string.Empty;

        public bool CanLaunch
        {
            get { return !string.IsNullOrEmpty(CommandPath); }
        }
    }

    internal sealed class ShelteredRenderHostCatalogBuilder
    {
        private const string DesktopBridgePipeNameEnvironmentVariable = "CORTEX_DESKTOP_BRIDGE_PIPE_NAME";
        private const string DesktopBridgeDefaultPipeName = "cortex.desktop.bridge";
        private const string DesktopBundleRootEnvironmentVariable = "CORTEX_DESKTOP_BUNDLE_ROOT";
        private readonly Func<string, bool> _fileExists;
        private readonly Func<string, string> _readEnvironmentVariable;

        public ShelteredRenderHostCatalogBuilder()
            : this(File.Exists, Environment.GetEnvironmentVariable)
        {
        }

        internal ShelteredRenderHostCatalogBuilder(
            Func<string, bool> fileExists,
            Func<string, string> readEnvironmentVariable)
        {
            _fileExists = fileExists ?? File.Exists;
            _readEnvironmentVariable = readEnvironmentVariable ?? Environment.GetEnvironmentVariable;
        }

        public ShelteredRenderHostCatalog Build(ShelteredHostPathLayout layout, string selectedRenderHostId)
        {
            var catalog = ShelteredRenderHostCatalog.CreateDefault();
            catalog.SelectedRenderHostId = ShelteredRenderHostSettings.NormalizeRenderHostId(selectedRenderHostId);
            catalog.AvaloniaLaunchRequest = CreateAvaloniaLaunchRequest(layout);

            if (catalog.AvaloniaLaunchRequest.CanLaunch)
            {
                catalog.AvailableOptions.Add(new SettingChoiceOption
                {
                    Value = ShelteredRenderHostSettings.AvaloniaExternalRenderHostId,
                    DisplayName = "Avalonia",
                    Description = "Launch the external Avalonia desktop host on next startup. IMGUI stays available as the bootstrap and fallback surface."
                });
            }
            else if (!string.IsNullOrEmpty(catalog.AvaloniaLaunchRequest.FailureReason))
            {
                catalog.UnavailableReasons.Add("Avalonia (external): " + catalog.AvaloniaLaunchRequest.FailureReason);
            }

            catalog.EffectiveRenderHostId =
                string.Equals(catalog.SelectedRenderHostId, ShelteredRenderHostSettings.AvaloniaExternalRenderHostId, StringComparison.OrdinalIgnoreCase) &&
                catalog.AvaloniaLaunchRequest.CanLaunch
                    ? ShelteredRenderHostSettings.AvaloniaExternalRenderHostId
                    : ShelteredRenderHostSettings.ImguiRenderHostId;

            catalog.SettingsHelpText = BuildSettingsHelpText(catalog);
            catalog.StatusSummary = BuildStatusSummary(catalog);
            catalog.StartupStatusMessage = BuildStartupStatusMessage(catalog);
            return catalog;
        }

        internal ShelteredExternalHostLaunchRequest CreateAvaloniaLaunchRequest(ShelteredHostPathLayout layout)
        {
            var request = new ShelteredExternalHostLaunchRequest();
            var runtimeCandidates = BuildRuntimeCandidates(layout);
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

        private string BuildSettingsHelpText(ShelteredRenderHostCatalog catalog)
        {
            var helpText =
                "Select how Cortex presents its workbench in the Sheltered host. " +
                "IMGUI runs in-game. Avalonia launches the external desktop host on the next startup over the runtime bridge.";
            if (catalog != null && catalog.UnavailableReasons.Count > 0)
            {
                helpText += " Unavailable right now: " + string.Join(" ", catalog.UnavailableReasons.ToArray());
            }

            return helpText;
        }

        private static string BuildStatusSummary(ShelteredRenderHostCatalog catalog)
        {
            if (catalog == null)
            {
                return "Host: IMGUI (in-game)";
            }

            if (string.Equals(catalog.EffectiveRenderHostId, ShelteredRenderHostSettings.AvaloniaExternalRenderHostId, StringComparison.OrdinalIgnoreCase))
            {
                return "Host: Avalonia (external) | Renderer: IMGUI bridge bootstrap";
            }

            if (string.Equals(catalog.SelectedRenderHostId, ShelteredRenderHostSettings.AvaloniaExternalRenderHostId, StringComparison.OrdinalIgnoreCase) &&
                catalog.UnavailableReasons.Count > 0)
            {
                return "Host: IMGUI (Avalonia unavailable)";
            }

            return "Host: IMGUI (in-game)";
        }

        private static string BuildStartupStatusMessage(ShelteredRenderHostCatalog catalog)
        {
            if (catalog == null)
            {
                return string.Empty;
            }

            if (string.Equals(catalog.SelectedRenderHostId, ShelteredRenderHostSettings.AvaloniaExternalRenderHostId, StringComparison.OrdinalIgnoreCase))
            {
                return catalog.AvaloniaLaunchRequest != null && catalog.AvaloniaLaunchRequest.CanLaunch
                    ? "Launching external Avalonia host."
                    : "Avalonia external host unavailable. Using IMGUI: " +
                        (catalog.AvaloniaLaunchRequest != null ? catalog.AvaloniaLaunchRequest.FailureReason ?? string.Empty : string.Empty);
            }

            return string.Empty;
        }

        private List<RuntimeRootCandidate> BuildRuntimeCandidates(ShelteredHostPathLayout layout)
        {
            var candidates = new List<RuntimeRootCandidate>();
            if (layout != null && !string.IsNullOrEmpty(layout.HostBinPath))
            {
                AddRuntimeCandidate(candidates, Path.Combine(layout.HostBinPath, "decompiler"), string.Empty);
            }

            var configuredBundleRoot = NormalizePath(_readEnvironmentVariable(DesktopBundleRootEnvironmentVariable));
            if (!string.IsNullOrEmpty(configuredBundleRoot))
            {
                AddRuntimeCandidate(
                    candidates,
                    Path.Combine(Path.Combine(configuredBundleRoot, "host"), "lib"),
                    configuredBundleRoot);
            }

            var repoRoot = TryFindRepositoryRoot(layout != null ? layout.ApplicationRootPath : string.Empty);
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
            public string HostRuntimeRootPath = string.Empty;
            public string BundleRootPath = string.Empty;
        }
    }
}
