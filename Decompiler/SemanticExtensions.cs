using System;
using System.Collections.Generic;
using System.Linq;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.Semantics;
using ICSharpCode.Decompiler.Util;
using ICSharpCode.Decompiler.TypeSystem;

namespace ModAPI.Decompiler
{
    /// <summary>
    /// Provides extension methods and semantic analysis helpers for ICSharpCode.Decompiler v8.
    /// Follows Single Responsibility Principle by separating analysis logic from the DecompilerService.
    /// </summary>
    public static class SemanticExtensions
    {
        /* 
         * ARCHITECTURAL NOTE (v8 Discovery):
         * The ICSharpCode.Decompiler v8 library contains several advanced features we can leverage:
         * 1. Semantic Pattern Matching: Use 'ICSharpCode.Decompiler.IL.Patterns' for structure-aware patching.
         * 2. Type Resolution: Access 'ResolveResult' via node annotations to see exactly what types the compiler inferred.
         * 3. Control Flow Analysis: 'ICSharpCode.Decompiler.IL.ControlFlow' can detect loops, switches, and nested blocks.
         * 4. Sequence Points: 'SequencePointBuilder' maps IL to original source line/column precisely.
         */

        /// <summary>
        /// Retrieves the ResolveResult for a node.
        /// This reveals the EXACT type the decompiler inferred for any expression or variable.
        /// Useful for the VariableInspector to show real types rather than just 'object'.
        /// </summary>
        public static ResolveResult? GetResolveResult(this AstNode node)
        {
            if (node == null) return null;
            // Annotations are added during decompiler.Decompile(handle)
            return node.Annotation<ResolveResult>();
        }

        /// <summary>
        /// Retrieves the ISymbol (Method, Property, Field) for a node.
        /// This can be used to jump to definitions or identify backing fields.
        /// </summary>
        public static ISymbol? GetSymbol(this AstNode node)
        {
            var rr = node.GetResolveResult();
            return rr?.GetSymbol();
        }

        /// <summary>
        /// Bi-directional Mapping Helper.
        /// Given an IL Offset, finds the most specific SyntaxNode that covers it.
        /// This allows the 'Click instruction -> Highlight C#' feature.
        /// </summary>
        public static AstNode? FindNodeByILOffset(this AstNode root, int ilOffset)
        {
            if (root == null) return null;
            return root.Descendants.FirstOrDefault(n => {
                var range = n.Annotation(typeof(Interval)) is Interval i ? i : default(Interval);
                return !range.IsEmpty && ilOffset >= range.Start && ilOffset < range.End;
            });
        }

        /// <summary>
        /// Gets all IL Intervals associated with this node.
        /// Handles both single Interval annotations and List<Interval> annotations.
        /// </summary>
        public static IEnumerable<Interval> GetILRanges(this AstNode node)
        {
            if (node == null) yield break;

            if (node.Annotation(typeof(Interval)) is Interval single && !single.IsEmpty)
                yield return single;

            var list = node.Annotation<List<Interval>>();
            if (list != null)
            {
                foreach (var interval in list)
                {
                    if (!interval.IsEmpty)
                        yield return interval;
                }
            }
        }
    }
}
