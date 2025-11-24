import bpy
import os

# Get the directory of this script
script_dir = os.path.dirname(os.path.abspath(__file__))

# Clear existing objects
bpy.ops.wm.read_factory_settings(use_empty=True)

# Open the Blender file
blend_file = os.path.join(script_dir, "source", "Portal.blend")
bpy.ops.wm.open_mainfile(filepath=blend_file)

# Export to FBX
output_file = os.path.join(script_dir, "WORMHOLE.fbx")
bpy.ops.export_scene.fbx(
    filepath=output_file,
    use_selection=False,
    global_scale=1.0,
    apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_NONE',
    bake_space_transform=False,
    object_types={'MESH', 'ARMATURE'},
    use_mesh_modifiers=True,
    use_mesh_modifiers_render=True,
    mesh_smooth_type='FACE',
    use_custom_props=False,
    add_leaf_bones=False,
    primary_bone_axis='Y',
    secondary_bone_axis='X',
    use_armature_deform_only=False,
    armature_nodetype='NULL',
    bake_anim=True,
    bake_anim_use_all_bones=True,
    bake_anim_use_nla_strips=True,
    bake_anim_use_all_actions=True,
    bake_anim_force_startend_keying=True,
    bake_anim_step=1.0,
    bake_anim_simplify_factor=1.0,
    path_mode='AUTO',
    embed_textures=False,
    batch_mode='OFF',
    use_batch_own_dir=True,
    use_metadata=True,
    axis_forward='-Z',
    axis_up='Y'
)

print(f"Export complete: {output_file}")
