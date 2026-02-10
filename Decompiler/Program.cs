using System;
using System.CommandLine;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ModAPI.Decompiler
{
    /// <summary>
    /// Command-line entry point for the decompiler sidecar.
    /// Supports source, json, and binary (.modtrace) output plus privacy probing.
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// Parses CLI options, runs privacy checks or decompilation, and writes output artifacts.
        /// Exit codes:
        /// 0 = success, 1 = invalid input, 2 = runtime/decompilation failure.
        /// </summary>
        private static async Task<int> Main(string[] args)
        {
            var assemblyOption = new Option<string>(
                name: "--assembly",
                description: "Path to the assembly file");

            var tokenOption = new Option<int>(
                name: "--token",
                description: "Metadata token of the method");

            var outputOption = new Option<string>(
                name: "--output",
                description: "Output file path");

            var formatOption = new Option<string>(
                name: "--format",
                getDefaultValue: () => "source",
                description: "Output format: source, json, binary");

            var privacyCheckOption = new Option<bool>(
                name: "--privacy-check",
                description: "Check ModPrivacy for this method and return decision text.");

            assemblyOption.IsRequired = true;
            tokenOption.IsRequired = true;

            var rootCommand = new RootCommand("ModAPI Decompiler Service");
            rootCommand.AddOption(assemblyOption);
            rootCommand.AddOption(tokenOption);
            rootCommand.AddOption(outputOption);
            rootCommand.AddOption(formatOption);
            rootCommand.AddOption(privacyCheckOption);

            rootCommand.SetHandler((assembly, token, output, format, privacyCheck) =>
            {
                try
                {
                    if (!File.Exists(assembly))
                    {
                        Console.Error.WriteLine($"Error: Assembly not found: {assembly}");
                        Environment.Exit(1);
                    }

                    if (privacyCheck)
                    {
                        var privacy = new PrivacyInspector().Check(assembly, token);
                        var text = BuildPrivacyText(privacy);
                        if (!string.IsNullOrWhiteSpace(output))
                        {
                            EnsureOutputDirectory(output);
                            File.WriteAllText(output, text, Encoding.UTF8);
                        }

                        Console.WriteLine(text);
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(output))
                    {
                        Console.Error.WriteLine("Error: --output is required unless --privacy-check is used.");
                        Environment.Exit(1);
                    }

                    EnsureOutputDirectory(output);

                    var outputFormat = ParseFormat(format);
                    var engine = new DecompilerEngine();
                    var artifact = engine.Decompile(assembly, token);

                    if (outputFormat == OutputFormat.Source)
                    {
                        File.WriteAllText(output, artifact.SourceCode ?? string.Empty, Encoding.UTF8);
                        File.WriteAllText(Path.ChangeExtension(output, ".map"), artifact.GetMapText(), Encoding.UTF8);
                    }
                    else if (outputFormat == OutputFormat.Json)
                    {
                        var json = JsonSerializer.Serialize(artifact, new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(output, json, Encoding.UTF8);
                    }
                    else
                    {
                        var serializer = new BinarySerializer();
                        serializer.Write(output, artifact);
                    }

                    Console.WriteLine("Generated (" + outputFormat + "): " + output);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error: {ex.Message}");
                    Console.Error.WriteLine(ex.StackTrace);
                    Environment.Exit(2);
                }
            }, assemblyOption, tokenOption, outputOption, formatOption, privacyCheckOption);

            return await rootCommand.InvokeAsync(args);
        }

        /// <summary>
        /// Produces a stable text payload for --privacy-check output.
        /// The ModAPI runtime parses these keys directly.
        /// </summary>
        private static string BuildPrivacyText(PrivacyCheckResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Level:" + result.Level);
            sb.AppendLine("Reason:" + (result.Reason ?? string.Empty));
            sb.AppendLine("MethodName:" + (result.MethodName ?? string.Empty));
            sb.AppendLine("Signature:" + (result.MethodSignature ?? string.Empty));
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Maps user input to a supported output format.
        /// </summary>
        private static OutputFormat ParseFormat(string format)
        {
            if (string.IsNullOrWhiteSpace(format))
            {
                return OutputFormat.Source;
            }

            switch (format.Trim().ToLowerInvariant())
            {
                case "source":
                case "text":
                    return OutputFormat.Source;
                case "json":
                    return OutputFormat.Json;
                case "binary":
                case "modtrace":
                    return OutputFormat.Binary;
                default:
                    throw new ArgumentException("Unsupported format '" + format + "'. Expected: source, json, binary.");
            }
        }

        /// <summary>
        /// Ensures the output directory exists before writing any artifact files.
        /// </summary>
        private static void EnsureOutputDirectory(string outputPath)
        {
            var fullPath = Path.GetFullPath(outputPath);
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
    }
}
