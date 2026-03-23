using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cortex.LanguageService.Protocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.QuickInfo;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.Text;

namespace Cortex.Roslyn.Worker
{
    internal sealed partial class RoslynLanguageServiceServer
    {
        private static readonly string[] OrganizeImportsTypeNames =
        {
            "Microsoft.CodeAnalysis.OrganizeImports.OrganizeImportsService",
            "Microsoft.CodeAnalysis.RemoveUnnecessaryImports.RemoveUnnecessaryImportsService"
        };

        private static readonly string[] QuickInfoSignatureSectionKinds =
        {
            "Description",
            "TypeParameters",
            "AnonymousTypes",
            "Usage"
        };

        private static readonly string[] QuickInfoDocumentationSectionKinds =
        {
            "DocumentationComments",
            "RemarksDocumentationComments",
            "ReturnsDocumentationComments",
            "ValueDocumentationComments",
            "Exceptions"
        };

        private LanguageServiceHoverResponse BuildHoverResponse(
            DocumentContext documentContext,
            LanguageServiceHoverRequest request,
            SourceText text,
            int position,
            ISymbol symbol)
        {
            var quickInfoItem = TryGetQuickInfo(documentContext != null ? documentContext.Document : null, position);
            if (symbol == null && quickInfoItem == null)
            {
                return new LanguageServiceHoverResponse
                {
                    Success = false,
                    StatusMessage = "No Roslyn hover details were found at that position.",
                    DocumentPath = documentContext != null && documentContext.Document != null
                        ? documentContext.Document.FilePath ?? request.DocumentPath ?? string.Empty
                        : request.DocumentPath ?? string.Empty,
                    ProjectFilePath = documentContext != null ? documentContext.ProjectPath ?? string.Empty : request.ProjectFilePath ?? string.Empty,
                    DocumentVersion = request != null ? request.DocumentVersion : 0,
                    DisplayParts = new LanguageServiceHoverDisplayPart[0]
                };
            }

            var syntaxTree = documentContext != null && documentContext.Document != null
                ? documentContext.Document.GetSyntaxTreeAsync().Result
                : null;
            var documentationXml = symbol != null ? symbol.GetDocumentationCommentXml() : string.Empty;
            var sourceLocation = symbol != null
                ? symbol.Locations.FirstOrDefault(location => location.IsInSource && location.SourceTree == syntaxTree)
                : null;
            var definitionLocation = symbol != null ? symbol.Locations.FirstOrDefault(location => location.IsInSource) : null;
            var definitionText = definitionLocation != null && definitionLocation.SourceTree != null
                ? definitionLocation.SourceTree.GetText()
                : null;
            var quickInfoDisplay = BuildQuickInfoMainDisplay(quickInfoItem);
            var displayParts = BuildQuickInfoDisplayParts(quickInfoItem, symbol);
            if (displayParts.Length == 0 && symbol != null)
            {
                displayParts = BuildHoverDisplayParts(symbol);
            }

            return new LanguageServiceHoverResponse
            {
                Success = true,
                StatusMessage = "Hover info resolved.",
                DocumentPath = documentContext != null && documentContext.Document != null
                    ? documentContext.Document.FilePath ?? request.DocumentPath ?? string.Empty
                    : request.DocumentPath ?? string.Empty,
                ProjectFilePath = documentContext != null ? documentContext.ProjectPath ?? string.Empty : request.ProjectFilePath ?? string.Empty,
                DocumentVersion = request != null ? request.DocumentVersion : 0,
                SymbolDisplay = !string.IsNullOrEmpty(quickInfoDisplay)
                    ? quickInfoDisplay
                    : symbol != null
                        ? symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)
                        : string.Empty,
                QualifiedSymbolDisplay = symbol != null ? GetQualifiedSymbolDisplay(symbol) : string.Empty,
                SymbolKind = symbol != null ? symbol.Kind.ToString() : string.Empty,
                MetadataName = symbol != null ? symbol.MetadataName ?? string.Empty : string.Empty,
                ContainingTypeName = symbol != null ? GetContainingTypeName(symbol) : string.Empty,
                ContainingAssemblyName = symbol != null && symbol.ContainingAssembly != null ? symbol.ContainingAssembly.Identity.Name : string.Empty,
                DocumentationCommentId = symbol != null ? symbol.GetDocumentationCommentId() ?? string.Empty : string.Empty,
                DocumentationXml = documentationXml ?? string.Empty,
                DocumentationText = BuildQuickInfoDocumentationText(quickInfoItem, FlattenDocumentation(documentationXml)),
                Range = BuildRange(text, quickInfoItem != null && quickInfoItem.Span != default(TextSpan)
                    ? quickInfoItem.Span
                    : sourceLocation != null ? sourceLocation.SourceSpan : default(TextSpan)),
                DefinitionDocumentPath = definitionLocation != null && definitionLocation.SourceTree != null
                    ? definitionLocation.SourceTree.FilePath ?? string.Empty
                    : string.Empty,
                DefinitionRange = BuildRange(definitionText, definitionLocation != null ? definitionLocation.SourceSpan : default(TextSpan)),
                DisplayParts = displayParts
            };
        }

        private static QuickInfoItem TryGetQuickInfo(Document document, int position)
        {
            try
            {
                var quickInfoService = document != null ? QuickInfoService.GetService(document) : null;
                return quickInfoService != null
                    ? quickInfoService.GetQuickInfoAsync(document, position, CancellationToken.None).Result
                    : null;
            }
            catch
            {
                return null;
            }
        }

        private static string BuildQuickInfoMainDisplay(QuickInfoItem quickInfoItem)
        {
            if (quickInfoItem == null || quickInfoItem.Sections.IsDefaultOrEmpty)
            {
                return string.Empty;
            }

            for (var i = 0; i < quickInfoItem.Sections.Length; i++)
            {
                var section = quickInfoItem.Sections[i];
                if (section == null || !IsQuickInfoSignatureSection(section.Kind))
                {
                    continue;
                }

                var text = FlattenTaggedParts(section.TaggedParts);
                if (!string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }

            return string.Empty;
        }

        private static string BuildQuickInfoDocumentationText(QuickInfoItem quickInfoItem, string fallbackDocumentation)
        {
            if (quickInfoItem == null || quickInfoItem.Sections.IsDefaultOrEmpty)
            {
                return fallbackDocumentation ?? string.Empty;
            }

            var sections = new List<string>();
            for (var i = 0; i < quickInfoItem.Sections.Length; i++)
            {
                var section = quickInfoItem.Sections[i];
                if (section == null)
                {
                    continue;
                }

                if (!IsQuickInfoDocumentationSection(section.Kind) && IsQuickInfoSignatureSection(section.Kind))
                {
                    continue;
                }

                var text = FlattenTaggedParts(section.TaggedParts);
                if (!string.IsNullOrEmpty(text))
                {
                    sections.Add(text);
                }
            }

            return sections.Count > 0
                ? string.Join(Environment.NewLine, sections.ToArray())
                : fallbackDocumentation ?? string.Empty;
        }

        private static LanguageServiceHoverDisplayPart[] BuildQuickInfoDisplayParts(QuickInfoItem quickInfoItem, ISymbol fallbackSymbol)
        {
            if (quickInfoItem == null || quickInfoItem.Sections.IsDefaultOrEmpty)
            {
                return new LanguageServiceHoverDisplayPart[0];
            }

            var results = new List<LanguageServiceHoverDisplayPart>();
            for (var i = 0; i < quickInfoItem.Sections.Length; i++)
            {
                var section = quickInfoItem.Sections[i];
                if (section == null || !IsQuickInfoSignatureSection(section.Kind) || section.TaggedParts.IsDefaultOrEmpty)
                {
                    continue;
                }

                for (var partIndex = 0; partIndex < section.TaggedParts.Length; partIndex++)
                {
                    var taggedPart = section.TaggedParts[partIndex];
                    var part = new LanguageServiceHoverDisplayPart
                    {
                        Text = taggedPart.Text ?? string.Empty,
                        Classification = taggedPart.Tag ?? string.Empty,
                        IsInteractive = fallbackSymbol != null && IsInteractiveTaggedPart(taggedPart.Tag)
                    };
                    if (part.IsInteractive)
                    {
                        PopulateHoverDisplayPart(part, fallbackSymbol);
                    }

                    results.Add(part);
                }
            }

            return results.ToArray();
        }

        private static bool IsInteractiveTaggedPart(string tag)
        {
            var normalized = tag ?? string.Empty;
            return normalized.IndexOf("class", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("struct", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("interface", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("enum", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("delegate", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("method", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("property", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("event", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("field", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("namespace", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsQuickInfoSignatureSection(string kind)
        {
            for (var i = 0; i < QuickInfoSignatureSectionKinds.Length; i++)
            {
                if (string.Equals(QuickInfoSignatureSectionKinds[i], kind ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsQuickInfoDocumentationSection(string kind)
        {
            for (var i = 0; i < QuickInfoDocumentationSectionKinds.Length; i++)
            {
                if (string.Equals(QuickInfoDocumentationSectionKinds[i], kind ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string FlattenTaggedParts(IEnumerable<TaggedText> taggedParts)
        {
            return taggedParts == null
                ? string.Empty
                : string.Concat(taggedParts.Select(part => part.Text ?? string.Empty).ToArray()).Trim();
        }

        private LanguageServiceDocumentTransformResponse PreviewDocumentTransform(LanguageServiceDocumentTransformRequest request)
        {
            var documentContext = ResolveDocument(
                request != null ? request.DocumentPath : string.Empty,
                request != null ? request.ProjectFilePath : string.Empty,
                request != null ? request.WorkspaceRootPath : string.Empty,
                request != null ? request.SourceRoots : null,
                request != null ? request.DocumentText : string.Empty,
                request != null ? request.DocumentVersion : 0);

            if (documentContext.Document == null)
            {
                return new LanguageServiceDocumentTransformResponse
                {
                    Success = false,
                    StatusMessage = "Roslyn could not resolve a document for cleanup preview.",
                    CommandId = request != null ? request.CommandId ?? string.Empty : string.Empty,
                    Title = request != null ? request.Title ?? string.Empty : string.Empty,
                    ApplyLabel = request != null ? request.ApplyLabel ?? string.Empty : string.Empty,
                    DocumentPath = request != null ? request.DocumentPath ?? string.Empty : string.Empty,
                    ProjectFilePath = request != null ? request.ProjectFilePath ?? string.Empty : string.Empty,
                    DocumentVersion = request != null ? request.DocumentVersion : 0,
                    CanApply = false,
                    Documents = new LanguageServiceDocumentChange[0]
                };
            }

            var originalDocument = documentContext.Document;
            var transformedDocument = originalDocument;
            if (request != null && request.OrganizeImports)
            {
                transformedDocument = TryOrganizeImports(transformedDocument);
            }

            if (request != null && request.SimplifyNames)
            {
                transformedDocument = TryReduceDocument(transformedDocument);
            }

            if (request != null && request.FormatDocument)
            {
                transformedDocument = TryFormatDocument(transformedDocument);
            }

            var changes = BuildDocumentChangesFromSolutionDiff(
                originalDocument != null ? originalDocument.Project.Solution : null,
                transformedDocument != null ? transformedDocument.Project.Solution : null);

            return new LanguageServiceDocumentTransformResponse
            {
                Success = true,
                StatusMessage = changes.Length > 0
                    ? "Document cleanup preview resolved."
                    : "No Roslyn cleanup changes were needed.",
                CommandId = request != null ? request.CommandId ?? string.Empty : string.Empty,
                Title = request != null ? request.Title ?? string.Empty : string.Empty,
                ApplyLabel = request != null ? request.ApplyLabel ?? string.Empty : string.Empty,
                DocumentPath = originalDocument != null ? originalDocument.FilePath ?? string.Empty : string.Empty,
                ProjectFilePath = documentContext.ProjectPath ?? string.Empty,
                DocumentVersion = request != null ? request.DocumentVersion : 0,
                CanApply = changes.Length > 0,
                Documents = changes
            };
        }

        private Document TryOrganizeImports(Document document)
        {
            if (document == null)
            {
                return null;
            }

            for (var i = 0; i < OrganizeImportsTypeNames.Length; i++)
            {
                var serviceType = FindLoadedType(OrganizeImportsTypeNames[i]);
                if (serviceType == null)
                {
                    continue;
                }

                var organized = InvokeBestTaskResult(serviceType, "OrganizeImportsAsync", new object[] { document }) as Document;
                if (organized != null)
                {
                    return organized;
                }
            }

            return document;
        }

        private static Document TryReduceDocument(Document document)
        {
            if (document == null)
            {
                return null;
            }

            try
            {
                var reduced = Simplifier.ReduceAsync(document).Result;
                return reduced ?? document;
            }
            catch
            {
                return document;
            }
        }

        private static Document TryFormatDocument(Document document)
        {
            if (document == null)
            {
                return null;
            }

            try
            {
                var formatted = Formatter.FormatAsync(document).Result;
                return formatted ?? document;
            }
            catch
            {
                return document;
            }
        }

        private LanguageServiceDocumentChange[] BuildDocumentChangesFromSolutionDiff(Solution originalSolution, Solution updatedSolution)
        {
            if (originalSolution == null || updatedSolution == null)
            {
                return new LanguageServiceDocumentChange[0];
            }

            var changes = new List<LanguageServiceDocumentChange>();
            foreach (var project in updatedSolution.Projects)
            {
                foreach (var updatedDocument in project.Documents)
                {
                    var originalDocument = originalSolution.GetDocument(updatedDocument.Id);
                    if (originalDocument == null)
                    {
                        continue;
                    }

                    var originalText = originalDocument.GetTextAsync().Result;
                    var updatedText = updatedDocument.GetTextAsync().Result;
                    var textChanges = updatedText.GetTextChanges(originalText);
                    if (textChanges == null || textChanges.Count == 0)
                    {
                        continue;
                    }

                    var edits = new List<LanguageServiceTextEdit>();
                    for (var changeIndex = 0; changeIndex < textChanges.Count; changeIndex++)
                    {
                        var change = textChanges[changeIndex];
                        string previewStartLineText;
                        string previewText;
                        BuildLocationText(originalText, change.Span, out previewStartLineText, out previewText);
                        edits.Add(new LanguageServiceTextEdit
                        {
                            Range = BuildRange(originalText, change.Span),
                            OldText = originalText.ToString(change.Span),
                            NewText = change.NewText ?? string.Empty,
                            PreviewText = !string.IsNullOrEmpty(previewText) ? previewText : change.NewText ?? string.Empty
                        });
                    }

                    changes.Add(new LanguageServiceDocumentChange
                    {
                        DocumentPath = updatedDocument.FilePath ?? string.Empty,
                        ProjectFilePath = project.FilePath ?? string.Empty,
                        DisplayPath = updatedDocument.FilePath ?? string.Empty,
                        ChangeCount = edits.Count,
                        Edits = edits.ToArray()
                    });
                }
            }

            changes.Sort(delegate(LanguageServiceDocumentChange left, LanguageServiceDocumentChange right)
            {
                return string.Compare(left != null ? left.DocumentPath : string.Empty, right != null ? right.DocumentPath : string.Empty, StringComparison.OrdinalIgnoreCase);
            });
            return changes.ToArray();
        }

        private LanguageServiceSignatureHelpResponse GetSignatureHelp(LanguageServiceSignatureHelpRequest request)
        {
            var documentContext = ResolveDocument(
                request != null ? request.DocumentPath : string.Empty,
                request != null ? request.ProjectFilePath : string.Empty,
                request != null ? request.WorkspaceRootPath : string.Empty,
                request != null ? request.SourceRoots : null,
                request != null ? request.DocumentText : string.Empty,
                request != null ? request.DocumentVersion : 0);

            if (documentContext.Document == null)
            {
                return new LanguageServiceSignatureHelpResponse
                {
                    Success = false,
                    StatusMessage = "Roslyn could not resolve a document for signature help.",
                    DocumentPath = request != null ? request.DocumentPath ?? string.Empty : string.Empty,
                    ProjectFilePath = request != null ? request.ProjectFilePath ?? string.Empty : string.Empty,
                    DocumentVersion = request != null ? request.DocumentVersion : 0,
                    Items = new LanguageServiceSignatureHelpItem[0]
                };
            }

            var text = documentContext.Document.GetTextAsync().Result;
            var position = ResolveRequestPosition(text, request != null ? request.Line : 0, request != null ? request.Column : 0, request != null ? request.AbsolutePosition : -1);
            if (position < 0 || position > text.Length)
            {
                return new LanguageServiceSignatureHelpResponse
                {
                    Success = false,
                    StatusMessage = "Signature help position is outside the document.",
                    DocumentPath = documentContext.Document.FilePath ?? string.Empty,
                    ProjectFilePath = documentContext.ProjectPath ?? string.Empty,
                    DocumentVersion = request != null ? request.DocumentVersion : 0,
                    Items = new LanguageServiceSignatureHelpItem[0]
                };
            }

            var signatureHelpServiceType = FindLoadedType("Microsoft.CodeAnalysis.SignatureHelp.SignatureHelpService");
            var triggerInfoType = FindLoadedType("Microsoft.CodeAnalysis.SignatureHelp.SignatureHelpTriggerInfo");
            var service = signatureHelpServiceType != null
                ? InvokeBestTaskResult(signatureHelpServiceType, "GetService", new object[] { documentContext.Document })
                : null;
            var signatureHelpItems = InvokeBestTaskResult(
                service,
                "GetItemsAsync",
                new object[]
                {
                    documentContext.Document,
                    position,
                    triggerInfoType != null && triggerInfoType.IsValueType ? Activator.CreateInstance(triggerInfoType) : null
                });
            if (signatureHelpItems == null)
            {
                return new LanguageServiceSignatureHelpResponse
                {
                    Success = true,
                    StatusMessage = "No signature help items were available.",
                    DocumentPath = documentContext.Document.FilePath ?? string.Empty,
                    ProjectFilePath = documentContext.ProjectPath ?? string.Empty,
                    DocumentVersion = request != null ? request.DocumentVersion : 0,
                    Items = new LanguageServiceSignatureHelpItem[0]
                };
            }

            var applicableSpan = GetStructPropertyValue<TextSpan>(signatureHelpItems, "ApplicableSpan");
            var activeSignatureIndex = GetIntPropertyValue(signatureHelpItems, "SelectedItemIndex");
            var activeParameterIndex = GetIntPropertyValue(signatureHelpItems, "ArgumentIndex");
            var items = BuildSignatureHelpItems(GetEnumerablePropertyValue(signatureHelpItems, "Items"));
            if (activeSignatureIndex < 0 && items.Length > 0)
            {
                activeSignatureIndex = 0;
            }

            if (activeParameterIndex < 0)
            {
                activeParameterIndex = 0;
            }

            return new LanguageServiceSignatureHelpResponse
            {
                Success = true,
                StatusMessage = items.Length > 0 ? "Signature help resolved." : "No signature help items were available.",
                DocumentPath = documentContext.Document.FilePath ?? string.Empty,
                ProjectFilePath = documentContext.ProjectPath ?? string.Empty,
                DocumentVersion = request != null ? request.DocumentVersion : 0,
                ApplicableRange = BuildRange(text, applicableSpan),
                ActiveSignatureIndex = activeSignatureIndex,
                ActiveParameterIndex = activeParameterIndex,
                Items = items
            };
        }

        private static LanguageServiceSignatureHelpItem[] BuildSignatureHelpItems(IEnumerable items)
        {
            var results = new List<LanguageServiceSignatureHelpItem>();
            if (items == null)
            {
                return results.ToArray();
            }

            foreach (var item in items)
            {
                if (item == null)
                {
                    continue;
                }

                results.Add(new LanguageServiceSignatureHelpItem
                {
                    PrefixDisplay = FlattenReflectedTaggedParts(GetEnumerablePropertyValue(item, "PrefixDisplayParts")),
                    SeparatorDisplay = FlattenReflectedTaggedParts(GetEnumerablePropertyValue(item, "SeparatorDisplayParts")),
                    SuffixDisplay = FlattenReflectedTaggedParts(GetEnumerablePropertyValue(item, "SuffixDisplayParts")),
                    Documentation = FlattenReflectedTaggedParts(GetEnumerablePropertyValue(item, "DocumentationParts")),
                    Parameters = BuildSignatureHelpParameters(GetEnumerablePropertyValue(item, "Parameters"))
                });
            }

            return results.ToArray();
        }

        private static LanguageServiceSignatureHelpParameter[] BuildSignatureHelpParameters(IEnumerable parameters)
        {
            var results = new List<LanguageServiceSignatureHelpParameter>();
            if (parameters == null)
            {
                return results.ToArray();
            }

            foreach (var parameter in parameters)
            {
                if (parameter == null)
                {
                    continue;
                }

                results.Add(new LanguageServiceSignatureHelpParameter
                {
                    Name = GetStringPropertyValue(parameter, "Name"),
                    Display = FlattenReflectedTaggedParts(GetEnumerablePropertyValue(parameter, "DisplayParts")),
                    Documentation = FlattenReflectedTaggedParts(GetEnumerablePropertyValue(parameter, "DocumentationParts")),
                    IsOptional = GetBoolPropertyValue(parameter, "IsOptional")
                });
            }

            return results.ToArray();
        }

        private static Type FindLoadedType(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
            {
                return null;
            }

            var direct = Type.GetType(fullName, false);
            if (direct != null)
            {
                return direct;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                var resolved = assemblies[i].GetType(fullName, false);
                if (resolved != null)
                {
                    return resolved;
                }
            }

            return null;
        }

        private static object InvokeBestTaskResult(object target, string methodName, object[] leadingArguments)
        {
            if (target == null || string.IsNullOrEmpty(methodName))
            {
                return null;
            }

            var targetType = target as Type ?? target.GetType();
            var flags = BindingFlags.Public | BindingFlags.NonPublic |
                ((target is Type) ? BindingFlags.Static : BindingFlags.Instance);
            var methods = targetType.GetMethods(flags)
                .Where(method => string.Equals(method.Name, methodName, StringComparison.Ordinal))
                .OrderBy(method => method.GetParameters().Length)
                .ToArray();
            for (var i = 0; i < methods.Length; i++)
            {
                var method = methods[i];
                object[] arguments;
                if (!TryBuildInvocationArguments(method, leadingArguments, out arguments))
                {
                    continue;
                }

                try
                {
                    var result = method.Invoke(target is Type ? null : target, arguments);
                    return UnwrapTaskResult(result);
                }
                catch
                {
                }
            }

            return null;
        }

        private static bool TryBuildInvocationArguments(MethodInfo method, object[] leadingArguments, out object[] arguments)
        {
            arguments = null;
            if (method == null)
            {
                return false;
            }

            var parameters = method.GetParameters();
            if (parameters.Length < (leadingArguments != null ? leadingArguments.Length : 0))
            {
                return false;
            }

            arguments = new object[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                if (leadingArguments != null && i < leadingArguments.Length)
                {
                    var value = leadingArguments[i];
                    if (value != null && !parameters[i].ParameterType.IsInstanceOfType(value))
                    {
                        return false;
                    }

                    arguments[i] = value;
                    continue;
                }

                arguments[i] = BuildDefaultInvocationValue(parameters[i]);
            }

            return true;
        }

        private static object BuildDefaultInvocationValue(ParameterInfo parameter)
        {
            if (parameter == null)
            {
                return null;
            }

            if (parameter.HasDefaultValue)
            {
                return parameter.DefaultValue;
            }

            if (parameter.ParameterType == typeof(CancellationToken))
            {
                return CancellationToken.None;
            }

            return parameter.ParameterType.IsValueType
                ? Activator.CreateInstance(parameter.ParameterType)
                : null;
        }

        private static object UnwrapTaskResult(object result)
        {
            if (result == null)
            {
                return null;
            }

            var resultType = result.GetType();
            if (!typeof(System.Threading.Tasks.Task).IsAssignableFrom(resultType))
            {
                return result;
            }

            var task = (System.Threading.Tasks.Task)result;
            task.Wait();
            var resultProperty = resultType.GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);
            return resultProperty != null ? resultProperty.GetValue(result, null) : null;
        }

        private static IEnumerable GetEnumerablePropertyValue(object target, string propertyName)
        {
            var value = GetPropertyValue(target, propertyName);
            return value as IEnumerable;
        }

        private static string GetStringPropertyValue(object target, string propertyName)
        {
            var value = GetPropertyValue(target, propertyName);
            return value != null ? value.ToString() : string.Empty;
        }

        private static int GetIntPropertyValue(object target, string propertyName)
        {
            var value = GetPropertyValue(target, propertyName);
            return value is int ? (int)value : -1;
        }

        private static bool GetBoolPropertyValue(object target, string propertyName)
        {
            var value = GetPropertyValue(target, propertyName);
            return value is bool && (bool)value;
        }

        private static T GetStructPropertyValue<T>(object target, string propertyName) where T : struct
        {
            var value = GetPropertyValue(target, propertyName);
            return value is T ? (T)value : default(T);
        }

        private static object GetPropertyValue(object target, string propertyName)
        {
            if (target == null || string.IsNullOrEmpty(propertyName))
            {
                return null;
            }

            var property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return property != null ? property.GetValue(target, null) : null;
        }

        private static string FlattenReflectedTaggedParts(IEnumerable taggedParts)
        {
            if (taggedParts == null)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            foreach (var part in taggedParts)
            {
                if (part == null)
                {
                    continue;
                }

                var textProperty = part.GetType().GetProperty("Text", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var text = textProperty != null ? textProperty.GetValue(part, null) as string : part.ToString();
                if (!string.IsNullOrEmpty(text))
                {
                    parts.Add(text);
                }
            }

            return string.Concat(parts.ToArray()).Trim();
        }
    }
}
