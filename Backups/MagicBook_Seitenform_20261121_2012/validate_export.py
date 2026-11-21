import sys
import bpy
from mathutils import Vector

sys.dont_write_bytecode=True
sys.path.insert(0,r'D:/GameDesign/Hotzenplotz/Unity/HP_3/BlenderSource/Study_Room')
from export_magic_book_unity import capture_pose,apply_pose
scene=bpy.context.scene
root=bpy.data.objects['Book_Root']
objects=[root]+list(root.children_recursive)
pages=[bpy.data.objects[f'Page_{i:02d}'] for i in range(1,11)]
meshes=[o for o in objects if o.type=='MESH']
shape_owners=[o.data.shape_keys for o in meshes if o.data.shape_keys]
actions={o:o.animation_data.action for o in objects+shape_owners if o.animation_data}
poses={f:capture_pose(objects,shape_owners,f) for f in (24,54)}
materials={o.name:[m.name for m in o.data.materials if m] for o in meshes}
def polygon_materials(obj):
    return [obj.data.materials[p.material_index].name if obj.data.materials and obj.data.materials[p.material_index] else None for p in obj.data.polygons]
face_materials={o.name:polygon_materials(o) for o in meshes}
expected={}

def sample(clip,frame,output_frame):
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    dg=bpy.context.evaluated_depsgraph_get()
    expected[clip,output_frame]={o.name:[tuple(o.evaluated_get(dg).matrix_world@v.co) for v in o.evaluated_get(dg).data.vertices] for o in meshes}

for f in range(25):
    sample('Buch_Oeffnen',f,f+1)
for i,page in enumerate(pages):
    apply_pose(poses[24],objects,shape_owners)
    for prev in pages[:i]:
        for path,value in poses[54][0][prev].items():
            setattr(prev,path,value)
        for name,value in poses[54][1][prev.data.shape_keys].items():
            prev.data.shape_keys.key_blocks[name].value=value
    page.data.shape_keys.animation_data.action=actions[page.data.shape_keys]
    for f in range(24,55):
        sample(f'Page_{i+1:02d}_Umblaettern',f,f-23)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=r'D:/GameDesign/Hotzenplotz/Unity/HP_3/Assets/GameObjects/Environment/Study_Room/Magic_Book_Pages.fbx')
scene=bpy.context.scene
for name,names in materials.items():
    actual_materials=[m.name for m in bpy.data.objects[name].data.materials if m]
    assert sorted(actual_materials)==sorted(names),(name,names,actual_materials)
    assert polygon_materials(bpy.data.objects[name])==face_materials[name],name
max_error=0
for (clip,frame),coords in expected.items():
    for o in bpy.data.objects:
        if o.animation_data:
            o.animation_data.action=bpy.data.actions.get(f'{o.name}|{clip}')
        if o.type=='MESH' and o.data.shape_keys:
            k=o.data.shape_keys
            k.animation_data.action=bpy.data.actions.get(f'{k.name}|{clip}')
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    dg=bpy.context.evaluated_depsgraph_get()
    for name,positions in coords.items():
        ev=bpy.data.objects[name].evaluated_get(dg)
        actual=[ev.matrix_world@v.co for v in ev.data.vertices]
        assert len(actual)==len(positions)
        error=max((a-Vector(b)).length for a,b in zip(actual,positions))
        max_error=max(max_error,error)
        assert error<0.0005,(clip,frame,name,error)
print('ROUNDTRIP_VERIFIED',len(expected),'poses; maximum vertex error',max_error)
print('MATERIAL_ASSIGNMENTS_PRESERVED')
