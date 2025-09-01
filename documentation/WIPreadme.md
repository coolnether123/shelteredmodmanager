# Sheltered Mod Manager (Enhanced POC)

This project is a Proof-of-Concept (POC) mod manager for the game [Sheltered](https://store.steampowered.com/app/356040/Sheltered/) by Team17. It acts as a drop-in application, designed to enable modding support without modifying the original game files.

## Overview

The Sheltered Mod Manager provides a graphical user interface (GUI) for managing game paths and installed mods. It features a robust plugin API, allowing developers to create custom mods that interact with the game. A key enhancement during this development cycle has been the implementation of a fully functional in-game console for debugging and command execution.

## Key Features

*   **GUI-based Management:** Easily locate your game executable and manage mod installations.
*   **Plugin API:** A flexible API (`ModAPI`) for developing custom game modifications.
*   **In-Game Console:** A newly implemented, extensible console for executing commands directly within the game.
*   **Mod Enable/Disable:** Functionality to activate and deactivate mods by moving them between 'enabled' and 'disabled' states.
*   **Automated Deployment:** Streamlined build process for easier installation.

## Installation

1.  **Backup your game:** It's always recommended to create a backup of your entire Sheltered game directory.
2.  **Clone this repository:** Get the project files onto your local machine.
3.  **Build the solution:** Open `ShelteredModManager.sln` in Visual Studio 2017+ or JetBrains Rider. Build the entire solution in `Debug` configuration.
4.  **Copy built files:** After a successful build, the `Dist` folder in the project root will contain all necessary files. Copy the *entire contents* of this `Dist` folder into the root of your Sheltered game installation (e.g., `D:\Epic Games\Sheltered\`). Overwrite any existing files.
5.  **Run the Manager:** Execute `SMM\Manager.exe` from your game directory.

## Building the Project

*   **Prerequisites:** Visual Studio 2017+ or JetBrains Rider.
*   **Target Framework:** The project targets .NET Framework 3.5.
*   **Architecture:** The `Doorstop` project is configured to build for `x64` to match the game's architecture.
*   **Automated Deployment:** The `ManagerGUI.csproj` includes a Post-Build Event that automatically copies the compiled files from `$(SolutionDir)\Dist` to your game directory (`D:\Epic Games\Sheltered\`) upon a successful build.

## Mod Development

Mods are implemented as plugins that adhere to the `ModAPI`'s `IPlugin` interface.

*   **Creating a Plugin:**
    1.  Create a new C# Class Library project.
    2.  Add a project reference to `ModAPI.csproj`.
    3.  Create a public class that implements the `IPlugin` interface.
    4.  Implement the `Name`, `Version`, `initialize()`, and `start(GameObject root)` methods.
    5.  Compile your project into a DLL and place it in the `mods/disabled` folder within your game's directory. Use the Manager GUI to enable it.
*   **Using Harmony:** The project integrates the Harmony library (`PluginHarmony`), allowing for powerful runtime patching of game methods.

## Key Improvements & Fixes Implemented

This project has undergone significant debugging and enhancement:

*   **Core Mod Loading Reliability:**
    *   **`PluginManager` Pathing:** Fixed the `PluginManager` to reliably locate the `mods` folder using `Application.dataPath`.
    *   **Architecture Compatibility:** Corrected `Doorstop.csproj` to compile `Doorstop.dll` for `x64` to match the game's 64-bit executable.
    *   **Doorstop Binary Update:** Integrated a 64-bit `winhttp.dll` (Doorstop injector) to resolve loading issues.
    *   **Non-Blocking Initialization:** Refactored `Loader.cs` to use Unity Coroutines for delayed plugin loading, preventing game freezes during startup.
    *   **Robust Logging (`MMLog`):** Implemented a custom `MMLog` utility for reliable file-based logging from all parts of the mod loader, crucial for debugging.

*   **In-Game Console System:**
    *   **Extensible Command Pattern:** Implemented `ICommand` interface and `CommandProcessor` for a scalable command system.
    *   **Core Commands:** Added `help`, `clear`, and `sceneinfo` commands.
    *   **UI Integration:** Integrated the `CommandProcessor` into `ConsoleWindowComponent` for interactive command execution.

*   **Manager GUI Enhancements:**
    *   **Correct Path Display:** Fixed the "Mods-Path" display in the GUI to accurately reflect the game's mod directory.
    *   **Mod Enable/Disable:** Implemented the functionality to move mods between "Available" (`mods/disabled`) and "Installed" (`mods/enabled`) lists, with corresponding file system operations.
    *   **DoubleClick Support:** Added `DoubleClick` event handlers to the mod list views for quick enabling/disabling.

*   **Build System Stability:**
    *   **Post-Build Event Fixes:** Resolved multiple errors in `ManagerGUI.csproj`'s Post-Build Event (removed broken `copy` commands).
    *   **Debugging Symbol Copying:** Ensured `Doorstop.pdb` and `PluginConsole.pdb` are copied for debugging.

## Troubleshooting

*   **No Mods Loading / No Logs:**
    *   Ensure you have copied the *entire contents* of the `Dist` folder to your game's root directory.
    *   Verify that the `winhttp.dll` in your game folder is a 64-bit version compatible with your game. This was a major hurdle, and sometimes requires trying different Doorstop releases.
    *   Check `mod_manager.log` (located in `Application.persistentDataPath` or the game's root) for detailed errors.
*   **Build Errors:** Ensure your project is targeting .NET Framework 3.5 and that all project references are correct. Perform a clean and rebuild if issues persist.

## Credits

*   **Original Sheltered Mod Manager Author:** benjaminfoo (https://github.com/benjaminfoo/shelteredmodmanager)
*   **Team17:** For the game Sheltered.
*   **NeighTools:** For Unity Doorstop (https://github.com/NeighTools/UnityDoorstop).
*   **Pardeike:** For Harmony (https://github.com/pardeike/Harmony).