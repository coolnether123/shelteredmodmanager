using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;

namespace Cortex.Services.Semantics.Analysis
{
    internal static class DocumentLanguageAnalysisCloneUtility
    {
        public static LanguageServiceDiagnostic[] CloneDiagnostics(LanguageServiceDiagnostic[] diagnostics)
        {
            if (diagnostics == null || diagnostics.Length == 0)
            {
                return new LanguageServiceDiagnostic[0];
            }

            var clone = new LanguageServiceDiagnostic[diagnostics.Length];
            for (var i = 0; i < diagnostics.Length; i++)
            {
                clone[i] = diagnostics[i] == null ? null : new LanguageServiceDiagnostic
                {
                    Id = diagnostics[i].Id,
                    Severity = diagnostics[i].Severity,
                    Message = diagnostics[i].Message,
                    Category = diagnostics[i].Category,
                    FilePath = diagnostics[i].FilePath,
                    Line = diagnostics[i].Line,
                    Column = diagnostics[i].Column,
                    EndLine = diagnostics[i].EndLine,
                    EndColumn = diagnostics[i].EndColumn
                };
            }

            return clone;
        }

        public static LanguageServiceClassifiedSpan[] CloneClassifications(LanguageServiceClassifiedSpan[] classifications)
        {
            if (classifications == null || classifications.Length == 0)
            {
                return new LanguageServiceClassifiedSpan[0];
            }

            var clone = new LanguageServiceClassifiedSpan[classifications.Length];
            for (var i = 0; i < classifications.Length; i++)
            {
                clone[i] = CloneClassification(classifications[i]);
            }

            return clone;
        }

        public static LanguageServiceClassifiedSpan CloneClassification(LanguageServiceClassifiedSpan classification)
        {
            return classification == null ? null : new LanguageServiceClassifiedSpan
            {
                Classification = classification.Classification,
                SemanticTokenType = classification.SemanticTokenType,
                Start = classification.Start,
                Length = classification.Length,
                Line = classification.Line,
                Column = classification.Column
            };
        }

        public static LanguageServiceAnalysisResponse CloneLanguageAnalysis(LanguageServiceAnalysisResponse response)
        {
            if (response == null)
            {
                return new LanguageServiceAnalysisResponse();
            }

            return new LanguageServiceAnalysisResponse
            {
                Success = response.Success,
                StatusMessage = response.StatusMessage ?? string.Empty,
                DocumentPath = response.DocumentPath ?? string.Empty,
                ProjectFilePath = response.ProjectFilePath ?? string.Empty,
                DocumentVersion = response.DocumentVersion,
                Diagnostics = CloneDiagnostics(response.Diagnostics),
                Classifications = CloneClassifications(response.Classifications)
            };
        }
    }
}
