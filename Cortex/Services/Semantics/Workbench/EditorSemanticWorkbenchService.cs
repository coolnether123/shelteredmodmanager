using System;
using System.IO;
using Cortex.Core.Models;

namespace Cortex.Services.Semantics.Workbench
{
    internal sealed class EditorSemanticWorkbenchService : IEditorSemanticWorkbenchService
    {
        public UnitTestGenerationPlan BuildUnitTestPlan(CortexShellState state, EditorCommandTarget target)
        {
            if (state == null || target == null || string.IsNullOrEmpty(target.SymbolText))
            {
                return new UnitTestGenerationPlan
                {
                    StatusMessage = "Select a symbol before generating unit test scaffolding.",
                    CanApply = false
                };
            }

            var sourcePath = target.DocumentPath ?? string.Empty;
            var sourceDirectory = !string.IsNullOrEmpty(sourcePath) ? Path.GetDirectoryName(sourcePath) ?? string.Empty : string.Empty;
            var sourceFileName = !string.IsNullOrEmpty(sourcePath) ? Path.GetFileNameWithoutExtension(sourcePath) ?? "Generated" : "Generated";
            var projectRoot = state.SelectedProject != null ? state.SelectedProject.SourceRootPath ?? string.Empty : string.Empty;
            var testDirectory = Directory.Exists(projectRoot)
                ? Path.Combine(projectRoot, "Tests")
                : sourceDirectory;
            var testClassName = SanitizeIdentifier(sourceFileName) + "Tests";
            var methodName = SanitizeIdentifier(target.SymbolText) + "_scaffold";
            var outputFilePath = Path.Combine(testDirectory, testClassName + ".cs");
            var generatedText =
"namespace GeneratedTests\r\n" +
"{\r\n" +
"    public sealed class " + testClassName + "\r\n" +
"    {\r\n" +
"        public void " + methodName + "()\r\n" +
"        {\r\n" +
"            // Add your preferred test framework attribute here.\r\n" +
"            // TODO: Arrange\r\n" +
"            // TODO: Act\r\n" +
"            // TODO: Assert\r\n" +
"        }\r\n" +
"    }\r\n" +
"}\r\n";

            return new UnitTestGenerationPlan
            {
                SymbolDisplay = target.SymbolText ?? string.Empty,
                SymbolName = target.SymbolText ?? string.Empty,
                SymbolKind = string.Empty,
                TestProjectPath = testDirectory,
                OutputFilePath = outputFilePath,
                GeneratedText = generatedText,
                StatusMessage = "Generated unit test scaffold preview for " + (target.SymbolText ?? string.Empty) + ".",
                CanApply = !string.IsNullOrEmpty(outputFilePath)
            };
        }

        public void OpenDocumentEditPreview(CortexShellState state, DocumentEditPreviewPlan previewPlan)
        {
            if (state == null || state.Semantic == null)
            {
                return;
            }

            state.Semantic.Workbench.DocumentEditPreview = previewPlan;
            state.Semantic.Workbench.ActiveView = previewPlan != null
                ? SemanticWorkbenchViewKind.DocumentEditPreview
                : SemanticWorkbenchViewKind.None;
        }

        private static string SanitizeIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "Generated";
            }

            var characters = value.ToCharArray();
            for (var i = 0; i < characters.Length; i++)
            {
                if (!char.IsLetterOrDigit(characters[i]) && characters[i] != '_')
                {
                    characters[i] = '_';
                }
            }

            var result = new string(characters).Trim('_');
            if (string.IsNullOrEmpty(result))
            {
                result = "Generated";
            }

            if (!char.IsLetter(result[0]) && result[0] != '_')
            {
                result = "_" + result;
            }

            return result;
        }
    }
}
