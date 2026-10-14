import bpy,bmesh
from mathutils import Vector
root=r'D:\GameDesign\Hotzenplotz\Unity\HP_3';path=root+r'\BlenderSource\Environment\Castle\Study_Room\Brick_Stove_Fugen.blend'
bpy.ops.wm.open_mainfile(filepath=path);o=bpy.data.objects['Cube']
bm=bmesh.new();bm.from_mesh(o.data);bm.normal_update()
idx=next(i for i,m in enumerate(o.data.materials) if m.name=='Material')
def inside(f):
 c=f.calc_center_median();n=f.normal
 if f.material_index==idx:return False
 floor=abs(c.z-.03968976)<.0001 and n.z>.9
 arch=abs(c.x)<1.1 and .04<c.z<.78 and abs(n.y)<.5 and (n.z<-.1 or c.x*n.x<-.1)
 wall=abs(c.x)<1.12 and .04<c.z<.78 and .6<abs(c.y)<.735 and c.y*n.y<-.5
 return floor or arch or wall
for axis,centers in [(0,[-1.3136,-.9501,-.5503,-.1506,.2492,.6478,1.0439]),(1,[-.364,.0023,.3448]),(2,[.16,.282,.403,.525,.653])]:
 for center in centers:
  for value in [center-.0058475,center+.0058475]:
   fs=[f for f in bm.faces if inside(f)]
   geom=set(fs)
   for f in fs:geom.update(f.edges);geom.update(f.verts)
   point=Vector();point[axis]=value;normal=Vector();normal[axis]=1
   bmesh.ops.bisect_plane(bm,geom=list(geom),dist=.000001,plane_co=point,plane_no=normal)
   bm.normal_update()
 bm.normal_update()
chosen=[]
for f in bm.faces:
 if not inside(f):continue
 spans=[max(v.co[k] for v in f.verts)-min(v.co[k] for v in f.verts) for k in range(3)]
 c=f.calc_center_median()
 bands=[[-1.3136,-.9501,-.5503,-.1506,.2492,.6478,1.0439],[-.364,.0023,.3448],[.16,.282,.403,.525,.653]]
 if any(.00001<spans[k]<.012 and any(abs(c[k]-b)<.006 for b in bands[k]) for k in range(3)) and f.calc_area()>.00003:chosen.append(f)
normals={}
for f in chosen:
 f.material_index=idx
 for v in f.verts:
  key=tuple(round(x,7) for x in v.co);normals.setdefault(key,Vector()).__iadd__(f.normal)
for n in normals.values():n.normalize()
res=bmesh.ops.extrude_face_region(bm,geom=chosen)
for v in res['geom']:
 if isinstance(v,bmesh.types.BMVert):v.co-=normals[tuple(round(x,7) for x in v.co)]*.003913
bmesh.ops.delete(bm,geom=chosen,context='FACES_ONLY');bm.normal_update();bm.to_mesh(o.data);bm.free();o.data.update()
bpy.ops.wm.save_as_mainfile(filepath=path);print('Interior recessed faces',len(chosen))
scene=bpy.context.scene;scene.render.engine='BLENDER_EEVEE_NEXT';scene.render.resolution_x=1200;scene.render.resolution_y=900;scene.render.resolution_percentage=100;scene.world.color=(.3,.3,.3)
target=Vector((0,0,.65));bpy.ops.object.camera_add(location=(4,-6,3.5));cam=bpy.context.object;cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=4.2;scene.camera=cam
for pos in [(-3,-4,5),(4,2,5)]:
 bpy.ops.object.light_add(type='AREA',location=pos);l=bpy.context.object;l.data.energy=650;l.data.size=4;l.rotation_euler=(target-l.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=root+r'\Backups\StoveJoints_20261013\inside_preview.png';bpy.ops.render.render(write_still=True)
