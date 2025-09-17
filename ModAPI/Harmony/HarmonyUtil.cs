using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ModAPI.Harmony
{
    /// <summary>
    /// Safer Harmony patching helpers with gating and readiness checks.
    /// </summary>
    public static class HarmonyUtil
    {
        // ---- Attributes ----------------------------------------------------

        [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
        public sealed class DebugPatchAttribute : Attribute
        {
            public string Key;
            public DebugPatchAttribute() { }
            public DebugPatchAttribute(string key) { Key = key; }
        }

        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
        public sealed class DangerousAttribute : Attribute
        {
            public string Reason;
            public DangerousAttribute() { }
            public DangerousAttribute(string reason) { Reason = reason; }
        }

        // ---- Options -------------------------------------------------------

        public sealed class PatchOptions
        {
            public bool AllowDebugPatches;
            public bool AllowDangerousPatches;
            public bool AllowStructReturns;
            public string[] Before;   // global Before ids (best-effort)
            public string[] After;    // global After ids (best-effort)
            public int? Priority;     // global priority (best-effort)
            public Action<object, string> OnResult; // per-target applied/skipped reason (Type or MethodBase)
        }

        // Denylist for sensitive targets unless explicitly allowed
        private static readonly HashSet<string> SensitiveDeny = new HashSet<string>(StringComparer.Ordinal)
        {
            "ExplorationParty.PopState",
            "FamilyMember.OnDestroy"
        };

        // ---- PatchAll with gating -----------------------------------------

        public static void PatchAll(HarmonyLib.Harmony h, Assembly asm, PatchOptions options)
        {
            if (h == null || asm == null) return;
            if (options == null) options = new PatchOptions();

            foreach (var type in SafeTypes(asm))
            {
                try
                {
                    // Skip non-patch classes quickly
                    if (!HasHarmonyPatchAttributes(type)) continue;

                    // Gate: [DebugPatch]
                    if (!options.AllowDebugPatches && HasAttribute<DebugPatchAttribute>(type))
                    {
                        options.OnResult?.Invoke((object)type, "skipped: DebugPatch not enabled");
                        continue;
                    }

                    // Gate: [Dangerous]
                    if (!options.AllowDangerousPatches && HasAttribute<DangerousAttribute>(type))
                    {
                        options.OnResult?.Invoke((object)type, "skipped: Dangerous not enabled");
                        continue;
                    }

                    // Attempt to resolve target methods for struct-return/sensitive checks
                    var targets = TryGetTargets(type);
                    if (targets != null)
                    {
                        bool skip = false;
                        foreach (var m in targets)
                        {
                            var key = TargetKey(m);
                            if (!options.AllowDangerousPatches && SensitiveDeny.Contains(key) && !HasAttribute<DangerousAttribute>(type))
                            {
                                options.OnResult?.Invoke((object)m, "skipped: sensitive target requires [Dangerous]");
                                skip = true; break;
                            }
                            if (!options.AllowStructReturns && IsStructReturn(m) && !HasAttribute<DangerousAttribute>(type))
                            {
                                options.OnResult?.Invoke((object)m, "skipped: struct-return target not allowed");
                                skip = true; break;
                            }
                        }
                        if (skip) continue;
                    }

                    // Apply patches via Harmony's PatchClassProcessor
                    var proc = new PatchClassProcessor(h, type);
                    var patched = proc.Patch();

                    // Report results
                    if (patched != null && patched.Count > 0)
                    {
                        foreach (var m in patched)
                            options.OnResult?.Invoke((object)m, "patched");
                    }
                    else
                    {
                        options.OnResult?.Invoke((object)type, "no methods patched");
                    }
                }
                catch (Exception ex)
                {
                    options.OnResult?.Invoke((object)type, "error: " + ex.Message);
                }
            }
        }

        // Convenience: read common toggles from plugin context settings
        public static void PatchAll(HarmonyLib.Harmony h, Assembly asm, IPluginContext ctx)
        {
            var opts = new PatchOptions();
            try
            {
                if (ctx != null && ctx.Settings != null)
                {
                    opts.AllowDebugPatches = ctx.Settings.GetBool("enableDebugPatches", false);
                    opts.AllowDangerousPatches = ctx.Settings.GetBool("dangerousPatches", false);
                    opts.AllowStructReturns = ctx.Settings.GetBool("allowStructReturns", false);
                }
            }
            catch { }
            opts.OnResult = (mb, reason) =>
            {
                try
                {
                    var who = mb is MethodBase ? ((MethodBase)mb).DeclaringType.FullName + "." + ((MethodBase)mb).Name : (mb != null ? mb.ToString() : "<null>");
                    ctx?.Log?.Info("Patch: " + who + " -> " + reason);
                }
                catch { }
            };
            PatchAll(h, asm, opts);
        }

        public static bool IsStructReturn(MethodBase m)
        {
            var mi = m as MethodInfo;
            if (mi == null) return false;
            try { return mi.ReturnType != null && mi.ReturnType.IsValueType && mi.ReturnType != typeof(void); }
            catch { return false; }
        }

        private static string TargetKey(MethodBase mb)
        {
            try { return mb.DeclaringType.FullName + "." + mb.Name; } catch { return "<unknown>"; }
        }

        private static IEnumerable<Type> SafeTypes(Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch (ReflectionTypeLoadException rtle) { return rtle.Types.Where(t => t != null); }
            catch { return Enumerable.Empty<Type>(); }
        }

        private static bool HasHarmonyPatchAttributes(Type t)
        {
            try
            {
                return t.GetCustomAttributes(true).Any(a => string.Equals(a.GetType().FullName, "HarmonyLib.HarmonyPatch", StringComparison.Ordinal));
            }
            catch { return false; }
        }

        private static bool HasAttribute<T>(Type t) where T : Attribute
        {
            try { return t.GetCustomAttributes(typeof(T), false).FirstOrDefault() != null; } catch { return false; }
        }

        private static IEnumerable<MethodBase> TryGetTargets(Type patchClass)
        {
            // 1) TargetMethods(): IEnumerable<MethodBase>
            try
            {
                var tm = patchClass.GetMethod("TargetMethods", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, Type.EmptyTypes, null);
                if (tm != null)
                {
                    var e = tm.Invoke(null, null) as System.Collections.IEnumerable;
                    if (e != null)
                    {
                        var list = new List<MethodBase>();
                        foreach (var it in e) { var mb = it as MethodBase; if (mb != null) list.Add(mb); }
                        if (list.Count > 0) return list;
                    }
                }
            }
            catch { }

            // 2) TargetMethod(): MethodBase
            try
            {
                var tm = patchClass.GetMethod("TargetMethod", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, Type.EmptyTypes, null);
                if (tm != null)
                {
                    var mb = tm.Invoke(null, null) as MethodBase;
                    if (mb != null) return new[] { mb };
                }
            }
            catch { }

            // 3) HarmonyPatch(type, methodName) attributes
            try
            {
                var attrs = patchClass.GetCustomAttributes(true);
                var list = new List<MethodBase>();
                foreach (var a in attrs)
                {
                    var at = a.GetType();
                    if (!string.Equals(at.FullName, "HarmonyLib.HarmonyPatch", StringComparison.Ordinal))
                        continue;

                    var typeProp = at.GetProperty("type") ?? (MemberInfo)at.GetField("type");
                    var nameProp = at.GetProperty("methodName") ?? (MemberInfo)at.GetField("methodName");
                    Type targetType = typeProp is PropertyInfo tp
                        ? tp.GetValue(a, null) as Type
                        : (typeProp is FieldInfo tf ? tf.GetValue(a) as Type : null);
                    string methodName = nameProp is PropertyInfo np
                        ? np.GetValue(a, null) as string
                        : (nameProp is FieldInfo nf ? nf.GetValue(a) as string : null);
                    if (targetType == null || string.IsNullOrEmpty(methodName)) continue;

                    try
                    {
                        var mb = targetType.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                        if (mb != null) list.Add(mb);
                    }
                    catch { }
                }
                if (list.Count > 0) return list;
            }
            catch { }

            return null;
        }

        // ---- Readiness gates ----------------------------------------------

        public static bool IsLoaded<TManager>() where TManager : UnityEngine.Object
        {
            try
            {
                var mgr = GetSingletonInstance(typeof(TManager)) as UnityEngine.Object;
                if (mgr == null) return false;
                if (SaveManager.instance == null) return false;
                var saveable = mgr as ISaveable;
                return saveable != null ? SaveManager.instance.HasBeenLoaded(saveable) : true;
            }
            catch { return false; }
        }

        public static void PatchWhenLoaded<TManager>(HarmonyLib.Harmony h, Action applyPatches) where TManager : UnityEngine.Object
        {
            if (h == null || applyPatches == null) return;
            if (IsLoaded<TManager>()) { SafeInvoke(applyPatches); return; }
            LoadGateRunner.Ensure().Enqueue(() => IsLoaded<TManager>(), applyPatches);
        }

        public static void WaitUntilLoaded<TManager>(Action action) where TManager : UnityEngine.Object
        {
            if (action == null) return;
            if (IsLoaded<TManager>()) { SafeInvoke(action); return; }
            LoadGateRunner.Ensure().Enqueue(() => IsLoaded<TManager>(), action);
        }

        private static object GetSingletonInstance(Type t)
        {
            try
            {
                // Common Unity pattern: public static T Instance { get; }
                var p = t.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (p != null) return p.GetValue(null, null);
            }
            catch { }
            try
            {
                // Some classes use 'instance'
                var p = t.GetProperty("instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (p != null) return p.GetValue(null, null);
            }
            catch { }
            return null;
        }

        private static void SafeInvoke(Action a) { try { a(); } catch (Exception ex) { MMLog.Write("PatchWhenLoaded action failed: " + ex.Message); } }

        // ---- Runner --------------------------------------------------------

        private class LoadGateRunner : MonoBehaviour
        {
            private static LoadGateRunner _inst;
            private readonly Queue<Item> _queue = new Queue<Item>();

            public static LoadGateRunner Ensure()
            {
                if (_inst != null) return _inst;
                var go = new GameObject("ModAPI_LoadGateRunner");
                DontDestroyOnLoad(go);
                _inst = go.AddComponent<LoadGateRunner>();
                return _inst;
            }

            public void Enqueue(Func<bool> condition, Action action)
            {
                _queue.Enqueue(new Item { Condition = condition, Action = action, Deadline = Time.realtimeSinceStartup + 60f });
            }

            private void Update()
            {
                int count = _queue.Count; // cap one per frame
                if (count <= 0) return;
                var it = _queue.Peek();
                bool ready = false;
                try { ready = it.Condition(); } catch { ready = false; }
                if (ready)
                {
                    _queue.Dequeue();
                    SafeInvoke(it.Action);
                }
                else if (Time.realtimeSinceStartup > it.Deadline)
                {
                    _queue.Dequeue();
                    MMLog.WriteDebug("LoadGateRunner: condition timed out");
                }
            }

            private struct Item
            {
                public Func<bool> Condition;
                public Action Action;
                public float Deadline;
            }
        }
    }
}
