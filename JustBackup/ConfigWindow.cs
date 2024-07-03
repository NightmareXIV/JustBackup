using Dalamud.Interface.Components;
using ECommons.Funding;
using ECommons.Logging;
using System.Diagnostics;
using System.IO;
using static Dalamud.Interface.Utility.Raii.ImRaii;

namespace JustBackup
{
    internal class ConfigWindow : Window
    {
        private JustBackup p;
        private string newIgnoredFile = string.Empty;

        public ConfigWindow(JustBackup p) : base("JustBackup configuration")
        {
            this.p = p;
        }

        void Settings()
        {
            ImGuiEx.LineCentered("restore", () =>
            {
                ImGuiEx.WithTextColor(ImGuiColors.DalamudOrange, delegate
                {
                    if (ImGui.Button("Read how to restore a backup"))
                    {
                        ShellStart("https://github.com/NightmareXIV/JustBackup/blob/master/README.md#restoring-a-backup");
                    }
                });
            });
            ImGuiEx.Text(@"Custom backup path (by default: %localappdata%\JustBackup):");
            ImGui.SetNextItemWidth(400f);
            ImGui.InputText("##PathToBkp", ref p.config.BackupPath, 100);
            ImGuiEx.Text(@"Custom temporary files path (by default: %temp%):");
            ImGui.SetNextItemWidth(400f);
            ImGui.InputText("##PathToTmp", ref p.config.TempPath, 100);
            ImGui.Checkbox("Automatically remove old backups", ref p.config.DeleteBackups);
            if (p.config.DeleteBackups)
            {
                ImGui.SetNextItemWidth(50f);
                ImGui.DragInt("Delete backups older than, days", ref p.config.DaysToKeep, 0.1f, 3, 730);
                if (p.config.DaysToKeep < 3) p.config.DaysToKeep = 3;
                if (p.config.DaysToKeep > 730) p.config.DaysToKeep = 730;
                ImGui.Checkbox("Delete to recycle bin, if available.", ref p.config.DeleteToRecycleBin);
                ImGui.SetNextItemWidth(50f);
                ImGui.DragInt("Always keep at least this number of backup regardless of their date", ref p.config.BackupsToKeep, 0.1f, 10, 100000);
                if (p.config.BackupsToKeep < 0) p.config.BackupsToKeep = 0;
                ImGui.Separator();
            }
            ImGui.Checkbox("Include plugin configurations", ref p.config.BackupPluginConfigs);
            ImGui.Checkbox("Include ALL files inside FFXIV's data folder into backup", ref p.config.BackupAll);
            ImGuiEx.Text("  (otherwise only config files will be saved, screenshots, logs, etc will be skipped)");
            ImGui.Checkbox($"Exclude replays from backup", ref p.config.ExcludeReplays);
            ImGui.Checkbox("Use built-in zip method instead of 7-zip", ref p.config.UseDefaultZip);
            if (p.config.UseDefaultZip) ImGuiEx.Text(ImGuiColors.DalamudRed, "7-zip archives are taking up to 15 times less space!");
            ImGui.Checkbox("Do not restrict amount of resources 7-zip can use", ref p.config.NoThreadLimit);
            ImGui.SetNextItemWidth(100f);
            ImGui.SliderInt($"Minimal interval between backups, minutes", ref p.config.MinIntervalBetweenBackups, 0, 60);
            ImGuiComponents.HelpMarker("Backup will not be created if previous backup was created less than this amount of minutes. Note that only successfully completed backups will update interval.");
        }

        void Tools()
        {
            if (ImGui.Button("Open backup folder"))
            {
                ShellStart(p.GetBackupPath());
            }
            ImGuiEx.WithTextColor(ImGuiColors.DalamudOrange, delegate
            {
                if (ImGui.Button("Read how to restore a backup"))
                {
                    ShellStart("https://github.com/NightmareXIV/JustBackup/blob/master/README.md#restoring-a-backup");
                }
            });
            if (ImGui.Button("Open FFXIV configuration folder"))
            {
                ShellStart(p.GetFFXIVConfigFolder());
            }
            if (ImGui.Button("Open plugins configuration folder"))
            {
                ShellStart(JustBackup.GetPluginsConfigDir().FullName);
            }
            if (Svc.ClientState.LocalPlayer != null)
            {
                if (ImGui.Button("Open current character's config directory"))
                {
                    ShellStart(Path.Combine(p.GetFFXIVConfigFolder(), $"FFXIV_CHR{Svc.ClientState.LocalContentId:X16}"));
                }
                if (ImGui.Button("Add identification info"))
                {
                    Safe(() =>
                    {
                        var fname = Path.Combine(p.GetFFXIVConfigFolder(), $"FFXIV_CHR{Svc.ClientState.LocalContentId:X16}",
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
        }


        void Ignored()
        {
            var id = 0;
            foreach (var file in p.config.Ignore.ToArray())
            {
                if (ImGui.SmallButton($"x##{id++}"))
                {
                    p.config.Ignore.Remove(file);
                }
                ImGui.SameLine();
                ImGui.Text(file);
            }

            if (ImGui.SmallButton("+"))
            {
                if (!p.config.Ignore.Contains(newIgnoredFile, StringComparer.InvariantCultureIgnoreCase))
                {
                    p.config.Ignore.Add(newIgnoredFile);
                    newIgnoredFile = string.Empty;
                }
            }
            ImGui.SameLine();

            ImGui.InputText("Ignored (partial) Path", ref newIgnoredFile, 512);
        }

        void Expert()
        {
            ImGuiEx.Text($"Override game configuration folder path:");
            ImGuiEx.SetNextItemFullWidth();
            ImGui.InputText($"##pathGame", ref p.config.OverrideGamePath, 2000);
            ImGui.SetNextItemWidth(150f);
            ImGui.InputInt("Maximum threads", ref p.config.MaxThreads.ValidateRange(1, 99), 1, 99);
            ImGui.SetNextItemWidth(100f);
            ImGui.SliderInt($"Throttle copying, ms", ref p.config.CopyThrottle.ValidateRange(0, 50), 0, 5);
            ImGuiComponents.HelpMarker("The higher this value, the longer backup creation will take but the less loaded your SSD/HDD will be. Increase this value if you're experiencing lag during backup process.");
        }

        public override void Draw()
        {
            PatreonBanner.DrawRight();
            ImGuiEx.EzTabBar("default", PatreonBanner.Text,
                ("Settings", Settings, null, true),
                ("Tools", Tools, null, true),
                ("Ignored pathes (beta)", Ignored, null, true),
                ("Expert options", Expert, null, true),
                InternalLog.ImGuiTab()
                );
                       
        }

        public override void OnClose()
        {
            Svc.PluginInterface.SavePluginConfig(p.config);
            Notify.Success("Configuration saved");
            base.OnClose();
        }
    }
}
