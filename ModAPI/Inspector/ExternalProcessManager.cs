using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using ModAPI.Core;

namespace ModAPI.Inspector
{
    public sealed class ExternalProcessManager
    {
        public sealed class ProcessResult
        {
            public int ExitCode;
            public bool TimedOut;
            public string StandardOutput;
            public string StandardError;
        }

        public sealed class PrivacyProbeResult
        {
            public PrivacyLevel Level;
            public string Reason;
            public string MethodName;
            public string Signature;
        }

        public string ResolveDecompilerPath()
        {
            var modApiDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
            var local = Path.Combine(Path.Combine(Path.Combine(modApiDir, "bin"), "decompiler"), "Decompiler.exe");
            if (File.Exists(local))
            {
                return local;
            }

            var fallback = Path.Combine(modApiDir, "Decompiler.exe");
            return fallback;
        }

        public PrivacyProbeResult ProbePrivacy(string decompilerPath, string assemblyPath, int token, int timeoutMs)
        {
            var args = BuildArgs(assemblyPath, token) + " --privacy-check";
            var result = Run(decompilerPath, args, timeoutMs);
            if (result.TimedOut)
            {
                throw new TimeoutException("Decompiler privacy check timed out.");
            }

            if (result.ExitCode != 0)
            {
                throw new Exception("Decompiler privacy check failed: " + result.StandardError);
            }

            return ParsePrivacyOutput(result.StandardOutput);
        }

        public ProcessResult RequestDecompileSource(string decompilerPath, string assemblyPath, int token, string outputPath, int timeoutMs)
        {
            var args = BuildArgs(assemblyPath, token) + " --output " + Quote(outputPath) + " --format source";
            return Run(decompilerPath, args, timeoutMs);
        }

        public ProcessResult Run(string executable, string arguments, int timeoutMs)
        {
            var psi = new ProcessStartInfo(executable, arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = Process.Start(psi))
            {
                if (process == null)
                {
                    throw new Exception("Failed to start process: " + executable);
                }

                if (!process.WaitForExit(timeoutMs))
                {
                    try { process.Kill(); } catch { }
                    return new ProcessResult
                    {
                        ExitCode = -1,
                        TimedOut = true,
                        StandardOutput = string.Empty,
                        StandardError = "Process timed out."
                    };
                }

                return new ProcessResult
                {
                    ExitCode = process.ExitCode,
                    TimedOut = false,
                    StandardOutput = process.StandardOutput.ReadToEnd(),
                    StandardError = process.StandardError.ReadToEnd()
                };
            }
        }

        public static string BuildArgs(string assemblyPath, int token)
        {
            return "--assembly " + Quote(assemblyPath) + " --token " + token;
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private static PrivacyProbeResult ParsePrivacyOutput(string output)
        {
            var result = new PrivacyProbeResult
            {
                Level = PrivacyLevel.Public,
                Reason = string.Empty,
                MethodName = string.Empty,
                Signature = string.Empty
            };

            using (var reader = new StringReader(output ?? string.Empty))
            {
                while (true)
                {
                    var line = reader.ReadLine();
                    if (line == null) break;

                    if (line.StartsWith("Level:", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Level = ParseLevel(line.Substring("Level:".Length).Trim());
                    }
                    else if (line.StartsWith("Reason:", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Reason = line.Substring("Reason:".Length).Trim();
                    }
                    else if (line.StartsWith("MethodName:", StringComparison.OrdinalIgnoreCase))
                    {
                        result.MethodName = line.Substring("MethodName:".Length).Trim();
                    }
                    else if (line.StartsWith("Signature:", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Signature = line.Substring("Signature:".Length).Trim();
                    }
                }
            }

            return result;
        }

        private static PrivacyLevel ParseLevel(string raw)
        {
            if (string.Equals(raw, "Private", StringComparison.OrdinalIgnoreCase))
            {
                return PrivacyLevel.Private;
            }

            if (string.Equals(raw, "Obfuscated", StringComparison.OrdinalIgnoreCase))
            {
                return PrivacyLevel.Obfuscated;
            }

            return PrivacyLevel.Public;
        }
    }
}
