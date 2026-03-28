using Cortex.Roslyn.Worker;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Completion;
using Xunit;

namespace Cortex.Roslyn.Worker.Tests
{
    public sealed class WorkerCacheBehaviorTests
    {
        [Fact]
        public void DocumentTextFingerprint_FromString_ProducesStableValue()
        {
            var left = DocumentTextFingerprint.From("alpha beta");
            var right = DocumentTextFingerprint.From("alpha beta");
            var different = DocumentTextFingerprint.From("alpha gamma");

            Assert.True(left.HasValue);
            Assert.Equal(left, right);
            Assert.NotEqual(left, different);
        }

        [Fact]
        public void CompletionInsertTextBuilder_BuildFastInsertText_CombinesPrefixDisplayAndSuffix()
        {
            var item = CompletionItem.Create(
                displayText: "WriteLine",
                displayTextPrefix: "Console.",
                displayTextSuffix: "()",
                filterText: "ignored");

            var insertText = CompletionInsertTextBuilder.BuildFastInsertText(item);

            Assert.Equal("Console.WriteLine()", insertText);
        }

        [Fact]
        public void CompletionInsertTextBuilder_BuildFastInsertText_FallsBackToFilterText()
        {
            var item = CompletionItem.Create(displayText: string.Empty, filterText: "fallback");

            var insertText = CompletionInsertTextBuilder.BuildFastInsertText(item);

            Assert.Equal("fallback", insertText);
        }

        [Fact]
        public void GetNavigationMetadataSymbol_ForMetadataNamespace_ResolvesRepresentativeType()
        {
            var syntaxTree = CSharpSyntaxTree.ParseText("using System;\ninternal sealed class Demo { }\n");
            var compilation = CSharpCompilation.Create(
                "NavigationMetadata",
                new[] { syntaxTree },
                new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var usingDirective = syntaxTree.GetRoot().DescendantNodes().OfType<UsingDirectiveSyntax>().Single();
            var namespaceSymbol = semanticModel.GetSymbolInfo(usingDirective.Name).Symbol;

            var navigationSymbol = RoslynLanguageServiceServer.GetNavigationMetadataSymbol(namespaceSymbol);

            Assert.NotNull(namespaceSymbol);
            Assert.Equal(SymbolKind.Namespace, namespaceSymbol.Kind);
            Assert.NotNull(navigationSymbol);
            Assert.NotEqual(SymbolKind.Namespace, navigationSymbol.Kind);
            Assert.False(string.IsNullOrEmpty(navigationSymbol.MetadataName));
            Assert.NotNull(navigationSymbol.ContainingAssembly);
        }
    }
}
