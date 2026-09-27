# DLL-injection launch mechanism

DW2 loads third-party code mods via a native command-line flag:

```
--low-level-inject <dllPath>!<Namespace.Type.Method>
```

`DWCommandLineArgs.LowLevelInjections` (see `decomp/DistantWorlds.Core/DistantWorlds.Core/DWCommandLineArgs.cs`)
is typed `IEnumerable<string>`, a `CommandLineParser` "sequence" option: **one** occurrence of
`--low-level-inject` carries several space-separated `dll!entryPoint` targets. A second
*occurrence* of the flag does not merge with the first, it replaces it. So the launcher must
compose every enabled code-mod's injection target into a single flag itself; mods cannot each
append their own `--low-level-inject ...` string and have them combine.

## Manifest schema

A mod declares its injection target declaratively, in either `mod.json` (`launcher.injection`)
or `launcher.json` (`injection`) - both are read, see `ModScanner.ReadModInfo` and
`MainForm.Ini.ReadLauncherMeta`:

```json
{
  "launcher": {
    "injection": {
      "dll": "MyMod.dll",
      "entryPoint": "MyMod.Bootstrap.Init"
    }
  }
}
```

- `dll` is a path **relative to the mod's own content folder** (`ModInfo.ContentRoot`, falling
  back to `ModInfo.Folder`). The launcher resolves it to an absolute path at launch time, so
  this works identically for a local "managed" mod and a Steam Workshop mod - nothing needs to
  be copied into a shared folder for injection to find it.
- `entryPoint` is the `Namespace.Type.Method` the game invokes on that DLL.

This `injection` field is the only manifest mechanism the launcher uses to build the
`--low-level-inject` flag. A raw `launchArguments` string is not read for this purpose - a mod
that only sets `launchArguments` does not get injected.

## How the launcher builds the flag

`MainForm.Launch.CollectInjectionTargets` gathers every enabled mod's target, in load order,
de-duplicated. `BuildLaunchArguments` composes them into one flag:

```
--low-level-inject "C:\...\ModA.dll"!Ns.MethodA "C:\...\ModB.dll"!Ns2.MethodB
```

A path is quoted only when it contains a space. `MainForm.Conflicts.BuildLaunchDiagnostics`
checks that every declared target's DLL exists on disk before launch, surfacing a warning if
one is missing.

Because injection targets are resolved directly from each mod's own folder, no mod needs a
shared loader, a shared load-order file, or an installer script to participate - enabling a
mod in the launcher's MOD list is the only step required.
