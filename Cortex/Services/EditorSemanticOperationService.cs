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

            state.Semantic.Request.RequestedKey = (target.DocumentPath ?? string.Empty) + "|" + target.AbsolutePosition + "|" + requestKind + "|" + DateTime.UtcNow.Ticks;
            state.Semantic.Request.RequestedContextKey = target.ContextKey ?? string.Empty;
            state.Semantic.Request.RequestedKind = requestKind;
            state.Semantic.Request.RequestedDocumentPath = target.DocumentPath ?? string.Empty;
            state.Semantic.Request.RequestedLine = target.Line;
            state.Semantic.Request.RequestedColumn = target.Column;
            state.Semantic.Request.RequestedAbsolutePosition = target.AbsolutePosition;
            state.Semantic.Request.RequestedSymbolText = target.SymbolText ?? string.Empty;
            state.Semantic.Request.RequestedNewName = newName ?? string.Empty;
            state.Semantic.Request.RequestedCommandId = string.Empty;
            state.Semantic.Request.RequestedTitle = string.Empty;
            state.Semantic.Request.RequestedApplyLabel = string.Empty;
            state.Semantic.Request.RequestedOrganizeImports = false;
            state.Semantic.Request.RequestedSimplifyNames = false;
            state.Semantic.Request.RequestedFormatDocument = false;
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

            state.Semantic.Request.RequestedKey = (target.DocumentPath ?? string.Empty) + "|" + target.AbsolutePosition + "|" + SemanticRequestKind.DocumentTransformPreview + "|" + DateTime.UtcNow.Ticks;
            state.Semantic.Request.RequestedContextKey = target.ContextKey ?? string.Empty;
            state.Semantic.Request.RequestedKind = SemanticRequestKind.DocumentTransformPreview;
            state.Semantic.Request.RequestedDocumentPath = target.DocumentPath ?? string.Empty;
            state.Semantic.Request.RequestedLine = target.Line;
            state.Semantic.Request.RequestedColumn = target.Column;
            state.Semantic.Request.RequestedAbsolutePosition = target.AbsolutePosition;
            state.Semantic.Request.RequestedSymbolText = target.SymbolText ?? string.Empty;
            state.Semantic.Request.RequestedNewName = string.Empty;
            state.Semantic.Request.RequestedCommandId = commandId ?? string.Empty;
            state.Semantic.Request.RequestedTitle = title ?? string.Empty;
            state.Semantic.Request.RequestedApplyLabel = applyLabel ?? string.Empty;
            state.Semantic.Request.RequestedOrganizeImports = organizeImports;
            state.Semantic.Request.RequestedSimplifyNames = simplifyNames;
            state.Semantic.Request.RequestedFormatDocument = formatDocument;
        }

        public void OpenQuickActions(CortexShellState state, EditorCommandTarget target, EditorResolvedContextAction[] actions)
        {
            if (state == null || state.Semantic == null)
            {
                return;
            }

            state.Semantic.QuickActions.Visible = true;
            state.Semantic.QuickActions.Title = target != null && !string.IsNullOrEmpty(target.SymbolText)
                ? target.SymbolText
                : "Quick Actions";
            state.Semantic.QuickActions.FilterText = string.Empty;
            state.Semantic.QuickActions.SelectedIndex = actions != null && actions.Length > 0 ? 0 : -1;
            state.Semantic.QuickActions.ContextKey = target != null ? target.ContextKey ?? string.Empty : string.Empty;
            state.Semantic.QuickActions.Actions = actions ?? new EditorResolvedContextAction[0];
        }

        public void CloseQuickActions(CortexShellState state)
        {
            if (state == null || state.Semantic == null)
            {
                return;
            }

            state.Semantic.QuickActions.Visible = false;
            state.Semantic.QuickActions.Title = string.Empty;
            state.Semantic.QuickActions.FilterText = string.Empty;
            state.Semantic.QuickActions.SelectedIndex = -1;
            state.Semantic.QuickActions.ContextKey = string.Empty;
            state.Semantic.QuickActions.Actions = new EditorResolvedContextAction[0];
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
