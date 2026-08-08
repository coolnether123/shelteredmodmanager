using System.Collections.Generic;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.Util;

namespace ModAPI.Decompiler
{
    /// <summary>
    /// Provides the source-to-IL range extraction used by the decompiler engine.
    /// </summary>
    public static class SemanticExtensions
    {
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
