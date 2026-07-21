using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace ModAPI.Harmony
{
    /// <summary>
    /// High-level recipes for common call-site rewrites.
    /// </summary>
    public static class FluentTranspilerCallRecipes
    {
        /// <summary>
        /// Selects calls by declaring type and method name.
        /// </summary>
        /// <example>
        /// <code>
        /// t.ForCall(typeof(TargetUi), "BuildTooltip")
        ///  .ReplaceWith(typeof(MyHooks), "BuildTooltip");
        /// </code>
        /// </example>
        public static FluentCallSelection ForCall(this FluentTranspiler transpiler, Type sourceType, string sourceMethod)
        {
            return new FluentCallSelection(transpiler, sourceType, sourceMethod, null);
        }

        /// <summary>
        /// Selects calls by exact method.
        /// </summary>
        public static FluentCallSelection ForCall(this FluentTranspiler transpiler, MethodInfo sourceMethod)
        {
            return new FluentCallSelection(transpiler, sourceMethod?.DeclaringType, sourceMethod?.Name, sourceMethod);
        }

        /// <summary>
        /// Compatibility alias for replacing all calls to an exact method.
        /// </summary>
        public static FluentCallReplacementSelection ReplaceCalls(this FluentTranspiler transpiler, MethodInfo sourceMethod)
        {
            return new FluentCallReplacementSelection(transpiler, sourceMethod);
        }

        /// <summary>
        /// Redirects <b>every</b> call to <paramref name="source"/> to <paramref name="replacement"/>,
        /// pushing <paramref name="appendedLiteral"/> as an extra <b>trailing</b> argument immediately
        /// before each call. This is the batch, manifest-driven redirect helper: it replaces the
        /// hand-rolled "scan indices → insert a tag constant → replace-at index+1 → walk backwards"
        /// loop, and it also handles generic call sites (when <paramref name="source"/> is a generic
        /// method definition, each site's type arguments are applied to <paramref name="replacement"/>).
        /// </summary>
        /// <remarks>
        /// The replacement must be a static method whose parameters are the original call's stack inputs
        /// (including the instance for an instance call) followed by one parameter accepting the literal's
        /// type. Every matched site is signature-validated <b>before</b> any IL is mutated; a mismatch
        /// returns <see cref="FluentReplacementResult.UnsafeMatch"/> and leaves the stream untouched.
        /// Supported literal kinds: string, bool, integral types, char, float, double, long.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Redirect Random.Range(int,int) to Bridge.Range(int,int,string) tagging each site "map":
        /// t.RedirectCallsAppendingLiteral(RangeII, BridgeRangeIIWithDomain, "map");
        /// // Redirect the generic ExtensionMethods.Shuffle&lt;T&gt; to Bridge.Shuffle&lt;T&gt;(list, string):
        /// t.RedirectCallsAppendingLiteral(ShuffleDef, BridgeShuffleDef, "map");
        /// </code>
        /// </example>
        public static FluentReplacementResult RedirectCallsAppendingLiteral(
            this FluentTranspiler transpiler,
            MethodInfo source,
            MethodInfo replacement,
            object appendedLiteral,
            string editLabel = null)
        {
            const string caller = "RedirectCallsAppendingLiteral";
            if (transpiler == null)
            {
                return FluentReplacementResult.NoMatch;
            }

            if (source == null || replacement == null)
            {
                transpiler.AddWarning($"{caller} received a null {(source == null ? "source" : "replacement")} method.");
                return FluentReplacementResult.Failed;
            }

            CodeInstruction literalLoadTemplate;
            Type literalType;
            if (!FluentRecipeUtility.TryCreateLiteralLoad(appendedLiteral, out literalLoadTemplate, out literalType))
            {
                transpiler.AddWarning(
                    $"{caller} cannot push the appended argument for {FluentTranspilerFormatting.FormatMethod(replacement)}: " +
                    (appendedLiteral == null
                        ? "the literal was null."
                        : "unsupported literal type " + appendedLiteral.GetType().FullName + ".") +
                    " Fix: pass a string, bool, integral, char, float, double or long constant.");
                return FluentReplacementResult.Failed;
            }

            bool sourceIsGeneric = source.IsGenericMethodDefinition;

            var instructions = transpiler.Instructions().ToList();
            var callIndices = new List<int>();
            for (int i = 0; i < instructions.Count; i++)
            {
                CodeInstruction instruction = instructions[i];
                if (instruction == null ||
                    (instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt))
                {
                    continue;
                }

                MethodInfo call = instruction.operand as MethodInfo;
                if (call == null)
                {
                    continue;
                }

                bool matches = sourceIsGeneric
                    ? (call.IsGenericMethod && call.GetGenericMethodDefinition() == source)
                    : (call == source);
                if (matches)
                {
                    callIndices.Add(i);
                }
            }

            if (callIndices.Count == 0)
            {
                transpiler.AddSoftFailure(TranspilerDiagnosticCategory.Match,
                    $"{caller} found no calls to {FluentTranspilerFormatting.FormatMethod(source)}.");
                return FluentReplacementResult.NoMatch;
            }

            // Resolve + validate every call site up front so a single bad site aborts before any mutation.
            var concreteReplacements = new Dictionary<int, MethodInfo>();
            foreach (int callIndex in callIndices)
            {
                MethodInfo concreteSource = instructions[callIndex].operand as MethodInfo;
                MethodInfo concreteReplacement;
                if (sourceIsGeneric)
                {
                    Type[] typeArgs = concreteSource.GetGenericArguments();
                    if (!replacement.IsGenericMethodDefinition ||
                        replacement.GetGenericArguments().Length != typeArgs.Length)
                    {
                        transpiler.AddWarning(
                            $"{caller} replacement {FluentTranspilerFormatting.FormatMethod(replacement)} must be a generic method " +
                            $"definition with {typeArgs.Length} type parameter(s) to redirect generic {FluentTranspilerFormatting.FormatMethod(source)}.");
                        return FluentReplacementResult.UnsafeMatch;
                    }

                    try
                    {
                        concreteReplacement = replacement.MakeGenericMethod(typeArgs);
                    }
                    catch (Exception ex)
                    {
                        transpiler.AddWarning(
                            $"{caller} could not instantiate {FluentTranspilerFormatting.FormatMethod(replacement)} for the call-site type arguments: {ex.Message}.");
                        return FluentReplacementResult.UnsafeMatch;
                    }
                }
                else
                {
                    concreteReplacement = replacement;
                }

                if (!FluentTranspilerRecipeValidation.ValidateReplacementCallSignatureWithAppended(
                        transpiler, concreteSource, concreteReplacement, literalType, caller))
                {
                    return FluentReplacementResult.UnsafeMatch;
                }

                concreteReplacements[callIndex] = concreteReplacement;
            }

            // Apply back-to-front so each insertion leaves the remaining absolute indices stable.
            for (int i = callIndices.Count - 1; i >= 0; i--)
            {
                int callIndex = callIndices[i];
                transpiler.MoveTo(callIndex).InsertBefore(new CodeInstruction(literalLoadTemplate));
                transpiler.ReplaceAtWithCall(callIndex + 1, concreteReplacements[callIndex]);
            }

            if (!string.IsNullOrEmpty(editLabel))
            {
                transpiler.AddNote($"{editLabel}: redirected {callIndices.Count} call(s) to {FluentTranspilerFormatting.FormatMethod(source)} with an appended {literalType.Name}.");
            }

            return FluentReplacementResult.PatternReplaced;
        }
    }

    public sealed class FluentCallSelection
    {
        private readonly FluentTranspiler _transpiler;
        private readonly Type _sourceType;
        private readonly string _sourceMethod;
        private readonly MethodInfo _exactSourceMethod;
        private Func<IList<CodeInstruction>, int, bool> _surroundingPredicate;

        internal FluentCallSelection(
            FluentTranspiler transpiler,
            Type sourceType,
            string sourceMethod,
            MethodInfo exactSourceMethod)
        {
            _transpiler = transpiler;
            _sourceType = sourceType;
            _sourceMethod = sourceMethod;
            _exactSourceMethod = exactSourceMethod;
        }

        /// <summary>
        /// Restricts the call recipe to a caller-supplied surrounding pattern.
        /// </summary>
        /// <example>
        /// <code>
        /// t.ForCall(typeof(SomeType), "ComputeValue")
        ///  .WhenSurroundedBy((instructions, callIndex) => instructions[callIndex - 1].IsLdcI4(4))
        ///  .ReplaceWith(typeof(MyHooks), "ComputeValue");
        /// </code>
        /// </example>
        public FluentCallSelection WhenSurroundedBy(Func<IList<CodeInstruction>, int, bool> predicate)
        {
            _surroundingPredicate = predicate;
            return this;
        }

        /// <summary>
        /// Replaces the first matching call with a static hook call.
        /// </summary>
        public FluentReplacementResult ReplaceWith(Type targetType, string targetMethod, Type[] parameterTypes = null, SearchMode mode = SearchMode.Start)
        {
            return ReplaceWith(FluentRecipeUtility.ResolveStaticMethod(targetType, targetMethod, parameterTypes), mode);
        }

        /// <summary>
        /// Replaces the first matching call with a static hook call.
        /// </summary>
        public FluentReplacementResult ReplaceWith(MethodInfo replacementMethod, SearchMode mode = SearchMode.Start)
        {
            if (!IsValid("ReplaceWith"))
            {
                return FluentReplacementResult.Failed;
            }

            var candidates = FindCallIndexes(mode);
            if (!TryGetSingleCandidate(candidates, "ReplaceWith", out int callIndex))
            {
                return candidates.Count == 0 ? FluentReplacementResult.NoMatch : FluentReplacementResult.AmbiguousMatch;
            }

            MethodInfo sourceMethod = GetCallMethod(callIndex);
            if (!FluentTranspilerRecipeValidation.ValidateReplacementCallSignature(
                _transpiler,
                sourceMethod,
                replacementMethod,
                "ForCall.ReplaceWith"))
            {
                return FluentReplacementResult.UnsafeMatch;
            }

            _transpiler.ReplaceAtWithCall(callIndex, replacementMethod);
            return FluentReplacementResult.PatternReplaced;
        }

        /// <summary>
        /// Replaces every matching call with a static hook call.
        /// </summary>
        public FluentReplacementResult ReplaceAllWith(Type targetType, string targetMethod, Type[] parameterTypes = null, SearchMode mode = SearchMode.Start)
        {
            return ReplaceAllWith(FluentRecipeUtility.ResolveStaticMethod(targetType, targetMethod, parameterTypes), mode);
        }

        /// <summary>
        /// Replaces every matching call with a static hook call.
        /// </summary>
        public FluentReplacementResult ReplaceAllWith(MethodInfo replacementMethod, SearchMode mode = SearchMode.Start)
        {
            if (!IsValid("ReplaceAllWith"))
            {
                return FluentReplacementResult.Failed;
            }

            var candidates = FindCallIndexes(mode);
            if (candidates.Count == 0)
            {
                _transpiler.AddSoftFailure($"ForCall found no calls to {_sourceType?.FullName}.{_sourceMethod}.");
                return FluentReplacementResult.NoMatch;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                MethodInfo sourceMethod = GetCallMethod(candidates[i]);
                if (!FluentTranspilerRecipeValidation.ValidateReplacementCallSignature(
                    _transpiler,
                    sourceMethod,
                    replacementMethod,
                    "ForCall.ReplaceAllWith"))
                {
                    return FluentReplacementResult.UnsafeMatch;
                }
            }

            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                _transpiler.ReplaceAtWithCall(candidates[i], replacementMethod);
            }

            return FluentReplacementResult.PatternReplaced;
        }

        /// <summary>
        /// Injects a parameterless static hook immediately before the first matching call.
        /// </summary>
        /// <example>
        /// <code>
        /// t.ForCall(typeof(TargetUi), "DrawPanel")
        ///  .InjectBefore(typeof(MyHooks), "BeforeDrawPanel");
        /// </code>
        /// </example>
        public FluentReplacementResult InjectBefore(Type hookType, string hookMethod, SearchMode mode = SearchMode.Start)
        {
            return InjectBefore(FluentRecipeUtility.ResolveStaticMethod(hookType, hookMethod, null), mode);
        }

        /// <summary>
        /// Injects a parameterless static hook immediately before the first matching call.
        /// </summary>
        public FluentReplacementResult InjectBefore(MethodInfo hookMethod, SearchMode mode = SearchMode.Start)
        {
            if (!IsVoidParameterlessHook(hookMethod, "InjectBefore"))
            {
                return FluentReplacementResult.UnsafeMatch;
            }

            var candidates = FindCallIndexes(mode);
            if (!TryGetSingleCandidate(candidates, "InjectBefore", out int callIndex))
            {
                return candidates.Count == 0 ? FluentReplacementResult.NoMatch : FluentReplacementResult.AmbiguousMatch;
            }

            _transpiler.MoveTo(callIndex).InsertBefore(new CodeInstruction(OpCodes.Call, hookMethod));
            return FluentReplacementResult.PatternReplaced;
        }

        /// <summary>
        /// Injects a parameterless static hook immediately after the first matching call.
        /// </summary>
        public FluentReplacementResult InjectAfter(Type hookType, string hookMethod, SearchMode mode = SearchMode.Start)
        {
            return InjectAfter(FluentRecipeUtility.ResolveStaticMethod(hookType, hookMethod, null), mode);
        }

        /// <summary>
        /// Injects a parameterless static hook immediately after the first matching call.
        /// </summary>
        public FluentReplacementResult InjectAfter(MethodInfo hookMethod, SearchMode mode = SearchMode.Start)
        {
            if (!IsVoidParameterlessHook(hookMethod, "InjectAfter"))
            {
                return FluentReplacementResult.UnsafeMatch;
            }

            var candidates = FindCallIndexes(mode);
            if (!TryGetSingleCandidate(candidates, "InjectAfter", out int callIndex))
            {
                return candidates.Count == 0 ? FluentReplacementResult.NoMatch : FluentReplacementResult.AmbiguousMatch;
            }

            _transpiler.MoveTo(callIndex).InsertAfter(new CodeInstruction(OpCodes.Call, hookMethod));
            return FluentReplacementResult.PatternReplaced;
        }

        /// <summary>
        /// Passes the first matching call result through a static wrapper method.
        /// </summary>
        /// <example>
        /// <code>
        /// t.ForCall(typeof(SomeType), "ComputeValue")
        ///  .WrapReturnValue(typeof(MyHooks), "AdjustValue");
        /// </code>
        /// </example>
        public FluentReplacementResult WrapReturnValue(Type wrapperType, string wrapperMethod, SearchMode mode = SearchMode.Start)
        {
            return WrapReturnValue(FluentRecipeUtility.ResolveStaticMethod(wrapperType, wrapperMethod, null), mode);
        }

        /// <summary>
        /// Passes the first matching call result through a static wrapper method.
        /// The wrapper must accept and return the original call return type.
        /// </summary>
        public FluentReplacementResult WrapReturnValue(MethodInfo wrapperMethod, SearchMode mode = SearchMode.Start)
        {
            var candidates = FindCallIndexes(mode);
            if (!TryGetSingleCandidate(candidates, "WrapReturnValue", out int callIndex))
            {
                return candidates.Count == 0 ? FluentReplacementResult.NoMatch : FluentReplacementResult.AmbiguousMatch;
            }

            MethodInfo source = ((CodeInstruction)_transpiler.Instructions().ElementAt(callIndex)).operand as MethodInfo;
            if (!FluentTranspilerRecipeValidation.ValidateWrapperSignature(
                _transpiler,
                wrapperMethod,
                source?.ReturnType,
                source?.ReturnType,
                null,
                "ForCall.WrapReturnValue"))
            {
                return FluentReplacementResult.UnsafeMatch;
            }

            _transpiler.MoveTo(callIndex).InsertAfter(new CodeInstruction(OpCodes.Call, wrapperMethod));
            return FluentReplacementResult.PatternReplaced;
        }

        private bool IsValid(string caller)
        {
            if (_transpiler == null)
            {
                return false;
            }

            if (_sourceType == null || string.IsNullOrEmpty(_sourceMethod))
            {
                _transpiler.AddWarning($"{caller} received a null source call.");
                return false;
            }

            return true;
        }

        private bool IsVoidParameterlessHook(MethodInfo hookMethod, string caller)
        {
            if (_transpiler == null)
            {
                return false;
            }

            if (hookMethod == null ||
                !hookMethod.IsStatic ||
                hookMethod.ReturnType != typeof(void) ||
                hookMethod.GetParameters().Length != 0)
            {
                _transpiler.AddWarning($"{caller} expected a parameterless static void hook, got {FluentTranspilerFormatting.FormatMethod(hookMethod)}.");
                return false;
            }

            return true;
        }

        private List<int> FindCallIndexes(SearchMode mode)
        {
            IList<CodeInstruction> instructions = _transpiler.Instructions().ToList();
            int startIndex = FluentRecipeUtility.GetSearchStartIndex(_transpiler, mode);
            var matches = new List<int>();
            for (int i = startIndex; i < instructions.Count; i++)
            {
                if (!MatchesSourceCall(instructions[i]))
                {
                    continue;
                }

                if (_surroundingPredicate != null && !_surroundingPredicate(instructions, i))
                {
                    continue;
                }

                matches.Add(i);
            }

            return matches;
        }

        private bool MatchesSourceCall(CodeInstruction instruction)
        {
            if (instruction == null ||
                (instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt) ||
                !(instruction.operand is MethodInfo method))
            {
                return false;
            }

            if (_exactSourceMethod != null)
            {
                return method == _exactSourceMethod;
            }

            return method.Name == _sourceMethod &&
                   (method.DeclaringType == _sourceType ||
                    _sourceType.IsAssignableFrom(method.DeclaringType) ||
                    method.DeclaringType?.FullName == _sourceType.FullName);
        }

        private MethodInfo GetCallMethod(int callIndex)
        {
            CodeInstruction instruction = _transpiler.Instructions().ElementAt(callIndex);
            return instruction != null ? instruction.operand as MethodInfo : null;
        }

        private bool TryGetSingleCandidate(List<int> candidates, string caller, out int callIndex)
        {
            callIndex = -1;
            if (candidates.Count == 1)
            {
                callIndex = candidates[0];
                return true;
            }

            if (candidates.Count == 0)
            {
                _transpiler.AddSoftFailure($"{caller} found no calls to {_sourceType?.FullName}.{_sourceMethod}.");
                return false;
            }

            _transpiler.AddWarning($"{caller} found {candidates.Count} calls to {_sourceType?.FullName}.{_sourceMethod}; use ReplaceAllWith or WhenSurroundedBy to make the intent precise.");
            return false;
        }
    }

    public sealed class FluentCallReplacementSelection
    {
        private readonly FluentTranspiler _transpiler;
        private readonly MethodInfo _sourceMethod;
        private string _name;
        private string _editLabel;
        private string _failureMessage;
        private bool _optional;
        private bool _strict;

        internal FluentCallReplacementSelection(FluentTranspiler transpiler, MethodInfo sourceMethod)
        {
            _transpiler = transpiler;
            _sourceMethod = sourceMethod;
        }

        public FluentCallReplacementSelection Named(string name)
        {
            _name = name;
            return this;
        }

        public FluentCallReplacementSelection WithEditLabel(string editLabel)
        {
            _editLabel = editLabel;
            return this;
        }

        public FluentCallReplacementSelection WithFailureMessage(string failureMessage)
        {
            _failureMessage = failureMessage;
            return this;
        }

        public FluentCallReplacementSelection RequireSingleMatch()
        {
            return this;
        }

        public FluentCallReplacementSelection Optional()
        {
            _optional = true;
            return this;
        }

        public FluentCallReplacementSelection Strict()
        {
            _strict = true;
            return this;
        }

        /// <summary>
        /// Replaces all calls to the exact source method with <paramref name="replacementMethod"/>.
        /// </summary>
        public FluentReplacementResult WithCall(MethodInfo replacementMethod, string editLabel = null)
        {
            if (_transpiler == null)
            {
                return FluentReplacementResult.NoMatch;
            }

            if (_sourceMethod == null)
            {
                _transpiler.AddWarning("ReplaceCalls received a null source method. Fix: the MethodInfo you passed to ReplaceCalls(...) resolved to null — check the AccessTools.Method/PropertyGetter lookup (name, declaring type, and overload parameter types) that produced it.");
                AddPatchDiagnostic(
                    "source method call to replace.",
                    "source method was null.",
                    "patch skipped; original IL returned.",
                    FluentPatchSeverity.Critical,
                    editLabel);
                return FluentReplacementResult.Failed;
            }

            if (!FluentTranspilerRecipeValidation.ValidateReplacementCallSignature(
                _transpiler,
                _sourceMethod,
                replacementMethod,
                "ReplaceCalls.WithCall"))
            {
                AddPatchDiagnostic(
                    "replacement call with a stack-compatible static signature for " + FluentTranspilerFormatting.FormatMethod(_sourceMethod) + ".",
                    "signature mismatch for replacement " + FluentTranspilerFormatting.FormatMethod(replacementMethod) + ".",
                    "patch skipped; original IL returned.",
                    FluentPatchSeverity.Critical,
                    editLabel);
                return FluentReplacementResult.UnsafeMatch;
            }

            int matchingCalls = CountSourceCalls();
            int replaced = _transpiler.ReplaceMatchingCalls(
                method => method == _sourceMethod,
                replacementMethod,
                EffectiveEditLabel(editLabel) ?? $"Replace calls to {FluentTranspilerFormatting.FormatMethod(_sourceMethod)}");

            if (replaced > 0)
            {
                return FluentReplacementResult.PatternReplaced;
            }

            if (replacementMethod != null && _transpiler.HasMatchingCall(method => method == replacementMethod))
            {
                AddPatchDiagnostic(
                    "call to " + FluentTranspilerFormatting.FormatMethod(_sourceMethod) + ".",
                    "replacement call already present: " + FluentTranspilerFormatting.FormatMethod(replacementMethod) + ".",
                    "patch already applied; no IL changes made.",
                    FluentPatchSeverity.Info,
                    editLabel);
                return FluentReplacementResult.ReplacementAlreadyPresent;
            }

            _transpiler.AddSoftFailure($"ReplaceCalls found no calls to {FluentTranspilerFormatting.FormatMethod(_sourceMethod)}.");
            AddPatchDiagnostic(
                "call to " + FluentTranspilerFormatting.FormatMethod(_sourceMethod) + ".",
                "found " + matchingCalls + " matching call(s).",
                "patch skipped; original IL returned.",
                Severity(),
                editLabel);
            return FluentReplacementResult.NoMatch;
        }

        private int CountSourceCalls()
        {
            if (_transpiler == null || _sourceMethod == null)
            {
                return 0;
            }

            return _transpiler.Instructions().Count(instruction =>
                instruction != null &&
                (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) &&
                instruction.operand is MethodInfo method &&
                method == _sourceMethod);
        }

        private void AddPatchDiagnostic(
            string expected,
            string found,
            string action,
            FluentPatchSeverity severity,
            string fallbackEditLabel)
        {
            if (_transpiler == null)
            {
                return;
            }

            _transpiler.AddPatchDiagnostic(new FluentPatchDiagnostic
            {
                RecipeName = RecipeText(),
                ExpectedShape = !string.IsNullOrEmpty(_failureMessage) ? _failureMessage : expected,
                FoundShape = found,
                ActionTaken = action,
                Severity = severity,
                EditLabel = EffectiveEditLabel(fallbackEditLabel)
            });
        }

        private FluentPatchSeverity Severity()
        {
            return _strict ? FluentPatchSeverity.Critical : (_optional ? FluentPatchSeverity.Info : FluentPatchSeverity.Warning);
        }

        private string EffectiveEditLabel(string fallbackEditLabel)
        {
            if (!string.IsNullOrEmpty(_editLabel))
            {
                return _editLabel;
            }

            if (!string.IsNullOrEmpty(fallbackEditLabel))
            {
                return fallbackEditLabel;
            }

            return _name;
        }

        private string RecipeText()
        {
            return "ReplaceCalls(" + FluentTranspilerFormatting.FormatMethod(_sourceMethod) + ").WithCall(...)";
        }
    }
}
