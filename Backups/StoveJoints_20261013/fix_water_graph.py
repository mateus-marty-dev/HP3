import json,uuid,copy,shutil
from pathlib import Path
root=Path(r'D:\GameDesign\Hotzenplotz\Unity\HP_3')
def read(path):
 s=path.read_text(encoding='utf-8-sig');d=json.JSONDecoder();out=[];pos=0
 while pos<len(s):
  while pos<len(s) and s[pos].isspace():pos+=1
  if pos==len(s):break
  obj,end=d.raw_decode(s,pos);out.append(obj);pos=end
 return out
p=root/'Assets/Art/Material/Shader Graph/Water_Boiling.shadergraph'
shutil.copy2(p,root/'Backups/StoveJoints_20261013/Water_Boiling.before.shadergraph')
objs=read(p);graph=objs[0];byid={o['m_ObjectId']:o for o in objs}
templates=read(root/'Assets/TerrainSampleAssets/ShaderGraphs/TerrainGrass.shadergraph')
propbase=next(o for o in templates if o['m_Type'].endswith('.Vector1ShaderProperty'))
nodebase=next(o for o in templates if o['m_Type'].endswith('.PropertyNode'))
slotbase=next(o for o in templates if o['m_Type'].endswith('.Vector1MaterialSlot') and o['m_SlotType']==1)
uid=lambda:uuid.uuid4().hex
def property_node(name,ref,x,y):
 prop=copy.deepcopy(propbase);node=copy.deepcopy(nodebase);slot=copy.deepcopy(slotbase)
 for o in [prop,node,slot]:o['m_ObjectId']=uid()
 prop.update(m_Name=name,m_DefaultReferenceName=ref,m_OverrideReferenceName=ref,m_RefNameGeneratedByDisplayName=name,m_Value=0.0)
 prop['m_Guid']={'m_GuidSerialized':str(uuid.uuid4())}
 node['m_Group']={'m_Id':''};node['m_Property']={'m_Id':prop['m_ObjectId']};node['m_Slots']=[{'m_Id':slot['m_ObjectId']}]
 node['m_DrawState']['m_Position'].update(x=x,y=y)
 slot.update(m_Id=0,m_DisplayName=name,m_ShaderOutputName='Out',m_Value=0.0,m_DefaultValue=0.0)
 graph['m_Properties'].append({'m_Id':prop['m_ObjectId']});graph['m_Nodes'].append({'m_Id':node['m_ObjectId']})
 category=byid[graph['m_CategoryData'][0]['m_Id']];category['m_ChildObjectList'].append({'m_Id':prop['m_ObjectId']})
 objs.extend([prop,node,slot]);return node['m_ObjectId']
normal=next(o for o in objs if o['m_Type'].endswith('.NormalFromHeightNode'))
multiply=next(o for o in objs if o['m_Type'].endswith('.MultiplyNode'))
noise=next(o for o in objs if o['m_Type'].endswith('.NoiseNode'))
for s in noise['m_Slots']:
 slot=byid[s['m_Id']]
 if slot['m_Id']==1:slot['m_Value']=8.0
strength=property_node('Wave Strength','_WaveStrength',-350,350)
time=property_node('Water Time','_WaterTime',-1200,-200)
graph['m_Edges']=[e for e in graph['m_Edges'] if not(e['m_InputSlot']['m_Node']['m_Id']==multiply['m_ObjectId'] and e['m_InputSlot']['m_SlotId']==0)]
def edge(src,dst,slot):
 graph['m_Edges'].append({'m_OutputSlot':{'m_Node':{'m_Id':src},'m_SlotId':0},'m_InputSlot':{'m_Node':{'m_Id':dst},'m_SlotId':slot}})
edge(time,multiply['m_ObjectId'],0);edge(strength,normal['m_ObjectId'],2)
p.write_text('\n\n'.join(json.dumps(o,indent=4) for o in objs)+'\n',encoding='utf-8')
check=read(p);ids={o['m_ObjectId'] for o in check}
assert len(ids)==len(check)
for e in check[0]['m_Edges']:
 for side in ['m_InputSlot','m_OutputSlot']:assert e[side]['m_Node']['m_Id'] in ids
print('Graph updated; references verified. Cold defaults: time 0, strength 0. Noise scale 8.')
