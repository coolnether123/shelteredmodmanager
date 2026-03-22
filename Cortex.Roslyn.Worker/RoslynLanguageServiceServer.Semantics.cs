using System;
using System.Collections.Generic;
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
        }

        private sealed class OutgoingCallGroup
        {
            public ISymbol Symbol;
            public readonly List<LanguageServiceSymbolLocation> Locations = new List<LanguageServiceSymbolLocation>();
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

            var renameLocations = CollectReferenceLocations(context.Symbol, context.Document.Project.Solution, true);
            var documentChanges = BuildDocumentChanges(renameLocations, newName);
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

            var text = documentContext.Document.GetTextAsync().Result;
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

            var symbol = ResolveSymbol(documentContext.Document, position);
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
                Symbol = symbol
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

            var implementationSymbols = SymbolFinder.FindImplementationsAsync(context.Symbol, context.Document.Project.Solution).Result
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
            var preferredLocation = GetPreferredSourceLocation(symbol, context.Document.GetSyntaxTreeAsync().Result);
            response.DocumentPath = context.Document.FilePath ?? (request != null ? request.DocumentPath ?? string.Empty : string.Empty);
            response.ProjectFilePath = context.Document.Project != null ? context.Document.Project.FilePath ?? string.Empty : string.Empty;
            response.DocumentVersion = request != null ? request.DocumentVersion : 0;
            response.SymbolDisplay = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
            response.QualifiedSymbolDisplay = GetQualifiedSymbolDisplay(symbol);
            response.SymbolKind = symbol.Kind.ToString();
            response.MetadataName = symbol.MetadataName ?? string.Empty;
            response.ContainingTypeName = GetContainingTypeName(symbol);
            response.ContainingAssemblyName = symbol.ContainingAssembly != null ? symbol.ContainingAssembly.Identity.Name : string.Empty;
            response.DocumentationCommentId = symbol.GetDocumentationCommentId() ?? string.Empty;
            response.DocumentationXml = symbol.GetDocumentationCommentXml() ?? string.Empty;
            response.DocumentationText = FlattenDocumentation(response.DocumentationXml);
            response.Range = BuildRange(context.Text, preferredLocation != null && preferredLocation.SourceTree == context.Document.GetSyntaxTreeAsync().Result
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
            if (symbol == null || solution == null)
            {
                return results;
            }

            if (includeDefinitions)
            {
                AddSymbolPrimaryLocations(results, solution, symbol, "Definition", seen);
            }

            var references = SymbolFinder.FindReferencesAsync(symbol, solution).Result;
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
                            IsWriteReference(solution, referenceLocation),
                            false));
                }
            }

            return results;
        }

        private void AddSymbolPrimaryLocations(IList<LanguageServiceSymbolLocation> results, Solution solution, ISymbol symbol, string relationship)
        {
            AddSymbolPrimaryLocations(results, solution, symbol, relationship, null);
        }

        private void AddSymbolPrimaryLocations(IList<LanguageServiceSymbolLocation> results, Solution solution, ISymbol symbol, string relationship, HashSet<string> seen)
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

                AddUniqueLocation(results, seen, ToProtocolLocation(solution, symbol, location, relationship, true, true, false, true));
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
                "|" + (range != null ? range.Length.ToString() : "0") +
                "|" + (location != null ? location.Relationship ?? string.Empty : string.Empty);
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

        private LanguageServiceCallHierarchyItem[] BuildIncomingCallItems(ISymbol symbol, Solution solution)
        {
            var items = new List<LanguageServiceCallHierarchyItem>();
            var callers = SymbolFinder.FindCallersAsync(symbol, solution).Result;
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

                    locations.Add(ToProtocolLocation(solution, caller.CallingSymbol, location, "Incoming Call", false, false, false, false));
                }

                items.Add(new LanguageServiceCallHierarchyItem
                {
                    SymbolDisplay = caller.CallingSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                    QualifiedSymbolDisplay = GetQualifiedSymbolDisplay(caller.CallingSymbol),
                    SymbolKind = caller.CallingSymbol.Kind.ToString(),
                    MetadataName = caller.CallingSymbol.MetadataName ?? string.Empty,
                    ContainingTypeName = GetContainingTypeName(caller.CallingSymbol),
                    ContainingAssemblyName = caller.CallingSymbol.ContainingAssembly != null ? caller.CallingSymbol.ContainingAssembly.Identity.Name : string.Empty,
                    DocumentationCommentId = caller.CallingSymbol.GetDocumentationCommentId() ?? string.Empty,
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

                var syntax = syntaxReference.GetSyntaxAsync().Result;
                var document = solution.GetDocument(syntax.SyntaxTree);
                if (document == null)
                {
                    continue;
                }

                var semanticModel = document.GetSemanticModelAsync().Result;
                if (semanticModel == null)
                {
                    continue;
                }

                CollectOutgoingSymbols(grouped, solution, semanticModel, syntax);
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
                    MetadataName = group.Symbol.MetadataName ?? string.Empty,
                    ContainingTypeName = GetContainingTypeName(group.Symbol),
                    ContainingAssemblyName = group.Symbol.ContainingAssembly != null ? group.Symbol.ContainingAssembly.Identity.Name : string.Empty,
                    DocumentationCommentId = group.Symbol.GetDocumentationCommentId() ?? string.Empty,
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
            SyntaxNode syntax)
        {
            if (grouped == null || solution == null || semanticModel == null || syntax == null)
            {
                return;
            }

            foreach (var invocation in syntax.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                AddOutgoingGroup(grouped, solution, semanticModel.GetSymbolInfo(invocation).Symbol, invocation.GetLocation());
            }

            foreach (var creation in syntax.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                AddOutgoingGroup(grouped, solution, semanticModel.GetSymbolInfo(creation).Symbol, creation.GetLocation());
            }
        }

        private void AddOutgoingGroup(IDictionary<string, OutgoingCallGroup> grouped, Solution solution, ISymbol symbol, Location location)
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

            group.Locations.Add(ToProtocolLocation(solution, symbol, location, "Outgoing Call", false, false, false, false));
        }

        private LanguageServiceValueSourceItem[] BuildValueSourceItems(ResolvedSymbolRequestContext context)
        {
            var items = new List<LanguageServiceValueSourceItem>();
            var declarationLocation = GetPreferredSourceLocation(context.Symbol, context.Document.GetSyntaxTreeAsync().Result);
            if (declarationLocation != null && declarationLocation.IsInSource)
            {
                items.Add(new LanguageServiceValueSourceItem
                {
                    FlowKind = "Declaration",
                    SymbolDisplay = context.Symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                    Relationship = "Declared here",
                    Location = ToProtocolLocation(context.Document.Project.Solution, context.Symbol, declarationLocation, "Declaration", true, true, false, true)
                });
            }

            var references = SymbolFinder.FindReferencesAsync(context.Symbol, context.Document.Project.Solution).Result;
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

                    if (!IsWriteReference(context.Document.Project.Solution, referenceLocation))
                    {
                        continue;
                    }

                    items.Add(new LanguageServiceValueSourceItem
                    {
                        FlowKind = "Write",
                        SymbolDisplay = context.Symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                        Relationship = "Assigned here",
                        Location = ToProtocolLocation(context.Document.Project.Solution, context.Symbol, referenceLocation.Location, "Value Source", false, false, true, false)
                    });
                }
            }

            return items.ToArray();
        }

        private static bool IsWriteReference(Solution solution, ReferenceLocation referenceLocation)
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

            var root = document.GetSyntaxRootAsync().Result;
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

        private LanguageServiceSymbolLocation ToProtocolLocation(
            Solution solution,
            ISymbol symbol,
            Location location,
            string relationship,
            bool isPrimary,
            bool isDefinition,
            bool isWrite,
            bool isDeclaration)
        {
            if (location == null || !location.IsInSource || location.SourceTree == null)
            {
                return new LanguageServiceSymbolLocation();
            }

            var text = location.SourceTree.GetText();
            var range = BuildRange(text, location.SourceSpan);
            var document = solution != null ? solution.GetDocument(location.SourceTree) : null;
            string lineText;
            string previewText;
            BuildLocationText(text, location.SourceSpan, out lineText, out previewText);
            return new LanguageServiceSymbolLocation
            {
                DocumentPath = location.SourceTree.FilePath ?? string.Empty,
                ProjectFilePath = document != null && document.Project != null ? document.Project.FilePath ?? string.Empty : string.Empty,
                SymbolDisplay = symbol != null ? symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) : string.Empty,
                SymbolKind = symbol != null ? symbol.Kind.ToString() : string.Empty,
                MetadataName = symbol != null ? symbol.MetadataName ?? string.Empty : string.Empty,
                ContainingTypeName = GetContainingTypeName(symbol),
                ContainingAssemblyName = symbol != null && symbol.ContainingAssembly != null ? symbol.ContainingAssembly.Identity.Name : string.Empty,
                DocumentationCommentId = symbol != null ? symbol.GetDocumentationCommentId() ?? string.Empty : string.Empty,
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
    }
}
