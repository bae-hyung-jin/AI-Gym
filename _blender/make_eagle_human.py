"""Turn the Mixamo Ch38 character into a Hanwha Eagles baseball player.
Run: blender -b --python make_eagle_human.py   (AVATAR_PREVIEW=<png> renders previews)
Keeps Mixamo rig/skin; shirt -> short-sleeve black jersey with print, shorts -> baseball pants,
socks navy, black cap. Output: Assets/Characters/EagleHuman.fbx (original FBX untouched)."""
import bpy, bmesh, math, os
from mathutils import Vector, Matrix, kdtree
from mathutils.bvhtree import BVHTree

IN_FBX = r"G:\Unity\AI GYM\Assets\Characters\Ch38_nonPBR.fbx"
OUT_FBX = r"G:\Unity\AI GYM\Assets\Characters\EagleHuman.fbx"
OUT_BLEND = r"G:\Unity\AI GYM\_blender\EagleHuman.blend"
PREVIEW = os.environ.get("AVATAR_PREVIEW", "")

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=IN_FBX)
scene = bpy.context.scene
col = scene.collection
bpy.context.view_layer.update()

arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
M = {o.name.split('_', 1)[1]: o for o in bpy.data.objects if o.type == 'MESH'}
body, shirt, hair = M["Body"], M["Shirt"], M["Hair"]

def bone_pos(suffix, tail=False):
    b = next(b for b in arm.data.bones if b.name.endswith(suffix))
    return arm.matrix_world @ (b.tail_local if tail else b.head_local)

# ---------- materials ----------
def make_mat(name, rgb, rough=.6):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*rgb, 1)
    m.use_nodes = True
    p = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    p.inputs["Base Color"].default_value = (*rgb, 1)
    p.inputs["Roughness"].default_value = rough
    return m
MAT = {
    "Jersey": make_mat("Eagle_Jersey", (0.012, 0.012, 0.015), .55),
    "White":  make_mat("Eagle_White", (0.93, 0.93, 0.93), .5),
    "Pants":  make_mat("Eagle_Pants", (0.86, 0.86, 0.88), .7),
    "Socks":  make_mat("Eagle_Socks", (0.015, 0.04, 0.14), .8),
    "Cap":    make_mat("Eagle_Cap", (0.008, 0.008, 0.012), .7),
}

# ---------- helpers ----------
def world_bm(ob):
    bm = bmesh.new(); bm.from_mesh(ob.data)
    bmesh.ops.transform(bm, matrix=ob.matrix_world, verts=bm.verts)
    bm.normal_update(); bm.verts.index_update()
    return bm

def kd_of(ob):
    kd = kdtree.KDTree(len(ob.data.vertices))
    for v in ob.data.vertices:
        kd.insert(ob.matrix_world @ v.co, v.index)
    kd.balance()
    return kd

KD = {}
def add_child(name, bm, mat, src, smooth=True):
    """bm in world space -> new skinned object cloned from `src` (same parent/modifiers), weights copied from nearest src vertex."""
    bmesh.ops.transform(bm, matrix=src.matrix_world.inverted(), verts=bm.verts)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for p in me.polygons:
        p.use_smooth = smooth
    me.materials.append(mat)
    ob = src.copy(); ob.name = name; ob.data = me
    col.objects.link(ob)
    for vg in src.vertex_groups:
        ob.vertex_groups.new(name=vg.name)
    kd = KD.setdefault(src.name, kd_of(src))
    for v in me.vertices:
        _co, idx, _d = kd.find(ob.matrix_world @ v.co)
        for g in src.data.vertices[idx].groups:
            ob.vertex_groups[src.vertex_groups[g.group].name].add([v.index], g.weight, 'REPLACE')
    return ob

def extract(src_bm, pred, offset):
    new = bmesh.new(); vm = {}
    for f in src_bm.faces:
        if not pred(f.calc_center_median()):
            continue
        vs = []
        for v in f.verts:
            if v.index not in vm:
                vm[v.index] = new.verts.new(v.co + v.normal * offset)
            vs.append(vm[v.index])
        new.faces.new(vs)
    new.normal_update()
    return new

def ellipsoid(center, scale, rot=None):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=28, v_segments=18, radius=1.0)
    Mx = Matrix.Translation(center) @ (rot or Matrix.Identity(4)) @ Matrix.Diagonal((*scale, 1))
    bmesh.ops.transform(bm, matrix=Mx, verts=bm.verts)
    return bm

def front_ray(bvh, x, z):
    hit = bvh.ray_cast(Vector((x, -2.0, z)), Vector((0, 1, 0)))
    return None if hit[0] is None else hit[0].y

# ---------- 1. short sleeves ----------
sleeve_cut = {}
for side, up, fore in (("Left", "LeftArm", "LeftForeArm"), ("Right", "RightArm", "RightForeArm")):
    a, f = bone_pos(up), bone_pos(fore)
    sleeve_cut[side] = a.x + (f.x - a.x) * .55
sh_all = world_bm(shirt)
shirt.data.materials.clear(); shirt.data.materials.append(MAT["Socks"])  # navy undershirt
for p_ in shirt.data.polygons:
    p_.use_smooth = True
cx = min(abs(x) for x in sleeve_cut.values())
jersey_raw = extract(sh_all, lambda c: abs(c.x) <= cx, .013)
sh_bvh = BVHTree.FromBMesh(jersey_raw)
hem_z = min(v.co.z for v in jersey_raw.verts)
add_child("Jersey", jersey_raw, MAT["Jersey"], shirt)

# ---------- 2. shorts -> baseball pants, socks navy ----------
shorts = M["Shorts"]
shorts.data.materials.clear(); shorts.data.materials.append(MAT["Pants"])
body_bm = world_bm(body)
knee_z = bone_pos("LeftLeg").z
short_min = min((shorts.matrix_world @ v.co).z for v in shorts.data.vertices)
add_child("PantsLower", extract(body_bm, lambda c: knee_z - .16 <= c.z <= short_min + .05 and abs(c.x) < .30, .018), MAT["Pants"], body)
M["Socks"].data.materials.clear(); M["Socks"].data.materials.append(MAT["Socks"])

# ---------- 3. jersey details ----------
neck = bone_pos("Neck"); s2 = bone_pos("Spine2")

def strip(x, z0, z1, w=.008, off=.0025, n=40):
    b = bmesh.new(); rows = []
    for i in range(n + 1):
        z = z0 + (z1 - z0) * i / n
        row = []
        for dx in (-w / 2, w / 2):
            y = front_ray(sh_bvh, x + dx, z)
            if y is None:
                break
            row.append(b.verts.new((x + dx, y - off, z)))
        if len(row) == 2:
            rows.append(row)
    for p, q in zip(rows, rows[1:]):
        b.faces.new((p[0], p[1], q[1], q[0]))
    return b
for px in (.04, -.04):
    add_child(f"Piping{px}", strip(px, hem_z + .02, neck.z - .085), MAT["White"], shirt, smooth=False)
for i in range(7):
    z = neck.z - .11 - i * .075
    y = front_ray(sh_bvh, 0, z)
    if y is not None and z > hem_z + .03:
        add_child(f"Button{i}", ellipsoid((0, y - .001, z), (.009, .004, .009)), MAT["Jersey"], shirt)

def text_part(name, body_txt, x, z, size, ext=.0035):
    cu = bpy.data.curves.new(name, 'FONT')
    cu.body = body_txt; cu.size = size; cu.extrude = ext
    cu.align_x = 'CENTER'; cu.align_y = 'CENTER'
    tmp = bpy.data.objects.new(name + "_tmp", cu); col.objects.link(tmp)
    tmp.rotation_euler = (math.pi / 2, 0, 0); tmp.location = (x, -.5, z)
    bpy.context.view_layer.update()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    b = bmesh.new(); b.from_mesh(me)
    bmesh.ops.transform(b, matrix=tmp.matrix_world, verts=b.verts)
    bmesh.ops.triangulate(b, faces=b.faces)
    bmesh.ops.subdivide_edges(b, edges=b.edges[:], cuts=3, use_grid_fill=True)
    for v in b.verts:
        y = front_ray(sh_bvh, v.co.x, v.co.z)
        if y is None:  # off the torso edge: snap to nearest cloth point
            hit = sh_bvh.find_nearest(Vector((v.co.x, -.3, v.co.z)))
            y = hit[0].y if hit[0] is not None else -.3
        v.co.y = y + (v.co.y + .5) - ext - .001
    bpy.data.objects.remove(tmp); bpy.data.curves.remove(cu); bpy.data.meshes.remove(me)
    return add_child(name, b, MAT["White"], shirt, smooth=False)

cz = s2.z + .02
text_part("Num99", "99", .125, cz, .13)
text_part("Eagles", "Eagles", .115, cz - .115, .042)
text_part("RYU", "RYU", -.125, cz - .01, .065)

# ---------- 4. cap ----------
hb = [hair.matrix_world @ Vector(c) for c in hair.bound_box]
lo = Vector((min(v.x for v in hb), min(v.y for v in hb), min(v.z for v in hb)))
hi = Vector((max(v.x for v in hb), max(v.y for v in hb), max(v.z for v in hb)))
ctr, half = (lo + hi) / 2, (hi - lo) / 2
rim_z = ctr.z - half.z * .12
fy = front_ray(BVHTree.FromBMesh(body_bm), 0, rim_z + .03)
dome_ctr = Vector((ctr.x, (fy - .02 + half.y * 1.06) if fy is not None else ctr.y, ctr.z))
dome = ellipsoid(dome_ctr, (half.x * 1.08, half.y * 1.06, half.z * 1.04))
bmesh.ops.bisect_plane(dome, geom=dome.verts[:] + dome.edges[:] + dome.faces[:],
                       plane_co=(0, 0, rim_z), plane_no=(0, 0, 1), clear_inner=True)
hb2 = world_bm(hair)
bmesh.ops.delete(hb2, geom=[f for f in hb2.faces if f.calc_center_median().z > rim_z - .01], context='FACES')
bmesh.ops.transform(hb2, matrix=hair.matrix_world.inverted(), verts=hb2.verts)
hb2.to_mesh(hair.data); hb2.free(); hair.data.update()
add_child("CapDome", dome, MAT["Cap"], body)
brim = ellipsoid((ctr.x, (fy if fy is not None else lo.y) - .05, rim_z + .012), (half.x * .86, .095, .007), Matrix.Rotation(math.radians(-10), 4, 'X'))
add_child("CapBrim", brim, MAT["Cap"], body)

# ---------- preview ----------
def render(path, loc, rot, ortho=None):
    sc = scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.color_type = 'TEXTURE'
    sc.display.shading.light = 'FLAT'
    sc.render.resolution_x, sc.render.resolution_y = 700, 900
    cam = bpy.data.objects.new("c", bpy.data.cameras.new("c")); col.objects.link(cam)
    sc.camera = cam
    cam.location, cam.rotation_euler = loc, rot
    cam.data.type = 'ORTHO' if ortho else 'PERSP'
    if ortho:
        cam.data.ortho_scale = ortho
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam)

if PREVIEW:
    render(PREVIEW, (0, -6, .9), (math.pi / 2, 0, 0), 2.1)
    render(PREVIEW.replace(".png", "_close.png"), (0, -6, 1.35), (math.pi / 2, 0, 0), .8)
    render(PREVIEW.replace(".png", "_side.png"), (3.2, -3.2, 1.3), (math.radians(82), 0, math.radians(45)))

# ---------- export ----------
bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
for ob in bpy.context.view_layer.objects:
    ob.select_set(False)
for ob in bpy.data.objects:
    if ob.type in {'ARMATURE', 'MESH'}:
        ob.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(
    filepath=OUT_FBX, use_selection=True, object_types={'ARMATURE', 'MESH'},
    add_leaf_bones=False, bake_anim=False, apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z', axis_up='Y', path_mode='COPY', embed_textures=True)
print("EXPORTED", OUT_FBX, os.path.getsize(OUT_FBX))
