using Dalamud.Interface.Colors;
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
            ImGui.Checkbox("Automatically remove old backups", ref p.config.DeleteBackups);
            if (p.config.DeleteBackups)
            {
                ImGui.SetNextItemWidth(50f);
                ImGui.DragInt("Delete backups older than, days", ref p.config.DaysToKeep, 0.1f, 3, 730);
                if (p.config.DaysToKeep < 3) p.config.DaysToKeep = 3;
                if (p.config.DaysToKeep > 730) p.config.DaysToKeep = 730;
                ImGui.Text("  Note: backups will be deleted to recycle bin, if available.");
                ImGui.SetNextItemWidth(50f);
                ImGui.DragInt("Always keep at least this number of backup regardless of their date", ref p.config.BackupsToKeep, 0.1f, 10, 100000);
                if (p.config.BackupsToKeep < 0) p.config.BackupsToKeep = 0;
                ImGui.Separator();
            }
            ImGui.Checkbox("Include plugin configurations", ref p.config.BackupPluginConfigs);
            ImGui.Checkbox("Include ALL files inside FFXIV's data folder into backup", ref p.config.BackupAll);
            ImGui.Text("  (otherwise only config files will be saved, screenshots, logs, etc will be skipped)");
            ImGui.Checkbox("Use built-in zip method instead of 7-zip", ref p.config.UseDefaultZip);
            if (p.config.UseDefaultZip) ImGui.TextColored(ImGuiColors.DalamudRed, "7-zip archives are taking up to 15 times less space!");
        }

        public override void OnClose()
        {
            Svc.PluginInterface.SavePluginConfig(p.config);
            Svc.PluginInterface.UiBuilder.AddNotification("Configuration saved", p.Name, NotificationType.Success);
            base.OnClose();
        }
    }
}
