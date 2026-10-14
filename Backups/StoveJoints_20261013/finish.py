import bpy, json, math
from mathutils import Vector
root=r'D:\GameDesign\Hotzenplotz\Unity\HP_3'
bpy.ops.wm.open_mainfile(filepath=root+r'\Backups\StoveJoints_20261013\Brick_Stove.before.blend')
if bpy.context.object and bpy.context.object.mode!='OBJECT': bpy.ops.object.mode_set(mode='OBJECT')
o=bpy.data.objects['Cube']
bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
front=min(v.co.y for v in o.data.vertices)
# Match the depth already used on the first tall joint, rather than guessing a UI arrow increment.
reference=bpy.data.objects['Cube.003']
depth=sum(v.co.y for v in reference.data.vertices if v.co.y<-.735)/len([v for v in reference.data.vertices if v.co.y<-.735])-front
assert 0.001<depth<0.02, depth
for v in o.data.vertices:v.select=False
for e in o.data.edges:e.select=False
selected=[]
for p in o.data.polygons:
 vs=[o.data.vertices[i].co for i in p.vertices]
 spans=[max(v[k] for v in vs)-min(v[k] for v in vs) for k in range(3)]
 p.select=(p.normal.y<-.99 and all(abs(v.y-front)<.00005 for v in vs)
           and (0.0001<spans[0]<.016 or 0.0001<spans[2]<.016))
 if p.select:selected.append(p.index)
assert len(selected)>30,len(selected)
mat=bpy.data.materials['Material']
o.data.materials.append(mat); idx=len(o.data.materials)-1
for i in selected:o.data.polygons[i].material_index=idx
bpy.context.tool_settings.mesh_select_mode=(False,False,True)
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.extrude_region_move(TRANSFORM_OT_translate={'value':(0,depth,0)})
bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False)
bpy.ops.object.mode_set(mode='OBJECT')
out=root+r'\BlenderSource\Environment\Castle\Study_Room\Brick_Stove_Fugen.blend'
bpy.ops.wm.save_as_mainfile(filepath=out)
print('Completed',len(selected),'front joint faces; depth',depth,'saved',out)
# Render the review image without persisting preview camera/lights in the asset.
scene=bpy.context.scene
scene.render.engine='BLENDER_EEVEE_NEXT';scene.render.resolution_x=1400;scene.render.resolution_y=700;scene.render.resolution_percentage=100
scene.world.color=(.35,.35,.35)
for image in bpy.data.images:
 if image.source=='FILE':
  name=bpy.path.basename(image.filepath)
  candidate=root+'\\Assets\\Art\\Textures\\'+name
  import os
  if os.path.isfile(candidate):image.filepath=candidate;image.reload()
bpy.ops.object.camera_add(location=(-.045,-6,2.5));cam=bpy.context.object
target=Vector((-.045,0,.65));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=3.8;scene.camera=cam
for pos,energy,size in [((-3,-4,5),600,4),((3,-2,3),350,3)]:
 bpy.ops.object.light_add(type='AREA',location=pos);light=bpy.context.object;light.data.energy=energy;light.data.shape='DISK';light.data.size=size;light.rotation_euler=(target-light.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=root+r'\Backups\StoveJoints_20261013\preview.png';bpy.ops.render.render(write_still=True)
