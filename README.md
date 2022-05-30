# JustBackup
Dalamud plugin that automatically creates backup of your game and plugins configuration upon game starting.

## Restoring a backup
In the event something gone wrong, you can restore backup manually. Plugin can not restore your settings automatically because in most cases you need to have game closed while restoring.

Before you start:
1. Make sure you have closed the game before starting to restore any configuration files (there are exceptions to this step, read further)
2. Make sure you have suitable program to open 7-zip archives. You may download free 7-zip program to do so from it's official website: https://www.7-zip.org/

## Locating needed backup
By default backups are located in `%localappdata%\JustBackup`. You can also open backup folder from the plugin itself using "Open backup folder" button:

![image](Manual/image_467.png)

Inside that folder you will find number of recent backups. Most recent one will likely contain corrupted configuration; you will want to use previous one or go back few backups, depending how much times you have started the game after your configuration got corrupted.

![image](Manual/image_468.png)

Now just open it up and proceed to restoring files.

## Locating game configuration folder
Your game confguration is usually located in `%userprofile%\Documents\My Games\FINAL FANTASY XIV - A Realm Reborn` folder. You can as well use corresponding button inside JustBackup's configuration to open it up.

![image](Manual/image_469.png)

## Restoring whole game configuration at once
To do so, simply go into `game` folder inside backup archive, select everything and drag files into `FINAL FANTASY XIV - A Realm Reborn`, and confirm all requests to overwrite existing files. After that your game configuration will be restored to previous state and you may launch your game.

![image](Manual/image_470.png)

## Restoring specific character's configuration
If you have lost only one specific character's configuration settings and wish to restore it, you will need to locate your character's directory. To do so, follow these steps:
1. Log in on character whose settings you want to restore.
2. Open JustBackup's configuration and press "Open current character's config directory" button. Alternatively, use SimpleTweaks and it's "Character directory command" tweak to do so:

![image](Manual/image_471.png)

3. Log out. In this specific situation you do not have to close the whole game completely; logging out will be enough.
4. Locate folder with same name inside backup archive and copy it's contents over to the folder you have just opened up:

![image](Manual/image_472.png)

5. Confirm all requests to overwrite existing files. After copying is complete, you may log back on your character.

## Locating plugins configuration folder
Your plugins confguration is usually located in `%appdata%\XIVLauncher\pluginConfigs` folder. You can as well use corresponding button inside JustBackup's configuration to open it up:

![image](Manual/image_473.png)

## Restoring whole plugins configuration at once
To do so, firstly remove all the contents of `pluginConfigs` folder, or move it elsewhere. Then go into `plugins` folder inside backup archive, select everything and drag files into `pluginConfigs`. After that your plugins configuration will be restored to previous state and you may launch your game.

![image](Manual/image_474.png)
