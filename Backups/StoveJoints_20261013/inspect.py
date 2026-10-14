import bpy, json
from mathutils import Vector
bpy.ops.wm.open_mainfile(filepath=r'D:\GameDesign\Hotzenplotz\Unity\HP_3\BlenderSource\Environment\Castle\Study_Room\Brick_Stove.blend')
data=[]
for o in bpy.data.objects:
 if o.type!='MESH': continue
 data.append(dict(name=o.name,location=list(o.location),dimensions=list(o.dimensions),vertices=len(o.data.vertices),polygons=len(o.data.polygons),materials=[m.name if m else None for m in o.data.materials],bounds=[list(o.matrix_world@Vector(c)) for c in o.bound_box],verts=[list(v.co) for v in o.data.vertices] if o.name.startswith('Cube') else []))
with open(r'D:\GameDesign\Hotzenplotz\Unity\HP_3\Backups\StoveJoints_20261013\inspect.json','w') as f: json.dump(data,f)
o=bpy.data.objects['Cube']; rows=[]
for p in o.data.polygons:
 vs=[o.data.vertices[i].co for i in p.vertices]; mn=[min(v[k] for v in vs) for k in range(3)]; mx=[max(v[k] for v in vs) for k in range(3)]
 if max(v.y for v in vs)<-0.70 and p.normal.y<-.8:
  rows.append(dict(i=p.index,c=list(p.center),s=[mx[k]-mn[k] for k in range(3)]))
with open(r'D:\GameDesign\Hotzenplotz\Unity\HP_3\Backups\StoveJoints_20261013\faces.json','w') as f:json.dump(rows,f)
