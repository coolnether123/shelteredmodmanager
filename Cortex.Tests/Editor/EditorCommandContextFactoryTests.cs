using System;
using System.IO;
using Cortex.Core.Models;
using Cortex.Tests.Testing;
using Xunit;
using Cortex.Services.Editor.Context;

namespace Cortex.Tests.Editor
{
    public sealed class EditorCommandContextFactoryTests
    {
        [Fact]
        public void CreateForTarget_UsesTargetDocumentAsActiveDocument()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var state = new CortexShellState();
                state.Workbench.FocusedContainerId = "editor";
                state.Documents.ActiveDocumentPath = @"D:\other\Active.cs";

                var factory = new EditorCommandContextFactory();
                var invocation = factory.CreateForTarget(
                    state,
                    new EditorCommandTarget
                    {
                        DocumentPath = @"D:\target\Selected.cs"
                    });

                Assert.NotNull(invocation);
                Assert.Equal("editor", invocation.ActiveContainerId);
                Assert.Equal(@"D:\target\Selected.cs", invocation.ActiveDocumentId);
                Assert.Equal("editor", invocation.FocusedRegionId);
            });
        }

        [Fact]
        public void CreateDocumentInvocation_CapturesSelectionAndEditModeState()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var session = new DocumentSession
                {
                    FilePath = @"D:\workspace\Sample.cs",
                    Kind = DocumentKind.SourceCode,
                    IsReadOnly = false,
                    Text = "hello world"
                };
                session.EditorState.SelectionAnchorIndex = 0;
                session.EditorState.CaretIndex = 5;
                var state = new CortexShellState
                {
                    Settings = new CortexSettings
                    {
                        EnableFileEditing = true
                    }
                };

                var factory = new EditorCommandContextFactory(
                    new TestEditorService(),
                    new EditorSymbolInteractionService(),
                    new EditorDocumentModeService());
                var invocation = factory.CreateDocumentInvocation(session, state, false, 5);

                Assert.NotNull(invocation);
                Assert.NotNull(invocation.Target);
                Assert.Equal(EditorContextIds.Document, invocation.Target.ContextId);
                Assert.Equal(@"D:\workspace\Sample.cs", invocation.ActiveDocumentId);
                Assert.True(invocation.Target.SupportsEditing);
                Assert.False(invocation.Target.IsEditModeEnabled);
                Assert.True(invocation.Target.CanToggleEditMode);
                Assert.True(invocation.Target.HasSelection);
                Assert.Equal(0, invocation.Target.SelectionStart);
                Assert.Equal(5, invocation.Target.SelectionEnd);
                Assert.Equal("hello", invocation.Target.SelectionText);
                Assert.Equal(5, invocation.Target.CaretIndex);
            });
        }

        [Fact]
        public void TryCreateTokenInvocation_UsesSharedSessionAndHoverMetadata()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var filePath = CreateTempFile("class Sample { }");
                try
                {
                    var session = new DocumentSession
                    {
                        FilePath = filePath,
                        Kind = DocumentKind.SourceCode,
                        IsReadOnly = false,
                        Text = "class Sample { }"
                    };
                    var state = new CortexShellState
                    {
                        Settings = new CortexSettings
                        {
                            EnableFileEditing = true
                        }
                    };
                    session.EditorState.EditModeEnabled = true;
                    var hoverResponse = new Cortex.LanguageService.Protocol.LanguageServiceHoverResponse
                    {
                        Success = true,
                        DefinitionDocumentPath = filePath,
                        DefinitionRange = new Cortex.LanguageService.Protocol.LanguageServiceRange
                        {
                            Start = 6,
                            Length = 6,
                            StartLine = 1,
                            StartColumn = 7
                        }
                    };

                    var factory = new EditorCommandContextFactory();
                    EditorCommandInvocation invocation;
                    var created = factory.TryCreateTokenInvocation(
                        session,
                        state,
                        6,
                        1,
                        7,
                        "Sample",
                        hoverResponse,
                        true,
                        out invocation);

                    Assert.True(created);
                    Assert.NotNull(invocation);
                    Assert.NotNull(invocation.Target);
                    Assert.Equal("Sample", invocation.Target.SymbolText);
                    Assert.Equal(filePath, invocation.Target.DefinitionDocumentPath);
                    Assert.True(invocation.Target.IsEditModeEnabled);
                    Assert.True(invocation.Target.CanToggleEditMode);
                    Assert.True(invocation.Target.CanGoToDefinition);
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
