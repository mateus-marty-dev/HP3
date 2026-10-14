import bpy,collections,json
bpy.ops.wm.open_mainfile(filepath=r'D:\GameDesign\Hotzenplotz\Unity\HP_3\Backups\StoveJoints_20261013\Brick_Stove.before.blend')
o=bpy.data.objects['Cube']; groups=collections.defaultdict(list)
for p in o.data.polygons:
 vs=[o.data.vertices[i].co for i in p.vertices]
 spans=[max(v[k] for v in vs)-min(v[k] for v in vs) for k in range(3)]
 axis=max(range(3),key=lambda k:abs(p.normal[k]));tangents=[k for k in range(3) if k!=axis]
 skinny=any(.0001<spans[k]<.016 for k in tangents)
 key=str(tuple(round(n,2) for n in p.normal))
 if skinny:groups[key].append(dict(i=p.index,c=list(p.center),s=spans))
print(json.dumps({k:dict(count=len(v),sample=v[:2]) for k,v in groups.items() if k in ['(0.0, 0.0, 1.0)','(0.0, 0.0, -1.0)','(1.0, 0.0, 0.0)','(-1.0, 0.0, 0.0)','(0.0, 1.0, 0.0)','(0.0, -1.0, 0.0)']}))
