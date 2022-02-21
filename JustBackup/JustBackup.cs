using Dalamud.Configuration;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Plugin;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading;

namespace JustBackup
{
    public class JustBackup : IDalamudPlugin
    {
        public string Name => "JustBackup";
        internal Config config;
        WindowSystem windowSystem;
        ConfigWindow configWindow;

        public JustBackup(DalamudPluginInterface pluginInterface)
        {
            pluginInterface.Create<Svc>();
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
            Svc.PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
            Svc.Commands.RemoveHandler("/justbackup");
        }

        internal string GetBackupPath()
        {
            return config.BackupPath != string.Empty ? config.BackupPath : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JustBackup");
        }

        void DoBackup()
        {
            var path = GetBackupPath();
            var stamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            var temp = Path.Combine(Path.GetTempPath(), $"JustBackup-{stamp}");
            var ffxivcfg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
                "My Games", "FINAL FANTASY XIV - A Realm Reborn");
            DirectoryInfo pluginsConfigsDir = null;
            if (config.BackupPluginConfigs)
            {
                try
                {
                    var c = Svc.PluginInterface.GetType().GetField("configs", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Svc.PluginInterface);
                    pluginsConfigsDir = (DirectoryInfo)c.GetType().GetField("configDirectory", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(c);
                }
                catch(Exception e)
                {
                    PluginLog.Error($"Can't back up plugin configurations: {e.Message}\n{e.StackTrace ?? ""}");
                    Svc.PluginInterface.UiBuilder.AddNotification("Error creating plugin configuration backup:\n" + e.Message, this.Name, NotificationType.Error);
                }
            }
            var daysToKeep = TimeSpan.FromDays(config.DaysToKeep);
            var backupAll = config.BackupAll;
            PluginLog.Information($"Backup path: {path}\nTemp folder: {temp}\nFfxiv config folder: {ffxivcfg}\nPlugin config folder: {pluginsConfigsDir?.FullName}");
            new Thread(() =>
            {
                try
                {
                    CloneDirectory(ffxivcfg, Path.Combine(temp, "game"), backupAll);
                    if(pluginsConfigsDir != null) CloneDirectory(pluginsConfigsDir.FullName, Path.Combine(temp, "plugins"), true);

                    if (!Directory.Exists(path))
                    {
                        PluginLog.Information($"Creating {path}");
                        var di = Directory.CreateDirectory(path);
                    }
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
                        szinfo.ArgumentList.Add("-mmt1");
                        szinfo.ArgumentList.Add("-mx9");
                        szinfo.ArgumentList.Add("-t7z");
                        szinfo.ArgumentList.Add(outfile + ".7z");
                        szinfo.ArgumentList.Add(Path.Combine(temp, "*"));
                        var szproc = new Process()
                        {
                            StartInfo = szinfo
                        };
                        szproc.OutputDataReceived += (sender, args) => PluginLog.Information("7-zip output: {0}", args.Data);
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
                    foreach (var file in Directory.GetFiles(path))
                    {
                        if(DateTimeOffset.TryParseExact(Path.GetFileName(file).Replace("Backup-FFXIV-", "").Replace(".zip", "").Replace(".7z", ""), "yyyy-MM-dd HH-mm-ss-fffffff", CultureInfo.InvariantCulture.DateTimeFormat, DateTimeStyles.AssumeLocal, out var time))
                        {
                            if(DateTimeOffset.Now.ToUnixTimeSeconds() > time.ToUnixTimeSeconds() + (long)daysToKeep.TotalSeconds)
                            {
                                PluginLog.Information($"Deleting outdated backup {file}.");
                                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(file,
                                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                            }
                        }
                    }
                    new TickScheduler(delegate
                    {
                        Svc.PluginInterface.UiBuilder.AddNotification("Backup created successfully!", this.Name, NotificationType.Success);
                    }, Svc.Framework);
                }
                catch(Exception ex)
                {
                    PluginLog.Error($"Error creating backup: {ex.Message}\n{ex.StackTrace ?? ""}");
                    new TickScheduler(delegate
                    {
                        Svc.PluginInterface.UiBuilder.AddNotification("Error creating backup:\n" + ex.Message, this.Name, NotificationType.Error);
                    }, Svc.Framework);
                }
                try
                {
                    Directory.Delete(temp, true);
                }
                catch (Exception ex)
                {
                    PluginLog.Error($"Error deleting temp files: {ex.Message}\n{ex.StackTrace ?? ""}");
                    new TickScheduler(delegate
                    {
                        Svc.PluginInterface.UiBuilder.AddNotification("Error deleting temp files:\n" + ex.Message, this.Name, NotificationType.Error);
                    }, Svc.Framework);
                }
            }).Start();
        }

        void CloneDirectory(string root, string dest, bool all)
        {
            foreach (var directory in Directory.GetDirectories(root))
            {
                string dirName = Path.GetFileName(directory);
                //if (dirName.Equals("splatoon", StringComparison.OrdinalIgnoreCase)) continue; //don't need to backup backups
                var path = Path.Combine(dest, dirName);
                if (!Directory.Exists(path))
                {
                    PluginLog.Information($"Creating {path}");
                    Directory.CreateDirectory(path);
                }
                CloneDirectory(directory, path, all);
            }

            foreach (var file in Directory.GetFiles(root))
            {
                if(all || file.EndsWith(".dat", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".cfg", StringComparison.OrdinalIgnoreCase))
                {
                    PluginLog.Information($"Copying from {file} to {dest}");
                    CopyFile(file, dest);
                }
            }
        }

        static void CopyFile(string file, string dest)
        {
            using var inputFile = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var outputFile = new FileStream(Path.Combine(dest, Path.GetFileName(file)), FileMode.Create);
            var size = inputFile.Length;
            var content = new byte[size];
            inputFile.Read(content, 0, (int)size);
            outputFile.Write(content, 0, (int)size);
        }
    }
}
