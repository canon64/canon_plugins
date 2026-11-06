# MainGameDisplayEffectViewer

Maintained source and build location: `F:\kks\work\plugins\canon_plugins\MainGameDisplayEffectViewer`.
Source imported from `F:\kks\work\plugins\devtools\MainGameDisplayEffectViewer` on 2026-10-27; subsequent edits and builds belong here.

Build: `dotnet build MainGameDisplayEffectViewer.csproj -c Release`.
Install only `bin\Release\net472\MainGameDisplayEffectViewer.dll` into `F:\kks\BepInEx\plugins\canon_plugins\MainGameDisplayEffectViewer`. Remove the old `plugins\devtool\MainGameDisplayEffectViewer\MainGameDisplayEffectViewer.dll` when migrating, and copy its settings JSON to the new location.

`[Logging] LogEnabled` in `F:\kks\BepInEx\config\com.kks.main.displayeffectviewer.cfg` is the master switch (default `false`). It is exposed to BepInEx ConfigurationManager and changes take effect immediately. When enabled, the existing JSON settings `EnableFileLog` and `EnableUnityLog` choose the outputs. When disabled, startup messages and file creation stop. Warnings remain available.
