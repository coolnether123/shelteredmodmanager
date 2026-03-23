using System;
using System.IO;
using Cortex.Core.Models;

namespace Cortex.Services
{
    internal sealed class EditorSemanticOperationService
    {
        public void QueueRequest(CortexShellState state, EditorCommandTarget target, SemanticRequestKind requestKind)
        {
            QueueRequest(state, target, requestKind, string.Empty);
        }

        public void QueueRequest(CortexShellState state, EditorCommandTarget target, SemanticRequestKind requestKind, string newName)
        {
            if (state == null || state.Semantic == null || target == null)
            {
                return;
            }

            state.Semantic.RequestedKey = (target.DocumentPath ?? string.Empty) + "|" + target.AbsolutePosition + "|" + requestKind + "|" + DateTime.UtcNow.Ticks;
            state.Semantic.RequestedKind = requestKind;
            state.Semantic.RequestedDocumentPath = target.DocumentPath ?? string.Empty;
            state.Semantic.RequestedLine = target.Line;
            state.Semantic.RequestedColumn = target.Column;
            state.Semantic.RequestedAbsolutePosition = target.AbsolutePosition;
            state.Semantic.RequestedSymbolText = target.SymbolText ?? string.Empty;
            state.Semantic.RequestedNewName = newName ?? string.Empty;
            state.Semantic.RequestedCommandId = string.Empty;
            state.Semantic.RequestedTitle = string.Empty;
            state.Semantic.RequestedApplyLabel = string.Empty;
            state.Semantic.RequestedOrganizeImports = false;
            state.Semantic.RequestedSimplifyNames = false;
            state.Semantic.RequestedFormatDocument = false;
        }

        public void QueueDocumentTransformRequest(
            CortexShellState state,
            EditorCommandTarget target,
            string commandId,
            string title,
            string applyLabel,
            bool organizeImports,
            bool simplifyNames,
            bool formatDocument)
        {
            if (state == null || state.Semantic == null || target == null)
            {
                return;
            }

            state.Semantic.RequestedKey = (target.DocumentPath ?? string.Empty) + "|" + target.AbsolutePosition + "|" + SemanticRequestKind.DocumentTransformPreview + "|" + DateTime.UtcNow.Ticks;
            state.Semantic.RequestedKind = SemanticRequestKind.DocumentTransformPreview;
            state.Semantic.RequestedDocumentPath = target.DocumentPath ?? string.Empty;
            state.Semantic.RequestedLine = target.Line;
            state.Semantic.RequestedColumn = target.Column;
            state.Semantic.RequestedAbsolutePosition = target.AbsolutePosition;
            state.Semantic.RequestedSymbolText = target.SymbolText ?? string.Empty;
            state.Semantic.RequestedNewName = string.Empty;
            state.Semantic.RequestedCommandId = commandId ?? string.Empty;
            state.Semantic.RequestedTitle = title ?? string.Empty;
            state.Semantic.RequestedApplyLabel = applyLabel ?? string.Empty;
            state.Semantic.RequestedOrganizeImports = organizeImports;
            state.Semantic.RequestedSimplifyNames = simplifyNames;
            state.Semantic.RequestedFormatDocument = formatDocument;
        }

        public void OpenQuickActions(CortexShellState state, EditorCommandTarget target, EditorResolvedContextAction[] actions)
        {
            if (state == null || state.Semantic == null)
            {
                return;
            }

            state.Semantic.QuickActionsVisible = true;
            state.Semantic.QuickActionsTitle = target != null && !string.IsNullOrEmpty(target.SymbolText)
                ? target.SymbolText
                : "Quick Actions";
            state.Semantic.QuickActionsFilterText = string.Empty;
            state.Semantic.QuickActionsSelectedIndex = actions != null && actions.Length > 0 ? 0 : -1;
            state.Semantic.QuickActionsTarget = target;
            state.Semantic.QuickActions = actions ?? new EditorResolvedContextAction[0];
        }

        public void CloseQuickActions(CortexShellState state)
        {
            if (state == null || state.Semantic == null)
            {
                return;
            }

            state.Semantic.QuickActionsVisible = false;
            state.Semantic.QuickActionsTitle = string.Empty;
            state.Semantic.QuickActionsFilterText = string.Empty;
            state.Semantic.QuickActionsSelectedIndex = -1;
            state.Semantic.QuickActionsTarget = null;
            state.Semantic.QuickActions = new EditorResolvedContextAction[0];
        }

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

            state.Semantic.DocumentEditPreview = previewPlan;
            state.Semantic.ActiveView = previewPlan != null
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
