using System;
using System.Diagnostics;
using System.IO;
using DW2ModLauncher.Core.Services.Publishing.Interop;

namespace DW2ModLauncher.Core.Services.Publishing
{
    /// <summary>
    /// Publishes a local MOD by shelling out to DW2's own "DistantWorlds2.exe --ugc-publish
    /// mods/&lt;folder&gt;" (see docs/workshop-publish.md) and reading the new/updated Workshop
    /// item's id straight off the console DW2 allocates for itself. Every bit of the
    /// attach/read/parse/dismiss wizardry that takes is private to this one class - callers only
    /// ever see IModPublisher/ModPublishResult.
    /// </summary>
    public class Dw2ExeModPublisher : IModPublisher
    {
        private readonly string gameRoot;

        public Dw2ExeModPublisher(string gameRoot)
        {
            this.gameRoot = gameRoot;
        }

        public ModPublishResult Publish(string modName)
        {
            ModPublishResult result = new ModPublishResult();
            string modFolder = Path.Combine(gameRoot ?? "", "mods", modName ?? "");
            string relative = ModPublishCommandBuilder.GetModsRelativePath(gameRoot, modFolder);
            if (relative == null)
            {
                result.ErrorMessage = "MOD is not directly under the game's own \"mods\" folder.";
                return result;
            }
            string exe = Path.Combine(gameRoot ?? "", "DistantWorlds2.exe");
            if (!File.Exists(exe))
            {
                result.ErrorMessage = "DistantWorlds2.exe was not found.";
                return result;
            }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = exe;
                psi.WorkingDirectory = gameRoot;
                psi.Arguments = ModPublishCommandBuilder.BuildArguments(relative);
                psi.UseShellExecute = false;
                using (Process process = Process.Start(psi))
                {
                    long? workshopId = ModWorkshopIdWatcher.WaitForWorkshopId(
                        () => process.HasExited,
                        () => ChildConsoleCapture.TryReadScreenText(process.Id, TimeSpan.FromSeconds(2)),
                        TimeSpan.FromMinutes(10), TimeSpan.FromMilliseconds(500));
                    result.WorkshopId = workshopId;

                    // Found it - rather than yanking the process out from under it, try
                    // dismissing its "Press any key to exit." prompt ourselves (see
                    // ChildConsoleCapture.TryDismiss) so it gets to run its own normal exit path,
                    // which may include opening a browser to the published item. Only fall back
                    // to killing it if that didn't make it exit promptly on its own.
                    if (workshopId.HasValue) ChildConsoleCapture.TryDismiss(process.Id);
                    if (!process.WaitForExit(workshopId.HasValue ? 5000 : 0))
                    {
                        try { process.Kill(true); } catch { }
                        process.WaitForExit(5000);
                    }
                    result.ExitCode = process.HasExited ? process.ExitCode : -1;
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
            }
            return result;
        }
    }
}
