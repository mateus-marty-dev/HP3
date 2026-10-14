import bpy, os
root=r'D:\GameDesign\Hotzenplotz\Unity\HP_3'
path=root+r'\BlenderSource\Environment\Castle\Study_Room\Brick_Stove_Fugen.blend'
bpy.ops.wm.open_mainfile(filepath=path)
for image in bpy.data.images:
 if image.source!='FILE':continue
 candidate=os.path.join(root,'Assets','Art','Textures',bpy.path.basename(image.filepath))
 if os.path.isfile(candidate):
  image.filepath=candidate
  image.reload()
  image.pack()
  print('Packed',image.name,image.size[:])
brick=bpy.data.materials['Brick']
textures=[n.image for n in brick.node_tree.nodes if n.type=='TEX_IMAGE' and n.image]
assert textures and all(i.packed_file for i in textures),'Brick texture not packed'
bpy.ops.wm.save_as_mainfile(filepath=path)
