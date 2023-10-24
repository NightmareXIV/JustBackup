using ECommons.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JustBackup
{
    internal static class Utils
    {
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
}
