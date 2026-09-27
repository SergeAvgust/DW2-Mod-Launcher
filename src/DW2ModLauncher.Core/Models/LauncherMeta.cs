namespace DW2ModLauncher.Core.Models
{
    public class LauncherMeta
    {
        public string iniPath { get; set; }
        public string enabledKey { get; set; }
        public string languageKey { get; set; }
        public LauncherInjection injection { get; set; }
    }

    // Declarative low-level-inject target: dll is a path relative to the
    // mod's own content folder; entryPoint is the Namespace.Type.Method the
    // game should invoke on it. See ModInfo.InjectionDll for why this is
    // preferred over a raw launchArguments string for code-mods.
    public class LauncherInjection
    {
        public string dll { get; set; }
        public string entryPoint { get; set; }
    }
}
