using ECommons.Logging;
using System.IO;

namespace JustBackup;

internal static class Utils
{
    internal static string[][] ForcedExclusions = [
        ["pluginConfigs","vnavmesh","meshcache"],
        ["pluginConfigs","MareSynchronos","eventlog"],
        ["pluginConfigs","MareSynchronos","tracelog"],
        ["pluginConfigs","Browsingway","cef-cache"],
        ["pluginConfigs","Browsingway","dependencies"],
        ["pluginConfigs","Splatoon","ScriptCache"],
        ["pluginConfigs","Splatoon","Logs"],
        ["pluginConfigs","ResLogger2.Plugin"],
        ["pluginConfigs","AutoRetainer", "session.lock"],
        ];

    internal static bool IsPathForceExcluded(string fullPath)
    {
        foreach(var x in ForcedExclusions)
        {
            var excl = Path.Combine(x);
            var result = fullPath.Contains(excl, StringComparison.OrdinalIgnoreCase);
            //PluginLog.Verbose($"Comparing exclusion: \n  excl={excl}\n  fullPath={fullPath}\n  result: {result}");
            if(result) return true;
        }
        return false;
    }

    internal static bool HasWriteAccessToFolder(string folderPath)
    {
        try
        {
            var tempFile = Path.Combine(folderPath, "JustBackupCheckFile");
            File.WriteAllText(tempFile, "");
            File.Delete(tempFile);
            return true;
        }
        catch(Exception e)
        {
            e.Log();
            return false;
        }
    }
}
