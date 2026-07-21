[CmdletBinding()]
param(
    [string]$RepoRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
} else {
    $RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path
}

$repositoryPath = Join-Path $RepoRoot "ShelteredAPI\Saves\SaveBackups\SaveBackupRepository.cs"
$repositorySource = Get-Content -LiteralPath $repositoryPath -Raw
$durableWriterPath = Join-Path $RepoRoot "ShelteredAPI\Saves\SaveBackups\DurableFileWriter.cs"
$durableWriterSource = Get-Content -LiteralPath $durableWriterPath -Raw

function Assert-SourcePattern {
    param(
        [string]$Name,
        [string]$Pattern
    )

    if (-not [regex]::IsMatch(
        $repositorySource,
        $Pattern,
        [Text.RegularExpressions.RegexOptions]::Singleline)) {
        throw "${Name}: expected production-source contract was not found."
    }
}

Assert-SourcePattern `
    "restore and recovery share a repository-wide lock" `
    "RestoreRepositoryLockFileName.*AcquireRestoreRepositoryLock\(\).*RecoverIncompleteRestoreTransactionsUnderLock"

Assert-SourcePattern `
    "rollback files are adjacent to destinations" `
    "BuildRestoreTemporaryPath\(file\.DestinationPath,\s*transactionId,\s*""rollback""\)"

Assert-SourcePattern `
    "existing files use atomic replacement" `
    "File\.Replace\(replacementPath,\s*destinationPath,\s*rollbackPath,\s*true\)"

Assert-SourcePattern `
    "journal is published before live mutations" `
    "WriteRestoreTransactionJournal\(journalPath,\s*transactionId,\s*plan,\s*mutations\).*CommitRestoreMutation"

Assert-SourcePattern `
    "durable writes use the shared runtime-compatible writer" `
    "DurableFileWriter\.WriteNew"

if ($durableWriterSource -match "FileOptions\.WriteThrough" -or
    $durableWriterSource -notmatch "new FileStream\(\s*path,\s*FileMode\.CreateNew,\s*FileAccess\.Write,\s*FileShare\.None\)" -or
    $durableWriterSource -notmatch "FlushFileBuffers\(handle\)") {
    throw "durable writer compatibility: expected the Unity 5.3-compatible FileStream constructor and explicit disk flush."
}

Assert-SourcePattern `
    "snapshot manifests use durable atomic publication" `
    "PublishDurableFile\(manifestPath,\s*manifestBytes,\s*false\).*Published snapshot manifest identity validation failed"

Assert-SourcePattern `
    "new blobs are validated before manifest publication" `
    "New backup blob failed codec round-trip validation.*PublishDurableFile\(blobPath,\s*compressedBytes,\s*true\).*Published backup blob failed content validation"

Assert-SourcePattern `
    "committed payloads are validated before cleanup" `
    "ValidateCommittedRestoreState\(mutations\).*CleanupRestoreTemporaryFiles\(mutations\)"

Assert-SourcePattern `
    "unresolved recovery fails restore closed" `
    "if\s*\(!RecoverIncompleteRestoreTransactionsUnderLock\(out recoveryError\)\).*throw new IOException"

Assert-SourcePattern `
    "journal mutations are exactly manifest-bound" `
    "BuildRestorePlan\(manifestPath,\s*restoreDestination\).*AddInterruptedRestoreDeletions.*BuildExpectedRestoreMutations.*does not exactly match the validated restore plan"

Assert-SourcePattern `
    "custom restores accept a validated physical destination" `
    "RestoreSnapshot\(\s*string manifestPath,\s*SaveBackupRestoreDestination destination,\s*out string error\).*ResolveCustomRestoreDestination"

$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("smm-restore-crash-safety-" + [guid]::NewGuid().ToString("N"))
$projectRoot = Join-Path $tempRoot "RestoreCrashSafety"
New-Item -ItemType Directory -Path $projectRoot -Force | Out-Null

try {
    $sources = @(
        "ModAPI\Util\ManualJson.cs",
        "ShelteredAPI\Saves\CRC32.cs",
        "ShelteredAPI\Saves\SaveBackups\SaveBackupBlobCodec.cs",
        "ShelteredAPI\Saves\SaveBackups\DurableFileWriter.cs",
        "ShelteredAPI\Saves\SaveBackups\SaveBackupModels.cs",
        "ShelteredAPI\Saves\SaveBackups\SaveBackupRepository.cs"
    )
    foreach ($relativePath in $sources) {
        Copy-Item `
            -LiteralPath (Join-Path $RepoRoot $relativePath) `
            -Destination (Join-Path $projectRoot ([IO.Path]::GetFileName($relativePath)))
    }

    @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>
</Project>
'@ | Set-Content -LiteralPath (Join-Path $projectRoot "RestoreCrashSafety.csproj") -Encoding UTF8

    @'
public sealed class SaveManager
{
    public enum SaveType { Invalid = 0, Slot1 = 1, Slot2 = 2, Slot3 = 3 }
}

public sealed class SaveInfo
{
    public string familyName;
    public string saveTime;
}

public sealed class SaveEntry
{
    public string id;
    public int absoluteSlot;
    public string name;
    public string createdAt;
    public string updatedAt;
    public long fileSize;
    public uint crc32;
    public string scenarioId;
    public SaveInfo saveInfo;
}

public sealed class SlotManifest { }

namespace ModAPI.Core
{
    public static class MMLog
    {
        public static void WriteDebug(string message) { }
        public static void WriteInfo(string message) { }
        public static void WriteWarning(string message) { }
        public static void WriteError(string message) { }
    }
}

namespace ShelteredAPI.Saves
{
    internal static class SaveRegistryCore
    {
        internal static SaveInfo ReadVanillaSaveInfoFromEncryptedBytes(byte[] bytes) { return null; }
        internal static SaveInfo ReadSaveInfoFromXml(byte[] bytes) { return null; }
        internal static SlotManifest DeserializeSlotManifest(string json) { return null; }
        internal static string GetVanillaSavePath(int slot) { return string.Empty; }
    }
}

namespace ShelteredAPI.Saves.Runtime
{
    internal static class SaveStorageRouter
    {
        internal static string NormalizeScenarioId(string scenarioId) { return scenarioId ?? string.Empty; }
    }

    internal static class DirectoryProvider
    {
        internal static string TestSlotRoot;
        internal static string TestScenarioRoot;

        internal static string ScenarioRoot(string scenarioId, bool create)
        {
            if (create && !System.IO.Directory.Exists(TestScenarioRoot))
                System.IO.Directory.CreateDirectory(TestScenarioRoot);
            return TestScenarioRoot;
        }

        internal static string SlotRoot(string scenarioId, int absoluteSlot, bool create)
        {
            string result = !string.IsNullOrEmpty(TestSlotRoot)
                ? TestSlotRoot
                : System.IO.Path.Combine(TestScenarioRoot, "Slot_" + absoluteSlot);
            if (create && !System.IO.Directory.Exists(result))
                System.IO.Directory.CreateDirectory(result);
            return result;
        }
    }

    internal sealed class VanillaSaveRoute
    {
        internal int VanillaSlotNumber;
        internal int AbsoluteSlot;
        internal string StorageScenarioId;
    }

    internal static class VanillaSaveRouting
    {
        internal static bool TryGetRoute(SaveManager.SaveType saveType, out VanillaSaveRoute route)
        {
            route = null;
            return false;
        }
    }
}
'@ | Set-Content -LiteralPath (Join-Path $projectRoot "RuntimeStubs.cs") -Encoding UTF8

    @'
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using ModAPI.Util;
using ShelteredAPI.Saves.Backups;

internal static class Program
{
    private static void Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "--fault-child", StringComparison.Ordinal))
        {
            RunFaultBoundaryChild(args);
            return;
        }

        string root = Path.Combine(Path.GetTempPath(), "smm-restore-runtime-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            VerifyNormalRestore(root);
            VerifyMovedCustomRestoreRoutesToCurrentSlot(root);
            VerifyBlobPublicationFailurePublishesNoManifest(root);
            VerifyAbruptChildExitAfterCommit(root);
            VerifyRepositoryLockSerializesInstances(root);
            VerifyInterruptedReplacementRollsBack(root);
            VerifyInterruptedDeletionRollsBack(root);
            VerifyInterruptedCreationRollsBack(root);
            VerifyPartialCommitMarkerRollsBack(root);
            VerifyCommittedTransactionKeepsNewState(root);
            VerifyCommittedStateMismatchFailsClosed(root);
            VerifyCorruptJournalFailsClosed(root);
            VerifyPathAttackFailsClosed(root);
            VerifyReparseTraversalFailsClosedWhenSupported(root);
            VerifyDuplicateDestinationFailsClosed(root);
            VerifyMissingMutationFailsClosed(root);
            VerifySchemaAndIdValidation(root);
            Console.WriteLine("Save restore crash-safety and fail-closed tests passed.");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    private static void RunFaultBoundaryChild(string[] args)
    {
        if (args.Length != 5)
            Environment.Exit(91);

        string storeRoot = args[1];
        string slotRoot = args[2];
        string manifest = args[3];
        string destination = args[4];
        ShelteredAPI.Saves.Runtime.DirectoryProvider.TestSlotRoot = slotRoot;
        File.WriteAllText(destination, "current-before-fault");

        SaveBackupRepository repository = new SaveBackupRepository(storeRoot);
        string error;
        if (!repository.RestoreSnapshot(manifest, out error))
            Environment.Exit(92);

        Environment.FailFast("Intentional restore-boundary interruption.");
    }

    private static void VerifyNormalRestore(string root)
    {
        Fixture fixture = CreateFixture(root, "normal");
        File.WriteAllText(fixture.Destination, "current");
        string error;
        Assert(fixture.Repository.RestoreSnapshot(fixture.Manifest, out error), "Normal restore failed: " + error);
        Assert(File.ReadAllText(fixture.Destination) == "snapshot", "Normal restore did not install snapshot bytes.");
        AssertCommittedTransactionIsPending(fixture.StoreRoot);
        new SaveBackupRepository(fixture.StoreRoot);
        AssertTransactionStoreIsClean(fixture.StoreRoot);
    }

    private static void VerifyMovedCustomRestoreRoutesToCurrentSlot(string root)
    {
        string caseRoot = Path.Combine(root, "moved-custom-lineage");
        string storeRoot = Path.Combine(caseRoot, "store");
        string scenarioRoot = Path.Combine(caseRoot, "scenario");
        string slot7 = Path.Combine(scenarioRoot, "Slot_7");
        string slot4 = Path.Combine(scenarioRoot, "Slot_4");
        string lineageId = Guid.NewGuid().ToString("N");
        string otherLineageId = Guid.NewGuid().ToString("N");
        string snapshotIdentity = CreateIdentityJson(lineageId);
        byte[] snapshotBytes = Encoding.UTF8.GetBytes("slot7-snapshot");

        Directory.CreateDirectory(slot7);
        File.WriteAllBytes(Path.Combine(slot7, "SaveData.xml"), snapshotBytes);
        File.WriteAllText(Path.Combine(slot7, "backup.identity.json"), snapshotIdentity);
        File.WriteAllBytes(Path.Combine(slot7, "snapshot-sidecar.bin"), new byte[] { 0, 1, 2, 3, 255 });

        ShelteredAPI.Saves.Runtime.DirectoryProvider.TestScenarioRoot = scenarioRoot;
        ShelteredAPI.Saves.Runtime.DirectoryProvider.TestSlotRoot = null;
        SaveBackupRepository repository = new SaveBackupRepository(storeRoot);
        SaveBackupTarget target = new SaveBackupTarget
        {
            TimelineKey = "custom:" + lineageId,
            SaveKind = "CustomSlot",
            ScenarioId = "MovedScenario",
            AbsoluteSlot = 7,
            SaveId = "moved-custom-save"
        };
        target.Sources.Add(new SaveBackupSource
        {
            Id = "slot",
            Path = slot7,
            Kind = SaveBackupSourceKind.Directory
        });

        string snapshotId = repository.CreateSnapshot(
            target,
            SaveBackupReason.Manual,
            new SaveBackupRetentionPolicy { Mode = SaveBackupRetentionMode.Forever });
        Assert(!string.IsNullOrEmpty(snapshotId), "Moved-lineage fixture did not create a snapshot.");
        string[] manifests = Directory.GetFiles(
            Path.Combine(storeRoot, "timelines"),
            snapshotId + ".json",
            SearchOption.AllDirectories);
        Assert(manifests.Length == 1, "Moved-lineage snapshot manifest was not uniquely found.");

        Directory.Move(slot7, slot4);
        File.WriteAllText(Path.Combine(slot4, "SaveData.xml"), "slot4-current");

        Directory.CreateDirectory(slot7);
        byte[] occupiedSaveBytes = Encoding.UTF8.GetBytes("different-save-in-slot7");
        byte[] occupiedIdentityBytes = Encoding.UTF8.GetBytes(CreateIdentityJson(otherLineageId));
        byte[] occupiedSidecarBytes = new byte[] { 255, 17, 4, 0, 99 };
        File.WriteAllBytes(Path.Combine(slot7, "SaveData.xml"), occupiedSaveBytes);
        File.WriteAllBytes(Path.Combine(slot7, "backup.identity.json"), occupiedIdentityBytes);
        File.WriteAllBytes(Path.Combine(slot7, "occupied-sidecar.bin"), occupiedSidecarBytes);

        SaveBackupRestoreDestination destination = new SaveBackupRestoreDestination
        {
            ScenarioId = "MovedScenario",
            AbsoluteSlot = 4,
            ExpectedLineageId = lineageId,
            AllowHistoricalSlotWhenUnoccupied = false
        };
        string error;
        Assert(
            repository.RestoreSnapshot(manifests[0], destination, out error),
            "Moved-lineage restore failed: " + error);
        Assert(
            ComputeSha256(File.ReadAllBytes(Path.Combine(slot4, "SaveData.xml")))
                == ComputeSha256(snapshotBytes),
            "Moved-lineage restore did not target the current physical slot.");
        Assert(
            File.ReadAllText(Path.Combine(slot4, "backup.identity.json")) == snapshotIdentity,
            "Moved-lineage restore changed the snapshot lineage identity.");
        Assert(
            Directory.GetFiles(slot7, "*", SearchOption.AllDirectories).Length == 3
                && ComputeSha256(File.ReadAllBytes(Path.Combine(slot7, "SaveData.xml")))
                    == ComputeSha256(occupiedSaveBytes)
                && ComputeSha256(File.ReadAllBytes(Path.Combine(slot7, "backup.identity.json")))
                    == ComputeSha256(occupiedIdentityBytes)
                && ComputeSha256(File.ReadAllBytes(Path.Combine(slot7, "occupied-sidecar.bin")))
                    == ComputeSha256(occupiedSidecarBytes),
            "Moved-lineage restore modified the newly occupied historical slot.");

        AssertCommittedTransactionIsPending(storeRoot);
        new SaveBackupRepository(storeRoot);
        AssertTransactionStoreIsClean(storeRoot);
        Assert(
            Directory.GetFiles(slot7, "*", SearchOption.AllDirectories).Length == 3
                && ComputeSha256(File.ReadAllBytes(Path.Combine(slot7, "SaveData.xml")))
                    == ComputeSha256(occupiedSaveBytes)
                && ComputeSha256(File.ReadAllBytes(Path.Combine(slot7, "backup.identity.json")))
                    == ComputeSha256(occupiedIdentityBytes)
                && ComputeSha256(File.ReadAllBytes(Path.Combine(slot7, "occupied-sidecar.bin")))
                    == ComputeSha256(occupiedSidecarBytes),
            "Deferred moved-lineage recovery modified the occupied historical slot.");
    }

    private static void VerifyBlobPublicationFailurePublishesNoManifest(string root)
    {
        string caseRoot = Path.Combine(root, "blob-publication-failure");
        Fixture fixture = new Fixture();
        fixture.CaseRoot = caseRoot;
        fixture.StoreRoot = Path.Combine(caseRoot, "store");
        fixture.SlotRoot = Path.Combine(caseRoot, "slot");
        fixture.Destination = Path.Combine(fixture.SlotRoot, "save.dat");
        fixture.ScenarioId = "Survival";
        fixture.AbsoluteSlot = 1;
        fixture.LineageId = Guid.NewGuid().ToString("N");
        fixture.IdentityJson = CreateIdentityJson(fixture.LineageId);
        Directory.CreateDirectory(fixture.SlotRoot);
        ShelteredAPI.Saves.Runtime.DirectoryProvider.TestSlotRoot = fixture.SlotRoot;
        ShelteredAPI.Saves.Runtime.DirectoryProvider.TestScenarioRoot = fixture.CaseRoot;
        File.WriteAllText(fixture.Destination, "blocked-publication");
        File.WriteAllText(Path.Combine(fixture.SlotRoot, "backup.identity.json"), fixture.IdentityJson);
        fixture.Repository = new SaveBackupRepository(fixture.StoreRoot);

        string hash = ComputeSha256(Encoding.UTF8.GetBytes("blocked-publication"));
        string blockedBlobPath = Path.Combine(
            Path.Combine(Path.Combine(fixture.StoreRoot, "blobs"), hash.Substring(0, 2)),
            hash + ".bin.slz");
        Directory.CreateDirectory(blockedBlobPath);

        string snapshotId = fixture.Repository.CreateSnapshot(
            BuildTarget(fixture),
            SaveBackupReason.Manual,
            new SaveBackupRetentionPolicy { Mode = SaveBackupRetentionMode.Forever });
        Assert(string.IsNullOrEmpty(snapshotId), "Snapshot creation did not fail closed on blob publication failure.");

        string timelinesRoot = Path.Combine(fixture.StoreRoot, "timelines");
        Assert(
            !Directory.Exists(timelinesRoot)
                || Directory.GetFiles(timelinesRoot, "*.json", SearchOption.AllDirectories).Length == 0,
            "A manifest was published after its blob publication failed.");
    }

    private static void VerifyAbruptChildExitAfterCommit(string root)
    {
        Fixture fixture = CreateFixture(root, "fault-child");
        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = Environment.ProcessPath;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.ArgumentList.Add("--fault-child");
        startInfo.ArgumentList.Add(fixture.StoreRoot);
        startInfo.ArgumentList.Add(fixture.SlotRoot);
        startInfo.ArgumentList.Add(fixture.Manifest);
        startInfo.ArgumentList.Add(fixture.Destination);

        using (Process child = Process.Start(startInfo))
        {
            Assert(child != null, "Fault-boundary child process did not start.");
            Assert(child.WaitForExit(15000), "Fault-boundary child process did not terminate.");
            Assert(child.ExitCode != 0, "Fault-boundary child did not terminate abruptly.");
        }

        Assert(File.ReadAllText(fixture.Destination) == "snapshot", "Fault-boundary child did not commit snapshot bytes.");
        AssertCommittedTransactionIsPending(fixture.StoreRoot);
        ShelteredAPI.Saves.Runtime.DirectoryProvider.TestSlotRoot = fixture.SlotRoot;
        new SaveBackupRepository(fixture.StoreRoot);
        Assert(File.ReadAllText(fixture.Destination) == "snapshot", "Recovery changed a validated committed destination.");
        AssertTransactionStoreIsClean(fixture.StoreRoot);
    }

    private static void VerifyRepositoryLockSerializesInstances(string root)
    {
        Fixture fixture = CreateFixture(root, "repository-lock");
        string lockPath = Path.Combine(fixture.StoreRoot, "restore-transactions", ".repository.lock");
        Exception workerError = null;
        ManualResetEvent started = new ManualResetEvent(false);
        ManualResetEvent completed = new ManualResetEvent(false);

        using (FileStream heldLock = new FileStream(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Thread worker = new Thread(delegate()
            {
                try
                {
                    started.Set();
                    string snapshotId = fixture.Repository.CreateSnapshot(
                        BuildTarget(fixture),
                        SaveBackupReason.Manual,
                        new SaveBackupRetentionPolicy { Mode = SaveBackupRetentionMode.Forever });
                    if (string.IsNullOrEmpty(snapshotId))
                        throw new InvalidOperationException("Locked snapshot mutation failed.");
                }
                catch (Exception ex)
                {
                    workerError = ex;
                }
                finally
                {
                    completed.Set();
                }
            });
            worker.IsBackground = true;
            worker.Start();
            Assert(started.WaitOne(2000), "Concurrent repository worker did not start.");
            Assert(!completed.WaitOne(200), "Concurrent repository bypassed the repository-wide lock.");
        }

        Assert(completed.WaitOne(5000), "Concurrent repository did not proceed after lock release.");
        Assert(workerError == null, "Concurrent repository failed after lock release: " + workerError);
    }

    private static void VerifyInterruptedReplacementRollsBack(string root)
    {
        Fixture fixture = CreateFixture(root, "replacement");
        string transactionId = NewTransactionId();
        File.WriteAllText(fixture.Destination, "snapshot");
        File.WriteAllText(Artifact(fixture.Destination, transactionId, "rollback"), "old");
        File.WriteAllText(Artifact(fixture.Destination, transactionId, "discard"), "stale-new");
        WriteJournal(fixture, transactionId, fixture.Destination, true, true, "snapshot", 1, transactionId, false, null);

        new SaveBackupRepository(fixture.StoreRoot);

        Assert(File.ReadAllText(fixture.Destination) == "old", "Interrupted replacement did not restore old bytes.");
        AssertTransactionStoreIsClean(fixture.StoreRoot);
        AssertNoArtifacts(fixture.Destination, transactionId);
    }

    private static void VerifyInterruptedDeletionRollsBack(string root)
    {
        Fixture fixture = CreateFixture(root, "deletion");
        string destination = Path.Combine(fixture.SlotRoot, "extra.dat");
        File.WriteAllText(destination, "old");
        string transactionId = NewTransactionId();
        File.Delete(destination);
        File.WriteAllText(Artifact(destination, transactionId, "rollback"), "old");
        WriteJournal(fixture, transactionId, destination, true, false, null, 1, transactionId, false, null, true);

        new SaveBackupRepository(fixture.StoreRoot);

        Assert(File.ReadAllText(destination) == "old", "Interrupted deletion did not restore the deleted file.");
        AssertTransactionStoreIsClean(fixture.StoreRoot);
        AssertNoArtifacts(destination, transactionId);
    }

    private static void VerifyInterruptedCreationRollsBack(string root)
    {
        Fixture fixture = CreateFixture(root, "creation");
        string destination = fixture.Destination;
        File.Delete(destination);
        string transactionId = NewTransactionId();
        File.WriteAllText(destination, "snapshot");
        File.WriteAllText(Artifact(destination, transactionId, "absent"), string.Empty);
        WriteJournal(fixture, transactionId, destination, false, true, "snapshot", 1, transactionId, false, null);

        new SaveBackupRepository(fixture.StoreRoot);

        Assert(!File.Exists(destination), "Interrupted creation left a file that did not exist before restore.");
        AssertTransactionStoreIsClean(fixture.StoreRoot);
        AssertNoArtifacts(destination, transactionId);
    }

    private static void VerifyPartialCommitMarkerRollsBack(string root)
    {
        Fixture fixture = CreateFixture(root, "partial-marker");
        string transactionId = NewTransactionId();
        File.WriteAllText(fixture.Destination, "snapshot");
        File.WriteAllText(Artifact(fixture.Destination, transactionId, "rollback"), "old");
        WriteJournal(fixture, transactionId, fixture.Destination, true, true, "snapshot", 1, transactionId, false, null);
        File.WriteAllText(TransactionPath(fixture.StoreRoot, transactionId, ".committed"), string.Empty);

        new SaveBackupRepository(fixture.StoreRoot);

        Assert(File.ReadAllText(fixture.Destination) == "old", "An incomplete commit marker incorrectly kept new bytes.");
        AssertTransactionStoreIsClean(fixture.StoreRoot);
        AssertNoArtifacts(fixture.Destination, transactionId);
    }

    private static void VerifyCommittedTransactionKeepsNewState(string root)
    {
        Fixture fixture = CreateFixture(root, "committed");
        string transactionId = NewTransactionId();
        File.WriteAllText(fixture.Destination, "snapshot");
        File.WriteAllText(Artifact(fixture.Destination, transactionId, "rollback"), "old");
        WriteJournal(fixture, transactionId, fixture.Destination, true, true, "snapshot", 1, transactionId, false, null);
        File.WriteAllText(TransactionPath(fixture.StoreRoot, transactionId, ".committed"), transactionId);

        new SaveBackupRepository(fixture.StoreRoot);

        Assert(File.ReadAllText(fixture.Destination) == "snapshot", "Committed recovery replaced the new state.");
        AssertTransactionStoreIsClean(fixture.StoreRoot);
        AssertNoArtifacts(fixture.Destination, transactionId);
    }

    private static void VerifyCommittedStateMismatchFailsClosed(string root)
    {
        Fixture fixture = CreateFixture(root, "committed-mismatch");
        string transactionId = NewTransactionId();
        File.WriteAllText(fixture.Destination, "tampered");
        string rollback = Artifact(fixture.Destination, transactionId, "rollback");
        File.WriteAllText(rollback, "old");
        WriteJournal(fixture, transactionId, fixture.Destination, true, true, "snapshot", 1, transactionId, false, null);
        File.WriteAllText(TransactionPath(fixture.StoreRoot, transactionId, ".committed"), transactionId);

        AssertRestoreFailsClosed(fixture, "unexpected");
        Assert(File.Exists(rollback), "Committed-state mismatch discarded its rollback evidence.");
    }

    private static void VerifyCorruptJournalFailsClosed(string root)
    {
        Fixture fixture = CreateFixture(root, "corrupt-journal");
        string transactionId = NewTransactionId();
        File.WriteAllText(TransactionPath(fixture.StoreRoot, transactionId, ".json"), "{not-json");

        string snapshotId = fixture.Repository.CreateSnapshot(
            BuildTarget(fixture),
            SaveBackupReason.Manual,
            new SaveBackupRetentionPolicy { Mode = SaveBackupRetentionMode.Forever });
        Assert(string.IsNullOrEmpty(snapshotId), "CreateSnapshot mutated the store with unresolved recovery.");

        string deleteError;
        Assert(
            !fixture.Repository.DeleteSnapshot(fixture.Manifest, out deleteError),
            "DeleteSnapshot mutated the store with unresolved recovery.");
        Assert(File.Exists(fixture.Manifest), "DeleteSnapshot removed a manifest with unresolved recovery.");
        Assert(
            fixture.Repository.PruneSnapshotsAfter("crash-safety", DateTime.MinValue, string.Empty) == 0,
            "PruneSnapshotsAfter mutated the store with unresolved recovery.");
        Assert(File.Exists(fixture.Manifest), "PruneSnapshotsAfter removed a manifest with unresolved recovery.");

        AssertRestoreFailsClosed(fixture, "journal");
        Assert(File.Exists(TransactionPath(fixture.StoreRoot, transactionId, ".json")), "Corrupt journal was discarded.");
    }

    private static void VerifyPathAttackFailsClosed(string root)
    {
        Fixture fixture = CreateFixture(root, "path-attack");
        string outside = Path.Combine(fixture.CaseRoot, "outside.dat");
        File.WriteAllText(outside, "untouched");
        string transactionId = NewTransactionId();
        WriteJournal(fixture, transactionId, outside, false, true, "attack", 1, transactionId, false, null);

        AssertRestoreFailsClosed(fixture, "outside");
        Assert(File.ReadAllText(outside) == "untouched", "Path-attack journal mutated a file outside the save root.");
    }

    private static void VerifyReparseTraversalFailsClosedWhenSupported(string root)
    {
        Fixture fixture = CreateFixture(root, "reparse");
        string outsideRoot = Path.Combine(fixture.CaseRoot, "outside-root");
        Directory.CreateDirectory(outsideRoot);
        string linkPath = Path.Combine(fixture.SlotRoot, "linked");
        try
        {
            Directory.CreateSymbolicLink(linkPath, outsideRoot);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Reparse traversal runtime case skipped: " + ex.GetType().Name);
            return;
        }

        string destination = Path.Combine(linkPath, "attack.dat");
        string transactionId = NewTransactionId();
        WriteJournal(fixture, transactionId, destination, false, true, "attack", 1, transactionId, false, null);
        AssertRestoreFailsClosed(fixture, "reparse");
        Assert(!File.Exists(Path.Combine(outsideRoot, "attack.dat")), "Reparse journal mutated its external target.");
    }

    private static void VerifyDuplicateDestinationFailsClosed(string root)
    {
        Fixture fixture = CreateFixture(root, "duplicate-destination");
        string transactionId = NewTransactionId();
        WriteJournal(
            fixture,
            transactionId,
            fixture.Destination,
            true,
            true,
            "snapshot",
            1,
            transactionId,
            true,
            null);
        AssertRestoreFailsClosed(fixture, "duplicate");
    }

    private static void VerifyMissingMutationFailsClosed(string root)
    {
        Fixture fixture = CreateFixture(root, "missing-mutation");
        string extra = Path.Combine(fixture.SlotRoot, "extra.dat");
        File.WriteAllText(extra, "extra");
        string transactionId = NewTransactionId();
        WriteJournal(
            fixture,
            transactionId,
            fixture.Destination,
            true,
            true,
            "snapshot",
            1,
            transactionId,
            false,
            null);
        AssertRestoreFailsClosed(fixture, "do not match");
        Assert(File.ReadAllText(extra) == "extra", "Missing-mutation journal changed an unlisted destination.");
    }

    private static void VerifySchemaAndIdValidation(string root)
    {
        Fixture schemaFixture = CreateFixture(root, "schema");
        string schemaId = NewTransactionId();
        WriteJournal(
            schemaFixture,
            schemaId,
            schemaFixture.Destination,
            true,
            true,
            "snapshot",
            2,
            schemaId,
            false,
            null);
        AssertRestoreFailsClosed(schemaFixture, "schema");

        Fixture idFixture = CreateFixture(root, "id");
        string fileId = "not-a-hex-transaction-id";
        WriteJournal(
            idFixture,
            fileId,
            idFixture.Destination,
            true,
            true,
            "snapshot",
            1,
            fileId,
            false,
            null);
        AssertRestoreFailsClosed(idFixture, "hexadecimal");
    }

    private static Fixture CreateFixture(string root, string name)
    {
        Fixture fixture = new Fixture();
        fixture.CaseRoot = Path.Combine(root, name);
        fixture.StoreRoot = Path.Combine(fixture.CaseRoot, "store");
        fixture.SlotRoot = Path.Combine(fixture.CaseRoot, "slot");
        fixture.Destination = Path.Combine(fixture.SlotRoot, "save.dat");
        fixture.ScenarioId = "Survival";
        fixture.AbsoluteSlot = 1;
        fixture.LineageId = Guid.NewGuid().ToString("N");
        fixture.IdentityJson = CreateIdentityJson(fixture.LineageId);
        Directory.CreateDirectory(fixture.SlotRoot);
        ShelteredAPI.Saves.Runtime.DirectoryProvider.TestSlotRoot = fixture.SlotRoot;
        ShelteredAPI.Saves.Runtime.DirectoryProvider.TestScenarioRoot = fixture.CaseRoot;
        File.WriteAllText(fixture.Destination, "snapshot");
        File.WriteAllText(Path.Combine(fixture.SlotRoot, "backup.identity.json"), fixture.IdentityJson);

        fixture.Repository = new SaveBackupRepository(fixture.StoreRoot);
        string snapshotId = fixture.Repository.CreateSnapshot(
            BuildTarget(fixture),
            SaveBackupReason.Manual,
            new SaveBackupRetentionPolicy { Mode = SaveBackupRetentionMode.Forever });
        Assert(!string.IsNullOrEmpty(snapshotId), "Fixture did not create a snapshot.");
        string[] manifests = Directory.GetFiles(
            Path.Combine(fixture.StoreRoot, "timelines"),
            snapshotId + ".json",
            SearchOption.AllDirectories);
        Assert(manifests.Length == 1, "Fixture snapshot manifest was not uniquely found.");
        fixture.Manifest = manifests[0];
        return fixture;
    }

    private static SaveBackupTarget BuildTarget(Fixture fixture)
    {
        SaveBackupTarget target = new SaveBackupTarget
        {
            TimelineKey = "custom:" + fixture.LineageId,
            SaveKind = "CustomSlot",
            ScenarioId = fixture.ScenarioId,
            AbsoluteSlot = fixture.AbsoluteSlot,
            SaveId = "crash-safety-save"
        };
        target.Sources.Add(new SaveBackupSource
        {
            Id = "slot",
            Path = fixture.SlotRoot,
            Kind = SaveBackupSourceKind.Directory
        });
        return target;
    }

    private static void WriteJournal(
        Fixture fixture,
        string fileTransactionId,
        string destination,
        bool originalExisted,
        bool hasReplacement,
        string expectedContent,
        int schemaVersion,
        string journalTransactionId,
        bool duplicateDestination,
        string allowedRootOverride,
        bool includeSnapshotReplacement = false)
    {
        string transactionRoot = Path.Combine(fixture.StoreRoot, "restore-transactions");
        Directory.CreateDirectory(transactionRoot);

        ManualJsonArray mutations = new ManualJsonArray();
        mutations.Add(ManualJsonValue.Object(CreateMutation(
            destination,
            originalExisted,
            hasReplacement,
            expectedContent)));
        mutations.Add(ManualJsonValue.Object(CreateMutation(
            Path.Combine(fixture.SlotRoot, "backup.identity.json"),
            true,
            true,
            fixture.IdentityJson)));
        if (duplicateDestination)
        {
            mutations.Add(ManualJsonValue.Object(CreateMutation(
                destination,
                originalExisted,
                hasReplacement,
                expectedContent)));
        }
        if (includeSnapshotReplacement)
        {
            mutations.Add(ManualJsonValue.Object(CreateMutation(
                fixture.Destination,
                true,
                true,
                "snapshot")));
        }

        ManualJsonObject allowedRoot = new ManualJsonObject();
        allowedRoot.Set(
            "path",
            ManualJsonValue.String(string.IsNullOrEmpty(allowedRootOverride) ? fixture.SlotRoot : allowedRootOverride));
        allowedRoot.Set("kind", ManualJsonValue.String("Directory"));
        ManualJsonArray allowedRoots = new ManualJsonArray();
        allowedRoots.Add(ManualJsonValue.Object(allowedRoot));

        ManualJsonObject journal = new ManualJsonObject();
        journal.Set("schemaVersion", ManualJsonValue.Number(schemaVersion));
        journal.Set("transactionId", ManualJsonValue.String(journalTransactionId));
        journal.Set("manifestPath", ManualJsonValue.String(fixture.Manifest));
        journal.Set("allowedRoots", ManualJsonValue.Array(allowedRoots));
        journal.Set("mutations", ManualJsonValue.Array(mutations));
        File.WriteAllText(
            TransactionPath(fixture.StoreRoot, fileTransactionId, ".json"),
            ManualJson.Serialize(journal, true));
    }

    private static ManualJsonObject CreateMutation(
        string destination,
        bool originalExisted,
        bool hasReplacement,
        string expectedContent)
    {
        ManualJsonObject mutation = new ManualJsonObject();
        mutation.Set("destinationPath", ManualJsonValue.String(destination));
        mutation.Set("originalExisted", ManualJsonValue.Boolean(originalExisted));
        mutation.Set("hasReplacement", ManualJsonValue.Boolean(hasReplacement));
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedContent ?? string.Empty);
        mutation.Set("expectedSize", ManualJsonValue.Number(hasReplacement ? expectedBytes.LongLength : 0));
        mutation.Set(
            "expectedHash",
            ManualJsonValue.String(hasReplacement ? ComputeSha256(expectedBytes) : string.Empty));
        return mutation;
    }

    private static void AssertRestoreFailsClosed(Fixture fixture, string expectedErrorFragment)
    {
        ShelteredAPI.Saves.Runtime.DirectoryProvider.TestSlotRoot = fixture.SlotRoot;
        SaveBackupRepository repository = new SaveBackupRepository(fixture.StoreRoot);
        string error;
        Assert(!repository.RestoreSnapshot(fixture.Manifest, out error), "Restore did not fail closed.");
        Assert(
            !string.IsNullOrEmpty(error)
                && error.IndexOf(expectedErrorFragment, StringComparison.OrdinalIgnoreCase) >= 0,
            "Restore error did not identify the unresolved condition: " + error);
    }

    private static string ComputeSha256(byte[] bytes)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(bytes);
            StringBuilder result = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                result.Append(hash[i].ToString("x2"));
            return result.ToString();
        }
    }

    private static string CreateIdentityJson(string lineageId)
    {
        ManualJsonObject identity = new ManualJsonObject();
        identity.Set("schemaVersion", ManualJsonValue.Number(1));
        identity.Set("lineageId", ManualJsonValue.String(lineageId));
        return ManualJson.Serialize(identity, true);
    }

    private static string NewTransactionId()
    {
        return Guid.NewGuid().ToString("N");
    }

    private static string Artifact(string destination, string transactionId, string purpose)
    {
        return destination + "." + transactionId + ".restore." + purpose + ".tmp";
    }

    private static string TransactionPath(string storeRoot, string transactionId, string extension)
    {
        return Path.Combine(storeRoot, "restore-transactions", transactionId + extension);
    }

    private static void AssertTransactionStoreIsClean(string storeRoot)
    {
        string transactionRoot = Path.Combine(storeRoot, "restore-transactions");
        if (!Directory.Exists(transactionRoot))
            return;

        string[] files = Directory.GetFiles(transactionRoot);
        for (int i = 0; i < files.Length; i++)
        {
            Assert(
                string.Equals(Path.GetFileName(files[i]), ".repository.lock", StringComparison.Ordinal),
                "Restore transaction metadata was not cleaned: " + files[i]);
        }
    }

    private static void AssertCommittedTransactionIsPending(string storeRoot)
    {
        string transactionRoot = Path.Combine(storeRoot, "restore-transactions");
        string[] journals = Directory.GetFiles(transactionRoot, "*.json", SearchOption.TopDirectoryOnly);
        string[] markers = Directory.GetFiles(transactionRoot, "*.committed", SearchOption.TopDirectoryOnly);
        Assert(journals.Length == 1, "Committed restore did not preserve exactly one recovery journal.");
        Assert(markers.Length == 1, "Committed restore did not preserve exactly one durable commit marker.");
    }

    private static void AssertNoArtifacts(string destination, string transactionId)
    {
        Assert(!File.Exists(Artifact(destination, transactionId, "stage")), "Staged artifact was not cleaned.");
        Assert(!File.Exists(Artifact(destination, transactionId, "rollback")), "Rollback artifact was not cleaned.");
        Assert(!File.Exists(Artifact(destination, transactionId, "absent")), "Absent marker was not cleaned.");
        Assert(!File.Exists(Artifact(destination, transactionId, "discard")), "Discard artifact was not cleaned.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class Fixture
    {
        public string CaseRoot;
        public string StoreRoot;
        public string SlotRoot;
        public string Destination;
        public string Manifest;
        public string ScenarioId;
        public int AbsoluteSlot;
        public string LineageId;
        public string IdentityJson;
        public SaveBackupRepository Repository;
    }
}
'@ | Set-Content -LiteralPath (Join-Path $projectRoot "Program.cs") -Encoding UTF8

    dotnet run --project (Join-Path $projectRoot "RestoreCrashSafety.csproj") --configuration Release
    if ($LASTEXITCODE -ne 0) {
        throw "Restore crash-safety harness failed with exit code $LASTEXITCODE."
    }
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
