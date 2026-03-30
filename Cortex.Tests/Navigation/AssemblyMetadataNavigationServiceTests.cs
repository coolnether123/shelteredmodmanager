using System.Reflection;
using Cortex.Core.Models;
using Cortex.Services.Navigation.Metadata;
using Xunit;

namespace Cortex.Tests.Navigation
{
    public sealed class AssemblyMetadataNavigationServiceTests
    {
        private sealed class MetadataExample
        {
            public void Execute(string input)
            {
            }
        }

        [Fact]
        public void TryResolveMetadataTarget_ResolvesMethodDocumentationId_ToMethodEntity()
        {
            var service = new AssemblyMetadataNavigationService();
            var method = typeof(MetadataExample).GetMethod("Execute", BindingFlags.Instance | BindingFlags.Public);

            MetadataNavigationTarget target;
            var resolved = service.TryResolveMetadataTarget(
                typeof(MetadataExample).Assembly.Location,
                BuildMethodDocumentationId(method),
                typeof(MetadataExample).FullName,
                "Method",
                out target);

            Assert.True(resolved);
            Assert.NotNull(target);
            Assert.Equal(DecompilerEntityKind.Method, target.EntityKind);
            Assert.Equal(method.MetadataToken, target.MetadataToken);
        }

        [Fact]
        public void TryResolveTypeNavigationTarget_ResolvesFullTypeName()
        {
            var service = new AssemblyMetadataNavigationService();

            string fullTypeName;
            var resolved = service.TryResolveTypeNavigationTarget(
                typeof(MetadataExample).Assembly.Location,
                typeof(MetadataExample).MetadataToken,
                out fullTypeName);

            Assert.True(resolved);
            Assert.Equal(typeof(MetadataExample).FullName, fullTypeName);
        }

        private static string BuildMethodDocumentationId(MethodBase method)
        {
            return "M:" + method.DeclaringType.FullName.Replace('+', '.') + "." + method.Name + "(System.String)";
        }
    }
}
