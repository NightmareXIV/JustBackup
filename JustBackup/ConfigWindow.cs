using System.Diagnostics;
using System.IO;

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
            ImGuiEx.Text(@"Custom backup path (by default: %localappdata%\JustBackup):");
            ImGui.SetNextItemWidth(400f);
            ImGui.InputText("##PathToBkp", ref p.config.BackupPath, 100);
            if (ImGui.Button("Open backup folder"))
            {
                ShellStart(p.GetBackupPath());
            }
            ImGui.SameLine();
            ImGuiEx.WithTextColor(ImGuiColors.DalamudOrange, delegate
            {
                if (ImGui.Button("Read how to restore a backup"))
                {
                    ShellStart("https://github.com/Eternita-S/JustBackup/blob/master/README.md#restoring-a-backup");
                }
            });
            if (ImGui.Button("Open FFXIV configuration folder"))
            {
                ShellStart(JustBackup.GetFFXIVConfigFolder());
            }
            ImGui.SameLine();
            if (ImGui.Button("Open plugins configuration folder"))
            {
                ShellStart(JustBackup.GetPluginsConfigDir().FullName);
            }
            if(Svc.ClientState.LocalPlayer != null)
            {
                if(ImGui.Button("Open current character's config directory"))
                {
                    ShellStart(Path.Combine(JustBackup.GetFFXIVConfigFolder(), $"FFXIV_CHR{Svc.ClientState.LocalContentId:X16}"));
                }
                ImGui.SameLine();
                if (ImGui.Button("Add identification info"))
                {
                    Safe(() =>
                    {
                        var fname = Path.Combine(JustBackup.GetFFXIVConfigFolder(), $"FFXIV_CHR{Svc.ClientState.LocalContentId:X16}",
                            $"_{Svc.ClientState.LocalPlayer.Name}@{Svc.ClientState.LocalPlayer.HomeWorld.GameData.Name}.dat");
                        File.Create(fname).Dispose();
                        Notify.Success("Added identification info for current character");
                    }, (e) =>
                    {
                        Notify.Error("Error while adding identification info for current character:\n" + e);
                    });
                }
                ImGuiEx.Tooltip("Adds an empty file into character's config directory\n" +
                    "containing character's name and home world");
            }
            
            ImGui.Checkbox("Automatically remove old backups", ref p.config.DeleteBackups);
            if (p.config.DeleteBackups)
            {
                ImGui.SetNextItemWidth(50f);
                ImGui.DragInt("Delete backups older than, days", ref p.config.DaysToKeep, 0.1f, 3, 730);
                if (p.config.DaysToKeep < 3) p.config.DaysToKeep = 3;
                if (p.config.DaysToKeep > 730) p.config.DaysToKeep = 730;
                ImGuiEx.Text("  Note: backups will be deleted to recycle bin, if available.");
                ImGui.SetNextItemWidth(50f);
                ImGui.DragInt("Always keep at least this number of backup regardless of their date", ref p.config.BackupsToKeep, 0.1f, 10, 100000);
                if (p.config.BackupsToKeep < 0) p.config.BackupsToKeep = 0;
                ImGui.Separator();
            }
            ImGui.Checkbox("Include plugin configurations", ref p.config.BackupPluginConfigs);
            ImGui.Checkbox("Include ALL files inside FFXIV's data folder into backup", ref p.config.BackupAll);
            ImGuiEx.Text("  (otherwise only config files will be saved, screenshots, logs, etc will be skipped)");
            ImGui.Checkbox("Use built-in zip method instead of 7-zip", ref p.config.UseDefaultZip);
            if (p.config.UseDefaultZip) ImGuiEx.Text(ImGuiColors.DalamudRed, "7-zip archives are taking up to 15 times less space!");
            ImGui.Checkbox("Do not restrict amount of resources 7-zip can use", ref p.config.NoThreadLimit);
        }

        public override void OnClose()
        {
            Svc.PluginInterface.SavePluginConfig(p.config);
            Notify.Success("Configuration saved");
            base.OnClose();
        }
    }
}
