# Sheltered ModManager
Welcome to the repository of the Sheltered ModManager!

This project currently is in an early phase of development so there are bugs, expect crashes and npes.

The ModManager consists of three modules (Doorstop, ModAPI and ManagerGUI) which will be described below.

## Architecture
**Doorstop**\
This directory contains the Doorstop-Loader which initializes the ModAPI.
This project acts as the bridge between the operating-system and the sheltered-engine.

**ModAPI**\
This project contains the Plug-In-Architecture and the Pluginmanager, which loads and runs the assemblies from within the game-directory on launch.

**ManagerGUI**\
The User-Interface of the application which allows the User to locate and launch the game with modding-support enabled.

### Plugins
**PluginInitializer**\
Demo plugin, gets executed on launch.

**PluginDebugGUI**\
Shows a unity-window within the game.

## Installation
* make a backup of the complete game (zip the contents of the whole sheltered-directory)
* download the latest doorstop-release (2.7) and extract its content to  the games root-directory
* clone this directory, open in JetBrains Rider or Visual Studio 2017 
* TODO: describe which contents have to be copied to which place
* launch the mod-manager


## Notes
This project is 32-bit only atm - because the Steam-release only contains 32-bit assemblies.

## Attribution
The following frameworks and tools, applications, etc. were used within this project, thanks to: 
* [Sheltered](https://store.steampowered.com/app/356040/Sheltered/)
* [UnityDoorstop](https://github.com/NeighTools/UnityDoorstop)
* [JetBrains Rider](https://www.jetbrains.com/rider/)
* [dnSpy](https://github.com/0xd4d/dnSpy/releases)



### Testing
This project has been tested on
* Windows 10, 64-bit
* Doorstop 2.7.0.0, 32-bit
* Sheltered 1.8 (from Steam, 32-bit)
