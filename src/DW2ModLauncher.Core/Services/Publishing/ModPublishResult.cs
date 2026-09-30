namespace DW2ModLauncher.Core.Services.Publishing
{
    /// <summary>
    /// What IModPublisher.Publish hands back. WorkshopId is set when the implementation managed to
    /// read the new/updated item's id back (however it does that - for Dw2ExeModPublisher, see
    /// docs/workshop-publish.md); it's null when publishing still ran but the id couldn't be
    /// recovered, in which case the caller falls back to asking the user for it by hand.
    /// ErrorMessage is set only for a hard failure (couldn't even start the publish command).
    /// </summary>
    public class ModPublishResult
    {
        public long? WorkshopId { get; set; }
        public string ErrorMessage { get; set; }
        public int ExitCode { get; set; }
    }
}
