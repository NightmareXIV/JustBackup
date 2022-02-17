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
        public int DaysToKeep = 7;
    }
}
