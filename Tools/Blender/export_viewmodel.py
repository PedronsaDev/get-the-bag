"""Run with Blender --background --factory-startup --disable-autoexec SOURCE.blend
--python Tools/Blender/export_viewmodel.py -- --output DIRECTORY --name Pistol_A.
Exports evaluated pairs without saving or modifying the source .blend file.
"""
import argparse
import json
import pathlib
import sys
import bpy
from mathutils import Matrix

parser = argparse.ArgumentParser()
parser.add_argument('--output', required=True)
parser.add_argument('--name', required=True)
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
output = pathlib.Path(args.output)
output.mkdir(parents=True, exist_ok=True)
arms = bpy.data.objects['Arms_Rig']
weapon = bpy.data.objects['Weap_Rig']
scene = bpy.context.scene
camera = scene.camera
assert camera, 'An authoring camera is required for view alignment.'
actions = {a.name.lower(): a for a in bpy.data.actions}
meshes = [o for o in scene.objects if o.type == 'MESH' and
          any(m.type == 'ARMATURE' and m.object in (arms, weapon) for m in o.modifiers)]
assert len(meshes) >= 2, 'Expected arms and weapon skinned meshes.'
for rig in (arms, weapon):
    for track in rig.animation_data.nla_tracks:
        track.mute = True

def activate(rig, action):
    rig.animation_data.action = action
    if action.slots:
        rig.animation_data.action_slot = action.slots[0]

def pair(suffix):
    left, right = actions['fp_' + suffix], actions['wep_' + suffix]
    activate(arms, left)
    activate(weapon, right)
    scene.frame_start = int(min(left.frame_range[0], right.frame_range[0]))
    scene.frame_end = max(scene.frame_start + 1, int(max(left.frame_range[1], right.frame_range[1])))
    scene.frame_set(scene.frame_start)
    bpy.context.view_layer.update()

def export(filename, objects, animation):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.hide_set(False)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = arms
    bpy.ops.export_scene.fbx(
        filepath=str(output / filename), use_selection=True,
        object_types={'ARMATURE', 'MESH', 'CAMERA', 'EMPTY'},
        axis_forward='-Z', axis_up='Y', global_scale=1.0,
        apply_scale_options='FBX_SCALE_ALL', bake_space_transform=False,
        add_leaf_bones=False, use_armature_deform_only=True,
        bake_anim=animation, bake_anim_use_all_actions=False,
        bake_anim_use_nla_strips=False, bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0, bake_anim_force_startend_keying=True,
        path_mode='AUTO')

pair('idle_pose')
# A plain transform survives camera import settings and cannot introduce a second camera.
reference = bpy.data.objects.new('ViewReference', None)
scene.collection.objects.link(reference)
reference.matrix_world = camera.matrix_world @ Matrix.Diagonal((-1.0, 1.0, -1.0, 1.0))
export(args.name + '_Model.fbx', [arms, weapon, reference] + meshes, False)
manifest = {'source': bpy.data.filepath, 'fps': scene.render.fps / scene.render.fps_base,
            'weapon': args.name, 'clips': []}
for suffix in ('idle', 'equip', 'fire', 'reload', 'reload_empty', 'sprint', 'inspect'):
    if 'fp_' + suffix not in actions or 'wep_' + suffix not in actions:
        continue
    pair(suffix)
    filename = args.name + '_' + suffix + '.fbx'
    export(filename, [arms, weapon, reference], True)
    manifest['clips'].append({'name': suffix, 'file': filename,
                              'start': scene.frame_start, 'end': scene.frame_end})
(output / (args.name + '_export.json')).write_text(json.dumps(manifest, indent=2), encoding='utf8')
print('VIEWMODEL_EXPORT', json.dumps(manifest))
