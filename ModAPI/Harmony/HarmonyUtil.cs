using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using ModAPI.Core;
using ModAPI.Spine;

namespace ModAPI.Harmony
{
    public static class HarmonyUtil
    {
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

        public sealed class PatchOptions
        {
            public bool AllowDebugPatches;
            public bool AllowDangerousPatches;
            public bool AllowStructReturns;
            public string[] Before;
            public string[] After;
            public int? Priority;
            public Action<object, string> OnResult;
        }

        private static readonly HashSet<string> SensitiveDeny = new HashSet<string>(StringComparer.Ordinal)
        {
            "ExplorationParty.PopState",
            "FamilyMember.OnDestroy"
        };

        public static void PatchAll(HarmonyLib.Harmony h, Assembly asm, PatchOptions options)
        {
            if (h == null || asm == null) return;
            if (options == null) options = new PatchOptions();

            foreach (var type in SafeTypes(asm))
            {
                PatchType(h, type, options);
            }
        }

        public static IList<MethodBase> PatchType(HarmonyLib.Harmony h, Type type, PatchOptions options)
        {
            if (h == null || type == null) return new MethodBase[0];
            if (options == null) options = new PatchOptions();

            try
            {
                if (!HasHarmonyPatchAttributes(type)) return new MethodBase[0];

                if (!options.AllowDebugPatches && HasDebugAttribute(type))
                {
                    options.OnResult?.Invoke((object)type, "skipped: DebugPatch not enabled");
                    return new MethodBase[0];
                }

                if (!options.AllowDangerousPatches && HasDangerousAttribute(type))
                {
                    options.OnResult?.Invoke((object)type, "skipped: Dangerous not enabled");
                    return new MethodBase[0];
                }

                var targets = GetPatchTargets(type);
                if (targets != null)
                {
                    foreach (var m in targets)
                    {
                        var key = TargetKey(m);
                        if (!options.AllowDangerousPatches && SensitiveDeny.Contains(key) && !HasDangerousAttribute(type))
                        {
                            options.OnResult?.Invoke((object)m, "skipped: sensitive target requires [Dangerous]");
                            return new MethodBase[0];
                        }
                        if (!options.AllowStructReturns && IsStructReturn(m) && !HasDangerousAttribute(type))
                        {
                            options.OnResult?.Invoke((object)m, "skipped: struct-return target not allowed");
                            return new MethodBase[0];
                        }
                    }
                }

                var proc = new PatchClassProcessor(h, type);
                var patched = proc.Patch();

                if (patched != null && patched.Count > 0)
                {
                    foreach (var m in patched)
                        options.OnResult?.Invoke((object)m, "patched");
                    return patched.Cast<MethodBase>().ToList();
                }

                options.OnResult?.Invoke((object)type, "no methods patched");
                return new MethodBase[0];
            }
            catch (Exception ex)
            {
                options.OnResult?.Invoke((object)type, "error: " + ex.Message);
                return new MethodBase[0];
            }
        }

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
            catch (Exception ex) { MMLog.WarnOnce("HarmonyUtil.PatchAll.Settings", "Error reading settings: " + ex.Message); }
            opts.OnResult = (mb, reason) =>
            {
                try
                {
                    // DynamicMethod can have null DeclaringType; guard to avoid noisy warnings.
                    var method = mb as MethodBase;
                    var declaring = method != null ? (method.DeclaringType != null ? method.DeclaringType.FullName : "<dynamic>") : null;
                    var who = method != null ? declaring + "." + method.Name : (mb != null ? mb.ToString() : "<null>");
                    ctx?.Log?.Info("Patch: " + who + " -> " + reason);
                }
                catch (Exception ex)
                {
                    // Suppress warning noise; log once to debug channel only.
                    MMLog.WriteDebug("HarmonyUtil.OnResult logging skipped: " + ex.Message);
                }
            };
            PatchAll(h, asm, opts);
        }

        public static bool IsStructReturn(MethodBase m)
        {
            var mi = m as MethodInfo;
            if (mi == null) return false;
            try { return mi.ReturnType != null && mi.ReturnType.IsValueType && mi.ReturnType != typeof(void); }
            catch (Exception ex) { MMLog.WarnOnce("HarmonyUtil.IsStructReturn", "Error checking for struct return: " + ex.Message); return false; }
        }

        private static string TargetKey(MethodBase mb)
        {
            try { return mb.DeclaringType.FullName + "." + mb.Name; }
            catch (Exception ex) { MMLog.WarnOnce("HarmonyUtil.TargetKey", "Error getting target key: " + ex.Message); return "<unknown>"; }
        }

        public static IEnumerable<Type> SafeTypes(Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch (ReflectionTypeLoadException rtle) { return rtle.Types.Where(t => t != null); }
            catch (Exception ex) { MMLog.WarnOnce("HarmonyUtil.SafeTypes", "Error getting types from assembly: " + ex.Message); return Enumerable.Empty<Type>(); }
        }

        public static bool HasHarmonyPatchAttributes(Type t)
        {
            try
            {
                if (t == null)
                    return false;

                if (CustomAttributeData.GetCustomAttributes(t).Any(a => HasHarmonyAttributeName(GetAttributeTypeName(a))))
                    return true;

                var methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                return methods.Any(m => CustomAttributeData.GetCustomAttributes(m).Any(a => HasHarmonyAttributeName(GetAttributeTypeName(a))));
            }
            catch (Exception ex)
            {
                if (!(ex is ReflectionTypeLoadException) && !(ex is TypeLoadException) && !(ex is FileNotFoundException))
                    MMLog.WarnOnce("HarmonyUtil.HasHarmonyPatchAttributes", "Error checking for Harmony attributes: " + ex.Message);
                return false;
            }
        }

        private static bool HasHarmonyAttributeName(string fullName)
        {
            return !string.IsNullOrEmpty(fullName) && fullName.StartsWith("HarmonyLib.Harmony", StringComparison.Ordinal);
        }

        private static string GetAttributeTypeName(CustomAttributeData attribute)
        {
            try
            {
                if (attribute == null)
                    return null;
                if (attribute.Constructor != null && attribute.Constructor.DeclaringType != null)
                    return attribute.Constructor.DeclaringType.FullName;
                if (attribute.AttributeType != null)
                    return attribute.AttributeType.FullName;
            }
            catch
            {
            }

            return null;
        }

        public static bool HasDebugAttribute(Type t)
        {
            return HasAttribute<DebugPatchAttribute>(t);
        }

        public static bool HasDangerousAttribute(Type t)
        {
            return HasAttribute<DangerousAttribute>(t);
        }

        private static bool HasAttribute<T>(Type t) where T : Attribute
        {
            try { return t.GetCustomAttributes(typeof(T), false).FirstOrDefault() != null; }
            catch (Exception ex) { MMLog.WarnOnce("HarmonyUtil.HasAttribute", "Error checking for attribute: " + ex.Message); return false; }
        }

        public static IEnumerable<MethodBase> GetPatchTargets(Type patchClass)
        {
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
            catch (Exception ex) { MMLog.WarnOnce("HarmonyUtil.TryGetTargets.TargetMethods", "Error invoking TargetMethods: " + ex.Message); }

            try
            {
                var tm = patchClass.GetMethod("TargetMethod", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, Type.EmptyTypes, null);
                if (tm != null)
                {
                    var mb = tm.Invoke(null, null) as MethodBase;
                    if (mb != null) return new[] { mb };
                }
            }
            catch (Exception ex) { MMLog.WarnOnce("HarmonyUtil.TryGetTargets.TargetMethod", "Error invoking TargetMethod: " + ex.Message); }

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
                    catch (Exception ex) { MMLog.WarnOnce("HarmonyUtil.TryGetTargets.GetMethod", "Error getting method: " + ex.Message); }
                }
                if (list.Count > 0) return list;
            }
            catch (Exception ex) { MMLog.WarnOnce("HarmonyUtil.TryGetTargets.Attributes", "Error reading HarmonyPatch attributes: " + ex.Message); }

            return null;
        }

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
            catch (Exception ex) { MMLog.WarnOnce("HarmonyUtil.IsLoaded", "Error checking if manager is loaded: " + ex.Message); return false; }
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
                var p = t.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (p != null) return p.GetValue(null, null);
            }
            catch (Exception ex) { MMLog.WarnOnce("HarmonyUtil.GetSingletonInstance.Instance", "Error getting singleton instance (Instance): " + ex.Message); }
            try
            {
                var p = t.GetProperty("instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (p != null) return p.GetValue(null, null);
            }
            catch (Exception ex) { MMLog.WarnOnce("HarmonyUtil.GetSingletonInstance.instance", "Error getting singleton instance (instance): " + ex.Message); }
            return null;
        }

        private static void SafeInvoke(Action a) { try { a(); } catch (Exception ex) { MMLog.Write("PatchWhenLoaded action failed: " + ex.Message); } }

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
                int count = _queue.Count;
                if (count <= 0) return;
                var it = _queue.Peek();
                bool ready = false;
                try { ready = it.Condition(); }
                catch (Exception ex) { MMLog.WarnOnce("LoadGateRunner.Update", "Condition check failed: " + ex.Message); ready = false; }
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
