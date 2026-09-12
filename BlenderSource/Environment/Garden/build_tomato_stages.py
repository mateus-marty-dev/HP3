import bpy
import math
from mathutils import Vector
from pathlib import Path

BASE = Path(__file__).resolve().parent
PROJECT = BASE.parents[2]
OUT = PROJECT / 'Assets/GameObjects/Environment/Garden/HP3_Tomato_5Stages.fbx'
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

def material(name, color):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*color, 1)
    bsdf.inputs['Roughness'].default_value = .65
    return m

green = material('Tomato_Stem', (.12, .28, .035))
leafmat = material('Tomato_Leaf', (.19, .40, .055))
yellow = material('Tomato_Petal', (1, .67, .025))
gold = material('Tomato_Pollen', (.83, .29, .008))
unripe = material('Tomato_Unripe', (.34, .56, .045))
red = material('Tomato_Ripe', (.75, .035, .018))

root = None
def attach(obj, name, mat):
    obj.name = name
    obj.data.materials.append(mat)
    obj.parent = root
    return obj

def stem(a, b, radius=.012):
    a, b = Vector(a), Vector(b)
    bpy.ops.mesh.primitive_cone_add(vertices=8, radius1=radius, radius2=radius*.7,
        depth=(b-a).length, location=(a+b)/2)
    obj = attach(bpy.context.object, 'Stem', green)
    obj.rotation_euler = (b-a).to_track_quat('Z', 'Y').to_euler()

def leaf(a, direction, length, width, mat=leafmat):
    a = Vector(a)
    d = Vector(direction).normalized()
    side = d.cross(Vector((0, 0, 1)))
    if side.length < .01:
        side = Vector((1, 0, 0))
    side.normalize()
    mid = a + d*length*.48
    verts = [a, mid+side*width*.5, a+d*length, mid-side*width*.5,
             mid+Vector((0, 0, width*.18))]
    mesh = bpy.data.meshes.new('FoldedLeaf')
    mesh.from_pydata(verts, [], [(0, 1, 4), (1, 2, 4), (2, 3, 4), (3, 0, 4)])
    mesh.update()
    obj = bpy.data.objects.new('Leaf', mesh)
    bpy.context.collection.objects.link(obj)
    attach(obj, 'Petal' if mat == yellow else 'Leaf', mat)
    mod = obj.modifiers.new('Thin leaf', 'SOLIDIFY')
    mod.thickness = .0015

def ball(pos, scale, mat, name):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=8, location=pos)
    obj = attach(bpy.context.object, name, mat)
    obj.scale = scale
    return obj

def flower(p, size=.07):
    p = Vector(p)
    for i in range(5):
        angle = i*math.tau/5
        leaf(p, (math.cos(angle), math.sin(angle), .15), size, size*.52, yellow)
    ball(p+Vector((0, 0, .006)), (size*.22, size*.22, size*.17), gold, 'Flower_Center')

def fruit(p, radius, ripe):
    p = Vector(p)
    ball(p, (radius, radius, radius*.88), red if ripe else unripe, 'Ripe_Tomato' if ripe else 'Green_Tomato')
    top = p+Vector((0, 0, radius*.84))
    for i in range(5):
        angle = i*math.tau/5
        leaf(top, (math.cos(angle), math.sin(angle), -.25), radius*.75, radius*.24, green)
    stem(top, top+Vector((0, 0, .04)), .006)

roots = []
for stage in range(5):
    root = bpy.data.objects.new(f'Tomato_Stage_{stage}', None)
    bpy.context.collection.objects.link(root)
    roots.append(root)
    if stage == 0:
        # Only a single flower on a short stalk; no foliage or fruit.
        stem((0, 0, 0), (0, 0, .10), .005)
        flower((0, 0, .10), .065)
        continue
    size_stage = min(stage, 2) if stage < 4 else 3
    height = [.1, .42, .70, .88][size_stage]
    stem((0, 0, 0), (.025, 0, height), .009+size_stage*.004)
    for i in range(3+size_stage):
        z = height*(.23+i*.12)
        angle = i*2.4+.5
        length = .13+size_stage*.035
        a = Vector((.025*z/height, 0, z))
        direction = Vector((math.cos(angle), math.sin(angle), .3))
        b = a+direction*length
        stem(a, b, .005+size_stage*.001)
        leaf(b, direction, length*.8, length*.47)
        for sign in [-1, 1]:
            leaf(a+direction*length*.58,
                 (math.cos(angle+sign*.85), math.sin(angle+sign*.85), .15),
                 length*.65, length*.33)
    if stage >= 2:
        for i in range(3 if stage < 4 else 5):
            angle = i*2.4
            z = height*(.43+(i%3)*.16)
            radius = .06 if stage < 4 else .088
            p = Vector((math.cos(angle)*.14, math.sin(angle)*.14, z))
            attachment = p+Vector((0, 0, radius+.03))
            stem((.02, 0, z+.12), attachment, .006)
            if stage == 2:
                flower(attachment, .055)
            else:
                fruit(p, radius, stage == 4)

# Export only the plant roots and meshes, all at the same soil origin.
bpy.ops.object.select_all(action='DESELECT')
for obj in bpy.context.scene.objects:
    obj.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(OUT), use_selection=True, object_types={'EMPTY','MESH'},
    axis_forward='-Z', axis_up='Y', apply_unit_scale=True, bake_anim=False,
    add_leaf_bones=False, use_mesh_modifiers=True)

# Side-by-side Blender presentation, with a separate collection for preview objects.
for i, r in enumerate(roots):
    r.location.x = (i-2)*.90
preview = bpy.data.collections.new('Preview_Only')
bpy.context.scene.collection.children.link(preview)
def preview_object(obj):
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    preview.objects.link(obj)

soil = material('Preview_Soil', (.15, .105, .065))
for r in roots:
    bpy.ops.mesh.primitive_cylinder_add(vertices=48, radius=.37, depth=.035, location=(r.location.x, 0, -.025))
    obj = bpy.context.object
    obj.name = 'Preview_Soil'
    obj.data.materials.append(soil)
    preview_object(obj)
    bpy.ops.object.text_add(location=(r.location.x-.23, -.43, -.04))
    obj = bpy.context.object
    obj.data.body = r.name.replace('Tomato_', '').replace('_', ' ')
    obj.data.size = .095
    obj.data.extrude = .0005
    preview_object(obj)

bpy.ops.object.camera_add(location=(1.5, -4.8, 3.0))
camera = bpy.context.object
camera.rotation_euler = (Vector((0, 0, .30))-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.type = 'ORTHO'
camera.data.ortho_scale = 5.3
bpy.context.scene.camera = camera
preview_object(camera)
for pos, energy, size in [((0,-3,5), 500, 5), ((-3,1,3), 350, 3)]:
    bpy.ops.object.light_add(type='AREA', location=pos)
    obj = bpy.context.object
    obj.data.energy = energy
    obj.data.shape = 'DISK'
    obj.data.size = size
    obj.rotation_euler = (Vector((0,0,.3))-obj.location).to_track_quat('-Z','Y').to_euler()
    preview_object(obj)
scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.samples = 32
scene.world.color = (.22,.22,.22)
scene.render.resolution_x = 1600
scene.render.resolution_y = 900
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.filepath = str(BASE/'HP3_Tomato_5Stages_Preview.png')
bpy.ops.object.select_all(action='DESELECT')
roots[0].select_set(True)
bpy.context.view_layer.objects.active = roots[0]
for area in bpy.context.screen.areas:
    if area.type == 'VIEW_3D':
        area.spaces.active.region_3d.view_perspective = 'CAMERA'
bpy.ops.wm.save_as_mainfile(filepath=str(BASE/'HP3_Tomato_5Stages_Blender33.blend'))
bpy.ops.render.render(write_still=True)
print('Created five tomato stages, FBX and preview:', OUT)
