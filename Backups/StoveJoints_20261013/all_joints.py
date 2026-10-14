import bpy,bmesh,os
from mathutils import Vector
root=r'D:\GameDesign\Hotzenplotz\Unity\HP_3'
path=root+r'\BlenderSource\Environment\Castle\Study_Room\Brick_Stove_Fugen.blend'
bpy.ops.wm.open_mainfile(filepath=path)
o=bpy.data.objects['Cube']
if o.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
bm=bmesh.new();bm.from_mesh(o.data);bm.normal_update()
matidx=next(i for i,m in enumerate(o.data.materials) if m.name=='Material')
chosen=[]
for f in bm.faces:
 spans=[max(v.co[k] for v in f.verts)-min(v.co[k] for v in f.verts) for k in range(3)]
 axis=max(range(3),key=lambda k:abs(f.normal[k]))
 tangents=[k for k in range(3) if k!=axis]
 strip=any(.0115<spans[k]<.0119 for k in tangents)
 # The front joints were already recessed in the previous pass.
 front=f.normal.y<-.99 and f.calc_center_median().y<-.72
 if strip and not front and f.calc_area()>.00008:chosen.append(f)
assert len(chosen)>100,len(chosen)
normals={}
for f in chosen:
 f.material_index=matidx
 for v in f.verts:
  key=tuple(round(x,7) for x in v.co)
  normals.setdefault(key,Vector()).__iadd__(f.normal)
for n in normals.values():n.normalize()
result=bmesh.ops.extrude_face_region(bm,geom=chosen,use_keep_orig=False)
newverts=[v for v in result['geom'] if isinstance(v,bmesh.types.BMVert)]
for v in newverts:
 key=tuple(round(x,7) for x in v.co)
 v.co-=normals[key]*.0039130449295043945
# Remove the old surface caps, leaving the recessed faces and connecting walls.
bmesh.ops.delete(bm,geom=chosen,context='FACES_ONLY')
bm.normal_update();bm.to_mesh(o.data);bm.free();o.data.update()
for img in bpy.data.images:
 if img.source=='FILE' and not img.packed_file:
  candidate=root+'\\Assets\\Art\\Textures\\'+bpy.path.basename(img.filepath)
  if os.path.isfile(candidate):img.filepath=candidate;img.reload();img.pack()
bpy.ops.wm.save_as_mainfile(filepath=path)
print('Added all-surface joints:',len(chosen),'faces',len(newverts),'vertices')
scene=bpy.context.scene;scene.render.engine='BLENDER_EEVEE_NEXT'
scene.render.resolution_x=1200;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.world.color=(.3,.3,.3)
target=Vector((0,0,.65))
bpy.ops.object.camera_add(location=(4,-6,3.5));cam=bpy.context.object
cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=4.2;scene.camera=cam
for pos in [(-3,-4,5),(4,2,5)]:
 bpy.ops.object.light_add(type='AREA',location=pos);l=bpy.context.object;l.data.energy=650;l.data.size=4;l.rotation_euler=(target-l.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=root+r'\Backups\StoveJoints_20261013\all_preview.png';bpy.ops.render.render(write_still=True)
