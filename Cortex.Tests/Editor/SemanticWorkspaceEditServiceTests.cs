using System;
using System.IO;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Services;
using Cortex.Tests.Testing;
using Xunit;

namespace Cortex.Tests.Editor
{
    public sealed class SemanticWorkspaceEditServiceTests
    {
        [Fact]
        public void ApplyDocumentEditPreview_WritesClosedFiles()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var filePath = CreateTempFile("class C { }");
                try
                {
                    var service = new SemanticWorkspaceEditService();
                    string statusMessage;

                    var applied = service.ApplyDocumentEditPreview(
                        new CortexShellState(),
                        null,
                        new DocumentEditPreviewPlan
                        {
                            CanApply = true,
                            Documents = new[]
                            {
                                new LanguageServiceDocumentChange
                                {
                                    DocumentPath = filePath,
                                    Edits = new[]
                                    {
                                        new LanguageServiceTextEdit
                                        {
                                            Range = new LanguageServiceRange
                                            {
                                                Start = 0,
                                                Length = "class C { }".Length
                                            },
                                            NewText = "class D { }"
                                        }
                                    }
                                }
                            }
                        },
                        out statusMessage);

                    Assert.True(applied);
                    Assert.Equal("class D { }", File.ReadAllText(filePath));
                    Assert.Equal("Applied preview changes across 1 document(s) and 1 edit(s).", statusMessage);
                }
                finally
                {
                    File.Delete(filePath);
                }
            });
        }

        [Fact]
        public void ApplyDocumentEditPreview_UpdatesOpenSessionsAndSavesWritableSourceDocuments()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var filePath = CreateTempFile("class C { }");
                try
                {
                    var state = new CortexShellState();
                    var session = new DocumentSession
                    {
                        FilePath = filePath,
                        Kind = DocumentKind.SourceCode,
                        IsReadOnly = false,
                        Text = "class C { }",
                        OriginalTextSnapshot = "class C { }"
                    };
                    state.Documents.OpenDocuments.Add(session);
                    var documentService = new TestDocumentService();
                    var service = new SemanticWorkspaceEditService();
                    string statusMessage;

                    var applied = service.ApplyDocumentEditPreview(
                        state,
                        documentService,
                        new DocumentEditPreviewPlan
                        {
                            CanApply = true,
                            Documents = new[]
                            {
                                new LanguageServiceDocumentChange
                                {
                                    DocumentPath = filePath,
                                    Edits = new[]
                                    {
                                        new LanguageServiceTextEdit
                                        {
                                            Range = new LanguageServiceRange
                                            {
                                                Start = 6,
                                                Length = 1
                                            },
                                            NewText = "D"
                                        }
                                    }
                                }
                            }
                        },
                        out statusMessage);

                    Assert.True(applied);
                    Assert.Equal("class D { }", session.Text);
                    Assert.Equal("class D { }", File.ReadAllText(filePath));
                    Assert.Equal(1, documentService.SaveCallCount);
                    Assert.Equal("Applied preview changes across 1 document(s) and 1 edit(s).", statusMessage);
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
