using System;
using System.IO;
using Cortex.Core.Models;
using Cortex.Tabby;
using Xunit;

namespace Cortex.Tests.Shell
{
    public sealed class BundledTabbyServerControllerTests
    {
        [Fact]
        public void ResolveServerPath_PrefersExplicitBundledToolRoot()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "cortex-tabby-tools-" + Guid.NewGuid().ToString("N"));
            var hostBinPath = Path.Combine(tempRoot, "host-bin");
            var bundledToolRootPath = Path.Combine(tempRoot, "tooling");
            var toolPath = Path.Combine(Path.Combine(bundledToolRootPath, "tabby"), "Cortex.Tabby.Server.dll");

            Directory.CreateDirectory(Path.GetDirectoryName(toolPath) ?? string.Empty);
            File.WriteAllText(toolPath, string.Empty);

            try
            {
                var resolvedPath = BundledTabbyServerController.ResolveServerPath(
                    new CompletionAugmentationProviderContext
                    {
                        HostBinPath = hostBinPath,
                        BundledToolRootPath = bundledToolRootPath
                    });

                Assert.Equal(Path.GetFullPath(toolPath), resolvedPath);
            }
            finally
            {
                TryDeleteDirectory(tempRoot);
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                return;
            }

            try
            {
                Directory.Delete(path, true);
            }
            catch
            {
            }
        }
    }
}
