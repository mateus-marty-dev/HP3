"""Keep every page attached to the shared centre of the unfolding spine."""
from pathlib import Path
import bpy
from mathutils import Matrix

source=Path(r'D:/GameDesign/Hotzenplotz/Unity/HP_3/BlenderSource/Study_Room/Magic_Book_Pages.blend')
original=source.parents[2]/'Backups/MagicBook_Zentrierung_20261121_2048/Magic_Book_Pages.blend'
bpy.ops.wm.open_mainfile(filepath=str(original))
scene=bpy.context.scene
cover=bpy.data.objects['Cube']
front=bpy.data.objects['Cover_Front_Pivot']
back=bpy.data.objects['Cover_Back_Pivot']
pages=[bpy.data.objects[f'Page_{i:02d}'] for i in range(1,11)]
samples={}
centres={}
scene.frame_set(24)
inv=cover.matrix_world.inverted()
open_centre=(inv@front.matrix_world.translation+inv@back.matrix_world.translation)*0.5
open_scales={}
for page in pages:
    ev=page.evaluated_get(bpy.context.evaluated_depsgraph_get())
    pts=[inv@ev.matrix_world@v.co for v in ev.data.vertices]
    hinge=(pts[0].x+pts[1].x)*0.5
    tip=(pts[24].x+pts[25].x)*0.5
    open_scales[page.name]=(open_centre.x-tip)/(hinge-tip)
for frame in range(55):
    scene.frame_set(frame)
    dg=bpy.context.evaluated_depsgraph_get()
    inv=cover.matrix_world.inverted()
    centres[frame]=(inv@front.matrix_world.translation+inv@back.matrix_world.translation)*0.5
    for page in pages:
        ev=page.evaluated_get(dg)
        points=[inv@ev.matrix_world@v.co for v in ev.data.vertices]
        old_x=(points[0].x+points[1].x)*0.5
        old_z=(points[0].z+points[1].z)*0.5
        if frame<=24:
            tip=(points[24].x+points[25].x)*0.5
            width_scale=(centres[frame].x-tip)/(old_x-tip)
        else:
            width_scale=open_scales[page.name]
        for point in points:
            point.x=centres[frame].x+(point.x-old_x)*width_scale
            point.z-=old_z
        samples[page.name,frame]=points

for page in pages:
    page.animation_data_clear()
    page.shape_key_clear()
    page.parent=cover
    page.matrix_parent_inverse=Matrix.Identity(4)
    page.matrix_basis=Matrix.Identity(4)
    for vertex,point in zip(page.data.vertices,samples[page.name,0]):
        vertex.co=point
    page.data.update()
    page.shape_key_add(name='Basis',from_mix=False)
    for frame in range(1,55):
        shape=page.shape_key_add(name=f'Buchbindung_{frame:02d}',from_mix=False)
        for vertex,point in zip(shape.data,samples[page.name,frame]):
            vertex.co=point
        for at,value in ((0,0.0),(frame-1,0.0),(frame,1.0)):
            shape.value=value
            shape.keyframe_insert(data_path='value',frame=at)
        if frame<54:
            shape.value=0.0
            shape.keyframe_insert(data_path='value',frame=frame+1)
    action=page.data.shape_keys.animation_data.action
    old=bpy.data.actions.get(page.name+'_Umblaettern')
    if old and old!=action:
        assert old.users==0
        bpy.data.actions.remove(old)
    action.name=page.name+'_Umblaettern'
    for curve in action.fcurves:
        for key in curve.keyframe_points:
            key.interpolation='LINEAR'

for action in list(bpy.data.actions):
    if action.users==0:
        bpy.data.actions.remove(action)
assert len(bpy.data.actions)==13
max_error=0
for frame in range(55):
    scene.frame_set(frame)
    inv=cover.matrix_world.inverted()
    dg=bpy.context.evaluated_depsgraph_get()
    for page in pages:
        ev=page.evaluated_get(dg)
        a=inv@ev.matrix_world@ev.data.vertices[0].co
        b=inv@ev.matrix_world@ev.data.vertices[1].co
        max_error=max(max_error,abs((a.x+b.x)*0.5-centres[frame].x))
assert max_error<0.00001,max_error
print('SHARED_BINDING_LINE_VERIFIED_ALL_PAGES_ALL_FRAMES',max_error)
scene.frame_set(0)
bpy.ops.wm.save_as_mainfile(filepath=str(source))
