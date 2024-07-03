using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Logging;
using ECommons;
using ECommons.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JustBackup
{
    internal class ModalWindowAskBackup : Window
    {
        public ModalWindowAskBackup() : base("JustBackup - another instance is already running", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize, true)
        {
            this.IsOpen = false;
            this.RespectCloseHotkey = false;
            this.ShowCloseButton = false;
        }

        public override void Draw()
        {
            ImGuiEx.Text($"Another backup is already in progress. \nAttempting to create new backup while old \n  is not yet finished may result both backups not being done properly.");
            if (ImGui.Button($"Force stop old backup and create new backup"))
            {
                try
                {
                    var FileName = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName, "7zr.exe");
                    foreach (var x in Process.GetProcesses().Where(x => x.ProcessName.Contains("7zr")))
                    {
                        if (x.MainModule.FileName == FileName)
                        {
                            PluginLog.Information($"Terminating process {x.ProcessName} ({x.Id})");
                            x.Kill();
                        }
                    }
                    (ECommonsMain.Instance as JustBackup).DoBackupInternal();
                    this.IsOpen = false;
                }
                catch(Exception ex )
                {
                    ex.Log();
                    Notify.Error("Could not terminate old backup");
                }
            }
            if(ImGui.Button($"Don't start new backup and let old backup finish"))
            {
                this.IsOpen = false;
            }
            if(ImGui.Button($"(not recommended) Run new backup anyway"))
            {
                (ECommonsMain.Instance as JustBackup).DoBackupInternal();
                this.IsOpen = false;
            }
            this.Position = (ImGuiHelpers.MainViewport.Size / 2) - ImGui.GetWindowSize() / 2;
        }
    }
}
