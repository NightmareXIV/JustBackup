using ECommons.Logging;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading;
using ECommons.Schedulers;
using ECommons;
using ECommons.Funding;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Common;
using ECommons.Reflection;
using PInvoke;

namespace JustBackup;

public unsafe class JustBackup : IDalamudPlugin
{
    public string Name => "JustBackup";
    const string UrlFileName = "How to restore a backup.url";
    internal Config config;
    WindowSystem windowSystem;
    ConfigWindow configWindow;
    ModalWindowAskBackup askBackup;
    internal static JustBackup P;

    public JustBackup(IDalamudPluginInterface pluginInterface)
    {
        P = this;
        ECommonsMain.Init(pluginInterface, this);
        PatreonBanner.IsOfficialPlugin = () => true;
        config = Svc.PluginInterface.GetPluginConfig() as Config ?? new Config();
        windowSystem = new();
        configWindow = new(this);
        askBackup = new();
        windowSystem.AddWindow(configWindow);
        windowSystem.AddWindow(askBackup);
        Svc.PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        DoBackup();
        Svc.PluginInterface.UiBuilder.OpenConfigUi += delegate { configWindow.IsOpen = true; };
        Svc.Commands.AddHandler("/justbackup", new Dalamud.Game.Command.CommandInfo(delegate { DoBackup(true); })
        {
            HelpMessage = "do a manual backup"
        });
        InternalLog.Debug($"Processor count: {Environment.ProcessorCount}");
    }

    public void Dispose()
    {
        ECommonsMain.Dispose();
        Svc.PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        Svc.Commands.RemoveHandler("/justbackup");
        config = null;
        P = null;
    }

    internal string GetBackupPath()
    {
        return config.BackupPath != string.Empty ? config.BackupPath : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JustBackup");
    }

    internal void DoBackup(bool force = false)
    {
        if (!force && config.MinIntervalBetweenBackups > 0)
        {
            if(DateTimeOffset.Now.ToUnixTimeMilliseconds() - config.LastSuccessfulBackup < config.MinIntervalBetweenBackups * 60 * 1000)
            {
                Notify.Info($"Backup skipped because {config.MinIntervalBetweenBackups} minutes did not passed yet since last backup");
                return;
            }
        }
        var FileName = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName, "7zr.exe");
        bool isRunning = false;
        foreach(var x in Process.GetProcesses().Where(x => x.ProcessName.Contains("7zr")))
        {
            try
            {
                if (x.MainModule.FileName == FileName)
                {
                    isRunning = true;
                    break;
                }
            }
            catch(Exception e)
            {
                PluginLog.Debug($"Process {x.ProcessName}");
                e.LogWarning();
            }
        }
        if (isRunning)
        {
            askBackup.IsOpen = true;
        }
        else
        {
            DoBackupInternal();
        }
    }

    internal void DoBackupInternal()
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
        var temp = "";
        if (config.TempPath != "" && Directory.Exists(config.TempPath) && Utils.HasWriteAccessToFolder(config.TempPath))
        {
            temp = Path.Combine(config.TempPath, $"JustBackup-{stamp}");
        }
        else
        {
            temp = Path.Combine(Path.GetTempPath(), $"JustBackup-{stamp}");
        }
        PluginLog.Debug($"Temp path is determined as {temp}");
        config.TempPathes.Add(temp);
        Svc.PluginInterface.SavePluginConfig(config);
        var ffxivcfg = GetFFXIVConfigFolder();
        DirectoryInfo pluginsConfigsDir = null;
        if (config.BackupPluginConfigs)
        {
            try
            {
                pluginsConfigsDir = GetPluginsConfigDir();
            }
            catch (Exception e)
            {
                PluginLog.Error($"Can't back up plugin configurations: {e.Message}\n{e.StackTrace ?? ""}");
                Svc.PluginInterface.UiBuilder.AddNotification("Error creating plugin configuration backup:\n" + e.Message, this.Name, NotificationType.Error);
            }
        }
        var daysToKeep = TimeSpan.FromDays(config.DaysToKeep);
        var backupAll = config.BackupAll;
        var toKeep = config.BackupsToKeep;
        var enableDelete = config.DeleteBackups;
        var selectedRecycleOption = config.DeleteToRecycleBin
            ? Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin
            : Microsoft.VisualBasic.FileIO.RecycleOption.DeletePermanently;
        var unlimited = config.NoThreadLimit;
        PluginLog.Information($"Backup path: {path}\nTemp folder: {temp}\nFfxiv config folder: {ffxivcfg}\nPlugin config folder: {pluginsConfigsDir?.FullName}");
        var exclusionsGame = new List<string>();
        if (config.ExcludeReplays)
        {
            exclusionsGame.Add("replay");
        }
        var cleanup = config.TempPathes.ToArray();

        var thread = new Thread(() =>
        {
            var loweredPrio = false;
            try
            {
                var threadHandle = Kernel32.GetCurrentThread().DangerousGetHandle();
                if(Interop.SetThreadPriority(threadHandle, Interop.ThreadPriorityClass.THREAD_MODE_BACKGROUND_BEGIN))
                {
                    PluginLog.Information($"Lowered worker thread priority");
                    loweredPrio = true;
                }
                else
                {
                    PluginLog.Warning($"Failed to lower worker thread priority: {Kernel32.GetLastError()}");
                }
            }
            catch(Exception e) { e.Log(); }
            try
            {
                PluginLog.Debug($"Cleaning up old temporary files");
                foreach(var x in cleanup)
                {
                    if (!x.Contains("JustBackup-"))
                    {
                        PluginLog.Error($"Cleanup path {x} contains invalid data. Skipping.");
                    }
                    else
                    {
                        if (Directory.Exists(x))
                        {
                            PluginLog.Warning($"Deleting old temporary directory {x}");
                            try
                            {
                                Directory.Delete(x, true);
                            }
                            catch (Exception e)
                            {
                                e.Log();
                            }
                        }
                    }
                }
                PluginLog.Debug($"Cleanup finished");
                new TickScheduler(delegate
                {
                    config.TempPathes.RemoveWhere(x => !Directory.Exists(x));
                    Svc.PluginInterface.SavePluginConfig(config);
                });

                var gameSuccess = CloneDirectory(ffxivcfg, Path.Combine(temp, "game"), backupAll, exclusionsGame.ToArray());
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
                    var UIConfigurationPath = Path.Combine(xivlauncherDir, "dalamudUI.ini");

                    try
                    {
                        if(DalamudReflector.TryGetDalamudStartInfo(out var dalamudStartInfo))
                        {
                            ConfigurationPath = dalamudStartInfo.ConfigurationPath;
                            UIConfigurationPath = Path.Combine(Path.GetDirectoryName(ConfigurationPath), "dalamudUI.ini");
                        }
                    }
                    catch(Exception e)
                    {
                        PluginLog.Error($"Could not obtain Dalamud start info:\n{e}");
                    }
                    PluginLog.Verbose($"Copying from {ConfigurationPath} to {temp}");
                    CopyFile(ConfigurationPath, temp);
                    PluginLog.Verbose($"Copying from {UIConfigurationPath} to {temp}");
                    CopyFile(UIConfigurationPath, temp);
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
                    if (!unlimited)
                    {
                        var threads = Math.Max(1, (int)(Environment.ProcessorCount / 4));
                        if(config.MaxThreads > threads && config.MaxThreads > 0) threads = config.MaxThreads;
                        PluginLog.Debug($"Threads to use: {threads}");
                        szinfo.ArgumentList.Add("-mmt1");
                    }
                    else
                    {
                        if(config.MaxThreads < Environment.ProcessorCount)
                        {
                            szinfo.ArgumentList.Add($"-mmt{config.MaxThreads}");
                        }
                    }
                    szinfo.ArgumentList.Add("-mx9");
                    szinfo.ArgumentList.Add("-t7z");
                    szinfo.ArgumentList.Add(outfile + ".7z");
                    szinfo.ArgumentList.Add(Path.Combine(temp, "*"));
                    var szproc = new Process()
                    {
                        StartInfo = szinfo
                    };
                    szproc.OutputDataReceived += (sender, args) => PluginLog.Debug($"7-zip output: {args.Data}");
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
                                    selectedRecycleOption);
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
                    new TickScheduler(() =>
                    {
                        config.LastSuccessfulBackup = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                        Svc.PluginInterface.SavePluginConfig(config);
                    });
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
            if(loweredPrio)
            {
                try
                {
                    var threadHandle = Kernel32.GetCurrentThread().DangerousGetHandle();
                    if(Interop.SetThreadPriority(threadHandle, Interop.ThreadPriorityClass.THREAD_MODE_BACKGROUND_END))
                    {
                        PluginLog.Information($"Restored worker thread priority");
                    }
                    else
                    {
                        PluginLog.Warning($"Failed to restore worker thread priority: {Kernel32.GetLastError()}");
                    }
                }
                catch(Exception e) { e.Log(); }
            }
        });
        thread.Start();
    }

    internal static DirectoryInfo GetPluginsConfigDir()
    {
        var c = Svc.PluginInterface.GetType().GetField("configs", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Svc.PluginInterface);
        return (DirectoryInfo)c.GetType().GetField("configDirectory", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(c);
    }

    unsafe internal string GetFFXIVConfigFolder()
    {
        if (config.OverrideGamePath.Trim() != "") return config.OverrideGamePath;
        var path = Framework.Instance()->UserPath;
        return new string(path).Split("\0")[0];
    }

    bool CloneDirectory(string root, string dest, bool all) => CloneDirectory(root, dest, all, Array.Empty<string>());

    bool CloneDirectory(string root, string dest, bool all, string[] Exclusions)
    {
        var success = true;
        foreach (var directory in Directory.GetDirectories(root))
        {
            string dirName = Path.GetFileName(directory);
            if (dirName.EqualsIgnoreCaseAny(Exclusions)) continue;
            if(Utils.IsPathForceExcluded(dirName))
            {
                PluginLog.Verbose($"Path {dirName} is forcibly excluded");
                continue;
            }
            var path = Path.Combine(dest, dirName);
            if (config.Ignore.Any(f => path.Contains(f, StringComparison.InvariantCultureIgnoreCase))) continue;
            if (!Directory.Exists(path))
            {
                PluginLog.Verbose($"Creating {path}");
                Directory.CreateDirectory(path);
            }
            CloneDirectory(directory, path, all, Exclusions);
        }

        foreach (var file in Directory.GetFiles(root))
        {
            if (config.Ignore.Any(f => file.Contains(f, StringComparison.InvariantCultureIgnoreCase))) continue;

            if(Utils.IsPathForceExcluded(file))
            {
                PluginLog.Verbose($"Path {file} is forcibly excluded");
                continue;
            }
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
            inputFile.ReadExactly(content, 0, (int)size);
            outputFile.Write(content, 0, (int)size);
            if (P.config.CopyThrottle > 0) Thread.Sleep(P.config.CopyThrottle);
            return true;
        }
        catch(Exception e)
        {
            PluginLog.Error($"Error copying file {file} to {dest}: {e.Message}\n{e.StackTrace}");
            return false;
        }
    }
}
