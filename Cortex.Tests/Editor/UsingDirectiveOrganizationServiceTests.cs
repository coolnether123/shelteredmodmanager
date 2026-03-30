using System;
using System.IO;
using Cortex.Core.Models;
using Cortex.Tests.Testing;
using Xunit;
using Cortex.Services.Editor.Commands;

namespace Cortex.Tests.Editor
{
    public sealed class UsingDirectiveOrganizationServiceTests
    {
        [Fact]
        public void TryBuildPreviewPlan_SortsAndDeduplicatesTopLevelUsings()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var filePath = CreateTempFile(
                    "// comment\r\n" +
                    "using Zeta;\r\n" +
                    "using Alpha;\r\n" +
                    "using Alpha;\r\n" +
                    "\r\n" +
                    "class C { }\r\n");

                try
                {
                    var service = new UsingDirectiveOrganizationService();
                    DocumentEditPreviewPlan previewPlan;
                    string updatedText;
                    string statusMessage;

                    var built = service.TryBuildPreviewPlan(
                        new CortexShellState(),
                        filePath,
                        out previewPlan,
                        out updatedText,
                        out statusMessage);

                    Assert.True(built);
                    Assert.NotNull(previewPlan);
                    Assert.Equal(
                        "// comment\r\nusing Alpha;\r\nusing Zeta;\r\n\r\nclass C { }\r\n",
                        updatedText);
                    Assert.Single(previewPlan.Documents);
                    Assert.Equal("cortex.editor.removeAndSortUsings", previewPlan.CommandId);
                    Assert.Contains(Path.GetFileName(filePath), statusMessage);
                }
                finally
                {
                    File.Delete(filePath);
                }
            });
        }

        [Fact]
        public void TryBuildPreviewPlan_ReturnsFalseWhenUsingsAreAlreadyOrganized()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var filePath = CreateTempFile(
                    "using Alpha;\r\n" +
                    "using Zeta;\r\n" +
                    "\r\n" +
                    "class C { }\r\n");

                try
                {
                    var service = new UsingDirectiveOrganizationService();
                    DocumentEditPreviewPlan previewPlan;
                    string updatedText;
                    string statusMessage;

                    var built = service.TryBuildPreviewPlan(
                        new CortexShellState(),
                        filePath,
                        out previewPlan,
                        out updatedText,
                        out statusMessage);

                    Assert.False(built);
                    Assert.Null(previewPlan);
                    Assert.Equal(string.Empty, updatedText);
                    Assert.Equal("Using directives are already organized.", statusMessage);
                }
                finally
                {
                    File.Delete(filePath);
                }
            });
        }

        [Fact]
        public void TryBuildPreviewPlan_PrefersOpenSessionTextOverDiskContents()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var filePath = CreateTempFile(
                    "using Zeta;\r\n" +
                    "using Alpha;\r\n" +
                    "\r\n" +
                    "class C { }\r\n");

                try
                {
                    var state = new CortexShellState();
                    state.Documents.OpenDocuments.Add(new DocumentSession
                    {
                        FilePath = filePath,
                        Kind = DocumentKind.SourceCode,
                        IsReadOnly = false,
                        Text = "using Alpha;\r\n\r\nclass C { }\r\n",
                        OriginalTextSnapshot = "using Alpha;\r\n\r\nclass C { }\r\n"
                    });

                    var service = new UsingDirectiveOrganizationService();
                    DocumentEditPreviewPlan previewPlan;
                    string updatedText;
                    string statusMessage;

                    var built = service.TryBuildPreviewPlan(
                        state,
                        filePath,
                        out previewPlan,
                        out updatedText,
                        out statusMessage);

                    Assert.False(built);
                    Assert.Null(previewPlan);
                    Assert.Equal(string.Empty, updatedText);
                    Assert.Equal("Using directives are already organized.", statusMessage);
                }
                finally
                {
                    File.Delete(filePath);
                }
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
