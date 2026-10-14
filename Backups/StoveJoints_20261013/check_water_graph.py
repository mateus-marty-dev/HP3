exec(open(r'D:\GameDesign\Hotzenplotz\Unity\HP_3\Backups\StoveJoints_20261013\fix_water_graph.py').read().split("p=root/")[0])
objects=read(root/'Assets/Art/Material/Shader Graph/Water_Boiling.shadergraph');byid={o['m_ObjectId']:o for o in objects}
for o in objects:
 if o['m_Type'].endswith(('MultiplyNode','PropertyNode','TilingAndOffsetNode','NormalFromHeightNode')):
  print(o.get('m_Name'),o.get('m_Property'),[(byid[s['m_Id']].get('m_DisplayName'),byid[s['m_Id']].get('m_Value')) for s in o['m_Slots']])
for e in objects[0]['m_Edges']:
 a=e['m_OutputSlot'];b=e['m_InputSlot'];print(byid[a['m_Node']['m_Id']].get('m_Name'),a['m_SlotId'],'->',byid[b['m_Node']['m_Id']].get('m_Name'),b['m_SlotId'])
