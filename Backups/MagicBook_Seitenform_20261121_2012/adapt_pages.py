"""Transfer the approved curved page profiles into the current book rig."""
from pathlib import Path
import bpy
from mathutils import Matrix

PROJECT = Path(r'D:/GameDesign/Hotzenplotz/Unity/HP_3')
SOURCE = PROJECT / 'BlenderSource/Study_Room/Magic_Book_Pages.blend'
REFERENCE = PROJECT / 'Backups/MagicBook_Seitenform_20261121_2012/Referenz_live.blend'

bpy.ops.wm.open_mainfile(filepath=str(REFERENCE))
reference = {}
for frame in range(31):
    bpy.context.scene.frame_set(frame)
    dg = bpy.context.evaluated_depsgraph_get()
    to_cover = bpy.data.objects['Cube'].matrix_world.inverted()
    for index in range(1, 11):
        obj = bpy.data.objects[f'Page_{index:02d}'].evaluated_get(dg)
        reference[index, frame] = [to_cover @ obj.matrix_world @ v.co for v in obj.data.vertices]

bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
scene = bpy.context.scene
cover = bpy.data.objects['Cube']
pages = [bpy.data.objects[f'Page_{i:02d}'] for i in range(1,11)]
hinges = {}
for frame in range(24,55):
    scene.frame_set(frame)
    dg = bpy.context.evaluated_depsgraph_get()
    to_cover = cover.matrix_world.inverted()
    for index, obj in enumerate(pages, 1):
        ev = obj.evaluated_get(dg)
        hinges[index, frame] = (to_cover @ ev.matrix_world @ ev.data.vertices[0].co).x

for index, page in enumerate(pages, 1):
    poses = {}
    for frame in range(24,55):
        points = [p.copy() for p in reference[11-index, 54-frame]]
        reference_hinge = points[0].x
        for point in points:
            # Fit the reference profile to this cover's usable width.
            point.x = hinges[index, frame] + 0.808 * (point.x-reference_hinge)
        poses[frame] = points
    page.animation_data_clear()
    page.shape_key_clear()
    page.parent = cover
    page.matrix_parent_inverse = Matrix.Identity(4)
    page.matrix_basis = Matrix.Identity(4)
    for vertex, point in zip(page.data.vertices, poses[24]):
        vertex.co = point
    page.data.update()
    page.shape_key_add(name='Basis', from_mix=False)
    for frame in range(25,55):
        shape = page.shape_key_add(name=f'Umblatt_{frame-24:02d}', from_mix=False)
        for vertex, point in zip(shape.data, poses[frame]):
            vertex.co = point
        for key_frame,value in ((0,0.0),(frame-1,0.0),(frame,1.0)):
            shape.value = value
            shape.keyframe_insert(data_path='value',frame=key_frame)
        if frame < 54:
            shape.value = 0.0
            shape.keyframe_insert(data_path='value',frame=frame+1)
    action = page.data.shape_keys.animation_data.action
    # Remove the detached former action before reclaiming the logical name.
    old = bpy.data.actions.get(f'Page_{index:02d}_Umblaettern')
    if old and old != action:
        assert old.users == 0
        bpy.data.actions.remove(old)
    action.name = f'Page_{index:02d}_Umblaettern'
    for curve in action.fcurves:
        for key in curve.keyframe_points:
            key.interpolation = 'LINEAR'

for action in list(bpy.data.actions):
    if action.users == 0:
        bpy.data.actions.remove(action)
assert len(bpy.data.actions) == 13, [(a.name,a.users) for a in bpy.data.actions]

def world_points(obj):
    ev = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    return [ev.matrix_world @ v.co for v in ev.data.vertices]

scene.frame_set(0)
closed = {p.name: world_points(p) for p in pages}
max_stationary_error = 0.0
for frame in range(25):
    scene.frame_set(frame)
    for page in pages:
        max_stationary_error = max(max_stationary_error, max((a-b).length for a,b in zip(closed[page.name],world_points(page))))
assert max_stationary_error < 0.00002, max_stationary_error
print('OPENING_PAGES_STATIONARY_ERROR',max_stationary_error)

for frame in (24,54):
    scene.frame_set(frame)
    dg=bpy.context.evaluated_depsgraph_get()
    inv=cover.matrix_world.inverted()
    heights=[]
    for page in pages:
        ev=page.evaluated_get(dg)
        heights.append([(inv @ ev.matrix_world @ ev.data.vertices[i].co).y for i in range(0,26,2)])
    sign=1 if frame==24 else -1
    for a,b in zip(heights,heights[1:]):
        # The outer edges and curved bodies keep the required page order.
        assert all(sign*(x-y)>0.02 for x,y in zip(a[1:],b[1:])), (frame,a,b)
print('PAGE_ORDER_VERIFIED_BOTH_STACKS')
scene.frame_start=0
scene.frame_end=56
scene.frame_set(0)
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))
print('SAVED',SOURCE)
