using System.Linq;
using System.Reflection;
using Cortex.Core.Abstractions;
using Cortex.Shell;
using Xunit;

namespace Cortex.Tests.Shell
{
    public sealed class ShellServiceMapTests
    {
        [Fact]
        public void ShellServiceMap_ExposesGetOnlyProperties_AndStableEmptyInstance()
        {
            var properties = typeof(ShellServiceMap)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .OrderBy(property => property.Name)
                .ToArray();

            Assert.NotEmpty(properties);
            Assert.All(properties, property => Assert.True(property.CanRead));
            Assert.All(properties, property => Assert.False(property.CanWrite));
            Assert.NotNull(ShellServiceMap.Empty);
            Assert.Null(ShellServiceMap.Empty.ProjectCatalog);
        }

        [Fact]
        public void ShellServiceMap_CapturesConstructorValues()
        {
            var projectCatalog = new StubProjectCatalog();
            var serviceMap = new ShellServiceMap(projectCatalog: projectCatalog);

            Assert.Same(projectCatalog, serviceMap.ProjectCatalog);
        }

        private sealed class StubProjectCatalog : IProjectCatalog
        {
            public System.Collections.Generic.IList<Cortex.Core.Models.CortexProjectDefinition> GetProjects() { return new Cortex.Core.Models.CortexProjectDefinition[0]; }
            public Cortex.Core.Models.CortexProjectDefinition GetProject(string modId) { return null; }
            public void Upsert(Cortex.Core.Models.CortexProjectDefinition definition) { }
            public void Remove(string modId) { }
        }
    }
}
