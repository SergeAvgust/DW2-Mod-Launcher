# DLLインジェクション起動の仕組み

*[English](dll-injection.md) | 日本語*

DW2 は、ネイティブのコマンドライン引数を介してサードパーティ製のコードMODを読み込みます:

```
--low-level-inject <dllPath>!<Namespace.Type.Method>
```

`DWCommandLineArgs.LowLevelInjections`(`decomp/DistantWorlds.Core/DistantWorlds.Core/DWCommandLineArgs.cs`
を参照)は `IEnumerable<string>` 型で、`CommandLineParser` の「sequence(連続値)」オプションです。
`--low-level-inject` は**1回だけ**指定でき、その1回でスペース区切りの複数の `dll!entryPoint` ターゲットを
まとめて渡します。このフラグを2回目に指定しても最初の指定とはマージされず、**上書き**されてしまいます。
そのため、ランチャー側で有効な全コードMODの注入ターゲットを1つのフラグにまとめて構築する必要があります。
各MODがそれぞれ独自に `--low-level-inject ...` を追加しても、それらは合成されません。

## マニフェストのスキーマ

MODは注入ターゲットを宣言的に指定します。`mod.json` の `launcher.injection`、または `launcher.json` の
`injection` のいずれかに記述でき、両方とも読み込まれます(`ModScanner.ReadModInfo` および
`MainForm.Ini.ReadLauncherMeta` を参照):

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

- `dll` は **MOD自身のコンテンツフォルダーからの相対パス**です(`ModInfo.ContentRoot`、なければ
  `ModInfo.Folder` にフォールバック)。ランチャーは起動時にこれを絶対パスへ解決するため、ローカルの
  「管理対象」MODでもSteam Workshop MODでも同じように動作します。注入のために共有フォルダーへファイルを
  コピーする必要はありません。
- `entryPoint` は、ゲームがそのDLLに対して呼び出す `Namespace.Type.Method` です。

この `injection` フィールドが、ランチャーが `--low-level-inject` フラグを構築する際に使用する唯一の
マニフェスト機構です。この目的のために生の `launchArguments` 文字列が読まれることはありません -
`launchArguments` のみを設定したMODは注入されません。

## ランチャーによるフラグの構築

`MainForm.Launch.CollectInjectionTargets` が、有効な全MODのターゲットをロード順に、重複を除いて収集します。
`BuildLaunchArguments` がそれらを1つのフラグへ合成します:

```
--low-level-inject "C:\...\ModA.dll"!Ns.MethodA "C:\...\ModB.dll"!Ns2.MethodB
```

パスはスペースを含む場合のみ引用符で囲まれます。`MainForm.Conflicts.BuildLaunchDiagnostics` は起動前に、
宣言された全ターゲットのDLLがディスク上に存在するかを確認し、見つからない場合は警告を表示します。

注入ターゲットは各MOD自身のフォルダーから直接解決されるため、参加するために共有ローダーや共有ロード順
ファイル、インストーラースクリプトは不要です。ランチャーのMOD一覧でMODを有効化するだけで参加できます。
