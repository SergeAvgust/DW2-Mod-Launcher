using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DW2ModLauncher.Core.Diagnostics;
using DW2ModLauncher.Core.Models;
using DW2ModLauncher.Core.Services;

namespace DW2ModLauncherBeta
{
    public partial class MainForm
    {
        private void WriteIniValues(string path, Dictionary<string, string> values)
        {
            try
            {
                IniFile.Write(path, values);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Text);
            }
        }

        private LauncherMeta ReadLauncherMeta(ModInfo mod)
        {
            try
            {
                if (mod == null || string.IsNullOrWhiteSpace(mod.Folder)) return null;
                string path = Path.Combine(mod.Folder, "launcher.json");
                if (!File.Exists(path)) return null;
                return System.Text.Json.JsonSerializer.Deserialize<LauncherMeta>(File.ReadAllText(path, System.Text.Encoding.UTF8));
            }
            catch { return null; }
        }

        private void ApplyManagedSelectionToIni(ModInfo mod, bool selected)
        {
            LauncherMeta meta = ReadLauncherMeta(mod);
            if (meta == null || string.IsNullOrWhiteSpace(meta.iniPath) || string.IsNullOrWhiteSpace(meta.enabledKey)) return;
            string ini = Path.Combine(mod.Folder, meta.iniPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(ini)) return;
            Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            d[meta.enabledKey] = selected ? "true" : "false";
            if (!string.IsNullOrWhiteSpace(meta.languageKey)) d[meta.languageKey] = settings.Language;
            WriteIniValues(ini, d);
        }

        private void ApplyLanguageToManagedMods()
        {
            List<ModInfo> mods = ScanMods(settings.ManagedModsRoot, false);
            mods.AddRange(ScanMods(settings.WorkshopRoot, true));
            foreach (ModInfo mod in mods)
            {
                LauncherMeta meta = ReadLauncherMeta(mod);
                if (meta == null || string.IsNullOrWhiteSpace(meta.iniPath) || string.IsNullOrWhiteSpace(meta.languageKey)) continue;
                string ini = Path.Combine(mod.Folder, meta.iniPath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(ini)) continue;
                Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                d[meta.languageKey] = settings.Language;
                WriteIniValues(ini, d);
            }
        }

        private string FindManagedIni(ModInfo mod)
        {
            if (mod == null) return null;
            LauncherMeta meta = ReadLauncherMeta(mod);
            if (meta != null && !string.IsNullOrWhiteSpace(meta.iniPath))
            {
                string configured = Path.Combine(mod.Folder, meta.iniPath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(configured)) return configured;
            }
            try
            {
                string[] files = Directory.GetFiles(mod.Folder, "*.ini", SearchOption.TopDirectoryOnly);
                return files.FirstOrDefault();
            }
            catch { return null; }
        }

        private void OpenSelectedManagedIniEditor()
        {
            if (modList == null || modList.SelectedItems.Count == 0) return;
            ModInfo mod = modList.SelectedItems[0].Tag as ModInfo;
            OpenIniEditor(mod);
        }

        private void OpenIniEditor(ModInfo mod)
        {
            if (mod == null) return;
            string ini = FindManagedIni(mod);
            if (ini == null)
            {
                MessageBox.Show(T("NoConfigurableIni"), Text);
                return;
            }

            List<IniEditorRow> rows;
            try { rows = ReadIniEditorRows(ini); }
            catch (Exception ex)
            {
                Logger.LogException("Open individual INI editor", ex);
                MessageBox.Show("The INI file could not be read.\r\n" + ex.Message, Text);
                return;
            }
            if (rows.Count == 0)
            {
                MessageBox.Show(T("IniHasNoSettings"), Text);
                return;
            }

            using (Form editor = new Form())
            {
                editor.Text = T("IndividualINISettings") + (mod.DisplayName ?? Path.GetFileName(mod.Folder));
                editor.StartPosition = FormStartPosition.CenterParent;
                editor.Size = new Size(980, 720);
                editor.MinimumSize = new Size(760, 520);
                editor.BackColor = Dw2Deep;
                editor.ForeColor = Dw2Text;
                editor.Font = Font;

                Panel bottom = new Panel();
                bottom.Dock = DockStyle.Bottom;
                bottom.Height = 58;
                bottom.BackColor = Dw2Void;
                editor.Controls.Add(bottom);

                Button save = MakeButton(T("Save"), 680, 12, 125, 34);
                Button cancel = MakeButton(T("Cancel"), 820, 12, 125, 34);
                save.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                cancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                cancel.DialogResult = DialogResult.Cancel;
                bottom.Controls.Add(save);
                bottom.Controls.Add(cancel);

                TableLayoutPanel table = new TableLayoutPanel();
                table.Dock = DockStyle.Fill;
                table.AutoScroll = true;
                table.Padding = new Padding(14);
                table.ColumnCount = 3;
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                editor.Controls.Add(table);
                table.BringToFront();

                table.Controls.Add(MakeIniHeader(T("Setting")), 0, 0);
                table.Controls.Add(MakeIniHeader(T("Value")), 1, 0);
                table.Controls.Add(MakeIniHeader(T("Description")), 2, 0);
                int rowIndex = 1;
                foreach (IniEditorRow row in rows)
                {
                    table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    Label keyLabel = new Label();
                    keyLabel.Text = HumanizeIniKey(row.Key);
                    keyLabel.AutoSize = true;
                    keyLabel.MaximumSize = new Size(220, 0);
                    keyLabel.Margin = new Padding(3, 9, 3, 8);
                    keyLabel.ForeColor = Dw2Gold;
                    table.Controls.Add(keyLabel, 0, rowIndex);

                    row.Editor.Width = 205;
                    row.Editor.Margin = new Padding(3, 5, 3, 7);
                    row.Editor.BackColor = Dw2Void;
                    row.Editor.ForeColor = Dw2Text;
                    table.Controls.Add(row.Editor, 1, rowIndex);

                    Label description = new Label();
                    description.Text = row.Description;
                    description.AutoSize = true;
                    description.MaximumSize = new Size(450, 0);
                    description.Margin = new Padding(3, 8, 3, 8);
                    description.ForeColor = Dw2Muted;
                    table.Controls.Add(description, 2, rowIndex);
                    rowIndex++;
                }

                save.Click += delegate
                {
                    Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (IniEditorRow row in rows) values[row.Key] = (row.Editor.Text ?? "").Trim();
                    try { File.Copy(ini, ini + ".launcher_backup", true); } catch { }
                    WriteIniValues(ini, values);
                    editor.DialogResult = DialogResult.OK;
                    editor.Close();
                };

                if (editor.ShowDialog(this) == DialogResult.OK)
                {
                    SetStatus(T("IndividualINISettingsSaved"));
                }
            }
        }

        private List<IniEditorRow> ReadIniEditorRows(string path)
        {
            List<IniEditorRow> result = new List<IniEditorRow>();
            List<string> comments = new List<string>();
            foreach (string raw in File.ReadAllLines(path, System.Text.Encoding.UTF8))
            {
                string line = raw.Trim();
                if (line.StartsWith("#") || line.StartsWith(";"))
                {
                    comments.Add(line.Substring(1).Trim());
                    continue;
                }
                if (line.Length == 0) { comments.Clear(); continue; }
                int eq = line.IndexOf('=');
                if (eq <= 0) { comments.Clear(); continue; }
                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();
                IniEditorRow row = new IniEditorRow();
                row.Key = key;
                row.Description = comments.Count == 0 ? T("IniGenericDescription", key) : string.Join(" ", comments.ToArray());
                row.Editor = BuildIniValueEditor(key, value);
                result.Add(row);
                comments.Clear();
            }
            return result;
        }

        private ComboBox BuildIniValueEditor(string key, string value)
        {
            ComboBox box = new ComboBox();
            string lower = (value ?? "").ToLowerInvariant();
            string[] options = null;
            if (lower == "true" || lower == "false") options = new string[] { "true", "false" };
            else if (key.Equals("Language", StringComparison.OrdinalIgnoreCase)) options = new string[] { "ja", "en" };
            if (options != null)
            {
                box.DropDownStyle = ComboBoxStyle.DropDownList;
                box.Items.AddRange(options);
                int index = Array.FindIndex(options, x => x.Equals(value, StringComparison.OrdinalIgnoreCase));
                box.SelectedIndex = index >= 0 ? index : 0;
            }
            else
            {
                box.DropDownStyle = ComboBoxStyle.DropDown;
                box.Text = value ?? "";
            }
            return box;
        }

        // Turns a PascalCase INI key (e.g. "WarControlEnabled") into a readable
        // label ("War Control Enabled"). This is the only "translation" a MOD's
        // own settings get unless its INI supplies a comment above the key -
        // the launcher has no built-in knowledge of any specific MOD's fields.
        private string HumanizeIniKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return key;
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < key.Length; i++)
            {
                char c = key[i];
                if (i > 0 && char.IsUpper(c) && (char.IsLower(key[i - 1]) || char.IsDigit(key[i - 1])))
                    result.Append(' ');
                result.Append(c);
            }
            return result.ToString();
        }
    }
}
