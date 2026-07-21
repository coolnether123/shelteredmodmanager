# Sheltered Mod Manager Updater

`ManagerUpdater.exe` is a standalone .NET Framework 3.5 Windows executable. It
waits for the running manager to exit, swaps a staged SMM directory into place,
preserves machine-local state, and restarts `Manager.exe`.

```text
ManagerUpdater.exe --parent-pid <pid> --current <SMM directory> ^
  --staged <staged SMM directory> --backup <backup directory> ^
  --restart <current SMM Manager.exe>
```

All paths must be absolute and separate. The current and backup directories must
be on the same Windows volume. Cross-volume staging is copied to a promotion
directory on the installation volume before the current manager is changed. The
backup path must not already exist, the staged directory must contain
`Manager.exe`, and the restart path must be the `Manager.exe` directly inside
the current SMM path.

The updater preserves these items from the previous installation:

- `bin/mod_manager.ini`
- `bin/manager_options.json`
- `mod_manager.log`
- `Feedback/`

On any failure after the swap starts, the updater moves the staged installation
out of the way and restores the backup. Logs are written outside the SMM
installation under `%TEMP%\ShelteredModManager`. Run with `--help` to display
the command syntax.
