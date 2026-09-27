using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DW2ModLauncher.Core.Models;

namespace DW2ModLauncherBeta
{
    public partial class MainForm
    {
        private List<ModInfo> OrderedEnabledMods()
        {
            List<ModInfo> launchMods = (currentManagedMods ?? new List<ModInfo>())
                .Concat(currentWorkshopMods ?? new List<ModInfo>()).Where(IsModSelected).ToList();
            return launchMods.OrderBy(m =>
            {
                int index = currentModOrder == null ? -1 : currentModOrder.FindIndex(x => x.Equals(m.ActiveToken, StringComparison.OrdinalIgnoreCase));
                return index < 0 ? int.MaxValue : index;
            }).ToList();
        }

        // The game's own --low-level-inject flag accepts multiple space-separated
        // "dll!entryPoint" targets, but only ONE occurrence of the flag actually
        // takes effect - a second occurrence doesn't merge with the first. So every
        // enabled mod's declarative injection target is collected here and composed
        // into a single flag in BuildLaunchArguments, rather than letting each mod
        // contribute its own separate --low-level-inject occurrence.
        private List<KeyValuePair<string, string>> CollectInjectionTargets(List<ModInfo> orderedMods)
        {
            List<KeyValuePair<string, string>> targets = new List<KeyValuePair<string, string>>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ModInfo mod in orderedMods)
            {
                string modRoot = mod.ContentRoot ?? mod.Folder;
                AddInjectionTarget(targets, seen, modRoot, mod.InjectionDll, mod.InjectionEntryPoint);
                LauncherMeta meta = ReadLauncherMeta(mod);
                if (meta != null && meta.injection != null)
                    AddInjectionTarget(targets, seen, modRoot, meta.injection.dll, meta.injection.entryPoint);
            }
            return targets;
        }

        private void AddInjectionTarget(List<KeyValuePair<string, string>> targets, HashSet<string> seen, string modRoot, string dllRelative, string entryPoint)
        {
            if (string.IsNullOrWhiteSpace(modRoot) || string.IsNullOrWhiteSpace(dllRelative) || string.IsNullOrWhiteSpace(entryPoint)) return;
            string full = Path.GetFullPath(Path.Combine(modRoot, dllRelative.Replace('/', Path.DirectorySeparatorChar)));
            if (!seen.Add(full + "!" + entryPoint)) return;
            targets.Add(new KeyValuePair<string, string>(full, entryPoint));
        }

        private string BuildLaunchArguments()
        {
            EnsureSettingsState();
            List<string> args = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<ModInfo> orderedMods = OrderedEnabledMods();
            List<KeyValuePair<string, string>> injections = CollectInjectionTargets(orderedMods);
            if (injections.Count > 0)
            {
                List<string> tokens = injections.Select(t => (t.Key.IndexOf(' ') >= 0 ? "\"" + t.Key + "\"" : t.Key) + "!" + t.Value).ToList();
                args.Add("--low-level-inject " + string.Join(" ", tokens));
            }
            string global = launchArgsBox == null ? settings.GlobalLaunchArguments : launchArgsBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(global) && seen.Add(global)) args.Add(global);
            return string.Join(" ", args.Where(a => !string.IsNullOrWhiteSpace(a)).ToArray()).Trim();
        }

        private void UpdateCommandPreview()
        {
            if (commandPreviewBox == null) return;
            string exe = string.IsNullOrEmpty(settings.GameRoot) ? "DistantWorlds2.exe" : Path.Combine(settings.GameRoot, "DistantWorlds2.exe");
            commandPreviewBox.Text = "\"" + exe + "\"" + (string.IsNullOrWhiteSpace(BuildLaunchArguments()) ? "" : " " + BuildLaunchArguments());
        }

        private void LaunchGame()
        {
            SaveSettingsFromUi();
            AnalyzeConflicts();
            RefreshModStatusColumns();
            List<string> diagnostics = BuildLaunchDiagnostics();
            if (diagnostics.Count > 0)
            {
                DialogResult diagnosticAnswer = MessageBox.Show(
                    T("DiagnosticsFoundIssues") +
                    string.Join("\r\n", diagnostics.Take(30).ToArray()) +
                    T("LaunchAnyway"),
                    T("PreLaunchDiagnostics"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (diagnosticAnswer != DialogResult.Yes) return;
            }
            if (currentCollisions.Count > 0)
            {
                string warning = BuildConflictWarning();
                DialogResult answer = MessageBox.Show(warning, T("MODConflictWarning"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (answer != DialogResult.Yes) return;
            }
            string exe = Path.Combine(settings.GameRoot ?? "", "DistantWorlds2.exe");
            if (!File.Exists(exe))
            {
                MessageBox.Show(T("GameExeNotFound"), Text);
                return;
            }
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = exe;
                psi.WorkingDirectory = settings.GameRoot;
                psi.Arguments = BuildLaunchArguments();
                psi.UseShellExecute = true;
                Process.Start(psi);
                SetStatus(T("DistantWorlds2Launched"));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Text);
            }
        }
    }
}
