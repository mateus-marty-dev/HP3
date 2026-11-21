"""Export the book's opening and ten page turns as eleven Unity clips.

Run in a separate Blender 4.2 process with Magic_Book_Pages.blend loaded.
An optional output filename can follow Blender's -- separator.
The source .blend is never saved by this exporter.
"""

from datetime import datetime
from pathlib import Path
from shutil import copy2
import sys

import bpy
from io_scene_fbx import export_fbx_bin, parse_fbx
from mathutils import Matrix


def bake_page(page, root):
    """Preserve page shear and bending with sampled, exportable geometry."""
    scene = bpy.context.scene
    samples = {}
    for frame in range(55):
        scene.frame_set(frame)
        evaluated = page.evaluated_get(bpy.context.evaluated_depsgraph_get())
        to_root = root.matrix_world.inverted() @ evaluated.matrix_world
        samples[frame] = [to_root @ vertex.co for vertex in evaluated.data.vertices]

    if page.animation_data:
        page.animation_data.action = None
    page.shape_key_clear()
    page.parent = root
    page.matrix_parent_inverse = Matrix.Identity(4)
    page.matrix_basis = Matrix.Identity(4)
    for vertex, position in zip(page.data.vertices, samples[0]):
        vertex.co = position
    page.data.update()
    page.shape_key_add(name="Basis", from_mix=False)
    for frame in range(1, 55):
        shape = page.shape_key_add(name=f"Seitenbewegung_{frame:02d}", from_mix=False)
        for vertex, position in zip(shape.data, samples[frame]):
            vertex.co = position
        for key_frame, value in ((0, 0.0), (frame - 1, 0.0), (frame, 1.0)):
            shape.value = value
            shape.keyframe_insert(data_path="value", frame=key_frame)
        if frame < 54:
            shape.value = 0.0
            shape.keyframe_insert(data_path="value", frame=frame + 1)
    for curve in page.data.shape_keys.animation_data.action.fcurves:
        for key in curve.keyframe_points:
            key.interpolation = "LINEAR"


def bake_spine(spine):
    """Transfer Blender Hook deformation to exportable blend shapes."""
    scene = bpy.context.scene
    assert spine.data.shape_keys is None
    samples = {}
    for frame in range(25):
        scene.frame_set(frame)
        evaluated = spine.evaluated_get(bpy.context.evaluated_depsgraph_get())
        samples[frame] = [vertex.co.copy() for vertex in evaluated.data.vertices]

    assert max((a - b.co).length for a, b in zip(samples[0], spine.data.vertices)) < 0.00002
    spine.shape_key_add(name="Basis", from_mix=False)
    for modifier in spine.modifiers:
        assert modifier.type == "HOOK"
        modifier.show_viewport = False
        modifier.show_render = False

    for frame in range(1, 25):
        shape = spine.shape_key_add(name=f"Ruecken_Oeffnung_{frame:02d}", from_mix=False)
        for vertex, position in zip(shape.data, samples[frame]):
            vertex.co = position
        keys = [(0, 0.0), (frame - 1, 0.0), (frame, 1.0)]
        if frame < 24:
            keys.append((frame + 1, 0.0))
        for key_frame, value in keys:
            shape.value = value
            shape.keyframe_insert(data_path="value", frame=key_frame)

    for curve in spine.data.shape_keys.animation_data.action.fcurves:
        for key in curve.keyframe_points:
            key.interpolation = "LINEAR"

    for frame in range(25):
        scene.frame_set(frame)
        evaluated = spine.evaluated_get(bpy.context.evaluated_depsgraph_get())
        error = max((a - b.co).length for a, b in zip(samples[frame], evaluated.data.vertices))
        assert error < 0.00002, ("Spine bake changed geometry", frame, error)


def capture_pose(objects, shape_owners, frame):
    bpy.context.scene.frame_set(frame)
    transforms = {
        obj: {
            path: getattr(obj, path).copy()
            for path in ("location", "rotation_euler", "rotation_quaternion", "scale")
        }
        for obj in objects
    }
    shapes = {
        owner: {shape.name: shape.value for shape in owner.key_blocks}
        for owner in shape_owners
    }
    return transforms, shapes


def apply_pose(pose, objects, shape_owners):
    transforms, shapes = pose
    for obj in objects:
        if obj.animation_data:
            obj.animation_data.action = None
        for path, value in transforms[obj].items():
            setattr(obj, path, value)
    for owner in shape_owners:
        owner.animation_data.action = None
        for name, value in shapes[owner].items():
            owner.key_blocks[name].value = value


def verify_fbx(path):
    root, _version = parse_fbx.parse(str(path))
    objects = next(element for element in root.elems if element.id == b"Objects")
    names = [
        element.props[1].split(b"\x00\x01")[0].decode()
        for element in objects.elems
        if element.id == b"AnimationStack"
    ]
    expected = ["Buch_Oeffnen"] + [f"Page_{number:02d}_Umblaettern" for number in range(1, 11)]
    assert sorted(names) == sorted(expected), names
    assert not any("|" in name for name in names)
    print("VERIFIED CLIPS", names)


def main():
    scene = bpy.context.scene
    source = Path(bpy.data.filepath)
    project = source.parents[2]
    arguments = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    output = Path(arguments[0]) if arguments else project / "Assets/GameObjects/Environment/Study_Room/Magic_Book_Pages.fbx"
    assert source.name == "Magic_Book_Pages.blend"

    pages = [bpy.data.objects[f"Page_{number:02d}"] for number in range(1, 11)]
    root = bpy.data.objects["Book_Root"]
    objects = [root] + list(root.children_recursive)
    objects = [obj for obj in objects if obj.type in {"MESH", "EMPTY"}]
    spine = bpy.data.objects["Cube.003"]
    assert spine in objects and all(page in objects for page in pages)
    markers = [bpy.data.actions.new("Buch_Oeffnen")] + [
        page.animation_data.action if page.animation_data and page.animation_data.action
        else page.data.shape_keys.animation_data.action
        for page in pages
    ]
    bake_spine(spine)
    for page in pages:
        bake_page(page, root)

    shape_owners = [obj.data.shape_keys for obj in objects if obj.type == "MESH" and obj.data.shape_keys]
    owners = objects + shape_owners
    actions = {owner: owner.animation_data.action for owner in owners if owner.animation_data}
    poses = {frame: capture_pose(objects, shape_owners, frame) for frame in (0, 24, 54)}

    def restore_actions():
        for owner, action in actions.items():
            owner.animation_data.action = action

    def export_clips(scene_data):
        animations = []
        animated = set()

        def add_clip(marker, first, last):
            animation = export_fbx_bin.fbx_animations_do(
                scene_data, marker, first, last, True, force_keep=True
            )
            assert animation is not None
            animations.append(animation)
            for element_key, (_unused, curve_nodes) in animation[1].items():
                for property_name in curve_nodes:
                    animated.add((element_key, property_name))

        restore_actions()
        add_clip(markers[0], 0, 24)

        for index, page in enumerate(pages):
            apply_pose(poses[24], objects, shape_owners)
            for previous in pages[:index]:
                for path, value in poses[54][0][previous].items():
                    setattr(previous, path, value)
                for name, value in poses[54][1][previous.data.shape_keys].items():
                    previous.data.shape_keys.key_blocks[name].value = value
            if page.animation_data:
                page.animation_data.action = actions.get(page)
            page.data.shape_keys.animation_data.action = actions[page.data.shape_keys]
            scene.frame_set(24)
            add_clip(markers[index + 1], 24, 54)

        restore_actions()
        scene.frame_set(0)
        return animations, animated, 0.0, 30.0

    output.parent.mkdir(parents=True, exist_ok=True)
    if output.exists() and output.resolve().is_relative_to(project.resolve()):
        backup = project / "Backups" / ("MagicBookExport_" + datetime.now().strftime("%Y%m%d_%H%M%S"))
        backup.mkdir(parents=True)
        copy2(output, backup / output.name)
        metadata = output.with_suffix(output.suffix + ".meta")
        if metadata.exists():
            copy2(metadata, backup / metadata.name)

    for obj in scene.objects:
        obj.select_set(obj in objects)
    bpy.context.view_layer.objects.active = root
    scene.frame_set(0)
    original_export_animations = export_fbx_bin.fbx_animations
    export_fbx_bin.fbx_animations = export_clips
    try:
        result = bpy.ops.export_scene.fbx(
            filepath=str(output), use_selection=True, object_types={"MESH", "EMPTY"},
            axis_forward="-Z", axis_up="Y", global_scale=1.0,
            apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE",
            use_mesh_modifiers=False, mesh_smooth_type="FACE", add_leaf_bones=False,
            bake_anim=True, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=False,
            bake_anim_use_all_bones=False, bake_anim_force_startend_keying=True,
            bake_anim_step=1.0, bake_anim_simplify_factor=0.0, path_mode="ABSOLUTE",
        )
        assert result == {"FINISHED"}, result
    finally:
        export_fbx_bin.fbx_animations = original_export_animations
    verify_fbx(output)
    print("EXPORTED", output)


if __name__ == "__main__":
    main()
