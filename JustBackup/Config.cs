using Dalamud.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JustBackup
{
    internal class Config : IPluginConfiguration
    {
        public int Version { get; set; } = 1;
        public string BackupPath = "";
        public bool DeleteBackups = true;
        public bool DeleteToRecycleBin = true;
        public int DaysToKeep = 7;
        public bool BackupAll = false;
        public bool ExcludeReplays = false;
        public bool UseDefaultZip = false;
        public bool BackupPluginConfigs = true;
        public int BackupsToKeep = 10;
        public bool NoThreadLimit = false;
        public List<string> Ignore = new();
        public HashSet<string> TempPathes = new();
        public string OverrideGamePath = "";
    }
}
