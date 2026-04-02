using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;

namespace Cortex.Shell
{
    public sealed class RoslynLanguageProviderFactory : ILanguageProviderFactory
    {
        public const string ProviderId = "roslyn";
        public const string WorkerPathOverrideSettingId = "workerPathOverride";
        public const string RequestTimeoutMsSettingId = "requestTimeoutMs";
        private const int DefaultRequestTimeoutMs = 15000;

        private readonly LanguageProviderDescriptor _descriptor = new LanguageProviderDescriptor
        {
            ProviderId = RoslynLanguageProviderFactory.ProviderId,
            DisplayName = "Roslyn",
            Version = ResolveAssemblyVersion(),
            Source = "built-in"
        };

        public LanguageProviderDescriptor Descriptor
        {
            get { return _descriptor; }
        }

        public string BuildConfigurationFingerprint(LanguageRuntimeConfiguration configuration)
        {
            var providerSettings = ResolveProviderSettings(configuration);
            var builder = new StringBuilder();
            builder.Append(configuration != null ? configuration.ProviderId ?? string.Empty : string.Empty);
            builder.Append("|host=");
            builder.Append(configuration != null ? configuration.HostBinPath ?? string.Empty : string.Empty);
            builder.Append("|path=");
            builder.Append(providerSettings.WorkerPathOverride ?? string.Empty);
            builder.Append("|timeout=");
            builder.Append(providerSettings.RequestTimeoutMs);
            return builder.ToString();
        }

        public bool TryCreate(LanguageRuntimeConfiguration configuration, out ILanguageProviderSession session, out string unavailableReason)
        {
            var workerPath = ResolveWorkerPath(configuration);
            if (string.IsNullOrEmpty(workerPath))
            {
                session = null;
                unavailableReason = "Roslyn worker path could not be resolved.";
                return false;
            }

            session = new RoslynLanguageProviderSession(
                CloneDescriptor(_descriptor),
                BuildConfigurationFingerprint(configuration),
                workerPath,
                ResolveProviderSettings(configuration).RequestTimeoutMs,
                0);
            unavailableReason = string.Empty;
            return true;
        }

        public ILanguageProviderSession Create(LanguageRuntimeConfiguration configuration, int generation)
        {
            var workerPath = ResolveWorkerPath(configuration);
            if (string.IsNullOrEmpty(workerPath))
            {
                return null;
            }

            return new RoslynLanguageProviderSession(
                CloneDescriptor(_descriptor),
                BuildConfigurationFingerprint(configuration),
                workerPath,
                ResolveProviderSettings(configuration).RequestTimeoutMs,
                generation);
        }

        private static string ResolveWorkerPath(LanguageRuntimeConfiguration configuration)
        {
            var providerSettings = ResolveProviderSettings(configuration);
            var candidates = new List<string>();
            if (!string.IsNullOrEmpty(providerSettings.WorkerPathOverride))
            {
                candidates.Add(providerSettings.WorkerPathOverride);
            }

            var hostBinPath = configuration != null ? configuration.HostBinPath ?? string.Empty : string.Empty;
            if (!string.IsNullOrEmpty(hostBinPath))
            {
                candidates.Add(BundledToolPathResolver.ResolveFromHostBin(
                    hostBinPath,
                    "roslyn",
                    "roslyn",
                    "Cortex.Roslyn.Worker.exe",
                    "Cortex.Roslyn.Worker.dll"));
            }

            return BundledToolPathResolver.ResolveCandidate(candidates);
        }

        private static RoslynProviderSettings ResolveProviderSettings(LanguageRuntimeConfiguration configuration)
        {
            var resolved = new RoslynProviderSettings
            {
                WorkerPathOverride = string.Empty,
                RequestTimeoutMs = DefaultRequestTimeoutMs
            };

            var providerConfiguration = configuration != null ? configuration.ProviderConfiguration : null;
            if (providerConfiguration == null ||
                !string.Equals(providerConfiguration.ProviderId ?? string.Empty, ProviderId, StringComparison.OrdinalIgnoreCase) ||
                providerConfiguration.Settings == null)
            {
                return resolved;
            }

            var workerPathOverride = LanguageProviderConfigurationHelper.GetSettingValue(providerConfiguration, WorkerPathOverrideSettingId);
            if (!string.IsNullOrEmpty(workerPathOverride))
            {
                resolved.WorkerPathOverride = workerPathOverride;
            }

            var requestTimeoutValue = LanguageProviderConfigurationHelper.GetSettingValue(providerConfiguration, RequestTimeoutMsSettingId);
            int timeoutMs;
            if (int.TryParse(requestTimeoutValue ?? string.Empty, out timeoutMs) && timeoutMs > 0)
            {
                resolved.RequestTimeoutMs = timeoutMs;
            }

            return resolved;
        }

        private static string ResolveAssemblyVersion()
        {
            try
            {
                var version = typeof(RoslynLanguageProviderFactory).Assembly.GetName().Version;
                return version != null ? version.ToString() : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static LanguageProviderDescriptor CloneDescriptor(LanguageProviderDescriptor descriptor)
        {
            return new LanguageProviderDescriptor
            {
                ProviderId = descriptor != null ? descriptor.ProviderId ?? string.Empty : string.Empty,
                DisplayName = descriptor != null ? descriptor.DisplayName ?? string.Empty : string.Empty,
                Version = descriptor != null ? descriptor.Version ?? string.Empty : string.Empty,
                Source = descriptor != null ? descriptor.Source ?? string.Empty : string.Empty
            };
        }

        private sealed class RoslynProviderSettings
        {
            public string WorkerPathOverride = string.Empty;
            public int RequestTimeoutMs = DefaultRequestTimeoutMs;
        }
    }
}
