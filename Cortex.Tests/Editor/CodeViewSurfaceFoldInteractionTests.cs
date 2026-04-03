using System;
using System.Reflection;
using Cortex.Core.Models;
using Cortex.Modules.Editor;
using Cortex.Tests.Testing;
using Xunit;

namespace Cortex.Tests.Editor
{
    public sealed class CodeViewSurfaceFoldInteractionTests
    {
        [Fact]
        public void TryHandleFoldToggle_WhenPrimaryButtonPressed_InvalidatesLayout()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var surface = new CodeViewSurface(new TestEditorContextService(null), null, null);
                var session = new DocumentSession { FilePath = @"C:\temp\Example.cs" };
                var layoutField = typeof(CodeViewSurface).GetField("_layout", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(layoutField);

                layoutField.SetValue(surface, CreateNestedInstance("CodeViewLayout"));

                var foldRegion = CreateNestedInstance("FoldRegion");
                SetNestedField(foldRegion, "Key", session.FilePath + "|brace|4|12");

                var method = typeof(CodeViewSurface).GetMethod("TryHandleFoldToggle", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(method);

                var abortDraw = (bool)method.Invoke(surface, new object[]
                {
                    session,
                    foldRegion,
                    0
                });

                Assert.True(abortDraw);
                Assert.Null(layoutField.GetValue(surface));
            });
        }

        private static object CreateNestedInstance(string typeName)
        {
            var type = typeof(CodeViewSurface).GetNestedType(typeName, BindingFlags.NonPublic);
            Assert.NotNull(type);
            return Activator.CreateInstance(type, true);
        }

        private static void SetNestedField(object instance, string fieldName, object value)
        {
            Assert.NotNull(instance);
            var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field.SetValue(instance, value);
        }

    }
}
