using Dalamud.Configuration;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Plugin;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading;
using ECommons.Schedulers;

namespace JustBackup
{
    public class JustBackup : IDalamudPlugin
    {
        public string Name => "JustBackup";
        const string UrlFileName = "How to restore a backup.url";
        internal Config config;
        WindowSystem windowSystem;
        ConfigWindow configWindow;

        public JustBackup(DalamudPluginInterface pluginInterface)
        {
            ECommons.ECommons.Init(pluginInterface);
            config = Svc.PluginInterface.GetPluginConfig() as Config ?? new Config();
            windowSystem = new();
            configWindow = new(this);
            windowSystem.AddWindow(configWindow);
            Svc.PluginInterface.UiBuilder.Draw += windowSystem.Draw;
            DoBackup();
            Svc.PluginInterface.UiBuilder.OpenConfigUi += delegate { configWindow.IsOpen = true; };
            Svc.Commands.AddHandler("/justbackup", new Dalamud.Game.Command.CommandInfo(delegate { DoBackup(); })
            {
                HelpMessage = "do a manual backup"
            });
        }

        public void Dispose()
        {
            ECommons.ECommons.Dispose();
            Svc.PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
            Svc.Commands.RemoveHandler("/justbackup");
        }

        internal string GetBackupPath()
        {
            return config.BackupPath != string.Empty ? config.BackupPath : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JustBackup");
        }

        void DoBackup()
        {
            if (config.DaysToKeep < 0)
            {
                PluginLog.Warning($"config.DaysToKeep={config.DaysToKeep}, which is invalid value and is reset to 0.");
                config.DaysToKeep = 0;
            }
            if (config.DaysToKeep > 730)
            {
                PluginLog.Warning($"config.DaysToKeep={config.DaysToKeep}, which is invalid value and is reset to 730.");
                config.DaysToKeep = 730;
            }
            if (config.BackupsToKeep < 0)
            {
                PluginLog.Warning($"config.BackupsToKeep={config.BackupsToKeep}, which is invalid value and is reset to 0.");
                config.BackupsToKeep = 0;
            }
            var path = GetBackupPath();
            var stamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            var temp = Path.Combine(Path.GetTempPath(), $"JustBackup-{stamp}");
            var ffxivcfg = GetFFXIVConfigFolder();
            DirectoryInfo pluginsConfigsDir = null;
            if (config.BackupPluginConfigs)
            {
                try
                {
                    pluginsConfigsDir = GetPluginsConfigDir();
                }
                catch(Exception e)
                {
                    PluginLog.Error($"Can't back up plugin configurations: {e.Message}\n{e.StackTrace ?? ""}");
                    Svc.PluginInterface.UiBuilder.AddNotification("Error creating plugin configuration backup:\n" + e.Message, this.Name, NotificationType.Error);
                }
            }
            var daysToKeep = TimeSpan.FromDays(config.DaysToKeep);
            var backupAll = config.BackupAll;
            var toKeep = config.BackupsToKeep;
            var enableDelete = config.DeleteBackups;
            var unlimited = config.NoThreadLimit;
            PluginLog.Information($"Backup path: {path}\nTemp folder: {temp}\nFfxiv config folder: {ffxivcfg}\nPlugin config folder: {pluginsConfigsDir?.FullName}");
            new Thread(() =>
            {
                try
                {
                    var gameSuccess = CloneDirectory(ffxivcfg, Path.Combine(temp, "game"), backupAll);
                    var pluginSuccess = true;
                    if (pluginsConfigsDir != null)
                    {
                        pluginSuccess = CloneDirectory(pluginsConfigsDir.FullName, Path.Combine(temp, "plugins"), true);
                    }

                    if (!Directory.Exists(path))
                    {
                        PluginLog.Verbose($"Creating {path}");
                        var di = Directory.CreateDirectory(path);
                    }
                    try
                    {
                        var appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                        var xivlauncherDir = Path.Combine(appDataDir, "XIVLauncher");
                        var ConfigurationPath = Path.Combine(xivlauncherDir, "dalamudConfig.json");
                        PluginLog.Verbose($"Copying from {ConfigurationPath} to {temp}");
                        CopyFile(ConfigurationPath, temp);
                    }
                    catch(Exception ex)
                    {
                        PluginLog.Error($"Error copying Dalamud configuration: {ex.Message}\n{ex.StackTrace ?? ""}");
                    }
                    CopyFile(Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName, UrlFileName), temp);
                    var outfile = Path.Combine(path, $"Backup-FFXIV-{DateTimeOffset.Now:yyyy-MM-dd HH-mm-ss-fffffff}");
                    if (config.UseDefaultZip)
                    {
                        ZipFile.CreateFromDirectory(temp, outfile + ".zip", CompressionLevel.Optimal, false);
                    }
                    else
                    {
                        var szinfo = new ProcessStartInfo()
                        {
                            FileName = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName, "7zr.exe"),
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true
                        };
                        //a -m0=LZMA2 -mmt1 -mx9 -t7z "H:\te mp\test1.7z" "c:\vs\NotificationMaster"
                        szinfo.ArgumentList.Add("a");
                        szinfo.ArgumentList.Add("-m0=LZMA2");
                        if(!unlimited) szinfo.ArgumentList.Add("-mmt1");
                        szinfo.ArgumentList.Add("-mx9");
                        szinfo.ArgumentList.Add("-t7z");
                        szinfo.ArgumentList.Add(outfile + ".7z");
                        szinfo.ArgumentList.Add(Path.Combine(temp, "*"));
                        var szproc = new Process()
                        {
                            StartInfo = szinfo
                        };
                        szproc.OutputDataReceived += (sender, args) => PluginLog.Debug("7-zip output: {0}", args.Data);
                        szproc.Start();
                        szproc.PriorityClass = ProcessPriorityClass.BelowNormal;
                        szproc.BeginOutputReadLine();
                        szproc.WaitForExit();
                        if (szproc.ExitCode != 0)
                        {
                            throw new Exception("7-zip exited with error code=" + szproc.ExitCode);
                        }
                    }
                    PluginLog.Information("Backup complete.");
                    if (enableDelete)
                    {
                        PluginLog.Debug($"Beginning auto-deletion of old backups");
                        var fileList = new List<(string path, DateTimeOffset time)>();
                        foreach (var file in Directory.GetFiles(path))
                        {
                            if (DateTimeOffset.TryParseExact(Path.GetFileName(file).Replace("Backup-FFXIV-", "").Replace(".zip", "").Replace(".7z", ""), "yyyy-MM-dd HH-mm-ss-fffffff", CultureInfo.InvariantCulture.DateTimeFormat, DateTimeStyles.AssumeLocal, out var time))
                            {
                                fileList.Add((file, time));
                            }
                        }
                        if (fileList.Count <= toKeep)
                        {
                            PluginLog.Debug($"{fileList.Count} backups found, {toKeep} ordered to be kept, not deleting anything");
                        }
                        else
                        {
                            foreach (var file in fileList.OrderByDescending(x => x.time).ToArray()[toKeep..])
                            {
                                if (DateTimeOffset.Now.ToUnixTimeSeconds() > file.time.ToUnixTimeSeconds() + (long)daysToKeep.TotalSeconds)
                                {
                                    PluginLog.Information($"Deleting outdated backup {file.path}.");
                                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(file.path,
                                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                                        Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                                }
                            }
                        }
                        PluginLog.Debug($"Finishing auto-deletion of old backups");
                    }
                    else
                    {
                        PluginLog.Debug("User disabled backup auto-deletion, skipping...");
                    }
                    if (pluginSuccess && gameSuccess)
                    {
                        Notify.Success("Backup created successfully!");
                    }
                    else
                    {
                        Notify.Warning("There were errors while creating backup, please check log");
                    }
                }
                catch(Exception ex)
                {
                    PluginLog.Error($"Error creating backup: {ex.Message}\n{ex.StackTrace ?? ""}");
                    Notify.Error("Could not create backup:\n" + ex.Message);
                }
                try
                {
                    Directory.Delete(temp, true);
                }
                catch (Exception ex)
                {
                    PluginLog.Error($"Error deleting temp files: {ex.Message}\n{ex.StackTrace ?? ""}");
                    Notify.Error("Error deleting temp files:\n" + ex.Message);
                }
            }).Start();
        }

        internal static DirectoryInfo GetPluginsConfigDir()
        {
            var c = Svc.PluginInterface.GetType().GetField("configs", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Svc.PluginInterface);
            return (DirectoryInfo)c.GetType().GetField("configDirectory", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(c);
        }

        internal static string GetFFXIVConfigFolder()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "My Games", "FINAL FANTASY XIV - A Realm Reborn");
        }

        bool CloneDirectory(string root, string dest, bool all)
        {
            var success = true;
            foreach (var directory in Directory.GetDirectories(root))
            {
                string dirName = Path.GetFileName(directory);
                //if (dirName.Equals("splatoon", StringComparison.OrdinalIgnoreCase)) continue; //don't need to backup backups
                var path = Path.Combine(dest, dirName);
                if (!Directory.Exists(path))
                {
                    PluginLog.Verbose($"Creating {path}");
                    Directory.CreateDirectory(path);
                }
                CloneDirectory(directory, path, all);
            }

            foreach (var file in Directory.GetFiles(root))
            {
                if(all || file.EndsWith(".dat", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".cfg", StringComparison.OrdinalIgnoreCase))
                {
                    PluginLog.Verbose($"Copying from {file} to {dest}");
                    if (!CopyFile(file, dest)) success = false;
                }
            }

            return success;
        }

        static bool CopyFile(string file, string dest)
        {
            try
            {
                using var inputFile = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var outputFile = new FileStream(Path.Combine(dest, Path.GetFileName(file)), FileMode.Create);
                var size = inputFile.Length;
                var content = new byte[size];
                inputFile.Read(content, 0, (int)size);
                outputFile.Write(content, 0, (int)size);
                return true;
            }
            catch(Exception e)
            {
                PluginLog.Error($"Error copying file {file} to {dest}: {e.Message}\n{e.StackTrace}");
                return false;
            }
        }
    }
}
