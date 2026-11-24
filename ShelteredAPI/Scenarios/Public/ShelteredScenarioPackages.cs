using System;
using System.IO;
using System.Threading;
using ModAPI.Core;

namespace ShelteredAPI.Scenarios.Public
{
    /// <summary>Operations reported immediately before a package replacement performs them.</summary>
    public enum ScenarioPackageStep { Write, Validate, MoveOld, MoveNew, Cleanup }

    /// <summary>Serializes package access, replaces validated directories, and recovers interrupted replacements.</summary>
    public static class ShelteredScenarioPackages
    {
        private const string StageSuffix = ".smm-package-stage";
        private const string BackupSuffix = ".smm-package-backup";
        private const string TransactionSuffix = ".smm-package-txn";
        private static readonly Mutex Gate = new Mutex(false, "ShelteredModManager.ScenarioPackages");

        /// <summary>Runs a synchronous operation without concurrent package replacements or catalog scans.</summary>
        public static T RunLocked<T>(Func<T> action)
        {
            try { Gate.WaitOne(); }
            catch (AbandonedMutexException) { /* The previous writer exited. Recovery inspects the directories. */ }
            try { return action(); }
            finally { Gate.ReleaseMutex(); }
        }

        private static string StagePath(string destination) { return WorkPath(destination, StageSuffix); }
        private static string BackupPath(string destination) { return WorkPath(destination, BackupSuffix); }
        private static string TransactionPath(string destination) { return WorkPath(destination, TransactionSuffix); }

        /// <summary>
        /// Writes and validates a sibling staging directory, then replaces the destination.
        /// A validation failure must throw. An optional observer receives the step and its affected path.
        /// After the destination rename commits, failed backup cleanup is retried during recovery.
        /// </summary>
        public static void Replace(string destination, Action<string> write, Action<string> validate,
            Action<ScenarioPackageStep, string> beforeStep = null)
        {
            RunLocked(delegate
            {
                RecoverPackage(destination);
                string stage = StagePath(destination);
                string backup = BackupPath(destination);
                string transaction = TransactionPath(destination);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                if (Directory.Exists(stage) || Directory.Exists(backup) || File.Exists(stage) || File.Exists(backup))
                    throw new IOException("A scenario package work directory is already in use.");
                // The marker identifies directories owned by this transaction, even
                // after a process exits. Manually installed folders may share the suffix.
                using (new FileStream(transaction, FileMode.CreateNew)) { }
                try
                {
                    Directory.CreateDirectory(stage);
                    if (beforeStep != null) beforeStep(ScenarioPackageStep.Write, stage);
                    write(stage);
                    if (beforeStep != null) beforeStep(ScenarioPackageStep.Validate, stage);
                    validate(stage);
                    if (Directory.Exists(destination))
                    {
                        if (beforeStep != null) beforeStep(ScenarioPackageStep.MoveOld, destination);
                        Directory.Move(destination, backup);
                    }
                    if (beforeStep != null) beforeStep(ScenarioPackageStep.MoveNew, stage);
                    Directory.Move(stage, destination);
                }
                catch
                {
                    TryRecover(destination);
                    throw;
                }

                try
                {
                    if (beforeStep != null) beforeStep(ScenarioPackageStep.Cleanup, backup);
                    DeleteWorkDirectory(backup);
                    File.Delete(transaction);
                }
                catch (IOException ex) { LogRecovery(ex); }
                catch (UnauthorizedAccessException ex) { LogRecovery(ex); }
                return true;
            });
        }

        /// <summary>Recovers one destination. Use the package lock to keep recovery and subsequent access together.</summary>
        public static void Recover(string destination)
        {
            RunLocked(delegate { RecoverPackage(destination); return true; });
        }

        private static void RecoverPackage(string destination)
        {
            string backup = BackupPath(destination);
            string stage = StagePath(destination);
            RequireOrdinaryDirectory(destination);
            string transaction = TransactionPath(destination);
            if (!File.Exists(transaction))
                return;
            RequireOrdinaryDirectory(backup);
            RequireOrdinaryDirectory(stage);
            if (Directory.Exists(backup) && !Directory.Exists(destination))
                Directory.Move(backup, destination);
            DeleteWorkDirectory(stage);
            DeleteWorkDirectory(backup);
            File.Delete(transaction);
        }

        /// <summary>Attempts recovery of marked package transactions directly under the given root.</summary>
        public static void RecoverRoot(string root)
        {
            RunLocked(delegate
            {
                try
                {
                    if (!Directory.Exists(root)) return false;
                    foreach (string path in Directory.GetFiles(root, "*" + TransactionSuffix))
                    {
                        string name = DestinationName(Path.GetFileName(path), TransactionSuffix);
                        if (name != null) TryRecover(Path.Combine(root, name));
                    }
                }
                catch (IOException ex) { LogRecovery(ex); }
                catch (UnauthorizedAccessException ex) { LogRecovery(ex); }
                return true;
            });
        }

        /// <summary>Identifies a directory owned by a pending package replacement.</summary>
        public static bool IsWorkDirectory(string path)
        {
            string name = Path.GetFileName(path);
            string destination = DestinationName(name, StageSuffix) ?? DestinationName(name, BackupSuffix);
            return destination != null && File.Exists(TransactionPath(Path.Combine(Path.GetDirectoryName(path), destination)));
        }

        private static string DestinationName(string name, string suffix)
        {
            if (!name.StartsWith(".", StringComparison.Ordinal) || !name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                || name.Length <= suffix.Length + 1) return null;
            string destination = name.Substring(1, name.Length - suffix.Length - 1);
            return destination[0] == '.' || destination[destination.Length - 1] == '.'
                || char.IsWhiteSpace(destination[destination.Length - 1]) ? null : destination;
        }

        private static string WorkPath(string destination, string suffix)
        {
            return Path.Combine(Path.GetDirectoryName(destination), "." + Path.GetFileName(destination) + suffix);
        }

        private static void TryRecover(string destination)
        {
            try { RecoverPackage(destination); }
            catch (IOException ex) { LogRecovery(ex); }
            catch (UnauthorizedAccessException ex) { LogRecovery(ex); }
        }

        private static void RequireOrdinaryDirectory(string path)
        {
            if (File.Exists(path) || (Directory.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0))
                throw new IOException("Scenario package transaction expected an ordinary directory: " + path);
        }

        private static void DeleteWorkDirectory(string path)
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }

        private static void LogRecovery(Exception ex)
        {
            MMLog.WriteWarning("[Scenario package recovery] " + ex.Message);
        }
    }
}
