namespace DW2ModLauncher.Core.Services.Publishing
{
    /// <summary>
    /// Publishes a local MOD to the Steam Workshop and reports back the Workshop item id, however
    /// that actually happens under the hood. DW2's own "--ugc-publish" (Dw2ExeModPublisher) is the
    /// only implementation today, but this exists as an interface on purpose: a future switch to
    /// driving steamcmd directly would be a completely different mechanism (a normal stdout stream
    /// and a VDF script file, no console to attach to and no regex to parse at all), and should
    /// only mean writing a new class here - nothing that calls Publish should need to change.
    /// </summary>
    public interface IModPublisher
    {
        /// <param name="modName">The MOD's own identifier relative to wherever this implementation
        /// keeps its MODs (for Dw2ExeModPublisher: the folder name under GameRoot\mods, e.g.
        /// "SlowerThanLight" or "Group/SubMod" for a nested one) - never a full path.</param>
        ModPublishResult Publish(string modName);
    }
}
