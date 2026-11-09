import bpy
from pathlib import Path
from mathutils import Matrix
from datetime import datetime
import shutil

ACTIONS = {
    'RELOAD_NORMAL': {
        'label': 'Reload - Normal', 'suffix': 'Reload_Normal',
        'arms': 'FP_Pistol_A_Reload_Normal_Arms', 'fp_weapon': 'FP_Pistol_A_Reload_Normal_Weapon',
        'body': 'Body_Pistol_A_Reload_Normal', 'world_weapon': 'WEP_Pistol_A_Reload_Normal',
        'fp_frames': (0, 60), 'world_frames': (1, 73),
    },
    'RELOAD_EMPTY': {
        'label': 'Reload - Empty', 'suffix': 'Reload_Empty',
        'arms': 'FP_Pistol_A_Reload_Empty_Arms', 'fp_weapon': 'FP_Pistol_A_Reload_Empty_Weapon',
        'body': 'Body_Pistol_A_Reload_Empty', 'world_weapon': 'WEP_Pistol_A_Reload_Empty',
        'fp_frames': (0, 60), 'world_frames': (1, 73),
    },
    'IDLE_EMPTY': {
        'label': 'Idle - Empty', 'suffix': 'Idle_Empty',
        'arms': 'FP_Pistol_A_Idle_Arms', 'fp_weapon': 'FP_Pistol_A_Idle_Empty_Weapon',
        'body': 'Body_Pistol_A_Hold', 'world_weapon': 'WEP_Pistol_A_Idle_Empty',
        'fp_frames': (0, 64), 'world_frames': (1, 31),
    },
}
UNITY_FOLDERS = {
    'FP_Pistol_A_Reload_Normal.fbx': 'Reload/Normal',
    'Body_Pistol_A_Reload_Normal.fbx': 'Reload/Normal',
    'WEP_Pistol_A_Reload_Normal.fbx': 'Reload/Normal',
    'FP_Pistol_A_Reload_Empty.fbx': 'Reload',
    'Body_Pistol_A_Reload_Empty.fbx': 'Reload/Empty',
    'WEP_Pistol_A_Reload_Empty.fbx': 'Reload/Empty',
    'FP_Pistol_A_Idle_Empty.fbx': 'Idle',
    'WEP_Pistol_A_Idle_Empty.fbx': 'Idle',
}

SETTINGS = dict(use_selection=True, object_types={'ARMATURE', 'MESH'},
    add_leaf_bones=False, use_armature_deform_only=True, bake_anim=True,
    bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=False,
    bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0,
    apply_unit_scale=True, apply_scale_options='FBX_SCALE_NONE', axis_forward='-Z', axis_up='Y',
    use_space_transform=True, bake_space_transform=False, primary_bone_axis='Y',
    secondary_bone_axis='X', armature_nodetype='NULL')

def rigs():
    fp, world = bpy.data.scenes['First Person'], bpy.data.scenes['World']
    return fp, world, (fp.objects['Arms_Rig'], fp.objects['Weap_Rig'],
                       world.objects['Body'], world.objects['Weapon_Pistol'])

def preview_action(action_id):
    config = ACTIONS[action_id]
    fp, world, objects = rigs()
    for obj, key in zip(objects, ('arms', 'fp_weapon', 'body', 'world_weapon')):
        name = config[key]
        action = bpy.data.actions[name]
        obj.animation_data.action = action
        obj.animation_data.action_slot = action.slots[0]
    for scene, bounds in ((fp, config['fp_frames']), (world, config['world_frames'])):
        scene.frame_start, scene.frame_end = bounds
        scene.frame_set(bounds[0])

def capture_state():
    fp, world, objects = rigs()
    state = {'scene': bpy.context.window.scene, 'mode': bpy.context.object.mode if bpy.context.object else 'OBJECT',
             'scenes': [], 'rigs': [], 'weapon_basis': objects[3].matrix_basis.copy(),
             'attachment_mute': objects[3].constraints['Reload_RightHand_Attachment'].mute}
    for scene in (fp, world):
        layer = scene.view_layers[0]
        state['scenes'].append((scene, scene.frame_start, scene.frame_end, scene.frame_current,
            [obj for obj in scene.objects if obj.select_get(view_layer=layer)], layer.objects.active))
    for obj in objects:
        poses = [(bone, bone.location.copy(), bone.rotation_quaternion.copy(), bone.rotation_euler.copy(),
                  tuple(bone.rotation_axis_angle), bone.scale.copy(), [(c, c.influence) for c in bone.constraints])
                 for bone in obj.pose.bones]
        state['rigs'].append((obj, obj.animation_data.action, obj.animation_data.action_slot, poses))
    return state

def restore_state(state):
    weapon = bpy.data.objects['Weapon_Pistol']
    weapon.constraints['Reload_RightHand_Attachment'].mute = state['attachment_mute']
    weapon.matrix_basis = state['weapon_basis']
    for obj, action, slot, poses in state['rigs']:
        obj.animation_data.action = action
        obj.animation_data.action_slot = slot
    for scene, start, end, frame, selected, active in state['scenes']:
        bpy.context.window.scene = scene
        scene.frame_start, scene.frame_end = start, end
        scene.frame_set(frame)
        bpy.ops.object.select_all(action='DESELECT')
        for obj in selected:
            obj.select_set(True)
        scene.view_layers[0].objects.active = active

    for obj, action, slot, poses in state['rigs']:
        for bone, location, quaternion, euler, axis_angle, scale, constraints in poses:
            bone.location, bone.rotation_quaternion = location, quaternion
            bone.rotation_euler, bone.rotation_axis_angle, bone.scale = euler, axis_angle, scale
            for constraint, influence in constraints:
                constraint.influence = influence
    bpy.context.window.scene = state['scene']
    if state['mode'] != 'OBJECT':
        bpy.ops.object.mode_set(mode=state['mode'])

def export_file(scene, names, filepath):
    bpy.context.window.scene = scene
    bpy.ops.object.select_all(action='DESELECT')
    for name in names:
        scene.objects[name].select_set(True)
    scene.view_layers[0].objects.active = scene.objects[names[0]]
    scene.frame_set(scene.frame_start)
    original_name = scene.name
    try:
        scene.name = 'Scene'
        if scene.name != 'Scene':
            raise RuntimeError('Another scene uses the reserved Scene export name.')
        bpy.ops.export_scene.fbx(filepath=str(filepath), **SETTINGS)
    finally:
        scene.name = original_name

def export_action(action_id):
    if bpy.context.object and bpy.context.object.mode not in ('OBJECT', 'POSE'):
        raise RuntimeError('Export from Object or Pose Mode.')
    output = Path(bpy.path.abspath('//Exported/WorkflowCheck'))
    output.mkdir(parents=True, exist_ok=True)
    fp, world, objects = rigs()
    state = capture_state()
    suffix = ACTIONS[action_id]['suffix']
    files = []
    try:
        if state['mode'] != 'OBJECT':
            bpy.ops.object.mode_set(mode='OBJECT')
        preview_action(action_id)
        files.append(output / ('FP_Pistol_A_' + suffix + '.fbx'))
        export_file(fp, ['Arms_Rig', 'arms', 'Weap_Rig', 'gun'], files[-1])
        if action_id != 'IDLE_EMPTY':
            files.append(output / ('Body_Pistol_A_' + suffix + '.fbx'))
            export_file(world, ['Body'], files[-1])
        weapon = objects[3]
        weapon.constraints['Reload_RightHand_Attachment'].mute = True
        weapon.matrix_basis = Matrix.Identity(4)
        files.append(output / ('WEP_Pistol_A_' + suffix + '.fbx'))
        export_file(world, ['Weapon_Pistol'], files[-1])
    finally:
        restore_state(state)
    return files

def unity_destinations(action_id, project_folder):
    if not project_folder:
        raise RuntimeError('Choose the Unity project folder first.')
    project = Path(bpy.path.abspath(project_folder)).resolve()
    if not (project / 'ProjectSettings/ProjectVersion.txt').is_file():
        raise RuntimeError('The chosen folder is not a Unity project.')
    suffix = ACTIONS[action_id]['suffix']
    names = ['FP_Pistol_A_' + suffix + '.fbx', 'WEP_Pistol_A_' + suffix + '.fbx']
    if action_id != 'IDLE_EMPTY':
        names.insert(1, 'Body_Pistol_A_' + suffix + '.fbx')
    root = project / 'Assets/_Project/Animations/Weapons/Pistol_A'
    targets = {name: root / UNITY_FOLDERS[name] / name for name in names}
    for path in targets.values():
        if not path.is_file() or not Path(str(path) + '.meta').is_file():
            raise RuntimeError('Export updates existing imported clips only. Missing FBX or .meta: ' + str(path))
    return project, targets

def replace_fbx(source, target):

    temporary = target.with_name('.' + target.name + '.export')
    try:
        shutil.copyfile(source, temporary)
        temporary.replace(target)
    finally:
        temporary.unlink(missing_ok=True)

def export_to_unity(action_id, project_folder):
    project, targets = unity_destinations(action_id, project_folder)
    files = export_action(action_id)
    backup = project / '.codex/animation-exports' / datetime.now().strftime('%Y%m%d-%H%M%S-%f')
    backup.mkdir(parents=True)
    for target in targets.values():
        shutil.copy2(target, backup / target.name)
        shutil.copy2(str(target) + '.meta', backup / (target.name + '.meta'))
    try:
        for source in files:
            replace_fbx(source, targets[source.name])
    except Exception:
        for target in targets.values():
            replace_fbx(backup / target.name, target)
        raise
    return list(targets.values())

class GTB_OT_preview_pistol_action(bpy.types.Operator):
    bl_idname = 'gtb.preview_pistol_action'
    bl_label = 'Preview Action'
    bl_description = 'Select the coordinated takes and frame ranges in both scenes'

    def execute(self, context):
        preview_action(bpy.data.scenes['World'].gtb_pistol_action)
        return {'FINISHED'}

class GTB_OT_export_pistol_action(bpy.types.Operator):
    bl_idname = 'gtb.export_pistol_action'
    bl_label = 'Export to Staging'
    bl_description = 'Export the selected action to Exported/WorkflowCheck; restore the current authoring state'

    def execute(self, context):
        try:
            files = export_action(bpy.data.scenes['World'].gtb_pistol_action)
        except Exception as error:
            self.report({'ERROR'}, str(error))
            return {'CANCELLED'}
        self.report({'INFO'}, 'Exported ' + str(len(files)) + ' FBXs to Exported/WorkflowCheck')
        return {'FINISHED'}

class GTB_OT_export_pistol_unity(bpy.types.Operator):
    bl_idname = 'gtb.export_pistol_unity'
    bl_label = 'Export to Unity'
    bl_description = 'Update the selected action FBXs; preserve Unity metadata and back up previous files'

    def execute(self, context):
        world = bpy.data.scenes['World']
        try:
            files = export_to_unity(world.gtb_pistol_action, world.gtb_unity_project)
        except Exception as error:
            self.report({'ERROR'}, str(error))
            return {'CANCELLED'}
        self.report({'INFO'}, 'Updated ' + str(len(files)) + ' FBXs in Unity. Run animation validation after import.')
        return {'FINISHED'}

class GTB_PT_pistol_animation(bpy.types.Panel):
    bl_label = 'Pistol Animation'
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = 'Pistol'

    @classmethod
    def poll(cls, context):
        return 'First Person' in bpy.data.scenes and 'World' in bpy.data.scenes

    def draw(self, context):
        layout = self.layout
        layout.prop(bpy.data.scenes['World'], 'gtb_pistol_action', text='Action')
        layout.operator('gtb.preview_pistol_action')
        layout.operator('gtb.export_pistol_action')
        layout.prop(bpy.data.scenes['World'], 'gtb_unity_project', text='Unity Project')
        layout.operator('gtb.export_pistol_unity')

def register():
    for cls in (GTB_PT_pistol_animation, GTB_OT_export_pistol_unity, GTB_OT_export_pistol_action, GTB_OT_preview_pistol_action):
        previous = getattr(bpy.types, cls.__name__, None)
        if previous:
            bpy.utils.unregister_class(previous)
    bpy.types.Scene.gtb_pistol_action = bpy.props.EnumProperty(name='Pistol Action',
        items=[(key, config['label'], '') for key, config in ACTIONS.items()], default='RELOAD_EMPTY')
    bpy.types.Scene.gtb_unity_project = bpy.props.StringProperty(name='Unity Project', subtype='DIR_PATH', options={'PATH_SUPPORTS_BLEND_RELATIVE'})
    for cls in (GTB_OT_preview_pistol_action, GTB_OT_export_pistol_action, GTB_OT_export_pistol_unity, GTB_PT_pistol_animation):
        bpy.utils.register_class(cls)

register()
