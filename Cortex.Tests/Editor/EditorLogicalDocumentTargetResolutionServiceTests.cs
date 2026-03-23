using System;
using System.IO;
using Cortex.Core.Models;
using Cortex.Services;
using Cortex.Tests.Testing;
using Xunit;

namespace Cortex.Tests.Editor
{
    public sealed class EditorLogicalDocumentTargetResolutionServiceTests
    {
        [Fact]
        public void TryResolveSourceDocument_UsesDefinitionPathForDecompilerSymbols()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var filePath = CreateTempFile("class C { }");
                try
                {
                    var service = new EditorLogicalDocumentTargetResolutionService();
                    EditorLogicalDocumentTarget resolvedTarget;
                    string reason;

                    var resolved = service.TryResolveSourceDocument(
                        new CortexShellState(),
                        new EditorCommandTarget
                        {
                            ContextId = EditorContextIds.Symbol,
                            DocumentKind = DocumentKind.DecompiledCode,
                            DefinitionDocumentPath = filePath,
                            DefinitionLine = 4,
                            DefinitionColumn = 6,
                            DefinitionStart = 12
                        },
                        out resolvedTarget,
                        out reason);

                    Assert.True(resolved);
                    Assert.NotNull(resolvedTarget);
                    Assert.Equal(Path.GetFullPath(filePath), resolvedTarget.DocumentPath);
                    Assert.Equal(4, resolvedTarget.Line);
                    Assert.Equal(6, resolvedTarget.Column);
                    Assert.Equal(12, resolvedTarget.AbsolutePosition);
                    Assert.True(resolvedTarget.CanApplyEdits);
                    Assert.Equal(string.Empty, reason);
                }
                finally
                {
                    File.Delete(filePath);
                }
            });
        }

        [Fact]
        public void TryResolveSourceDocument_RejectsDecompilerCacheTargets()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
                var cacheRoot = Path.Combine(rootPath, "cortex_cache");
                Directory.CreateDirectory(cacheRoot);
                var filePath = Path.Combine(cacheRoot, "Decompiled.cs");
                File.WriteAllText(filePath, "class C { }");

                try
                {
                    var state = new CortexShellState
                    {
                        Settings = new CortexSettings
                        {
                            DecompilerCachePath = cacheRoot
                        }
                    };
                    var service = new EditorLogicalDocumentTargetResolutionService();
                    EditorLogicalDocumentTarget resolvedTarget;
                    string reason;

                    var resolved = service.TryResolveSourceDocument(
                        state,
                        new EditorCommandTarget
                        {
                            ContextId = EditorContextIds.Symbol,
                            DocumentKind = DocumentKind.DecompiledCode,
                            DefinitionDocumentPath = filePath
                        },
                        out resolvedTarget,
                        out reason);

                    Assert.False(resolved);
                    Assert.Null(resolvedTarget);
                    Assert.Equal("The current context does not resolve to an editable source document.", reason);
                }
                finally
                {
                    Directory.Delete(rootPath, true);
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
