using System;
using System.Collections.Generic;
using System.IO;
using Cortex.Core.Diagnostics;
using Cortex.Host.Sheltered.Runtime;

namespace Cortex.Platform.ModAPI.Runtime
{
    public sealed class ModApiCortexDiagnosticConfiguration : ICortexDiagnosticConfiguration
    {
        private readonly bool _enableAll;
        private readonly CortexLogLevel _minimumLevel;
        private readonly string[] _channelPatterns;

        public ModApiCortexDiagnosticConfiguration()
        {
            var settings = ReadSettings();
            _minimumLevel = ParseLevel(GetSetting(settings, "CortexDiagnosticsLevel"), CortexLogLevel.Info);

            var configuredChannels = GetSetting(settings, "CortexDiagnostics");
            if (string.IsNullOrEmpty(configuredChannels))
            {
                _channelPatterns = new string[0];
                return;
            }

            var normalized = configuredChannels.Trim();
            if (string.Equals(normalized, "all", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "*", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase))
            {
                _enableAll = true;
                _channelPatterns = new string[0];
                return;
            }

            _channelPatterns = SplitPatterns(normalized);
        }

        public bool IsEnabled(string channel, CortexLogLevel level)
        {
            if (level < _minimumLevel)
            {
                return false;
            }

            if (_enableAll)
            {
                return true;
            }

            if (_channelPatterns == null || _channelPatterns.Length == 0)
            {
                return false;
            }

            var normalizedChannel = (channel ?? string.Empty).Trim();
            if (normalizedChannel.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < _channelPatterns.Length; i++)
            {
                if (Matches(_channelPatterns[i], normalizedChannel))
                {
                    return true;
                }
            }

            return false;
        }

        private static Dictionary<string, string> ReadSettings()
        {
            var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var iniPath = ShelteredHostPathLayout.FromCurrentDirectory().ModManagerIniPath;
                if (!File.Exists(iniPath))
                {
                    return settings;
                }

                var lines = File.ReadAllLines(iniPath);
                for (var i = 0; i < lines.Length; i++)
                {
                    var raw = lines[i];
                    if (string.IsNullOrEmpty(raw))
                    {
                        continue;
                    }

                    var line = raw.Trim();
                    if (line.Length == 0 ||
                        line[0] == '#' ||
                        line[0] == ';' ||
                        line[0] == '[')
                    {
                        continue;
                    }

                    var separator = line.IndexOf('=');
                    if (separator <= 0)
                    {
                        continue;
                    }

                    var key = line.Substring(0, separator).Trim();
                    var value = separator + 1 < line.Length ? line.Substring(separator + 1).Trim() : string.Empty;
                    if (key.Length == 0)
                    {
                        continue;
                    }

                    settings[key] = value;
                }
            }
            catch
            {
            }

            return settings;
        }

        private static string GetSetting(Dictionary<string, string> settings, string key)
        {
            string value;
            return settings != null && !string.IsNullOrEmpty(key) && settings.TryGetValue(key, out value)
                ? value ?? string.Empty
                : string.Empty;
        }

        private static CortexLogLevel ParseLevel(string value, CortexLogLevel fallback)
        {
            if (string.IsNullOrEmpty(value))
            {
                return fallback;
            }

            try
            {
                return (CortexLogLevel)Enum.Parse(typeof(CortexLogLevel), value, true);
            }
            catch
            {
                return fallback;
            }
        }

        private static string[] SplitPatterns(string value)
        {
            var parts = (value ?? string.Empty).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new List<string>(parts.Length);
            for (var i = 0; i < parts.Length; i++)
            {
                var trimmed = parts[i] != null ? parts[i].Trim() : string.Empty;
                if (trimmed.Length > 0)
                {
                    result.Add(trimmed);
                }
            }

            return result.ToArray();
        }

        private static bool Matches(string pattern, string channel)
        {
            if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(channel))
            {
                return false;
            }

            if (string.Equals(pattern, channel, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (pattern[pattern.Length - 1] == '*')
            {
                var prefix = pattern.Substring(0, pattern.Length - 1);
                return channel.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }
    }
}
