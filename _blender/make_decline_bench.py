"""Adjustable decline / sit-up bench (디클라인 벤치, 다리 롤러 4개) generator.

Run with Blender:      blender -b --python make_decline_bench.py
or with the bpy module: python make_decline_bench.py   [--out path.fbx]
Output: Assets/Gym/DeclineBench.fbx  (meters, floor at z=0)
Length runs along X: head end at -X, feet/rollers at +X. Width along Y.

Parts: rear stabilizer with a curved post, spine beam that drops to the front stabilizer
(with transport wheels), back pad + seat pad (stitched leather look: piping + lower shell),
leg arm with 4 foam rollers and chrome end caps, L-shaped grab handle, adjustment strut
resting in ladder hooks, pop-pins and bolt heads. Materials match the other Assets/Gym machines.
"""
import math
import os
import sys

import bpy  # bpy must be imported before bmesh when run as a module
import bmesh
from mathutils import Euler, Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "Assets", "Gym", "DeclineBench.fbx")
if "--out" in sys.argv:
    OUT = sys.argv[sys.argv.index("--out") + 1]

for coll in (bpy.data.objects, bpy.data.meshes, bpy.data.materials):
    for item in list(coll):
        coll.remove(item)


def make_mat(name, rgb, metal, rough):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*rgb, 1)
    b.inputs["Metallic"].default_value = metal
    b.inputs["Roughness"].default_value = rough
    m.diffuse_color = (*rgb, 1)
    return m


MAT = {
    "silver": make_mat("M_Frame_Silver", (0.62, 0.64, 0.67), 0.85, 0.32),
    "graphite": make_mat("M_Frame_Graphite", (0.07, 0.075, 0.085), 0.5, 0.45),
    "chrome": make_mat("M_Chrome", (0.9, 0.9, 0.92), 1.0, 0.08),
    "rubber": make_mat("M_Rubber", (0.02, 0.02, 0.02), 0.0, 0.9),
    "pad": make_mat("M_Upholstery", (0.035, 0.035, 0.04), 0.0, 0.55),
    "seam": make_mat("M_Seam", (0.12, 0.12, 0.13), 0.0, 0.7),
    "amber": make_mat("M_Accent_Amber", (1.0, 0.62, 0.04), 0.3, 0.3),
}

ROOT = bpy.data.objects.new("DeclineBench", None)
bpy.context.scene.collection.objects.link(ROOT)


def finish(name, bm, mat, smooth=False, bevel=0.0, xf=None):
    if xf is not None:
        bmesh.ops.transform(bm, matrix=xf, verts=bm.verts)
    me = bpy.data.meshes.new(name)
    bm.normal_update()
    bm.to_mesh(me)
    bm.free()
    me.materials.append(MAT[mat])
    if smooth:
        me.shade_smooth()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    ob.parent = ROOT
    if bevel:
        mod = ob.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 3
        mod.limit_method = "ANGLE"
        if smooth:
            mod.harden_normals = False
    return ob


def box(name, center, size, mat, rot=(0, 0, 0), bevel=0.004, xf=None, smooth=False):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    m = Matrix.Translation(center) @ Euler([math.radians(a) for a in rot]).to_matrix().to_4x4() @ Matrix.Diagonal((*size, 1))
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    return finish(name, bm, mat, smooth=smooth, bevel=bevel, xf=xf)


def _frame(d):
    d = d.normalized()
    up = Vector((0, 0, 1)) if abs(d.z) < 0.95 else Vector((1, 0, 0))
    x = up.cross(d).normalized()
    return Matrix((x, d.cross(x), d)).transposed()


def cyl(name, p1, p2, r, mat, seg=20, smooth=True, r2=None):
    p1, p2 = Vector(p1), Vector(p2)
    d = p2 - p1
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r if r2 is None else r2, depth=d.length)
    bmesh.ops.transform(bm, matrix=Matrix.Translation((p1 + p2) / 2) @ _frame(d).to_4x4(), verts=bm.verts)
    return finish(name, bm, mat, smooth)


def circle(r, seg=16):
    return [(math.cos(2 * math.pi * k / seg) * r, math.sin(2 * math.pi * k / seg) * r) for k in range(seg)]


def rrect(w, h, r, seg=3):
    pts = []
    for cx, cy, a0 in ((w / 2 - r, h / 2 - r, 0), (-w / 2 + r, h / 2 - r, 90), (-w / 2 + r, -h / 2 + r, 180), (w / 2 - r, -h / 2 + r, 270)):
        for k in range(seg + 1):
            a = math.radians(a0 + 90 * k / seg)
            pts.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
    return pts


def sweep(name, pts, side, profile, mat, smooth=True, xf=None, closed=False):
    pts = [Vector(p) for p in pts]
    side = Vector(side).normalized()
    n_pts = len(pts)
    bm = bmesh.new()
    rings = []
    for i, p in enumerate(pts):
        if closed:
            t = (pts[(i + 1) % n_pts] - pts[(i - 1) % n_pts]).normalized()
        else:
            t = (pts[min(i + 1, n_pts - 1)] - pts[max(i - 1, 0)]).normalized()
        s = (side - t * side.dot(t)).normalized()
        n = t.cross(s)
        rings.append([bm.verts.new(p + s * u + n * v) for u, v in profile])
    k = len(profile)
    pairs = list(zip(rings, rings[1:])) + ([(rings[-1], rings[0])] if closed else [])
    for ra, rb in pairs:
        for i in range(k):
            j = (i + 1) % k
            bm.faces.new((ra[i], ra[j], rb[j], rb[i]))
    if not closed:
        bm.faces.new(rings[0][::-1])
        bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return finish(name, bm, mat, smooth, xf=xf)


def bend_path(pts, radius, n=8):
    """Polyline through pts with every inner corner rounded by `radius`."""
    pts = [Vector(p) for p in pts]
    out = [pts[0]]
    for a, c, e in zip(pts, pts[1:], pts[2:]):
        d1, d2 = (c - a).normalized(), (e - c).normalized()
        r = min(radius, (c - a).length / 2, (e - c).length / 2)
        p1, p2 = c - d1 * r, c + d2 * r
        for i in range(n + 1):
            t = i / n
            out.append(p1 * (1 - t) ** 2 + c * 2 * (1 - t) * t + p2 * t * t)
    out.append(pts[-1])
    return out


def bolt(name, center, axis, r=0.011):
    c, a = Vector(center), Vector(axis).normalized()
    cyl(name, c, c + a * 0.008, r, "chrome", 6, smooth=False)


def pad(name, center, length, width, thick, tilt_deg):
    """Leather pad: soft top cushion + piping around the top edge + a graphite shell below."""
    xf = Matrix.Translation(center) @ Matrix.Rotation(math.radians(-tilt_deg), 4, "Y")
    box(f"{name}_Cushion", (0, 0, thick * 0.15), (length, width, thick * 0.7), "pad", bevel=0.022, xf=xf, smooth=True)
    box(f"{name}_Shell", (0, 0, -thick * 0.33), (length - 0.01, width - 0.01, thick * 0.3), "graphite", bevel=0.006, xf=xf)
    # piping: a thin rounded loop just below the cushion's top edge
    hl, hw, z = length / 2 - 0.012, width / 2 - 0.012, thick * 0.38
    loop = bend_path([(-hl, 0, z), (-hl, hw, z), (hl, hw, z), (hl, -hw, z), (-hl, -hw, z), (-hl, 0, z)], 0.03, 4)
    sweep(f"{name}_Piping", loop[:-1], (0, 0, 1), circle(0.0045, 8), "seam", xf=xf, closed=True)
    return xf


# ---------- layout (meters) ----------
W = 0.30           # half width of the stabilizers
SPINE = rrect(0.075, 0.055, 0.012)
TUBE = rrect(0.06, 0.06, 0.012)
SPINE_Z = 0.20

# rear stabilizer + curved post
RX = -0.60
box("RearBase", (RX, 0, 0.045), (0.07, 2 * W, 0.06), "silver")
for side, s in (("L", 1), ("R", -1)):
    box(f"RearBaseCap_{side}", (RX, s * (W + 0.012), 0.045), (0.07, 0.024, 0.06), "rubber", bevel=0.003)
    cyl(f"RearFoot_{side}", (RX, s * (W - 0.04), 0.015), (RX, s * (W - 0.04), 0.0), 0.022, "rubber")
box("RearBasePlate", (RX, 0, 0.078), (0.10, 0.16, 0.008), "silver", bevel=0.002)
for i, y in enumerate((-0.05, 0.05)):
    bolt(f"RearBaseBolt_{i}", (RX - 0.051, y, 0.05), (-1, 0, 0))

# spine: up from the rear base, flat under the pads, then down to the front base
FX = 0.70
spine_pts = bend_path([(RX + 0.02, 0, 0.08), (RX + 0.06, 0, SPINE_Z), (0.18, 0, SPINE_Z), (FX - 0.03, 0, 0.09)], 0.12, 10)
sweep("Spine", spine_pts, (0, 1, 0), SPINE, "silver")
box("SpineLogoPlate", (-0.15, 0.0, SPINE_Z), (0.26, 0.078, 0.032), "graphite", bevel=0.003)
for side, s in (("L", 1), ("R", -1)):
    box(f"SpineAccent_{side}", (-0.15, s * 0.0395, SPINE_Z), (0.22, 0.002, 0.008), "amber", bevel=0)

# front stabilizer with transport wheels
box("FrontBase", (FX, 0, 0.045), (0.07, 2 * W, 0.06), "silver")
box("FrontBasePlate", (FX - 0.02, 0, 0.078), (0.10, 0.16, 0.008), "silver", bevel=0.002)
for side, s in (("L", 1), ("R", -1)):
    box(f"FrontBaseCap_{side}", (FX, s * (W + 0.012), 0.045), (0.07, 0.024, 0.06), "rubber", bevel=0.003)
    cyl(f"FrontFoot_{side}", (FX - 0.02, s * (W - 0.04), 0.015), (FX - 0.02, s * (W - 0.04), 0.0), 0.022, "rubber")
    wy = s * (W + 0.035)
    cyl(f"Wheel_{side}", (FX + 0.035, wy - 0.012, 0.042), (FX + 0.035, wy + 0.012, 0.042), 0.04, "rubber", 24)
    cyl(f"WheelHub_{side}", (FX + 0.035, wy + s * 0.012, 0.042), (FX + 0.035, wy + s * 0.016, 0.042), 0.018, "chrome", 16)
    box(f"WheelBracket_{side}", (FX + 0.02, s * (W + 0.018), 0.05), (0.06, 0.006, 0.05), "silver", bevel=0.002)

# ---------- pads ----------
back_xf = pad("BackPad", (-0.40, 0, 0.355), 0.66, 0.27, 0.075, 9)
seat_xf = pad("SeatPad", (0.07, 0, 0.42), 0.30, 0.29, 0.075, 6)
# pad rails + brackets (silver channel under each pad, bolted)
sweep("BackRail", [(-0.68, 0, 0.29), (-0.10, 0, 0.38)], (0, 1, 0), rrect(0.05, 0.035, 0.008), "silver")
sweep("SeatRail", [(-0.07, 0, 0.375), (0.22, 0, 0.405)], (0, 1, 0), rrect(0.05, 0.035, 0.008), "silver")
for i, x in enumerate((-0.58, -0.30, -0.02, 0.15)):
    z = 0.30 + (x + 0.68) * 0.135 if x < -0.1 else 0.385 + (x + 0.07) * 0.1
    box(f"PadBracket_{i}", (x, 0, z - 0.03), (0.07, 0.065, 0.045), "graphite", bevel=0.003)
    for side, s in (("L", 1), ("R", -1)):
        bolt(f"PadBolt_{i}_{side}", (x, s * 0.0325, z - 0.03), (0, s, 0))

# rear hinge + support post from spine to back rail
sweep("BackSupport", [(-0.48, 0, SPINE_Z), (-0.50, 0, 0.30)], (0, 1, 0), rrect(0.05, 0.05, 0.01), "silver")
for side, s in (("L", 1), ("R", -1)):
    box(f"HingePlate_{side}", (-0.49, s * 0.04, 0.25), (0.09, 0.008, 0.12), "silver", bevel=0.003)
    bolt(f"HingeBolt_{side}", (-0.49, s * 0.044, 0.29), (0, s, 0), 0.013)
    bolt(f"HingeBoltLow_{side}", (-0.49, s * 0.044, 0.215), (0, s, 0), 0.011)

# head-end side grips (short cross tube under the back pad)
cyl("HeadGripTube", (-0.62, -0.34, 0.27), (-0.62, 0.34, 0.27), 0.017, "silver")
for side, s in (("L", 1), ("R", -1)):
    cyl(f"HeadGrip_{side}", (-0.62, s * 0.24, 0.27), (-0.62, s * 0.355, 0.27), 0.022, "rubber")
    cyl(f"HeadGripCap_{side}", (-0.62, s * 0.355, 0.27), (-0.62, s * 0.362, 0.27), 0.019, "chrome", 16)
box("HeadGripMount", (-0.62, 0, 0.285), (0.05, 0.08, 0.04), "graphite", bevel=0.003)

# pop-pin at the head end
cyl("RearPopPin", (-0.70, 0, 0.255), (-0.80, 0, 0.245), 0.008, "chrome", 10)
cyl("RearPopPinKnob", (-0.80, 0, 0.245), (-0.83, 0, 0.243), 0.016, "rubber", 16)

# ---------- leg arm with rollers ----------
HUB = Vector((0.46, 0, 0.62))
sweep("LegArm", [(0.18, 0, 0.40), HUB], (0, 1, 0), TUBE, "silver")
box("LegArmCollar", (0.26, 0, 0.47), (0.085, 0.085, 0.07), "silver", rot=(0, -36, 0), bevel=0.006)
bolt("LegArmCollarBolt", (0.26, 0.043, 0.47), (0, 1, 0), 0.012)
# roller beam (front-back) on top of the hub, two axles
sweep("RollerBeam", [(0.20, 0, 0.60), (0.52, 0, 0.60)], (0, 1, 0), rrect(0.055, 0.05, 0.01), "silver")
box("RollerBeamGusset", (0.44, 0, 0.585), (0.10, 0.05, 0.05), "silver", rot=(0, 30, 0), bevel=0.004)
for ai, ax in enumerate((0.23, 0.49)):
    axle = Vector((ax, 0, 0.635))
    box(f"AxleBlock_{ai}", (ax, 0, 0.615), (0.06, 0.06, 0.05), "graphite", bevel=0.004)
    cyl(f"Axle_{ai}", axle + Vector((0, -0.29, 0)), axle + Vector((0, 0.29, 0)), 0.014, "chrome", 16)
    for side, s in (("L", 1), ("R", -1)):
        y0, y1 = s * 0.045, s * 0.255
        cyl(f"Roller_{ai}_{side}", axle + Vector((0, y0, 0)), axle + Vector((0, y1, 0)), 0.078, "pad", 32)
        cyl(f"RollerBevel_{ai}_{side}", axle + Vector((0, y1, 0)), axle + Vector((0, y1 + s * 0.008, 0)), 0.078, "pad", 32, r2=0.068)
        cyl(f"RollerCap_{ai}_{side}", axle + Vector((0, y1 + s * 0.008, 0)), axle + Vector((0, y1 + s * 0.016, 0)), 0.038, "chrome", 24)
        cyl(f"RollerCapRing_{ai}_{side}", axle + Vector((0, y1 + s * 0.006, 0)), axle + Vector((0, y1 + s * 0.010, 0)), 0.048, "amber", 24)
        cyl(f"RollerKnob_{ai}_{side}", axle + Vector((0, y1 + s * 0.016, 0)), axle + Vector((0, y1 + s * 0.032, 0)), 0.016, "rubber", 6, smooth=False)

# L-shaped grab handle rising from the roller beam
handle = bend_path([(0.33, 0, 0.62), (0.27, 0, 0.92), (0.40, 0, 0.965)], 0.05, 8)
sweep("GrabHandle", handle, (0, 1, 0), circle(0.016, 16), "graphite")
sweep("GrabHandleGrip", handle[len(handle) // 2:], (0, 1, 0), circle(0.019, 16), "rubber")
cyl("GrabHandleCap", Vector(handle[-1]), Vector(handle[-1]) + (Vector(handle[-1]) - Vector(handle[-2])).normalized() * 0.015, 0.019, "chrome", 6, smooth=False)

# ---------- adjustment strut + ladder ----------
strut_top = Vector((0.27, 0, 0.47))
strut_bot = Vector((0.56, 0, 0.165))
sweep("Strut", [strut_top, strut_bot], (0, 1, 0), rrect(0.045, 0.045, 0.01), "silver")
sl = strut_top + (strut_bot - strut_top) * 0.22
box("StrutSleeve", tuple(sl), (0.07, 0.055, 0.055), "silver", rot=(0, 46, 0), bevel=0.005)
cyl("StrutRung", strut_bot + Vector((0, -0.06, 0.02)), strut_bot + Vector((0, 0.06, 0.02)), 0.01, "chrome", 12)
# ladder hooks welded on the sloped part of the spine
for i, (x, z) in enumerate(((0.36, 0.205), (0.47, 0.17), (0.58, 0.13))):
    for side, s in (("L", 1), ("R", -1)):
        box(f"LadderHook_{i}_{side}", (x, s * 0.032, z + 0.04), (0.05, 0.006, 0.06), "silver", rot=(0, -20, 0), bevel=0.002)
# side pop-pins on the spine (black knobs)
for i, x in enumerate((0.30, 0.52)):
    z = SPINE_Z if x < 0.25 else SPINE_Z - (x - 0.25) * 0.24
    cyl(f"SidePin_{i}", (x, -0.13, z), (x, 0.13, z), 0.011, "chrome", 12)
    for side, s in (("L", 1), ("R", -1)):
        cyl(f"SidePinKnob_{i}_{side}", (x, s * 0.10, z), (x, s * 0.14, z), 0.016, "rubber", 16)

# ---------- export ----------
bpy.ops.export_scene.fbx(
    filepath=os.path.abspath(OUT),
    use_selection=False,
    object_types={"EMPTY", "MESH"},
    use_mesh_modifiers=True,
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_UNITS",
    axis_forward="-Z",
    axis_up="Y",
    bake_space_transform=True,
    mesh_smooth_type="FACE",
    add_leaf_bones=False,
)
print("WROTE", os.path.abspath(OUT), len(ROOT.children), "parts")
