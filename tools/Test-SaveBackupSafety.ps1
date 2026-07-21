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

function Assert-SourcePattern {
    param(
        [string]$Name,
        [string]$RelativePath,
        [string]$Pattern
    )

    $text = Get-Content -LiteralPath (Join-Path $RepoRoot $RelativePath) -Raw
    if (-not [regex]::IsMatch($text, $Pattern, [Text.RegularExpressions.RegexOptions]::Singleline)) {
        throw "${Name}: expected source contract was not found in ${RelativePath}."
    }
}

Assert-SourcePattern `
    "vanilla pre-restore safety snapshot" `
    "ShelteredAPI\Saves\SaveBackups\SaveBackupService.cs" `
    "CreatePreRestoreSafetySnapshot\(snapshot,\s*null,\s*out error\).*repository\.RestoreSnapshot\(snapshot\.Ref\.ManifestPath,\s*out error\)"

Assert-SourcePattern `
    "moved custom pre-restore safety snapshot" `
    "ShelteredAPI\Saves\SaveBackups\SaveBackupService.cs" `
    "TryResolveCustomRestoreDestination\(snapshot,\s*out currentEntry,\s*out destination,\s*out error\).*CreatePreRestoreSafetySnapshot\(snapshot,\s*currentEntry,\s*out error\).*repository\.RestoreSnapshot\(snapshot\.Ref\.ManifestPath,\s*destination,\s*out error\)"

Assert-SourcePattern `
    "pre-restore snapshot uses a non-pruning creation policy" `
    "ShelteredAPI\Saves\SaveBackups\SaveBackupService.cs" `
    "safetySnapshotPolicy\s*=\s*new SaveBackupRetentionPolicy\s*\{.*Mode\s*=\s*SaveBackupRetentionMode\.Forever.*repository\.CreateSnapshot\(\s*target,\s*SaveBackupReason\.BeforeRestore,\s*safetySnapshotPolicy\)"

Assert-SourcePattern `
    "restore and delete safety snapshots persist retention pin metadata" `
    "ShelteredAPI\Saves\SaveBackups\SaveBackupRepository.cs" `
    "isSafetySnapshot\s*=\s*reason\s*==\s*SaveBackupReason\.BeforeRestore\s*\|\|\s*reason\s*==\s*SaveBackupReason\.BeforeDelete.*Set\(\""isPinned\"",\s*ManualJsonValue\.Boolean\(isSafetySnapshot\)\).*Set\(\s*\""pinnedAtUtc\"".*isSafetySnapshot\s*\?\s*createdAt\.ToString\(\""o\"",\s*CultureInfo\.InvariantCulture\).*Set\(\s*\""pinReason\"".*isSafetySnapshot\s*\?\s*reason\.ToString\(\)"

Assert-SourcePattern `
    "delete snapshot bypasses disabled ordinary retention" `
    "ShelteredAPI\Saves\SaveBackups\SaveBackupService.cs" `
    "BackupBeforeDelete\(.*?safetySnapshotPolicy\s*=\s*new SaveBackupRetentionPolicy\s*\{.*Mode\s*=\s*SaveBackupRetentionMode\.Forever.*repository\.CreateSnapshot\(\s*target,\s*SaveBackupReason\.BeforeDelete,\s*safetySnapshotPolicy\)"

Assert-SourcePattern `
    "delete router fails closed before mutation" `
    "ShelteredAPI\Saves\Runtime\SaveDeleteRouter.cs" `
    "BackupBeforeDelete\(.*?out error\).*?return false;.*?SaveStorageRouter\.DeleteBySlot"

Assert-SourcePattern `
    "delete UI suppresses vanilla deletion when preservation fails" `
    "ShelteredAPI\Saves\Paging\SlotSelectionPatchCoordinator.cs" `
    "OnDeleteMessageBox\.VanillaCleanup.*?out deleteError\)\).*?ShowDeletePreservationFailure\(deleteError\);\s*return false;"

Assert-SourcePattern `
    "delete intent capture binds at prompt open" `
    "ShelteredAPI\Saves\Paging\SlotSelectionPatches.cs" `
    "HarmonyPatch\(typeof\(SaveSlotButton\),\s*""OnClick""\).*?SaveSlotButtonOnClickPrefix\(__instance\).*?HarmonyPatch\(typeof\(SlotSelectionPanel\),\s*""PromptDeleteCurrentSlot""\).*?PromptDeleteCurrentSlotPrefix\(__instance\)"

Assert-SourcePattern `
    "vanilla delete validates intent before delete call" `
    "ShelteredAPI\Saves\Paging\SlotSelectionPatchCoordinator.cs" `
    "ValidateDeleteIntent\(panel,\s*selectedSlotIndex,\s*\(SaveEntry\)null,\s*out consistencyError\).*?SaveDeleteRouter\.DeleteAbsoluteSlot\(\s*absoluteSlot"

Assert-SourcePattern `
    "custom delete validates intent before delete call" `
    "ShelteredAPI\Saves\Paging\SlotSelectionPatchCoordinator.cs" `
    "ValidateDeleteIntent\(panel,\s*selectedSlotIndex,\s*entry,\s*out consistencyError\).*?SaveDeleteRouter\.DeleteAbsoluteSlot\(\s*scope\.StorageScenarioId,\s*entry\.absoluteSlot"

Assert-SourcePattern `
    "snapshot delete validates intent before delete call" `
    "ShelteredAPI\Saves\Paging\SlotSelectionPatchCoordinator.cs" `
    "ValidateDeleteIntent\(panel,\s*selectedSlotIndex,\s*snapshot,\s*out consistencyError\).*?SaveBackupService\.DeleteSnapshot\(snapshot,\s*out error\)"

Assert-SourcePattern `
    "page zero delete cancel clears intent" `
    "ShelteredAPI\Saves\Paging\SlotSelectionPatchCoordinator.cs" `
    "if\s*\(page\s*==\s*0\).*?else\s*\{\s*ClearDeleteIntent\(panel\);\s*\}.*?return true;"

Assert-SourcePattern `
    "custom delete cancel clears intent" `
    "ShelteredAPI\Saves\Paging\SlotSelectionPatchCoordinator.cs" `
    "if\s*\(response\s*!=\s*1\)\s*\{\s*ClearDeleteIntent\(panel\);\s*return false;\s*\}"

Assert-SourcePattern `
    "custom delete missing entry clears intent" `
    "ShelteredAPI\Saves\Paging\SlotSelectionPatchCoordinator.cs" `
    "else\s*\{\s*ClearDeleteIntent\(panel\);\s*MMLog\.WriteWarning\(\""\[OnDeleteMessageBox\] Could not find entry to delete"

Assert-SourcePattern `
    "snapshot delete cancel clears intent" `
    "ShelteredAPI\Saves\Paging\SlotSelectionPatchCoordinator.cs" `
    "HandleSnapshotDeleteMessageBox\(SlotSelectionPanel panel,\s*int response\).*?if\s*\(response\s*!=\s*1\)\s*\{\s*ClearDeleteIntent\(panel\);\s*return false;\s*\}"

Assert-SourcePattern `
    "snapshot delete identity compares SnapshotId" `
    "ShelteredAPI\Saves\Paging\SlotSelectionPatchCoordinator.cs" `
    "intentSnapshotId\s*=\s*intent\.Snapshot\.Ref\s*!=\s*null\s*\?\s*intent\.Snapshot\.Ref\.SnapshotId.*?snapshotId\s*=\s*snapshot\.Ref\s*!=\s*null\s*\?\s*snapshot\.Ref\.SnapshotId.*?!string\.Equals\(intentSnapshotId,\s*snapshotId,\s*StringComparison\.Ordinal\)"

Assert-SourcePattern `
    "retention excludes every pinned manifest from its limit" `
    "ShelteredAPI\Saves\SaveBackups\SaveBackupRepository.cs" `
    "if\s*\(!refs\[i\]\.IsPinned\)\s*unpinned\+\+.*if\s*\(snapshot\.IsPinned\)\s*continue.*File\.Delete\(snapshot\.ManifestPath\)"

Assert-SourcePattern `
    "garbage collection traces blobs through all surviving manifests" `
    "ShelteredAPI\Saves\SaveBackups\SaveBackupRepository.cs" `
    "List<SaveBackupSnapshotRef>\s+refs\s*=\s*ReadAllSnapshotRefs\(\).*AddReferencedHashes\(refs\[i\]\.ManifestPath,\s*referencedHashes\)"

Assert-SourcePattern `
    "failed custom backup blocks overwrite" `
    "ShelteredAPI\Saves\SaveRegistryCore.cs" `
    "BackupCustomEntryBeforeOverwrite\(entry\).*Refusing to overwrite custom save.*return null"

Assert-SourcePattern `
    "failed vanilla backup blocks overwrite" `
    "ShelteredAPI\Saves\Runtime\PlatformSaveOperationService.cs" `
    "BackupVanillaBeforeOverwrite\(type\).*Vanilla save cancelled.*return false"

Assert-SourcePattern `
    "empty slots remain archive candidates" `
    "ShelteredAPI\Saves\Paging\SlotSelectionSaveEntryResolver.cs" `
    "Empty slots remain relevant.*result\.Add\(visible\)"

$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("smm-save-backup-tests-" + [guid]::NewGuid().ToString("N"))
$projectRoot = Join-Path $tempRoot "CodecTests"
New-Item -ItemType Directory -Path $projectRoot -Force | Out-Null

try {
    $retentionProjectRoot = Join-Path $tempRoot "RetentionTests"
    New-Item -ItemType Directory -Path $retentionProjectRoot -Force | Out-Null
    $retentionSources = @(
        "ModAPI\Util\ManualJson.cs",
        "ShelteredAPI\Saves\CRC32.cs",
        "ShelteredAPI\Saves\SaveBackups\SaveBackupBlobCodec.cs",
        "ShelteredAPI\Saves\SaveBackups\DurableFileWriter.cs",
        "ShelteredAPI\Saves\SaveBackups\SaveBackupModels.cs",
        "ShelteredAPI\Saves\SaveBackups\SaveBackupRepository.cs"
    )
    foreach ($relativePath in $retentionSources) {
        Copy-Item `
            -LiteralPath (Join-Path $RepoRoot $relativePath) `
            -Destination (Join-Path $retentionProjectRoot ([IO.Path]::GetFileName($relativePath)))
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
'@ | Set-Content -LiteralPath (Join-Path $retentionProjectRoot "RetentionTests.csproj") -Encoding UTF8

    @'
using System;

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
        internal static string SlotRoot(string scenarioId, int absoluteSlot, bool create)
        {
            return string.Empty;
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
'@ | Set-Content -LiteralPath (Join-Path $retentionProjectRoot "RuntimeStubs.cs") -Encoding UTF8

    @'
using System;
using System.IO;
using ModAPI.Util;
using ShelteredAPI.Saves.Backups;

internal static class Program
{
    private static void Main()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "smm-retention-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
        try
        {
            Run(testRoot);
            Console.WriteLine("Save backup retention, pin, deduplication, and GC tests passed.");
        }
        finally
        {
            if (Directory.Exists(testRoot))
                Directory.Delete(testRoot, true);
        }
    }

    private static void Run(string testRoot)
    {
        string storeRoot = Path.Combine(testRoot, "store");
        string savePath = Path.Combine(testRoot, "save.dat");
        SaveBackupRepository repository = new SaveBackupRepository(storeRoot);
        SaveBackupTarget target = new SaveBackupTarget
        {
            TimelineKey = "retention-contract",
            SaveKind = "CustomSlot",
            ScenarioId = "Survival",
            AbsoluteSlot = 1,
            SaveId = "retention-contract-save"
        };
        target.Sources.Add(new SaveBackupSource
        {
            Id = "save",
            Path = savePath,
            Kind = SaveBackupSourceKind.File
        });

        string ordinaryId = Create(repository, target, savePath, "ordinary-prunable", SaveBackupReason.Manual, Forever());
        string ordinaryManifestPath = FindManifest(storeRoot, ordinaryId);
        ManualJsonObject ordinaryManifest = ReadManifest(ordinaryManifestPath);
        string ordinaryBlobPath = GetBlobPath(storeRoot, ordinaryManifest);

        string safetyId = Create(repository, target, savePath, "shared-protected-content", SaveBackupReason.BeforeRestore, Forever());
        string safetyManifestPath = FindManifest(storeRoot, safetyId);
        ManualJsonObject safetyManifest = ReadManifest(safetyManifestPath);
        string safetyBlobPath = GetBlobPath(storeRoot, safetyManifest);
        Assert(safetyManifest.GetBool("isPinned", false), "BeforeRestore manifest was not pinned.");
        Assert(safetyManifest.GetString("pinReason", string.Empty) == "BeforeRestore", "BeforeRestore pin reason was not persisted.");
        Assert(!string.IsNullOrEmpty(safetyManifest.GetString("pinnedAtUtc", string.Empty)), "BeforeRestore pin timestamp was empty.");

        string deleteSafetyId = Create(repository, target, savePath, "delete-protected-content", SaveBackupReason.BeforeDelete, Forever());
        string deleteSafetyManifestPath = FindManifest(storeRoot, deleteSafetyId);
        ManualJsonObject deleteSafetyManifest = ReadManifest(deleteSafetyManifestPath);
        Assert(deleteSafetyManifest.GetBool("isPinned", false), "BeforeDelete manifest was not pinned.");
        Assert(deleteSafetyManifest.GetString("pinReason", string.Empty) == "BeforeDelete", "BeforeDelete pin reason was not persisted.");
        Assert(!string.IsNullOrEmpty(deleteSafetyManifest.GetString("pinnedAtUtc", string.Empty)), "BeforeDelete pin timestamp was empty.");

        string userPinnedId = Create(repository, target, savePath, "shared-protected-content", SaveBackupReason.Manual, Forever());
        string userPinnedManifestPath = FindManifest(storeRoot, userPinnedId);
        ManualJsonObject userPinnedManifest = ReadManifest(userPinnedManifestPath);
        userPinnedManifest.Set("isPinned", ManualJsonValue.Boolean(true));
        userPinnedManifest.Set("pinnedAtUtc", ManualJsonValue.String(DateTime.UtcNow.ToString("o")));
        userPinnedManifest.Set("pinReason", ManualJsonValue.String("User"));
        File.WriteAllText(userPinnedManifestPath, ManualJson.Serialize(userPinnedManifest, true));
        string userPinnedBlobPath = GetBlobPath(storeRoot, userPinnedManifest);
        Assert(userPinnedBlobPath == safetyBlobPath, "Identical protected snapshots did not deduplicate.");

        string latestId = Create(repository, target, savePath, "latest-unpinned", SaveBackupReason.Manual, Limited(1));
        string latestManifestPath = FindManifest(storeRoot, latestId);
        string timelineRoot = Path.GetDirectoryName(latestManifestPath);
        string[] remainingManifests = Directory.GetFiles(timelineRoot, "*.json", SearchOption.TopDirectoryOnly);

        Assert(remainingManifests.Length == 4, "Limited retention did not keep three pins plus one unpinned snapshot.");
        Assert(!File.Exists(ordinaryManifestPath), "Ordinary over-limit snapshot survived retention.");
        Assert(!File.Exists(ordinaryBlobPath), "GC retained an unreferenced ordinary blob.");
        Assert(File.Exists(safetyManifestPath), "BeforeRestore safety snapshot was pruned.");
        Assert(File.Exists(deleteSafetyManifestPath), "BeforeDelete safety snapshot was pruned.");
        Assert(File.Exists(userPinnedManifestPath), "User-pinned snapshot was pruned.");
        Assert(File.Exists(latestManifestPath), "Newest ordinary snapshot was pruned.");
        Assert(File.Exists(safetyBlobPath), "GC deleted a blob referenced by protected snapshots.");

        ManualJsonObject persistedUserPin = ReadManifest(userPinnedManifestPath);
        Assert(persistedUserPin.GetBool("isPinned", false), "Retention changed user pin state.");
        Assert(persistedUserPin.GetString("pinReason", string.Empty) == "User", "Retention changed user pin metadata.");

        ManualJsonObject index = ReadManifest(Path.Combine(storeRoot, "index.json"));
        ManualJsonArray snapshots = index.GetArray("snapshots");
        int pinnedIndexEntries = 0;
        for (int i = 0; i < snapshots.Items.Count; i++)
        {
            ManualJsonObject item = snapshots.Items[i].ObjectValue;
            if (item != null && item.GetBool("isPinned", false))
                pinnedIndexEntries++;
        }
        Assert(pinnedIndexEntries == 3, "Index did not retain restore, delete, and user pin states.");

        int futureDeleted = repository.PruneSnapshotsAfter(
            target.TimelineKey,
            DateTime.MinValue,
            string.Empty);
        Assert(futureDeleted == 1, "Future pruning did not remove only the unpinned snapshot.");
        Assert(!File.Exists(latestManifestPath), "Future pruning retained an eligible unpinned snapshot.");
        Assert(File.Exists(safetyManifestPath), "Future pruning deleted a BeforeRestore safety snapshot.");
        Assert(File.Exists(deleteSafetyManifestPath), "Future pruning deleted a BeforeDelete safety snapshot.");
        Assert(File.Exists(userPinnedManifestPath), "Future pruning deleted a user-pinned snapshot.");
    }

    private static string Create(
        SaveBackupRepository repository,
        SaveBackupTarget target,
        string savePath,
        string content,
        SaveBackupReason reason,
        SaveBackupRetentionPolicy policy)
    {
        File.WriteAllText(savePath, content);
        string snapshotId = repository.CreateSnapshot(target, reason, policy);
        Assert(!string.IsNullOrEmpty(snapshotId), "CreateSnapshot returned no ID for " + reason + ".");
        return snapshotId;
    }

    private static SaveBackupRetentionPolicy Forever()
    {
        return new SaveBackupRetentionPolicy
        {
            Mode = SaveBackupRetentionMode.Forever,
            SnapshotLimit = 0
        };
    }

    private static SaveBackupRetentionPolicy Limited(int limit)
    {
        return new SaveBackupRetentionPolicy
        {
            Mode = SaveBackupRetentionMode.Limited,
            SnapshotLimit = limit
        };
    }

    private static string FindManifest(string storeRoot, string snapshotId)
    {
        string[] matches = Directory.GetFiles(storeRoot, snapshotId + ".json", SearchOption.AllDirectories);
        Assert(matches.Length == 1, "Expected one manifest for " + snapshotId + ".");
        return matches[0];
    }

    private static ManualJsonObject ReadManifest(string path)
    {
        ManualJsonObject root;
        string error;
        Assert(ManualJson.TryParseObject(File.ReadAllText(path), out root, out error), "Manifest parse failed: " + error);
        return root;
    }

    private static string GetBlobPath(string storeRoot, ManualJsonObject manifest)
    {
        ManualJsonObject file = manifest.GetArray("files").Items[0].ObjectValue;
        return Path.Combine(storeRoot, file.GetString("blobPath", string.Empty).Replace('/', Path.DirectorySeparatorChar));
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
'@ | Set-Content -LiteralPath (Join-Path $retentionProjectRoot "Program.cs") -Encoding UTF8

    dotnet run --project (Join-Path $retentionProjectRoot "RetentionTests.csproj") --configuration Release
    if ($LASTEXITCODE -ne 0) {
        throw "Save backup retention test process failed with exit code $LASTEXITCODE."
    }

    Copy-Item `
        -LiteralPath (Join-Path $RepoRoot "ShelteredAPI\Saves\SaveBackups\SaveBackupBlobCodec.cs") `
        -Destination (Join-Path $projectRoot "SaveBackupBlobCodec.cs")

    @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>
</Project>
'@ | Set-Content -LiteralPath (Join-Path $projectRoot "CodecTests.csproj") -Encoding UTF8

    @'
using System;
using System.Diagnostics;
using System.IO;
using ShelteredAPI.Saves.Backups;

internal static class Program
{
    private static void Main()
    {
        RoundTrip(new byte[0], "empty");
        RoundTrip(BuildRepeating(1024 * 1024), "repeating");
        RoundTrip(BuildRandom(1024 * 1024), "random");
        AssertLegacyDecode(
            new byte[] { (byte)'S', (byte)'B', (byte)'L', (byte)'Z', 1, 3, 0, 0, 0, 0, (byte)'A', (byte)'B', (byte)'C' },
            new byte[] { (byte)'A', (byte)'B', (byte)'C' },
            "legacy literals");
        AssertLegacyDecode(
            new byte[] { (byte)'S', (byte)'B', (byte)'L', (byte)'Z', 1, 10, 0, 0, 0, 2, (byte)'A', 22, 0 },
            BuildFilled(10, (byte)'A'),
            "legacy overlap match");

        byte[] malformed = new byte[] { (byte)'S', (byte)'B', (byte)'L', (byte)'Z', 1, 0xff, 0xff, 0xff, 0x7f };
        ExpectIOException(delegate { SaveBackupBlobCodec.Decompress(malformed); }, "oversized output");

        Stopwatch timer = Stopwatch.StartNew();
        SaveBackupBlobCodec.Compress(BuildRandom(1024 * 1024));
        timer.Stop();
        if (timer.Elapsed > TimeSpan.FromSeconds(10))
            throw new Exception("Random 1 MiB compression exceeded 10 seconds: " + timer.Elapsed);

        Console.WriteLine("Save backup codec tests passed in " + timer.ElapsedMilliseconds + " ms for random 1 MiB.");
    }

    private static void RoundTrip(byte[] input, string name)
    {
        byte[] compressed = SaveBackupBlobCodec.Compress(input);
        byte[] output = SaveBackupBlobCodec.Decompress(compressed);
        if (output.Length != input.Length)
            throw new Exception(name + " round trip changed length.");

        for (int i = 0; i < input.Length; i++)
        {
            if (input[i] != output[i])
                throw new Exception(name + " round trip differed at byte " + i + ".");
        }
    }

    private static byte[] BuildRepeating(int length)
    {
        byte[] bytes = new byte[length];
        byte[] pattern = new byte[] { 0x53, 0x61, 0x76, 0x65, 0x44, 0x61, 0x74, 0x61 };
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = pattern[i % pattern.Length];
        return bytes;
    }

    private static byte[] BuildRandom(int length)
    {
        byte[] bytes = new byte[length];
        new Random(1234567).NextBytes(bytes);
        return bytes;
    }

    private static byte[] BuildFilled(int length, byte value)
    {
        byte[] bytes = new byte[length];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = value;
        return bytes;
    }

    private static void AssertLegacyDecode(byte[] compressed, byte[] expected, string name)
    {
        byte[] output = SaveBackupBlobCodec.Decompress(compressed);
        if (output.Length != expected.Length)
            throw new Exception(name + " changed length.");

        for (int i = 0; i < output.Length; i++)
        {
            if (output[i] != expected[i])
                throw new Exception(name + " differed at byte " + i + ".");
        }
    }

    private static void ExpectIOException(Action action, string name)
    {
        try
        {
            action();
        }
        catch (IOException)
        {
            return;
        }

        throw new Exception(name + " input was accepted.");
    }
}
'@ | Set-Content -LiteralPath (Join-Path $projectRoot "Program.cs") -Encoding UTF8

    dotnet run --project (Join-Path $projectRoot "CodecTests.csproj") --configuration Release
    if ($LASTEXITCODE -ne 0) {
        throw "Save backup codec test process failed with exit code $LASTEXITCODE."
    }
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}

Write-Host "Save backup safety contracts passed."
