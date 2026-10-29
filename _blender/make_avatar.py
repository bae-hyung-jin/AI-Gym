"""Hanwha Eagles jersey humanoid avatar (v2: skin-modifier body + tailored clothes).
Run: blender -b --python make_avatar.py   (AVATAR_PREVIEW=<png> env var renders previews)
T-pose, Unity Humanoid bone names, distance-based weights. Faces -Y in Blender (+Z in Unity)."""
import bpy, bmesh, math, os
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

OUT_FBX = r"G:\Unity\AI GYM\Assets\Characters\EagleAvatar.fbx"
OUT_BLEND = r"G:\Unity\AI GYM\_blender\EagleAvatar.blend"
PREVIEW = os.environ.get("AVATAR_PREVIEW", "")

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
col = scene.collection

# ---------- materials ----------
def make_mat(name, rgb, rough=0.75):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*rgb, 1)
    try:
        m.use_nodes = True
        p = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
        p.inputs["Base Color"].default_value = (*rgb, 1)
        p.inputs["Roughness"].default_value = rough
    except Exception:
        pass
    return m

MATS = {
    "Skin":   make_mat("Eagle_Skin", (0.78, 0.56, 0.44), .6),
    "Jersey": make_mat("Eagle_Jersey", (0.015, 0.015, 0.018)),
    "White":  make_mat("Eagle_White", (0.95, 0.95, 0.95)),
    "Pants":  make_mat("Eagle_Pants", (0.88, 0.88, 0.90)),
    "Socks":  make_mat("Eagle_Socks", (0.02, 0.05, 0.16)),
    "Hair":   make_mat("Eagle_Hair", (0.02, 0.015, 0.012)),
    "Shoes":  make_mat("Eagle_Shoes", (0.03, 0.03, 0.035), .5),
    "Cap":    make_mat("Eagle_Cap", (0.01, 0.01, 0.015)),
    "Lip":    make_mat("Eagle_Lip", (0.45, 0.2, 0.18), .5),
}
MAT_ORDER = list(MATS)

# ---------- humanoid bones (T-pose) ----------
BONES = {
    "Hips":  (None,    (0, 0, .95),  (0, 0, 1.05)),
    "Spine": ("Hips",  (0, 0, 1.05), (0, 0, 1.20)),
    "Chest": ("Spine", (0, 0, 1.20), (0, 0, 1.40)),
    "Neck":  ("Chest", (0, 0, 1.40), (0, 0, 1.52)),
    "Head":  ("Neck",  (0, 0, 1.52), (0, 0, 1.74)),
}
for side, sx in (("Left", 1), ("Right", -1)):
    BONES.update({
        f"{side}Shoulder": ("Chest",           (sx*.04, 0, 1.38), (sx*.16, 0, 1.38)),
        f"{side}UpperArm": (f"{side}Shoulder", (sx*.16, 0, 1.38), (sx*.44, 0, 1.38)),
        f"{side}LowerArm": (f"{side}UpperArm", (sx*.44, 0, 1.38), (sx*.71, 0, 1.38)),
        f"{side}Hand":     (f"{side}LowerArm", (sx*.71, 0, 1.38), (sx*.83, 0, 1.38)),
        f"{side}UpperLeg": ("Hips",            (sx*.09, 0, .95),  (sx*.09, 0, .50)),
        f"{side}LowerLeg": (f"{side}UpperLeg", (sx*.09, 0, .50),  (sx*.09, 0, .09)),
        f"{side}Foot":     (f"{side}LowerLeg", (sx*.09, 0, .09),  (sx*.09, -.12, .03)),
        f"{side}Toes":     (f"{side}Foot",     (sx*.09, -.12, .03), (sx*.09, -.20, .03)),
    })
SEGS = {k: (Vector(h), Vector(t)) for k, (_p, h, t) in BONES.items()}

def weights_for(co):
    ds = []
    for name, (a, b) in SEGS.items():
        ab = b - a
        t = max(0.0, min(1.0, (co - a).dot(ab) / ab.length_squared))
        ds.append(((co - (a + ab * t)).length, name))
    ds.sort()
    top = [(1.0 / (d + .015) ** 3, n) for d, n in ds[:3]]
    tot = sum(w for w, _ in top)
    return {n: w / tot for w, n in top}

# ---------- body: skin-modifier skeleton ----------
NODES = []  # (name, co, (rx, ry), parent)
def node(name, co, r, parent=None):
    NODES.append((name, Vector(co), r, parent))

node("hips", (0, 0, .98), (.15, .10))
node("spine", (0, 0, 1.12), (.135, .09), "hips")
node("chest", (0, 0, 1.27), (.15, .098), "spine")
node("neckbase", (0, 0, 1.41), (.07, .062), "chest")
node("neck", (0, 0, 1.50), (.046, .046), "neckbase")
node("head", (0, -.002, 1.63), (.088, .098), "neck")
node("headtop", (0, .004, 1.715), (.062, .07), "head")
for s, sx in (("L", 1), ("R", -1)):
    node("sh" + s, (sx*.14, 0, 1.395), (.058, .058), "neckbase")
    node("elbow" + s, (sx*.44, 0, 1.38), (.043, .043), "sh" + s)
    node("wrist" + s, (sx*.71, 0, 1.38), (.031, .028), "elbow" + s)
    node("hand" + s, (sx*.78, 0, 1.38), (.04, .013), "wrist" + s)
    node("fing" + s, (sx*.85, 0, 1.38), (.028, .009), "hand" + s)
    node("thumb" + s, (sx*.765, -.05, 1.372), (.011, .011), "wrist" + s)
    node("hip" + s, (sx*.085, 0, .94), (.092, .09), "hips")
    node("knee" + s, (sx*.09, 0, .50), (.057, .057), "hip" + s)
    node("ankle" + s, (sx*.09, 0, .10), (.036, .036), "knee" + s)
    node("ball" + s, (sx*.09, -.11, .045), (.041, .036), "ankle" + s)
    node("toe" + s, (sx*.09, -.185, .035), (.034, .028), "ball" + s)

sk_bm = bmesh.new()
skin_layer = sk_bm.verts.layers.skin.verify()
vmap = {}
for name, co, r, parent in NODES:
    v = sk_bm.verts.new(co)
    v[skin_layer].radius = r
    vmap[name] = v
    if parent:
        sk_bm.edges.new((vmap[parent], v))
vmap["hips"][skin_layer].use_root = True
sk_me = bpy.data.meshes.new("SkinSkeleton")
sk_bm.to_mesh(sk_me); sk_bm.free()
sk_ob = bpy.data.objects.new("SkinSkeleton", sk_me)
col.objects.link(sk_ob)
sm = sk_ob.modifiers.new("Skin", 'SKIN')
sm.use_smooth_shade = True
sm.branch_smoothing = 1.0
ss = sk_ob.modifiers.new("Sub", 'SUBSURF')
ss.levels = 2; ss.render_levels = 2
bpy.context.view_layer.update()
dg = bpy.context.evaluated_depsgraph_get()
body_me = bpy.data.meshes.new_from_object(sk_ob.evaluated_get(dg))
bpy.data.objects.remove(sk_ob)

body_bm = bmesh.new(); body_bm.from_mesh(body_me)
body_bm.normal_update()
body_bm.verts.index_update()
body_bvh = BVHTree.FromBMesh(body_bm)

def ray_y(bvh, x, z):
    hit = bvh.ray_cast(Vector((x, -1.0, z)), Vector((0, 1, 0)))
    return hit[0].y if hit[0] is not None else None

# ---------- parts ----------
parts = []  # (obj, mat_key)

def add_bm_part(name, bm, mat_key, smooth=True):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    for p in me.polygons:
        p.use_smooth = smooth
    ob = bpy.data.objects.new(name, me)
    col.objects.link(ob)
    parts.append((ob, mat_key))
    return ob

def extract(src, pred, offset):
    src.normal_update(); src.verts.index_update()
    new = bmesh.new(); vm = {}
    for f in src.faces:
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

def solid(bm, thickness):
    """Give an open shell thickness (inward) through a Solidify modifier."""
    me = bpy.data.meshes.new("tmp"); bm.to_mesh(me)
    ob = bpy.data.objects.new("tmp", me); col.objects.link(ob)
    m = ob.modifiers.new("S", 'SOLIDIFY'); m.thickness = thickness; m.offset = -1
    bpy.context.view_layer.update()
    ev = bpy.data.meshes.new_from_object(ob.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    bpy.data.objects.remove(ob); bpy.data.meshes.remove(me)
    out = bmesh.new(); out.from_mesh(ev); bpy.data.meshes.remove(ev)
    return out

def ellipsoid(center, scale, rot=None):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=14, radius=1.0)
    M = Matrix.Translation(center) @ (rot or Matrix.Identity(4)) @ Matrix.Diagonal((*scale, 1))
    bmesh.ops.transform(bm, matrix=M, verts=bm.verts)
    return bm

# body, clothes cut from the body surface
add_bm_part("Body", body_bm.copy(), "Skin")
J = lambda c: .96 <= c.z <= 1.455 and abs(c.x) <= .31 and not (c.z > 1.425 and abs(c.x) < .085)
jersey_raw = extract(body_bm, J, .011)
jersey_bvh = BVHTree.FromBMesh(jersey_raw)
add_bm_part("Jersey", solid(jersey_raw.copy(), .005), "Jersey")
collar = extract(body_bm, lambda c: 1.40 <= c.z <= 1.455 and abs(c.x) < .115 and not (c.z > 1.425 and abs(c.x) < .085), .014)
add_bm_part("Collar", solid(collar, .004), "White")
add_bm_part("Pants", solid(extract(body_bm, lambda c: .22 <= c.z <= 1.04 and abs(c.x) < .21, .007), .005), "Pants")
add_bm_part("Socks", solid(extract(body_bm, lambda c: .095 <= c.z < .22 and abs(c.x) < .21, .006), .004), "Socks")
add_bm_part("Shoes", solid(extract(body_bm, lambda c: c.z < .095 and abs(c.x) < .21, .010), .006), "Shoes")
add_bm_part("Hair", solid(extract(body_bm, lambda c: 1.60 <= c.z <= 1.70 and c.y > -.03, .008), .005), "Hair")
add_bm_part("CapDome", solid(extract(body_bm, lambda c: c.z > 1.685, .017), .006), "Cap")

# cap brim
by = ray_y(body_bvh, 0, 1.672) or -.1
brim = ellipsoid((0, by - .04, 1.676), (.072, .078, .0075), Matrix.Rotation(math.radians(-8), 4, 'X'))
add_bm_part("CapBrim", brim, "Cap")

# face
def face_y(x, z):
    return ray_y(body_bvh, x, z) or -.095
for sx in (1, -1):
    ex, ez = sx * .034, 1.652
    fy = face_y(ex, ez)
    add_bm_part(f"Sclera{sx}", ellipsoid((ex, fy + .001, ez), (.0135, .008, .0095)), "White")
    add_bm_part(f"Iris{sx}", ellipsoid((ex, fy - .0045, ez), (.0072, .005, .0072)), "Hair")
    bz = 1.678
    add_bm_part(f"Brow{sx}", ellipsoid((sx * .036, face_y(sx * .036, bz) - .001, bz),
                (.019, .005, .0035), Matrix.Rotation(math.radians(8 * sx), 4, 'Y')), "Hair")
    hit = body_bvh.ray_cast(Vector((sx * 1.0, 0, 1.64)), Vector((-sx, 0, 0)))[0]
    if hit is not None:
        add_bm_part(f"Ear{sx}", ellipsoid((hit.x + sx * .002, hit.y + .005, 1.64), (.01, .016, .027)), "Skin")
ny = face_y(0, 1.627)
add_bm_part("Nose", ellipsoid((0, ny - .012, 1.625), (.012, .02, .022)), "Skin")
my = face_y(0, 1.595)
add_bm_part("Mouth", ellipsoid((0, my - .0005, 1.595), (.024, .005, .0045)), "Lip")

# ---------- jersey details: piping, buttons, lettering ----------
def jersey_y(x, z, off=0.0):
    y = ray_y(jersey_bvh, x, z)
    return None if y is None else y - off

def strip(x, z0, z1, w=.007, off=.0025, n=30):
    bm = bmesh.new(); rows = []
    for i in range(n + 1):
        z = z0 + (z1 - z0) * i / n
        row = []
        for dx in (-w / 2, w / 2):
            y = jersey_y(x + dx, z, off)
            if y is None:
                break
            row.append(bm.verts.new((x + dx, y, z)))
        if len(row) == 2:
            rows.append(row)
    for a, b in zip(rows, rows[1:]):
        bm.faces.new((a[0], a[1], b[1], b[0]))
    return bm
for px in (.04, -.04):
    add_bm_part(f"Piping{px}", strip(px, 1.00, 1.40), "White", smooth=False)
for i in range(6):
    z = 1.36 - i * .07
    y = jersey_y(0, z, .002)
    if y is not None:
        add_bm_part(f"Button{i}", ellipsoid((0, y, z), (.0085, .004, .0085)), "Jersey")

def text_part(name, body, x, z, size, ext=.003):
    cu = bpy.data.curves.new(name, 'FONT')
    cu.body = body; cu.size = size; cu.extrude = ext
    cu.align_x = 'CENTER'; cu.align_y = 'CENTER'
    tmp = bpy.data.objects.new(name + "_tmp", cu); col.objects.link(tmp)
    tmp.rotation_euler = (math.pi / 2, 0, 0); tmp.location = (x, -.3, z)
    bpy.context.view_layer.update()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.transform(bm, matrix=tmp.matrix_world, verts=bm.verts)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=3, use_grid_fill=True)
    for v in bm.verts:
        y = jersey_y(v.co.x, v.co.z)
        if y is not None:
            v.co.y = y + (v.co.y + .3) - ext - .0005
    bpy.data.objects.remove(tmp); bpy.data.curves.remove(cu); bpy.data.meshes.remove(me)
    return add_bm_part(name, bm, "White")

text_part("Num99", "99", .09, 1.27, .125)
text_part("Eagles", "Eagles", .09, 1.165, .05)
text_part("RYU", "RYU", -.09, 1.255, .08)

# ---------- armature ----------
arm_data = bpy.data.armatures.new("EagleArmature")
arm = bpy.data.objects.new("EagleArmature", arm_data)
col.objects.link(arm)
bpy.context.view_layer.objects.active = arm
arm.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
eb = {}
for name, (parent, head, tail) in BONES.items():
    b = arm_data.edit_bones.new(name)
    b.head, b.tail = Vector(head), Vector(tail)
    eb[name] = b
for name, (parent, *_r) in BONES.items():
    if parent:
        eb[name].parent = eb[parent]
bpy.ops.object.mode_set(mode='OBJECT')

# ---------- weights, materials, join ----------
for ob, mat_key in parts:
    me = ob.data
    for k in MAT_ORDER:
        me.materials.append(MATS[k])
    idx = MAT_ORDER.index(mat_key)
    for p in me.polygons:
        p.material_index = idx
    groups = {}
    for v in me.vertices:
        for g, val in weights_for(v.co).items():
            vg = groups.get(g)
            if vg is None:
                vg = groups[g] = ob.vertex_groups.new(name=g)
            vg.add([v.index], val, 'REPLACE')

for ob in bpy.context.view_layer.objects:
    ob.select_set(False)
objs = [p[0] for p in parts]
for ob in objs:
    ob.select_set(True)
bpy.context.view_layer.objects.active = objs[0]
bpy.ops.object.join()
body = bpy.context.view_layer.objects.active
body.name = "EagleBody"; body.data.name = "EagleBody"
body.parent = arm
mod = body.modifiers.new("Armature", 'ARMATURE'); mod.object = arm
print("TRIS", sum(len(p.vertices) - 2 for p in body.data.polygons), "SLOTS", len(body.data.materials))

# ---------- preview ----------
if PREVIEW:
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.color_type = 'MATERIAL'
    scene.display.shading.light = 'STUDIO'
    scene.render.resolution_x, scene.render.resolution_y = 700, 900
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    col.objects.link(cam); scene.camera = cam
    def shot(suffix, loc, rot, ortho=None):
        cam.location, cam.rotation_euler = loc, rot
        cam.data.type = 'ORTHO' if ortho else 'PERSP'
        if ortho:
            cam.data.ortho_scale = ortho
        scene.render.filepath = PREVIEW.replace(".png", suffix + ".png")
        bpy.ops.render.render(write_still=True)
    shot("", (0, -5, .9), (math.pi / 2, 0, 0), 2.1)
    shot("_close", (0, -5, 1.45), (math.pi / 2, 0, 0), .75)
    shot("_side", (3.0, -3.0, 1.2), (math.radians(82), 0, math.radians(45)))
    bpy.data.objects.remove(cam)

# ---------- export ----------
os.makedirs(os.path.dirname(OUT_FBX), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
for ob in bpy.context.view_layer.objects:
    ob.select_set(False)
arm.select_set(True); body.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(
    filepath=OUT_FBX, use_selection=True, object_types={'ARMATURE', 'MESH'},
    add_leaf_bones=False, bake_anim=False, apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z', axis_up='Y', path_mode='COPY', embed_textures=False)
print("EXPORTED", OUT_FBX)
