using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using UnityEngine;
using ModAPI.Core;

namespace ModAPI.Inspector
{
    /// <summary>
    /// Captures runtime execution frames for a selected method and streams them to snapshot files.
    /// This tracer is intentionally conservative to keep Unity 5.x main-thread overhead low.
    /// </summary>
    public class ExecutionTracer : MonoBehaviour
    {
        public static ExecutionTracer Instance;

        private const int MaxInMemoryFrames = 1000;
        private static int _snapshotSequence;

        private bool _isCapturing;
        private SnapshotBuffer _currentSnapshot;
        private HarmonyLib.Harmony _harmony;
        private StreamingSnapshotWriter _snapshotWriter;

        // Single producer (main thread) + single consumer (disk writer thread).
        private readonly Queue<TraceFrame> _pendingWrites = new Queue<TraceFrame>();
        private readonly object _pendingWriteLock = new object();
        private AutoResetEvent _writeSignal;
        private Thread _writerThread;
        private volatile bool _writerRunning;
        private volatile bool _writerBusy;

        private int _capturedFrameCount;
        private int _droppedFrameCount;
        private bool _losslessTrace;

        private readonly object _lockedVariableLock = new object();
        private readonly HashSet<string> _lockedVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _lastLockedValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public void Awake()
        {
            Instance = this;
            _harmony = new HarmonyLib.Harmony("ModAPI.ExecutionTracer");
            _writeSignal = new AutoResetEvent(false);
        }

        public void OnDestroy()
        {
            try
            {
                StopWriterThread();
            }
            catch { }

            try
            {
                if (_writeSignal != null)
                {
                    _writeSignal.Close();
                    _writeSignal = null;
                }
            }
            catch { }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        public bool IsCapturing
        {
            get { return _isCapturing; }
        }

        public bool LosslessTrace
        {
            get { return _losslessTrace; }
        }

        public bool IsWriterBusy
        {
            get
            {
                if (_writerBusy) return true;
                lock (_pendingWriteLock)
                {
                    return _pendingWrites.Count > 0;
                }
            }
        }

        public SnapshotBuffer CurrentSnapshot
        {
            get { return _currentSnapshot; }
        }

        public void SetLosslessTrace(bool enabled)
        {
            if (_losslessTrace == enabled) return;
            _losslessTrace = enabled;
            MMLog.WriteInfo("[ExecutionTracer] Lossless Trace " + (_losslessTrace ? "enabled." : "disabled."));
        }

        public void SetLockedVariables(IEnumerable<string> variableNames)
        {
            lock (_lockedVariableLock)
            {
                _lockedVariables.Clear();
                _lastLockedValues.Clear();

                if (variableNames == null) return;

                foreach (var variableName in variableNames)
                {
                    if (!string.IsNullOrEmpty(variableName))
                    {
                        _lockedVariables.Add(variableName);
                    }
                }
            }

            MMLog.WriteDebug("[ExecutionTracer] Locked variable count: " + _lockedVariables.Count);
        }

        public void BeginSnapshot(MethodBase method, int startILOffset)
        {
            if (method == null) return;
            if (_isCapturing)
            {
                MMLog.WriteWarning("[ExecutionTracer] BeginSnapshot ignored because capture is already active.");
                return;
            }

            var snapshotDir = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ModAPI"), "Snapshots");
            if (!Directory.Exists(snapshotDir)) Directory.CreateDirectory(snapshotDir);

            var seq = Interlocked.Increment(ref _snapshotSequence);
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            var id = stamp + "_" + seq.ToString("000");

            _capturedFrameCount = 0;
            _droppedFrameCount = 0;

            lock (_pendingWriteLock)
            {
                _pendingWrites.Clear();
            }

            lock (_lockedVariableLock)
            {
                _lastLockedValues.Clear();
            }

            _currentSnapshot = new SnapshotBuffer
            {
                StartMethod = method,
                StartOffset = startILOffset,
                StartTime = DateTime.UtcNow,
                Frames = new List<TraceFrame>(MaxInMemoryFrames),
                SnapshotFilePath = Path.Combine(snapshotDir, "snapshot_" + id + ".bin"),
                SnapshotMetaPath = Path.Combine(snapshotDir, "snapshot_" + id + ".meta"),
                LosslessTrace = _losslessTrace
            };

            _snapshotWriter = new StreamingSnapshotWriter();
            _snapshotWriter.BeginSnapshot(_currentSnapshot.SnapshotFilePath, _currentSnapshot);
            StartWriterThread();

            _isCapturing = true;
            MMLog.WriteInfo("[ExecutionTracer] Hooking " + BuildMethodName(method) + " at IL_" + startILOffset.ToString("X4"));

            try
            {
                _harmony.Patch(
                    original: method,
                    prefix: new HarmonyMethod(GetType(), nameof(OnMethodEntry)),
                    postfix: new HarmonyMethod(GetType(), nameof(OnMethodExit))
                );
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[ExecutionTracer] Failed to hook method: " + ex.Message);
                _isCapturing = false;
                StopWriterThread();

                if (_snapshotWriter != null)
                {
                    _snapshotWriter.Dispose();
                    _snapshotWriter = null;
                }
            }
        }

        public void EndSnapshot(int endILOffset)
        {
            if (!_isCapturing || _currentSnapshot == null) return;

            _isCapturing = false;
            _currentSnapshot.EndOffset = endILOffset;
            _currentSnapshot.DroppedFrameCount = _droppedFrameCount;

            if (_currentSnapshot.StartMethod != null)
            {
                _harmony.Unpatch(_currentSnapshot.StartMethod, HarmonyPatchType.Prefix, _harmony.Id);
                _harmony.Unpatch(_currentSnapshot.StartMethod, HarmonyPatchType.Postfix, _harmony.Id);
                MMLog.WriteInfo("[ExecutionTracer] Unhooked " + BuildMethodName(_currentSnapshot.StartMethod));
            }

            StopWriterThread();

            if (_snapshotWriter != null)
            {
                _snapshotWriter.EndSnapshot(_currentSnapshot.EndOffset, _currentSnapshot.SnapshotMetaPath, _currentSnapshot);
                _snapshotWriter = null;
            }

            MMLog.WriteInfo("[ExecutionTracer] Snapshot complete. Frames=" + _capturedFrameCount + ", Dropped=" + _droppedFrameCount + ", Lossless=" + _losslessTrace);
        }

        public void FlushToDiskAndClearBuffer()
        {
            if (_currentSnapshot == null || _currentSnapshot.Frames == null) return;

            if (_currentSnapshot.Frames.Count > 64)
            {
                _currentSnapshot.Frames.RemoveRange(0, _currentSnapshot.Frames.Count - 64);
            }
            else
            {
                _currentSnapshot.Frames.Clear();
            }
        }

        public static void OnMethodEntry(object __instance, object[] __args, MethodBase __originalMethod, ref long __state)
        {
            __state = DateTime.UtcNow.Ticks;

            if (Instance == null || !Instance._isCapturing || Instance._currentSnapshot == null) return;

            var frame = new TraceFrame
            {
                Timestamp = DateTime.UtcNow,
                MethodName = BuildMethodName(__originalMethod),
                ILOffset = Instance.ResolveCurrentILOffset(__originalMethod),
                Variables = CaptureArgs(__originalMethod, __args),
                Fields = CaptureInstanceFields(__instance),
                Statics = ModAPI.Reflection.StaticInspector.CaptureStatics(__originalMethod.DeclaringType),
                MemoryUsage = GC.GetTotalMemory(false),
                ExecutionTimeMs = 0d,
                WasModified = false,
                ModifiedVariables = new List<string>()
            };

            frame.WasModified = false;
            frame.ModifiedVariables = new List<string>();

            Instance.AppendFrame(frame);

            if (MemoryGovernor.CheckMemoryPressure())
            {
                Instance.FlushToDiskAndClearBuffer();
            }
        }

        public static void OnMethodExit(MethodBase __originalMethod, long __state)
        {
            if (Instance == null || Instance._currentSnapshot == null || Instance._currentSnapshot.Frames == null) return;
            if (Instance._currentSnapshot.Frames.Count == 0) return;

            var elapsedMs = (DateTime.UtcNow.Ticks - __state) / (double)TimeSpan.TicksPerMillisecond;
            var methodName = BuildMethodName(__originalMethod);
            for (var i = Instance._currentSnapshot.Frames.Count - 1; i >= 0; i--)
            {
                var frame = Instance._currentSnapshot.Frames[i];
                if (frame != null && frame.MethodName == methodName && frame.ExecutionTimeMs <= 0d)
                {
                    frame.ExecutionTimeMs = elapsedMs;
                    frame.MemoryUsage = GC.GetTotalMemory(false);
                    break;
                }
            }
        }

        private int ResolveCurrentILOffset(MethodBase method)
        {
            if (_currentSnapshot == null || method == null) return 0;
            if (_currentSnapshot.StartMethod == method)
            {
                return _currentSnapshot.StartOffset;
            }

            return 0;
        }

        private void AppendFrame(TraceFrame frame)
        {
            if (_currentSnapshot == null || _currentSnapshot.Frames == null || frame == null) return;

            _capturedFrameCount++;
            frame.FrameIndex = _capturedFrameCount;

            var writerBusy = IsWriterBusy;
            if (_capturedFrameCount > MaxInMemoryFrames && writerBusy)
            {
                if (_losslessTrace)
                {
                    // Option B (explicit): block briefly while the disk writer catches up.
                    var waited = 0;
                    while (IsWriterBusy && waited < 250)
                    {
                        Thread.Sleep(1);
                        waited++;
                    }
                }
            }

            if (_currentSnapshot.Frames.Count >= MaxInMemoryFrames)
            {
                _currentSnapshot.Frames.RemoveAt(0);
                _droppedFrameCount++;

                if (!_losslessTrace && (_droppedFrameCount == 1 || (_droppedFrameCount % 100) == 0))
                {
                    MMLog.WriteWarning("[ExecutionTracer] Circular buffer overwrite count: " + _droppedFrameCount);
                }
            }

            _currentSnapshot.Frames.Add(frame);
            QueueFrameWrite(frame);
        }

        private void QueueFrameWrite(TraceFrame frame)
        {
            if (_snapshotWriter == null || frame == null) return;

            lock (_pendingWriteLock)
            {
                _pendingWrites.Enqueue(frame);
            }

            if (_writeSignal != null)
            {
                _writeSignal.Set();
            }
        }

        private void StartWriterThread()
        {
            if (_writerRunning) return;

            _writerRunning = true;
            _writerThread = new Thread(WriterThreadLoop);
            _writerThread.IsBackground = true;
            _writerThread.Name = "ModAPI.ExecutionTracer.Writer";
            _writerThread.Start();
        }

        private void StopWriterThread()
        {
            if (!_writerRunning && _writerThread == null) return;

            _writerRunning = false;
            if (_writeSignal != null)
            {
                _writeSignal.Set();
            }

            if (_writerThread != null)
            {
                try
                {
                    if (!_writerThread.Join(1500))
                    {
                        MMLog.WriteWarning("[ExecutionTracer] Writer thread join timeout; continuing shutdown.");
                    }
                }
                catch (Exception ex)
                {
                    MMLog.WriteWarning("[ExecutionTracer] Writer thread join error: " + ex.Message);
                }
                finally
                {
                    _writerThread = null;
                }
            }

            _writerBusy = false;
        }

        private void WriterThreadLoop()
        {
            while (_writerRunning || HasPendingWrites())
            {
                TraceFrame frame = null;
                lock (_pendingWriteLock)
                {
                    if (_pendingWrites.Count > 0)
                    {
                        frame = _pendingWrites.Dequeue();
                    }
                }

                if (frame == null)
                {
                    if (_writeSignal != null)
                    {
                        _writeSignal.WaitOne(10);
                    }
                    continue;
                }

                try
                {
                    _writerBusy = true;
                    FlagLockedVariableMutations(frame); // Process mutations on background thread

                    if (_snapshotWriter != null)
                    {
                        _snapshotWriter.WriteFrame(frame);
                    }
                }
                catch (Exception ex)
                {
                    MMLog.WriteError("[ExecutionTracer] Failed to write frame: " + ex.Message);
                }
                finally
                {
                    _writerBusy = false;
                }
            }
        }

        private bool HasPendingWrites()
        {
            lock (_pendingWriteLock)
            {
                return _pendingWrites.Count > 0;
            }
        }

        private void FlagLockedVariableMutations(TraceFrame frame)
        {
            if (frame == null) return;

            List<string> changed = null;
            lock (_lockedVariableLock)
            {
                if (_lockedVariables.Count == 0)
                {
                    return;
                }

                foreach (var variableName in _lockedVariables)
                {
                    var currentValue = ResolveVariableValue(frame, variableName);
                    string previousValue;
                    if (_lastLockedValues.TryGetValue(variableName, out previousValue))
                    {
                        if (!string.Equals(previousValue, currentValue, StringComparison.Ordinal))
                        {
                            if (changed == null) changed = new List<string>();
                            changed.Add(variableName);
                        }
                    }

                    _lastLockedValues[variableName] = currentValue;
                }
            }

            if (changed != null && changed.Count > 0)
            {
                frame.WasModified = true;
                frame.ModifiedVariables = changed;
                MMLog.WriteDebug("[ExecutionTracer] Locked variable mutation detected: " + string.Join(", ", changed.ToArray()));
            }
        }

        private static string ResolveVariableValue(TraceFrame frame, string variableName)
        {
            if (frame == null || string.IsNullOrEmpty(variableName))
            {
                return "<null>";
            }

            object value;
            if (frame.Variables != null && frame.Variables.TryGetValue(variableName, out value))
            {
                return SafeToString(value);
            }

            if (frame.Fields != null && frame.Fields.TryGetValue(variableName, out value))
            {
                return SafeToString(value);
            }

            // Accept "Health" lock names for "this.Health" field captures.
            if (!variableName.StartsWith("this.", StringComparison.OrdinalIgnoreCase))
            {
                var thisKey = "this." + variableName;
                if (frame.Fields != null && frame.Fields.TryGetValue(thisKey, out value))
                {
                    return SafeToString(value);
                }
            }

            return "<missing>";
        }

        private static string SafeToString(object obj)
        {
            if (obj == null) return "null";
            try
            {
                // Basic string conversion. Note: In Unity, some objects (like destroyed components) 
                // might throw if ToString() is called from a background thread.
                // However, most standard types (int, float, string) and simple classes are safe.
                return obj.ToString();
            }
            catch
            {
                return "<error: " + obj.GetType().Name + ">";
            }
        }

        private static Dictionary<string, object> CaptureArgs(MethodBase method, object[] args)
        {
            var ret = new Dictionary<string, object>();
            if (method == null || args == null) return ret;

            var parameters = method.GetParameters();
            for (var i = 0; i < parameters.Length && i < args.Length; i++)
            {
                var key = string.IsNullOrEmpty(parameters[i].Name) ? "arg" + i : parameters[i].Name;
                ret[key] = args[i];
            }

            return ret;
        }

        private static Dictionary<string, object> CaptureInstanceFields(object instance)
        {
            var fields = new Dictionary<string, object>();
            if (instance == null) return fields;

            try
            {
                var binding = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                var infos = instance.GetType().GetFields(binding);
                for (var i = 0; i < infos.Length; i++)
                {
                    var f = infos[i];
                    var key = "this." + f.Name;
                    try
                    {
                        fields[key] = f.GetValue(instance);
                    }
                    catch
                    {
                        fields[key] = "<unavailable>";
                    }
                }
            }
            catch
            {
            }

            return fields;
        }

        private static string BuildMethodName(MethodBase method)
        {
            if (method == null) return "Unknown";
            if (method.DeclaringType == null) return method.Name;
            return method.DeclaringType.FullName + "." + method.Name;
        }
    }

    public class SnapshotBuffer
    {
        public MethodBase StartMethod;
        public int StartOffset;
        public int EndOffset;
        public DateTime StartTime;
        public List<TraceFrame> Frames;
        public string SnapshotFilePath;
        public string SnapshotMetaPath;
        public bool LosslessTrace;
        public int DroppedFrameCount;
    }

    [Serializable]
    public class TraceFrame
    {
        public DateTime Timestamp;
        public string MethodName;
        public int ILOffset;
        public Dictionary<string, object> Variables = new Dictionary<string, object>();
        public Dictionary<string, object> Fields = new Dictionary<string, object>();
        public Dictionary<string, object> Statics = new Dictionary<string, object>();
        public long MemoryUsage;
        public double ExecutionTimeMs;
        public bool WasModified;
        public List<string> ModifiedVariables = new List<string>();
        public int FrameIndex;

        public void Write(BinaryWriter writer)
        {
            writer.Write(Timestamp.Ticks);
            writer.Write(MethodName ?? "<unknown>");
            writer.Write(ILOffset);
            writer.Write(WasModified);
            WriteList(writer, ModifiedVariables);
            writer.Write(FrameIndex);
            writer.Write(MemoryUsage);
            writer.Write(ExecutionTimeMs);
            WriteMap(writer, Variables);
            WriteMap(writer, Fields);
            WriteMap(writer, Statics);
        }

        public static TraceFrame Read(BinaryReader reader, ushort version)
        {
            var frame = new TraceFrame();
            frame.Timestamp = new DateTime(reader.ReadInt64(), DateTimeKind.Utc);
            frame.MethodName = reader.ReadString();
            frame.ILOffset = reader.ReadInt32();
            frame.WasModified = reader.ReadBoolean();
            frame.ModifiedVariables = ReadList(reader);
            frame.FrameIndex = reader.ReadInt32();
            frame.MemoryUsage = reader.ReadInt64();
            frame.ExecutionTimeMs = reader.ReadDouble();
            frame.Variables = ReadMap(reader);
            frame.Fields = ReadMap(reader);
            if (version >= 4) frame.Statics = ReadMap(reader);
            return frame;
        }

        private static void WriteMap(BinaryWriter writer, Dictionary<string, object> map)
        {
            if (map == null) { writer.Write(0); return; }
            writer.Write(map.Count);
            foreach (var kv in map)
            {
                writer.Write(kv.Key ?? string.Empty);
                writer.Write(kv.Value != null ? kv.Value.ToString() : "null");
            }
        }

        private static Dictionary<string, object> ReadMap(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            var dict = new Dictionary<string, object>(count);
            for (int i = 0; i < count; i++)
            {
                string key = reader.ReadString();
                string val = reader.ReadString();
                dict[key] = val;
            }
            return dict;
        }

        private static void WriteList(BinaryWriter writer, List<string> list)
        {
            if (list == null) { writer.Write(0); return; }
            writer.Write(list.Count);
            foreach (var s in list) writer.Write(s ?? string.Empty);
        }

        private static List<string> ReadList(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            var list = new List<string>(count);
            for (int i = 0; i < count; i++) list.Add(reader.ReadString());
            return list;
        }
    }
}
