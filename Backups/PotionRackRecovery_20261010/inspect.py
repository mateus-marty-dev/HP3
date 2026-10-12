import bpy, json, os
root = r'D:\GameDesign\Hotzenplotz\Unity\HP_3'
folder = os.path.join(root, 'Backups', 'PotionRackRecovery_20261010')
files = [os.path.join(folder, n) for n in os.listdir(folder) if '.blend' in n]
files += [os.path.join(root, 'BlenderSource', 'Environment', 'Castle', n) for n in ['Magic_Portion_Pan.blend', 'Magic_Portion_Pan.blend1']]
result = []
for path in files:
    try:
        bpy.ops.wm.open_mainfile(filepath=path, load_ui=False)
        result.append({'file':path, 'objects':[{'name':o.name,'type':o.type,'vertices':len(o.data.vertices) if o.type=='MESH' else 0} for o in bpy.data.objects]})
    except Exception as e:
        result.append({'file':path,'error':str(e)})
with open(os.path.join(folder,'inspection.json'), 'w') as f: json.dump(result,f,indent=2)
