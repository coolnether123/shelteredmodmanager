using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Manager.Core.Services;
using Smm.FirstRun;

namespace Manager
{
    public partial class MainForm
    {
        private string _preparedGamePath;

        private bool PrepareFirstRun(bool refreshTabs)
        {
            if (!_settings.IsGamePathValid) return false;
            string gamePath = _settings.GamePath;
            if (string.Equals(_preparedGamePath, gamePath, StringComparison.OrdinalIgnoreCase)) return true;
            if (IsShelteredRunning())
            {
                if (refreshTabs)
                    MessageBox.Show(this, "Close Sheltered before preparing the first 2.0 backup.", "Sheltered is running");
                return false;
            }

            try
            {
                var backup = new PreUpgradeSaveBackup(Path.GetDirectoryName(gamePath));
                backup.Ensure();
                var migration = new LegacyInstallMigration(gamePath);
                if (migration.IsLegacyRoot)
                    _gameSetupTab.Log("Found Sheltered Mod Manager 1.2.2. Your save backup is ready.");

                bool imported = false;
                bool deferred = false;
                foreach (string wrapper in migration.FindWrappers())
                {
                    if (migration.WasImported(wrapper)) continue;
                    if (migration.Import(wrapper, ChooseFirstRunData))
                        imported = true;
                    else
                        deferred = true;
                }

                if (imported)
                {
                    var reloaded = _settingsService.Load();
                    reloaded.GamePath = gamePath;
                    reloaded.ModsPath = Path.Combine(Path.GetDirectoryName(gamePath), "mods");
                    ApplyInstalledApiVersions(reloaded, DetectInstalledApiVersions(reloaded));
                    if (refreshTabs)
                        SettingsService_SettingsChanged(reloaded);
                    else
                    {
                        _settings = reloaded;
                        RecreateNexusService();
                    }
                    MessageBox.Show(this,
                        "Your old files were imported. You can move the old 1.2.2 folder into backups from Settings.",
                        "Import complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                if (!deferred) _preparedGamePath = gamePath;
                return !deferred;
            }
            catch (Exception ex)
            {
                Trace.TraceError("First-run preparation failed: " + ex);
                MessageBox.Show(this,
                    "Sheltered Mod Manager could not finish the first-run backup or import. Close Sheltered and check that the game folder is writable and has room for a backup. Try Play again when ready.",
                    "First-run setup needs attention", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        private MigrationChoice ChooseFirstRunData()
        {
            var choice = MessageBox.Show(this,
                "There is player data in Sheltered and in the old 1.2.2 folder. Mods found only in the old folder will be copied. If both folders have a mod with the same folder name, your choice decides which copy to use. It also decides which saved mod order and enabled list to use when both have one. Replaced files are kept in backups.\n\n"
                + "Yes: Use 1.2.2 settings and mod choices.\n"
                + "No: Keep current settings and mod choices.\n"
                + "Cancel: Decide later.",
                "Import from Sheltered Mod Manager 1.2.2", MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question, MessageBoxDefaultButton.Button3);
            if (choice == DialogResult.Yes) return MigrationChoice.UseOld;
            return choice == DialogResult.No ? MigrationChoice.KeepCurrent : MigrationChoice.Later;
        }

        private void UpdateFirstRunActions()
        {
            if (!_settings.IsGamePathValid)
            {
                _settingsTab.SetFirstRunActions(false, false);
                return;
            }

            try
            {
                var backup = new PreUpgradeSaveBackup(Path.GetDirectoryName(_settings.GamePath));
                var migration = new LegacyInstallMigration(_settings.GamePath);
                _settingsTab.SetFirstRunActions(backup.HasBackup, migration.FindWrappers().Any(migration.WasImported));
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Could not inspect first-run backups: " + ex);
                _settingsTab.SetFirstRunActions(false, false);
            }
        }

        private void RestoreFirstRunBackup()
        {
            if (!_settings.IsGamePathValid || !ConfirmClosedForFirstRunAction()) return;
            if (MessageBox.Show(this,
                "Restore the files saved before 2.0? Matching files will be replaced. Newer saves without a match will stay in place. A separate backup will keep your current saves.",
                "Restore pre-2.0 backup", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            if (!ConfirmClosedForFirstRunAction()) return;

            try
            {
                new PreUpgradeSaveBackup(Path.GetDirectoryName(_settings.GamePath)).Restore();
                MessageBox.Show(this, "Your pre-2.0 save files have been restored.", "Restore complete");
            }
            catch (Exception ex)
            {
                Trace.TraceError("Pre-2.0 restore failed: " + ex);
                MessageBox.Show(this, "The restore could not finish. Check that the game folder is writable and has free space, then try again.",
                    "Restore interrupted", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void TidyOldInstall()
        {
            if (!_settings.IsGamePathValid || !ConfirmClosedForFirstRunAction()) return;
            if (MessageBox.Show(this, "Move the imported 1.2.2 folders to dated backups?",
                "Tidy up old 1.2.2 files", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            if (!ConfirmClosedForFirstRunAction()) return;

            try
            {
                var migration = new LegacyInstallMigration(_settings.GamePath);
                foreach (string wrapper in migration.FindWrappers().Where(migration.WasImported))
                    migration.Tidy(wrapper);
                MessageBox.Show(this, "Your old 1.2.2 files were moved to backups.", "Tidy up complete");
            }
            catch (Exception ex)
            {
                Trace.TraceError("Could not move the old 1.2.2 folders: " + ex);
                MessageBox.Show(this, "The old folders could not all be moved. Close any apps using them, then try again.",
                    "Tidy up interrupted", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            UpdateFirstRunActions();
        }

        private bool ConfirmClosedForFirstRunAction()
        {
            if (!IsShelteredRunning()) return true;
            MessageBox.Show(this, "Close Sheltered before changing its files.", "Sheltered is running");
            return false;
        }

        private static bool IsShelteredRunning()
        {
            return IsProcessRunning("Sheltered") || IsProcessRunning("ShelteredWindows64_EOS");
        }
    }
}
