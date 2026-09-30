using System;
using System.Threading;

namespace DW2ModLauncher.Core.Services.Publishing
{
    /// <summary>
    /// Polls a running "--ugc-publish" process's own console (via a caller-supplied screen-text
    /// reader, normally ChildConsoleCapture.TryReadScreenText) until ModPublishOutputParser finds a
    /// Workshop item id in it, the process exits first, or a timeout passes. Takes plain delegates
    /// rather than a real Process/ChildConsoleCapture dependency so the polling/timeout logic
    /// itself - the part actually worth unit testing - can be tested without touching Win32 or
    /// spawning a real process. Internal: a private implementation detail of Dw2ExeModPublisher.
    /// </summary>
    internal static class ModWorkshopIdWatcher
    {
        public static long? WaitForWorkshopId(Func<bool> hasExited, Func<string> readScreenText, TimeSpan timeout, TimeSpan pollInterval)
        {
            DateTime deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (hasExited()) return null;
                string screen = readScreenText();
                long workshopId;
                if (ModPublishOutputParser.TryParseWorkshopId(screen, out workshopId)) return workshopId;
                Thread.Sleep(pollInterval);
            }
            return null;
        }
    }
}
