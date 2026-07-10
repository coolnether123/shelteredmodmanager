using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using ModAPI.Core;

namespace ModAPI.Harmony
{
    public enum SearchMode
    {
        Start,   // Resets to beginning before searching
        Current, // Searches forward from current position (inclusive)
        Next     // Advances 1 then searches forward (for sequential matching)
    }

    public enum TranspilerDiagnosticSeverity
    {
        Note,
        SoftFailure,
        Warning
    }

    public enum TranspilerDiagnosticCategory
    {
        General,
        Match,
        Validation,
        Safety,
        Trace
    }

    public enum FluentPatchSeverity
    {
        Info,
        Warning,
        Critical
    }

    public sealed class TranspilerDiagnostic
    {
        public TranspilerDiagnosticSeverity Severity { get; set; }
        public TranspilerDiagnosticCategory Category { get; set; }
        public string Message { get; set; }

        public override string ToString()
        {
            return $"[{Severity}:{Category}] {Message}";
        }
    }

    public sealed class FluentPatchDiagnostic
    {
        public string RecipeName { get; set; }
        public string TargetMethod { get; set; }
        public string ExpectedShape { get; set; }
        public string FoundShape { get; set; }
        public string ActionTaken { get; set; }
        public FluentPatchSeverity Severity { get; set; }
        public string OwnerMod { get; set; }
        public string EditLabel { get; set; }

        public string ToSingleLine()
        {
            return Compact(ToString());
        }

        public override string ToString()
        {
            var lines = new List<string>();
            string target = string.IsNullOrEmpty(TargetMethod) ? "<unknown method>" : TargetMethod;
            lines.Add("Could not patch " + target + ".");

            if (!string.IsNullOrEmpty(EditLabel))
            {
                lines.Add("Patch: " + EditLabel);
            }

            if (!string.IsNullOrEmpty(RecipeName))
            {
                lines.Add("Recipe: " + RecipeName);
            }

            if (!string.IsNullOrEmpty(ExpectedShape))
            {
                lines.Add("Expected: " + ExpectedShape);
            }

            if (!string.IsNullOrEmpty(FoundShape))
            {
                lines.Add("Found: " + FoundShape);
            }

            if (!string.IsNullOrEmpty(ActionTaken))
            {
                lines.Add("Action: " + ActionTaken);
            }

            if (!string.IsNullOrEmpty(OwnerMod))
            {
                lines.Add("Owner: " + OwnerMod + ".");
            }

            return string.Join("\n", lines.ToArray());
        }

        private static string Compact(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\r", string.Empty).Replace("\n", " ");
        }
    }

    /// <summary>
    /// Fluent wrapper around Harmony transpilers for game and mod patch code.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The framework is built around a short lifecycle: open a session for the Harmony IL stream,
    /// find a stable anchor in the target method, apply a focused edit, and finish through
    /// <see cref="Build"/> so validation and diagnostics happen in one place.
    /// </para>
    /// <para>
    /// Most patches should use <see cref="Execute"/> because it wraps that lifecycle in the
    /// normal Harmony transpiler shape. Use <see cref="For"/> when a patch needs explicit
    /// control over when <see cref="Build"/> runs or which validation mode is used.
    /// </para>
    /// <para>
    /// The goal is to keep patch intent readable to developers who understand the target game
    /// logic, without forcing every patch to manually manage labels, branch targets, and stack checks.
    /// </para>
    /// </remarks>
    public partial class FluentTranspiler
    {
        public enum BuildProfile
        {
            Runtime,
            Strict,
            Debug
        }

        private struct StackExpectation
        {
            public int index;
            public int expectedDepth;
        }

        private readonly CodeMatcher _matcher;
        private readonly List<TranspilerDiagnostic> _diagnostics = new List<TranspilerDiagnostic>();
        private readonly List<FluentPatchDiagnostic> _patchDiagnostics = new List<FluentPatchDiagnostic>();
        private readonly List<StackExpectation> _stackExpectations = new List<StackExpectation>();
        private readonly MethodBase _originalMethod;
        private readonly ILGenerator _generator;
        private readonly string _callerMod;  // For logging context
        private readonly System.Diagnostics.Stopwatch _stopwatch;
        private readonly List<CodeInstruction> _initialInstructions;
        private readonly List<TranspilerDebugger.PatchEdit> _patchEdits = new List<TranspilerDebugger.PatchEdit>();
        private readonly Dictionary<Label, int> _labelIndexCache = new Dictionary<Label, int>();
        private readonly List<string> _traceMessages = new List<string>();
        private BuildProfile _buildProfile = BuildProfile.Runtime;
        private bool _labelIndexCacheDirty = true;

        private FluentTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase originalMethod = null, ILGenerator generator = null)
        {
            // Cache initial state for diff/timing. 
            // CRITICAL: We MUST buffer the enumerable here because it might be spent 
            // by the time the matcher is initialized if we use it directly.
            var instructionsList = instructions as List<CodeInstruction> ?? instructions.ToList();
            
            _initialInstructions = instructionsList.Select(i => new CodeInstruction(i)).ToList(); // Deep copy initial state
            _stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            _matcher = new CodeMatcher(instructionsList, generator);
            _originalMethod = originalMethod;
            _generator = generator;
            _callerMod = ResolveCallingModName();
        }

        /// <summary>
        /// Creates a fluent transpiler session for a Harmony instruction stream.
        /// </summary>
        /// <remarks>
        /// Use this entry point when a patch needs manual control over the session lifetime, such as
        /// choosing strict validation rules or building in multiple stages. For ordinary game patches,
        /// <see cref="Execute"/> is the simpler entry point.
        /// </remarks>
        /// <param name="instructions">The raw IL instructions provided by the Harmony transpiler delegate.</param>
        /// <param name="originalMethod">The method being patched. Providing this enables advanced <see cref="StackSentinel"/> validation.</param>
        /// <param name="generator">The ILGenerator from the transpiler signature. Required if you intend to use <c>DefineLabel</c> or <c>DeclareLocal</c>.</param>
        /// <returns>A new <see cref="FluentTranspiler"/> instance focused on the provided method.</returns>
        public static FluentTranspiler For(IEnumerable<CodeInstruction> instructions, MethodBase originalMethod = null, ILGenerator generator = null)
        {
            return new FluentTranspiler(instructions, originalMethod, generator);
        }

        /// <summary>
        /// Standard object equality. 
        /// </summary>
        /// <remarks>
        /// <b>Note:</b> This is a standard C# reference equality check. 
        /// It is NOT a transpiler matching command. To match an IL sequence, 
        /// use <see cref="Matches"/> or <see cref="MatchIntent"/>.
        /// </remarks>
        public new bool Equals(object obj) => base.Equals(obj);

        /// <summary>
        /// Runs the standard fluent transpiler lifecycle for a Harmony patch.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is the normal entry point for fluent transpilers. It creates the session, runs the
        /// caller's transform, and then finishes through <see cref="Build"/> with the framework's default
        /// non-strict validation policy.
        /// </para>
        /// <para>
        /// The callback should stay focused on IL intent: find a stable anchor in the target method,
        /// assert the match, and apply the smallest safe edit that redirects or replaces that behavior.
        /// </para>
        /// <para>
        /// <b>Usage Example:</b>
        /// <code>
        /// [HarmonyTranspiler]
        /// public static IEnumerable&lt;CodeInstruction&gt; Transpiler(IEnumerable&lt;CodeInstruction&gt; instructions, MethodBase original)
        /// {
        ///     return FluentTranspiler.Execute(instructions, original, null, t => {
        ///         t.MatchCall(typeof(Console), "WriteLine")
        ///          .ReplaceWith(OpCodes.Nop);
        ///     });
        /// }
        /// </code>
        /// </para>
        /// </remarks>
        /// <param name="instructions">
        /// The IL instruction stream supplied by Harmony for the method currently being patched.
        /// This is the method body that the fluent session will search and rewrite.
        /// </param>
        /// <param name="original">
        /// The game or mod method that produced <paramref name="instructions"/>.
        /// This is used for stack analysis, safety checks, diagnostics, and debug snapshots.
        /// </param>
        /// <param name="generator">
        /// The <see cref="ILGenerator"/> passed into the Harmony transpiler signature.
        /// Provide this when the patch needs to define labels or declare locals; otherwise <c>null</c> is valid.
        /// </param>
        /// <param name="transformer">
        /// The callback that receives the active <see cref="FluentTranspiler"/> session.
        /// Place all matching, insertion, replacement, and validation calls inside this lambda.
        /// </param>
        /// <returns>A modified instruction stream ready for Harmony consumption.</returns>
        public static IEnumerable<CodeInstruction> Execute(
            IEnumerable<CodeInstruction> instructions,
            MethodBase original,
            ILGenerator generator,
            Action<FluentTranspiler> transformer)
        {
            return Execute(
                instructions,
                original,
                generator,
                TranspilerSafetyPolicy.DefaultExecuteProfile,
                transformer);
        }

        /// <summary>
        /// Runs the fluent transpiler lifecycle with an explicit build profile.
        /// Use <see cref="BuildProfile.Debug"/> only for deliberate troubleshooting; runtime patches
        /// should stay on <see cref="BuildProfile.Runtime"/> so successful patches do not log noise.
        /// </summary>
        public static IEnumerable<CodeInstruction> Execute(
            IEnumerable<CodeInstruction> instructions,
            MethodBase original,
            ILGenerator generator,
            BuildProfile profile,
            Action<FluentTranspiler> transformer)
        {
            var transpiler = For(instructions, original, generator);
            transpiler._buildProfile = profile;
            transformer(transpiler);
            return transpiler.Build(profile);
        }
        /// <summary>
        /// Power-search for a method call using a high-level API.
        /// </summary>
        /// <param name="type">The declaring type (class) of the method.</param>
        /// <param name="methodName">The name of the method.</param>
        /// <param name="mode">Whether to start from the beginning of the method or continue from current position.</param>
        /// <param name="parameterTypes">Optional types for overload resolution.</param>
        /// <param name="genericArguments">Optional types for generic methods.</param>
        /// <param name="includeInherited">If true, matches methods defined in base classes.</param>
        public FluentTranspiler FindCall(Type type, string methodName, SearchMode mode = SearchMode.Start, Type[] parameterTypes = null, Type[] genericArguments = null, bool includeInherited = true)
        {
            if (mode == SearchMode.Start) _matcher.Start();
            else if (mode == SearchMode.Next) _matcher.Advance(1);
            
            return MatchCallForward(type, methodName, parameterTypes, genericArguments, includeInherited);
        }

        /// <summary>Match a method call by type and name. Supports overload and generic resolution.</summary>
        public FluentTranspiler MatchCall(Type type, string methodName, Type[] parameterTypes = null, Type[] genericArguments = null, bool includeInherited = true)
        {
            return FindCall(type, methodName, SearchMode.Start, parameterTypes, genericArguments, includeInherited);
        }

        /// <summary>
        /// Match a property getter call. 
        /// Automatically handles both Call and Callvirt opcodes.
        /// </summary>
        /// <param name="type">Declaring type of the property.</param>
        /// <param name="propertyName">Name of the property (without "get_" prefix).</param>
        public FluentTranspiler MatchPropertyGetter(Type type, string propertyName)
        {
            return MatchCall(type, "get_" + propertyName, includeInherited: true);
        }

        /// <summary>Continue matching from current position (for sequential matches).</summary>
        public FluentTranspiler MatchCallNext(Type type, string methodName, Type[] parameterTypes = null, Type[] genericArguments = null, bool includeInherited = true)
        {
            return FindCall(type, methodName, SearchMode.Next, parameterTypes, genericArguments, includeInherited);
        }

        private FluentTranspiler MatchCallForward(Type type, string methodName, Type[] parameterTypes, Type[] genericArguments, bool includeInherited)
        {
            var predicate = BuildCallPredicate(type, methodName, parameterTypes, genericArguments, includeInherited);
            int preMatch = _matcher.Pos;
            _matcher.MatchStartForward(new CodeMatch(predicate));

            if (!_matcher.IsValid)
            {
                string details = (genericArguments != null ? $"<{string.Join(", ", genericArguments.Select(t => t.Name).ToArray())}>" : "") +
                                 (parameterTypes != null ? $"({string.Join(", ", parameterTypes.Select(t => t.Name).ToArray())})" : "");
                AddSoftFailure($"No match for call {type.Name}.{methodName}{details}");
            }
            else
            {
                LogTrace($"[FluentTranspiler] MatchCall: Found {type.Name}.{methodName} at index {_matcher.Pos}");
            }
            return this;
        }

        private Func<CodeInstruction, bool> BuildCallPredicate(Type type, string methodName, Type[] parameterTypes = null, Type[] genericArguments = null, bool includeInherited = true)
        {
            return instr =>
            {
                if (instr.opcode != OpCodes.Call && instr.opcode != OpCodes.Callvirt)
                    return false;
                if (!(instr.operand is MethodInfo method))
                    return false;

                bool typeMatch = includeInherited
                    ? method.DeclaringType == type
                      || type.IsAssignableFrom(method.DeclaringType)
                      || method.DeclaringType.FullName == type.FullName
                    : method.DeclaringType == type
                      || method.DeclaringType.FullName == type.FullName;

                if (!typeMatch || method.Name != methodName) return false;

                if (genericArguments != null)
                {
                    if (!method.IsGenericMethod) return false;
                    var args = method.GetGenericArguments();
                    if (args.Length != genericArguments.Length) return false;
                    for (int i = 0; i < args.Length; i++)
                    {
                        if (!args[i].Equals(genericArguments[i]) &&
                            args[i].Name != genericArguments[i].Name &&
                            !(args[i].IsGenericParameter && genericArguments[i].IsGenericParameter))
                            return false;
                    }
                }

                if (parameterTypes != null)
                {
                    var ps = method.GetParameters();
                    if (ps.Length != parameterTypes.Length) return false;
                    for (int i = 0; i < parameterTypes.Length; i++)
                    {
                        if (ps[i].ParameterType != parameterTypes[i] &&
                            ps[i].ParameterType.FullName != parameterTypes[i].FullName)
                            return false;
                    }
                }
                return true;
            };
        }

        /// <summary>Unified method to find an OpCode.</summary>
        public FluentTranspiler FindOpCode(OpCode opcode, SearchMode mode = SearchMode.Start)
        {
            if (mode == SearchMode.Start) _matcher.Start();
            else if (mode == SearchMode.Next) _matcher.Advance(1);

            _matcher.MatchStartForward(new CodeMatch(opcode));
            
            if (!_matcher.IsValid)
            {
                AddSoftFailure($"No match for opcode {opcode}");
            }
            
            return this;
        }

        /// <summary>Match by OpCode.</summary>
        public FluentTranspiler MatchOpCode(OpCode opcode)
        {
            return FindOpCode(opcode, SearchMode.Start);
        }

        /// <summary>Match the return instruction (OpCodes.Ret).</summary>
        public FluentTranspiler MatchReturn()
        {
            return MatchOpCode(OpCodes.Ret);
        }

        /// <summary>Match a sequence of opcodes (pattern matching).</summary>
        /// <summary>Unified method to find a sequence of opcodes.</summary>
        public FluentTranspiler FindSequence(SearchMode mode, params OpCode[] opcodes)
        {
            if (mode == SearchMode.Start) _matcher.Start();
            else if (mode == SearchMode.Next) _matcher.Advance(1);

            var matches = opcodes.Select(op => new CodeMatch(op)).ToArray();
            _matcher.MatchStartForward(matches);
            
            if (!_matcher.IsValid)
            {
                AddSoftFailure($"Sequence not found: {string.Join(" -> ", opcodes.Select(o => o.Name).ToArray())}");
            }

            return this;
        }

        /// <summary>
        /// Finds a sequence using instruction predicates. Use this for IL patterns that depend
        /// on operand semantics, local loads/stores, or mixed opcode forms.
        /// </summary>
        public FluentTranspiler FindSequence(SearchMode mode, params Func<CodeInstruction, bool>[] predicates)
        {
            if (!TryFindSequence(mode, predicates))
            {
                int length = predicates != null ? predicates.Length : 0;
                AddSoftFailure($"Predicate sequence not found: length {length}");
            }

            return this;
        }

        /// <summary>
        /// Attempts to find a predicate sequence without adding diagnostics when absent.
        /// This is useful for optional compatibility patterns where another patch shape is valid.
        /// </summary>
        public bool TryFindSequence(SearchMode mode, params Func<CodeInstruction, bool>[] predicates)
        {
            if (predicates == null || predicates.Length == 0)
            {
                AddWarning("TryFindSequence received an empty predicate sequence.");
                return false;
            }

            var instructions = _matcher.Instructions();
            int startIndex = 0;
            if (mode == SearchMode.Current)
            {
                startIndex = _matcher.IsValid ? _matcher.Pos : 0;
            }
            else if (mode == SearchMode.Next)
            {
                startIndex = _matcher.IsValid ? _matcher.Pos + 1 : 0;
            }

            for (int i = Math.Max(0, startIndex); i <= instructions.Count - predicates.Length; i++)
            {
                bool matched = true;
                for (int j = 0; j < predicates.Length; j++)
                {
                    Func<CodeInstruction, bool> predicate = predicates[j];
                    if (predicate == null || !predicate(instructions[i + j]))
                    {
                        matched = false;
                        break;
                    }
                }

                if (!matched)
                {
                    continue;
                }

                _matcher.Start();
                if (i > 0)
                {
                    _matcher.Advance(i);
                }

                return true;
            }

            return false;
        }

        /// <summary>Match a sequence using instruction predicates.</summary>
        public FluentTranspiler MatchSequence(params Func<CodeInstruction, bool>[] predicates)
        {
            return FindSequence(SearchMode.Start, predicates);
        }

        /// <summary>Match a sequence of opcodes (pattern matching).</summary>
        public FluentTranspiler MatchSequence(params OpCode[] opcodes)
        {
            return FindSequence(SearchMode.Start, opcodes);
        }

        /// <summary>Match a field load (Ldfld or Ldsfld).</summary>
        /// <summary>Unified method to find a field load.</summary>
        public FluentTranspiler FindFieldLoad(Type type, string fieldName, SearchMode mode = SearchMode.Start)
        {
            if (mode == SearchMode.Start) _matcher.Start();
            else if (mode == SearchMode.Next) _matcher.Advance(1);

            Func<CodeInstruction, bool> predicate = instr =>
                (instr.opcode == OpCodes.Ldfld || instr.opcode == OpCodes.Ldsfld) &&
                instr.operand is FieldInfo f &&
                f.DeclaringType == type &&
                f.Name == fieldName;

            _matcher.MatchStartForward(new CodeMatch(predicate));
            if (!_matcher.IsValid) AddSoftFailure($"No match for field load {type.Name}.{fieldName}");
            return this;
        }

        /// <summary>Match a field load (Ldfld or Ldsfld).</summary>
        public FluentTranspiler MatchFieldLoad(Type type, string fieldName)
        {
            return FindFieldLoad(type, fieldName, SearchMode.Start);
        }

        /// <summary>Match a field store (Stfld or Stsfld).</summary>
        /// <summary>Unified method to find a field store.</summary>
        public FluentTranspiler FindFieldStore(Type type, string fieldName, SearchMode mode = SearchMode.Start)
        {
            if (mode == SearchMode.Start) _matcher.Start();
            else if (mode == SearchMode.Next) _matcher.Advance(1);

            Func<CodeInstruction, bool> predicate = instr =>
                (instr.opcode == OpCodes.Stfld || instr.opcode == OpCodes.Stsfld) &&
                instr.operand is FieldInfo f &&
                f.DeclaringType == type &&
                f.Name == fieldName;

            _matcher.MatchStartForward(new CodeMatch(predicate));
            if (!_matcher.IsValid) 
            {
                AddSoftFailure($"No match for field store {type.Name}.{fieldName}");
            }
            else
            {
                LogTrace($"[FluentTranspiler] FindFieldStore: Found {type.Name}.{fieldName} at index {_matcher.Pos}");
            }
            return this;
        }

        /// <summary>Match a field store (Stfld or Stsfld).</summary>
        public FluentTranspiler MatchFieldStore(Type type, string fieldName)
        {
            return FindFieldStore(type, fieldName, SearchMode.Start);
        }

        /// <summary>Unified method to find a string load.</summary>
        public FluentTranspiler FindString(string value, SearchMode mode = SearchMode.Start)
        {
             if (mode == SearchMode.Start) _matcher.Start();
            else if (mode == SearchMode.Next) _matcher.Advance(1);

            _matcher.MatchStartForward(new CodeMatch(OpCodes.Ldstr, value));
            if (!_matcher.IsValid)
                AddSoftFailure($"No match for string \"{value}\"");
            return this;
        }

        /// <summary>
        /// Search for a string constant (Ldstr).
        /// </summary>
        /// <param name="value">The exact string value to find.</param>
        public FluentTranspiler MatchString(string value)
        {
            return FindString(value, SearchMode.Start);
        }

        /// <summary>
        /// Search for an integer constant load.
        /// Automatically handles Ldc_I4_0 through Ldc_I4_S/Inline.
        /// </summary>
        /// <param name="value">The integer value to find.</param>
        /// <param name="mode">Whether to start from the beginning or continue.</param>
        public FluentTranspiler FindConstInt(int value, SearchMode mode = SearchMode.Start)
        {
            if (mode == SearchMode.Start) _matcher.Start();
            else if (mode == SearchMode.Next) _matcher.Advance(1);

            _matcher.MatchStartForward(new CodeMatch(instr =>
                instr.IsLdcI4(value)));
            if (!_matcher.IsValid)
                AddSoftFailure($"No match for int constant {value}");
            return this;
        }

        /// <summary>Match a constant integer load.</summary>
        public FluentTranspiler MatchConstInt(int value)
        {
            return FindConstInt(value, SearchMode.Start);
        }

        /// <summary>Match a constant float load.</summary>
        /// <summary>Unified method to find a float constant.</summary>
        public FluentTranspiler FindConstFloat(float value, SearchMode mode = SearchMode.Start)
        {
            if (mode == SearchMode.Start) _matcher.Start();
            else if (mode == SearchMode.Next) _matcher.Advance(1);

            _matcher.MatchStartForward(new CodeMatch(instr =>
                instr.IsLdcR4(value)));
            if (!_matcher.IsValid)
                AddSoftFailure($"No match for float constant {value}");
            return this;
        }

        /// <summary>Match a constant float load.</summary>
        public FluentTranspiler MatchConstFloat(float value)
        {
            return FindConstFloat(value, SearchMode.Start);
        }

        /// <summary>
        /// Highly resilient helper to extract a local variable index from a previous match.
        /// </summary>
        /// <remarks>
        /// This uses Harmony's "Named Match" feature. If you matched an instruction using 
        /// <c>.MatchStoreLocal("myVar")</c>, you can call this to get the integer index 
        /// the compiler assigned to that variable.
        /// </remarks>
        /// <param name="matchName">The name assigned to the match via the expressive API or CodeMatch.</param>
        /// <returns>The local variable index (0-N), or -1 if not found.</returns>
        public int CaptureLocalIndex(string matchName)
        {
            // Use Harmony's NamedMatch feature (Line 699 in their source)
            // to pull the instruction that was matched by name.
            var match = _matcher.NamedMatch(matchName);
            if (match == null) return -1;

            if (match.operand is int idx) return idx;
            if (match.opcode.ToString().Contains("."))
            {
                // Handles stloc.0, stloc.1 etc which have the index in the opcode name
                var parts = match.opcode.ToString().Split('.');
                if (parts.Length > 1 && int.TryParse(parts[1], out int opcodeIdx)) return opcodeIdx;
            }
            return -1;
        }

        #region Expressive Matching (English-like API)

        public FluentTranspiler MatchCall(MethodInfo method, string name = null)
        {
            var cm = CodeMatch.Calls(method);
            cm.name = name;
            _matcher.MatchStartForward(cm);
            if (!_matcher.IsValid)
            {
                AddSoftFailure(TranspilerDiagnosticCategory.Match, $"No match for call {FluentTranspilerFormatting.FormatMethod(method)}");
            }
            return this;
        }

        public FluentTranspiler MatchLoadField(FieldInfo field, string name = null)
        {
            var cm = CodeMatch.LoadsField(field);
            cm.name = name;
            _matcher.MatchStartForward(cm);
            if (!_matcher.IsValid)
            {
                AddSoftFailure(TranspilerDiagnosticCategory.Match, $"No match for field load {FluentTranspilerFormatting.FormatField(field)}");
            }
            return this;
        }

        public FluentTranspiler MatchStoreField(FieldInfo field, string name = null)
        {
            var cm = CodeMatch.StoresField(field);
            cm.name = name;
            _matcher.MatchStartForward(cm);
            if (!_matcher.IsValid)
            {
                AddSoftFailure(TranspilerDiagnosticCategory.Match, $"No match for field store {FluentTranspilerFormatting.FormatField(field)}");
            }
            return this;
        }

        public FluentTranspiler MatchLoadLocal(string name = null)
        {
            _matcher.MatchStartForward(CodeMatch.LoadsLocal(false, name));
            return this;
        }

        public FluentTranspiler MatchStoreLocal(string name = null)
        {
            _matcher.MatchStartForward(CodeMatch.StoresLocal(name));
            return this;
        }

        public FluentTranspiler MatchLoadArgument(int? index = null, string name = null)
        {
            var cm = CodeMatch.IsLdarg(index);
            cm.name = name;
            _matcher.MatchStartForward(cm);
            return this;
        }

        public FluentTranspiler MatchBranch(string name = null)
        {
            _matcher.MatchStartForward(CodeMatch.Branches(name));
            return this;
        }

        public FluentTranspiler Matches(params CodeMatch[] matches)
        {
            _matcher.MatchStartForward(matches);
            return this;
        }

        public FluentTranspiler MatchLoadConstant(string value, string name = null)
        {
            var cm = CodeMatch.LoadsConstant(value);
            cm.name = name;
            _matcher.MatchStartForward(cm);
            return this;
        }

        public FluentTranspiler MatchLoadConstant(long value, string name = null)
        {
            var cm = CodeMatch.LoadsConstant(value);
            cm.name = name;
            _matcher.MatchStartForward(cm);
            return this;
        }

        public FluentTranspiler MatchNewObject(ConstructorInfo ctor, string name = null)
        {
            _matcher.MatchStartForward(new CodeMatch(OpCodes.Newobj, ctor, name));
            if (!_matcher.IsValid)
            {
                string ctorType = ctor != null && ctor.DeclaringType != null ? ctor.DeclaringType.FullName : "<null>";
                AddSoftFailure(TranspilerDiagnosticCategory.Match, $"No match for newobj {ctorType}");
            }
            return this;
        }

        #endregion

        public FluentTranspiler MatchFuzzy(Func<CodeInstruction, bool> predicate, string name = null)
        {
            _matcher.MatchStartForward(new CodeMatch(predicate, name));
            return this;
        }

        /// <summary>
        /// Automatically backtracks and replaces an entire value assignment sequence.
        /// </summary>
        /// <remarks>
        /// <b>Why use this?</b> 
        /// <para>
        /// In standard IL, replacing `x = y + 1` is hard because you have to figure out exactly where the code 
        /// *started* pushing values for that line. 
        /// </para>
        /// <para>
        /// <b>ReplaceAssignment</b> uses the Stack Sentinel to do that work for you. It scans backwards 
        /// from your current position, finds the exact "root" of the expression, and swaps the 
        /// whole block. It saves you from having to manually count `ldarg` or `ldloc` instructions.
        /// </para>
        /// </remarks>
        /// <param name="newExpression">The instructions that should now generate and store the value.</param>
        public FluentTranspiler ReplaceAssignment(CodeInstruction[] newExpression)
        {
            // Backtrack to find the start of the expression that leads to the current stloc
            // In Sheltered's IL, this is usually a ldarg.0 or a sequence starting with a load.
            int endIdx = _matcher.Pos;
            int startIdx = BacktrackToExpressionStart(endIdx);
            int removeCount = endIdx - startIdx + 1;

            MoveTo(startIdx);
            ReplaceSequence(removeCount, newExpression);
            return this;
        }

        private int BacktrackToExpressionStart(int currentPos)
        {
            // Robust stack analysis: We need to find the instruction where 
            // the value currently being stored was first pushed.
            var instructions = _matcher.Instructions();
            var stackAnalysis = StackSentinel.Analyze(instructions, _originalMethod, out _);
            
            if (stackAnalysis == null || !stackAnalysis.TryGetValue(currentPos, out var targetStack))
            {
                 AddNote($"Backtrack failed: Could not analyze stack at index {currentPos}. Falling back to conservative match.");
                 return currentPos;
            }

            // We are looking for the point where the stack depth was exactly 
            // targetStack.Count - 1 (i.e., the depth before the current value was pushed).
            int targetDepth = Math.Max(0, targetStack.Count - 1);

            for (int i = currentPos - 1; i >= 0; i--)
            {
                if (stackAnalysis.TryGetValue(i, out var prevStack))
                {
                    // If we found a point where the stack was at our target depth,
                    // that's the start of the expression sequence.
                    if (prevStack.Count == targetDepth) return i;
                }
            }
            return currentPos; 
        }

        /// <summary>Reset to beginning.</summary>
        public FluentTranspiler Reset()
        {
            _matcher.Start();
            return this;
        }

        #region Modification Methods

        /// <summary>
        /// Replace current instruction with a new OpCode and operand.
        /// Automatically preserves any labels attached to the original instruction.
        /// </summary>
        public FluentTranspiler ReplaceWith(OpCode opcode, object operand = null)
        {
            if (!_matcher.IsValid)
            {
                AddSoftFailure("ReplaceWith: No valid match.");
                return this;
            }
            var beforeIndex = _matcher.Pos;
            var oldInstr = _matcher.Instruction;
            var newInstr = new CodeInstruction(opcode, operand);
            SetInstructionSafe(newInstr);
            RecordPatchEdit("ReplaceWith", beforeIndex, new[] { oldInstr }, beforeIndex, new[] { newInstr }, "Single instruction replacement", "exact");
            return this;
        }

        /// <summary>
        /// Replace current match with a call to a static replacement method.
        /// Automatically handles label preservation and validates that the target is static.
        /// </summary>
        /// <param name="type">The class containing your static hook.</param>
        /// <param name="methodName">The name of the static method.</param>
        /// <param name="parameterTypes">Optional parameter types for overload resolution.</param>
        /// <summary>
        /// Replaces the current instruction with a call to a static hook.
        /// Automatically handles label preservation and ensures the target method is compatible.
        /// </summary>
        /// <remarks>
        /// <b>Warning:</b> The target method MUST be <c>static</c>. If you are replacing an instance 
        /// method call, the target method should usually accept the 'this' instance as its first argument
        /// to maintain stack balance.
        /// </remarks>
        /// <param name="type">The mod class containing the static replacement method.</param>
        /// <param name="methodName">The name of the static method.</param>
        /// <param name="parameterTypes">Optional parameter types for overload resolution.</param>
        public FluentTranspiler ReplaceWithCall(Type type, string methodName, Type[] parameterTypes = null)
        {
            MethodInfo method;
            if (parameterTypes != null)
            {
                method = type.GetMethod(methodName, 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                    null, parameterTypes, null);
            }
            else
            {
                method = type.GetMethod(methodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            }

            if (method == null)
            {
                AddWarning($"Method {type.Name}.{methodName} not found");
                return this;
            }

            return ReplaceWithCall(method);
        }

        /// <summary>
        /// Replaces the current instruction with a static method call.
        /// </summary>
        public FluentTranspiler ReplaceWithCall(MethodInfo method)
        {
            if (!_matcher.IsValid)
            {
                AddSoftFailure("ReplaceWithCall: No valid match.");
                return this;
            }

            if (method == null)
            {
                AddWarning("ReplaceWithCall received a null method.");
                return this;
            }

            var beforeIndex = _matcher.Pos;
            var oldInstr = _matcher.Instruction;
            var originalCall = oldInstr != null ? oldInstr.operand as MethodInfo : null;
            if (originalCall != null)
            {
                if (!FluentTranspilerRecipeValidation.ValidateReplacementCallSignature(
                    this,
                    originalCall,
                    method,
                    nameof(ReplaceWithCall)))
                {
                    return this;
                }
            }
            else
            {
                if (!FluentTranspilerRecipeValidation.ValidateStaticMethod(this, method, nameof(ReplaceWithCall)) ||
                    !FluentTranspilerRecipeValidation.ValidateParameterCount(this, method, 0, nameof(ReplaceWithCall)))
                {
                    return this;
                }
            }

            var newInstr = new CodeInstruction(OpCodes.Call, method);
            SetInstructionSafe(newInstr);
            RecordPatchEdit("ReplaceWithCall", beforeIndex, new[] { oldInstr }, beforeIndex, new[] { newInstr }, $"{method.DeclaringType?.Name}.{method.Name}", "exact");
            return this;
        }

        /// <summary>
        /// Safely inserts instructions at the very beginning of the method.
        /// Automatically handles label preservation from the original first instruction.
        /// </summary>
        public FluentTranspiler InsertAtStart(params CodeInstruction[] instructions)
        {
            return Reset().InsertBefore(instructions);
        }

        /// <summary>
        /// Safely inserts instructions before the final 'ret' instruction.
        /// If multiple returns exist, it inserts before ALL of them.
        /// </summary>
        public FluentTranspiler InsertAtExit(params CodeInstruction[] instructions)
        {
            _matcher.Start();
            int count = 0;
            while (_matcher.MatchStartForward(new CodeMatch(OpCodes.Ret)).IsValid)
            {
                InsertBefore(instructions);
                _matcher.Advance(instructions.Length + 1); // Skip what we just added + the ret
                count++;
            }
            if (count == 0) AddSoftFailure("InsertAtExit: No return instructions found.");
            return this;
        }

        /// <summary>Insert instruction before current position (handles branch fixups automatically).</summary>
        public FluentTranspiler InsertBefore(OpCode opcode, object operand = null)
        {
            if (!_matcher.IsValid)
            {
                AddSoftFailure("InsertBefore: No valid match.");
                return this;
            }

            var newInstr = new CodeInstruction(opcode, operand);

            // Transfer labels so branches land on the new instruction
            var existingLabels = _matcher.Instruction.labels;
            if (existingLabels.Count > 0)
            {
                newInstr.labels.AddRange(existingLabels);
                existingLabels.Clear();
            }
            var existingBlocks = _matcher.Instruction.blocks;
            if (existingBlocks.Count > 0)
            {
                newInstr.blocks.AddRange(existingBlocks);
                existingBlocks.Clear();
            }

            var insertIndex = _matcher.Pos;
            _matcher.Insert(newInstr);
            InvalidateLabelIndexCache();
            RecordPatchEdit("InsertBefore", insertIndex, null, insertIndex, new[] { newInstr }, "Insert before current", "exact");
            return this;
        }

        /// <summary>
        /// Inserts a sequence of instructions BEFORE the current position.
        /// Automatically transfers labels from the original instruction to the FIRST new instruction.
        /// </summary>
        /// <remarks>
        /// This is the safest way to inject logic at a branch target, as it ensures that jumps 
        /// intended for the original instruction now land on your injected logic.
        /// </remarks>
        /// <param name="instructions">The array of instructions to insert.</param>
        public FluentTranspiler InsertBefore(params CodeInstruction[] instructions)
        {
            if (!_matcher.IsValid)
            {
                AddSoftFailure("InsertBefore: No valid match.");
                return this;
            }
            if (instructions == null)
            {
                AddWarning("InsertBefore: instruction array cannot be null.");
                return this;
            }

            // Clone caller-provided instructions so labels/operands edits here do not leak back to caller state.
            var toInsert = instructions.Select(i => new CodeInstruction(i)).ToArray();

            // Transfer labels to first new instruction
            var existingLabels = _matcher.Instruction.labels;
            if (existingLabels.Count > 0 && toInsert.Length > 0)
            {
                toInsert[0].labels.AddRange(existingLabels);
                existingLabels.Clear();
            }
            var existingBlocks = _matcher.Instruction.blocks;
            if (existingBlocks.Count > 0 && toInsert.Length > 0)
            {
                toInsert[0].blocks.AddRange(existingBlocks);
                existingBlocks.Clear();
            }

            var insertIndex = _matcher.Pos;
            for (int i = toInsert.Length - 1; i >= 0; i--)
            {
                _matcher.Insert(toInsert[i]);
            }
            InvalidateLabelIndexCache();
            RecordPatchEdit("InsertBefore", insertIndex, null, insertIndex, toInsert, "Insert sequence before current", "exact");
            return this;
        }

        /// <summary>
        /// Insert after current. Matcher stays on the ORIGINAL instruction.
        /// </summary>
        public FluentTranspiler InsertAfter(OpCode opcode, object operand = null)
        {
            if (!_matcher.IsValid)
            {
                AddSoftFailure("InsertAfter: No valid match.");
                return this;
            }

            var insertIndex = _matcher.Pos + 1;
            _matcher.Advance(1);
            var newInstr = new CodeInstruction(opcode, operand);
            _matcher.Insert(newInstr);
            _matcher.Advance(-1); // Return to original position
            InvalidateLabelIndexCache();
            RecordPatchEdit("InsertAfter", insertIndex, null, insertIndex, new[] { newInstr }, "Insert after current", "exact");
            return this;
        }

        /// <summary>
        /// Inserts a sequence of instructions AFTER the current position.
        /// The matcher remains on the ORIGINAL instruction.
        /// </summary>
        /// <remarks>
        /// This is useful for injecting logic that should execute immediately after a 
        /// prerequisite operation without moving the "cursor" of the transpiler.
        /// </remarks>
        /// <param name="instructions">The array of instructions to insert.</param>
        public FluentTranspiler InsertAfter(params CodeInstruction[] instructions)
        {
            if (!_matcher.IsValid)
            {
                AddSoftFailure("InsertAfter: No valid match.");
                return this;
            }
            if (instructions == null)
            {
                AddWarning("InsertAfter: instruction array cannot be null.");
                return this;
            }

            // Clone caller-provided instructions so insertion does not alias mutable instruction objects.
            var toInsert = instructions.Select(i => new CodeInstruction(i)).ToArray();

            var insertIndex = _matcher.Pos + 1;
            _matcher.Advance(1);
            foreach (var instr in toInsert)
            {
                _matcher.InsertAndAdvance(instr);
            }
            _matcher.Advance(-toInsert.Length - 1); // Restore to original position
            InvalidateLabelIndexCache();
            RecordPatchEdit("InsertAfter", insertIndex, null, insertIndex, toInsert, "Insert sequence after current", "exact");
            return this;
        }

        /// <summary>Remove current instruction.</summary>
        public FluentTranspiler Remove()
        {
            if (!_matcher.IsValid)
            {
                AddSoftFailure("Remove: No valid match.");
                return this;
            }

            return ReplaceSequence(1, new CodeInstruction[0]);
        }

        #endregion

        #region ILGenerator Wrappers

        /// <summary>
        /// Declare a new local variable for use in the transpiler.
        /// Throws if ILGenerator was not provided during construction.
        /// </summary>
        public FluentTranspiler DeclareLocal<T>(out LocalBuilder local)
        {
            if (_generator == null)
            {
                throw new InvalidOperationException(
                    "ILGenerator was not provided. " +
                    "Pass it to For(instructions, originalMethod, generator). " +
                    "Transpiler delegate signature: " +
                    "IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instr, ILGenerator gen)");
            }

            local = _generator.DeclareLocal(typeof(T));
            AddNote($"DeclareLocal<{typeof(T).FullName}>() -> LocalIndex {local.LocalIndex}");
            return this;
        }

        /// <summary>
        /// Define a new label for branching.
        /// Throws if ILGenerator was not provided during construction.
        /// </summary>
        public FluentTranspiler DefineLabel(out Label label)
        {
            if (_generator == null)
            {
                throw new InvalidOperationException(
                    "ILGenerator was not provided. " +
                    "Pass it to For(instructions, originalMethod, generator).");
            }

            label = _generator.DefineLabel();
            return this;
        }

        #endregion

        #region Variable Capture

        /// <summary>
        /// Automatically emits an Ldloc instruction for a local variable by index or name.
        /// </summary>
        /// <param name="localIndexOrName">The index (e.g. "0") or name (if symbols available) of the local variable.</param>
        public FluentTranspiler CaptureLocal(string localIndexOrName)
        {
            if (!_matcher.IsValid) return this;

            // 1. Try to parse as index
            if (int.TryParse(localIndexOrName, out int index))
            {
                _matcher.Insert(new CodeInstruction(GetLdlocOpCode(index), index > 3 ? (object)index : null));
                return this;
            }

            // 2. Try to find by name via reflection (requires debug symbols on the original method)
            if (_originalMethod != null)
            {
                try
                {
                    // Note: Standard LocalVariableInfo doesn't have names. 
                    // This is a placeholder for environments where names might be injected or available via metadata.
                    var locals = _originalMethod.GetMethodBody()?.LocalVariables;
                    if (locals != null)
                    {
                        foreach (var local in locals)
                        {
                            // In some contexts (like DynamicMethod or specific debug builds), 
                            // we might be able to resolve names. For now, we log a warning if we can't find it.
                        }
                    }
                }
                catch { }
            }

            AddNote($"CaptureLocal: Could not resolve variable '{localIndexOrName}' by name. Use numeric index instead.");
            return this;
        }

        private OpCode GetLdlocOpCode(int index)
        {
            switch (index)
            {
                case 0: return OpCodes.Ldloc_0;
                case 1: return OpCodes.Ldloc_1;
                case 2: return OpCodes.Ldloc_2;
                case 3: return OpCodes.Ldloc_3;
                default: return OpCodes.Ldloc_S;
            }
        }

        #endregion

        #region Control Flow Navigation

        /// <summary>Move forward or backward by N instructions.</summary>
        public FluentTranspiler Advance(int count)
        {
            _matcher.Advance(count);
            return this;
        }

        #endregion

        #region Bulk Operations

        /// <summary>
        /// Replace ALL occurrences of a specific method call throughout the entire instruction stream.
        /// Handles labels correctly and uses resilient type matching (by Name/FullName).
        /// </summary>
        /// <param name="sourceType">The class containing the method to replace.</param>
        /// <param name="sourceMethod">The name of the method to replace.</param>
        /// <param name="targetType">Your class containing the replacement static method.</param>
        /// <param name="targetMethod">The name of your static replacement method.</param>
        /// <param name="targetParams">Optional parameter types for target overload resolution.</param>
        public FluentTranspiler ReplaceAllCalls(Type sourceType, string sourceMethod, 
            Type targetType, string targetMethod, Type[] targetParams = null)
        {
            _matcher.Start();
            int replacements = 0;
            
            var targetMethodInfo = targetParams != null
                ? targetType.GetMethod(targetMethod, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, targetParams, null)
                : targetType.GetMethod(targetMethod, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            
            if (targetMethodInfo == null)
            {
                AddWarning($"Target method {targetType.Name}.{targetMethod} not found");
                return this;
            }
            
            if (!FluentTranspilerRecipeValidation.ValidateStaticMethod(this, targetMethodInfo, nameof(ReplaceAllCalls)))
            {
                return this;
            }
            
            while (_matcher.IsValid)
            {
                _matcher.MatchStartForward(new CodeMatch(instr =>
                    (instr.opcode == OpCodes.Call || instr.opcode == OpCodes.Callvirt) &&
                    instr.operand is MethodInfo m &&
                    (m.DeclaringType == sourceType || sourceType.IsAssignableFrom(m.DeclaringType) || m.DeclaringType.FullName == sourceType.FullName) &&
                    m.Name == sourceMethod));
                
                if (_matcher.IsValid)
                {
                    var beforeIndex = _matcher.Pos;
                    var oldInstr = _matcher.Instruction;
                    var sourceMethodInfo = oldInstr != null ? oldInstr.operand as MethodInfo : null;
                    if (!FluentTranspilerRecipeValidation.ValidateReplacementCallSignature(
                        this,
                        sourceMethodInfo,
                        targetMethodInfo,
                        nameof(ReplaceAllCalls)))
                    {
                        _matcher.Advance(1);
                        continue;
                    }

                    var newInstr = new CodeInstruction(OpCodes.Call, targetMethodInfo);
                    SetInstructionSafe(newInstr);
                    RecordPatchEdit("ReplaceAllCalls", beforeIndex, new[] { oldInstr }, beforeIndex, new[] { newInstr }, $"{sourceType.Name}.{sourceMethod} -> {targetType.Name}.{targetMethod}", "exact");
                    _matcher.Advance(1);
                    replacements++;
                }
            }
            
            if (replacements == 0)
            {
                AddSoftFailure($"No instances of {sourceType.Name}.{sourceMethod} found");
            }
            
            return this;
        }

        #endregion

        #region Navigation & Debugging

        /// <summary>Check if current position is valid.</summary>
        public bool HasMatch => _matcher.IsValid;

        /// <summary>Get current instruction (or null).</summary>
        public CodeInstruction Current => _matcher.IsValid ? _matcher.Instruction : null;

        /// <summary>Get current index.</summary>
        public int CurrentIndex => _matcher.Pos;

        /// <summary>The method whose IL is being patched, when supplied by the Harmony transpiler.</summary>
        public MethodBase OriginalMethod => _originalMethod;

        /// <summary>Move to next instruction.</summary>
        public FluentTranspiler Next()
        {
            _matcher.Advance(1);
            return this;
        }

        /// <summary>Move to previous instruction.</summary>
        public FluentTranspiler Previous()
        {
            _matcher.Advance(-1);
            return this;
        }

        /// <summary>Get all warnings that occurred.</summary>
        public IList<string> Warnings
        {
            get
            {
                return _diagnostics
                    .Where(d => d.Severity == TranspilerDiagnosticSeverity.Warning)
                    .Select(d => d.Message)
                    .ToList()
                    .AsReadOnly();
            }
        }

        /// <summary>Non-fatal match failures and probe misses captured during patch construction.</summary>
        public IList<string> SoftFailures
        {
            get
            {
                return _diagnostics
                    .Where(d => d.Severity == TranspilerDiagnosticSeverity.SoftFailure)
                    .Select(d => d.Message)
                    .ToList()
                    .AsReadOnly();
            }
        }

        /// <summary>Diagnostic notes collected during patch construction.</summary>
        public IList<string> Notes
        {
            get
            {
                return _diagnostics
                    .Where(d => d.Severity == TranspilerDiagnosticSeverity.Note)
                    .Select(d => d.Message)
                    .ToList()
                    .AsReadOnly();
            }
        }

        /// <summary>Structured diagnostics for callers that need severity/category instead of raw strings.</summary>
        public IList<TranspilerDiagnostic> Diagnostics { get { return _diagnostics.AsReadOnly(); } }

        /// <summary>Patch-recipe diagnostics captured during this transpiler run.</summary>
        public IList<FluentPatchDiagnostic> PatchDiagnostics { get { return _patchDiagnostics.AsReadOnly(); } }

        /// <summary>The most recent high-level patch diagnostic, useful for fail-safe fallback logging.</summary>
        public FluentPatchDiagnostic LatestPatchDiagnostic
        {
            get
            {
                return _patchDiagnostics.Count > 0 ? _patchDiagnostics[_patchDiagnostics.Count - 1] : null;
            }
        }

        internal string OwnerModName { get { return string.IsNullOrEmpty(_callerMod) ? "Unknown" : _callerMod; } }

        internal string TargetMethodName
        {
            get
            {
                return _originalMethod != null && _originalMethod.DeclaringType != null
                    ? _originalMethod.DeclaringType.FullName + "." + _originalMethod.Name
                    : (_originalMethod != null ? _originalMethod.Name : "<unknown method>");
            }
        }

        /// <summary>Add a warning to the transpiler state.</summary>
        public void AddWarning(string message)
        {
            AddDiagnostic(TranspilerDiagnosticSeverity.Warning, ClassifyDiagnostic(message, TranspilerDiagnosticSeverity.Warning), message);
        }

        /// <summary>Add a warning with an explicit category.</summary>
        public void AddWarning(TranspilerDiagnosticCategory category, string message)
        {
            AddDiagnostic(TranspilerDiagnosticSeverity.Warning, category, message);
        }

        /// <summary>Add a soft failure that should not be treated as a build warning by default.</summary>
        public void AddSoftFailure(string message)
        {
            AddDiagnostic(TranspilerDiagnosticSeverity.SoftFailure, ClassifyDiagnostic(message, TranspilerDiagnosticSeverity.SoftFailure), message);
        }

        /// <summary>Add a soft failure with an explicit category.</summary>
        public void AddSoftFailure(TranspilerDiagnosticCategory category, string message)
        {
            AddDiagnostic(TranspilerDiagnosticSeverity.SoftFailure, category, message);
        }

        /// <summary>Add a diagnostic note that should only surface in verbose/debug tooling.</summary>
        public void AddNote(string message)
        {
            AddDiagnostic(TranspilerDiagnosticSeverity.Note, ClassifyDiagnostic(message, TranspilerDiagnosticSeverity.Note), message);
        }

        /// <summary>Add a diagnostic note with an explicit category.</summary>
        public void AddNote(TranspilerDiagnosticCategory category, string message)
        {
            AddDiagnostic(TranspilerDiagnosticSeverity.Note, category, message);
        }

        /// <summary>Centralized typed diagnostic writer used by all helper entry points.</summary>
        public void AddDiagnostic(TranspilerDiagnosticSeverity severity, TranspilerDiagnosticCategory category, string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            _diagnostics.Add(new TranspilerDiagnostic
            {
                Severity = severity,
                Category = category,
                Message = message
            });
        }

        public void AddPatchDiagnostic(FluentPatchDiagnostic diagnostic)
        {
            if (diagnostic == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(diagnostic.TargetMethod))
            {
                diagnostic.TargetMethod = TargetMethodName;
            }

            if (string.IsNullOrEmpty(diagnostic.OwnerMod))
            {
                diagnostic.OwnerMod = OwnerModName;
            }

            _patchDiagnostics.Add(diagnostic);

            string message = diagnostic.ToString();
            if (diagnostic.Severity == FluentPatchSeverity.Critical)
            {
                AddWarning(TranspilerDiagnosticCategory.Safety, "[CRITICAL PATCH] " + message);
            }
            else if (diagnostic.Severity == FluentPatchSeverity.Warning)
            {
                AddWarning(TranspilerDiagnosticCategory.Match, message);
            }
            else
            {
                AddNote(TranspilerDiagnosticCategory.Match, message);
            }
        }

        /// <summary>Throw an exception immediately if the current operation has no match.</summary>
        public FluentTranspiler AssertValid()
        {
            if (!_matcher.IsValid)
            {
                string failure = SoftFailures.LastOrDefault()
                    ?? Warnings.LastOrDefault()
                    ?? "Unknown error";
                throw new InvalidOperationException($"[{_callerMod}] AssertValid failed: {failure} in method {_originalMethod?.DeclaringType.Name}.{_originalMethod?.Name}");
            }
            return this;
        }

        /// <summary>Log current state to console.</summary>
        public FluentTranspiler Log(string label = "")
        {
            var warnings = Warnings;
            var softFailures = SoftFailures;
            LogTrace($"[FluentTranspiler:{_callerMod}] {label}");
            LogTrace($"  Position: {_matcher.Pos}, Valid: {_matcher.IsValid}");
            if (_matcher.IsValid)
            {
                LogTrace($"  Current: {_matcher.Instruction}");
            }
            if (warnings.Count > 0)
            {
                LogTrace($"  Warnings: {warnings.Count}");
            }
            if (softFailures.Count > 0)
            {
                LogTrace($"  SoftFailures: {softFailures.Count}");
            }
            return this;
        }

        /// <summary>Dump all instructions to log (expensive, debugging only).</summary>
        public FluentTranspiler DumpAll(string label = "")
        {
            var instructions = _matcher.Instructions();
            LogTrace($"[FluentTranspiler:{_callerMod}] {label} ({instructions.Count} instructions):");
            for (int i = 0; i < instructions.Count; i++)
            {
                string marker = (i == _matcher.Pos) ? " >>>" : "    ";
                LogTrace($"{marker}{i:D3}: {instructions[i]}");
            }
            return this;
        }

        /// <summary>Get copy of current instructions.</summary>
        public IEnumerable<CodeInstruction> Instructions()
        {
            return _matcher.Instructions();
        }

        #endregion

        #region Pattern Matching & Safer Operations

        /// <summary>
        /// Move backwards in the instruction stream. 
        /// Safer than Previous() for checking context before removals.
        /// </summary>
        /// <param name="absolutePosition">Absolute index to move to.</param>
        public FluentTranspiler MoveTo(int absolutePosition)
        {
            var instructions = _matcher.Instructions().ToList();
            if (absolutePosition < 0 || absolutePosition >= instructions.Count)
            {
                AddSoftFailure($"MoveTo: Position {absolutePosition} out of range.");
                return this;
            }
            
            _matcher.Start().Advance(absolutePosition);
            return this;
        }

        /// <summary>
        /// Replaces a range of instructions with a new sequence.
        /// Optimized for replacing entire blocks of logic (e.g., an 'if' statement body).
        /// </summary>
        /// <remarks>
        /// This method includes a **Safety Analysis**: if a branch in the method body 
        /// targets an instruction INSIDE the range being removed, the transpiler will 
        /// throw a warning to prevent corruption. It also automatically preserves labels 
        /// from the first removed instruction.
        /// </remarks>
        /// <param name="removeCount">The number of original instructions to delete.</param>
        /// <param name="newInstructions">The instructions to insert in their place.</param>
        public FluentTranspiler ReplaceSequence(int removeCount, params CodeInstruction[] newInstructions)
        {
            return ReplaceSequence(removeCount, true, newInstructions);
        }

        /// <summary>
        /// Internal variant of <see cref="ReplaceSequence(int, CodeInstruction[])"/> that allows
        /// callers to skip automatic patch-edit recording when they capture higher-level edits.
        /// </summary>
        internal FluentTranspiler ReplaceSequence(int removeCount, bool recordPatchEdit, params CodeInstruction[] newInstructions)
        {
            if (!_matcher.IsValid)
            {
                AddSoftFailure("ReplaceSequence: No valid match.");
                return this;
            }
            if (removeCount < 0)
            {
                AddWarning("ReplaceSequence: removeCount cannot be negative.");
                return this;
            }
            if (newInstructions == null)
            {
                AddWarning("ReplaceSequence: replacement instructions cannot be null.");
                return this;
            }

            var beforeIndex = _matcher.Pos;
            var originalInstructions = _matcher.Instructions().ToList();
            var snapshot = originalInstructions.Select(i => new CodeInstruction(i)).ToList();
            int snapshotPos = _matcher.IsValid ? _matcher.Pos : 0;
            if (beforeIndex < 0 || beforeIndex + removeCount > originalInstructions.Count)
            {
                AddWarning($"[CRITICAL SAFETY] ReplaceSequence range out of bounds (start={beforeIndex}, removeCount={removeCount}, methodLength={originalInstructions.Count}). Aborting.");
                return this;
            }
            
            // 1. Analyze: Capture labels and check for hazardous jumps
            List<Label> capturedLabels;
            List<CodeInstruction> removedInstructions;
            List<List<ExceptionBlock>> capturedBlocksByOffset;
            if (!CaptureLabelsAndAnalyzeSafety(
                beforeIndex,
                removeCount,
                newInstructions,
                originalInstructions,
                out capturedLabels,
                out removedInstructions,
                out capturedBlocksByOffset))
            {
                return this; // Aborted due to safety check
            }

            try
            {
                // 2. Mutate: Remove the old instructions
                for (int i = 0; i < removeCount && _matcher.IsValid; i++)
                {
                    _matcher.RemoveInstruction();
                }

                // 3. Reconstruct: Insert new instructions and apply labels
                ApplyReplacementInstructions(newInstructions, capturedLabels, capturedBlocksByOffset);
                if (newInstructions.Length == 0)
                {
                    if (_matcher.IsValid)
                    {
                        // Preserve label/block anchors when removing without replacement.
                        for (int i = 0; i < capturedLabels.Count; i++)
                        {
                            var label = capturedLabels[i];
                            if (!_matcher.Instruction.labels.Contains(label))
                            {
                                _matcher.Instruction.labels.Add(label);
                            }
                        }

                        for (int i = 0; i < capturedBlocksByOffset.Count; i++)
                        {
                            var blocks = capturedBlocksByOffset[i];
                            if (blocks == null) continue;
                            for (int b = 0; b < blocks.Count; b++)
                            {
                                _matcher.Instruction.blocks.Add(blocks[b]);
                            }
                        }
                    }
                    else
                    {
                        bool hasAnchorsToPreserve = capturedLabels.Count > 0
                            || capturedBlocksByOffset.Any(blocks => blocks != null && blocks.Count > 0);
                        if (hasAnchorsToPreserve)
                        {
                            RestoreInstructionSnapshot(snapshot, snapshotPos);
                            AddWarning("[CRITICAL SAFETY] ReplaceSequence removed a labeled or exception-block-anchored suffix without replacement. Aborting.");
                            return this;
                        }

                        var current = _matcher.Instructions();
                        if (current.Count > 0)
                        {
                            _matcher.Start().Advance(Math.Min(beforeIndex, current.Count - 1));
                        }
                    }
                }

                InvalidateLabelIndexCache();
            }
            catch (Exception ex)
            {
                RestoreInstructionSnapshot(snapshot, snapshotPos);
                AddWarning("[CRITICAL SAFETY] ReplaceSequence failed and rolled back: " + ex.Message);
                return this;
            }

            // 4. Record
            if (recordPatchEdit)
            {
                RecordPatchEdit("ReplaceSequence", beforeIndex, removedInstructions, beforeIndex, newInstructions, $"remove:{removeCount} add:{newInstructions.Length}", "exact");
            }
            
            return this;
        }

        private bool CaptureLabelsAndAnalyzeSafety(
            int startIndex,
            int removeCount,
            CodeInstruction[] newInstructions,
            List<CodeInstruction> methodScope,
            out List<Label> capturedLabels,
            out List<CodeInstruction> removedInstructions,
            out List<List<ExceptionBlock>> capturedBlocksByOffset)
        {
            capturedLabels = new List<Label>();
            removedInstructions = new List<CodeInstruction>();
            capturedBlocksByOffset = new List<List<ExceptionBlock>>();
            var incomingBranchMap = BuildFirstTargetingInstructionMap(methodScope);

            for (var r = 0; r < removeCount; r++)
            {
                var index = startIndex + r;
                if (index < 0 || index >= methodScope.Count) continue;

                var instr = methodScope[index];
                removedInstructions.Add(instr);
                capturedBlocksByOffset.Add(instr.blocks != null ? new List<ExceptionBlock>(instr.blocks) : new List<ExceptionBlock>());

                if (instr.labels == null || instr.labels.Count == 0) continue;

                // Safety: If offset > 0, check for incoming jumps to this middle instruction
                if (r > 0)
                {
                    foreach (var label in instr.labels)
                    {
                        CodeInstruction jumper = null;
                        incomingBranchMap.TryGetValue(label, out jumper);
                        if (jumper != null)
                        {
                            AddWarning($"[CRITICAL SAFETY] Unsafe Jump Detected: Instruction @IL_{methodScope.IndexOf(jumper):X4} ({jumper.opcode}) targets the middle of your replacement block at offset {r} (Label: {label}). Aborting.");
                            return false;
                        }
                    }
                }
                capturedLabels.AddRange(instr.labels);
            }

            if (MethodHasExceptionHandlingClauses())
            {
                if (newInstructions == null || newInstructions.Length != removeCount)
                {
                    AddWarning("[CRITICAL SAFETY] ReplaceSequence on EH methods requires exact index-aligned replacement (removeCount == insertCount). Aborting.");
                    return false;
                }

                for (int r = 0; r < capturedBlocksByOffset.Count; r++)
                {
                    var blocks = capturedBlocksByOffset[r];
                    if (blocks == null || blocks.Count == 0) continue;

                    bool canMapByIndex = newInstructions != null && newInstructions.Length == removeCount;
                    bool canMapToEntry = newInstructions != null && newInstructions.Length > 0 && r == 0;
                    if (!canMapByIndex && !canMapToEntry)
                    {
                        AddWarning("[CRITICAL SAFETY] ReplaceSequence would relocate exception boundary markers without a safe mapping. Aborting.");
                        return false;
                    }
                }
            }

            if (removeCount > 0 && newInstructions != null && newInstructions.Length > 0 && startIndex >= 0 && startIndex < methodScope.Count)
            {
                var originalEntry = methodScope[startIndex];
                var replacementEntry = newInstructions[0];
                if (originalEntry != null && originalEntry.labels != null && originalEntry.labels.Count > 0)
                {
                    for (int i = 0; i < originalEntry.labels.Count; i++)
                    {
                        if (!incomingBranchMap.ContainsKey(originalEntry.labels[i])) continue;
                        if (!AreLabelEntryStackBehaviorsCompatible(originalEntry, replacementEntry))
                        {
                            AddWarning("[CRITICAL SAFETY] Label-targeted entry instruction replacement changed stack behavior. Aborting.");
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        private static Dictionary<Label, CodeInstruction> BuildFirstTargetingInstructionMap(List<CodeInstruction> methodScope)
        {
            var map = new Dictionary<Label, CodeInstruction>();
            if (methodScope == null) return map;

            for (int i = 0; i < methodScope.Count; i++)
            {
                var instr = methodScope[i];
                if (instr == null) continue;

                Label? branchLabel;
                if (instr.Branches(out branchLabel) && branchLabel.HasValue)
                {
                    var single = branchLabel.Value;
                    if (!map.ContainsKey(single)) map[single] = instr;
                }
                else if (instr.operand is Label[] many)
                {
                    for (int j = 0; j < many.Length; j++)
                    {
                        var label = many[j];
                        if (!map.ContainsKey(label)) map[label] = instr;
                    }
                }
            }
            return map;
        }

        private void ApplyReplacementInstructions(CodeInstruction[] newInstructions, List<Label> capturedLabels, List<List<ExceptionBlock>> capturedBlocksByOffset)
        {
            for (int i = 0; i < newInstructions.Length; i++)
            {
                var instr = new CodeInstruction(newInstructions[i]);
                if (i == 0 && capturedLabels.Count > 0)
                {
                    foreach (var label in capturedLabels)
                    {
                        if (!instr.labels.Contains(label))
                            instr.labels.Add(label);
                    }
                }
                if (capturedBlocksByOffset != null)
                {
                    if (i < capturedBlocksByOffset.Count)
                    {
                        var blocks = capturedBlocksByOffset[i];
                        if (blocks != null)
                        {
                            for (int b = 0; b < blocks.Count; b++)
                            {
                                instr.blocks.Add(blocks[b]);
                            }
                        }
                    }
                    else if (i == 0 && capturedBlocksByOffset.Count > 0)
                    {
                        // Fallback: preserve any unmapped markers on entry instruction.
                        for (int r = 1; r < capturedBlocksByOffset.Count; r++)
                        {
                            var blocks = capturedBlocksByOffset[r];
                            if (blocks == null) continue;
                            for (int b = 0; b < blocks.Count; b++)
                            {
                                instr.blocks.Add(blocks[b]);
                            }
                        }
                    }
                }
                _matcher.InsertAndAdvance(instr);
            }
        }

        /// <summary>Attaches a label to the current instruction.</summary>
        public FluentTranspiler AddLabel(Label label)
        {
            if (_matcher.IsValid)
            {
                _matcher.Instruction.labels.Add(label);
                InvalidateLabelIndexCache();
            }
            return this;
        }

        /// <summary>
        /// Completely replaces the entire method body with new instructions.
        /// Uses CodeMatcher operations first and only falls back to direct list mutation when inserting into an empty body.
        /// Applies transactional rollback on any failure and preserves entry labels/blocks when possible.
        /// </summary>
        public FluentTranspiler ReplaceAll(IEnumerable<CodeInstruction> newInstructions)
        {
            if (MethodHasExceptionHandlingClauses())
            {
                AddWarning("[CRITICAL SAFETY] ReplaceAll is blocked for methods with exception handlers. Use exact index-aligned replacements instead.");
                return this;
            }
            if (newInstructions == null)
            {
                AddWarning("[CRITICAL SAFETY] ReplaceAll received null replacement instruction sequence. Aborting.");
                return this;
            }

            var newCode = newInstructions.Select(i => new CodeInstruction(i)).ToList();
            var oldList = _matcher.Instructions();
            var oldCopy = oldList.Select(i => new CodeInstruction(i)).ToList();
            int oldCount = oldCopy.Count;
            int newCount = newCode.Count;
            string methodName = _originalMethod != null ? (_originalMethod.DeclaringType != null ? _originalMethod.DeclaringType.FullName + "." + _originalMethod.Name : _originalMethod.Name) : "<unknown-method>";
            string oldPreview = BuildOpcodePreview(oldCopy);
            string newPreview = BuildOpcodePreview(newCode);

            if (oldList.Count > 0 && newCode.Count > 0 
                && oldList[0].labels?.Count > 0)
            {
                newCode[0].labels.AddRange(oldList[0].labels);
            }
            if (oldList.Count > 0 && newCode.Count > 0
                && oldList[0].blocks?.Count > 0)
            {
                newCode[0].blocks.AddRange(oldList[0].blocks);
            }

            var snapshot = oldCopy.Select(i => new CodeInstruction(i)).ToList();
            int snapshotPos = _matcher.IsValid ? _matcher.Pos : 0;
            try
            {
                bool replacedUsingMatcher = false;
                if (oldCount > 0)
                {
                    _matcher.Start();
                    if (newCount > 0)
                    {
                        // Matcher-first replacement without dropping into an empty/invalid insert state:
                        // replace entry, trim old tail, then append remaining new tail.
                        _matcher.SetInstruction(newCode[0]);
                        if (oldCount > 1)
                        {
                            _matcher.Advance(1);
                            _matcher.RemoveInstructions(oldCount - 1);
                        }
                        if (newCount > 1)
                        {
                            _matcher.Start();
                            _matcher.InsertAfter(newCode.Skip(1));
                        }
                    }
                    else
                    {
                        _matcher.RemoveInstructions(oldCount);
                    }
                    replacedUsingMatcher = true;
                }
                else if (newCount > 0)
                {
                    // No instructions exist; matcher-driven insertion is invalid in this state.
                    oldList.Clear();
                    oldList.AddRange(newCode);
                    AddWarning($"[CRITICAL SAFETY] ReplaceAll used direct instruction-list fallback on {methodName} because CodeMatcher cannot insert into an empty body. oldCount={oldCount}, newCount={newCount}. oldOps={oldPreview}. newOps={newPreview}");
                }

                InvalidateLabelIndexCache();

                // Safety check: Verify the replacement took
                if (_matcher.Instructions().Count != newCount)
                {
                    AddWarning($"[CRITICAL SAFETY] ReplaceAll internal list mismatch on {methodName}. oldCount={oldCount}, newCount={newCount}, actualCount={_matcher.Instructions().Count}. oldOps={oldPreview}. newOps={newPreview}");
                    RestoreInstructionSnapshot(snapshot, snapshotPos);
                    AddWarning($"[CRITICAL SAFETY] ReplaceAll rolled back on {methodName} after internal list mismatch.");
                    return this;
                }

                if (replacedUsingMatcher)
                {
                    _matcher.Start();
                }
            }
            catch (Exception ex)
            {
                RestoreInstructionSnapshot(snapshot, snapshotPos);
                AddWarning($"[CRITICAL SAFETY] ReplaceAll failed and rolled back on {methodName}. oldCount={oldCount}, newCount={newCount}. oldOps={oldPreview}. newOps={newPreview}. Error={ex.Message}");
                return this;
            }

            RecordPatchEdit("ReplaceAll", 0, oldCopy, 0, newCode, "Replace entire method body", "exact");

            return Reset();
        }

        /// <summary>
        /// Performs a global search-and-replace for a specific multi-instruction pattern.
        /// </summary>
        /// <remarks>
        /// <b>Why use this?</b>
        /// <para>
        /// If you need to redirect something high-level (like every coordinate calculation in the game), 
        /// doing it manually is a nightmare. 
        /// </para>
        /// <para>
        /// <b>ReplaceAllPatterns</b> is your "find/replace all." It is designed for longevity: 
        /// it uses instruction fingerprints rather than hardcoded offsets, meaning your patch 
        /// is much more likely to survive game updates.
        /// </para>
        /// <para>
        /// <b>Step-by-Step Usage:</b>
        /// <list type="number">
        /// <item>Define the <paramref name="patternPredicates"/>: An array of lambdas where each one matches 
        /// one instruction in the sequence (e.g. <c>instr => instr.IsLdcI4(2)</c>).</item>
        /// <item>Define the <paramref name="replaceWith"/>: The new instructions that will occupy that space.</item>
        /// <item>Decide on <paramref name="preserveInstructionCount"/>: If true, the system will pad your 
        /// replacement with <c>Nop</c> instructions to ensure the total line count of the method doesn't change 
        /// (highly recommended if you aren't sure about branch offsets).</item>
        /// </list>
        /// </para>
        /// <para>
        /// <b>Usage Example (Redirecting 'this.width / 2'):</b>
        /// <code>
        /// t.ReplaceAllPatterns(
        ///     new Func&lt;CodeInstruction, bool&gt;[] {
        ///         i => i.opcode == OpCodes.Ldarg_0,
        ///         i => i.LoadsField(typeof(Map), "width"),
        ///         i => i.IsLdcI4(2),
        ///         i => i.opcode == OpCodes.Div
        ///     },
        ///     new[] {
        ///         new CodeInstruction(OpCodes.Call, typeof(MyMod).GetMethod("GetCustomWidth"))
        ///     },
        ///     preserveInstructionCount: true
        /// );
        /// </code>
        /// </para>
        /// </remarks>
        /// <param name="patternPredicates">An array of predicates defining the IL "fingerprint" to find. Each element matches one instruction in order.</param>
        /// <param name="replaceWith">The instructions to insert at every match location.</param>
        /// <param name="preserveInstructionCount">If true, fills remaining slots with <c>OpCodes.Nop</c> to maintain stable instruction indices.</param>
        public FluentTranspiler ReplaceAllPatterns(
            Func<CodeInstruction, bool>[] patternPredicates,
            CodeInstruction[] replaceWith,
            bool preserveInstructionCount = false)
        {
            string methodName = _originalMethod != null ? _originalMethod.Name : "<unknown-method>";
            bool effectivePreserveInstructionCount = ResolvePatternPreserveMode(preserveInstructionCount, patternPredicates != null ? patternPredicates.Length : 0);
            LogTrace($"[FluentTranspiler:{_callerMod}] ReplaceAllPatterns: Searching for pattern (length {patternPredicates.Length}) in {methodName}. Preserve count: requested={preserveInstructionCount}, effective={effectivePreserveInstructionCount}.");

            var instructions = _matcher.Instructions().ToList();
            
            // Find all match positions first (snapshot approach)
            var matchPositions = new List<int>();
            for (int i = 0; i <= instructions.Count - patternPredicates.Length; i++)
            {
                bool matches = true;
                for (int j = 0; j < patternPredicates.Length; j++)
                {
                    if (!patternPredicates[j](instructions[i + j]))
                    {
                        matches = false;
                        break;
                    }
                }
                
                if (matches)
                {
                    matchPositions.Add(i);
                    LogTrace($"[FluentTranspiler:{_callerMod}] ReplaceAllPatterns: Found match at index {i}.");
                    i += patternPredicates.Length - 1;
                }
            }

            if (matchPositions.Count == 0)
            {
                AddSoftFailure($"ReplaceAllPatterns: No valid matches found for pattern in method {methodName}. Verified opcodes: {string.Join(", ", patternPredicates.Select(p => "predicate").ToArray())}");
                return this;
            }

            if (!CanSafelyPatchWithExceptionBlocks(instructions, matchPositions, patternPredicates.Length, replaceWith.Length, methodName))
            {
                return this;
            }

            if (effectivePreserveInstructionCount && !CanSafelyPadWithNops(instructions, matchPositions, patternPredicates.Length, replaceWith.Length))
            {
                AddWarning($"[CRITICAL SAFETY] ReplaceAllPatterns cannot preserve instruction count safely in {methodName}. Removed tail instructions are not stack-neutral; aborting replacement.");
                return this;
            }
            
            LogTrace($"[FluentTranspiler] ReplaceAllPatterns: Found {matchPositions.Count} occurrences in {methodName}. Applying replacements...");
            
            // Apply replacements in reverse order to maintain indices
            for (int idx = matchPositions.Count - 1; idx >= 0; idx--)
            {
                int pos = matchPositions[idx];
                _matcher.Start();
                _matcher.Advance(pos);

                if (effectivePreserveInstructionCount && replaceWith.Length <= patternPredicates.Length)
                {
                    // Safe replacement logic:
                    // 1. Fill leading slots with actual replacement instructions (preserving labels at each index)
                    // 2. Fill remaining slots with Nops (preserving labels at each index)
                    
                    // Step 1: Replace leading instructions
                    for (int i = 0; i < replaceWith.Length; i++)
                    {
                        SetInstructionSafe(new CodeInstruction(replaceWith[i]));
                        _matcher.Advance(1);
                    }
                    // Step 2: Nop out remaining
                    for (int i = replaceWith.Length; i < patternPredicates.Length; i++)
                    {
                        SetInstructionSafe(new CodeInstruction(OpCodes.Nop));
                        _matcher.Advance(1);
                    }
                }
                else
                {
                    // Normal mode: remove and replace using the safer ReplaceSequence helper.
                    ReplaceSequence(patternPredicates.Length, false, replaceWith);
                }

                RecordPatchEdit(
                    "ReplaceAllPatterns",
                    pos,
                    instructions.Skip(pos).Take(patternPredicates.Length).ToList(),
                    pos,
                    replaceWith,
                    "preserveRequested=" + preserveInstructionCount + ",preserveEffective=" + effectivePreserveInstructionCount,
                    "exact");
            }
            
            _matcher.Start(); // Return to start
            
            return this;
        }

        /// <summary>
        /// NOP-padding only preserves stack safety if every removed tail instruction
        /// is stack-neutral. Otherwise branch targets that enter the padded span can observe
        /// different stack states and destabilize runtime.
        /// </summary>
        private static bool CanSafelyPadWithNops(List<CodeInstruction> instructions, List<int> matchPositions, int patternLength, int replaceLength)
        {
            if (replaceLength > patternLength) return false;
            if (replaceLength == patternLength) return true;

            for (int m = 0; m < matchPositions.Count; m++)
            {
                int start = matchPositions[m];
                for (int i = start + replaceLength; i < start + patternLength; i++)
                {
                    if (i < 0 || i >= instructions.Count) return false;
                    var instr = instructions[i];
                    if (!IsStackNeutralInstruction(instr)) return false;
                }
            }
            return true;
        }

        private static bool IsStackNeutralInstruction(CodeInstruction instr)
        {
            if (instr == null) return false;
            var op = instr.opcode;
            return op.StackBehaviourPop == StackBehaviour.Pop0 && op.StackBehaviourPush == StackBehaviour.Push0;
        }

        private bool MethodHasExceptionHandlingClauses()
        {
            if (_originalMethod == null) return false;
            try
            {
                var body = _originalMethod.GetMethodBody();
                return body != null && body.ExceptionHandlingClauses != null && body.ExceptionHandlingClauses.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private bool CanSafelyPatchWithExceptionBlocks(
            List<CodeInstruction> instructions,
            List<int> matchPositions,
            int patternLength,
            int replaceLength,
            string methodName)
        {
            if (!MethodHasExceptionHandlingClauses()) return true;
            if (instructions == null || matchPositions == null || patternLength <= 0) return true;

            if (replaceLength != patternLength)
            {
                AddWarning($"[CRITICAL SAFETY] ReplaceAllPatterns on EH method {methodName} requires exact index-aligned replacement (patternLength == replaceLength). Aborting.");
                return false;
            }

            for (int m = 0; m < matchPositions.Count; m++)
            {
                int start = matchPositions[m];
                for (int i = 0; i < patternLength; i++)
                {
                    int idx = start + i;
                    if (idx < 0 || idx >= instructions.Count) continue;
                    var instr = instructions[idx];
                    bool hasBlocks = instr != null && instr.blocks != null && instr.blocks.Count > 0;
                    if (!hasBlocks) continue;
                }
            }

            return true;
        }

        private static bool AreLabelEntryStackBehaviorsCompatible(CodeInstruction originalEntry, CodeInstruction replacementEntry)
        {
            if (originalEntry == null || replacementEntry == null) return false;
            int? originalPop = TryGetFixedStackPopCount(originalEntry.opcode);
            int? replacementPop = TryGetFixedStackPopCount(replacementEntry.opcode);
            int? originalPush = TryGetFixedStackPushCount(originalEntry.opcode);
            int? replacementPush = TryGetFixedStackPushCount(replacementEntry.opcode);

            if (originalPop.HasValue && replacementPop.HasValue && originalPush.HasValue && replacementPush.HasValue)
            {
                return originalPop.Value == replacementPop.Value && originalPush.Value == replacementPush.Value;
            }

            return originalEntry.opcode.StackBehaviourPop == replacementEntry.opcode.StackBehaviourPop
                && originalEntry.opcode.StackBehaviourPush == replacementEntry.opcode.StackBehaviourPush;
        }

        private static int? TryGetFixedStackPopCount(OpCode opcode)
        {
            switch (opcode.StackBehaviourPop)
            {
                case StackBehaviour.Pop0: return 0;
                case StackBehaviour.Pop1:
                case StackBehaviour.Popi:
                case StackBehaviour.Popref:
                    return 1;
                case StackBehaviour.Pop1_pop1:
                case StackBehaviour.Popi_pop1:
                case StackBehaviour.Popi_popi:
                case StackBehaviour.Popi_popi8:
                case StackBehaviour.Popi_popr4:
                case StackBehaviour.Popi_popr8:
                case StackBehaviour.Popref_pop1:
                case StackBehaviour.Popref_popi:
                    return 2;
                case StackBehaviour.Popi_popi_popi:
                case StackBehaviour.Popref_popi_pop1:
                case StackBehaviour.Popref_popi_popi:
                case StackBehaviour.Popref_popi_popi8:
                case StackBehaviour.Popref_popi_popr4:
                case StackBehaviour.Popref_popi_popr8:
                case StackBehaviour.Popref_popi_popref:
                    return 3;
                default:
                    return null;
            }
        }

        private static int? TryGetFixedStackPushCount(OpCode opcode)
        {
            switch (opcode.StackBehaviourPush)
            {
                case StackBehaviour.Push0: return 0;
                case StackBehaviour.Push1:
                case StackBehaviour.Pushi:
                case StackBehaviour.Pushi8:
                case StackBehaviour.Pushr4:
                case StackBehaviour.Pushr8:
                case StackBehaviour.Pushref:
                    return 1;
                case StackBehaviour.Push1_push1:
                    return 2;
                default:
                    return null;
            }
        }

        private static string BuildOpcodePreview(IList<CodeInstruction> instructions)
        {
            if (instructions == null || instructions.Count == 0) return "<empty>";
            var first = instructions.Take(3).Select(i => i != null ? i.opcode.Name : "null").ToArray();
            var last = instructions.Skip(Math.Max(0, instructions.Count - 3)).Select(i => i != null ? i.opcode.Name : "null").ToArray();
            return "first:[" + string.Join(",", first) + "] last:[" + string.Join(",", last) + "]";
        }

        /// <summary>
        /// Executes a mutation block atomically. If the block throws, instruction state is restored.
        /// This provides rollback support for complex multi-step patch chains.
        /// </summary>
        public FluentTranspiler WithTransaction(Action<FluentTranspiler> action)
        {
            var snapshot = _matcher.Instructions().Select(i => new CodeInstruction(i)).ToList();
            int snapshotPos = _matcher.IsValid ? _matcher.Pos : 0;
            try
            {
                action(this);
                return this;
            }
            catch (Exception ex)
            {
                RestoreInstructionSnapshot(snapshot, snapshotPos);
                AddNote("Transaction rollback applied: " + ex.Message);
                return this;
            }
        }

        private void RestoreInstructionSnapshot(List<CodeInstruction> snapshot, int pos)
        {
            var current = _matcher.Instructions();
            current.Clear();
            current.AddRange(snapshot);
            InvalidateLabelIndexCache();
            _matcher.Start();
            if (current.Count > 0)
            {
                int clamped = Math.Max(0, Math.Min(pos, current.Count - 1));
                _matcher.Advance(clamped);
            }
        }

        #endregion

        #region Navigation Helpers (Public for Extensions)

        /// <summary>Resolves a label to its current instruction index.</summary>
        public int LabelToIndex(Label label)
        {
            if (_labelIndexCacheDirty)
            {
                RebuildLabelIndexCache();
            }

            int index;
            return _labelIndexCache.TryGetValue(label, out index) ? index : -1;
        }

        #endregion

        #region Build

        /// <summary>
        /// Finalizes the current session and returns the rewritten instruction stream.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="Build"/> is where the framework runs stack validation, linting, debug snapshots,
        /// and warning escalation. When a patch uses <see cref="Execute"/>, this happens automatically.
        /// </para>
        /// <para>
        /// Use <paramref name="strict"/> for development or for patches that should abort on any warning.
        /// Leave it disabled for routine production patches that need diagnostics without failing on every
        /// non-critical game IL quirk.
        /// </para>
        /// </remarks>
        /// <param name="strict">If true, any validation warning aborts the build with an exception.</param>
        /// <param name="validateStack">If true, runs stack and lint validation before returning instructions.</param>
        public IEnumerable<CodeInstruction> Build(BuildProfile profile)
        {
            _buildProfile = profile;
            var options = TranspilerSafetyPolicy.ResolveBuildOptions(profile);
            return Build(options.Strict, options.ValidateStack, options.ForceSnapshot);
        }

        public IEnumerable<CodeInstruction> Build(bool strict = true, bool validateStack = true)
        {
            _buildProfile = strict ? BuildProfile.Strict : BuildProfile.Runtime;
            return Build(strict, validateStack, forceSnapshot: false);
        }

        private IEnumerable<CodeInstruction> Build(bool strict, bool validateStack, bool forceSnapshot)
        {
            var instructions = _matcher.Instructions().ToList();
            bool skipStackValidationForExceptionHandlers = validateStack && MethodHasExceptionHandlingClauses();
            if (validateStack)
            {
                if (!skipStackValidationForExceptionHandlers)
                {
                    if (!StackSentinel.Validate(instructions, _originalMethod, out string stackError))
                    {
                        AddWarning($"Stack Error: {stackError}");
                    }

                    // Validate explicit stack expectations
                    if (_stackExpectations.Count > 0)
                    {
                        Dictionary<int, List<Type>> stackAnalysis = StackSentinel.Analyze(instructions, _originalMethod, out _);
                        if (stackAnalysis != null)
                        {
                            foreach (var expectation in _stackExpectations)
                            {
                                int index = expectation.index;
                                int expectedDepth = expectation.expectedDepth;

                                if (stackAnalysis.TryGetValue(index, out var stack))
                                {
                                    int actualDepth = stack.Count;
                                    if (actualDepth != expectedDepth)
                                    {
                                        AddWarning($"Stack expectation failed at index {index}: Expected {expectedDepth}, got {actualDepth}");
                                    }
                                }
                            }
                        }
                    }

                    // Validate stack delta expectations
                    if (_stackDeltaExpectations.Count > 0)
                    {
                        Dictionary<int, List<Type>> stackAnalysis = StackSentinel.Analyze(instructions, _originalMethod, out _);
                        if (stackAnalysis != null)
                        {
                            foreach (var expectation in _stackDeltaExpectations)
                            {
                                if (stackAnalysis.TryGetValue(expectation.startIndex, out var startStack) &&
                                    stackAnalysis.TryGetValue(expectation.endIndex, out var endStack))
                                {
                                    int actualDelta = endStack.Count - startStack.Count;
                                    if (actualDelta != expectation.expectedDelta)
                                    {
                                        AddWarning($"Stack delta expectation failed between {expectation.startIndex} and {expectation.endIndex}: Expected {expectation.expectedDelta:+#;-#;0}, got {actualDelta:+#;-#;0}");
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    AddNote($"StackSentinel validation skipped for EH method {_originalMethod?.DeclaringType?.FullName}.{_originalMethod?.Name}.");
                }

                // Run Linter
                Lint(instructions);
            }

            var warnings = Warnings;
            var softFailures = SoftFailures;
            var notes = Notes;
            bool hasCriticalWarning = warnings.Any(TranspilerSafetyPolicy.IsCriticalWarning);

            _stopwatch.Stop();
            double duration = _stopwatch.Elapsed.TotalMilliseconds;

            bool shouldRecordSnapshot =
                forceSnapshot ||
                hasCriticalWarning ||
                (_buildProfile == BuildProfile.Debug &&
                 TranspilerSafetyPolicy.ShouldRecordDebugSnapshot(warnings.Count, softFailures.Count, notes.Count));

            if (shouldRecordSnapshot)
            {
                TranspilerDebugger.RecordSnapshot(
                    _callerMod,
                    null,
                    _initialInstructions,
                    _matcher.Instructions(),
                    duration,
                    warnings.Count,
                    _originalMethod,
                    BuildPatchOrigin(),
                    patchEdits: _patchEdits,
                    warnings: _diagnostics.Select(d => d.ToString()));
            }

            if (warnings.Count > 0)
            {
                var heading = hasCriticalWarning
                    ? $"[{_callerMod}] Transpiler validation failed:"
                    : $"[{_callerMod}] Transpiler validation warnings:";
                var message = heading + "\n" + string.Join("\n", warnings.Select(w => "  - " + w).ToArray());
                if (strict || (hasCriticalWarning && TranspilerSafetyPolicy.FailFastOnCritical))
                {
                    throw new InvalidOperationException(message);
                }

                if (hasCriticalWarning)
                {
                    MMLog.WriteError(message);
                }
                else if (_buildProfile == BuildProfile.Debug && TranspilerSafetyPolicy.LogValidationWarnings)
                {
                    MMLog.WriteWarning(message);
                }
            }

            FlushTraceLog(duration, warnings.Count, softFailures.Count, notes.Count);
            return _matcher.Instructions().ToList();
        }

        #endregion

        #region Internal Validation


        private static string ResolveCallingModName()
        {
            try
            {
                var trace = new System.Diagnostics.StackTrace();
                for (int i = 0; i < trace.FrameCount; i++)
                {
                    var method = trace.GetFrame(i).GetMethod();
                    var asm = method != null && method.DeclaringType != null ? method.DeclaringType.Assembly : null;
                    if (asm == null) continue;

                    if (asm == typeof(FluentTranspiler).Assembly) continue;
                    if (asm == typeof(HarmonyLib.Harmony).Assembly) continue;

                    var name = asm.GetName().Name ?? string.Empty;
                    if (name.StartsWith("System.", StringComparison.Ordinal)) continue;
                    if (name.StartsWith("mscorlib", StringComparison.OrdinalIgnoreCase)) continue;
                    if (name.StartsWith("UnityEngine", StringComparison.Ordinal)) continue;

                    return name;
                }
            }
            catch
            {
                // Fall through to default.
            }

            return Assembly.GetCallingAssembly().GetName().Name;
        }

        private string BuildPatchOrigin()
        {
            var methodId = _originalMethod != null && _originalMethod.DeclaringType != null
                ? _originalMethod.DeclaringType.FullName + "." + _originalMethod.Name
                : (_originalMethod != null ? _originalMethod.Name : "UnknownMethod");

            return "FluentTranspiler|Owner:" + (_callerMod ?? "Unknown") + "|Method:" + methodId;
        }

        private void LogTrace(string message)
        {
            if (_buildProfile != BuildProfile.Debug || !TranspilerSafetyPolicy.VerboseTracingEnabled)
            {
                return;
            }

            _traceMessages.Add(message);
        }

        private void FlushTraceLog(double durationMilliseconds, int warningCount, int softFailureCount, int noteCount)
        {
            if (_traceMessages.Count == 0)
            {
                return;
            }

            string methodName = _originalMethod?.DeclaringType != null
                ? _originalMethod.DeclaringType.FullName + "." + _originalMethod.Name
                : _originalMethod?.Name ?? "<unknown method>";

            var lines = new List<string>
            {
                "Method: " + methodName,
                "Duration: " + durationMilliseconds.ToString("F2") + " ms",
                "Diagnostics: warnings=" + warningCount + ", softFailures=" + softFailureCount + ", notes=" + noteCount
            };
            lines.AddRange(_traceMessages);
            _traceMessages.Clear();

            MMLog.WriteDebugBlock("[FluentTranspiler:" + (_callerMod ?? "Unknown") + "] Trace", lines);
        }

        private static TranspilerDiagnosticCategory ClassifyDiagnostic(string message, TranspilerDiagnosticSeverity severity)
        {
            if (string.IsNullOrEmpty(message))
            {
                return TranspilerDiagnosticCategory.General;
            }

            if (message.IndexOf("match", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("found", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return TranspilerDiagnosticCategory.Match;
            }

            if (message.IndexOf("stack", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("validate", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return TranspilerDiagnosticCategory.Validation;
            }

            if (message.IndexOf("[CRITICAL", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("unsafe", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("rollback", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return TranspilerDiagnosticCategory.Safety;
            }

            return severity == TranspilerDiagnosticSeverity.Note
                ? TranspilerDiagnosticCategory.Trace
                : TranspilerDiagnosticCategory.General;
        }

        /// <summary>
        /// Resolves preserve mode for pattern replacement.
        /// In safe mode we can automatically force preserve=true to avoid branch targets
        /// jumping into removed instruction spans.
        /// </summary>
        private bool ResolvePatternPreserveMode(bool requestedPreserveInstructionCount, int patternLength)
        {
            bool effective = TranspilerSafetyPolicy.ResolvePreserveInstructionCount(requestedPreserveInstructionCount);
            if (TranspilerSafetyPolicy.IsPreserveEscalated(requestedPreserveInstructionCount, effective))
            {
                TranspilerSafetyPolicy.WarnPreserveEscalation(_callerMod, _originalMethod != null ? _originalMethod.Name : "UnknownMethod");
            }

            if (!effective && patternLength > 1)
            {
                AddWarning("[CRITICAL SAFETY] ReplaceAllPatterns requested preserveInstructionCount=false for a multi-instruction pattern. This can invalidate branch targets.");
            }
            return effective;
        }

        /// <summary>
        /// Replaces the instruction at the current matcher position while meticulously preserving labels.
        /// This is a critical internal helper that prevents "Ghost Jumps" when replacing logic.
        /// </summary>
        /// <remarks>
        /// If the original instruction was a jump target (had labels), this method copies those labels 
        /// to the new instruction before setting it. This ensures that any <c>br</c> or <c>beq</c> 
        /// instructions in the rest of the method body still land on your new logic.
        /// </remarks>
        private void SetInstructionSafe(CodeInstruction newInstr)
        {
            var oldInstr = _matcher.Instruction;
            if (oldInstr.labels != null && oldInstr.labels.Count > 0)
            {
                newInstr.labels.AddRange(oldInstr.labels);
            }
            if (oldInstr.blocks != null && oldInstr.blocks.Count > 0)
            {
                newInstr.blocks.AddRange(oldInstr.blocks);
            }
            _matcher.SetInstruction(newInstr);
            InvalidateLabelIndexCache();
        }

        private void InvalidateLabelIndexCache()
        {
            _labelIndexCacheDirty = true;
        }

        private void RebuildLabelIndexCache()
        {
            _labelIndexCache.Clear();
            var instructions = _matcher.Instructions();
            for (int i = 0; i < instructions.Count; i++)
            {
                var labels = instructions[i].labels;
                if (labels == null || labels.Count == 0) continue;
                for (int j = 0; j < labels.Count; j++)
                {
                    var label = labels[j];
                    if (!_labelIndexCache.ContainsKey(label))
                    {
                        _labelIndexCache[label] = i;
                    }
                }
            }
            _labelIndexCacheDirty = false;
        }

        private void RecordPatchEdit(
            string kind,
            int startBefore,
            IEnumerable<CodeInstruction> removedInstructions,
            int startAfter,
            IEnumerable<CodeInstruction> addedInstructions,
            string note,
            string confidence)
        {
            _patchEdits.Add(new TranspilerDebugger.PatchEdit
            {
                Kind = kind ?? string.Empty,
                StartIndexBefore = Math.Max(0, startBefore),
                RemovedCount = removedInstructions != null ? removedInstructions.Count() : 0,
                StartIndexAfter = Math.Max(0, startAfter),
                AddedCount = addedInstructions != null ? addedInstructions.Count() : 0,
                RemovedInstructions = removedInstructions != null ? removedInstructions.Select(i => i != null ? i.ToString() : string.Empty).ToList() : new List<string>(),
                AddedInstructions = addedInstructions != null ? addedInstructions.Select(i => i != null ? i.ToString() : string.Empty).ToList() : new List<string>(),
                Note = note ?? string.Empty,
                Confidence = confidence ?? "mapped"
            });
        }

        #endregion
    }
    
    #region Extension Methods
    
    /// <summary>Extension methods for fluent usage.</summary>
    public static class FluentTranspilerExtensions
    {
        /// <summary>Pipe extension for chaining with debugger.</summary>
        public static T Pipe<T>(this T input, Func<T, T> func) => func(input);
        
        /// <summary>Pipe with side effect (doesn't transform).</summary>
        public static T Tap<T>(this T input, Action<T> action)
        {
            action(input);
            return input;
        }
    }
    
    #endregion
}
