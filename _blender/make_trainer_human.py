"""Turn the Mixamo Ch38 character into a clean, athletic AI Gym Trainer (black, slate & gold).
Run: blender -b --python make_trainer_human.py   (AVATAR_PREVIEW=<png> renders previews)
Keeps Mixamo Ch38_Shirt intact as fitted long-sleeve compression base + clean bisect-cut vest over torso,
so shoulders/arms deform 100% smoothly overhead with zero jagged cutouts or sleeve spikes.
Output: Assets/Characters/TrainerHuman.fbx"""
import bpy, bmesh, math, os
from mathutils import Vector, Matrix, kdtree
from mathutils.bvhtree import BVHTree

ROOT = r"G:\Unity_Gym\AI-Gym"
IN_FBX = os.path.join(ROOT, "Assets", "Characters", "Ch38_nonPBR.fbx")
OUT_FBX = os.path.join(ROOT, "Assets", "Characters", "TrainerHuman.fbx")
OUT_BLEND = os.path.join(ROOT, "_blender", "TrainerHuman.blend")
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

def dist_pt_seg(p, a, b):
    ab = b - a
    l2 = ab.length_squared
    if l2 < 1e-10:
        return (p - a).length
    t = max(0.0, min(1.0, (p - a).dot(ab) / l2))
    return (p - (a + ab * t)).length

def dist_pt_tri(p, a, b, c):
    ab, ac, ap = b - a, c - a, p - a
    d1, d2 = ab.dot(ap), ac.dot(ap)
    if d1 <= 0.0 and d2 <= 0.0: return (p - a).length
    bp = p - b
    d3, d4 = ab.dot(bp), ac.dot(bp)
    if d3 >= 0.0 and d4 <= d3: return (p - b).length
    vc = d1 * d4 - d3 * d2
    if vc <= 0.0 and d1 >= 0.0 and d3 <= 0.0:
        return (p - (a + ab * (d1 / (d1 - d3)))).length
    cp = p - c
    d5, d6 = ab.dot(cp), ac.dot(cp)
    if d6 >= 0.0 and d5 <= d6: return (p - c).length
    vb = d5 * d2 - d1 * d6
    if vb <= 0.0 and d2 >= 0.0 and d6 <= 0.0:
        return (p - (a + ac * (d2 / (d2 - d6)))).length
    va = d3 * d6 - d5 * d4
    if va <= 0.0 and (d4 - d3) >= 0.0 and (d5 - d6) >= 0.0:
        return (p - (b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6))))).length
    denom = 1.0 / max(1e-12, va + vb + vc)
    v = vb * denom; w = vc * denom
    return (p - (a + ab * v + ac * w)).length

def reskin_hands(ob):
    """Fix Mixamo Ch38 missing finger vertex groups by cleanly capsule-skinning all 5 fingers per hand."""
    mw_arm = arm.matrix_world
    mw_ob = ob.matrix_world
    for side in ("Left", "Right"):
        hand_b = next(b for b in arm.data.bones if b.name.endswith(side + "Hand"))
        prefix = hand_b.name[:-len(side + "Hand")]
        w_pos = mw_arm @ hand_b.head_local
        fingers = ("Thumb", "Index", "Middle", "Ring", "Pinky")
        segs = []
        knuckles = {}
        for fn in fingers:
            for j in (1, 2, 3):
                bn = f"{prefix}{side}Hand{fn}{j}"
                b = arm.data.bones.get(bn)
                if b is None:
                    continue
                if bn not in ob.vertex_groups:
                    ob.vertex_groups.new(name=bn)
                h = mw_arm @ b.head_local
                t = mw_arm @ b.tail_local
                if j == 1:
                    knuckles[fn] = h
                segs.append((bn, h, t))
        if hand_b.name not in ob.vertex_groups:
            ob.vertex_groups.new(name=hand_b.name)
        k_t = w_pos.lerp(knuckles["Thumb"], 0.62)
        k_i = w_pos.lerp(knuckles["Index"], 0.93)
        k_m = w_pos.lerp(knuckles["Middle"], 0.93)
        k_r = w_pos.lerp(knuckles["Ring"], 0.93)
        k_p = w_pos.lerp(knuckles["Pinky"], 0.93)
        palm_tris = [(w_pos, k_t, k_i), (w_pos, k_i, k_m), (w_pos, k_m, k_r), (w_pos, k_r, k_p)]
        hand_vg_names = {hand_b.name} | {s[0] for s in segs} | {
            f"{prefix}{side}Hand{fn}{j}" for fn in fingers for j in (1, 2, 3, 4)
        }
        hand_vg_indices = {vg.index for vg in ob.vertex_groups if vg.name in hand_vg_names}
        vg_by_idx = {vg.index: vg for vg in ob.vertex_groups}

        sigma = 0.0045
        for v in ob.data.vertices:
            hw = sum(g.weight for g in v.groups if g.group in hand_vg_indices)
            if hw < 1e-4:
                continue
            for idx in hand_vg_indices:
                vg_by_idx[idx].remove([v.index])
            p = mw_ob @ v.co
            dists = [(hand_b.name, min(dist_pt_tri(p, a, b, c) for a, b, c in palm_tris))]
            for bn, a, b in segs:
                dists.append((bn, dist_pt_seg(p, a, b)))
            dmin = min(d for _, d in dists)
            raw = [(bn, math.exp(-((d - dmin) / sigma) ** 2)) for bn, d in dists]
            raw = sorted([(bn, w) for bn, w in raw if w > 0.02], key=lambda x: -x[1])[:3]
            tot = sum(w for _, w in raw) or 1.0
            for bn, w in raw:
                ob.vertex_groups[bn].add([v.index], hw * (w / tot), 'REPLACE')

reskin_hands(body)

# ---------- materials ----------
def make_mat(name, rgb, rough=.58, metallic=0.0):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*rgb, 1)
    m.use_nodes = True
    p = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    p.inputs["Base Color"].default_value = (*rgb, 1)
    p.inputs["Roughness"].default_value = rough
    p.inputs["Metallic"].default_value = metallic
    return m

MAT = {
    "Compression": make_mat("Trainer_Compression", (0.045, 0.052, 0.068), .60),
    "Top":         make_mat("Trainer_Top", (0.016, 0.018, 0.024), .52),
    "Pants":       make_mat("Trainer_Pants", (0.032, 0.035, 0.045), .68),
    "Socks":       make_mat("Trainer_Socks", (0.018, 0.020, 0.026), .75),
    "Gold":        make_mat("Trainer_Gold", (0.94, 0.76, 0.20), .35, .15),
    "Mint":        make_mat("Trainer_Mint", (0.18, 0.85, 0.62), .40),
    "White":       make_mat("Trainer_White", (0.94, 0.94, 0.96), .45),
    "Metal":       make_mat("Trainer_Headset", (0.08, 0.08, 0.10), .30, .70),
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

def add_rigid_to_bone(name, bm, mat, src, bone_suffix):
    bmesh.ops.transform(bm, matrix=src.matrix_world.inverted(), verts=bm.verts)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for p in me.polygons:
        p.use_smooth = True
    me.materials.append(mat)
    ob = src.copy(); ob.name = name; ob.data = me
    col.objects.link(ob)
    vg_name = next(b.name for b in arm.data.bones if b.name.endswith(bone_suffix))
    vg = ob.vertex_groups.new(name=vg_name)
    vg.add([v.index for v in me.vertices], 1.0, 'REPLACE')
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
    bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=16, radius=1.0)
    Mx = Matrix.Translation(center) @ (rot or Matrix.Identity(4)) @ Matrix.Diagonal((*scale, 1))
    bmesh.ops.transform(bm, matrix=Mx, verts=bm.verts)
    return bm

def front_ray(bvh, x, z):
    hit = bvh.ray_cast(Vector((x, -2.0, z)), Vector((0, 1, 0)))
    return None if hit[0] is None else hit[0].y

# ---------- 1. long-sleeve compression base + clean bisect-cut vest ----------
arm_l = bone_pos("LeftArm")
arm_r = bone_pos("RightArm")
cut_x = min(abs(arm_l.x), abs(arm_r.x)) - 0.015

# Keep Ch38_Shirt intact as smooth long-sleeve athletic compression layer
shirt.data.materials.clear(); shirt.data.materials.append(MAT["Compression"])
for p_ in shirt.data.polygons:
    p_.use_smooth = True

# Build fitted black vest over torso with razor-clean bisect_plane cuts at armholes
vest_bm = world_bm(shirt)
for v in vest_bm.verts:
    v.co += v.normal * 0.008
bmesh.ops.bisect_plane(
    vest_bm, geom=vest_bm.verts[:] + vest_bm.edges[:] + vest_bm.faces[:],
    plane_co=(cut_x, 0, 0), plane_no=(1, 0, 0), clear_outer=True)
bmesh.ops.bisect_plane(
    vest_bm, geom=vest_bm.verts[:] + vest_bm.edges[:] + vest_bm.faces[:],
    plane_co=(-cut_x, 0, 0), plane_no=(-1, 0, 0), clear_outer=True)
vest_bm.normal_update()
top_bvh = BVHTree.FromBMesh(vest_bm)
hem_z = min(v.co.z for v in vest_bm.verts)
add_child("TrainerVest", vest_bm, MAT["Top"], shirt)

# ---------- 2. charcoal gym joggers & dark socks ----------
shorts = M["Shorts"]
shorts.data.materials.clear(); shorts.data.materials.append(MAT["Pants"])
for p_ in shorts.data.polygons:
    p_.use_smooth = True

body_bm = world_bm(body)
body_bvh = BVHTree.FromBMesh(body_bm)
knee_z = bone_pos("LeftLeg").z
short_min = min((shorts.matrix_world @ v.co).z for v in shorts.data.vertices)
pants_lower = extract(body_bm, lambda c: knee_z - .18 <= c.z <= short_min + .05 and abs(c.x) < .30, .016)
add_child("TrainerPantsLower", pants_lower, MAT["Pants"], body)
M["Socks"].data.materials.clear(); M["Socks"].data.materials.append(MAT["Socks"])

# ---------- 3. gold & mint athletic chest details ----------
neck = bone_pos("Neck"); s2 = bone_pos("Spine2")

def strip(x, z0, z1, w=.007, off=.0025, n=36):
    b = bmesh.new(); rows = []
    for i in range(n + 1):
        z = z0 + (z1 - z0) * i / n
        row = []
        for dx in (-w / 2, w / 2):
            y = front_ray(top_bvh, x + dx, z)
            if y is None:
                break
            row.append(b.verts.new((x + dx, y - off, z)))
        if len(row) == 2:
            rows.append(row)
    for p, q in zip(rows, rows[1:]):
        b.faces.new((p[0], p[1], q[1], q[0]))
    return b

add_child("ZipLine", strip(0.0, hem_z + .04, neck.z - .08, w=.006), MAT["Mint"], shirt, smooth=False)

def text_part(name, body_txt, x, z, size, mat, ext=.0035):
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
    bmesh.ops.subdivide_edges(b, edges=b.edges[:], cuts=2, use_grid_fill=True)
    for v in b.verts:
        y = front_ray(top_bvh, v.co.x, v.co.z)
        if y is None:
            hit = top_bvh.find_nearest(Vector((v.co.x, -.3, v.co.z)))
            y = hit[0].y if hit[0] is not None else -.3
        v.co.y = y + (v.co.y + .5) - ext - .001
    bpy.data.objects.remove(tmp); bpy.data.curves.remove(cu); bpy.data.meshes.remove(me)
    return add_child(name, b, mat, shirt, smooth=False)

cz = s2.z + .01
text_part("LogoAI", "AI", -.095, cz + .015, .048, MAT["Gold"])
text_part("LogoTrainer", "TRAINER", -.095, cz - .030, .022, MAT["White"])

# ---------- 4. sleek wireless coach headset snapped flush onto right ear ----------
head_pos = bone_pos("Head")
ear_probe = Vector((-0.14, head_pos.y + 0.01, head_pos.z + 0.075))
ear_hit = body_bvh.find_nearest(ear_probe)
if ear_hit[0] is not None:
    ep = ear_hit[0]
    add_rigid_to_bone("HeadsetPod", ellipsoid((ep.x - 0.004, ep.y, ep.z), (.008, .013, .013)), MAT["Metal"], body, "Head")
    mic_boom = ellipsoid((ep.x - 0.009, ep.y - 0.036, ep.z - 0.018), (.003, .036, .003), Matrix.Rotation(math.radians(-22), 4, 'X') @ Matrix.Rotation(math.radians(-16), 4, 'Z'))
    add_rigid_to_bone("HeadsetMic", mic_boom, MAT["Metal"], body, "Head")

# ---------- preview ----------
def render(path, loc, rot, ortho=None):
    sc = scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.color_type = 'MATERIAL'
    sc.display.shading.light = 'STUDIO'
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
    render(PREVIEW.replace(".png", "_close.png"), (0, -6, 1.35), (math.pi / 2, 0, 0), .85)

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
