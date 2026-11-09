# Animation verification tools

These scripts run through the existing Unity Pipeline CLI, outside Assets. They are developer
checks, not runtime gameplay components. Open the checkout you intend to test and always target
that project explicitly when multiple Editors are running.

With the Editor stopped and the active scene clean:

```powershell
./Tools/Animation/Run-Checks.ps1
```

This runs 92 saved-content, playback, fallback and lifecycle checks. Generated JSON goes to
`.codex/animation-checks`. It does not change saved scenes/prefabs or start networking.

Blender exporter regression (replace the executable with your installed Blender path):

```powershell
blender --background SourceArt/Pistol_A/Pistol_A_Authoring.blend --disable-autoexec --python-exit-code 1 --python Tools/Blender/CheckPistolExport.py
```

It uses a temporary Unity fixture, verifies pose/frame/action/mode restoration, preserves metadata
and exercises failed-copy rollback and invalid destinations. It regenerates ignored staging FBXs.

## Connected smoke checks

The human acceptance procedure is [V1-CHECKLIST](../../Docs/Animation/V1-CHECKLIST.md). For scripted
inspection, enable Pipeline **in development builds only** before building the test Player. Use
the existing settings command with `--settings '{"enable-in-development-builds":true}' --confirm true`,
then restore that setting to false after producing the instrumented test build. Release-build
instrumentation stays disabled.

`Build-SmokePlayer.cs`, entry `BuildSmokePlayer.Main`, builds a Windows development Player of the
combat gym under `.codex/v1/build`. It temporarily enables running in the background and restores
the original Player Setting. Build/run it only from stopped Edit Mode. Long builds should use
the CLI's detached-job support. This is a smoke build, not a replacement production build pipeline.

Start the gym host in the Editor and connect the separate test client with the existing UDP
NetworkManager. The gym's Editor-only quick-play UI is not included in the standalone Player.
Runtime inspection can invoke NetworkManager.StartClient after bootstrap readiness. Keep client
actions in the client process so ownership/RPC checks are actually exercised.

- `Connected-Host-Check.cs`, entry `ConnectedHostCheck.Main`: two connected peers; accepted host
  shots, Normal/Empty reloads, chamber capacity, switching, downing/recovery and presenter re-enable.
  The runtime reload duration is temporarily six seconds and restored in finally; no saved tuning changes.
- `Runtime-Snapshot.cs`: run with `script eval --file` on either peer. Captures selected action,
  clip phase, hand weight, visible views and culling. Reflection is limited to tooling because
  the Player evaluator's default reference list omits custom game assemblies.
- `Client-EmptyReload.cs`, `Client-RebuildDuringReload.cs`, `Client-SwitchCheck.cs`: client-owned
  requests and visual-lifetime checks, run with `script eval --file` in the development Player.
  The rebuild test requires a deliberately long runtime reload; restore tuning/end the test session afterward.
- `Capture-Observer.cs`: optional Player camera capture. It requires a test camera named
  AnimationReviewCamera; a hidden Player may skip rendering. A black capture is not visual evidence.

Do not run connected checks on an active user game or production session. Reports and test builds
are ignored; source art and exporter source are versioned. Record actual results rather than
treating compilation or a screenshot alone as proof that networking/animation advanced.
