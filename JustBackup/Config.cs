using Dalamud.Configuration;

namespace JustBackup;

internal class Config : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public string BackupPath = "";
    public string TempPath = "";
    public bool DeleteBackups = true;
    public bool DeleteToRecycleBin = true;
    public int DaysToKeep = 7;
    public bool BackupAll = false;
    public bool ExcludeReplays = false;
    public bool UseDefaultZip = false;
    public bool BackupPluginConfigs = true;
    public int BackupsToKeep = 10;
    public bool NoThreadLimit = false;
    public int MaxThreads = 99;
    public List<string> Ignore = new();
    public HashSet<string> TempPathes = new();
    public string OverrideGamePath = "";
    public int MinIntervalBetweenBackups = 0;
    public long LastSuccessfulBackup = 0;
    public int CopyThrottle = 0;
}
