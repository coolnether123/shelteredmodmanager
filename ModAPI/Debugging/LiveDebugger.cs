using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using ModAPI.Core;

namespace ModAPI.Debugging
{
    /// <summary>
    /// Manages real-time debugging, method hooking, and execution tracing.
    /// </summary>
    public static class LiveDebugger
    {
        private static HarmonyLib.Harmony _harmony = new HarmonyLib.Harmony("ModAPI.LiveDebugger");
        private static MethodBase _attachedMethod;
        private static Type _attachedType;
        private static readonly List<MethodBase> _attachedMethods = new List<MethodBase>();
        private static Dictionary<MethodBase, MethodTrace> _traces = new Dictionary<MethodBase, MethodTrace>();
        
        // Circular buffer for recent variables/states
        public static Queue<ExecutionFrame> RecentFrames = new Queue<ExecutionFrame>();
        public const int MaxFrames = 50;

        public static bool IsAttached => _attachedMethod != null;
        public static MethodBase AttachedMethod => _attachedMethod;
        public static Type AttachedType => _attachedType;
        public static int AttachedMethodCount => _attachedMethods.Count;

        public static MethodTrace GetTrace(MethodBase method)
        {
            if (method == null) return null;
            MethodTrace trace;
            return _traces.TryGetValue(method, out trace) ? trace : null;
        }

        public static void Attach(MethodBase method)
        {
            if (_attachedMethod == method) return;
            if (_attachedMethod != null) Detach();

            _attachedMethod = method;
            _attachedType = method != null ? method.DeclaringType : null;
            _attachedMethods.Clear();
            if (method != null) _attachedMethods.Add(method);
            _traces[method] = new MethodTrace { Method = method };
            
            MMLog.WriteInfo($"[LiveDebugger] Attaching to {method.Name}...");

            try 
            {
                // We use a Transpiler to inject line tracking, and Prefix/Postfix for timing/args
                _harmony.Patch(
                    method,
                    prefix: new HarmonyMethod(typeof(LiveDebugger), nameof(OnPrefix)),
                    postfix: new HarmonyMethod(typeof(LiveDebugger), nameof(OnPostfix)),
                    transpiler: new HarmonyMethod(typeof(LiveDebugger), nameof(TraceTranspiler))
                );
            }
            catch (Exception ex)
            {
                MMLog.WriteError($"[LiveDebugger] Failed to attach: {ex.Message}");
                Detach();
            }
        }

        public static void AttachAllMethodsInType(Type type)
        {
            if (type == null) return;
            if (_attachedMethod != null || _attachedMethods.Count > 0) Detach();

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            var methods = type.GetMethods(flags);
            if (methods == null || methods.Length == 0) return;

            _attachedType = type;
            _attachedMethod = methods[0];
            _attachedMethods.Clear();

            var attachedCount = 0;
            for (var i = 0; i < methods.Length; i++)
            {
                var method = methods[i];
                if (method == null) continue;
                if (method.IsAbstract) continue;
                if (method.IsGenericMethodDefinition) continue;

                try
                {
                    _harmony.Patch(
                        method,
                        prefix: new HarmonyMethod(typeof(LiveDebugger), nameof(OnPrefix)),
                        postfix: new HarmonyMethod(typeof(LiveDebugger), nameof(OnPostfix)),
                        transpiler: new HarmonyMethod(typeof(LiveDebugger), nameof(TraceTranspiler))
                    );

                    _attachedMethods.Add(method);
                    _traces[method] = new MethodTrace { Method = method };
                    attachedCount++;
                }
                catch (Exception ex)
                {
                    MMLog.WriteWarning("[LiveDebugger] Skipped attach for " + method.Name + ": " + ex.Message);
                }
            }

            if (_attachedMethods.Count > 0)
            {
                _attachedMethod = _attachedMethods[0];
            }

            MMLog.WriteInfo("[LiveDebugger] Attached to type " + type.FullName + " (" + attachedCount + " methods).");
        }

        public static void Detach()
        {
            if (_attachedMethod == null && _attachedMethods.Count == 0) return;

            if (_attachedMethods.Count > 0)
            {
                MMLog.WriteInfo("[LiveDebugger] Detaching from " + _attachedMethods.Count + " method(s)...");
                for (var i = 0; i < _attachedMethods.Count; i++)
                {
                    var method = _attachedMethods[i];
                    if (method == null) continue;
                    _harmony.Unpatch(method, HarmonyPatchType.All, _harmony.Id);
                }
            }
            else if (_attachedMethod != null)
            {
                MMLog.WriteInfo("[LiveDebugger] Detaching from " + _attachedMethod.Name + "...");
                _harmony.Unpatch(_attachedMethod, HarmonyPatchType.All, _harmony.Id);
            }

            _attachedMethod = null;
            _attachedType = null;
            _attachedMethods.Clear();
        }

        // --- HOOKS ---

        public static void OnPrefix(object __instance, object[] __args, MethodBase __originalMethod, out ActiveCallState __state)
        {
            var frame = new ExecutionFrame
            {
                Timestamp = DateTime.Now,
                Instance = __instance,
                Parameters = CaptureParameters(__originalMethod, __args),
                IsPending = true
            };

            if (__instance != null) frame.Fields = CaptureFields(__instance);
            
            // Capture statics for the declaring type
            if (__originalMethod != null)
            {
                frame.Statics = ModAPI.Reflection.StaticInspector.CaptureStatics(__originalMethod.DeclaringType);
            }

            lock (RecentFrames)
            {
                RecentFrames.Enqueue(frame);
                if (RecentFrames.Count > MaxFrames) RecentFrames.Dequeue();
            }

            __state = new ActiveCallState 
            { 
                StartTime = Time.realtimeSinceStartup,
                Instance = __instance,
                Args = __args,
                Frame = frame,
                Method = __originalMethod
            };
        }

        public static void OnPostfix(object __instance, object[] __args, MethodBase __originalMethod, ActiveCallState __state)
        {
            if (_attachedMethod == null || __state == null || __state.Frame == null) return;

            float duration = (Time.realtimeSinceStartup - __state.StartTime) * 1000f; // ms
            if (__originalMethod == null)
            {
                __originalMethod = __state.Method;
            }

            MethodTrace trace;
            if (__originalMethod != null && _traces.TryGetValue(__originalMethod, out trace))
            {
                trace.RecordHit(duration);
            }

            var frame = __state.Frame;
            frame.DurationMs = duration;
            frame.IsPending = false;
            
            // Re-capture fields to see any changes
            if (__instance != null)
            {
                frame.Fields = CaptureFields(__instance);
            }
        }

        public static IEnumerable<CodeInstruction> TraceTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = new List<CodeInstruction>(instructions);
            var result = new List<CodeInstruction>();

            for (int i = 0; i < list.Count; i++)
            {
                // Inject a trace callback before each instruction to capture precise execution flow.
                result.Add(new CodeInstruction(OpCodes.Ldc_I4, i)); 
                result.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(LiveDebugger), nameof(OnTraceStep))));
                
                result.Add(list[i]);
            }

            return result; 
        }

        public static void OnTraceStep(int index)
        {
            // Update the latest frame with the current execution index
            lock (RecentFrames)
            {
                if (RecentFrames.Count > 0)
                {
                    var latest = RecentFrames.Last();
                    latest.CurrentILIndex = index;
                    latest.ExecutionPath.Add(index);
                }
            }
        }

        // --- HELPERS ---

        private static Dictionary<string, object> CaptureParameters(MethodBase method, object[] args)
        {
            var dict = new Dictionary<string, object>();
            if (args == null || method == null) return dict;

            var paramsInfo = method.GetParameters();
            for (int i = 0; i < paramsInfo.Length && i < args.Length; i++)
            {
                dict[paramsInfo[i].Name] = args[i];
            }
            return dict;
        }

        private static Dictionary<string, object> CaptureFields(object instance)
        {
            var dict = new Dictionary<string, object>();
            if (instance == null) return dict;

            var type = instance.GetType();
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                try { dict[field.Name] = field.GetValue(instance); }
                catch { dict[field.Name] = "<error>"; }
            }
            return dict;
        }
    }

    public class MethodTrace
    {
        public MethodBase Method;
        public long HitCount;
        public float TotalDuration;
        
        public float AverageDuration => HitCount > 0 ? TotalDuration / HitCount : 0;

        public void RecordHit(float duration)
        {
            HitCount++;
            TotalDuration += duration;
        }
    }

    public class ActiveCallState
    {
        public float StartTime;
        public object Instance;
        public object[] Args;
        public MethodBase Method;
        public ExecutionFrame Frame;
    }

    public class ExecutionFrame
    {
        public DateTime Timestamp;
        public double DurationMs;
        public object Instance;
        public object Result;
        public bool IsPending;
        public int CurrentILIndex = -1;
        public List<int> ExecutionPath = new List<int>();
        public Dictionary<string, object> Parameters = new Dictionary<string, object>();
        public Dictionary<string, object> Fields = new Dictionary<string, object>();
        public Dictionary<string, object> Locals = new Dictionary<string, object>();
        public Dictionary<string, object> Statics = new Dictionary<string, object>();
    }
}
