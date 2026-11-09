# Animation 1.0: daily workflow

Use this guide for the current Pistol A and compatible arms/body family. The release candidate
uses Unity 6000.6.2f1 and Blender 5.2.3. See [the release checklist](V1-CHECKLIST.md) for acceptance
status. Different skeletons or proportions need their own compatibility/contact review.

## What you animate

| Track | Author | Runtime adds |
| --- | --- | --- |
| First Person | Arms, fingers and pistol together, composed for the camera; slide/magazine contact | ADS alignment, sway and recoil |
| World body | Holding/reload performance, fingers and support-hand motion; right hand carries the pistol | Look direction and holding-hand correction outside authored actions |
| World weapon | Slide, magazine and other mechanisms relative to the held pistol | Attachment to the animated right-hand socket |
| Body locomotion | Existing movement/stance clips | Post-motor movement data drives the Animator; NetworkAnimator replicates locomotion |

The movement motor owns the player root. Leave Animator root motion disabled. The world mechanism
export removes body carry motion so Unity does not apply it twice. Keep calibrated bone names,
rig hierarchy and conversion transforms. Separate runtime objects still share one source pistol.

## Update the reference pistol

1. Open [Pistol_A_Authoring.blend](../../SourceArt/Pistol_A/Pistol_A_Authoring.blend).
   In a 3D View, press **N > Pistol**. If the panel is absent, run the embedded **Pistol Animation
   Tools.py** text once. It loads the maintained [exporter](../../Tools/Blender/PistolAnimationTools.py).
   Global automatic-script execution is optional.
2. Choose **Reload - Normal**, **Reload - Empty** or **Idle - Empty**, then **Preview Action**.
   This selects the matching tracks/ranges in both scenes. **World is a scene**, selected from
   Blender's Scene selector. Its Body rig is the current baked FK rig; FP retains its hand controls.
3. Edit the keyed performance in context. World body and mechanism must use the same timeline.
   Normal/Empty reload use FP frames 0-60 and World frames 1-73 at 30 fps. Empty idle uses FP 0-64
   and World 1-31. World Empty slide release currently closes over frames 60-64.
4. Save source changes. Keep Unity stopped in Edit Mode. Check **Unity Project** in the panel:
   its saved relative path points to the checkout containing this source file. **Export to Unity**
   updates the existing action FBXs and keeps backups under `.codex/animation-exports/`. Existing
   `.meta` files and import settings stay in place. **Export to Staging** writes review files under
   this Blender file's `Exported/WorkflowCheck` folder instead.
5. Let Unity finish importing. Open `WEAPPRES_Pistol_A` under
   `Assets/_Project/Data/Weapons/Visual Definitions/Weapon/`. Expand **Reload Normal** or
   **Reload Empty**: First Person Clip, Body Clip, Weapon Clip (world mechanism), Support Hand Weight.
   Existing assignments survive re-export. Do not replace the controller's original template slots.
6. Tune **Support Hand Weight** for that variant. X is gameplay progress 0-1; Y is holding correction
   0-1. Zero leaves the authored hand free. The current curves release after 10% and recover after
   85%. Missing Empty body motion uses Normal's body clip and curve together. An assigned Empty body
   uses its own curve, even if both entries reference the same clip. An empty curve means weight 0.
7. Select the character prefab `CHAR_PF_Player` and its pistol WeaponDefinition, then run
   **Tools > Animation > Validate Selected Character and Weapons**. Fix errors before testing.
8. Run the combat gym and review both owner and observer views: loaded/empty idle, fire, both
   reloads, moving reload, cancellation, switching and downed/recovery. Check hand/magazine contact
   and the owner's body/world shadows. A successful import alone does not certify final art.

Gameplay owns action selection, ammunition and duration. For a 1.6-second reload, a 2-second FP clip
plays at 1.25x and 2.4-second body/mechanism clips at 1.5x. At gameplay time 0.8 seconds, progress is
0.5: FP samples at 1 second, body/mechanism at 1.2 seconds, and the selected hand curve at 0.5.

## Files you normally touch

- `SourceArt/Pistol_A/Pistol_A_Authoring.blend`: performances and preview scenes.
- `SourceArt/Pistol_A/Models/Pistol_A_Model.blend`: shared model/rest rig; changing its skeleton is a compatibility change.
- `Assets/_Project/Animations/Weapons/Pistol_A`: imported clips/controllers, preserving metadata.
- `WEAPPRES_Pistol_A`: clips, compatibility IDs and per-variant hand curves.
- WeaponDefinition: gameplay duration/ammunition and visual prefab references.

The `.blend` files use Git LFS. Textures needed by their preview are packed; the master library link
is relative. Keep SourceArt and Tools in their repository locations. Do not commit regenerated
Exported folders, `.blend1` backups or `.codex` reports.

## Add another compatible weapon

Use the current weapon definitions and view prefabs as examples. Configure its own compatible
clips, hand/socket calibration, presentation data and gameplay definition, then validate the
explicit character/weapon pair. Reuse the body locomotion controller for the same body family.
The Pistol panel exports the reference pistol only; it does not generate other weapons' prefabs
or repair skeletons. The debug rifle explicitly reuses the pistol body hold/Normal reload and its
hand curve; Empty body motion falls back to that Normal entry. Its visual controller defaults
remain in use. This is placeholder switching/fallback content, not a finished rifle performance.

## Repeatable developer checks

With this checkout's Editor open, stopped, and its active scene clean:

```powershell
./Tools/Animation/Run-Checks.ps1
```

Checks run outside Assets and do not add runtime components. Reports go to `.codex/animation-checks`.
The [validation guide](VALIDATION.md) describes the existing saved-asset checks. Connected-client
and visual acceptance are recorded separately in the 1.0 checklist.
