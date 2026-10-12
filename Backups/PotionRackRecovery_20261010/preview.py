import bpy, os
from mathutils import Vector
folder=r'D:\GameDesign\Hotzenplotz\Unity\HP_3\Backups\PotionRackRecovery_20261010'
bpy.ops.wm.open_mainfile(filepath=os.path.join(folder,'33652_autosave.blend1'),load_ui=False)
points=[o.matrix_world @ Vector(c) for o in bpy.context.scene.objects if o.type=='MESH' for c in o.bound_box]
lo=Vector(tuple(min(p[i] for p in points) for i in range(3))); hi=Vector(tuple(max(p[i] for p in points) for i in range(3))); center=(lo+hi)/2; extent=max(hi-lo)
bpy.ops.object.camera_add(location=center+Vector((1,-2,1))*extent)
cam=bpy.context.object; cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler(); cam.data.type='ORTHO'; cam.data.ortho_scale=extent*1.5
scene=bpy.context.scene; scene.camera=cam; scene.render.engine='BLENDER_WORKBENCH'; scene.render.resolution_x=800; scene.render.resolution_y=800; scene.render.resolution_percentage=100
scene.display.shading.light='STUDIO'; scene.display.shading.color_type='MATERIAL'; scene.display.shading.show_shadows=True; scene.display.shading.show_cavity=True
scene.render.filepath=os.path.join(folder,'recovered_preview.png'); bpy.ops.render.render(write_still=True)
