# Capture saved animation assets

This diagnostic runs through the project's Unity CLI Pipeline bridge. It does not belong under Assets and does not add runtime/editor assemblies to the game.

With the **main project Editor in Edit Mode**, run from the repository root:

```powershell
unity command script run --caller plugin --skill unity-cli --project-path 'C:\Projects\unity-projects\get-the-bag' --format json --file 'Tools/AnimationBaseline/CaptureAssetInventory.cs'
```

Change project-path for another checkout. The tool writes `Docs/Animation/Baseline/<UTC-date_time>/asset-inventory.json` and returns its path. Each run gets a new folder; keep frozen records separate from later captures. The main Editor must have the current assets imported. It reads saved prefab/clip/controller/import/Addressable bindings; it does not reimport, save or modify those assets.

The tool intentionally uses the current Player, pistol and debug-rifle asset paths as a fixed regression fixture. Update those explicit paths deliberately when migrating content; do not silently substitute a different asset when one is missing. This is a snapshot diagnostic, not general rig compatibility validation or a multiplayer pass/fail test.

For the connected host/observer sequence and what must be reviewed in motion, follow [Milestone 0](../../Docs/Animation/MILESTONE-0.md). Re-run the inventory after a planned binding migration and compare hierarchy/GUID/import/tuning fields before accepting the result.
