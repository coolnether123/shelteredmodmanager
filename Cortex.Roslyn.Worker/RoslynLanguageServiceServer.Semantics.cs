using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Cortex.LanguageService.Protocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;

namespace Cortex.Roslyn.Worker
{
    internal sealed partial class RoslynLanguageServiceServer
    {
        private sealed class ResolvedSymbolRequestContext
        {
            public DocumentContext DocumentContext;
            public Document Document;
            public SourceText Text;
            public int Position;
            public ISymbol Symbol;
            public SyntaxTree SyntaxTree;
            public SyntaxNode SyntaxRoot;
            public SemanticModel SemanticModel;
        }

        private sealed class OutgoingCallGroup
        {
            public ISymbol Symbol;
            public readonly List<LanguageServiceSymbolLocation> Locations = new List<LanguageServiceSymbolLocation>();
        }

        private sealed class ProtocolLocationConversionContext
        {
            public readonly Dictionary<SyntaxTree, SourceText> SourceTextCache = new Dictionary<SyntaxTree, SourceText>();
            public readonly Dictionary<SyntaxTree, Document> DocumentCache = new Dictionary<SyntaxTree, Document>();

            public ProtocolLocationConversionContext(Solution solution) { }
        }

        private LanguageServiceSymbolContextResponse GetSymbolContext(LanguageServiceSymbolContextRequest request)
        {
            ResolvedSymbolRequestContext context;
            string failureMessage;
            if (!TryResolveSymbolRequestContext(request, out context, out failureMessage))
            {
                return new LanguageServiceSymbolContextResponse
                {
                    Success = false,
                    StatusMessage = failureMessage ?? "Roslyn could not resolve semantic symbol context.",
                    DocumentPath = request != null ? request.DocumentPath ?? string.Empty : string.Empty,
                    ProjectFilePath = request != null ? request.ProjectFilePath ?? string.Empty : string.Empty,
                    DocumentVersion = request != null ? request.DocumentVersion : 0
                };
            }

            var response = new LanguageServiceSymbolContextResponse
            {
                Success = true,
                StatusMessage = "Semantic symbol context resolved."
            };
            PopulateSymbolResponse(response, request, context);
            return response;
        }

        private LanguageServiceRenameResponse PreviewRename(LanguageServiceRenameRequest request)
        {
            ResolvedSymbolRequestContext context;
            string failureMessage;
            if (!TryResolveSymbolRequestContext(request, out context, out failureMessage))
            {
                return new LanguageServiceRenameResponse
                {
                    Success = false,
                    StatusMessage = failureMessage ?? "Roslyn could not resolve a symbol for rename preview.",
                    DocumentPath = request != null ? request.DocumentPath ?? string.Empty : string.Empty,
                    ProjectFilePath = request != null ? request.ProjectFilePath ?? string.Empty : string.Empty,
                    DocumentVersion = request != null ? request.DocumentVersion : 0,
                    Documents = new LanguageServiceDocumentChange[0]
                };
            }

            var newName = request != null ? request.NewName ?? string.Empty : string.Empty;
            if (string.IsNullOrEmpty(newName))
            {
                var emptyResponse = new LanguageServiceRenameResponse
                {
                    Success = false,
                    StatusMessage = "Enter a new symbol name before previewing rename.",
                    Documents = new LanguageServiceDocumentChange[0]
                };
                PopulateSymbolResponse(emptyResponse, request, context);
                emptyResponse.OldName = GetRenameDisplayName(context.Symbol);
                emptyResponse.NewName = string.Empty;
                return emptyResponse;
            }

            Solution renamedSolution;
            string renameFailureMessage;
            if (TryRenameSymbolSolution(context.Document.Project.Solution, context.Symbol, newName, out renamedSolution, out renameFailureMessage))
            {
                var documentChanges = BuildDocumentChangesFromSolutionDiff(context.Document.Project.Solution, renamedSolution);
                var response = new LanguageServiceRenameResponse
                {
                    Success = documentChanges.Length > 0,
                    StatusMessage = documentChanges.Length > 0
                        ? "Semantic rename preview resolved."
                        : "No semantic rename locations were found for the selected symbol.",
                    OldName = GetRenameDisplayName(context.Symbol),
                    NewName = newName,
                    TotalChangeCount = documentChanges.Sum(change => change != null ? change.ChangeCount : 0),
                    Documents = documentChanges
                };
                PopulateSymbolResponse(response, request, context);
                return response;
            }

            List<LanguageServiceSymbolLocation> fallbackLocations = null;
            try
            {
                fallbackLocations = CollectReferenceLocations(context.Symbol, context.Document.Project.Solution, true);
            }
            catch (Exception fallbackException)
            {
                renameFailureMessage = !string.IsNullOrEmpty(renameFailureMessage)
                    ? renameFailureMessage + " Fallback reference collection failed: " + fallbackException.Message
                    : "Fallback reference collection failed: " + fallbackException.Message;
            }

            var fallbackChanges = fallbackLocations != null
                ? BuildDocumentChanges(fallbackLocations, newName)
                : new LanguageServiceDocumentChange[0];
            if (fallbackChanges.Length > 0)
            {
                var fallbackResponse = new LanguageServiceRenameResponse
                {
                    Success = true,
                    StatusMessage = "Roslyn rename engine was unavailable; generated a reference-based rename preview.",
                    OldName = GetRenameDisplayName(context.Symbol),
                    NewName = newName,
                    TotalChangeCount = fallbackChanges.Sum(change => change != null ? change.ChangeCount : 0),
                    Documents = fallbackChanges
                };
                PopulateSymbolResponse(fallbackResponse, request, context);
                return fallbackResponse;
            }

            var failedResponse = new LanguageServiceRenameResponse
            {
                Success = false,
                StatusMessage = !string.IsNullOrEmpty(renameFailureMessage)
                    ? renameFailureMessage
                    : "Roslyn rename preview could not be generated.",
                OldName = GetRenameDisplayName(context.Symbol),
                NewName = newName,
                Documents = new LanguageServiceDocumentChange[0]
            };
            PopulateSymbolResponse(failedResponse, request, context);
            return failedResponse;
        }

        private LanguageServiceReferencesResponse FindReferences(LanguageServiceReferencesRequest request)
        {
            ResolvedSymbolRequestContext context;
            string failureMessage;
            if (!TryResolveSymbolRequestContext(request, out context, out failureMessage))
            {
                return new LanguageServiceReferencesResponse
                {
                    Success = false,
                    StatusMessage = failureMessage ?? "Roslyn could not resolve references for the current symbol.",
                    DocumentPath = request != null ? request.DocumentPath ?? string.Empty : string.Empty,
                    ProjectFilePath = request != null ? request.ProjectFilePath ?? string.Empty : string.Empty,
                    DocumentVersion = request != null ? request.DocumentVersion : 0,
                    Locations = new LanguageServiceSymbolLocation[0]
                };
            }

            var locations = CollectReferenceLocations(context.Symbol, context.Document.Project.Solution, true);
            var response = new LanguageServiceReferencesResponse
            {
                Success = true,
                StatusMessage = locations.Count > 0
                    ? "Semantic references resolved."
                    : "No semantic references were found.",
                TotalLocationCount = locations.Count,
                Locations = locations.ToArray()
            };
            PopulateSymbolResponse(response, request, context);
            return response;
        }

        private bool TryResolveSymbolRequestContext(LanguageServiceSymbolRequest request, out ResolvedSymbolRequestContext context, out string failureMessage)
        {
            context = null;
            failureMessage = string.Empty;

            var documentContext = ResolveDocument(
                request != null ? request.DocumentPath : string.Empty,
                request != null ? request.ProjectFilePath : string.Empty,
                request != null ? request.WorkspaceRootPath : string.Empty,
                request != null ? request.SourceRoots : null,
                request != null ? request.DocumentText : string.Empty,
                request != null ? request.DocumentVersion : 0);
            if (documentContext.Document == null)
            {
                failureMessage = "Roslyn could not resolve a backing document for the current symbol.";
                return false;
            }

            var text = GetDocumentText(documentContext);
            var position = ResolveRequestPosition(
                text,
                request != null ? request.Line : 0,
                request != null ? request.Column : 0,
                request != null ? request.AbsolutePosition : -1);
            if (position < 0 || position > text.Length)
            {
                failureMessage = "The semantic request position is outside the current document.";
                return false;
            }

            var symbol = ResolveSymbol(documentContext, position);
            if (symbol == null)
            {
                failureMessage = "No Roslyn symbol was found at that position.";
                return false;
            }

            context = new ResolvedSymbolRequestContext
            {
                DocumentContext = documentContext,
                Document = documentContext.Document,
                Text = text,
                Position = position,
                Symbol = symbol,
                SyntaxTree = GetDocumentSyntaxTree(documentContext),
                SyntaxRoot = GetDocumentSyntaxRoot(documentContext),
                SemanticModel = GetDocumentSemanticModel(documentContext)
            };
            return true;
        }

        private LanguageServiceBaseSymbolResponse GetBaseSymbols(LanguageServiceBaseSymbolRequest request)
        {
            ResolvedSymbolRequestContext context;
            string failureMessage;
            if (!TryResolveSymbolRequestContext(request, out context, out failureMessage))
            {
                return new LanguageServiceBaseSymbolResponse
                {
                    Success = false,
                    StatusMessage = failureMessage ?? "Roslyn could not resolve base symbols for the current symbol.",
                    DocumentPath = request != null ? request.DocumentPath ?? string.Empty : string.Empty,
                    ProjectFilePath = request != null ? request.ProjectFilePath ?? string.Empty : string.Empty,
                    DocumentVersion = request != null ? request.DocumentVersion : 0,
                    Locations = new LanguageServiceSymbolLocation[0]
                };
            }

            var baseSymbols = GetBaseSymbols(context.Symbol);
            var locations = new List<LanguageServiceSymbolLocation>();
            for (var i = 0; i < baseSymbols.Count; i++)
            {
                AddSymbolPrimaryLocations(locations, context.Document.Project.Solution, baseSymbols[i], "Base Symbol");
            }

            var response = new LanguageServiceBaseSymbolResponse
            {
                Success = true,
                StatusMessage = locations.Count > 0
                    ? "Base symbols resolved."
                    : "No distinct base symbols were found.",
                TotalLocationCount = locations.Count,
                Locations = locations.ToArray()
            };
            PopulateSymbolResponse(response, request, context);
            return response;
        }

        private LanguageServiceImplementationResponse GetImplementations(LanguageServiceImplementationRequest request)
        {
            ResolvedSymbolRequestContext context;
            string failureMessage;
            if (!TryResolveSymbolRequestContext(request, out context, out failureMessage))
            {
                return new LanguageServiceImplementationResponse
                {
                    Success = false,
                    StatusMessage = failureMessage ?? "Roslyn could not resolve implementations for the current symbol.",
                    DocumentPath = request != null ? request.DocumentPath ?? string.Empty : string.Empty,
                    ProjectFilePath = request != null ? request.ProjectFilePath ?? string.Empty : string.Empty,
                    DocumentVersion = request != null ? request.DocumentVersion : 0,
                    Locations = new LanguageServiceSymbolLocation[0]
                };
            }

            var implementationSymbols = SymbolFinder.FindImplementationsAsync(context.Symbol, context.Document.Project.Solution, cancellationToken: CurrentCancellationToken).GetAwaiter().GetResult()
                .Where(symbol => symbol != null)
                .ToList();
            var locations = new List<LanguageServiceSymbolLocation>();
            for (var i = 0; i < implementationSymbols.Count; i++)
            {
                AddSymbolPrimaryLocations(locations, context.Document.Project.Solution, implementationSymbols[i], "Implementation");
            }

            var response = new LanguageServiceImplementationResponse
            {
                Success = true,
                StatusMessage = locations.Count > 0
                    ? "Implementations resolved."
                    : "No distinct implementations were found.",
                TotalLocationCount = locations.Count,
                Locations = locations.ToArray()
            };
            PopulateSymbolResponse(response, request, context);
            return response;
        }

        private LanguageServiceCallHierarchyResponse GetCallHierarchy(LanguageServiceCallHierarchyRequest request)
        {
            ResolvedSymbolRequestContext context;
            string failureMessage;
            if (!TryResolveSymbolRequestContext(request, out context, out failureMessage))
            {
                return new LanguageServiceCallHierarchyResponse
                {
                    Success = false,
                    StatusMessage = failureMessage ?? "Roslyn could not resolve call hierarchy for the current symbol.",
                    DocumentPath = request != null ? request.DocumentPath ?? string.Empty : string.Empty,
                    ProjectFilePath = request != null ? request.ProjectFilePath ?? string.Empty : string.Empty,
                    DocumentVersion = request != null ? request.DocumentVersion : 0,
                    IncomingCalls = new LanguageServiceCallHierarchyItem[0],
                    OutgoingCalls = new LanguageServiceCallHierarchyItem[0]
                };
            }

            var solution = context.Document.Project.Solution;
            var response = new LanguageServiceCallHierarchyResponse
            {
                Success = true,
                StatusMessage = "Semantic call hierarchy resolved.",
                IncomingCalls = BuildIncomingCallItems(context.Symbol, solution),
                OutgoingCalls = BuildOutgoingCallItems(context.Symbol, solution)
            };
            PopulateSymbolResponse(response, request, context);
            return response;
        }

        private LanguageServiceValueSourceResponse GetValueSource(LanguageServiceValueSourceRequest request)
        {
            ResolvedSymbolRequestContext context;
            string failureMessage;
            if (!TryResolveSymbolRequestContext(request, out context, out failureMessage))
            {
                return new LanguageServiceValueSourceResponse
                {
                    Success = false,
                    StatusMessage = failureMessage ?? "Roslyn could not resolve value-source information for the current symbol.",
                    DocumentPath = request != null ? request.DocumentPath ?? string.Empty : string.Empty,
                    ProjectFilePath = request != null ? request.ProjectFilePath ?? string.Empty : string.Empty,
                    DocumentVersion = request != null ? request.DocumentVersion : 0,
                    Items = new LanguageServiceValueSourceItem[0]
                };
            }

            var items = BuildValueSourceItems(context);
            var response = new LanguageServiceValueSourceResponse
            {
                Success = true,
                StatusMessage = items.Length > 0
                    ? "Semantic value-source flow resolved."
                    : "No semantic value-source entries were found.",
                Items = items
            };
            PopulateSymbolResponse(response, request, context);
            return response;
        }

        private void PopulateSymbolResponse(LanguageServiceSymbolResponse response, LanguageServiceSymbolRequest request, ResolvedSymbolRequestContext context)
        {
            if (response == null || context == null || context.Symbol == null)
            {
                return;
            }

            var symbol = context.Symbol;
            string navigationMetadataName;
            string navigationContainingTypeName;
            string navigationContainingAssemblyName;
            string navigationDocumentationCommentId;
            PopulateNavigationMetadata(
                symbol,
                out navigationMetadataName,
                out navigationContainingTypeName,
                out navigationContainingAssemblyName,
                out navigationDocumentationCommentId);
            var preferredLocation = GetDefinitionNavigationLocation(symbol, context.SyntaxTree);
            response.DocumentPath = context.Document.FilePath ?? (request != null ? request.DocumentPath ?? string.Empty : string.Empty);
            response.ProjectFilePath = context.Document.Project != null ? context.Document.Project.FilePath ?? string.Empty : string.Empty;
            response.DocumentVersion = request != null ? request.DocumentVersion : 0;
            response.SymbolDisplay = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
            response.QualifiedSymbolDisplay = GetQualifiedSymbolDisplay(symbol);
            response.SymbolKind = symbol.Kind.ToString();
            response.MetadataName = navigationMetadataName;
            response.ContainingTypeName = navigationContainingTypeName;
            response.ContainingAssemblyName = navigationContainingAssemblyName;
            response.DocumentationCommentId = navigationDocumentationCommentId;
            response.DocumentationXml = symbol.GetDocumentationCommentXml() ?? string.Empty;
            response.DocumentationText = FlattenDocumentation(response.DocumentationXml);
            response.Range = BuildRange(context.Text, preferredLocation != null && preferredLocation.SourceTree == context.SyntaxTree
                ? preferredLocation.SourceSpan
                : default(TextSpan));
            response.DefinitionDocumentPath = preferredLocation != null && preferredLocation.SourceTree != null
                ? preferredLocation.SourceTree.FilePath ?? string.Empty
                : string.Empty;
            response.DefinitionRange = preferredLocation != null && preferredLocation.SourceTree != null
                ? BuildRange(preferredLocation.SourceTree.GetText(), preferredLocation.SourceSpan)
                : new LanguageServiceRange();
        }

        private static string GetRenameDisplayName(ISymbol symbol)
        {
            if (symbol == null)
            {
                return string.Empty;
            }

            return !string.IsNullOrEmpty(symbol.Name)
                ? symbol.Name
                : symbol.MetadataName ?? string.Empty;
        }

        private static IList<ISymbol> GetBaseSymbols(ISymbol symbol)
        {
            var results = new List<ISymbol>();
            if (symbol == null)
            {
                return results;
            }

            var namedType = symbol as INamedTypeSymbol;
            if (namedType != null)
            {
                if (namedType.BaseType != null && namedType.BaseType.SpecialType != SpecialType.System_Object)
                {
                    results.Add(namedType.BaseType);
                }

                for (var i = 0; i < namedType.Interfaces.Length; i++)
                {
                    AddDistinctSymbol(results, namedType.Interfaces[i]);
                }

                return results;
            }

            var method = symbol as IMethodSymbol;
            if (method != null)
            {
                AddDistinctSymbol(results, method.OverriddenMethod);
                for (var i = 0; i < method.ExplicitInterfaceImplementations.Length; i++)
                {
                    AddDistinctSymbol(results, method.ExplicitInterfaceImplementations[i]);
                }

                return results;
            }

            var property = symbol as IPropertySymbol;
            if (property != null)
            {
                AddDistinctSymbol(results, property.OverriddenProperty);
                for (var i = 0; i < property.ExplicitInterfaceImplementations.Length; i++)
                {
                    AddDistinctSymbol(results, property.ExplicitInterfaceImplementations[i]);
                }

                return results;
            }

            var eventSymbol = symbol as IEventSymbol;
            if (eventSymbol != null)
            {
                AddDistinctSymbol(results, eventSymbol.OverriddenEvent);
                for (var i = 0; i < eventSymbol.ExplicitInterfaceImplementations.Length; i++)
                {
                    AddDistinctSymbol(results, eventSymbol.ExplicitInterfaceImplementations[i]);
                }
            }

            return results;
        }

        private static void AddDistinctSymbol(IList<ISymbol> results, ISymbol symbol)
        {
            if (results == null || symbol == null)
            {
                return;
            }

            for (var i = 0; i < results.Count; i++)
            {
                if (SymbolEqualityComparer.Default.Equals(results[i], symbol))
                {
                    return;
                }
            }

            results.Add(symbol);
        }

        private List<LanguageServiceSymbolLocation> CollectReferenceLocations(ISymbol symbol, Solution solution, bool includeDefinitions)
        {
            var results = new List<LanguageServiceSymbolLocation>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var syntaxRootCache = new Dictionary<DocumentId, SyntaxNode>();
            var locationContext = new ProtocolLocationConversionContext(solution);
            if (symbol == null || solution == null)
            {
                return results;
            }

            if (includeDefinitions)
            {
                AddSymbolPrimaryLocations(results, solution, symbol, "Definition", seen, locationContext);
            }

            var scopedDocuments = GetReferenceSearchDocuments(symbol, solution);
            var references = SymbolFinder.FindReferencesAsync(
                symbol,
                solution,
                documents: scopedDocuments,
                cancellationToken: CurrentCancellationToken).GetAwaiter().GetResult();
            foreach (var referencedSymbol in references)
            {
                if (referencedSymbol == null)
                {
                    continue;
                }

                foreach (var referenceLocation in referencedSymbol.Locations)
                {
                    if (referenceLocation.Location == null || !referenceLocation.Location.IsInSource)
                    {
                        continue;
                    }

                    AddUniqueLocation(
                        results,
                        seen,
                        ToProtocolLocation(
                            solution,
                            referencedSymbol.Definition,
                            referenceLocation.Location,
                            "Reference",
                            false,
                            false,
                            IsWriteReference(solution, referenceLocation, syntaxRootCache),
                            false,
                            locationContext));
                }
            }

            return results;
        }

        private void AddSymbolPrimaryLocations(IList<LanguageServiceSymbolLocation> results, Solution solution, ISymbol symbol, string relationship)
        {
            AddSymbolPrimaryLocations(results, solution, symbol, relationship, null, new ProtocolLocationConversionContext(solution));
        }

        private void AddSymbolPrimaryLocations(IList<LanguageServiceSymbolLocation> results, Solution solution, ISymbol symbol, string relationship, HashSet<string> seen, ProtocolLocationConversionContext locationContext)
        {
            if (results == null || solution == null || symbol == null)
            {
                return;
            }

            foreach (var location in symbol.Locations)
            {
                if (location == null || !location.IsInSource)
                {
                    continue;
                }

                AddUniqueLocation(results, seen, ToProtocolLocation(solution, symbol, location, relationship, true, true, false, true, locationContext));
            }
        }

        private static void AddUniqueLocation(IList<LanguageServiceSymbolLocation> results, HashSet<string> seen, LanguageServiceSymbolLocation location)
        {
            if (results == null || location == null)
            {
                return;
            }

            if (seen == null)
            {
                results.Add(location);
                return;
            }

            var key = BuildLocationKey(location);
            if (seen.Add(key))
            {
                results.Add(location);
            }
        }

        private static string BuildLocationKey(LanguageServiceSymbolLocation location)
        {
            var range = location != null ? location.Range : null;
            return (location != null ? location.DocumentPath ?? string.Empty : string.Empty) +
                "|" + (range != null ? range.Start.ToString() : "0") +
                "|" + (range != null ? range.Length.ToString() : "0");
        }

        private LanguageServiceDocumentChange[] BuildDocumentChanges(IList<LanguageServiceSymbolLocation> locations, string newName)
        {
            var byDocument = new Dictionary<string, List<LanguageServiceSymbolLocation>>(StringComparer.OrdinalIgnoreCase);
            if (locations != null)
            {
                for (var i = 0; i < locations.Count; i++)
                {
                    var location = locations[i];
                    if (location == null || location.Range == null || string.IsNullOrEmpty(location.DocumentPath))
                    {
                        continue;
                    }

                    List<LanguageServiceSymbolLocation> documentLocations;
                    if (!byDocument.TryGetValue(location.DocumentPath, out documentLocations))
                    {
                        documentLocations = new List<LanguageServiceSymbolLocation>();
                        byDocument[location.DocumentPath] = documentLocations;
                    }

                    documentLocations.Add(location);
                }
            }

            var changes = new List<LanguageServiceDocumentChange>();
            foreach (var pair in byDocument)
            {
                var documentLocations = pair.Value;
                documentLocations.Sort(delegate(LanguageServiceSymbolLocation left, LanguageServiceSymbolLocation right)
                {
                    var leftStart = left != null && left.Range != null ? left.Range.Start : 0;
                    var rightStart = right != null && right.Range != null ? right.Range.Start : 0;
                    return leftStart.CompareTo(rightStart);
                });

                var edits = new List<LanguageServiceTextEdit>();
                for (var i = 0; i < documentLocations.Count; i++)
                {
                    var location = documentLocations[i];
                    edits.Add(new LanguageServiceTextEdit
                    {
                        Range = location.Range,
                        OldText = GetRenameDisplayNameFromLocation(location),
                        NewText = newName ?? string.Empty,
                        PreviewText = BuildRenamePreviewText(location, newName)
                    });
                }

                changes.Add(new LanguageServiceDocumentChange
                {
                    DocumentPath = pair.Key,
                    ProjectFilePath = documentLocations.Count > 0 ? documentLocations[0].ProjectFilePath ?? string.Empty : string.Empty,
                    DisplayPath = pair.Key,
                    ChangeCount = edits.Count,
                    Edits = edits.ToArray()
                });
            }

            changes.Sort(delegate(LanguageServiceDocumentChange left, LanguageServiceDocumentChange right)
            {
                return string.Compare(left.DocumentPath, right.DocumentPath, StringComparison.OrdinalIgnoreCase);
            });
            return changes.ToArray();
        }

        private static string GetRenameDisplayNameFromLocation(LanguageServiceSymbolLocation location)
        {
            if (location == null)
            {
                return string.Empty;
            }

            return !string.IsNullOrEmpty(location.MetadataName)
                ? location.MetadataName
                : location.SymbolDisplay ?? string.Empty;
        }

        private static string BuildRenamePreviewText(LanguageServiceSymbolLocation location, string newName)
        {
            var preview = location != null ? location.PreviewText ?? string.Empty : string.Empty;
            if (string.IsNullOrEmpty(preview))
            {
                return newName ?? string.Empty;
            }

            var oldText = GetRenameDisplayNameFromLocation(location);
            return !string.IsNullOrEmpty(oldText)
                ? preview.Replace(oldText, newName ?? string.Empty)
                : preview;
        }

        private bool TryRenameSymbolSolution(Solution solution, ISymbol symbol, string newName, out Solution renamedSolution, out string failureMessage)
        {
            renamedSolution = null;
            failureMessage = string.Empty;
            if (solution == null || symbol == null || string.IsNullOrEmpty(newName))
            {
                failureMessage = "Rename preview requires a source-backed symbol and a new name.";
                return false;
            }

            var renamerType = FindLoadedType("Microsoft.CodeAnalysis.Rename.Renamer");
            if (renamerType == null)
            {
                failureMessage = "Roslyn rename engine is unavailable.";
                return false;
            }

            var renameResult = InvokeBestTaskResult(
                renamerType,
                "RenameSymbolAsync",
                new object[]
                {
                    solution,
                    symbol,
                    newName
                });
            renamedSolution = renameResult as Solution;
            if (renamedSolution != null)
            {
                return true;
            }

            failureMessage = "Roslyn rename service did not return a renamed solution.";
            return false;
        }

        private LanguageServiceCallHierarchyItem[] BuildIncomingCallItems(ISymbol symbol, Solution solution)
        {
            var items = new List<LanguageServiceCallHierarchyItem>();
            var locationContext = new ProtocolLocationConversionContext(solution);
            var callers = SymbolFinder.FindCallersAsync(symbol, solution, cancellationToken: CurrentCancellationToken).GetAwaiter().GetResult();
            foreach (var caller in callers)
            {
                if (caller.CallingSymbol == null)
                {
                    continue;
                }

                var locations = new List<LanguageServiceSymbolLocation>();
                var callerLocations = caller.Locations.ToArray();
                for (var i = 0; i < callerLocations.Length; i++)
                {
                    var location = callerLocations[i];
                    if (location == null || !location.IsInSource)
                    {
                        continue;
                    }

                    locations.Add(ToProtocolLocation(solution, caller.CallingSymbol, location, "Incoming Call", false, false, false, false, locationContext));
                }

                items.Add(new LanguageServiceCallHierarchyItem
                {
                    SymbolDisplay = caller.CallingSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                    QualifiedSymbolDisplay = GetQualifiedSymbolDisplay(caller.CallingSymbol),
                    SymbolKind = caller.CallingSymbol.Kind.ToString(),
                    MetadataName = GetNavigationMetadataName(caller.CallingSymbol),
                    ContainingTypeName = GetNavigationContainingTypeName(caller.CallingSymbol),
                    ContainingAssemblyName = GetNavigationContainingAssemblyName(caller.CallingSymbol),
                    DocumentationCommentId = GetNavigationDocumentationCommentId(caller.CallingSymbol),
                    Relationship = "Incoming Call",
                    CallCount = locations.Count,
                    Locations = locations.ToArray()
                });
            }

            return items.ToArray();
        }

        private LanguageServiceCallHierarchyItem[] BuildOutgoingCallItems(ISymbol symbol, Solution solution)
        {
            var grouped = new Dictionary<string, OutgoingCallGroup>(StringComparer.OrdinalIgnoreCase);
            var semanticModelCache = new Dictionary<DocumentId, SemanticModel>();
            var locationContext = new ProtocolLocationConversionContext(solution);
            if (symbol == null || solution == null)
            {
                return new LanguageServiceCallHierarchyItem[0];
            }

            foreach (var syntaxReference in symbol.DeclaringSyntaxReferences)
            {
                if (syntaxReference == null)
                {
                    continue;
                }

                var syntax = syntaxReference.GetSyntaxAsync(CurrentCancellationToken).GetAwaiter().GetResult();
                var document = solution.GetDocument(syntax.SyntaxTree);
                if (document == null)
                {
                    continue;
                }

                var semanticModel = GetCachedSemanticModel(document, semanticModelCache);
                if (semanticModel == null)
                {
                    continue;
                }

                CollectOutgoingSymbols(grouped, solution, semanticModel, syntax, locationContext);
            }

            var items = new List<LanguageServiceCallHierarchyItem>();
            foreach (var pair in grouped)
            {
                var group = pair.Value;
                if (group == null || group.Symbol == null)
                {
                    continue;
                }

                items.Add(new LanguageServiceCallHierarchyItem
                {
                    SymbolDisplay = group.Symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                    QualifiedSymbolDisplay = GetQualifiedSymbolDisplay(group.Symbol),
                    SymbolKind = group.Symbol.Kind.ToString(),
                    MetadataName = GetNavigationMetadataName(group.Symbol),
                    ContainingTypeName = GetNavigationContainingTypeName(group.Symbol),
                    ContainingAssemblyName = GetNavigationContainingAssemblyName(group.Symbol),
                    DocumentationCommentId = GetNavigationDocumentationCommentId(group.Symbol),
                    Relationship = "Outgoing Call",
                    CallCount = group.Locations.Count,
                    Locations = group.Locations.ToArray()
                });
            }

            return items.ToArray();
        }

        private void CollectOutgoingSymbols(
            IDictionary<string, OutgoingCallGroup> grouped,
            Solution solution,
            SemanticModel semanticModel,
            SyntaxNode syntax,
            ProtocolLocationConversionContext locationContext)
        {
            if (grouped == null || solution == null || semanticModel == null || syntax == null)
            {
                return;
            }

            foreach (var invocation in syntax.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                AddOutgoingGroup(grouped, solution, semanticModel.GetSymbolInfo(invocation).Symbol, invocation.GetLocation(), locationContext);
            }

            foreach (var creation in syntax.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                AddOutgoingGroup(grouped, solution, semanticModel.GetSymbolInfo(creation).Symbol, creation.GetLocation(), locationContext);
            }
        }

        private void AddOutgoingGroup(IDictionary<string, OutgoingCallGroup> grouped, Solution solution, ISymbol symbol, Location location, ProtocolLocationConversionContext locationContext)
        {
            if (grouped == null || solution == null || symbol == null || location == null || !location.IsInSource)
            {
                return;
            }

            var key = (symbol.ContainingAssembly != null ? symbol.ContainingAssembly.Identity.Name : string.Empty) +
                "|" + (symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) ?? string.Empty);
            OutgoingCallGroup group;
            if (!grouped.TryGetValue(key, out group))
            {
                group = new OutgoingCallGroup
                {
                    Symbol = symbol
                };
                grouped[key] = group;
            }

            group.Locations.Add(ToProtocolLocation(solution, symbol, location, "Outgoing Call", false, false, false, false, locationContext));
        }

        private LanguageServiceValueSourceItem[] BuildValueSourceItems(ResolvedSymbolRequestContext context)
        {
            var items = new List<LanguageServiceValueSourceItem>();
            var declarationLocation = GetPreferredSourceLocation(context.Symbol, context.SyntaxTree);
            var syntaxRootCache = new Dictionary<DocumentId, SyntaxNode>();
            var locationContext = new ProtocolLocationConversionContext(context.Document.Project.Solution);
            if (declarationLocation != null && declarationLocation.IsInSource)
            {
                items.Add(new LanguageServiceValueSourceItem
                {
                    FlowKind = "Declaration",
                    SymbolDisplay = context.Symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                    Relationship = "Declared here",
                    Location = ToProtocolLocation(context.Document.Project.Solution, context.Symbol, declarationLocation, "Declaration", true, true, false, true, locationContext)
                });
            }

            var scopedDocuments = GetReferenceSearchDocuments(context.Symbol, context.Document.Project.Solution);
            var references = SymbolFinder.FindReferencesAsync(
                context.Symbol,
                context.Document.Project.Solution,
                documents: scopedDocuments,
                cancellationToken: CurrentCancellationToken).GetAwaiter().GetResult();
            foreach (var referencedSymbol in references)
            {
                if (referencedSymbol == null)
                {
                    continue;
                }

                foreach (var referenceLocation in referencedSymbol.Locations)
                {
                    if (referenceLocation.Location == null || !referenceLocation.Location.IsInSource)
                    {
                        continue;
                    }

                    if (!IsWriteReference(context.Document.Project.Solution, referenceLocation, syntaxRootCache))
                    {
                        continue;
                    }

                    items.Add(new LanguageServiceValueSourceItem
                    {
                        FlowKind = "Write",
                        SymbolDisplay = context.Symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                        Relationship = "Assigned here",
                        Location = ToProtocolLocation(context.Document.Project.Solution, context.Symbol, referenceLocation.Location, "Value Source", false, false, true, false, locationContext)
                    });
                }
            }

            return items.ToArray();
        }

        private static bool IsWriteReference(Solution solution, ReferenceLocation referenceLocation, IDictionary<DocumentId, SyntaxNode> syntaxRootCache)
        {
            if (solution == null || referenceLocation.Location == null || !referenceLocation.Location.IsInSource)
            {
                return false;
            }

            var document = solution.GetDocument(referenceLocation.Location.SourceTree);
            if (document == null)
            {
                return false;
            }

            var root = GetCachedSyntaxRoot(document, syntaxRootCache);
            if (root == null)
            {
                return false;
            }

            var node = root.FindNode(referenceLocation.Location.SourceSpan, true, true);
            if (node == null)
            {
                return false;
            }

            for (var current = node; current != null; current = current.Parent)
            {
                var assignment = current as AssignmentExpressionSyntax;
                if (assignment != null && assignment.Left != null && assignment.Left.Span.Contains(referenceLocation.Location.SourceSpan))
                {
                    return true;
                }

                var argument = current as ArgumentSyntax;
                if (argument != null && argument.RefOrOutKeyword.RawKind != 0)
                {
                    return true;
                }

                var prefix = current as PrefixUnaryExpressionSyntax;
                if (prefix != null)
                {
                    var kind = prefix.Kind().ToString();
                    if (string.Equals(kind, "PreIncrementExpression", StringComparison.Ordinal) ||
                        string.Equals(kind, "PreDecrementExpression", StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                var postfix = current as PostfixUnaryExpressionSyntax;
                if (postfix != null)
                {
                    var kind = postfix.Kind().ToString();
                    if (string.Equals(kind, "PostIncrementExpression", StringComparison.Ordinal) ||
                        string.Equals(kind, "PostDecrementExpression", StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static IImmutableSet<Document> GetReferenceSearchDocuments(ISymbol symbol, Solution solution)
        {
            if (symbol == null || solution == null)
            {
                return null;
            }

            var declaringDocument = GetFirstDeclaringSourceDocument(symbol, solution);
            if (declaringDocument == null)
            {
                return null;
            }

            // Narrow only when Roslyn's symbol rules make the search scope provably local.
            if (IsDocumentScopedReferenceSymbol(symbol))
            {
                return ImmutableHashSet.Create(declaringDocument);
            }

            if (symbol.DeclaredAccessibility == Accessibility.Private)
            {
                return declaringDocument.Project.Documents.ToImmutableHashSet();
            }

            return null;
        }

        private static bool IsDocumentScopedReferenceSymbol(ISymbol symbol)
        {
            if (symbol == null)
            {
                return false;
            }

            switch (symbol.Kind)
            {
                case SymbolKind.Alias:
                case SymbolKind.Label:
                case SymbolKind.Local:
                case SymbolKind.Parameter:
                case SymbolKind.RangeVariable:
                    return true;
                default:
                    return false;
            }
        }

        private static Document GetFirstDeclaringSourceDocument(ISymbol symbol, Solution solution)
        {
            if (symbol == null || solution == null)
            {
                return null;
            }

            for (var i = 0; i < symbol.Locations.Length; i++)
            {
                var location = symbol.Locations[i];
                if (location == null || !location.IsInSource || location.SourceTree == null)
                {
                    continue;
                }

                var document = solution.GetDocument(location.SourceTree);
                if (document != null)
                {
                    return document;
                }
            }

            return null;
        }

        private static SemanticModel GetCachedSemanticModel(Document document, IDictionary<DocumentId, SemanticModel> semanticModelCache)
        {
            if (document == null)
            {
                return null;
            }

            SemanticModel semanticModel;
            if (semanticModelCache != null && semanticModelCache.TryGetValue(document.Id, out semanticModel))
            {
                return semanticModel;
            }

            if (!document.TryGetSemanticModel(out semanticModel))
            {
                semanticModel = document.GetSemanticModelAsync(CurrentCancellationToken).GetAwaiter().GetResult();
            }
            if (semanticModelCache != null && semanticModel != null)
            {
                semanticModelCache[document.Id] = semanticModel;
            }

            return semanticModel;
        }

        private static SyntaxNode GetCachedSyntaxRoot(Document document, IDictionary<DocumentId, SyntaxNode> syntaxRootCache)
        {
            if (document == null)
            {
                return null;
            }

            SyntaxNode root;
            if (syntaxRootCache != null && syntaxRootCache.TryGetValue(document.Id, out root))
            {
                return root;
            }

            if (!document.TryGetSyntaxRoot(out root))
            {
                root = document.GetSyntaxRootAsync(CurrentCancellationToken).GetAwaiter().GetResult();
            }
            if (syntaxRootCache != null && root != null)
            {
                syntaxRootCache[document.Id] = root;
            }

            return root;
        }

        private LanguageServiceSymbolLocation ToProtocolLocation(
            Solution solution,
            ISymbol symbol,
            Location location,
            string relationship,
            bool isPrimary,
            bool isDefinition,
            bool isWrite,
            bool isDeclaration,
            ProtocolLocationConversionContext locationContext)
        {
            if (location == null || !location.IsInSource || location.SourceTree == null)
            {
                return new LanguageServiceSymbolLocation();
            }

            var text = GetCachedSourceText(location.SourceTree, locationContext);
            var range = BuildRange(text, location.SourceSpan);
            var document = GetCachedDocument(solution, location.SourceTree, locationContext);
            string lineText;
            string previewText;
            BuildLocationText(text, location.SourceSpan, out lineText, out previewText);
            return new LanguageServiceSymbolLocation
            {
                DocumentPath = location.SourceTree.FilePath ?? string.Empty,
                ProjectFilePath = document != null && document.Project != null ? document.Project.FilePath ?? string.Empty : string.Empty,
                SymbolDisplay = symbol != null ? symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) : string.Empty,
                SymbolKind = symbol != null ? symbol.Kind.ToString() : string.Empty,
                MetadataName = GetNavigationMetadataName(symbol),
                ContainingTypeName = GetNavigationContainingTypeName(symbol),
                ContainingAssemblyName = GetNavigationContainingAssemblyName(symbol),
                DocumentationCommentId = GetNavigationDocumentationCommentId(symbol),
                Range = range,
                LineText = lineText,
                PreviewText = previewText,
                Relationship = relationship ?? string.Empty,
                IsPrimary = isPrimary,
                IsDefinition = isDefinition,
                IsWrite = isWrite,
                IsDeclaration = isDeclaration
            };
        }

        private static SourceText GetCachedSourceText(SyntaxTree syntaxTree, ProtocolLocationConversionContext locationContext)
        {
            if (syntaxTree == null)
            {
                return null;
            }

            SourceText text;
            if (locationContext != null && locationContext.SourceTextCache.TryGetValue(syntaxTree, out text))
            {
                return text;
            }

            text = syntaxTree.GetText();
            if (locationContext != null && text != null)
            {
                locationContext.SourceTextCache[syntaxTree] = text;
            }

            return text;
        }

        private static Document GetCachedDocument(Solution solution, SyntaxTree syntaxTree, ProtocolLocationConversionContext locationContext)
        {
            if (syntaxTree == null)
            {
                return null;
            }

            Document document;
            if (locationContext != null && locationContext.DocumentCache.TryGetValue(syntaxTree, out document))
            {
                return document;
            }

            document = solution != null ? solution.GetDocument(syntaxTree) : null;
            if (locationContext != null && document != null)
            {
                locationContext.DocumentCache[syntaxTree] = document;
            }

            return document;
        }

        private static void BuildLocationText(SourceText text, TextSpan span, out string lineText, out string previewText)
        {
            lineText = string.Empty;
            previewText = string.Empty;
            if (text == null)
            {
                return;
            }

            var startLine = text.Lines.GetLineFromPosition(span.Start);
            lineText = startLine.ToString();

            var previewStartLine = Math.Max(0, startLine.LineNumber - 1);
            var previewEndLine = Math.Min(text.Lines.Count - 1, startLine.LineNumber + 1);
            var parts = new List<string>();
            for (var i = previewStartLine; i <= previewEndLine; i++)
            {
                parts.Add(text.Lines[i].ToString());
            }

            previewText = string.Join(Environment.NewLine, parts.ToArray());
        }

        private static Location GetPreferredSourceLocation(ISymbol symbol, SyntaxTree preferredTree)
        {
            if (symbol == null)
            {
                return null;
            }

            Location fallback = null;
            foreach (var location in symbol.Locations)
            {
                if (location == null || !location.IsInSource)
                {
                    continue;
                }

                if (preferredTree != null && location.SourceTree == preferredTree)
                {
                    return location;
                }

                if (fallback == null)
                {
                    fallback = location;
                }
            }

            return fallback;
        }

        private static Location GetDefinitionNavigationLocation(ISymbol symbol, SyntaxTree preferredTree)
        {
            var preferred = GetPreferredSourceLocation(symbol, preferredTree);
            if (preferred != null)
            {
                return preferred;
            }

            return GetPreferredSourceLocation(GetNavigationMetadataSymbol(symbol), preferredTree);
        }

        private static void PopulateNavigationMetadata(
            ISymbol symbol,
            out string metadataName,
            out string containingTypeName,
            out string containingAssemblyName,
            out string documentationCommentId)
        {
            var navigationSymbol = GetNavigationMetadataSymbol(symbol);
            metadataName = navigationSymbol != null ? navigationSymbol.MetadataName ?? string.Empty : string.Empty;
            containingTypeName = GetContainingTypeName(navigationSymbol);
            containingAssemblyName = navigationSymbol != null && navigationSymbol.ContainingAssembly != null
                ? navigationSymbol.ContainingAssembly.Identity.Name
                : string.Empty;
            documentationCommentId = navigationSymbol != null ? navigationSymbol.GetDocumentationCommentId() ?? string.Empty : string.Empty;
        }

        private static string GetNavigationMetadataName(ISymbol symbol)
        {
            string metadataName;
            string containingTypeName;
            string containingAssemblyName;
            string documentationCommentId;
            PopulateNavigationMetadata(
                symbol,
                out metadataName,
                out containingTypeName,
                out containingAssemblyName,
                out documentationCommentId);
            return metadataName;
        }

        private static string GetNavigationContainingTypeName(ISymbol symbol)
        {
            string metadataName;
            string containingTypeName;
            string containingAssemblyName;
            string documentationCommentId;
            PopulateNavigationMetadata(
                symbol,
                out metadataName,
                out containingTypeName,
                out containingAssemblyName,
                out documentationCommentId);
            return containingTypeName;
        }

        private static string GetNavigationContainingAssemblyName(ISymbol symbol)
        {
            string metadataName;
            string containingTypeName;
            string containingAssemblyName;
            string documentationCommentId;
            PopulateNavigationMetadata(
                symbol,
                out metadataName,
                out containingTypeName,
                out containingAssemblyName,
                out documentationCommentId);
            return containingAssemblyName;
        }

        private static string GetNavigationDocumentationCommentId(ISymbol symbol)
        {
            string metadataName;
            string containingTypeName;
            string containingAssemblyName;
            string documentationCommentId;
            PopulateNavigationMetadata(
                symbol,
                out metadataName,
                out containingTypeName,
                out containingAssemblyName,
                out documentationCommentId);
            return documentationCommentId;
        }

        internal static ISymbol GetNavigationMetadataSymbol(ISymbol symbol)
        {
            var namespaceSymbol = symbol as INamespaceSymbol;
            if (namespaceSymbol == null || namespaceSymbol.IsGlobalNamespace)
            {
                return symbol;
            }

            var representativeType = FindRepresentativeNamespaceType(namespaceSymbol);
            return representativeType ?? symbol;
        }

        private static INamedTypeSymbol FindRepresentativeNamespaceType(INamespaceSymbol namespaceSymbol)
        {
            if (namespaceSymbol == null || namespaceSymbol.IsGlobalNamespace)
            {
                return null;
            }

            var preferredType = SelectPreferredNamespaceType(namespaceSymbol.GetTypeMembers(), namespaceSymbol.Name);
            if (preferredType != null)
            {
                return preferredType;
            }

            var namespaceMembers = namespaceSymbol.GetNamespaceMembers().ToArray();
            var orderedMembers = new List<INamespaceSymbol>(namespaceMembers.Length);
            for (var i = 0; i < namespaceMembers.Length; i++)
            {
                var member = namespaceMembers[i];
                if (member != null && !member.IsGlobalNamespace)
                {
                    orderedMembers.Add(member);
                }
            }

            orderedMembers.Sort(delegate(INamespaceSymbol left, INamespaceSymbol right)
            {
                return string.Compare(left != null ? left.ToDisplayString() : string.Empty, right != null ? right.ToDisplayString() : string.Empty, StringComparison.OrdinalIgnoreCase);
            });

            for (var i = 0; i < orderedMembers.Count; i++)
            {
                var nested = FindRepresentativeNamespaceType(orderedMembers[i]);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static INamedTypeSymbol SelectPreferredNamespaceType(ImmutableArray<INamedTypeSymbol> candidates, string namespaceName)
        {
            var trimmedNamespaceName = !string.IsNullOrEmpty(namespaceName) && namespaceName.EndsWith("Lib", StringComparison.OrdinalIgnoreCase)
                ? namespaceName.Substring(0, namespaceName.Length - 3)
                : string.Empty;
            INamedTypeSymbol best = null;
            var bestRank = int.MaxValue;
            var bestLength = int.MaxValue;
            var bestDisplay = string.Empty;

            for (var i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (candidate == null || candidate.IsImplicitlyDeclared || candidate.TypeKind == TypeKind.Error)
                {
                    continue;
                }

                var rank = 3;
                if (!string.IsNullOrEmpty(namespaceName) && string.Equals(candidate.Name, namespaceName, StringComparison.OrdinalIgnoreCase))
                {
                    rank = 0;
                }
                else if (!string.IsNullOrEmpty(trimmedNamespaceName) && string.Equals(candidate.Name, trimmedNamespaceName, StringComparison.OrdinalIgnoreCase))
                {
                    rank = 1;
                }
                else if (candidate.DeclaredAccessibility == Accessibility.Public && !candidate.IsGenericType)
                {
                    rank = 2;
                }

                var display = candidate.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
                var length = candidate.Name != null ? candidate.Name.Length : int.MaxValue;
                if (best == null ||
                    rank < bestRank ||
                    (rank == bestRank && length < bestLength) ||
                    (rank == bestRank && length == bestLength && string.Compare(display, bestDisplay, StringComparison.OrdinalIgnoreCase) < 0))
                {
                    best = candidate;
                    bestRank = rank;
                    bestLength = length;
                    bestDisplay = display;
                }
            }

            return best;
        }
    }
}
