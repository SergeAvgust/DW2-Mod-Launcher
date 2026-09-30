using System;
using System.IO;

namespace DW2ModLauncher.Core.Services.Publishing
{
    /// <summary>
    /// Builds the argument for DW2's own publish flag (see the official "How to upload MODs to the
    /// Steam Workshop" guide): "DistantWorlds2.exe --ugc-publish mods/&lt;folder&gt;", run with the
    /// game's own install folder as the working directory. The path after "mods/" is relative to
    /// the game's own "mods" folder specifically - not an absolute path, and not relative to
    /// wherever a MOD is actually stored on disk (a Workshop-subscribed copy, for instance, can
    /// never be published this way, only a MOD living directly under GameRoot\mods). Kept public
    /// (unlike the rest of this namespace) because the App layer also needs
    /// GetModsRelativePath/BuildArguments itself, to validate eligibility and preview the command
    /// before ever calling IModPublisher.Publish.
    /// </summary>
    public static class ModPublishCommandBuilder
    {
        public static string GetModsRelativePath(string gameRoot, string modFolder)
        {
            if (string.IsNullOrWhiteSpace(gameRoot) || string.IsNullOrWhiteSpace(modFolder)) return null;
            string modsRoot;
            string full;
            try
            {
                modsRoot = Path.GetFullPath(Path.Combine(gameRoot, "mods"))
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                full = Path.GetFullPath(modFolder);
            }
            catch { return null; }
            if (!full.StartsWith(modsRoot, StringComparison.OrdinalIgnoreCase)) return null;
            string relative = full.Substring(modsRoot.Length)
                .Replace(Path.AltDirectorySeparatorChar, '/').Replace(Path.DirectorySeparatorChar, '/')
                .TrimEnd('/');
            if (string.IsNullOrWhiteSpace(relative)) return null;
            return "mods/" + relative;
        }

        public static string BuildArguments(string modsRelativePath)
        {
            if (string.IsNullOrWhiteSpace(modsRelativePath)) return null;
            string quoted = modsRelativePath.IndexOf(' ') >= 0 ? "\"" + modsRelativePath + "\"" : modsRelativePath;
            return "--ugc-publish " + quoted;
        }
    }
}
