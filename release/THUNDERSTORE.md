# Thunderstore upload (v1.12.1)

Per [Creating a Package](https://wiki.thunderstore.io/mods/creating-a-package):

1. Build the plugin: `dotnet build TheFloorIsLava.csproj -c Release`
2. Copy build output and config into this folder:
   ```
   release/plugins/TheFloorIsLava.dll
   release/config/com.thefloorislava.whiteknuckle.cfg
   ```
3. Select **these files at the root of `release/`** (not the folder itself) and zip:
   - `manifest.json`
   - `icon.png`
   - `README.md`
   - `plugins/TheFloorIsLava.dll`
   - `config/com.thefloorislava.whiteknuckle.cfg`

   The `config/` folder is routed by mod managers to `BepInEx/config/` (unlike `plugins/`, which lands under a team-package subfolder).
4. Upload the zip to [White Knuckle Thunderstore](https://thunderstore.io/c/white-knuckle/).

**Dependency:** `BepInEx-BepInExPack-5.4.2305` ([BepInExPack](https://thunderstore.io/c/white-knuckle/p/BepInEx/BepInExPack/v/5.4.2305/))

When bumping versions, update `manifest.json`, `VERSION.txt`, and `TheFloorIsLava/Plugin.cs` together.
