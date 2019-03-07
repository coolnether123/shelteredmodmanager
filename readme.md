<img src="/documentation/logo.png"> 

# Sheltered ModManager
This project aims to enable modding-support for the game [Sheltered](https://store.steampowered.com/app/356040/Sheltered/) by Team17\
The project acts as in drop-in application to a regular installation of Sheltered - no files are touched during the whole lifecycle of the application.

This project consists of the following modules which are described in the following paragraphs.

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
This plugin is used to print the string "Modding-API active" on the screen.
The purpose of this plugin is to signalize that the plugin-mechanism works as early as possible, as the console is not 
available currently.

**PluginConsole**\
This plugin contains a custom console and userinterface. It can be used to interact with the application, the engine, etc.

**PluginHarmony**\
This plugin contains the the c#-patching-library Harmony (https://github.com/pardeike/Harmony) 

**PluginDebugGUI**\
This plugin contains a userinterface which visualizes different informations of the application and a summary of the loaded and executed plugins.

## Installation
* make a backup of the complete game (zip the contents of the whole sheltered-directory)
* clone this directory, open in Visual Studio 2017 or JetBrains Rider
* build the whole solution
* at the projet-root, there is a directory with the name "Dist"
* it contains all the files which are provided by this project - copy them to the root of your sheltered-installation.
* execute the launcher in <game_root>\SMM\Manager.exe

## Compilation
* **Notes:** 
This project is 32-bit only atm - because the Steam-release only contains 32-bit assemblies.

## Screenshots
This are screenshots which were made during development:

<img src="/documentation/manager_gui.png"> 

<img src="/documentation/ingame.png"> 


## Credits & Copyright
The following frameworks and libraries where used the development of this project, so big thanks to: 
* [Team 17 for Sheltered](https://store.steampowered.com/app/356040/Sheltered/)
* [NeighTools for UnityDoorstop](https://github.com/NeighTools/UnityDoorstop)
* [Pardeike for Harmony](https://github.com/pardeike/Harmony)

### Testing
This project has been tested on
* Windows 10, 64-bit
* Doorstop 2.7.0.0, 32-bit
* Sheltered 1.8 (from Steam, 32-bit)
* Harmony.Lib 1.2.0.1
