using Dalamud.Interface.Internal.Notifications;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Plugin;
using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
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
            var daysToKeep = TimeSpan.FromDays(config.DaysToKeep);
            PluginLog.Information($"Backup path: {path}\nTemp folder: {temp}\nFfxiv config folder: {ffxivcfg}");
            new Thread(() =>
            {
                try
                {
                    CloneDirectory(ffxivcfg, temp);
                    if (!Directory.Exists(path))
                    {
                        PluginLog.Information($"Creating {path}");
                        Directory.CreateDirectory(path);
                    }
                    ZipFile.CreateFromDirectory(temp, Path.Combine(path,
                        $"Backup-game-{DateTimeOffset.Now:yyyy-MM-dd HH-mm-ss-fffffff}.zip"), CompressionLevel.Optimal, false);
                    Directory.Delete(temp, true);
                    PluginLog.Information("Backup complete.");
                    foreach (var file in Directory.GetFiles(path))
                    {
                        if(DateTimeOffset.TryParseExact(Path.GetFileName(file).Replace("Backup-game-", "").Replace(".zip", ""), "yyyy-MM-dd HH-mm-ss-fffffff", CultureInfo.InvariantCulture.DateTimeFormat, DateTimeStyles.AssumeLocal, out var time))
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
            }).Start();
        }

        void CloneDirectory(string root, string dest)
        {
            foreach (var directory in Directory.GetDirectories(root))
            {
                string dirName = Path.GetFileName(directory);
                var path = Path.Combine(dest, dirName);
                if (!Directory.Exists(path))
                {
                    PluginLog.Information($"Creating {path}");
                    Directory.CreateDirectory(path);
                }
                CloneDirectory(directory, path);
            }

            foreach (var file in Directory.GetFiles(root))
            {
                if(file.EndsWith(".dat", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".cfg", StringComparison.OrdinalIgnoreCase))
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
