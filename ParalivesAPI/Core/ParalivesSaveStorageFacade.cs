using System;
using System.IO;
using System.Reflection;
using ModAPI.Core;

namespace ParalivesAPI.Core
{
    public interface IParalivesSaveStorage
    {
        bool HasActiveSave { get; }

        string CurrentSaveKey { get; }

        string GetSaveRootPath();

        string GetModDirectory(string modId);

        string GetStatePath(string modId, string name);

        bool Exists(string modId, string name);

        bool TryReadJson(string modId, string name, out string json, out string error);

        bool TryWriteJson(string modId, string name, string json, out string error);

        bool TryLoadJson<T>(string modId, string name, T data, out string error) where T : class;

        bool TrySaveJson<T>(string modId, string name, T data, out string error) where T : class;
    }

    public sealed class ParalivesSaveStorageFacade : IParalivesSaveStorage, ISaveRuntimeAdapter
    {
        public const string RegistryId = "GameRuntime.Paralives.SaveStorage";

        public static readonly ParalivesSaveStorageFacade Current =
            new ParalivesSaveStorageFacade(ParalivesGameLifecycleFacade.Current);

        private readonly ParalivesGameLifecycleFacade _lifecycle;

        private ParalivesSaveStorageFacade(ParalivesGameLifecycleFacade lifecycle)
        {
            _lifecycle = lifecycle;
        }

        public bool HasActiveSave
        {
            get { return !string.IsNullOrEmpty(CurrentSaveKey); }
        }

        public string CurrentSaveKey
        {
            get { return _lifecycle.GetStorageSaveKey(); }
        }

        public string GetSaveRootPath()
        {
            string saveKey = CurrentSaveKey;
            if (string.IsNullOrEmpty(saveKey))
                return null;

            return Path.Combine(Path.Combine(Path.Combine(ModApiPaths.SmmRoot, "SaveState"), "paralives"), saveKey);
        }

        public string GetModDirectory(string modId)
        {
            string saveRoot = GetSaveRootPath();
            if (string.IsNullOrEmpty(saveRoot))
                return null;

            string safeModId = SanitizeSegment(modId);
            if (string.IsNullOrEmpty(safeModId))
                return null;

            return Path.Combine(saveRoot, safeModId);
        }

        public string GetStatePath(string modId, string name)
        {
            string modDirectory = GetModDirectory(modId);
            if (string.IsNullOrEmpty(modDirectory))
                return null;

            string safeName = SanitizeSegment(name);
            if (string.IsNullOrEmpty(safeName))
                return null;

            if (!safeName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                safeName += ".json";

            return Path.Combine(modDirectory, safeName);
        }

        public bool Exists(string modId, string name)
        {
            string path = GetStatePath(modId, name);
            return !string.IsNullOrEmpty(path) && File.Exists(path);
        }

        public bool TryReadJson(string modId, string name, out string json, out string error)
        {
            json = null;
            error = null;

            string path = GetStatePath(modId, name);
            if (string.IsNullOrEmpty(path))
            {
                error = "No active Paralives save or invalid storage name.";
                return false;
            }

            if (!File.Exists(path))
            {
                error = "Save-scoped state file was not found: " + path;
                return false;
            }

            try
            {
                json = File.ReadAllText(path);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public bool TryWriteJson(string modId, string name, string json, out string error)
        {
            error = null;

            string path = GetStatePath(modId, name);
            if (string.IsNullOrEmpty(path))
            {
                error = "No active Paralives save or invalid storage name.";
                return false;
            }

            string directory = Path.GetDirectoryName(path);
            string tempPath = path + ".tmp";
            string backupPath = path + ".bak";

            try
            {
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(tempPath, json ?? "{}");
                ReplaceWithBackup(tempPath, path, backupPath);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                TryDelete(tempPath);
                return false;
            }
        }

        public bool TryLoadJson<T>(string modId, string name, T data, out string error) where T : class
        {
            error = null;
            if (data == null)
            {
                error = "A target data object is required.";
                return false;
            }

            string json;
            if (!TryReadJson(modId, name, out json, out error))
                return false;

            try
            {
                return TryFromJsonOverwrite(json, data, out error);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public bool TrySaveJson<T>(string modId, string name, T data, out string error) where T : class
        {
            error = null;
            if (data == null)
            {
                error = "A source data object is required.";
                return false;
            }

            string json;
            if (!TryToJson(data, out json, out error))
                return false;

            return TryWriteJson(modId, name, json, out error);
        }

        string ISaveRuntimeAdapter.GetCurrentSlotPath()
        {
            return GetSaveRootPath();
        }

        int ISaveRuntimeAdapter.ActiveSlotIndex
        {
            get { return -1; }
        }

        IModSaveContext ISaveRuntimeAdapter.GetCurrentSaveContext()
        {
            string root = GetSaveRootPath();
            string saveKey = CurrentSaveKey;
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(saveKey))
                return null;

            return new ModSaveContext(root, -1, "paralives", saveKey, null);
        }

        void ISaveRuntimeAdapter.EnsureRuntimeReady()
        {
        }

        void ISaveRuntimeAdapter.ResetRuntimeState()
        {
        }

        string ISaveRuntimeAdapter.GetQuitHeartbeatDetail()
        {
            string saveKey = CurrentSaveKey;
            return string.IsNullOrEmpty(saveKey)
                ? "Paralives save runtime has no active save."
                : "Paralives save runtime active save key: " + saveKey;
        }

        private static void ReplaceWithBackup(string tempPath, string path, string backupPath)
        {
            if (File.Exists(path))
            {
                try
                {
                    File.Replace(tempPath, path, backupPath, true);
                    return;
                }
                catch
                {
                    File.Copy(path, backupPath, true);
                    File.Copy(tempPath, path, true);
                    TryDelete(tempPath);
                    return;
                }
            }

            File.Move(tempPath, path);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }

        private static string SanitizeSegment(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            char[] invalid = Path.GetInvalidFileNameChars();
            char[] chars = value.Trim().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (IsInvalid(chars[i], invalid))
                    chars[i] = '_';
            }

            string sanitized = new string(chars).Trim('.', ' ');
            return sanitized.Length == 0 ? string.Empty : sanitized;
        }

        private static bool IsInvalid(char value, char[] invalid)
        {
            if (value == Path.DirectorySeparatorChar || value == Path.AltDirectorySeparatorChar)
                return true;

            for (int i = 0; i < invalid.Length; i++)
            {
                if (value == invalid[i])
                    return true;
            }

            return false;
        }

        private static bool TryToJson(object data, out string json, out string error)
        {
            json = null;
            error = null;

            MethodInfo method = GetJsonUtilityMethod("ToJson", new Type[] { typeof(object), typeof(bool) }, out error);
            if (method == null)
                return false;

            try
            {
                json = method.Invoke(null, new object[] { data, true }) as string;
                if (json == null)
                    json = "{}";
                return true;
            }
            catch (Exception ex)
            {
                error = UnwrapReflectionError(ex);
                return false;
            }
        }

        private static bool TryFromJsonOverwrite(string json, object data, out string error)
        {
            error = null;

            MethodInfo method = GetJsonUtilityMethod("FromJsonOverwrite", new Type[] { typeof(string), typeof(object) }, out error);
            if (method == null)
                return false;

            try
            {
                method.Invoke(null, new object[] { json, data });
                return true;
            }
            catch (Exception ex)
            {
                error = UnwrapReflectionError(ex);
                return false;
            }
        }

        private static MethodInfo GetJsonUtilityMethod(string name, Type[] arguments, out string error)
        {
            error = null;
            Type jsonUtility = ResolveJsonUtilityType();
            if (jsonUtility == null)
            {
                error = "UnityEngine.JsonUtility is unavailable.";
                return null;
            }

            MethodInfo method = jsonUtility.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, arguments, null);
            if (method == null)
                error = "UnityEngine.JsonUtility." + name + " is unavailable.";
            return method;
        }

        private static Type ResolveJsonUtilityType()
        {
            Type type = Type.GetType("UnityEngine.JsonUtility, UnityEngine.JSONSerializeModule", false);
            if (type != null)
                return type;

            type = Type.GetType("UnityEngine.JsonUtility, UnityEngine", false);
            if (type != null)
                return type;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                try
                {
                    type = assemblies[i].GetType("UnityEngine.JsonUtility", false);
                    if (type != null)
                        return type;
                }
                catch
                {
                }
            }

            return null;
        }

        private static string UnwrapReflectionError(Exception ex)
        {
            TargetInvocationException invocation = ex as TargetInvocationException;
            if (invocation != null && invocation.InnerException != null)
                return invocation.InnerException.Message;
            return ex.Message;
        }
    }
}
