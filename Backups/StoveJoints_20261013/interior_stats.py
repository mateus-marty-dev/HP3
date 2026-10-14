import bpy,json
bpy.ops.wm.open_mainfile(filepath=r'D:\GameDesign\Hotzenplotz\Unity\HP_3\BlenderSource\Environment\Castle\Study_Room\Brick_Stove_Fugen.blend')
o=bpy.data.objects['Cube']
print(json.dumps([dict(i=p.index,c=list(p.center),n=list(p.normal),a=p.area,s=[max(o.data.vertices[i].co[k] for i in p.vertices)-min(o.data.vertices[i].co[k] for i in p.vertices) for k in range(3)]) for p in o.data.polygons if p.area>.03 and abs(p.center.x)<1.1 and p.center.z<.75]))
