using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Xunit;

namespace Cortex.Tests.Testing
{
    public sealed class UnityManagedAssemblyResolverTests
    {
        [Fact]
        public void SearchRoots_IncludeLocalRuntimeAndLoadedUnityAssemblyDirectories()
        {
            var roots = UnityManagedAssemblyResolver.GetManagedAssemblySearchRoots();
            var runtimeDirectory = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
            var unityAssemblyDirectory = Path.GetDirectoryName(Path.GetFullPath(typeof(Rect).Assembly.Location));

            Assert.Contains(roots, root => string.Equals(root, runtimeDirectory, StringComparison.OrdinalIgnoreCase));
            Assert.Contains(roots, root => string.Equals(root, unityAssemblyDirectory, StringComparison.OrdinalIgnoreCase));
        }
    }
}
