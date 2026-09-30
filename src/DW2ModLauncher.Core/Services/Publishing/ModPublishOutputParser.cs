using System.Text.RegularExpressions;

namespace DW2ModLauncher.Core.Services.Publishing
{
    /// <summary>
    /// Pulls the Workshop item id out of DW2's own "--ugc-publish" console text (see
    /// ChildConsoleCapture/docs/workshop-publish.md for how that text is captured at all). Kept as
    /// plain regex matching over a string - no interop, no process handling - so this part of the
    /// "read the id back automatically" feature is ordinary, unit-testable logic. Internal: a
    /// private implementation detail of Dw2ExeModPublisher, not part of the public IModPublisher
    /// surface (visible to DW2ModLauncher.Tests via InternalsVisibleTo).
    /// </summary>
    internal static class ModPublishOutputParser
    {
        // Captured verbatim from a real run: "Success: Created Workshop item 3807392576 (OK)".
        // The same wording was observed for an update (a mod.json that already had a workshopId)
        // as the guide implies it would for a first-time publish - DW2 doesn't distinguish the two
        // in its own output.
        private static readonly Regex SuccessPattern = new Regex(@"Success:\s*Created Workshop item\s*(\d+)", RegexOptions.IgnoreCase);

        // Fallback: "URL: http://steamcommunity.com/sharedfiles/filedetails/?source=...&id=3807392576".
        private static readonly Regex UrlIdPattern = new Regex(@"[?&]id=(\d+)", RegexOptions.IgnoreCase);

        public static bool TryParseWorkshopId(string consoleText, out long workshopId)
        {
            workshopId = 0;
            if (string.IsNullOrEmpty(consoleText)) return false;
            Match match = SuccessPattern.Match(consoleText);
            if (match.Success && long.TryParse(match.Groups[1].Value, out workshopId)) return true;
            match = UrlIdPattern.Match(consoleText);
            if (match.Success && long.TryParse(match.Groups[1].Value, out workshopId)) return true;
            workshopId = 0;
            return false;
        }
    }
}
