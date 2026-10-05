// Behavioural tests for the desktop manager's mod handling: load order and
// fixes, undo, profiles, shared lists, local installs, crash reports, ZIP
// output and the Content Workshop's generated IDs. Runs headless against a
// built Manager.exe (see Test-ModManagerBehavior.ps1). Every fixture lives in
// a temporary folder; nothing touches the network, the game or Documents.
//
// Determinism is checked three ways: repeating an operation, feeding the same
// data in a different physical order, and running under other system
// languages. Results must be identical each time.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Manager.Controls;
using Manager.Core.Models;
using Manager.Core.Services;
using Manager.Views;
using ShelteredModManager.ContentPacks;

internal static class ModManagerBehaviorHarness
{
    private const BindingFlags AnyMember = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly string[] Cultures = new string[] { "en-US", "tr-TR", "de-DE", "ar-SA", "ja-JP" };
    private static readonly DateTime FixedTime = new DateTime(2026, 1, 2, 3, 4, 6);
    private static int _passed;
    private static int _failed;
    private static string _root;
    private static string _only;

    [STAThread]
    private static int Main(string[] args)
    {
        string independentOutput = args.Length > 0 && args[0].Length > 0 ? args[0] : null;
        // Optional second argument: run only the tests whose names contain it.
        _only = args.Length > 1 ? args[1] : null;
        _root = Path.Combine(Path.GetTempPath(), "SmmBehavior-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        try
        {
            // Load order
            Run("fix order keeps a valid order unchanged", FixOrderKeepsValidOrder);
            Run("fix order moves only the dependency that is out of place", FixOrderMovesOnlyWhatMust);
            Run("fix order meets every rule on a large random setup", FixOrderRandomSetups);
            Run("fix order is idempotent", FixOrderIdempotent);
            Run("fix order ignores the order rules are written in", FixOrderRuleOrderIndependent);
            Run("fix order breaks dependency loops without dropping mods", FixOrderLoops);
            Run("fix order ignores self, duplicate, missing and malformed rules", FixOrderOddRules);
            Run("fix order reads version constraints and any casing", FixOrderConstraintsAndCase);
            Run("fix order handles empty, null and duplicate input", FixOrderDegenerateInput);
            Run("enabled and disabled lists do not depend on discovery order", SplitIsDeterministic);
            Run("discovery returns the same order every time", DiscoveryIsSorted);
            Run("Harmony is treated as part of SMM", HarmonyIsBundled);
            Run("dependency problems name the mod and what it needs", DependencyProblems);

            // Mod lists and profiles
            Run("mod list round trip keeps order and fields", ModListRoundTrip);
            Run("mod list import drops invalid and duplicate entries", ModListSanitises);
            Run("mod list import refuses broken, oversized and foreign files", ModListRefusesJunk);
            Run("mod list saves are identical apart from the timestamp", ModListSaveDeterministic);
            Run("saved JSON keeps one member order however reflection was used before", StableJsonOrder);
            Run("stable JSON writes exactly the data the serializer would", StableJsonSameData);
            Run("stable JSON refuses reference loops instead of crashing", StableJsonLoops);
            Run("profile names that Windows cannot store are refused", ProfileNameRules);
            Run("profiles list in the same order in every language", ProfileListOrder);
            Run("renaming a profile keeps it active and takes new casing", ProfileRename);
            Run("the active profile never points at a missing or unsafe file", ActiveProfileSafety);
            Run("profile writes refuse names outside the profile folder", ProfileWriteBoundary);
            Run("profile reads and deletes never use an invalid name", ProfileReadDeleteBoundary);
            Run("profile rename and activation refuse invalid names", ProfileRenameActiveBoundary);

            // ZIP writing
            Run("zip output is byte-identical for the same input", ZipDeterministic);
            Run("zip round trip covers empty, unicode, stored and deflated entries", ZipRoundTrip);
            Run("zip CRC matches the standard check value", ZipCrc);
            Run("zip refuses more entries than the format holds", ZipEntryLimit);

            // Local installs
            Run("dropped zip with the mod at its root installs under its ID", InstallRootZip);
            Run("dropped zip with a wrapper folder installs the mod folder", InstallWrappedZip);
            Run("zip with two mods installs the same one every time", InstallAmbiguousZipDeterministic);
            Run("unsafe, empty, fake and oversized archives are refused", InstallRefusesBadArchives);
            Run("zips that would expand past the size limits are refused before extracting", InstallRefusesSizeBombs);
            Run("mods with missing metadata or reserved names are refused", InstallRefusesBadMods);
            Run("folders already in the mods folder are refused", InstallRefusesInsideModsFolder);
            Run("replacing asks first, keeps a backup and can be declined", InstallReplace);
            Run("a different mod in the target folder is never overwritten", InstallFolderCollision);
            Run("a failed replace leaves the installed mod intact", InstallRollbackOnLockedFile);
            Run("installing the same zip twice gives the same files", InstallIdempotent);
            Run("non-ASCII folder names install", InstallUnicodeFolder);

            // Crash reports
            Run("crash folders: pattern, contents and time window", CrashFoldersFound);
            Run("crash folders sort newest first, then by path", CrashFoldersOrder);
            Run("newer Unity builds are found through app.info only", CrashFoldersAppInfo);
            Run("crash report contents, redaction and dump handling", CrashReportContents);
            Run("crash report handles locked, huge and missing logs", CrashReportDifficultFiles);
            Run("crash reports never overwrite each other and name the same way everywhere", CrashReportNaming);
            Run("crash report entries are the same on every run", CrashReportDeterministic);

            // Workshop IDs and item names
            Run("workshop IDs are ASCII, accent-folded and culture independent", WorkshopSlugs);
            Run("workshop pack IDs, unique IDs and folder names", WorkshopIdsAndFolders);
            Run("workshop saves and exports are the same every time", WorkshopSavesStable);
            Run("a rejected icon import preserves the assigned image", WorkshopRejectedIcon);
            Run("a failed workshop save keeps both previous documents", WorkshopSaveRollback);
            Run("importing after switching items keeps icon saves on the imported item", WorkshopImportTarget);
            Run("base-game item names and IDs map one to one", VanillaItemsBijective);
            Run("pack items that clash with base-game names stay distinct", ItemCatalogClashes);

            // Settings
            Run("session timestamps survive every calendar and culture", SettingsTimestamps);

            // Mod manager tab (headless)
            Run("toggling and moving write the load order and keep unknown entries", TabToggleAndMove);
            Run("undo restores exact earlier states and stops at its limit", TabUndo);
            Run("a failed save leaves nothing to undo", TabFailedSaveNotUndoable);
            Run("fix load order on a working order changes nothing", TabFixNoop);
            Run("switch keyboard and drag gestures raise the right changes", TabListGestures);
            Run("a switch double-click changes only its original mod", SwitchDoubleClickTarget);
            Run("only a left double-click on row text changes a mod", SwitchDoubleClickButtons);
            Run("order-only changes keep the discovered inventory until explicit refresh", TabOrderOnlyInventory);
            Run("order-only validation retains intrinsic API incompatibility", TabOrderOnlyCompatibility);
            Run("filtering out the selected mod clears its details", TabFilterClearsDetails);
            Run("switch list rebuilds publish only the final selection", SwitchListRebuildSelection);
            Run("profiles switch, follow changes and skip missing mods", TabProfiles);
            Run("profile switches resolve newly added and removed folders", TabProfileCurrentInventory);
            Run("broken and odd mods folders load without errors", TabOddModsFolders);
            Run("dropping a zip on the tab installs and turns it on", TabDropInstall);
            Run("a mod list waiting on installs is applied once, or dropped if they never start", TabPendingList);

            // Nexus tab
            Run("Update all queue runs each install once and finishes once", NexusBatchQueue);
            Run("Update all can be stopped and ignores late results", NexusBatchStop);
            Run("a batch never replaces a running one, and says when nothing started", NexusBatchBusy);
            Run("Nexus manual actions stay disabled while a batch runs", NexusBatchSelectionActions);
            Run("Nexus manual actions stay disabled while updates are checked", NexusPreparingSelectionActions);
            Run("Nexus manual install requests cannot enter a busy batch", NexusBusyManualRequests);
            Run("Nexus sign-in install shortcut waits for the batch", NexusBusySignInShortcut);
            Run("Nexus completion callbacks keep actions gated until the batch ends", NexusBatchCallbackActions);
            Run("stale results never advance a restarted Nexus batch", NexusRestartIgnoresStaleResults);
            Run("queued stale and manual callbacks never count toward a Nexus batch", NexusQueuedCompletionOwnership);
            Run("an invalid Nexus link never advances the current batch", NexusInvalidLinkOwnership);
            Run("active Nexus install work blocks other install actions", NexusActiveWorkGating);
            Run("stopping a Nexus queue retains its worker gate until that worker ends", NexusStoppedWorkGating);
            Run("changelog summaries are plain text", NexusChangelogSummary);

            if (!string.IsNullOrEmpty(independentOutput))
                WriteIndependentSamples(independentOutput);
        }
        finally
        {
            TryDeleteDirectory(_root);
        }

        Console.WriteLine();
        Console.WriteLine("Mod manager behaviour: " + _passed + " passed, " + _failed + " failed.");
        return _failed == 0 ? 0 : 1;
    }

    // ------------------------------------------------------------------ runner

    private static void Run(string name, Action test)
    {
        if (!string.IsNullOrEmpty(_only) && name.IndexOf(_only, StringComparison.OrdinalIgnoreCase) < 0)
            return;
        try
        {
            test();
            _passed++;
            Console.WriteLine("PASS " + name);
        }
        catch (Exception ex)
        {
            Exception inner = ex;
            while (inner is TargetInvocationException && inner.InnerException != null)
                inner = inner.InnerException;
            _failed++;
            Console.WriteLine("FAIL " + name + ": " + inner.GetType().Name + ": " + inner.Message);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }

    private static void Same(IList<string> expected, IList<string> actual, string what)
    {
        string e = string.Join(", ", ToArray(expected));
        string a = string.Join(", ", ToArray(actual));
        if (!string.Equals(e, a, StringComparison.Ordinal))
            throw new Exception(what + ": expected [" + e + "] but got [" + a + "]");
    }

    /// <summary>Fails with the first position where two texts differ.</summary>
    private static void SameText(string expected, string actual, string what)
    {
        if (string.Equals(expected, actual, StringComparison.Ordinal))
            return;
        int at = 0;
        while (at < expected.Length && at < actual.Length && expected[at] == actual[at])
            at++;
        int start = Math.Max(0, at - 20);
        throw new Exception(what + " at character " + at + ": expected \"" + Excerpt(expected, start) + "\" but got \"" + Excerpt(actual, start) + "\"");
    }

    private static string Excerpt(string text, int start)
    {
        return start >= text.Length ? string.Empty : text.Substring(start, Math.Min(60, text.Length - start));
    }

    private static string[] ToArray(IList<string> list)
    {
        string[] result = new string[list.Count];
        list.CopyTo(result, 0);
        return result;
    }

    private static void Throws(Action action, string what)
    {
        try
        {
            action();
        }
        catch (Exception)
        {
            return;
        }
        throw new Exception(what + " did not throw");
    }

    private static T InCulture<T>(string culture, Func<T> body)
    {
        CultureInfo previous = Thread.CurrentThread.CurrentCulture;
        CultureInfo previousUi = Thread.CurrentThread.CurrentUICulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(culture);
            return body();
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
            Thread.CurrentThread.CurrentUICulture = previousUi;
        }
    }

    // ------------------------------------------------------------------ reflection

    private static Type ManagerType(string fullName)
    {
        return typeof(ModItem).Assembly.GetType(fullName, true);
    }

    private static object Call(object target, string name, params object[] args)
    {
        Type type = target as Type ?? target.GetType();
        object instance = target is Type ? null : target;
        for (Type t = type; t != null; t = t.BaseType)
        {
            foreach (MethodInfo method in t.GetMethods(AnyMember | BindingFlags.DeclaredOnly))
            {
                if (method.Name != name || method.GetParameters().Length != args.Length)
                    continue;
                try
                {
                    return method.Invoke(instance, args);
                }
                catch (TargetInvocationException ex)
                {
                    throw ex.InnerException;
                }
            }
        }
        throw new MissingMethodException(type.Name, name);
    }

    private static object Field(object target, string name)
    {
        for (Type t = target.GetType(); t != null; t = t.BaseType)
        {
            FieldInfo field = t.GetField(name, AnyMember | BindingFlags.DeclaredOnly);
            if (field != null)
                return field.GetValue(target);
        }
        throw new MissingFieldException(target.GetType().Name, name);
    }

    private static void SetField(object target, string name, object value)
    {
        for (Type t = target.GetType(); t != null; t = t.BaseType)
        {
            FieldInfo field = t.GetField(name, AnyMember | BindingFlags.DeclaredOnly);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }
        }
        throw new MissingFieldException(target.GetType().Name, name);
    }

    // ------------------------------------------------------------------ fixtures

    private static string NewDir(string name)
    {
        string path = Path.Combine(_root, name + "-" + Guid.NewGuid().ToString("N").Substring(0, 6));
        Directory.CreateDirectory(path);
        return path;
    }

    private static ModItem Mod(string id, params string[] dependsOn)
    {
        ModItem mod = new ModItem(id, id, string.Empty);
        mod.DependsOn = dependsOn ?? new string[0];
        return mod;
    }

    private static ModItem ModRules(string id, string[] dependsOn, string[] loadAfter, string[] loadBefore)
    {
        ModItem mod = Mod(id, dependsOn);
        mod.LoadAfter = loadAfter ?? new string[0];
        mod.LoadBefore = loadBefore ?? new string[0];
        return mod;
    }

    private static List<string> Ids(IEnumerable<ModItem> mods)
    {
        List<string> ids = new List<string>();
        foreach (ModItem mod in mods)
            ids.Add(mod.Id);
        return ids;
    }

    private static void WriteModFolder(string folder, string id, string name, string version, string[] dependsOn)
    {
        Directory.CreateDirectory(Path.Combine(folder, "About"));
        Dictionary<string, object> about = new Dictionary<string, object>();
        if (id != null) about["id"] = id;
        if (name != null) about["name"] = name;
        if (version != null) about["version"] = version;
        about["authors"] = new string[] { "Tester" };
        about["dependsOn"] = dependsOn ?? new string[0];
        File.WriteAllText(Path.Combine(Path.Combine(folder, "About"), "About.json"), new JavaScriptSerializer().Serialize(about));
    }

    private static string AboutJson(string id, string name, string version)
    {
        Dictionary<string, object> about = new Dictionary<string, object>();
        about["id"] = id;
        about["name"] = name;
        about["version"] = version;
        return new JavaScriptSerializer().Serialize(about);
    }

    /// <summary>Writes a ZIP through the manager's own writer at a fixed time.</summary>
    private static void WriteZip(string path, bool compress, params string[] nameThenText)
    {
        Type writer = ManagerType("Manager.Core.Services.ZipFileWriter");
        Type entryType = ManagerType("Manager.Core.Services.ZipFileWriter+Entry");
        IList entries = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(entryType));
        for (int i = 0; i + 1 < nameThenText.Length; i += 2)
            entries.Add(Activator.CreateInstance(entryType, new object[] { nameThenText[i], Encoding.UTF8.GetBytes(nameThenText[i + 1]) }));
        Call(writer, "Write", path, entries, compress, FixedTime);
    }

    private static void WriteZipBytes(string path, bool compress, string[] names, byte[][] data)
    {
        Type writer = ManagerType("Manager.Core.Services.ZipFileWriter");
        Type entryType = ManagerType("Manager.Core.Services.ZipFileWriter+Entry");
        IList entries = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(entryType));
        for (int i = 0; i < names.Length; i++)
            entries.Add(Activator.CreateInstance(entryType, new object[] { names[i], data[i] }));
        Call(writer, "Write", path, entries, compress, FixedTime);
    }

    /// <summary>Reads a ZIP back with the manager's extractor (which checks paths, sizes and CRCs).</summary>
    private static Dictionary<string, byte[]> ReadZip(string path)
    {
        string target = NewDir("unzipped");
        object extractor = Activator.CreateInstance(ManagerType("Manager.Core.Services.ZipArchiveExtractor"), true);
        object[] args = new object[] { path, target, null };
        bool ok = (bool)extractor.GetType().GetMethod("TryExtract").Invoke(extractor, args);
        Check(ok, "the manager could not read back its own zip: " + args[2]);
        Dictionary<string, byte[]> files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        string prefix = Path.GetFullPath(target).TrimEnd('\\') + "\\";
        foreach (string file in Directory.GetFiles(target, "*", SearchOption.AllDirectories))
            files[Path.GetFullPath(file).Substring(prefix.Length).Replace('\\', '/')] = File.ReadAllBytes(file);
        return files;
    }

    private static List<string> ZipEntryNames(string path)
    {
        // Central directory walk: names in stored order.
        byte[] bytes = File.ReadAllBytes(path);
        int end = -1;
        for (int i = bytes.Length - 22; i >= 0; i--)
        {
            if (BitConverter.ToUInt32(bytes, i) == 0x06054b50u) { end = i; break; }
        }
        Check(end >= 0, "no end of central directory in " + path);
        int count = BitConverter.ToUInt16(bytes, end + 10);
        int offset = (int)BitConverter.ToUInt32(bytes, end + 16);
        List<string> names = new List<string>();
        for (int n = 0; n < count; n++)
        {
            Check(BitConverter.ToUInt32(bytes, offset) == 0x02014b50u, "bad central directory entry");
            int nameLength = BitConverter.ToUInt16(bytes, offset + 28);
            int extra = BitConverter.ToUInt16(bytes, offset + 30);
            int comment = BitConverter.ToUInt16(bytes, offset + 32);
            names.Add(Encoding.UTF8.GetString(bytes, offset + 46, nameLength));
            offset += 46 + nameLength + extra + comment;
        }
        return names;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                    File.SetAttributes(file, FileAttributes.Normal);
                Directory.Delete(path, true);
            }
        }
        catch
        {
        }
    }

    // ================================================================== load order

    private static void FixOrderKeepsValidOrder()
    {
        List<ModItem> mods = new List<ModItem>
        {
            Mod("harmony"), Mod("core", "harmony"), Mod("ui"), Mod("addon", "core"), Mod("extra")
        };
        Same(Ids(mods), LoadOrderService.FixOrder(mods), "valid order");
    }

    private static void FixOrderMovesOnlyWhatMust()
    {
        // vanilla needs harmony, which sits two places too low.
        List<ModItem> mods = new List<ModItem>
        {
            Mod("vanilla", "harmony"), Mod("display"), Mod("harmony"), Mod("lifespan")
        };
        Same(new List<string> { "harmony", "vanilla", "display", "lifespan" }, LoadOrderService.FixOrder(mods), "minimal fix");

        // A chain pulls each link up just far enough.
        List<ModItem> chain = new List<ModItem> { Mod("c", "b"), Mod("x"), Mod("b", "a"), Mod("a") };
        Same(new List<string> { "a", "b", "c", "x" }, LoadOrderService.FixOrder(chain), "chain");
    }

    private static void FixOrderRandomSetups()
    {
        for (int seed = 1; seed <= 25; seed++)
        {
            List<ModItem> mods = RandomSetup(seed, 150);
            List<string> first = LoadOrderService.FixOrder(mods);
            Check(first.Count == mods.Count, "seed " + seed + " lost mods");
            AssertRulesMet(mods, first, "seed " + seed);
            for (int repeat = 0; repeat < 3; repeat++)
                Same(first, LoadOrderService.FixOrder(mods), "seed " + seed + " repeat");
            foreach (string culture in Cultures)
            {
                List<ModItem> captured = mods;
                Same(first, InCulture(culture, delegate { return LoadOrderService.FixOrder(captured); }), "seed " + seed + " in " + culture);
            }
        }
    }

    /// <summary>A random acyclic setup: each mod may depend on or load after mods with lower numbers.</summary>
    private static List<ModItem> RandomSetup(int seed, int count)
    {
        Random random = new Random(seed);
        List<string> names = new List<string>();
        for (int i = 0; i < count; i++)
            names.Add("mod" + i.ToString("000"));
        List<ModItem> mods = new List<ModItem>();
        for (int i = 0; i < count; i++)
        {
            List<string> depends = new List<string>();
            List<string> after = new List<string>();
            List<string> before = new List<string>();
            for (int j = 0; j < i; j++)
            {
                int roll = random.Next(100);
                if (roll < 3) depends.Add(names[j]);
                else if (roll < 5) after.Add(names[j]);
            }
            for (int j = i + 1; j < count; j++)
                if (random.Next(100) < 2) before.Add(names[j]);
            mods.Add(ModRules(names[i], depends.ToArray(), after.ToArray(), before.ToArray()));
        }
        // Present them in a shuffled load order.
        for (int i = mods.Count - 1; i > 0; i--)
        {
            int k = random.Next(i + 1);
            ModItem swap = mods[i];
            mods[i] = mods[k];
            mods[k] = swap;
        }
        return mods;
    }

    private static void AssertRulesMet(List<ModItem> mods, List<string> order, string what)
    {
        Dictionary<string, int> at = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < order.Count; i++)
            at[order[i]] = i;
        foreach (ModItem mod in mods)
        {
            foreach (string d in mod.DependsOn)
                if (at.ContainsKey(d)) Check(at[d] < at[mod.Id], what + ": " + d + " must load before " + mod.Id);
            foreach (string d in mod.LoadAfter)
                if (at.ContainsKey(d)) Check(at[d] < at[mod.Id], what + ": " + d + " must load before " + mod.Id);
            foreach (string d in mod.LoadBefore)
                if (at.ContainsKey(d)) Check(at[mod.Id] < at[d], what + ": " + mod.Id + " must load before " + d);
        }
    }

    private static void FixOrderIdempotent()
    {
        for (int seed = 100; seed < 110; seed++)
        {
            List<ModItem> mods = RandomSetup(seed, 80);
            List<string> once = LoadOrderService.FixOrder(mods);
            Dictionary<string, ModItem> byId = new Dictionary<string, ModItem>();
            foreach (ModItem mod in mods) byId[mod.Id] = mod;
            List<ModItem> reordered = new List<ModItem>();
            foreach (string id in once) reordered.Add(byId[id]);
            Same(once, LoadOrderService.FixOrder(reordered), "seed " + seed + " fixing a fixed order");
        }
    }

    private static void FixOrderRuleOrderIndependent()
    {
        for (int seed = 200; seed < 210; seed++)
        {
            List<ModItem> mods = RandomSetup(seed, 60);
            List<string> expected = LoadOrderService.FixOrder(mods);
            Random random = new Random(seed);
            List<ModItem> shuffledRules = new List<ModItem>();
            foreach (ModItem mod in mods)
                shuffledRules.Add(ModRules(mod.Id, Shuffle(mod.DependsOn, random), Shuffle(mod.LoadAfter, random), Shuffle(mod.LoadBefore, random)));
            Same(expected, LoadOrderService.FixOrder(shuffledRules), "seed " + seed + " with rules reordered");
        }
    }

    private static string[] Shuffle(string[] values, Random random)
    {
        string[] copy = (string[])values.Clone();
        for (int i = copy.Length - 1; i > 0; i--)
        {
            int k = random.Next(i + 1);
            string swap = copy[i];
            copy[i] = copy[k];
            copy[k] = swap;
        }
        return copy;
    }

    private static void FixOrderLoops()
    {
        // a and b need each other; c needs a. Nothing may be dropped, and the result is stable.
        List<ModItem> mods = new List<ModItem> { Mod("x"), Mod("b", "a"), Mod("a", "b"), Mod("c", "a") };
        List<string> fixedOrder = LoadOrderService.FixOrder(mods);
        Check(fixedOrder.Count == 4, "a loop dropped mods: " + string.Join(", ", fixedOrder.ToArray()));
        Same(fixedOrder, LoadOrderService.FixOrder(mods), "loop repeat");
        Check(fixedOrder.IndexOf("a") < fixedOrder.IndexOf("c"), "c must still follow a");
        // The loop is broken at its earliest mod (b), so b stays near its place.
        Same(new List<string> { "x", "b", "a", "c" }, fixedOrder, "loop break point");

        List<ModItem> triangle = new List<ModItem> { Mod("p", "r"), Mod("q", "p"), Mod("r", "q") };
        List<string> t = LoadOrderService.FixOrder(triangle);
        Check(t.Count == 3, "triangle lost mods");
        Same(t, LoadOrderService.FixOrder(triangle), "triangle repeat");
    }

    private static void FixOrderOddRules()
    {
        List<ModItem> mods = new List<ModItem>
        {
            ModRules("a", new string[] { "a", "", null, "   ", "not.installed", "b", "b" }, new string[] { ">= 1.0", "!!" }, null),
            Mod("b")
        };
        Same(new List<string> { "b", "a" }, LoadOrderService.FixOrder(mods), "odd rules");
    }

    private static void FixOrderConstraintsAndCase()
    {
        List<ModItem> mods = new List<ModItem>
        {
            ModRules("coolnether123.addon", new string[] { "CoolNether123.Core >= 1.2.0" }, null, null),
            Mod("coolnether123.core")
        };
        Same(new List<string> { "coolnether123.core", "coolnether123.addon" }, LoadOrderService.FixOrder(mods), "constraint with casing");
    }

    private static void FixOrderDegenerateInput()
    {
        Check(LoadOrderService.FixOrder(new List<ModItem>()).Count == 0, "empty list");
        Check(LoadOrderService.FixOrder(null).Count == 0, "null list");
        List<ModItem> duplicates = new List<ModItem> { Mod("same"), null, Mod("SAME"), Mod("other", "same") };
        Same(new List<string> { "same", "other" }, LoadOrderService.FixOrder(duplicates), "duplicates and nulls");
    }

    private static void SplitIsDeterministic()
    {
        string mods = NewDir("split");
        File.WriteAllText(Path.Combine(mods, "loadorder.json"), "{\"order\":[\"b\",\"dup\",\"a\"]}");
        List<ModItem> all = new List<ModItem>();
        foreach (string id in new string[] { "a", "b", "c", "d", "dup" })
            all.Add(new ModItem(id, "Same name", Path.Combine(mods, id)));
        all.Add(new ModItem("dup", "Same name", Path.Combine(mods, "dup-copy")));
        LoadOrderService service = new LoadOrderService();
        List<string> enabled = PathsOf(service.GetEnabledMods(all, mods));
        List<string> disabled = PathsOf(service.GetDisabledMods(all, mods));
        Random random = new Random(7);
        for (int round = 0; round < 20; round++)
        {
            List<ModItem> shuffled = new List<ModItem>(all);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int k = random.Next(i + 1);
                ModItem swap = shuffled[i];
                shuffled[i] = shuffled[k];
                shuffled[k] = swap;
            }
            Same(enabled, PathsOf(service.GetEnabledMods(shuffled, mods)), "enabled round " + round);
            Same(disabled, PathsOf(service.GetDisabledMods(shuffled, mods)), "disabled round " + round);
        }
    }

    private static List<string> PathsOf(IEnumerable<ModItem> mods)
    {
        List<string> paths = new List<string>();
        foreach (ModItem mod in mods)
            paths.Add(mod.Id + "@" + Path.GetFileName(mod.RootPath));
        return paths;
    }

    private static void DiscoveryIsSorted()
    {
        string mods = NewDir("discovery");
        foreach (string folder in new string[] { "Zeta", "alpha", "Mid", "_smm_backup", "SMM", "disabled" })
            WriteModFolder(Path.Combine(mods, folder), folder.ToLowerInvariant(), folder, "1.0", null);
        Directory.CreateDirectory(Path.Combine(mods, "NoAbout"));
        ModDiscoveryService discovery = new ModDiscoveryService((string)null);
        List<string> first = PathsOf(discovery.DiscoverMods(mods));
        Same(new List<string> { "alpha@alpha", "mid@Mid", "noabout@NoAbout", "zeta@Zeta" }, first, "discovered folders");
        for (int i = 0; i < 5; i++)
            Same(first, PathsOf(discovery.DiscoverMods(mods)), "discovery repeat");
    }

    private static void HarmonyIsBundled()
    {
        foreach (string id in new string[] { "harmony", "0Harmony", "com.harmony.0harmony", " COM.HARMONY.0HARMONY ", "lib.harmony", "brrainz.harmony", "pardeike.harmony", "harmonylib" })
            Check(LoadOrderService.IsBundledDependency(id), id + " should count as bundled");
        foreach (string id in new string[] { null, "", "com.plugin.harmony", "harmonyx", "coolnether123.harmonyexample" })
            Check(!LoadOrderService.IsBundledDependency(id), (id ?? "null") + " should not count as bundled");
    }

    private static void DependencyProblems()
    {
        string mods = NewDir("problems");
        List<ModItem> enabled = new List<ModItem>
        {
            Mod("uses.harmony", "com.harmony.0harmony"),
            Mod("needs.missing", "not.here"),
            Mod("needs.new", "old.lib >= 2.0"),
            Mod("old.lib")
        };
        enabled[3].Version = "1.0";
        File.WriteAllText(Path.Combine(mods, "loadorder.json"), "{\"order\":[\"old.lib\",\"uses.harmony\",\"needs.missing\",\"needs.new\"]}");
        LoadOrderValidation validation = new LoadOrderService().ValidateOrder(enabled, mods, false);
        Dictionary<string, DependencyProblem> byMod = new Dictionary<string, DependencyProblem>();
        foreach (DependencyProblem problem in validation.DependencyProblems)
            byMod[problem.ModId] = problem;
        Check(!byMod.ContainsKey("uses.harmony"), "Harmony was reported missing");
        Check(!validation.HardIssueModIds.Contains("uses.harmony"), "Harmony dependent flagged");
        Check(byMod.ContainsKey("needs.missing") && byMod["needs.missing"].DependencyId == "not.here" && !byMod["needs.missing"].NeedsNewerVersion, "missing dependency");
        Check(byMod.ContainsKey("needs.new") && byMod["needs.new"].DependencyId == "old.lib" && byMod["needs.new"].NeedsNewerVersion, "version too old");
    }

    // ================================================================== mod lists and profiles

    private static List<ModItem> SampleMods()
    {
        ModItem a = new ModItem("coolnether123.lifespan", "Lifespan \u2014 \u00E9dition", "x");
        a.Version = "1.0.4";
        a.NexusGameDomain = "sheltered";
        a.NexusModId = 21;
        ModItem b = new ModItem("tester.local", "Local only", "y");
        b.Version = "0.1";
        return new List<ModItem> { a, b };
    }

    private static void ModListRoundTrip()
    {
        ModListFileService service = new ModListFileService();
        string path = Path.Combine(NewDir("list"), "shared.smmlist");
        service.Save(path, service.Build("Friday", SampleMods()));
        ModListFile list;
        string error;
        Check(service.TryLoad(path, out list, out error), "load failed: " + error);
        Same(new List<string> { "coolnether123.lifespan", "tester.local" }, ModListFileService.OrderOf(list), "order");
        Check(list.name == "Friday", "name");
        Check(list.mods[0].name == "Lifespan \u2014 \u00E9dition" && list.mods[0].version == "1.0.4", "fields");
        Check(list.mods[0].nexusGameDomain == "sheltered" && list.mods[0].nexusModId == 21, "Nexus link");
        Check(list.mods[1].nexusModId == 0 && string.IsNullOrEmpty(list.mods[1].nexusGameDomain), "no Nexus link for a local mod");

        // Saving over an existing file replaces it.
        service.Save(path, service.Build("Friday", new List<ModItem> { SampleMods()[1] }));
        Check(service.TryLoad(path, out list, out error) && list.mods.Length == 1, "overwrite");
        Check(!File.Exists(path + ".tmp"), "temporary file left behind");
    }

    private static void ModListSanitises()
    {
        ModListFileService service = new ModListFileService();
        string path = Path.Combine(NewDir("list"), "odd.smmlist");
        string longName = new string('n', 500);
        File.WriteAllText(path, "\uFEFF{\"format\":\"smm-modlist\",\"version\":1,\"name\":\"" + longName + "\",\"mods\":[" +
            "{\"id\":\" Keep.Me \",\"name\":\"" + longName + "\"}," +
            "{\"id\":\"keep.me\"}," +
            "{\"id\":\"bad id\"},{\"id\":\"..\\\\up\"},{\"id\":\"a/b\"},{\"id\":\"\"},{\"id\":null},null," +
            "{\"id\":\"nexus.ok\",\"nexusGameDomain\":\"Sheltered\",\"nexusModId\":5}," +
            "{\"id\":\"nexus.bad\",\"nexusGameDomain\":\"bad domain!\",\"nexusModId\":5}," +
            "{\"id\":\"nexus.neg\",\"nexusGameDomain\":\"sheltered\",\"nexusModId\":-3}]}", new UTF8Encoding(false));
        ModListFile list;
        string error;
        Check(service.TryLoad(path, out list, out error), "load failed: " + error);
        Same(new List<string> { "keep.me", "nexus.ok", "nexus.bad", "nexus.neg" }, ModListFileService.OrderOf(list), "kept entries");
        Check(list.mods[0].name.Length == 200 && list.name.Length == 80, "long text is capped");
        Check(list.mods[1].nexusGameDomain == "sheltered" && list.mods[1].nexusModId == 5, "valid Nexus link kept (lower-cased)");
        Check(list.mods[2].nexusModId == 0 && list.mods[3].nexusModId == 0, "invalid Nexus links dropped");
    }

    private static void ModListRefusesJunk()
    {
        ModListFileService service = new ModListFileService();
        string folder = NewDir("junk");
        string[] bodies = new string[]
        {
            "not json", "[]", "null", "{}", "{\"format\":\"other\",\"version\":1,\"mods\":[]}",
            "{\"format\":\"smm-modlist\",\"version\":2,\"mods\":[]}", "{\"format\":\"smm-modlist\",\"version\":1}",
            "{\"format\":\"smm-modlist\",\"version\":1,\"mods\":[]} trailing"
        };
        for (int i = 0; i < bodies.Length; i++)
        {
            string path = Path.Combine(folder, "junk" + i + ".smmlist");
            File.WriteAllText(path, bodies[i]);
            ModListFile list;
            string error;
            Check(!service.TryLoad(path, out list, out error) && !string.IsNullOrEmpty(error), "accepted: " + bodies[i]);
        }

        StringBuilder many = new StringBuilder("{\"format\":\"smm-modlist\",\"version\":1,\"mods\":[");
        for (int i = 0; i < 2001; i++)
            many.Append(i == 0 ? "" : ",").Append("{\"id\":\"m").Append(i).Append("\"}");
        many.Append("]}");
        string tooMany = Path.Combine(folder, "many.smmlist");
        File.WriteAllText(tooMany, many.ToString());
        ModListFile ignored;
        string manyError;
        Check(!service.TryLoad(tooMany, out ignored, out manyError), "2001 entries accepted");

        string huge = Path.Combine(folder, "huge.smmlist");
        File.WriteAllText(huge, "{\"format\":\"smm-modlist\",\"version\":1,\"name\":\"" + new string('x', 1024 * 1024) + "\",\"mods\":[]}");
        string hugeError;
        Check(!service.TryLoad(huge, out ignored, out hugeError), "file over 1 MB accepted");

        string missingError;
        Check(!service.TryLoad(Path.Combine(folder, "nope.smmlist"), out ignored, out missingError), "missing file accepted");
    }

    private static void ModListSaveDeterministic()
    {
        ModListFileService service = new ModListFileService();
        string folder = NewDir("same");
        string first = Path.Combine(folder, "a.smmlist");
        string second = Path.Combine(folder, "b.smmlist");
        service.Save(first, service.Build("Same", SampleMods()));
        service.Save(second, service.Build("Same", SampleMods()));
        string pattern = "\"savedUtc\":\"[^\"]*\"";
        string a = System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(first), pattern, string.Empty);
        string b = System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(second), pattern, string.Empty);
        SameText(a, b, "two saves of the same mods differ beyond the timestamp");
        foreach (string culture in Cultures)
        {
            string path = Path.Combine(folder, culture + ".smmlist");
            InCulture(culture, delegate { service.Save(path, service.Build("Same", SampleMods())); return 0; });
            SameText(a, System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(path), pattern, string.Empty), "save in " + culture);
        }
    }

    private static string StableSerialize(object value)
    {
        return (string)Call(ManagerType("ShelteredModManager.ContentPacks.StableJson"), "Serialize", new JavaScriptSerializer(), value);
    }

    private static void StableJsonOrder()
    {
        // Reading types by name, in an unusual key order, then collecting the
        // reflection caches is what made the plain serializer reorder members.
        JavaScriptSerializer reader = new JavaScriptSerializer();
        ModListFile list = new ModListFileService().Build("Same", SampleMods());
        string expected = StableSerialize(list);
        Check(expected.StartsWith("{\"format\":\"smm-modlist\",\"version\":1,\"name\":\"Same\",\"savedUtc\":"), "declaration order: " + expected);
        for (int round = 0; round < 10; round++)
        {
            reader.Deserialize<ModListFile>("{\"mods\":[{\"nexusModId\":1,\"id\":\"a\"}],\"savedUtc\":\"x\",\"version\":1}");
            reader.Deserialize<ContentWorkshopAbout>("{\"tags\":[],\"website\":null,\"id\":\"a\"}");
            GC.Collect();
            GC.WaitForPendingFinalizers();
            SameText(expected, StableSerialize(list), "round " + round);
        }
    }

    private static void StableJsonSameData()
    {
        ContentPackDocument document = new ContentPackDocument();
        document.modId = "tester.pack";
        ContentPackItem item = new ContentPackItem();
        item.id = "tester.pack.widget";
        item.displayName = "Widget \u00E9 \"quoted\" </script>";
        item.burnValue = 1.25f;
        document.items.Add(item);
        ContentWorkshopAbout about = new ContentWorkshopAbout();
        about.id = "tester.pack";
        about.authors = new string[] { "A", "B" };
        Dictionary<string, object> map = new Dictionary<string, object>();
        map["n"] = 3;
        map["list"] = new List<int> { 1, 2 };
        map["when"] = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        map["none"] = null;
        foreach (object value in new object[] { document, about, SampleMods()[0], map, new int[0], "text", 5 })
        {
            JavaScriptSerializer plain = new JavaScriptSerializer();
            object expected = plain.DeserializeObject(plain.Serialize(value));
            object actual = plain.DeserializeObject(StableSerialize(value));
            SameText(Canonical(expected), Canonical(actual), value.GetType().Name);
        }
    }

    /// <summary>JSON values with object keys sorted, so member order does not matter.</summary>
    private static string Canonical(object value)
    {
        IDictionary<string, object> map = value as IDictionary<string, object>;
        if (map != null)
        {
            List<string> keys = new List<string>(map.Keys);
            keys.Sort(StringComparer.Ordinal);
            StringBuilder text = new StringBuilder("{");
            foreach (string key in keys)
                text.Append(key).Append('=').Append(Canonical(map[key])).Append(';');
            return text.Append('}').ToString();
        }
        object[] array = value as object[];
        if (array != null)
        {
            StringBuilder text = new StringBuilder("[");
            foreach (object item in array)
                text.Append(Canonical(item)).Append(',');
            return text.Append(']').ToString();
        }
        return value == null ? "null" : value.GetType().Name + ":" + Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private sealed class Loop
    {
        public Loop Next;
    }

    private static void StableJsonLoops()
    {
        Loop loop = new Loop();
        loop.Next = loop;
        Throws(delegate { StableSerialize(loop); }, "serializing a reference loop");
        List<object> selfList = new List<object>();
        selfList.Add(selfList);
        Throws(delegate { StableSerialize(selfList); }, "serializing a list that contains itself");
    }

    private static void ProfileNameRules()
    {
        string error;
        foreach (string good in new string[] { "My mods", "Hard mode 2", "Co-op", "\u00DCber", "a", new string('x', 40), "CONsole", "Com10" })
            Check(ModListFileService.IsValidProfileName(good, out error), "refused: " + good + " (" + error + ")");
        foreach (string bad in new string[] { null, "", "   ", new string('x', 41), "a/b", "a\\b", "a:b", "a*b", "a?b", "a\"b", "a<b", "a>b", "a|b", ".hidden", "trailing.", "CON", "con", "Nul", "COM1", "lpt9", "aux.backup" })
            Check(!ModListFileService.IsValidProfileName(bad, out error) && !string.IsNullOrEmpty(error), "accepted: " + (bad ?? "null"));

        // A valid name must also be storable as a file.
        ModListFileService service = new ModListFileService();
        string mods = NewDir("profiles");
        foreach (string name in new string[] { "My mods", "\u00DCber", "CONsole" })
        {
            service.SaveProfile(mods, name, SampleMods());
            Check(service.ProfileExists(mods, name), "could not store " + name);
        }
    }

    private static void ProfileListOrder()
    {
        ModListFileService service = new ModListFileService();
        string mods = NewDir("order");
        foreach (string name in new string[] { "zebra", "Item", "item two", "ilk", "\u0130stanbul", "apple" })
            service.SaveProfile(mods, name, SampleMods());
        List<string> expected = service.ListProfiles(mods);
        foreach (string culture in Cultures)
            Same(expected, InCulture(culture, delegate { return service.ListProfiles(mods); }), "profiles in " + culture);
        Check(!Directory.Exists(Path.Combine(mods, "_smm_profiles")) || new ModDiscoveryService((string)null).DiscoverMods(mods).Count == 0, "profiles folder listed as a mod");
    }

    private static void ProfileRename()
    {
        ModListFileService service = new ModListFileService();
        string mods = NewDir("rename");
        string error;
        service.SaveProfile(mods, "my mods", SampleMods());
        service.SetActiveProfile(mods, "my mods");

        Check(service.RenameProfile(mods, "my mods", "My Mods", out error), "case rename failed: " + error);
        Same(new List<string> { "My Mods" }, service.ListProfiles(mods), "file takes the new casing");
        Check(service.GetActiveProfile(mods) == "My Mods", "active profile after case rename");

        Check(service.RenameProfile(mods, "My Mods", "Weekend", out error), "rename failed: " + error);
        Same(new List<string> { "Weekend" }, service.ListProfiles(mods), "old file removed");
        Check(service.GetActiveProfile(mods) == "Weekend", "renaming the active profile keeps it active");

        service.SaveProfile(mods, "Other", SampleMods());
        Check(!service.RenameProfile(mods, "Weekend", "other", out error) && error != null, "rename onto another profile allowed");
        Check(!service.RenameProfile(mods, "Weekend", "CON", out error), "rename to a reserved name allowed");
        Check(!service.RenameProfile(mods, "Missing", "Fresh", out error), "rename of a missing profile succeeded");
        Same(new List<string> { "Other", "Weekend" }, service.ListProfiles(mods), "profiles unchanged by refused renames");
    }

    private static void ActiveProfileSafety()
    {
        ModListFileService service = new ModListFileService();
        string mods = NewDir("active");
        Check(service.GetActiveProfile(mods) == null, "no profiles yet");
        service.SaveProfile(mods, "Real", SampleMods());
        service.SetActiveProfile(mods, "Real");
        Check(service.GetActiveProfile(mods) == "Real", "active");

        string activeFile = Path.Combine(Path.Combine(mods, "_smm_profiles"), "active.txt");
        foreach (string junk in new string[] { "..\\..\\evil", "Ghost", "", "CON", "a/b" })
        {
            File.WriteAllText(activeFile, junk);
            Check(service.GetActiveProfile(mods) == null, "active.txt \"" + junk + "\" was trusted");
        }
        service.SetActiveProfile(mods, "Real");
        service.DeleteProfile(mods, "Real");
        Check(service.GetActiveProfile(mods) == null && !File.Exists(activeFile), "deleting the active profile clears it");
        service.DeleteProfile(mods, "Never existed");
        service.SetActiveProfile(mods, null);
        Check(service.GetActiveProfile(mods) == null, "clearing again is harmless");
    }

    private static void ProfileWriteBoundary()
    {
        ModListFileService service = new ModListFileService();
        string mods = NewDir("profile-write-boundary");
        foreach (string name in new string[] { @"..\outside", "../outside", Path.Combine(mods, "absolute"), null, "", ".hidden" })
        {
            Throws(delegate { service.SaveProfile(mods, name, SampleMods()); }, "invalid profile save " + name);
            Check(!Directory.Exists(Path.Combine(mods, "_smm_profiles")), "invalid save created the profiles folder");
            Check(Directory.GetFiles(mods).Length == 0, "invalid save wrote outside the profiles folder");
        }
        service.SaveProfile(mods, "  Valid profile  ", SampleMods());
        Check(service.ProfileExists(mods, "Valid profile"), "valid trimmed profile no longer saves");
    }

    private static void ProfileReadDeleteBoundary()
    {
        ModListFileService service = new ModListFileService();
        string mods = NewDir("profile-read-boundary");
        service.SaveProfile(mods, "Valid", SampleMods());
        service.SetActiveProfile(mods, "Valid");
        string outside = Path.Combine(mods, "outside.smmlist");
        service.Save(outside, service.Build("Outside sentinel", SampleMods()));
        string before = File.ReadAllText(outside);
        foreach (string name in new string[] { @"..\outside", "../outside", Path.Combine(mods, "outside"), null, "", ".hidden" })
        {
            ModListFile loaded;
            string error;
            Check(!service.TryLoadProfile(mods, name, out loaded, out error) && loaded == null && !string.IsNullOrEmpty(error),
                "invalid profile load accepted " + name);
            Check(!service.ProfileExists(mods, name), "invalid profile exists accepted " + name);
            Throws(delegate { service.DeleteProfile(mods, name); }, "invalid profile delete " + name);
            SameText(before, File.ReadAllText(outside), "outside sentinel after delete");
            Check(service.GetActiveProfile(mods) == "Valid", "invalid delete changed active profile");
        }
    }

    private static void ProfileRenameActiveBoundary()
    {
        ModListFileService service = new ModListFileService();
        string mods = NewDir("profile-rename-boundary");
        service.SaveProfile(mods, "Valid", SampleMods());
        service.SetActiveProfile(mods, "Valid");
        string outside = Path.Combine(mods, "outside.smmlist");
        service.Save(outside, service.Build("Outside sentinel", SampleMods()));
        string before = File.ReadAllText(outside);
        string error;
        Check(!service.RenameProfile(mods, @"..\outside", "Renamed", out error) && !string.IsNullOrEmpty(error), "rename moved an outside file");
        SameText(before, File.ReadAllText(outside), "outside sentinel after rename");
        Check(!service.ProfileExists(mods, "Renamed"), "invalid rename created a profile");
        Throws(delegate { service.SetActiveProfile(mods, @"..\outside"); }, "invalid active profile");
        Check(service.GetActiveProfile(mods) == "Valid", "invalid activation replaced the active profile");
    }

    // ================================================================== ZIP writing

    private static void ZipDeterministic()
    {
        string folder = NewDir("zip");
        string text = BigText(4000);
        List<string> hashes = new List<string>();
        foreach (string culture in Cultures)
        {
            string path = Path.Combine(folder, culture + ".zip");
            InCulture(culture, delegate { WriteZip(path, true, "logs/a.log", text, "b.txt", "short", "\u00FCnic\u00F8de/\u6587.txt", "x"); return 0; });
            hashes.Add(Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(path))));
        }
        for (int i = 1; i < hashes.Count; i++)
            Check(hashes[i] == hashes[0], "zip bytes differ in " + Cultures[i]);
    }

    private static string BigText(int lines)
    {
        StringBuilder text = new StringBuilder();
        for (int i = 0; i < lines; i++)
            text.Append("[12:00:").Append(i % 60).Append("] [INFO ] line ").Append(i).Append(" of a long session log\r\n");
        return text.ToString();
    }

    private static void ZipRoundTrip()
    {
        string folder = NewDir("roundtrip");
        byte[] random = new byte[50000];
        new Random(3).NextBytes(random);
        byte[] text = Encoding.UTF8.GetBytes(BigText(3000));
        string[] names = new string[] { "empty.txt", "folder\\back\\slash.txt", "\u00FCnic\u00F8de \u6587.txt", "random.bin", "text.log" };
        byte[][] data = new byte[][] { new byte[0], Encoding.UTF8.GetBytes("slash"), Encoding.UTF8.GetBytes("unicode"), random, text };
        foreach (bool compress in new bool[] { false, true })
        {
            string path = Path.Combine(folder, (compress ? "deflated" : "stored") + ".zip");
            WriteZipBytes(path, compress, names, data);
            Dictionary<string, byte[]> files = ReadZip(path);
            Check(files.Count == names.Length, "entry count " + files.Count);
            Check(files["folder/back/slash.txt"].Length == 5, "backslashes become folders");
            Check(Convert.ToBase64String(files["random.bin"]) == Convert.ToBase64String(random), "random bytes");
            Check(Convert.ToBase64String(files["text.log"]) == Convert.ToBase64String(text), "text bytes");
            Check(files["empty.txt"].Length == 0, "empty entry");
            if (compress)
                Check(new FileInfo(path).Length < text.Length / 4 + random.Length + 2000, "text was not compressed");
        }

        string empty = Path.Combine(folder, "none.zip");
        WriteZipBytes(empty, true, new string[0], new byte[0][]);
        Check(new FileInfo(empty).Length == 22 && ZipEntryNames(empty).Count == 0, "an empty zip is just the end record");
    }

    private static void ZipCrc()
    {
        uint crc = (uint)Call(ManagerType("Manager.Core.Services.ZipFileWriter"), "Crc32", Encoding.ASCII.GetBytes("123456789"));
        Check(crc == 0xCBF43926u, "CRC-32 of \"123456789\" was " + crc.ToString("X8"));
        Check((uint)Call(ManagerType("Manager.Core.Services.ZipFileWriter"), "Crc32", new byte[0]) == 0u, "CRC of nothing");
    }

    private static void ZipEntryLimit()
    {
        string path = Path.Combine(NewDir("limit"), "too-many.zip");
        string[] names = new string[65536];
        byte[][] data = new byte[65536][];
        for (int i = 0; i < names.Length; i++)
        {
            names[i] = "f" + i;
            data[i] = new byte[0];
        }
        Throws(delegate { WriteZipBytes(path, false, names, data); }, "writing 65536 entries");
    }

    // ================================================================== local installs

    private static NexusInstallService Installer()
    {
        return new NexusInstallService();
    }

    private static NexusInstallResult Install(string package, string mods, bool replace, out string modId, out string error)
    {
        return Installer().InstallLocalPackage(package, mods, delegate(string name, string path) { return replace; }, out modId, out error);
    }

    private static void InstallRootZip()
    {
        string mods = NewDir("mods");
        string zip = Path.Combine(_root, "root-" + Guid.NewGuid().ToString("N") + ".zip");
        WriteZip(zip, true, "About/About.json", AboutJson("tester.rootmod", "Root Mod", "1.0.0"), "Assemblies/readme.txt", "x");
        string modId;
        string error;
        NexusInstallResult result = Install(zip, mods, false, out modId, out error);
        Check(result != null && error == null, "install failed: " + error);
        Check(modId == "tester.rootmod", "mod id " + modId);
        Check(File.Exists(Path.Combine(Path.Combine(mods, "tester.rootmod"), "Assemblies\\readme.txt")), "installed under its ID");
    }

    private static void InstallWrappedZip()
    {
        string mods = NewDir("mods");
        string zip = Path.Combine(_root, "wrapped-" + Guid.NewGuid().ToString("N") + ".zip");
        WriteZip(zip, true, "Download v1.2/Better Beds/About/About.json", AboutJson("tester.beds", "Better Beds", "1.2"), "Download v1.2/readme.txt", "notes");
        string modId;
        string error;
        Check(Install(zip, mods, false, out modId, out error) != null, "install failed: " + error);
        Check(Directory.Exists(Path.Combine(mods, "Better Beds")), "the mod folder, not the wrapper, is installed");
        Check(!Directory.Exists(Path.Combine(mods, "Download v1.2")), "wrapper installed");
    }

    private static void InstallAmbiguousZipDeterministic()
    {
        string zip = Path.Combine(_root, "two-" + Guid.NewGuid().ToString("N") + ".zip");
        WriteZip(zip, true,
            "Zeta Mod/About/About.json", AboutJson("tester.zeta", "Zeta", "1.0"),
            "Alpha Mod/About/About.json", AboutJson("tester.alpha", "Alpha", "1.0"));
        for (int i = 0; i < 3; i++)
        {
            string mods = NewDir("mods");
            string modId;
            string error;
            Check(Install(zip, mods, false, out modId, out error) != null, "install failed: " + error);
            Check(modId == "tester.alpha", "run " + i + " installed " + modId);
        }
    }

    private static void InstallRefusesBadArchives()
    {
        string mods = NewDir("mods");
        string folder = NewDir("bad");
        string modId;
        string error;

        string slip = Path.Combine(folder, "slip.zip");
        WriteZip(slip, false, "../../escaped.txt", "evil", "Mod/About/About.json", AboutJson("tester.slip", "Slip", "1.0"));
        Check(Install(slip, mods, false, out modId, out error) == null && error != null, "path traversal accepted");
        Check(!File.Exists(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(mods)), "escaped.txt")), "file escaped the extract folder");

        string absolute = Path.Combine(folder, "absolute.zip");
        WriteZip(absolute, false, "C:/Windows/evil.txt", "evil");
        Check(Install(absolute, mods, false, out modId, out error) == null && error != null, "absolute path accepted");

        string empty = Path.Combine(folder, "empty.zip");
        WriteZipBytes(empty, false, new string[0], new byte[0][]);
        Check(Install(empty, mods, false, out modId, out error) == null && error != null, "empty zip accepted");

        string fake = Path.Combine(folder, "fake.zip");
        File.WriteAllText(fake, "this is not a zip");
        Check(Install(fake, mods, false, out modId, out error) == null && error != null && error.IndexOf(".zip") >= 0, "fake zip accepted: " + error);

        string truncated = Path.Combine(folder, "truncated.zip");
        WriteZip(truncated, true, "Mod/About/About.json", AboutJson("tester.cut", "Cut", "1.0"));
        byte[] whole = File.ReadAllBytes(truncated);
        byte[] half = new byte[whole.Length / 2];
        Array.Copy(whole, half, half.Length);
        File.WriteAllBytes(truncated, half);
        Check(Install(truncated, mods, false, out modId, out error) == null && error != null, "truncated zip accepted");

        string noMod = Path.Combine(folder, "nomod.zip");
        WriteZip(noMod, false, "notes.txt", "hello");
        Check(Install(noMod, mods, false, out modId, out error) == null && error.IndexOf("About.json") >= 0, "zip without a mod accepted");

        string tooMany = Path.Combine(folder, "bomb.zip");
        string[] names = new string[20001];
        byte[][] data = new byte[20001][];
        for (int i = 0; i < names.Length; i++)
        {
            names[i] = "Mod/f" + i + ".txt";
            data[i] = new byte[0];
        }
        names[0] = "Mod/About/About.json";
        data[0] = Encoding.UTF8.GetBytes(AboutJson("tester.bomb", "Bomb", "1.0"));
        WriteZipBytes(tooMany, false, names, data);
        Check(Install(tooMany, mods, false, out modId, out error) == null && error != null && error.IndexOf("too many entries") >= 0, "20001-entry zip: " + error);

        string missing = Path.Combine(folder, "gone.zip");
        Check(Install(missing, mods, false, out modId, out error) == null && error != null, "missing file accepted");
        Check(new ModDiscoveryService((string)null).DiscoverMods(mods).Count == 0, "refused archives left files in the mods folder");
    }

    private static void InstallRefusesSizeBombs()
    {
        string mods = NewDir("mods");
        string folder = NewDir("bombs");
        string modId;
        string error;

        // One entry that says it expands to 600 MB (the limit is 512 MB per entry).
        string single = Path.Combine(folder, "single.zip");
        WriteZip(single, true, "Mod/About/About.json", AboutJson("tester.bomb", "Bomb", "1.0"), "Mod/huge.bin", "small");
        DeclareSize(single, "Mod/huge.bin", 600u * 1024 * 1024);
        Check(Install(single, mods, false, out modId, out error) == null && error != null && error.IndexOf("expanded size") >= 0, "600 MB entry: " + error);

        // Five entries of 500 MB each: every one is allowed, the 2.5 GB total is not.
        string total = Path.Combine(folder, "total.zip");
        WriteZip(total, true, "Mod/About/About.json", AboutJson("tester.bomb", "Bomb", "1.0"),
            "Mod/a.bin", "a", "Mod/b.bin", "b", "Mod/c.bin", "c", "Mod/d.bin", "d", "Mod/e.bin", "e");
        foreach (string name in new string[] { "Mod/a.bin", "Mod/b.bin", "Mod/c.bin", "Mod/d.bin", "Mod/e.bin" })
            DeclareSize(total, name, 500u * 1024 * 1024);
        Check(Install(total, mods, false, out modId, out error) == null && error != null && error.IndexOf("total expanded size") >= 0, "2.5 GB total: " + error);

        // A size that is wrong but small is caught while extracting.
        string lying = Path.Combine(folder, "lying.zip");
        WriteZip(lying, true, "Mod/About/About.json", AboutJson("tester.liar", "Liar", "1.0"), "Mod/data.bin", "twelve bytes");
        DeclareSize(lying, "Mod/data.bin", 4);
        Check(Install(lying, mods, false, out modId, out error) == null && error != null, "an entry larger than it claims was accepted");
        Check(Directory.GetDirectories(mods).Length == 0, "refused archives left folders in the mods folder");
    }

    /// <summary>Rewrites an entry's declared expanded size in the central directory.</summary>
    private static void DeclareSize(string zipPath, string entryName, uint size)
    {
        byte[] bytes = File.ReadAllBytes(zipPath);
        int end = -1;
        for (int i = bytes.Length - 22; i >= 0; i--)
        {
            if (BitConverter.ToUInt32(bytes, i) == 0x06054b50u) { end = i; break; }
        }
        int count = BitConverter.ToUInt16(bytes, end + 10);
        int offset = (int)BitConverter.ToUInt32(bytes, end + 16);
        bool found = false;
        for (int n = 0; n < count; n++)
        {
            int nameLength = BitConverter.ToUInt16(bytes, offset + 28);
            int extra = BitConverter.ToUInt16(bytes, offset + 30);
            int comment = BitConverter.ToUInt16(bytes, offset + 32);
            if (Encoding.UTF8.GetString(bytes, offset + 46, nameLength) == entryName)
            {
                Array.Copy(BitConverter.GetBytes(size), 0, bytes, offset + 24, 4);
                found = true;
            }
            offset += 46 + nameLength + extra + comment;
        }
        Check(found, "no entry " + entryName + " in " + zipPath);
        File.WriteAllBytes(zipPath, bytes);
    }

    private static void InstallRefusesBadMods()
    {
        string mods = NewDir("mods");
        string folder = NewDir("badmods");
        string modId;
        string error;
        string[][] abouts = new string[][]
        {
            new string[] { "{\"name\":\"No id\",\"version\":\"1.0\"}", "missing id" },
            new string[] { "{\"id\":\"tester.noversion\",\"name\":\"No version\"}", "missing version" },
            new string[] { "{\"id\":\"tester.noname\",\"version\":\"1.0\"}", "missing name" },
            new string[] { "{ broken json", "unreadable About.json" }
        };
        for (int i = 0; i < abouts.Length; i++)
        {
            string zip = Path.Combine(folder, "bad" + i + ".zip");
            WriteZip(zip, false, "Mod/About/About.json", abouts[i][0]);
            Check(Install(zip, mods, false, out modId, out error) == null && error != null, abouts[i][1] + " accepted");
        }
        foreach (string reserved in new string[] { "smm", "disabled", "modapi", "_smm_backup" })
        {
            string zip = Path.Combine(folder, "reserved-" + reserved + ".zip");
            WriteZip(zip, false, "About/About.json", AboutJson(reserved, "Reserved", "1.0"));
            Check(Install(zip, mods, false, out modId, out error) == null && error != null, "reserved folder " + reserved + " accepted");
        }
        Check(new ModDiscoveryService((string)null).DiscoverMods(mods).Count == 0, "bad mods left files behind");
    }

    private static void InstallRefusesInsideModsFolder()
    {
        string mods = NewDir("mods");
        string inside = Path.Combine(mods, "Already Here");
        WriteModFolder(inside, "tester.here", "Here", "1.0", null);
        string modId;
        string error;
        Check(Install(inside, mods, true, out modId, out error) == null && error != null, "a mod folder inside the mods folder was installed again");
        Check(Install(mods, mods, true, out modId, out error) == null && error != null, "the mods folder itself was installed");
        Check(Directory.GetDirectories(mods).Length == 1, "folders changed");
    }

    private static void InstallReplace()
    {
        string mods = NewDir("mods");
        string modId;
        string error;
        string v1 = Path.Combine(_root, "v1-" + Guid.NewGuid().ToString("N") + ".zip");
        string v2 = Path.Combine(_root, "v2-" + Guid.NewGuid().ToString("N") + ".zip");
        WriteZip(v1, true, "Beds/About/About.json", AboutJson("tester.beds", "Beds", "1.0"), "Beds/old.txt", "old");
        WriteZip(v2, true, "Beds Renamed/About/About.json", AboutJson("tester.beds", "Beds", "2.0"), "Beds Renamed/new.txt", "new");
        Check(Install(v1, mods, false, out modId, out error) != null, "first install failed: " + error);

        bool asked = false;
        NexusInstallResult declined = Installer().InstallLocalPackage(v2, mods, delegate(string name, string path) { asked = true; return false; }, out modId, out error);
        Check(asked && declined == null && error == null, "declining should cancel quietly");
        Check(File.Exists(Path.Combine(Path.Combine(mods, "Beds"), "old.txt")), "declined replace changed files");

        NexusInstallResult replaced = Install(v2, mods, true, out modId, out error);
        Check(replaced != null, "replace failed: " + error);
        string target = Path.Combine(mods, "Beds");
        Check(File.Exists(Path.Combine(target, "new.txt")) && !File.Exists(Path.Combine(target, "old.txt")), "the installed folder was replaced in place");
        Check(!Directory.Exists(Path.Combine(mods, "Beds Renamed")), "a second copy was made");
        Check(replaced.BackupPath != null && File.Exists(Path.Combine(replaced.BackupPath, "old.txt")), "no backup of the old version");
        Check(new ModDiscoveryService((string)null).DiscoverMods(mods).Count == 1, "backup shows up as a mod");
    }

    private static void InstallFolderCollision()
    {
        string mods = NewDir("mods");
        WriteModFolder(Path.Combine(mods, "Beds"), "someone.else", "Someone else's beds", "1.0", null);
        string zip = Path.Combine(_root, "collide-" + Guid.NewGuid().ToString("N") + ".zip");
        WriteZip(zip, false, "Beds/About/About.json", AboutJson("tester.beds", "Beds", "1.0"));
        string modId;
        string error;
        Check(Install(zip, mods, true, out modId, out error) == null && error != null, "overwrote a different mod's folder");
        Check(new ModDiscoveryService((string)null).DiscoverMods(mods)[0].Id == "someone.else", "the other mod changed");
    }

    private static void InstallRollbackOnLockedFile()
    {
        string mods = NewDir("mods");
        string modId;
        string error;
        string v1 = Path.Combine(_root, "lock1-" + Guid.NewGuid().ToString("N") + ".zip");
        string v2 = Path.Combine(_root, "lock2-" + Guid.NewGuid().ToString("N") + ".zip");
        WriteZip(v1, false, "Locked/About/About.json", AboutJson("tester.locked", "Locked", "1.0"), "Locked/data.txt", "original");
        WriteZip(v2, false, "Locked/About/About.json", AboutJson("tester.locked", "Locked", "2.0"), "Locked/data.txt", "replacement");
        Check(Install(v1, mods, false, out modId, out error) != null, "first install failed: " + error);
        string data = Path.Combine(Path.Combine(mods, "Locked"), "data.txt");
        using (FileStream hold = new FileStream(data, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Check(Install(v2, mods, true, out modId, out error) == null && error != null, "replace succeeded while a file was locked");
        }
        Check(File.ReadAllText(data) == "original", "the installed mod was damaged by the failed replace");
        Check(new ModDiscoveryService((string)null).DiscoverMods(mods)[0].Version == "1.0", "version changed");
    }

    private static void InstallIdempotent()
    {
        string mods = NewDir("mods");
        string zip = Path.Combine(_root, "same-" + Guid.NewGuid().ToString("N") + ".zip");
        WriteZip(zip, true, "Same/About/About.json", AboutJson("tester.same", "Same", "1.0"), "Same/a/b/c.txt", "content");
        string modId;
        string error;
        Check(Install(zip, mods, true, out modId, out error) != null, "first: " + error);
        string first = TreeSignature(Path.Combine(mods, "Same"));
        Check(Install(zip, mods, true, out modId, out error) != null, "second: " + error);
        Check(TreeSignature(Path.Combine(mods, "Same")) == first, "reinstalling changed the files");
    }

    private static string TreeSignature(string folder)
    {
        List<string> parts = new List<string>();
        string prefix = Path.GetFullPath(folder).TrimEnd('\\') + "\\";
        foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            parts.Add(Path.GetFullPath(file).Substring(prefix.Length) + "=" + Convert.ToBase64String(File.ReadAllBytes(file)));
        parts.Sort(StringComparer.Ordinal);
        return string.Join("|", parts.ToArray());
    }

    private static void InstallUnicodeFolder()
    {
        string mods = NewDir("mods");
        string zip = Path.Combine(_root, "unicode-" + Guid.NewGuid().ToString("N") + ".zip");
        WriteZip(zip, true, "\u041C\u043E\u0434 \u6587/About/About.json", AboutJson("tester.unicode", "\u041C\u043E\u0434", "1.0"));
        string modId;
        string error;
        Check(Install(zip, mods, false, out modId, out error) != null, "install failed: " + error);
        Check(Directory.Exists(Path.Combine(mods, "\u041C\u043E\u0434 \u6587")), "unicode folder name");
    }

    // ================================================================== crash reports

    private static string MakeCrashFolder(string gameDir, string name, bool errorLog, bool dump, DateTime createdUtc)
    {
        string folder = Path.Combine(gameDir, name);
        Directory.CreateDirectory(folder);
        if (errorLog) File.WriteAllText(Path.Combine(folder, "error.log"), "Crash in " + name);
        if (dump) File.WriteAllBytes(Path.Combine(folder, "crash.dmp"), new byte[1024]);
        Directory.SetCreationTimeUtc(folder, createdUtc);
        return folder;
    }

    private static void CrashFoldersFound()
    {
        string game = NewDir("game");
        DateTime baseTime = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
        string good = MakeCrashFolder(game, "2026-05-01_120500", true, false, baseTime.AddMinutes(5));
        string dumpOnly = MakeCrashFolder(game, "2026-05-01_120600", false, true, baseTime.AddMinutes(6));
        MakeCrashFolder(game, "2026-05-01_120700", false, false, baseTime.AddMinutes(7));
        MakeCrashFolder(game, "2026-5-1_120800", true, false, baseTime.AddMinutes(8));
        MakeCrashFolder(game, "crash-2026-05-01", true, false, baseTime.AddMinutes(8));
        MakeCrashFolder(game, "2026-05-01_120900x", true, false, baseTime.AddMinutes(9));
        CrashReportService crashes = new CrashReportService();
        Same(new List<string> { dumpOnly, good }, crashes.FindCrashFolders(game, baseTime), "valid crash folders");
        Same(new List<string> { dumpOnly }, crashes.FindCrashFolders(game, baseTime.AddMinutes(5)), "the boundary is exclusive");
        Check(crashes.FindCrashFolders(game, baseTime.AddHours(1)).Count == 0, "future boundary");
        Check(crashes.FindCrashFolders(Path.Combine(game, "missing"), baseTime).Count == 0, "missing game folder");
        Check(crashes.FindCrashFolders(null, baseTime).Count == 0, "null game folder");
    }

    private static void CrashFoldersOrder()
    {
        string game = NewDir("game");
        DateTime same = new DateTime(2026, 5, 2, 8, 0, 0, DateTimeKind.Utc);
        string b = MakeCrashFolder(game, "2026-05-02_080002", true, false, same);
        string a = MakeCrashFolder(game, "2026-05-02_080001", true, false, same);
        string newest = MakeCrashFolder(game, "2026-05-02_090000", true, false, same.AddHours(1));
        List<string> expected = new List<string> { newest, a, b };
        for (int i = 0; i < 5; i++)
            Same(expected, new CrashReportService().FindCrashFolders(game, same.AddDays(-1)), "crash order");
    }

    private static void CrashFoldersAppInfo()
    {
        string game = NewDir("game");
        string data = Path.Combine(game, "ShelteredWindows64_EOS_Data");
        Directory.CreateDirectory(data);
        string company = "SmmTest" + Guid.NewGuid().ToString("N").Substring(0, 8);
        string crashesRoot = Path.Combine(Path.Combine(Path.Combine(Path.GetTempPath(), company), "Sheltered Test"), "Crashes");
        try
        {
            File.WriteAllText(Path.Combine(data, "app.info"), company + "\r\nSheltered Test\r\n");
            string crash = Path.Combine(crashesRoot, "Crash_2026-05-03_101010");
            Directory.CreateDirectory(crash);
            File.WriteAllText(Path.Combine(crash, "error.log"), "epic crash");
            Directory.CreateDirectory(Path.Combine(crashesRoot, "NotACrash"));
            File.WriteAllText(Path.Combine(Path.Combine(crashesRoot, "NotACrash"), "error.log"), "x");
            DateTime started = DateTime.UtcNow;
            List<string> found = new CrashReportService().FindCrashFolders(game, DateTime.UtcNow.AddMinutes(-5));
            Check((DateTime.UtcNow - started).TotalMilliseconds < 1000, "lookup took too long");
            Same(new List<string> { crash }, found, "app.info crash folder");

            File.WriteAllText(Path.Combine(data, "app.info"), "only one line");
            Check(new CrashReportService().FindCrashFolders(game, DateTime.UtcNow.AddMinutes(-5)).Count == 0, "incomplete app.info");
            File.WriteAllText(Path.Combine(data, "app.info"), "bad|name\r\nx<y");
            Check(new CrashReportService().FindCrashFolders(game, DateTime.UtcNow.AddMinutes(-5)).Count == 0, "invalid characters in app.info");
        }
        finally
        {
            TryDeleteDirectory(Path.Combine(Path.GetTempPath(), company));
        }
    }

    private static string CreateReport(string game, string crash, bool includeDump, string output, DateTime from, DateTime to)
    {
        string error;
        string zip = new CrashReportService().CreateReport(game, crash, from, to, includeDump, "Summary for tests", output, out error);
        Check(zip != null, "report failed: " + error);
        return zip;
    }

    private static void CrashReportContents()
    {
        string game = NewDir("game");
        string profile = Environment.GetEnvironmentVariable("USERPROFILE");
        string crash = MakeCrashFolder(game, "2026-05-04_101010", true, true, DateTime.UtcNow);
        File.WriteAllText(Path.Combine(crash, "error.log"), "Access violation in " + profile + "\\Game\\x.dll and " + profile.ToUpperInvariant() + " and " + profile.Replace('\\', '/') + "/y");
        File.WriteAllText(Path.Combine(crash, "output_log.txt"), "player log");
        Directory.CreateDirectory(Path.Combine(game, "SMM"));
        File.WriteAllText(Path.Combine(Path.Combine(game, "SMM"), "mod_manager.log"), "mods from " + profile + "\\mods");
        foreach (string dataFolder in new string[] { "Sheltered_Data", "Other_Data" })
        {
            Directory.CreateDirectory(Path.Combine(game, dataFolder));
            File.WriteAllText(Path.Combine(Path.Combine(game, dataFolder), "output_log.txt"), dataFolder + " log");
        }
        string output = NewDir("reports");
        DateTime from = DateTime.UtcNow.AddMinutes(-10);
        DateTime to = DateTime.UtcNow.AddMinutes(1);

        string without = CreateReport(game, crash, false, output, from, to);
        Same(new List<string> { "session-info.txt", "crash/error.log", "crash/output_log.txt", "game/output_log.txt", "game/output_log (2).txt", "smm/mod_manager.log" },
            ZipEntryNames(without), "report entries");
        Dictionary<string, byte[]> files = ReadZip(without);
        string errorLog = Encoding.UTF8.GetString(files["crash/error.log"]);
        Check(errorLog.IndexOf(profile, StringComparison.OrdinalIgnoreCase) < 0 && errorLog.IndexOf(profile.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase) < 0, "profile path leaked: " + errorLog);
        Check(errorLog.Split(new string[] { "%USERPROFILE%" }, StringSplitOptions.None).Length == 4, "every profile path is replaced once");
        Check(Encoding.UTF8.GetString(files["game/output_log.txt"]) == "Other_Data log", "data folders are read in path order");
        Check(Encoding.UTF8.GetString(files["session-info.txt"]).IndexOf("Crash dump included: no") >= 0, "session info");

        string with = CreateReport(game, crash, true, output, from, to);
        Check(ZipEntryNames(with).Contains("crash/crash.dmp"), "dump included when asked");
        Check(ReadZip(with)["crash/crash.dmp"].Length == 1024, "dump is not altered");

        // An oversized dump is left out, not cut, and the report says so.
        using (FileStream big = new FileStream(Path.Combine(crash, "crash.dmp"), FileMode.Create))
            big.SetLength(65L * 1024 * 1024);
        string oversized = CreateReport(game, crash, true, output, from, to);
        Check(!ZipEntryNames(oversized).Contains("crash/crash.dmp"), "oversized dump included");
        Check(Encoding.UTF8.GetString(ReadZip(oversized)["session-info.txt"]).IndexOf("crash.dmp was left out") >= 0, "oversized dump not explained");
    }

    private static void CrashReportDifficultFiles()
    {
        string game = NewDir("game");
        string smm = Path.Combine(game, "SMM");
        Directory.CreateDirectory(smm);
        string log = Path.Combine(smm, "mod_manager.log");
        StringBuilder huge = new StringBuilder();
        while (huge.Length < 9 * 1024 * 1024)
            huge.Append("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcde\n");
        huge.Append("LAST LINE");
        File.WriteAllText(log, huge.ToString());
        DateTime from = DateTime.UtcNow.AddMinutes(-10);
        DateTime to = DateTime.UtcNow.AddMinutes(1);
        string output = NewDir("reports");

        string tail = CreateReport(game, null, false, output, from, to);
        byte[] kept = ReadZip(tail)["smm/mod_manager.log"];
        Check(kept.Length == 8 * 1024 * 1024 && Encoding.UTF8.GetString(kept).EndsWith("LAST LINE"), "a large log keeps its last 8 MB");

        // Shared-read handles (the game writing) are fine; an exclusive lock is skipped, not fatal.
        using (FileStream writer = new FileStream(log, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            Check(ZipEntryNames(CreateReport(game, null, false, output, from, to)).Contains("smm/mod_manager.log"), "open log was not read");
        using (FileStream exclusive = new FileStream(log, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Check(!ZipEntryNames(CreateReport(game, null, false, output, from, to)).Contains("smm/mod_manager.log"), "locked log included");

        // Only the three newest rolled logs inside the window are included.
        for (int i = 0; i < 5; i++)
        {
            string rolled = Path.Combine(smm, "mod_manager_2026050" + i + "-000000.log");
            File.WriteAllText(rolled, "rolled " + i);
            File.SetLastWriteTimeUtc(rolled, DateTime.UtcNow.AddMinutes(-5 + i));
        }
        string outside = Path.Combine(smm, "mod_manager_20250101-000000.log");
        File.WriteAllText(outside, "old");
        File.SetLastWriteTimeUtc(outside, DateTime.UtcNow.AddDays(-30));
        List<string> names = ZipEntryNames(CreateReport(game, null, false, output, from, to));
        Check(names.Contains("smm/mod_manager_20260504-000000.log") && names.Contains("smm/mod_manager_20260503-000000.log") && names.Contains("smm/mod_manager_20260502-000000.log"), "newest rolled logs");
        Check(!names.Contains("smm/mod_manager_20260501-000000.log") && !names.Contains("smm/mod_manager_20250101-000000.log"), "too many or too old rolled logs");

        string empty = CreateReport(Path.Combine(game, "missing"), Path.Combine(game, "no-crash"), true, output, from, to);
        Same(new List<string> { "session-info.txt" }, ZipEntryNames(empty), "nothing to collect still makes a report");
    }

    private static void CrashReportNaming()
    {
        string output = NewDir("reports");
        List<string> made = new List<string>();
        for (int i = 0; i < 3; i++)
            made.Add(Path.GetFileName(CreateReport(null, null, false, output, DateTime.UtcNow, DateTime.UtcNow)));
        Check(made[0] != made[1] && made[1] != made[2] && made[0] != made[2], "reports in the same second overwrote each other");
        Check(Directory.GetFiles(output, "*.zip").Length == 3, "report count");

        string arabic = InCulture("ar-SA", delegate { return Path.GetFileName(CreateReport(null, null, false, NewDir("ar"), DateTime.UtcNow, DateTime.UtcNow)); });
        Check(arabic.StartsWith("Sheltered crash " + DateTime.Now.Year.ToString(CultureInfo.InvariantCulture) + "-"), "report name used another calendar: " + arabic);
    }

    private static void CrashReportDeterministic()
    {
        string game = NewDir("game");
        string crash = MakeCrashFolder(game, "2026-05-05_111111", true, false, DateTime.UtcNow);
        foreach (string name in new string[] { "z.txt", "a.txt", "M.log" })
            File.WriteAllText(Path.Combine(crash, name), name);
        DateTime from = DateTime.UtcNow.AddMinutes(-10);
        DateTime to = DateTime.UtcNow.AddMinutes(1);
        string first = null;
        foreach (string culture in Cultures)
        {
            string zip = InCulture(culture, delegate { return CreateReport(game, crash, false, NewDir("det"), from, to); });
            Dictionary<string, byte[]> files = ReadZip(zip);
            List<string> parts = new List<string>();
            foreach (string entry in ZipEntryNames(zip))
                parts.Add(entry + "=" + (entry == "session-info.txt" ? "-" : Convert.ToBase64String(files[entry])));
            string signature = string.Join("|", parts.ToArray());
            if (first == null)
                first = signature;
            Check(signature == first, "report differs in " + culture);
        }
        Check(first.IndexOf("crash/a.txt") < first.IndexOf("crash/error.log") && first.IndexOf("crash/M.log") < first.IndexOf("crash/z.txt"), "crash files not in path order");
    }

    // ================================================================== workshop IDs

    private static void WorkshopImportTarget()
    {
        string root = NewDir("icon-target");
        ContentWorkshopProjectService service = new ContentWorkshopProjectService();
        ContentWorkshopProject project = service.Create(root, "tester.icon", "Icon targets");
        string source = Path.Combine(root, "blue.png");
        using (System.Drawing.Bitmap image = new System.Drawing.Bitmap(1, 1))
        {
            image.SetPixel(0, 0, System.Drawing.Color.Blue);
            image.Save(source, System.Drawing.Imaging.ImageFormat.Png);
        }
        string originalRelative;
        Check(service.ImportIcon(project, source, "original-a", out originalRelative).Success, "initial A icon");
        string originalPath = Path.Combine(root, originalRelative);
        string originalBytes = Convert.ToBase64String(File.ReadAllBytes(originalPath));
        project.Content.items.Add(new ContentPackItem { id = "tester.icon.a", displayName = "A", iconPath = originalRelative });
        project.Content.items.Add(new ContentPackItem { id = "tester.icon.b", displayName = "B" });
        using (ContentWorkshopTab tab = new ContentWorkshopTab())
        {
            SetField(tab, "_project", project);
            Call(tab, "RefreshProject");
            ListBox items = (ListBox)Field(tab, "_itemList");
            ComboBox targets = (ComboBox)Field(tab, "_iconItemPicker");
            Check(items.SelectedIndex == 0 && targets.SelectedIndex == 0, "initial item selection");
            object canvas = Field(tab, "_pixelCanvas");
            Call(canvas, "CreateDocument", 1, 1);
            items.SelectedIndex = 1;
            Check(targets.SelectedIndex == 0, "switching items discarded the dirty canvas target");

            if (typeof(ContentWorkshopTab).GetMethod("ImportIconFromFile", AnyMember) != null)
                Call(tab, "ImportIconFromFile", source);
            else
            {
                // The old accepted-file path, without opening a dialog. This
                // preserves its operation order so the saved baseline reproduces the stale target.
                Call(tab, "CommitSelectedItem");
                string relative;
                ContentWorkshopOperationResult imported = service.ImportIcon(project, source, "blue", out relative);
                Check(imported.Success, "accepted icon import");
                project.Content.items[1].iconPath = relative;
                Call(tab, "MarkDirty");
                SetField(tab, "_loadedItemIndex", -1);
                Call(tab, "LoadSelectedItem");
                object[] args = new object[] { imported.Path, null };
                Check((bool)Call(canvas, "LoadPng", args), "load imported PNG");
                Call(canvas, "FitZoomToClient");
                Call(tab, "UpdatePixelStatus");
            }
            // A new edit followed by Save icon must write B, never the formerly dirty A.
            Call(canvas, "CreateDocument", 1, 1);
            Check((bool)Call(tab, "SaveIconToSelectedItem", false), "saving the edited import");
            Check(project.Content.items[0].iconPath == originalRelative && Convert.ToBase64String(File.ReadAllBytes(originalPath)) == originalBytes,
                "saving B's imported image changed A's icon");
            using (System.Drawing.Bitmap image = new System.Drawing.Bitmap(Path.Combine(root, project.Content.items[1].iconPath)))
                Check(image.GetPixel(0, 0).A == 0, "the edited pixels were not saved to B");
            Check(items.SelectedIndex == 1 && targets.SelectedIndex == 1, "the imported item was not the save target");
        }
    }

    private static void WorkshopRejectedIcon()
    {
        string root = NewDir("icon-rejection");
        string asset = Path.Combine(root, "Assets/Icons/noise.png");
        Directory.CreateDirectory(Path.GetDirectoryName(asset));
        using (System.Drawing.Bitmap original = new System.Drawing.Bitmap(1, 1))
        {
            original.SetPixel(0, 0, System.Drawing.Color.Magenta);
            original.Save(asset, System.Drawing.Imaging.ImageFormat.Png);
        }
        string originalBytes = Convert.ToBase64String(File.ReadAllBytes(asset));
        string source = Path.Combine(root, "source.png");
        using (System.Drawing.Bitmap noise = new System.Drawing.Bitmap(2048, 2048, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
        {
            System.Drawing.Imaging.BitmapData data = noise.LockBits(new System.Drawing.Rectangle(0, 0, noise.Width, noise.Height),
                System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                byte[] pixels = new byte[data.Stride * noise.Height];
                new Random(1701).NextBytes(pixels);
                System.Runtime.InteropServices.Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
            }
            finally { noise.UnlockBits(data); }
            noise.Save(source, System.Drawing.Imaging.ImageFormat.Png);
        }
        ContentWorkshopProjectService service = new ContentWorkshopProjectService();
        ContentWorkshopProject project = service.Create(root, "tester.icons", "Icons");
        project.IsDirty = false;
        string relative;
        ContentWorkshopOperationResult result = service.ImportIcon(project, source, "noise", out relative);
        Check(!result.Success && result.ErrorMessage.IndexOf("size", StringComparison.OrdinalIgnoreCase) >= 0, "oversized PNG was not rejected for its size");
        Check(Convert.ToBase64String(File.ReadAllBytes(asset)) == originalBytes, "a rejected import replaced the old icon");
        Check(!project.IsDirty, "a rejected import changed the project's saved state");
    }

    private static void WorkshopSaveRollback()
    {
        ContentWorkshopProjectService service = new ContentWorkshopProjectService();
        string root = NewDir("workshop-rollback");
        ContentWorkshopProject project = service.Create(root, "tester.rollback", "Before");
        Check(service.Save(project).Success, "initial save");
        string about = Path.Combine(root, "About/About.json");
        string content = Path.Combine(root, "Content/content-pack.json");
        string oldAbout = File.ReadAllText(about);
        string oldContent = File.ReadAllText(content);
        project.About.name = "After";
        project.Content.items.Add(new ContentPackItem { id = "tester.rollback.item", displayName = "Changed" });
        project.IsDirty = true;
        ContentWorkshopOperationResult result;
        using (FileStream locked = new FileStream(content, FileMode.Open, FileAccess.Read, FileShare.None))
            result = service.Save(project);
        Check(!result.Success, "a locked document did not fail the save");
        Check(File.ReadAllText(about) == oldAbout && File.ReadAllText(content) == oldContent, "a failed save left documents at different revisions");
        Check(project.IsDirty, "a failed save cleared unsaved changes");
        Check(service.Save(project).Success && !project.IsDirty, "retry did not save after unlocking");
        ContentWorkshopProject reopened;
        Check(service.Open(root, out reopened).Success && reopened.About.name == "After" && reopened.Content.items.Count == 1,
            "retry did not persist both documents");

        root = NewDir("workshop-first-save-rollback");
        project = service.Create(root, "tester.firstsave", "New project");
        Directory.CreateDirectory(Path.Combine(root, "Content/content-pack.json"));
        result = service.Save(project);
        Check(!result.Success && project.IsDirty, "an obstructed first save succeeded or cleared dirty state");
        Check(!File.Exists(Path.Combine(root, "About/About.json")), "a failed first save left a partial project");
    }

    private static void WorkshopSlugs()
    {
        string[][] cases = new string[][]
        {
            new string[] { "Canned Beans", "canned_beans" },
            new string[] { "  Cr\u00E8me   Br\u00FBl\u00E9e!! ", "creme_brulee" },
            new string[] { "Stra\u00DFe", "strasse" },
            new string[] { "\u00C6on Flux", "aeon_flux" },
            new string[] { "\u0130stanbul Ice", "istanbul_ice" },
            new string[] { "\u00D8resund \u0141\u00F3d\u017A", "oresund_lodz" },
            new string[] { "123 Go", "123_go" },
            new string[] { "\u65E5\u672C\u8A9E", "" },
            new string[] { "--__--", "" },
            new string[] { null, "" },
            new string[] { "Ammo_Pistol (x2)", "ammo_pistol_x2" }
        };
        foreach (string[] c in cases)
        {
            foreach (string culture in Cultures)
            {
                string input = c[0];
                string slug = InCulture(culture, delegate { return ContentWorkshopIds.Slug(input, "_"); });
                Check(slug == c[1], "Slug(\"" + input + "\") in " + culture + " was \"" + slug + "\", expected \"" + c[1] + "\"");
            }
        }
        Check(ContentWorkshopIds.Slug("A B C", string.Empty) == "abc", "empty separator");
    }

    private static void WorkshopIdsAndFolders()
    {
        Check(ContentWorkshopIds.ModIdFor("Bunker Supplies", "Coolnether123") == "coolnether123.bunkersupplies", "pack id");
        Check(ContentWorkshopIds.ModIdFor(null, null) == ContentWorkshopIds.PlaceholderOwner + ".contentpack", "defaults");
        Check(ContentWorkshopIds.ModIdFor("!!!", " , Bob Smith, Ann") == "bobsmith.contentpack", "first real author");
        Check(ContentWorkshopIds.ModIdFor("P\u00E2t\u00E9", "\u00C9lise") == "elise.pate", "accents");
        Check(ContentWorkshopIds.Unique("a.b", new string[] { "A.B", "a.b_2", null }) == "a.b_3", "unique counts past taken ids");
        Check(ContentWorkshopIds.Unique("a.b", null) == "a.b", "nothing taken");

        string[][] folders = new string[][]
        {
            new string[] { "Bunker Supplies", "Bunker Supplies" },
            new string[] { "a<b>c:d", "a b c d" },
            new string[] { "Pack...", "Pack" },
            new string[] { "   ", "Content Pack" },
            new string[] { null, "Content Pack" },
            new string[] { "CON", "CON pack" },
            new string[] { "nul.old", "nul pack.old" },
            new string[] { "Com1 .txt", "Com1 pack.txt" },
            new string[] { "Console", "Console" },
            new string[] { "tab\there", "tab here" }
        };
        foreach (string[] f in folders)
            Check(ContentWorkshopIds.FolderNameFor(f[0]) == f[1], "FolderNameFor(\"" + f[0] + "\") was \"" + ContentWorkshopIds.FolderNameFor(f[0]) + "\"");
        string longName = ContentWorkshopIds.FolderNameFor(new string('x', 200));
        Check(longName.Length == 80, "long names are shortened");
        foreach (string[] f in folders)
        {
            string folder = Path.Combine(NewDir("folders"), ContentWorkshopIds.FolderNameFor(f[0]));
            Directory.CreateDirectory(folder);
            Check(Directory.Exists(folder), "Windows could not create " + folder);
        }
    }

    private static void WorkshopSavesStable()
    {
        ContentWorkshopProjectService service = new ContentWorkshopProjectService();
        string root = Path.Combine(NewDir("workshop"), "Bunker Supplies");
        ContentWorkshopProject project = service.Create(root, "tester.bunkersupplies", "Bunker Supplies");
        foreach (string id in new string[] { "tester.bunkersupplies.zeta", "tester.bunkersupplies.alpha" })
        {
            ContentPackItem item = new ContentPackItem();
            item.id = id;
            item.displayName = id.Substring(id.LastIndexOf('.') + 1);
            project.Content.items.Add(item);
        }
        ContentWorkshopOperationResult saved = service.Save(project);
        Check(saved.Success, "save failed: " + saved.ErrorMessage);
        string aboutPath = Path.Combine(Path.Combine(root, "About"), "About.json");
        string contentPath = Path.Combine(Path.Combine(root, "Content"), "content-pack.json");
        string about = File.ReadAllText(aboutPath);
        string content = File.ReadAllText(contentPath);
        Check(about.StartsWith("{\"id\":\"tester.bunkersupplies\",\"name\":"), "About.json order: " + about);

        // Open a copy whose About.json was written by hand in another order, then save again.
        JavaScriptSerializer plain = new JavaScriptSerializer();
        Dictionary<string, object> values = (Dictionary<string, object>)plain.DeserializeObject(about);
        List<string> keys = new List<string>(values.Keys);
        Dictionary<string, object> reversed = new Dictionary<string, object>();
        for (int i = keys.Count - 1; i >= 0; i--)
            reversed[keys[i]] = values[keys[i]];
        File.WriteAllText(aboutPath, plain.Serialize(reversed));
        for (int round = 0; round < 3; round++)
        {
            ContentWorkshopProject reopened;
            ContentWorkshopOperationResult opened = service.Open(root, out reopened);
            Check(opened.Success, "open failed: " + opened.ErrorMessage);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            reopened.IsDirty = true;
            Check(service.Save(reopened).Success, "resave");
            SameText(about, File.ReadAllText(aboutPath), "About.json after reopening, round " + round);
            SameText(content, File.ReadAllText(contentPath), "content-pack.json after reopening, round " + round);
        }

        // Two exports hold the same entries, in the same order, with the same bytes.
        string first = Path.Combine(NewDir("export"), "pack.zip");
        string second = Path.Combine(NewDir("export"), "pack.zip");
        ContentWorkshopOperationResult exported = service.ExportZip(project, first);
        Check(exported.Success, "export failed: " + exported.ErrorMessage);
        Check(service.ExportZip(project, second).Success, "second export failed");
        Same(ZipEntryNames(first), ZipEntryNames(second), "export entries");
        List<string> sorted = ZipEntryNames(first);
        sorted.Sort(StringComparer.OrdinalIgnoreCase);
        List<string> expectedOrder = new List<string>();
        foreach (string prefix in new string[] { "/About/", "/Content/", "/Assets/", "/README.md" })
            foreach (string name in sorted)
                if (name.IndexOf(prefix, StringComparison.Ordinal) >= 0)
                    expectedOrder.Add(name);
        Same(expectedOrder, ZipEntryNames(first), "export entries are grouped and sorted");
        Dictionary<string, byte[]> a = ReadZip(first);
        Dictionary<string, byte[]> b = ReadZip(second);
        foreach (string name in a.Keys)
            Check(Convert.ToBase64String(a[name]) == Convert.ToBase64String(b[name]), "export content differs for " + name);
    }

    private static void VanillaItemsBijective()
    {
        HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ItemCatalog catalog = new ItemCatalog();
        List<string> order = new List<string>();
        foreach (ItemChoice item in VanillaItems.All)
        {
            Check(names.Add(item.Name), "duplicate name " + item.Name);
            Check(ids.Add(item.Id), "duplicate id " + item.Id);
            Check(catalog.IdFor(catalog.NameFor(item.Id)) == item.Id, "round trip " + item.Id);
            Check(catalog.IdFor(item.Id.ToLowerInvariant()) == item.Id, "typed id " + item.Id);
            order.Add(item.Name);
        }
        Check(ids.Count > 150, "catalog size " + ids.Count);
        List<string> sorted = new List<string>(order);
        sorted.Sort(StringComparer.OrdinalIgnoreCase);
        Same(sorted, order, "base-game items are in name order");
        Check(catalog.NameFor("com.other.mod") == "com.other.mod" && catalog.IdFor("  com.other.mod  ") == "com.other.mod", "unknown ids pass through");
        Check(catalog.IdFor("  dUCT tAPE ") == "DuctTape", "typed names ignore case and spaces");
        Check(catalog.NameFor(null) == string.Empty && catalog.IdFor(null) == string.Empty, "null");
    }

    private static void ItemCatalogClashes()
    {
        ItemCatalog catalog = new ItemCatalog();
        int changes = 0;
        catalog.Changed += delegate { changes++; };
        catalog.SetPackItems(new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("pack.wood", "Wood"),
            new KeyValuePair<string, string>("pack.wood2", "Wood"),
            new KeyValuePair<string, string>("pack.blank", ""),
            new KeyValuePair<string, string>("", "No id"),
            new KeyValuePair<string, string>(null, "Null id")
        });
        Check(changes == 1, "Changed raised " + changes + " times");
        Check(catalog.IdFor("Wood") == "Wood", "base-game Wood keeps its name");
        Check(catalog.NameFor("pack.wood") == "Wood (this pack)", "clash gets a qualifier: " + catalog.NameFor("pack.wood"));
        Check(catalog.NameFor("pack.wood2") == "Wood (this pack) pack.wood2", "second clash: " + catalog.NameFor("pack.wood2"));
        Check(catalog.IdFor(catalog.NameFor("pack.wood2")) == "pack.wood2", "qualified names round trip");
        Check(catalog.NameFor("pack.blank") == "pack.blank", "blank name falls back to the id");
        int count = 0;
        foreach (ItemChoice choice in catalog.Choices)
            count++;
        Check(count == VanillaItems.All.Count + 3, "entries without an id are skipped");
        catalog.SetPackItems(null);
        Check(catalog.NameFor("pack.wood") == "pack.wood", "clearing pack items");
    }

    // ================================================================== settings

    private static void SettingsTimestamps()
    {
        Type service = typeof(SettingsService);
        DateTime value = new DateTime(2026, 9, 28, 11, 45, 48, 123, DateTimeKind.Utc);
        foreach (string culture in Cultures)
        {
            string text = InCulture(culture, delegate { return (string)Call(service, "FormatUtc", value); });
            Check(text == "2026-09-28T11:45:48.1230000Z", "format in " + culture + ": " + text);
            foreach (string readCulture in Cultures)
            {
                Dictionary<string, string> raw = new Dictionary<string, string>();
                raw["When"] = text;
                DateTime back = InCulture(readCulture, delegate { return (DateTime)Call(service, "ReadUtc", raw, "When"); });
                Check(back == value && back.Kind == DateTimeKind.Utc, "read in " + readCulture + " gave " + back.ToString("o"));
            }
        }
        Dictionary<string, string> junk = new Dictionary<string, string>();
        junk["When"] = "yesterday";
        Check((DateTime)Call(service, "ReadUtc", junk, "When") == DateTime.MinValue, "junk time");
        Check((DateTime)Call(service, "ReadUtc", new Dictionary<string, string>(), "When") == DateTime.MinValue, "missing time");
        Check((string)Call(service, "FormatUtc", DateTime.MinValue) == string.Empty, "unset time is written as empty");
    }

    // ================================================================== mod manager tab

    private sealed class TabFixture
    {
        public string ModsPath;
        public ModManagerTab Tab;
        public LoadOrderService Order = new LoadOrderService();

        public string[] ReadOrder()
        {
            return Order.ReadOrder(ModsPath);
        }

        public ModItem Mod(string id)
        {
            foreach (ModItem mod in (List<ModItem>)Field(Tab, "_allMods"))
                if (string.Equals(mod.Id, id, StringComparison.OrdinalIgnoreCase))
                    return mod;
            throw new Exception("no mod " + id);
        }

        public void Toggle(string id, bool on)
        {
            Call(Tab, "ModList_ToggleRequested", Field(Tab, "_modList"), Mod(id), on);
        }

        public void Move(string id, int index)
        {
            Call(Tab, "ModList_MoveRequested", Field(Tab, "_modList"), Mod(id), index);
        }

        public string Banner
        {
            get { return ((NoticeBanner)Field(Tab, "_actionBanner")).Message; }
        }

        public int UndoCount
        {
            get { return ((IList)Field(Tab, "_undo")).Count; }
        }
    }

    private static TabFixture NewTab(string orderJson)
    {
        TabFixture fixture = new TabFixture();
        fixture.ModsPath = NewDir("tabmods");
        WriteModFolder(Path.Combine(fixture.ModsPath, "Harmony"), "com.harmony.0harmony", "Harmony", "2.0", null);
        WriteModFolder(Path.Combine(fixture.ModsPath, "Core"), "test.core", "Core", "1.0", new string[] { "com.harmony.0harmony" });
        WriteModFolder(Path.Combine(fixture.ModsPath, "Addon"), "test.addon", "Addon", "1.0", new string[] { "test.core" });
        WriteModFolder(Path.Combine(fixture.ModsPath, "Solo"), "test.solo", "Solo", "1.0", null);
        WriteModFolder(Path.Combine(fixture.ModsPath, "Spare"), "test.spare", "Spare", "1.0", null);
        File.WriteAllText(Path.Combine(fixture.ModsPath, "loadorder.json"), orderJson ?? "{\"order\":[\"com.harmony.0harmony\",\"test.core\",\"test.addon\",\"not.installed\"]}");
        AppSettings settings = new AppSettings();
        settings.ModsPath = fixture.ModsPath;
        settings.EnableNexusIntegration = false;
        fixture.Tab = new ModManagerTab();
        fixture.Tab.Initialize(new ModDiscoveryService((string)null), fixture.Order, settings, null);
        fixture.Tab.RefreshMods();
        return fixture;
    }

    private static void TabToggleAndMove()
    {
        TabFixture f = NewTab(null);
        f.Toggle("test.solo", true);
        Same(new List<string> { "com.harmony.0harmony", "test.core", "test.addon", "test.solo", "not.installed" }, f.ReadOrder(), "turn on appends; unknown entry kept last");
        Check(f.Banner.IndexOf("Turned on Solo") >= 0, "banner: " + f.Banner);
        f.Toggle("test.solo", true);
        Same(new List<string> { "com.harmony.0harmony", "test.core", "test.addon", "test.solo", "not.installed" }, f.ReadOrder(), "turning on twice");
        f.Move("test.solo", -5);
        Check(f.ReadOrder()[0] == "test.solo", "negative index clamps to the top");
        f.Move("test.solo", 999);
        Check(f.ReadOrder()[3] == "test.solo", "large index clamps to the bottom of the active mods");
        f.Move("test.spare", 1);
        Same(new List<string> { "com.harmony.0harmony", "test.spare", "test.core", "test.addon", "test.solo", "not.installed" }, f.ReadOrder(), "moving an inactive mod turns it on in place");
        f.Toggle("test.core", false);
        Check(Array.IndexOf(f.ReadOrder(), "test.core") < 0, "turn off removes it");
        Check(Array.IndexOf(f.ReadOrder(), "not.installed") == f.ReadOrder().Length - 1, "unknown entries survive every change");
    }

    private static void TabUndo()
    {
        TabFixture f = NewTab(null);
        List<string[]> states = new List<string[]>();
        states.Add(f.ReadOrder());
        string[] sequence = new string[] { "test.solo", "test.spare", "test.core", "test.solo" };
        bool[] onOff = new bool[] { true, true, false, false };
        for (int i = 0; i < sequence.Length; i++)
        {
            f.Toggle(sequence[i], onOff[i]);
            states.Add(f.ReadOrder());
        }
        for (int i = states.Count - 2; i >= 0; i--)
        {
            Call(f.Tab, "Undo");
            Same(states[i], f.ReadOrder(), "undo back to step " + i);
        }
        Call(f.Tab, "Undo");
        Check(f.Banner == "Nothing to undo.", "extra undo: " + f.Banner);
        Same(states[0], f.ReadOrder(), "extra undo changed the order");

        // The history keeps the last 30 changes.
        for (int i = 0; i < 35; i++)
            f.Toggle("test.solo", i % 2 == 0);
        Check(f.UndoCount == 30, "history holds " + f.UndoCount);
        for (int i = 0; i < 40; i++)
            Call(f.Tab, "Undo");
        Check(f.UndoCount == 0, "history not emptied");
        // Changes 5 to 34 were kept, so the oldest restorable state is the one after change 4 (Solo on).
        Check(Array.IndexOf(f.ReadOrder(), "test.solo") >= 0, "30 undos should stop at the state after change 4");
    }

    private static void TabFailedSaveNotUndoable()
    {
        TabFixture f = NewTab(null);
        string file = Path.Combine(f.ModsPath, "loadorder.json");
        string before = File.ReadAllText(file);
        File.SetAttributes(file, FileAttributes.ReadOnly);
        try
        {
            f.Toggle("test.solo", true);
            Check(f.UndoCount == 0, "a change that failed to save can be undone");
            Check(File.ReadAllText(file) == before, "the file changed");
            Check(((NoticeBanner)Field(f.Tab, "_actionBanner")).Kind == NoticeKind.Error, "no error shown");
        }
        finally
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }
        f.Toggle("test.solo", true);
        File.SetAttributes(file, FileAttributes.ReadOnly);
        try
        {
            Call(f.Tab, "Undo");
            Check(f.UndoCount == 1, "an undo that failed to save was used up");
        }
        finally
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }
        Call(f.Tab, "Undo");
        Check(f.UndoCount == 0 && Array.IndexOf(f.ReadOrder(), "test.solo") < 0, "undo works once the file is writable");
    }

    private static void TabFixNoop()
    {
        TabFixture f = NewTab(null);
        string before = File.ReadAllText(Path.Combine(f.ModsPath, "loadorder.json"));
        Call(f.Tab, "FixLoadOrder");
        Check(f.UndoCount == 0, "fixing a working order added an undo step");
        Check(File.ReadAllText(Path.Combine(f.ModsPath, "loadorder.json")) == before, "fixing a working order rewrote the file");
        Check(f.Banner.IndexOf("already") >= 0, "banner: " + f.Banner);

        f.Move("test.addon", 0);
        Call(f.Tab, "FixLoadOrder");
        Same(new List<string> { "com.harmony.0harmony", "test.core", "test.addon", "not.installed" }, f.ReadOrder(), "fixed order");
        Call(f.Tab, "Undo");
        Same(new List<string> { "test.addon", "com.harmony.0harmony", "test.core", "not.installed" }, f.ReadOrder(), "undo the fix");
    }

    private static void TabListGestures()
    {
        // The list is live inside the tab, so every gesture is applied before the next one.
        TabFixture f = NewTab(null);
        ModSwitchList list = (ModSwitchList)Field(f.Tab, "_modList");
        ListBox box = (ListBox)Field(list, "_list");
        IntPtr handle = box.Handle;
        List<string> events = new List<string>();
        list.ToggleRequested += delegate(object s, ModItem m, bool on) { events.Add("toggle " + m.Id + " " + on); };
        list.MoveRequested += delegate(object s, ModItem m, int i) { events.Add("move " + m.Id + " " + i); };

        // Rows: 0 header, 1 harmony, 2 core, 3 addon, 4 header, 5 solo, 6 spare.
        Check(RowIndex(box, "test.solo") == 5 && RowIndex(box, "test.addon") == 3, "row layout");
        box.SelectedIndex = 0;
        Check(list.SelectedMod != null && list.SelectedMod.Id == "com.harmony.0harmony", "selecting a header selects the mod below it");
        box.SelectedIndex = 4;
        Check(list.SelectedMod != null && list.SelectedMod.Id == "test.solo", "selecting the Not active header selects the mod below it");

        list.SelectMod("test.spare");
        Key(list, box, Keys.Delete);                       // inactive: nothing to turn off
        list.SelectMod("test.solo");
        Key(list, box, Keys.Space);                        // on, at the end of the active mods
        Key(list, box, Keys.Delete);                       // off again
        list.SelectMod("test.core");
        Key(list, box, Keys.Control | Keys.Up);            // core to the top
        Key(list, box, Keys.Control | Keys.Down);          // and back to second
        Key(list, box, Keys.Delete);                       // core off
        list.SelectMod("com.harmony.0harmony");
        Key(list, box, Keys.Control | Keys.Up);            // already first
        list.SelectMod("test.addon");
        Key(list, box, Keys.Control | Keys.Down);          // already last
        list.SelectMod("does.not.exist");
        Same(new List<string> { "toggle test.solo True", "toggle test.solo False", "move test.core 0", "move test.core 1", "toggle test.core False" }, events, "keyboard");
        Same(new List<string> { "com.harmony.0harmony", "test.addon", "not.installed" }, f.ReadOrder(), "order after keys");

        // Drags. A slot is the gap before that active position; the tab starts from its first state again.
        f = NewTab(null);
        list = (ModSwitchList)Field(f.Tab, "_modList");
        box = (ListBox)Field(list, "_list");
        handle = box.Handle;
        events.Clear();
        list.ToggleRequested += delegate(object s, ModItem m, bool on) { events.Add("toggle " + m.Id + " " + on); };
        list.MoveRequested += delegate(object s, ModItem m, int i) { events.Add("move " + m.Id + " " + i); };
        Drag(list, box, "test.core", 3, false);            // below addon
        Drag(list, box, "test.core", 3, false);            // its own gap below
        Drag(list, box, "test.core", 2, false);            // its own gap above
        Drag(list, box, "com.harmony.0harmony", 1, false); // just below itself
        Drag(list, box, "test.addon", -1, true);           // onto Not active
        Drag(list, box, "test.spare", -1, true);           // inactive onto Not active
        Drag(list, box, "test.solo", 0, false);            // inactive to the top
        Drag(list, box, "test.solo", -1, false);           // released outside any slot
        SetField(list, "_pressIndex", 0);                  // a header cannot be dragged
        SetField(list, "_dragging", true);
        SetField(list, "_dropSlot", 1);
        Call(list, "List_MouseUp", box, new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
        SetField(list, "_pressIndex", RowIndex(box, "test.spare"));
        SetField(list, "_dragging", true);
        SetField(list, "_dropSlot", 0);
        box.ClearSelected();
        Key(list, box, Keys.Escape);                       // Esc cancels a drag even with nothing selected
        Check(!(bool)Field(list, "_dragging"), "Esc did not cancel the drag");
        Call(list, "List_MouseUp", box, new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
        Same(new List<string> { "move test.core 2", "toggle test.addon False", "move test.solo 0" }, events, "drag");
        Same(new List<string> { "test.solo", "com.harmony.0harmony", "test.core", "not.installed" }, f.ReadOrder(), "order after drags");

        list.SetFilter("SOL");
        Check(RowIndex(box, "test.solo") >= 0 && RowIndex(box, "test.core") < 0, "filter");
        list.SetFilter("zzz");
        Check(CountMods(box) == 0, "no matches");
        list.SetFilter(null);
        Check(CountMods(box) == 5, "clearing the filter");
    }

    private static void SwitchDoubleClickTarget()
    {
        using (ModSwitchList list = new ModSwitchList())
        {
            ModItem alpha = new ModItem("test.alpha", "Alpha", "");
            ModItem beta = new ModItem("test.beta", "Beta", "");
            List<ModItem> active = new List<ModItem> { alpha, beta };
            List<ModItem> inactive = new List<ModItem>();
            list.Size = new System.Drawing.Size(420, 260);
            list.SetMods(active, inactive, null, null, null);
            ListBox box = (ListBox)Field(list, "_list");
            IntPtr handle = box.Handle;
            List<string> toggles = new List<string>();
            list.ToggleRequested += delegate(object sender, ModItem mod, bool on)
            {
                toggles.Add(mod.Id);
                active.Remove(mod);
                inactive.Add(mod);
                list.SetMods(active, inactive, null, null, null);
            };
            System.Drawing.Rectangle bounds = box.GetItemRectangle(RowIndex(box, alpha.Id));
            System.Drawing.Rectangle hit = (System.Drawing.Rectangle)Call(list, "SwitchBounds", bounds);
            System.Drawing.Point point = new System.Drawing.Point(hit.Left + hit.Width / 2, hit.Top + hit.Height / 2);
            Call(list, "List_MouseDown", box, new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0));
            Call(list, "List_MouseDown", box, new MouseEventArgs(MouseButtons.Left, 2, point.X, point.Y, 0));
            Call(list, "List_MouseDoubleClick", box, new MouseEventArgs(MouseButtons.Left, 2, point.X, point.Y, 0));
            Same(new List<string> { alpha.Id }, toggles, "a double-click followed the moving row");
            Check(active.Count == 1 && active[0] == beta, "a second mod was turned off");
        }
    }

    private static void TabOrderOnlyInventory()
    {
        TabFixture f = NewTab(null);
        using (f.Tab)
        {
            object inventory = Field(f.Tab, "_allMods");
            WriteModFolder(Path.Combine(f.ModsPath, "Added"), "test.added", "Added", "1.0", null);
            f.Toggle("test.solo", true);
            Check(ReferenceEquals(inventory, Field(f.Tab, "_allMods")), "a toggle rescanned the inventory");
            f.Move("test.solo", 0);
            Check(ReferenceEquals(inventory, Field(f.Tab, "_allMods")), "a reorder rescanned the inventory");
            f.Tab.Undo();
            Check(ReferenceEquals(inventory, Field(f.Tab, "_allMods")), "undo rescanned the inventory");
            Check(((List<ModItem>)inventory).Count == 5, "an order edit added disk-discovered mods");
            f.Tab.RefreshMods();
            Check(!ReferenceEquals(inventory, Field(f.Tab, "_allMods")) && f.Mod("test.added").DisplayName == "Added", "explicit refresh did not rescan disk");
            Check(Array.IndexOf(f.ReadOrder(), "not.installed") >= 0, "unknown order entries were lost");
        }
    }

    private static void SwitchDoubleClickButtons()
    {
        using (ModSwitchList list = new ModSwitchList())
        {
            ModItem mod = new ModItem("test.alpha", "Alpha", "");
            List<string> toggles = new List<string>();
            list.ToggleRequested += delegate(object sender, ModItem item, bool on) { toggles.Add(item.Id); };
            list.Size = new System.Drawing.Size(700, 400);
            list.SetMods(new List<ModItem> { mod }, new List<ModItem>(), new HashSet<string>(), new HashSet<string>(), null);
            ListBox box = (ListBox)Field(list, "_list");
            IntPtr handle = box.Handle;
            System.Drawing.Rectangle row = box.GetItemRectangle(RowIndex(box, mod.Id));
            int x = row.Left + 150;
            int y = row.Top + row.Height / 2;
            Call(list, "List_MouseDoubleClick", box, new MouseEventArgs(MouseButtons.Right, 2, x, y, 0));
            Check(toggles.Count == 0, "a right double-click changed a mod");
            Call(list, "List_MouseDown", box, new MouseEventArgs(MouseButtons.Left, 1, x, y, 0));
            Call(list, "List_MouseDown", box, new MouseEventArgs(MouseButtons.Left, 2, x, y, 0));
            Call(list, "List_MouseDoubleClick", box, new MouseEventArgs(MouseButtons.Left, 2, x, y, 0));
            Same(new List<string> { mod.Id }, toggles, "left row-text double-click");
        }
    }

    private static void TabProfileCurrentInventory()
    {
        TabFixture f = NewTab(null);
        using (f.Tab)
        {
            ModListFileService service = (ModListFileService)Field(f.Tab, "_listService");
            service.SaveProfile(f.ModsPath, "Changed", new List<ModItem> { new ModItem("test.added", "Added", ""), f.Mod("test.solo") });
            WriteModFolder(Path.Combine(f.ModsPath, "Added"), "test.added", "Added", "1.0", null);
            Directory.Move(Path.Combine(f.ModsPath, "Solo"), Path.Combine(NewDir("removed-mod"), "Solo"));
            Call(f.Tab, "SwitchProfile", "Changed");
            Same(new List<string> { "test.added" }, f.ReadOrder(), "profile used a stale installed inventory");
            Check(f.Mod("test.added").IsEnabled, "the newly installed profile mod was not shown as active");
            Check(f.Banner.IndexOf("1 mod in it isn't installed") >= 0, "the removed folder was not reported as missing");
        }
    }

    private static void TabOrderOnlyCompatibility()
    {
        TabFixture f = NewTab(null);
        using (f.Tab)
        {
            ModItem core = f.Mod("test.addon");
            core.ApiCompatibility.Severity = ApiCompatibilitySeverity.Error;
            core.ApiCompatibility.Summary = "Needs a newer ModAPI";
            core.IsModApiCompatible = false;
            core.Status = ModStatus.VersionMismatch;
            core.StatusMessage = core.ApiCompatibility.Summary;
            f.Toggle("test.core", false);
            Check(f.Mod("test.addon").Status == ModStatus.Error, "the missing dependency was not shown");
            f.Toggle("test.core", true);
            f.Move("test.core", 1);
            Check(f.Mod("test.addon").Status == ModStatus.VersionMismatch && f.Mod("test.addon").StatusMessage == "Needs a newer ModAPI",
                "clearing a dependency problem lost the intrinsic API issue");
            f.Toggle("test.core", false);
            f.Toggle("test.addon", false);
            Check(f.Mod("test.addon").Status == ModStatus.VersionMismatch && f.Mod("test.addon").StatusMessage == "Needs a newer ModAPI",
                "an inactive mod retained its dependency error instead of its intrinsic API issue");
        }
    }

    private static void TabFilterClearsDetails()
    {
        TabFixture f = NewTab(null);
        using (f.Tab)
        {
            ModSwitchList list = (ModSwitchList)Field(f.Tab, "_modList");
            IntPtr handle = ((ListBox)Field(list, "_list")).Handle;
            ModDetailsPanel details = (ModDetailsPanel)Field(f.Tab, "_detailsPanel");
            list.SelectMod("test.core");
            Check(((Label)Field(details, "_nameLabel")).Text == "Core", "selected mod details were not shown");
            list.SetFilter("Solo");
            Check(list.SelectedMod == null, "a hidden mod stayed selected");
            Check(((Control)Field(details, "_placeholderPanel")).Visible, "the details still show the filtered-out mod");
            Check(((Label)Field(details, "_placeholderTitle")).Text == "Nothing selected", "selection placeholder was not restored");
            list.SetFilter(null);
            list.SelectMod("test.solo");
            Check(((Label)Field(details, "_nameLabel")).Text == "Solo", "details did not recover after clearing the filter");
        }
    }

    private static void SwitchListRebuildSelection()
    {
        using (ModSwitchList list = new ModSwitchList())
        {
            ModItem alpha = new ModItem("test.alpha", "Alpha", "");
            ModItem beta = new ModItem("test.beta", "Beta", "");
            list.SetMods(new List<ModItem> { alpha, beta }, new List<ModItem>(), null, null, null);
            IntPtr handle = ((ListBox)Field(list, "_list")).Handle;
            List<ModItem> selections = new List<ModItem>();
            list.SelectionChanged += delegate(object sender, ModItem mod) { selections.Add(mod); };
            list.SelectMod(alpha.Id);
            Check(selections.Count == 1 && selections[0] == alpha, "initial selection");
            selections.Clear();
            list.SetFilter("Alpha");
            Check(selections.Count == 1 && selections[0] == alpha, "retained selection published intermediate states");

            ModItem refreshed = new ModItem(alpha.Id, "Alpha refreshed", "");
            selections.Clear();
            list.SetMods(new List<ModItem> { refreshed, beta }, new List<ModItem>(), null, null, null);
            Check(selections.Count == 1 && selections[0] == refreshed && list.SelectedMod == refreshed,
                "selection did not publish refreshed metadata");

            selections.Clear();
            list.SetFilter("Beta");
            Check(selections.Count == 1 && selections[0] == null && list.SelectedMod == null,
                "filtered-out selection did not publish null");
            selections.Clear();
            list.SetFilter(null);
            Check(selections.Count == 1 && selections[0] == null, "clearing the filter revived the old selection");
            list.SelectMod(beta.Id);
            selections.Clear();
            list.SetMods(new List<ModItem>(), new List<ModItem>(), null, null, null);
            Check(selections.Count == 1 && selections[0] == null && list.SelectedMod == null,
                "removing all mods did not clear selection");
        }
    }

    private static int RowIndex(ListBox box, string id)
    {
        for (int i = 0; i < box.Items.Count; i++)
        {
            ModItem mod = (ModItem)Field(box.Items[i], "Mod");
            if (mod != null && mod.Id == id)
                return i;
        }
        return -1;
    }

    private static int CountMods(ListBox box)
    {
        int count = 0;
        foreach (object row in box.Items)
            if (Field(row, "Mod") != null)
                count++;
        return count;
    }

    private static void Key(ModSwitchList list, ListBox box, Keys keys)
    {
        Call(list, "List_KeyDown", box, new KeyEventArgs(keys));
    }

    /// <summary>Simulates releasing a drag of <paramref name="id"/> at an active slot, or on "Not active".</summary>
    private static void Drag(ModSwitchList list, ListBox box, string id, int slot, bool toInactive)
    {
        SetField(list, "_pressIndex", RowIndex(box, id));
        SetField(list, "_dragging", true);
        SetField(list, "_dropSlot", slot);
        SetField(list, "_dropToInactive", toInactive);
        Call(list, "List_MouseUp", box, new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
        Check(!(bool)Field(list, "_dragging"), "drag state not cleared");
    }

    private static void TabProfiles()
    {
        TabFixture f = NewTab(null);
        ModListFileService service = (ModListFileService)Field(f.Tab, "_listService");
        service.SaveProfile(f.ModsPath, "Full", (List<ModItem>)Field(f.Tab, "_enabledMods"));
        f.Toggle("test.addon", false);
        service.SaveProfile(f.ModsPath, "Lean", (List<ModItem>)Field(f.Tab, "_enabledMods"));
        service.SetActiveProfile(f.ModsPath, "Lean");

        Call(f.Tab, "SwitchProfile", "Full");
        Check(Array.IndexOf(f.ReadOrder(), "test.addon") >= 0 && service.GetActiveProfile(f.ModsPath) == "Full", "switch to Full");
        f.Toggle("test.solo", true);
        ModListFile full;
        string error;
        service.TryLoadProfile(f.ModsPath, "Full", out full, out error);
        Check(Array.IndexOf(ModListFileService.OrderOf(full), "test.solo") >= 0, "the active profile follows changes");
        ModListFile lean;
        service.TryLoadProfile(f.ModsPath, "Lean", out lean, out error);
        Check(Array.IndexOf(ModListFileService.OrderOf(lean), "test.solo") < 0, "other profiles are untouched");

        Call(f.Tab, "Undo");
        Check(Array.IndexOf(f.ReadOrder(), "test.solo") < 0, "undo inside a profile");
        Call(f.Tab, "Undo");
        Check(service.GetActiveProfile(f.ModsPath) == "Lean" && Array.IndexOf(f.ReadOrder(), "test.addon") < 0, "undoing a switch restores the previous profile");

        // A profile listing mods that are not installed applies the installed ones in order.
        ModListFile partial = service.Build("Partial", new List<ModItem> { new ModItem("gone.mod", "Gone", "x"), f.Mod("test.spare"), f.Mod("test.solo") });
        service.Save(Path.Combine(Path.Combine(f.ModsPath, "_smm_profiles"), "Partial.smmlist"), partial);
        Call(f.Tab, "SwitchProfile", "Partial");
        string[] order = f.ReadOrder();
        Check(order[0] == "test.spare" && order[1] == "test.solo" && Array.IndexOf(order, "gone.mod") < 0, "partial profile: " + string.Join(", ", order));
        Check(f.Banner.IndexOf("1 mod in it isn't installed") >= 0, "missing mods are reported: " + f.Banner);

        Call(f.Tab, "SwitchProfile", "Does not exist");
        Check(((NoticeBanner)Field(f.Tab, "_actionBanner")).Kind == NoticeKind.Error, "missing profile reported");
        Check(service.GetActiveProfile(f.ModsPath) == "Partial", "a failed switch changed the active profile");
    }

    private static void TabOddModsFolders()
    {
        TabFixture corrupt = NewTab("{ not json");
        Check(((List<ModItem>)Field(corrupt.Tab, "_enabledMods")).Count == 0, "a corrupt load order means nothing is on");
        corrupt.Toggle("test.solo", true);
        Same(new List<string> { "test.solo" }, corrupt.ReadOrder(), "a corrupt file is replaced by a valid one");

        TabFixture f = NewTab(null);
        WriteModFolder(Path.Combine(f.ModsPath, "Core Copy"), "test.core", "Core", "1.1", null);
        Directory.CreateDirectory(Path.Combine(f.ModsPath, "No About"));
        f.Tab.RefreshMods();
        f.Toggle("no about", true);
        int coreCount = 0;
        foreach (string id in f.ReadOrder())
            if (id == "test.core") coreCount++;
        Check(coreCount == 1, "duplicate mod IDs are listed once in the load order");
        Check(Array.IndexOf(f.ReadOrder(), "no about") >= 0, "a mod without About.json can be turned on");
        Check((bool)f.Tab.EnableModById("test.core") && !f.Tab.EnableModById("not.here"), "EnableModById");
        int undo = f.UndoCount;
        f.Tab.EnableModById("test.core");
        Check(f.UndoCount == undo, "enabling an enabled mod added an undo step");
    }

    private static void TabDropInstall()
    {
        TabFixture f = NewTab(null);
        string zip = Path.Combine(_root, "drop-" + Guid.NewGuid().ToString("N") + ".zip");
        WriteZip(zip, true, "Dropped/About/About.json", AboutJson("test.dropped", "Dropped", "1.0"));
        string junk = Path.Combine(_root, "junk-" + Guid.NewGuid().ToString("N") + ".zip");
        File.WriteAllText(junk, "not a zip");
        Call(f.Tab, "InstallPackages", new List<string> { zip, junk });
        string[] order = f.ReadOrder();
        Check(order[3] == "test.dropped", "the dropped mod is turned on at the end of the active mods: " + string.Join(", ", order));
        NoticeBanner banner = (NoticeBanner)Field(f.Tab, "_actionBanner");
        Check(banner.Kind == NoticeKind.Error && banner.Message.IndexOf("junk-") >= 0, "the failed file is named: " + banner.Message);
        Call(f.Tab, "Undo");
        Check(Array.IndexOf(f.ReadOrder(), "test.dropped") < 0 && Directory.Exists(Path.Combine(f.ModsPath, "Dropped")), "undo turns it off but keeps it installed");
    }

    private static void TabPendingList()
    {
        TabFixture f = NewTab(null);
        ModListFileService service = new ModListFileService();
        ModListFile list = service.Build("Shared", new List<ModItem> { f.Mod("test.spare"), new ModItem("not.yet", "Not yet", "x"), f.Mod("test.solo") });

        // Installs finished: the installed mods of the list are applied, in its order, exactly once.
        SetField(f.Tab, "_pendingList", list);
        f.Tab.ApplyPendingList();
        Same(new List<string> { "test.spare", "test.solo", "not.installed" }, f.ReadOrder(), "pending list applied");
        int undo = f.UndoCount;
        f.Toggle("test.core", true);
        f.Tab.ApplyPendingList();
        Check(Array.IndexOf(f.ReadOrder(), "test.core") >= 0 && f.UndoCount == undo + 1, "a list was applied twice");

        // Installs never started: the list is forgotten and a later batch changes nothing.
        SetField(f.Tab, "_pendingList", list);
        f.Tab.CancelPendingList();
        string[] before = f.ReadOrder();
        f.Tab.ApplyPendingList();
        Same(before, f.ReadOrder(), "a cancelled list was applied");
    }

    // ================================================================== nexus tab

    private static object NewBatch(NexusModsTab tab, string name)
    {
        Type batchType = typeof(NexusModsTab).GetNestedType("BatchInstall", BindingFlags.NonPublic);
        object batch = Activator.CreateInstance(batchType, true);
        batchType.GetField("Name").SetValue(batch, name);
        batchType.GetField("Remote").SetValue(batch, new NexusRemoteMod { GameDomain = "sheltered", ModId = name.Length, Name = name });
        return batch;
    }

    private static bool StartBatch(NexusModsTab tab, params string[] names)
    {
        Type batchType = typeof(NexusModsTab).GetNestedType("BatchInstall", BindingFlags.NonPublic);
        IList planned = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(batchType));
        foreach (string name in names)
            planned.Add(NewBatch(tab, name));
        object result = Call(tab, "StartBatch", planned);
        return result == null || (bool)result;
    }

    // Completion records now carry the item that started the operation. Keeping
    // the older signature here also lets these tests reproduce the bug on the saved baseline.
    private static void CompleteBatch(NexusModsTab tab, object origin, bool success, string name, string error)
    {
        if (typeof(NexusModsTab).GetMethod("OnBatchInstallFinished", AnyMember).GetParameters().Length == 4)
            Call(tab, "OnBatchInstallFinished", origin, success, name, error);
        else
            Call(tab, "OnBatchInstallFinished", success, name, error);
    }

    private static void CompleteInstall(NexusModsTab tab, object origin, string name)
    {
        NexusRemoteMod mod = new NexusRemoteMod { Name = name };
        NexusInstallResult result = new NexusInstallResult { InstalledPath = "fixture-only" };
        if (typeof(NexusModsTab).GetMethod("FinishInstallSuccess", AnyMember).GetParameters().Length == 3)
            Call(tab, "FinishInstallSuccess", mod, result, origin);
        else
            Call(tab, "FinishInstallSuccess", mod, result);
    }

    private static void FailInstall(NexusModsTab tab, object origin, string error)
    {
        if (typeof(NexusModsTab).GetMethod("FinishInstallWithError", AnyMember).GetParameters().Length == 2)
            Call(tab, "FinishInstallWithError", error, origin);
        else
            Call(tab, "FinishInstallWithError", error);
    }

    private static void NexusRestartIgnoresStaleResults()
    {
        using (NexusModsTab tab = new NexusModsTab())
        {
            StartBatch(tab, "Old");
            object old = Field(tab, "_batchCurrent");
            Call(tab, "StopBatch", "fixture stop");
            StartBatch(tab, "New", "Next");
            object current = Field(tab, "_batchCurrent");
            CompleteBatch(tab, old, true, "Old", null);
            CompleteBatch(tab, old, false, "Old", "old failure");
            Check(ReferenceEquals(current, Field(tab, "_batchCurrent")), "an old result advanced the new batch");
            Check((int)Field(tab, "_batchSucceeded") == 0 && ((IList)Field(tab, "_batchFailures")).Count == 0, "old results entered the new tally");
            CompleteBatch(tab, current, true, "New", null);
            Check((int)Field(tab, "_batchSucceeded") == 1 && !ReferenceEquals(current, Field(tab, "_batchCurrent")), "the current result did not advance");
        }
    }

    private static void NexusQueuedCompletionOwnership()
    {
        foreach (bool manual in new bool[] { false, true })
        using (NexusModsTab tab = new NexusModsTab())
        {
            IntPtr handle = tab.Handle;
            int completions = 0;
            tab.InstallCompleted += delegate { completions++; };
            object origin = null;
            if (!manual)
            {
                StartBatch(tab, "Old");
                origin = Field(tab, "_batchCurrent");
            }
            CompleteInstall(tab, origin, manual ? "Manual" : "Old");
            if (!manual)
                Call(tab, "StopBatch", "fixture stop");
            StartBatch(tab, "New", "Next");
            object current = Field(tab, "_batchCurrent");
            Application.DoEvents();
            Check(completions == 1, "the completed installation was not reported");
            Check(ReferenceEquals(current, Field(tab, "_batchCurrent")) && (int)Field(tab, "_batchSucceeded") == 0,
                (manual ? "a manual" : "a queued stale") + " completion counted toward the new batch");
            string currentTitle = ((Label)Field(Field(tab, "_detailsPanel"), "_placeholderTitle")).Text;
            FailInstall(tab, origin, "unrelated failure");
            Application.DoEvents();
            Check(ReferenceEquals(current, Field(tab, "_batchCurrent")) && ((IList)Field(tab, "_batchFailures")).Count == 0,
                "an unrelated failure advanced the new batch");
            Check(((Label)Field(Field(tab, "_detailsPanel"), "_placeholderTitle")).Text == currentTitle,
                "an unrelated failure replaced the current install's details");
        }
    }

    private static void NexusInvalidLinkOwnership()
    {
        using (NexusModsTab tab = new NexusModsTab())
        {
            IntPtr handle = tab.Handle;
            StartBatch(tab, "Current", "Next");
            object current = Field(tab, "_batchCurrent");
            tab.HandleNxmLinkAsync("not-an-nxm-link");
            Application.DoEvents();
            Check(ReferenceEquals(current, Field(tab, "_batchCurrent")) && ((IList)Field(tab, "_batchFailures")).Count == 0,
                "an invalid link failed the active install");
        }
    }

    private static Guid AcquireInstallWork(NexusModsTab tab, object batch)
    {
        object[] args = new object[] { batch, Guid.Empty };
        object result = Call(tab, "TryAcquireInstallWorkLease", args);
        Check(result.ToString() == "Acquired" && (Guid)args[1] != Guid.Empty, "install work was not admitted");
        return (Guid)args[1];
    }

    private static void NexusActiveWorkGating()
    {
        using (NexusModsTab tab = NewNexusSelectionTab(true))
        {
            IntPtr handle = tab.Handle;
            Guid work = AcquireInstallWork(tab, null);
            Application.DoEvents();
            object[] duplicate = new object[] { null, Guid.Empty };
            Check(Call(tab, "TryAcquireInstallWorkLease", duplicate).ToString() == "Busy" && (Guid)duplicate[1] == Guid.Empty,
                "a second worker acquired the active install gate");
            Button install = (Button)Field(tab, "_installSelectedButton");
            Button updateAll = (Button)Field(tab, "_updateAllButton");
            Check(!install.Enabled && !updateAll.Enabled, "active work left install actions enabled");
            ((ListBox)Field(tab, "_primaryList")).SelectedIndex = 1;
            Check(!install.Enabled && !((ModDetailsPanel)Field(tab, "_detailsPanel")).ShowNexusActionButtons,
                "selection reopened install actions during active work");
            foreach (string handler in new string[] { "InstallSelectedButton_Click", "DetailsPanel_NexusUpdateRequested", "DetailsPanel_NexusStableRequested" })
            {
                Call(tab, handler, tab, EventArgs.Empty);
                Check(((Label)Field(Field(tab, "_detailsPanel"), "_placeholderTitle")).Text == "Already installing", handler + " bypassed active work");
            }
            Check(!tab.InstallReferences(new List<NexusModReference> { new NexusModReference { GameDomain = "sheltered", ModId = 5 } }),
                "a shared list started over active work");
            Check(!StartBatch(tab, "Unexpected"), "batch admission reported success over active work");
            Check(!tab.IsBatchRunning && Field(tab, "_batchCurrent") == null, "a batch started over active work");
            Call(tab, "ReleaseInstallWorkLease", Guid.NewGuid());
            Application.DoEvents();
            Check(!install.Enabled && (Guid)Field(tab, "_activeInstallWorkToken") == work, "an unrelated release cleared active work");
            Call(tab, "ReleaseInstallWorkLease", work);
            Application.DoEvents();
            Check(install.Enabled && updateAll.Enabled, "the owning release did not restore actions");

            // A real install-service refusal also has to release the view's work gate.
            // The empty URL is rejected locally before any download or filesystem work.
            object[] refused = new object[] { Installer(), "", NewDir("nexus-refused"),
                new NexusRemoteMod { GameDomain = "sheltered", ModId = 5 }, new NexusRemoteModFile { FileId = 8 },
                null, null, null, null };
            Check(Call(tab, "DownloadAndInstallWithLease", refused) == null && !string.IsNullOrEmpty((string)refused[7]) && refused[8].ToString() == "Acquired",
                "a refused install did not return its failure");
            Application.DoEvents();
            Check((Guid)Field(tab, "_activeInstallWorkToken") == Guid.Empty && install.Enabled && updateAll.Enabled,
                "a failed install retained the work gate");
        }
    }

    private static void NexusStoppedWorkGating()
    {
        using (NexusModsTab tab = NewNexusSelectionTab(true))
        {
            IntPtr handle = tab.Handle;
            StartBatch(tab, "Old", "Queued");
            object old = Field(tab, "_batchCurrent");
            Guid oldWork = AcquireInstallWork(tab, old);
            Call(tab, "StopBatch", "fixture stop");
            Application.DoEvents();
            Button install = (Button)Field(tab, "_installSelectedButton");
            Check(!tab.IsBatchRunning && !install.Enabled && (Guid)Field(tab, "_activeInstallWorkToken") == oldWork,
                "stopping the queue exposed another install while its worker ran");
            object[] stale = new object[] { old, Guid.Empty };
            Check(Call(tab, "TryAcquireInstallWorkLease", stale).ToString() == "StaleBatch" && (Guid)stale[1] == Guid.Empty,
                "a stopped batch admitted new download work");
            Check(!StartBatch(tab, "Too early"), "a stopped worker's replacement was reported as started");
            Check(!tab.IsBatchRunning, "a replacement batch started before old work ended");
            CompleteBatch(tab, old, true, "Old", null);
            Check(!tab.IsBatchRunning && (int)Field(tab, "_batchSucceeded") == 0, "a stopped result advanced the queue");
            Call(tab, "ReleaseInstallWorkLease", oldWork);
            Application.DoEvents();
            Check(install.Enabled, "old worker completion did not restore actions");
            StartBatch(tab, "New");
            object current = Field(tab, "_batchCurrent");
            Guid currentWork = AcquireInstallWork(tab, current);
            Call(tab, "ReleaseInstallWorkLease", oldWork);
            Application.DoEvents();
            Check(!install.Enabled && (Guid)Field(tab, "_activeInstallWorkToken") == currentWork, "an old release cleared a newer worker");
            Call(tab, "ReleaseInstallWorkLease", currentWork);
            CompleteBatch(tab, current, true, "New", null);
            Application.DoEvents();
            Check(!tab.IsBatchRunning && install.Enabled && (int)Field(tab, "_batchSucceeded") == 1, "current work did not finish normally");
        }
    }

    private static void NexusBatchQueue()
    {
        NexusModsTab tab = new NexusModsTab();
        int finished = 0;
        tab.BatchInstallFinished += delegate { finished++; };
        StartBatch(tab, "One", "Two", "Three");
        Check(tab.IsBatchRunning, "running");
        List<string> seen = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            object current = Field(tab, "_batchCurrent");
            seen.Add((string)current.GetType().GetField("Name").GetValue(current));
            CompleteBatch(tab, current, i != 1, null, i == 1 ? "network" : null);
        }
        Same(new List<string> { "One", "Two", "Three" }, seen, "installs in order");
        Check(!tab.IsBatchRunning && finished == 1, "finished " + finished + " times");
        Check((int)Field(tab, "_batchSucceeded") == 2 && ((IList)Field(tab, "_batchFailures")).Count == 1, "tally");
        CompleteBatch(tab, null, true, "late", null);
        Check(finished == 1 && (int)Field(tab, "_batchSucceeded") == 2, "a result after the queue ended counted");

        StartBatch(tab);
        Check(!tab.IsBatchRunning && finished == 2, "an empty queue finishes straight away");
    }

    private static void NexusBatchStop()
    {
        NexusModsTab tab = new NexusModsTab();
        int finished = 0;
        tab.BatchInstallFinished += delegate { finished++; };
        StartBatch(tab, "One", "Two", "Three");
        object stopped = Field(tab, "_batchCurrent");
        Call(tab, "StopBatch", "stopped");
        Check(!tab.IsBatchRunning && finished == 1, "stop");
        CompleteBatch(tab, stopped, true, "One", null);
        Check(finished == 1 && !tab.IsBatchRunning, "the in-flight result after stopping restarted the queue");
    }

    private static void NexusBatchBusy()
    {
        NexusModsTab tab = new NexusModsTab();
        int finished = 0;
        tab.BatchInstallFinished += delegate { finished++; };
        List<NexusModReference> references = new List<NexusModReference> { new NexusModReference { GameDomain = "sheltered", ModId = 5 } };
        Check(!tab.InstallReferences(null) && !tab.InstallReferences(new List<NexusModReference>()), "nothing to install still started");
        Check(!tab.InstallReferences(references) && !tab.IsBatchRunning, "installs started without Nexus set up");

        StartBatch(tab, "One", "Two");
        object current = Field(tab, "_batchCurrent");
        Check(!tab.InstallReferences(references), "a second request started while installing");
        StartBatch(tab, "Replacement");
        Check(Field(tab, "_batchCurrent") == current && ((ICollection)Field(tab, "_batchQueue")).Count == 1, "a running batch was replaced");
        tab.StartUpdateAll();
        Check(Field(tab, "_batchCurrent") == current, "Update all interrupted a running batch");

        CompleteBatch(tab, Field(tab, "_batchCurrent"), true, "One", null);
        CompleteBatch(tab, Field(tab, "_batchCurrent"), true, "Two", null);
        Check(!tab.IsBatchRunning && finished == 1 && (int)Field(tab, "_batchSucceeded") == 2, "the original batch did not finish normally");

        // While Update all is still checking, nothing else may start.
        SetField(tab, "_batchPreparing", true);
        Check(!tab.InstallReferences(references), "a list install started during the update check");
        SetField(tab, "_batchPreparing", false);
    }

    private static NexusModsTab NewNexusSelectionTab(bool signedIn)
    {
        AppSettings settings = new AppSettings { ModsPath = NewDir("nexus-selection"), EnableNexusIntegration = true };
        if (signedIn)
        {
            settings.NexusOAuthTokens.AccessToken = "fixture-access";
            settings.NexusOAuthTokens.RefreshToken = "fixture-refresh";
            settings.NexusOAuthTokens.ExpiresAtUtc = DateTime.UtcNow.AddHours(1);
        }
        NexusModsTab tab = new NexusModsTab();
        // No service is attached: these UI fixtures cannot issue a network request.
        tab.Initialize(null, settings, "2.0.0");
        tab.UpdateInstalledMods(new List<ModItem>
        {
            new ModItem("test.alpha", "Alpha", "") { NexusGameDomain = "sheltered", NexusModId = 4, HasUpdateAvailable = true },
            new ModItem("test.beta", "Beta", "") { NexusGameDomain = "sheltered", NexusModId = 5, HasUpdateAvailable = true }
        }, 2, 2, null);
        ListBox list = (ListBox)Field(tab, "_primaryList");
        IntPtr handle = list.Handle;
        list.SelectedIndex = 0;
        Check(((Control)Field(tab, "_installSelectedButton")).Enabled, "idle install action is not enabled");
        return tab;
    }

    private static void NexusBatchSelectionActions()
    {
        foreach (bool signedIn in new bool[] { true, false })
        using (NexusModsTab tab = NewNexusSelectionTab(signedIn))
        {
            StartBatch(tab, "One", "Two");
            Button install = (Button)Field(tab, "_installSelectedButton");
            Button stop = (Button)Field(tab, "_updateAllButton");
            Check(tab.IsBatchRunning && !install.Enabled, "batch start left manual install enabled");
            ((ListBox)Field(tab, "_primaryList")).SelectedIndex = 1;
            Check(!install.Enabled, "selection re-enabled install during a batch");
            ModDetailsPanel details = (ModDetailsPanel)Field(tab, "_detailsPanel");
            Check(!((Control)Field(details, "_nexusUpdateButton")).Visible, "details offered a second install during a batch");
            Check(stop.Enabled && stop.Text == "Stop (1 of 2)", "selection lost the Stop action");
            tab.UpdateInstalledMods(new List<ModItem>
            {
                new ModItem("test.beta", "Beta refreshed", "") { NexusGameDomain = "sheltered", NexusModId = 5, HasUpdateAvailable = true }
            }, 1, 1, null);
            ((ListBox)Field(tab, "_primaryList")).SelectedIndex = 0;
            Check(!install.Enabled && stop.Text == "Stop (1 of 2)", "installed refresh lost batch gating");
            CompleteBatch(tab, Field(tab, "_batchCurrent"), true, "One", null);
            Check(tab.IsBatchRunning && !install.Enabled && stop.Text == "Stop (2 of 2)", "queue advance lost batch gating");
            CompleteBatch(tab, Field(tab, "_batchCurrent"), true, "Two", null);
            Check(!tab.IsBatchRunning && install.Enabled && stop.Enabled, "completion did not restore manual actions");
            ((ListBox)Field(tab, "_primaryList")).ClearSelected();
            ((ListBox)Field(tab, "_primaryList")).SelectedIndex = 0;
            Check(((Control)Field(details, "_nexusUpdateButton")).Visible, "details actions did not recover after completion");
            StartBatch(tab, "Three");
            Check(!install.Enabled, "a later batch left install enabled");
            Call(tab, "StopBatch", "stopped");
            Check(!tab.IsBatchRunning && install.Enabled, "stopping did not restore manual actions");
        }
    }

    private static void NexusPreparingSelectionActions()
    {
        foreach (bool signedIn in new bool[] { true, false })
        using (NexusModsTab tab = NewNexusSelectionTab(signedIn))
        {
            SetField(tab, "_batchPreparing", true);
            Call(tab, "UpdateActionButtons");
            Button install = (Button)Field(tab, "_installSelectedButton");
            Button updateAll = (Button)Field(tab, "_updateAllButton");
            Check(!install.Enabled && !updateAll.Enabled, "preparation left install actions enabled");
            ((ListBox)Field(tab, "_primaryList")).SelectedIndex = 1;
            Check(!install.Enabled && !updateAll.Enabled, "selection re-enabled actions during preparation");
            Check(!((ModDetailsPanel)Field(tab, "_detailsPanel")).ShowNexusActionButtons, "details offered installs during preparation");
            SetField(tab, "_batchPreparing", false);
            Call(tab, "UpdateActionButtons");
            Check(install.Enabled && updateAll.Enabled, "cancelled preparation did not restore actions");
        }
    }

    private static void NexusBusyManualRequests()
    {
        foreach (bool preparing in new bool[] { false, true })
        using (NexusModsTab tab = NewNexusSelectionTab(true))
        {
            if (preparing)
                SetField(tab, "_batchPreparing", true);
            else
                StartBatch(tab, "One", "Two");
            object current = Field(tab, "_batchCurrent");
            foreach (string handler in new string[] { "InstallSelectedButton_Click", "DetailsPanel_NexusUpdateRequested", "DetailsPanel_NexusStableRequested" })
            {
                ModDetailsPanel details = (ModDetailsPanel)Field(tab, "_detailsPanel");
                details.ShowMessage("Fixture", "Before manual request");
                Call(tab, handler, tab, EventArgs.Empty);
                Check(((Label)Field(details, "_placeholderTitle")).Text == "Already installing", handler + " bypassed the busy gate");
                Check(Field(tab, "_batchCurrent") == current, handler + " changed the current batch install");
                Check((int)Field(tab, "_batchSucceeded") == 0, handler + " changed batch results");
            }
        }
    }

    private static void NexusBusySignInShortcut()
    {
        using (NexusModsTab tab = NewNexusSelectionTab(false))
        {
            int signIns = 0;
            tab.SignInRequested += delegate { signIns++; };
            SetField(tab, "_batchPreparing", true);
            Call(tab, "InstallSelectedButton_Click", tab, EventArgs.Empty);
            Check(signIns == 0, "sign-in shortcut bypassed preparation gating");
            SetField(tab, "_batchPreparing", false);
            StartBatch(tab, "One");
            Call(tab, "InstallSelectedButton_Click", tab, EventArgs.Empty);
            Check(signIns == 0, "sign-in shortcut bypassed active-batch gating");
            Call(tab, "StopBatch", "stopped");
            Call(tab, "InstallSelectedButton_Click", tab, EventArgs.Empty);
            Check(signIns == 1, "idle sign-in shortcut stopped working");
            tab.SetSignInAvailable(false);
            Call(tab, "InstallSelectedButton_Click", tab, EventArgs.Empty);
            Check(signIns == 1 && !((Control)Field(tab, "_installSelectedButton")).Enabled, "unavailable sign-in was offered");
        }
    }

    private static void NexusBatchCallbackActions()
    {
        using (NexusModsTab tab = NewNexusSelectionTab(true))
        {
            IntPtr handle = tab.Handle;
            Button install = (Button)Field(tab, "_installSelectedButton");
            bool enabledAtSuccess = true;
            bool enabledAtFailure = true;
            int completions = 0;
            int failures = 0;
            int finished = 0;
            tab.InstallCompleted += delegate { enabledAtSuccess = install.Enabled; completions++; };
            tab.NexusActivity += delegate(string message)
            {
                if (message == "fixture failure")
                {
                    enabledAtFailure = install.Enabled;
                    failures++;
                }
            };
            tab.BatchInstallFinished += delegate { finished++; };
            StartBatch(tab, "One", "Two");
            // Both callbacks marshal to the tab's UI thread. The null service prevents actual installs.
            CompleteInstall(tab, Field(tab, "_batchCurrent"), "One");
            Application.DoEvents();
            Check(completions == 1 && !enabledAtSuccess, "completion notification exposed a second manual install");
            Check(tab.IsBatchRunning && !install.Enabled && finished == 0, "completion lost gating before the next item");
            FailInstall(tab, Field(tab, "_batchCurrent"), "fixture failure");
            Application.DoEvents();
            Check(failures == 1 && !enabledAtFailure, "failure notification exposed a second manual install");
            Check(!tab.IsBatchRunning && install.Enabled && finished == 1, "last callback did not restore actions exactly once");
            Check((int)Field(tab, "_batchSucceeded") == 1 && ((IList)Field(tab, "_batchFailures")).Count == 1, "callback tally changed");
        }
    }

    private static void NexusChangelogSummary()
    {
        string[][] cases = new string[][]
        {
            new string[] { null, null },
            new string[] { "", null },
            new string[] { "<p></p><br/>", null },
            new string[] { "- Fixed crash<br />- Added beds", "Fixed crash" },
            new string[] { "<ul><li>New traps &amp; turrets</li><li>More</li></ul>", "New traps & turrets" },
            new string[] { "[b]Bold[/b] change [url=http://x]link[/url]", "Bold change link" },
            new string[] { "\r\n\r\n   * Star item", "Star item" },
            new string[] { new string('x', 300), new string('x', 107) + "..." },
            new string[] { "&lt;tag&gt; &quot;quoted&quot; &#39;single&#39;&nbsp;space", "<tag> \"quoted\" 'single' space" }
        };
        foreach (string[] c in cases)
        {
            string result = (string)Call(typeof(NexusModsTab), "SummarizeChangelog", c[0]);
            Check(result == c[1], "SummarizeChangelog(\"" + c[0] + "\") was \"" + result + "\"");
        }
    }

    // ================================================================== independent check samples

    /// <summary>Writes zips for the runner to open with System.IO.Compression, a reader independent of SMM.</summary>
    private static void WriteIndependentSamples(string folder)
    {
        Directory.CreateDirectory(folder);
        WriteZip(Path.Combine(folder, "deflated.zip"), true, "logs/big.log", BigText(20000), "empty.txt", "", "\u00FCnic\u00F8de/\u6587.txt", "unicode");
        WriteZip(Path.Combine(folder, "stored.zip"), false, "About/About.json", AboutJson("tester.sample", "Sample", "1.0"));
        File.WriteAllText(Path.Combine(folder, "expected-big.log"), BigText(20000), new UTF8Encoding(false));
    }
}
