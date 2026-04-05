using System;
using System.IO;
using Cortex.Bridge;

namespace Cortex.Host.Avalonia.Composition
{
    internal sealed class DesktopSessionStartupService
    {
        private readonly DesktopHostPathPolicy _pathPolicy;
        private readonly DesktopBundlePolicy _bundlePolicy;

        public DesktopSessionStartupService()
            : this(new DesktopHostPathPolicy(), new DesktopBundlePolicy())
        {
        }

        public DesktopSessionStartupService(
            DesktopHostPathPolicy pathPolicy,
            DesktopBundlePolicy bundlePolicy)
        {
            _pathPolicy = pathPolicy ?? new DesktopHostPathPolicy();
            _bundlePolicy = bundlePolicy ?? new DesktopBundlePolicy();
        }

        public DesktopHostApplicationSession Start(string[] args)
        {
            return new DesktopHostApplicationSession(BuildOptions(args));
        }

        public DesktopHostOptions BuildOptions(string[] args)
        {
            var dataRootPath = _pathPolicy.ResolveDataRootPath();
            var bundleRootPath = _pathPolicy.ResolveBundleRootPath(
                AppContext.BaseDirectory,
                ResolveBundleRootOverride(args));
            var logFilePath = _pathPolicy.ResolveLogFilePath(dataRootPath, AppContext.BaseDirectory, bundleRootPath);
            var environmentPaths = DesktopHostEnvironmentPaths.Create(
                AppContext.BaseDirectory,
                bundleRootPath,
                _bundlePolicy);
            Directory.CreateDirectory(dataRootPath);
            EnsureParentDirectoryExists(logFilePath);

            return new DesktopHostOptions
            {
                BundleProfileName = _bundlePolicy.ProfileName,
                DataRootPath = dataRootPath,
                LogFilePath = logFilePath,
                ShellStateFilePath = _pathPolicy.ResolveShellStateFilePath(dataRootPath),
                DockLayoutFilePath = _pathPolicy.ResolveDockLayoutFilePath(dataRootPath),
                StartupModeSummary = BuildStartupModeSummary(environmentPaths),
                LaunchToken = ResolveOptionValue(args, "--launch-token"),
                EnvironmentPaths = environmentPaths,
                BridgeClient = new DesktopBridgeClientOptions
                {
                    PipeName = ResolvePipeName(args),
                    ClientDisplayName = DesktopBridgeProtocol.DefaultClientDisplayName
                }
            };
        }

        private static string ResolvePipeName(string[] args)
        {
            var explicitPipeName = ResolveOptionValue(args, "--pipe-name");
            if (!string.IsNullOrEmpty(explicitPipeName))
            {
                return explicitPipeName;
            }

            var configuredPipeName = Environment.GetEnvironmentVariable(DesktopBridgeProtocol.PipeNameEnvironmentVariable);
            return string.IsNullOrEmpty(configuredPipeName)
                ? DesktopBridgeProtocol.DefaultPipeName
                : configuredPipeName;
        }

        private static string ResolveBundleRootOverride(string[] args)
        {
            var explicitBundleRoot = ResolveOptionValue(args, DesktopDefaultHostProfile.BundleRootArgumentName);
            if (!string.IsNullOrEmpty(explicitBundleRoot))
            {
                return explicitBundleRoot;
            }

            return Environment.GetEnvironmentVariable(DesktopDefaultHostProfile.BundleRootEnvironmentVariable) ?? string.Empty;
        }

        private static string ResolveOptionValue(string[] args, string optionName)
        {
            if (args == null || string.IsNullOrEmpty(optionName))
            {
                return string.Empty;
            }

            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], optionName, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1] ?? string.Empty;
                }
            }

            return string.Empty;
        }

        private string BuildStartupModeSummary(DesktopHostEnvironmentPaths environmentPaths)
        {
            var bundleRootPath = environmentPaths != null ? environmentPaths.BundleRootPath ?? string.Empty : string.Empty;
            return "Desktop-first launch profile " + _bundlePolicy.ProfileName +
                " with Avalonia as the host entry point. BundleRoot=" + bundleRootPath + ".";
        }

        private static void EnsureParentDirectoryExists(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            try
            {
                var parentDirectory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(parentDirectory))
                {
                    Directory.CreateDirectory(parentDirectory);
                }
            }
            catch
            {
            }
        }
    }
}
