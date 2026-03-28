using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Shell
{
    public sealed class RoslynLanguageProviderFactory : ILanguageProviderFactory
    {
        public const string ProviderId = "roslyn";

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
            var settings = configuration != null ? configuration.Settings : null;
            var builder = new StringBuilder();
            builder.Append(configuration != null ? configuration.ProviderId ?? string.Empty : string.Empty);
            builder.Append("|host=");
            builder.Append(configuration != null ? configuration.HostBinPath ?? string.Empty : string.Empty);
            builder.Append("|path=");
            builder.Append(settings != null ? settings.RoslynServicePathOverride ?? string.Empty : string.Empty);
            builder.Append("|timeout=");
            builder.Append(settings != null ? settings.RoslynServiceTimeoutMs : 0);
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
                configuration != null && configuration.Settings != null
                    ? configuration.Settings.RoslynServiceTimeoutMs
                    : 15000,
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
                configuration != null && configuration.Settings != null
                    ? configuration.Settings.RoslynServiceTimeoutMs
                    : 15000,
                generation);
        }

        private static string ResolveWorkerPath(LanguageRuntimeConfiguration configuration)
        {
            var settings = configuration != null ? configuration.Settings : null;
            var candidates = new List<string>();
            if (settings != null && !string.IsNullOrEmpty(settings.RoslynServicePathOverride))
            {
                candidates.Add(settings.RoslynServicePathOverride);
            }

            var hostBinPath = configuration != null ? configuration.HostBinPath ?? string.Empty : string.Empty;
            if (!string.IsNullOrEmpty(hostBinPath))
            {
                candidates.Add(Path.Combine(Path.Combine(hostBinPath, "roslyn"), "Cortex.Roslyn.Worker.exe"));
                candidates.Add(Path.Combine(Path.Combine(hostBinPath, "roslyn"), "Cortex.Roslyn.Worker.dll"));
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                try
                {
                    var candidate = Path.GetFullPath(candidates[i]);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch
                {
                }
            }

            return string.Empty;
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
    }
}
