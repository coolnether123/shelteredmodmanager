using System;
using System.IO;
using Cortex.Core.Models;
using Cortex.Services;
using Cortex.Tests.Testing;
using Xunit;

namespace Cortex.Tests.Editor
{
    public sealed class EditorCommandExecutionStrategyServiceTests
    {
        [Fact]
        public void GetAvailability_RemoveAndSortUsings_UsesPreviewForSourceTabOutsideEditMode()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var service = new EditorCommandExecutionStrategyService(
                    new EditorLogicalDocumentTargetResolutionService(),
                    new TestClipboardService());

                var availability = service.GetAvailability(
                    "cortex.editor.removeAndSortUsings",
                    new CortexShellState(),
                    new EditorCommandTarget
                    {
                        DocumentKind = DocumentKind.SourceCode,
                        DocumentPath = @"D:\workspace\Test.cs",
                        SupportsEditing = true,
                        IsEditModeEnabled = false,
                        CanToggleEditMode = true
                    });

                Assert.True(availability.Visible);
                Assert.True(availability.Enabled);
                Assert.Equal(EditorCommandExecutionKind.PreviewApply, availability.ExecutionKind);
            });
        }

        [Fact]
        public void GetAvailability_RemoveAndSortUsings_UsesResolvedSourceForDecompilerSymbol()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var filePath = CreateTempFile("using Zeta;\r\nclass C { }\r\n");
                try
                {
                    var service = new EditorCommandExecutionStrategyService(
                        new EditorLogicalDocumentTargetResolutionService(),
                        new TestClipboardService());

                    var availability = service.GetAvailability(
                        "cortex.editor.removeAndSortUsings",
                        new CortexShellState(),
                        new EditorCommandTarget
                        {
                            ContextId = EditorContextIds.Symbol,
                            DocumentKind = DocumentKind.DecompiledCode,
                            DefinitionDocumentPath = filePath,
                            DefinitionLine = 1,
                            DefinitionColumn = 1,
                            DefinitionStart = 0
                        });

                    Assert.True(availability.Visible);
                    Assert.True(availability.Enabled);
                    Assert.Equal(EditorCommandExecutionKind.PreviewApply, availability.ExecutionKind);
                }
                finally
                {
                    File.Delete(filePath);
                }
            });
        }

        [Fact]
        public void GetAvailability_RemoveAndSortUsings_HidesDecompilerContextsWithoutResolvedSource()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var service = new EditorCommandExecutionStrategyService(
                    new EditorLogicalDocumentTargetResolutionService(),
                    new TestClipboardService());

                var availability = service.GetAvailability(
                    "cortex.editor.removeAndSortUsings",
                    new CortexShellState(),
                    new EditorCommandTarget
                    {
                        ContextId = EditorContextIds.Symbol,
                        DocumentKind = DocumentKind.DecompiledCode
                    });

                Assert.False(availability.Visible);
                Assert.False(availability.Enabled);
                Assert.Equal(EditorCommandExecutionKind.Unavailable, availability.ExecutionKind);
            });
        }

        [Fact]
        public void GetAvailability_Cut_HidesDecompilerContexts()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var service = new EditorCommandExecutionStrategyService(
                    new EditorLogicalDocumentTargetResolutionService(),
                    new TestClipboardService("clipboard"));

                var availability = service.GetAvailability(
                    "cortex.editor.cut",
                    new CortexShellState(),
                    new EditorCommandTarget
                    {
                        DocumentKind = DocumentKind.DecompiledCode
                    });

                Assert.False(availability.Visible);
                Assert.False(availability.Enabled);
                Assert.Equal(EditorCommandExecutionKind.Unavailable, availability.ExecutionKind);
            });
        }

        [Fact]
        public void GetAvailability_Paste_UsesSourceRedirectForReadableSourceTabs()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var service = new EditorCommandExecutionStrategyService(
                    new EditorLogicalDocumentTargetResolutionService(),
                    new TestClipboardService("value"));

                var availability = service.GetAvailability(
                    "cortex.editor.paste",
                    new CortexShellState(),
                    new EditorCommandTarget
                    {
                        DocumentKind = DocumentKind.SourceCode,
                        DocumentPath = @"D:\workspace\Test.cs",
                        SupportsEditing = true,
                        IsEditModeEnabled = false,
                        CanToggleEditMode = true
                    });

                Assert.True(availability.Visible);
                Assert.True(availability.Enabled);
                Assert.Equal(EditorCommandExecutionKind.SourceRedirect, availability.ExecutionKind);
            });
        }

        private static string CreateTempFile(string text)
        {
            var filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".cs");
            File.WriteAllText(filePath, text);
            return filePath;
        }
    }
}
