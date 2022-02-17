using Dalamud.Interface.Internal.Notifications;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JustBackup
{
    internal class ConfigWindow : Window
    {
        private JustBackup p;

        public ConfigWindow(JustBackup p) : base("JustBackup configuration", ImGuiWindowFlags.AlwaysAutoResize)
        {
            this.p = p;
        }

        public override void Draw()
        {
            ImGui.TextUnformatted(@"Custom backup path (by default: %localappdata%\JustBackup):");
            ImGui.SetNextItemWidth(400f);
            ImGui.InputText("##PathToBkp", ref p.config.BackupPath, 100);
            if(ImGui.Button("Open backup folder"))
            {
                Process.Start(new ProcessStartInfo()
                {
                    FileName = p.GetBackupPath(),
                    UseShellExecute = true
                });
            }
            ImGui.SetNextItemWidth(50f);
            ImGui.DragInt("Delete backups older than, days", ref p.config.DaysToKeep, 0.1f, 7, 100000);
            if (p.config.DaysToKeep < 7) p.config.DaysToKeep = 7;
            ImGui.Text("  Note: backups will be deleted to recycle bin, if available.");
            ImGui.Checkbox("Include ALL files inside FFXIV's data folder into backup", ref p.config.BackupAll);
            ImGui.Text("  (otherwise only config files will be saved, screenshots, logs, etc will be skipped)");
        }

        public override void OnClose()
        {
            Svc.PluginInterface.SavePluginConfig(p.config);
            Svc.PluginInterface.UiBuilder.AddNotification("Configuration saved", p.Name, NotificationType.Success);
            base.OnClose();
        }
    }
}
