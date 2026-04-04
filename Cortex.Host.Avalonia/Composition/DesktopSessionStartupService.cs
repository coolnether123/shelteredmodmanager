using System;
using System.IO;
using Cortex.Bridge;

namespace Cortex.Host.Avalonia.Composition
{
    internal sealed class DesktopSessionStartupService
    {
        private readonly DesktopHostPathPolicy _pathPolicy;

        public DesktopSessionStartupService()
            : this(new DesktopHostPathPolicy())
        {
        }

        public DesktopSessionStartupService(DesktopHostPathPolicy pathPolicy)
        {
            _pathPolicy = pathPolicy ?? new DesktopHostPathPolicy();
        }

        public DesktopHostApplicationSession Start(string[] args)
        {
            return new DesktopHostApplicationSession(BuildOptions(args));
        }

        public DesktopHostOptions BuildOptions(string[] args)
        {
            var dataRootPath = _pathPolicy.ResolveDataRootPath();
            Directory.CreateDirectory(dataRootPath);

            return new DesktopHostOptions
            {
                DataRootPath = dataRootPath,
                LogFilePath = _pathPolicy.ResolveLogFilePath(dataRootPath),
                ShellStateFilePath = _pathPolicy.ResolveShellStateFilePath(dataRootPath),
                DockLayoutFilePath = _pathPolicy.ResolveDockLayoutFilePath(dataRootPath),
                BridgeClient = new DesktopBridgeClientOptions
                {
                    PipeName = ResolvePipeName(args),
                    ClientDisplayName = DesktopBridgeProtocol.DefaultClientDisplayName
                }
            };
        }

        private static string ResolvePipeName(string[] args)
        {
            if (args != null)
            {
                for (var i = 0; i < args.Length - 1; i++)
                {
                    if (string.Equals(args[i], "--pipe-name", StringComparison.OrdinalIgnoreCase))
                    {
                        return string.IsNullOrEmpty(args[i + 1])
                            ? DesktopBridgeProtocol.DefaultPipeName
                            : args[i + 1];
                    }
                }
            }

            var configuredPipeName = Environment.GetEnvironmentVariable(DesktopBridgeProtocol.PipeNameEnvironmentVariable);
            return string.IsNullOrEmpty(configuredPipeName)
                ? DesktopBridgeProtocol.DefaultPipeName
                : configuredPipeName;
        }
    }
}
