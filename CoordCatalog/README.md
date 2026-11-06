# CoordCatalog

Maintained source and build location: `F:\kks\work\plugins\canon_plugins\CoordCatalog`.
Source imported from the original project at `F:\kks\work\plugins\CoordCatalog` on 2026-10-27; subsequent edits and builds belong here.

Build: `dotnet build CoordCatalog.csproj -c Release`.

Logging is controlled by `[Logging] LogEnabled` in `F:\kks\BepInEx\config\koikatsu.coordcatalog.cfg` (default `false`). It is exposed to BepInEx ConfigurationManager and changes take effect immediately. When disabled, normal messages and startup log file creation stop. Capture functionality is independent. Warnings and errors remain available.
