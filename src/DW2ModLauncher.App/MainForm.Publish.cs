using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DW2ModLauncher.Core.Diagnostics;
using DW2ModLauncher.Core.Models;
using DW2ModLauncher.Core.Services;
using DW2ModLauncher.Core.Services.Publishing;

namespace DW2ModLauncherBeta
{
    public partial class MainForm
    {
        private void PublishSelectedMod()
        {
            if (modList == null || modList.SelectedItems.Count == 0) return;
            OpenPublishDialog(modList.SelectedItems[0].Tag as ModInfo);
        }

        // One dialog that both edits the mod.json fields Steam Workshop publish itself reads
        // (title, description, preview image, version, bundles - see docs/workshop-publish.md) and
        // kicks off the actual publish, via Publish/Cancel buttons - not a separate "edit" step
        // followed by a separate confirmation popup. Not a MOD's own settings.schema.json, which is
        // a different, MOD-author-defined thing entirely (see MainForm.ModSettings).
        private void OpenPublishDialog(ModInfo mod)
        {
            if (mod == null) return;
            if (mod.IsWorkshop || string.IsNullOrWhiteSpace(mod.ModJsonPath))
            {
                MessageBox.Show(T("PublishNotLocalMod"), Text);
                return;
            }
            string relative = ModPublishCommandBuilder.GetModsRelativePath(settings.GameRoot, mod.Folder);
            if (relative == null)
            {
                MessageBox.Show(T("PublishRequiresGameModsFolder"), Text);
                return;
            }
            if (IsGameRunning())
            {
                MessageBox.Show(T("GameRunningWarning"), Text);
                return;
            }
            string exe = Path.Combine(settings.GameRoot ?? "", "DistantWorlds2.exe");
            if (!File.Exists(exe))
            {
                MessageBox.Show(T("GameExeNotFound"), Text);
                return;
            }

            ModPublishMetadata metadata;
            try { metadata = ModPublishMetadataEditor.Read(mod.ModJsonPath); }
            catch (Exception ex)
            {
                Logger.LogException("Read mod.json for publish", ex);
                MessageBox.Show(ex.Message, Text);
                return;
            }
            bool isUpdate = !string.IsNullOrWhiteSpace(mod.WorkshopId);

            using (Form dialog = new Form())
            {
                dialog.Text = T("PublishToWorkshop") + " - " + (mod.DisplayName ?? mod.Id);
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;
                dialog.ClientSize = new Size(700, 860);
                dialog.BackColor = Dw2Deep;
                dialog.ForeColor = Dw2Text;
                dialog.Font = Font;

                Label titleLabel = new Label { Text = T("PublishInfoTitle"), Location = new Point(24, 20), AutoSize = true };
                dialog.Controls.Add(titleLabel);
                TextBox titleBox = new TextBox { Location = new Point(24, 44), Size = new Size(640, 25), BackColor = Dw2Void, ForeColor = Dw2Text, BorderStyle = BorderStyle.FixedSingle, Text = metadata.DisplayName };
                dialog.Controls.Add(titleBox);

                Label versionLabel = new Label { Text = T("Version"), Location = new Point(24, 82), AutoSize = true };
                dialog.Controls.Add(versionLabel);
                TextBox versionBox = new TextBox { Location = new Point(24, 106), Size = new Size(200, 25), BackColor = Dw2Void, ForeColor = Dw2Text, BorderStyle = BorderStyle.FixedSingle, Text = metadata.Version };
                dialog.Controls.Add(versionBox);

                Label previewLabel = new Label { Text = T("PublishInfoPreviewImage"), Location = new Point(24, 144), AutoSize = true };
                dialog.Controls.Add(previewLabel);
                TextBox previewBox = new TextBox { Location = new Point(24, 168), Size = new Size(560, 25), BackColor = Dw2Void, ForeColor = Dw2Text, BorderStyle = BorderStyle.FixedSingle, Text = metadata.PreviewImage };
                dialog.Controls.Add(previewBox);
                Button previewBrowse = MakeButton("...", 594, 166, 70, 27);
                previewBrowse.Click += delegate
                {
                    using (OpenFileDialog picker = new OpenFileDialog())
                    {
                        picker.InitialDirectory = mod.ContentRoot ?? mod.Folder;
                        picker.Filter = "Image files (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png";
                        if (picker.ShowDialog(dialog) == DialogResult.OK) previewBox.Text = Path.GetFileName(picker.FileName);
                    }
                };
                dialog.Controls.Add(previewBrowse);

                Label descriptionLabel = new Label { Text = T("Description"), Location = new Point(24, 204), AutoSize = true };
                dialog.Controls.Add(descriptionLabel);
                TextBox descriptionBox = new TextBox
                {
                    Location = new Point(24, 228),
                    Size = new Size(640, 320),
                    Multiline = true,
                    ScrollBars = ScrollBars.Vertical,
                    BackColor = Dw2Void,
                    ForeColor = Dw2Text,
                    BorderStyle = BorderStyle.FixedSingle,
                    Text = metadata.Description
                };
                dialog.Controls.Add(descriptionBox);

                Label bundlesLabel = new Label { Text = T("PublishInfoBundles"), Location = new Point(24, 562), AutoSize = true };
                dialog.Controls.Add(bundlesLabel);
                TextBox bundlesBox = new TextBox
                {
                    Location = new Point(24, 586),
                    Size = new Size(640, 90),
                    Multiline = true,
                    ScrollBars = ScrollBars.Vertical,
                    BackColor = Dw2Void,
                    ForeColor = Dw2Text,
                    BorderStyle = BorderStyle.FixedSingle,
                    Text = string.Join(Environment.NewLine, metadata.Bundles ?? new System.Collections.Generic.List<string>())
                };
                dialog.Controls.Add(bundlesBox);

                Label note = new Label
                {
                    Location = new Point(24, 690),
                    Size = new Size(650, 100),
                    ForeColor = Dw2Muted,
                    Text = T(isUpdate ? "PublishAboutToRunUpdate" : "PublishAboutToRunFirstTime", relative)
                };
                dialog.Controls.Add(note);

                Button publish = MakeButton(T("PublishToWorkshop"), 400, 806, 130, 34);
                Button cancel = MakeButton(T("Cancel"), 540, 806, 130, 34);
                cancel.DialogResult = DialogResult.Cancel;
                dialog.Controls.Add(publish);
                dialog.Controls.Add(cancel);

                publish.Click += delegate
                {
                    metadata.DisplayName = titleBox.Text.Trim();
                    metadata.Version = versionBox.Text.Trim();
                    metadata.PreviewImage = previewBox.Text.Trim();
                    metadata.Description = descriptionBox.Text;
                    metadata.Bundles = bundlesBox.Lines.Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
                    try
                    {
                        ModPublishMetadataEditor.Write(mod.ModJsonPath, metadata);
                        dialog.DialogResult = DialogResult.OK;
                        dialog.Close();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException("Write mod.json publish info", ex);
                        MessageBox.Show(ex.Message, Text);
                    }
                };

                if (dialog.ShowDialog(this) == DialogResult.OK) RunPublishProcess(mod, isUpdate);
            }
        }

        // Only this method (and ApplyCapturedWorkshopId/OpenCaptureWorkshopIdDialog below) know
        // IModPublisher exists - everything about how a publish actually happens (attaching to a
        // console, killing the process, or something completely different for a future publisher)
        // is that implementation's own business, not this class's.
        private void RunPublishProcess(ModInfo mod, bool isUpdate)
        {
            if (publishRunning) return;
            publishRunning = true;
            if (publishButton != null) publishButton.Enabled = false;
            SetStatus(T("PublishRunning"));

            IModPublisher publisher = new Dw2ExeModPublisher(settings.GameRoot);
            string modName = ModPublishCommandBuilder.GetModsRelativePath(settings.GameRoot, mod.Folder);
            if (modName != null && modName.StartsWith("mods/", StringComparison.OrdinalIgnoreCase)) modName = modName["mods/".Length..];

            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += delegate (object sender, DoWorkEventArgs e) { e.Result = publisher.Publish(modName); };
            worker.RunWorkerCompleted += delegate (object sender, RunWorkerCompletedEventArgs e)
            {
                publishRunning = false;
                if (publishButton != null) publishButton.Enabled = modList != null && modList.SelectedItems.Count > 0 &&
                    !((modList.SelectedItems[0].Tag as ModInfo)?.IsWorkshop ?? true);
                if (e.Error != null)
                {
                    Logger.LogException("Publish MOD to Workshop", e.Error);
                    MessageBox.Show(T("PublishFailed", e.Error.Message), Text);
                    SetStatus(T("PublishFailedStatus"));
                    return;
                }
                ModPublishResult result = e.Result as ModPublishResult;
                if (result == null || !string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    MessageBox.Show(T("PublishFailed", result?.ErrorMessage ?? ""), Text);
                    SetStatus(T("PublishFailedStatus"));
                    return;
                }
                SetStatus(T("PublishCommandSent"));
                if (result.WorkshopId.HasValue)
                {
                    ApplyCapturedWorkshopId(mod, result.WorkshopId.Value);
                }
                else if (isUpdate)
                {
                    MessageBox.Show(T("PublishUpdateSent"), T("PublishToWorkshop"));
                }
                else
                {
                    OpenCaptureWorkshopIdDialog(mod);
                }
            };
            worker.RunWorkerAsync();
        }

        // Reached when the IModPublisher implementation actually managed to read the Workshop
        // item's id back on its own (see docs/workshop-publish.md for how Dw2ExeModPublisher does
        // that) - the common case. Safe to call for an update too (mod.json already had the same
        // id; this just rewrites it).
        private void ApplyCapturedWorkshopId(ModInfo mod, long workshopId)
        {
            if (mod == null || string.IsNullOrWhiteSpace(mod.ModJsonPath)) return;
            try
            {
                ModJsonWorkshopIdWriter.Write(mod.ModJsonPath, workshopId);
                string url = "https://steamcommunity.com/sharedfiles/filedetails/?id=" + workshopId;
                MessageBox.Show(T("PublishCapturedIdMessage", workshopId, url), T("PublishToWorkshop"));
                SetStatus(T("WorkshopIdSaved"));
                RefreshAll();
            }
            catch (Exception ex)
            {
                Logger.LogException("Write workshopId to mod.json", ex);
                MessageBox.Show(ex.Message, Text);
            }
        }

        // Fallback for when IModPublisher couldn't capture the id on its own (e.g. the user closed
        // DW2's console before it caught up, or something about a given build/OS made the
        // underlying trick fail) - the one part of the official manual workflow (copy the id off
        // the item's URL, hand-edit it into mod.json) the launcher can still take over once the id
        // is known.
        private void OpenCaptureWorkshopIdDialog(ModInfo mod)
        {
            if (mod == null || string.IsNullOrWhiteSpace(mod.ModJsonPath)) return;

            using (Form dialog = new Form())
            {
                dialog.Text = T("PublishToWorkshop");
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;
                dialog.Size = new Size(560, 300);
                dialog.BackColor = Dw2Deep;
                dialog.ForeColor = Dw2Text;
                dialog.Font = Font;

                Label note = new Label();
                note.Location = new Point(18, 18);
                note.Size = new Size(510, 90);
                note.Text = T("PublishFirstTimeNote");
                dialog.Controls.Add(note);

                Button openWorkshopFiles = MakeButton(T("OpenMyWorkshopFiles"), 18, 118, 220, 34);
                openWorkshopFiles.Click += delegate
                {
                    try { Process.Start(new ProcessStartInfo("https://steamcommunity.com/my/myworkshopfiles/?appid=" + SteamLocator.AppId) { UseShellExecute = true }); }
                    catch (Exception ex) { MessageBox.Show(ex.Message, Text); }
                };
                dialog.Controls.Add(openWorkshopFiles);

                Label idLabel = new Label();
                idLabel.Text = T("WorkshopIdLabel");
                idLabel.Location = new Point(18, 168);
                idLabel.AutoSize = true;
                dialog.Controls.Add(idLabel);

                TextBox idBox = new TextBox();
                idBox.Location = new Point(18, 192);
                idBox.Size = new Size(250, 25);
                idBox.BackColor = Dw2Void;
                idBox.ForeColor = Dw2Text;
                idBox.BorderStyle = BorderStyle.FixedSingle;
                dialog.Controls.Add(idBox);

                Button save = MakeButton(T("Save"), 290, 230, 115, 34);
                Button cancel = MakeButton(T("Cancel"), 415, 230, 115, 34);
                cancel.DialogResult = DialogResult.Cancel;
                dialog.Controls.Add(save);
                dialog.Controls.Add(cancel);

                save.Click += delegate
                {
                    long workshopId;
                    if (!long.TryParse((idBox.Text ?? "").Trim(), out workshopId) || workshopId <= 0)
                    {
                        MessageBox.Show(T("InvalidWorkshopId"), Text);
                        return;
                    }
                    try
                    {
                        ModJsonWorkshopIdWriter.Write(mod.ModJsonPath, workshopId);
                        dialog.DialogResult = DialogResult.OK;
                        dialog.Close();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException("Write workshopId to mod.json", ex);
                        MessageBox.Show(ex.Message, Text);
                    }
                };

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    SetStatus(T("WorkshopIdSaved"));
                    RefreshAll();
                }
            }
        }
    }
}
