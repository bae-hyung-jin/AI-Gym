"""Functional trainer (dual adjustable pulley / 다용도 케이블 머신) generator.

Run:  blender -b --python make_functional_trainer.py
  or  python -c "import bpy" based env:  python make_functional_trainer.py
Output: Assets/Gym/FunctionalTrainer.fbx  (meters, Z-up in Blender, front = -Y)
Materials use the same names/colors as the other Assets/Gym machines.
"""
import math
import os
import sys

import bpy  # noqa: E402 (bpy must be imported before bmesh when run as a module)
import bmesh
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "Assets", "Gym", "FunctionalTrainer.fbx")
if "--out" in sys.argv:
    OUT = sys.argv[sys.argv.index("--out") + 1]

# ---------- scene reset ----------
for o in list(bpy.data.objects):
    bpy.data.objects.remove(o)
for m in list(bpy.data.meshes):
    bpy.data.meshes.remove(m)
for m in list(bpy.data.materials):
    bpy.data.materials.remove(m)


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
    "shroud": make_mat("M_Shroud", (0.1, 0.105, 0.115), 0.2, 0.4),
    "amber": make_mat("M_Accent_Amber", (1.0, 0.62, 0.04), 0.3, 0.3),
    "pin": make_mat("M_SelectorPin", (0.85, 0.08, 0.06), 0.3, 0.3),
    "plate": make_mat("M_WeightPlate", (0.05, 0.05, 0.055), 0.6, 0.35),
    "cable": make_mat("M_Seam", (0.12, 0.12, 0.13), 0.0, 0.7),
}

ROOT = bpy.data.objects.new("FunctionalTrainer", None)
bpy.context.scene.collection.objects.link(ROOT)


def finish(name, bm, mat, smooth=False):
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
    return ob


def box(name, center, size, mat):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.transform(bm, matrix=Matrix.Translation(center) @ Matrix.Diagonal((*size, 1)), verts=bm.verts)
    return finish(name, bm, mat)


def tapered(name, z0, z1, bot, top, center_xy, mat):
    """Box whose top face is smaller than the bottom (tower shape)."""
    bm = bmesh.new()
    cx, cy = center_xy
    vs = []
    for z, (sx, sy, oy) in ((z0, bot), (z1, top)):
        for x, y in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            vs.append(bm.verts.new((cx + x * sx / 2, cy + oy + y * sy / 2, z)))
    b, t = vs[:4], vs[4:]
    bm.faces.new(b[::-1])
    bm.faces.new(t)
    for i in range(4):
        j = (i + 1) % 4
        bm.faces.new((b[i], b[j], t[j], t[i]))
    return finish(name, bm, mat)


def _frame(d):
    d = d.normalized()
    up = Vector((0, 0, 1)) if abs(d.z) < 0.95 else Vector((1, 0, 0))
    x = up.cross(d).normalized()
    y = d.cross(x)
    return Matrix((x, y, d)).transposed()


def cyl(name, p1, p2, r, mat, seg=16, smooth=True):
    p1, p2 = Vector(p1), Vector(p2)
    d = p2 - p1
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r, depth=d.length)
    rot = _frame(d).to_4x4()
    bmesh.ops.transform(bm, matrix=Matrix.Translation((p1 + p2) / 2) @ rot, verts=bm.verts)
    return finish(name, bm, mat, smooth)


def wheel(name, center, axis, r, width, mat, seg=24):
    c, a = Vector(center), Vector(axis).normalized()
    return cyl(name, c - a * width / 2, c + a * width / 2, r, mat, seg)


def sweep(name, pts, r, mat, seg=14):
    """Round tube along a polyline (used for the curved top bars)."""
    pts = [Vector(p) for p in pts]
    bm = bmesh.new()
    rings = []
    for i, p in enumerate(pts):
        t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        m = _frame(t)
        ring = []
        for k in range(seg):
            a = 2 * math.pi * k / seg
            ring.append(bm.verts.new(p + m @ Vector((math.cos(a) * r, math.sin(a) * r, 0))))
        rings.append(ring)
    for ra, rb in zip(rings, rings[1:]):
        for k in range(seg):
            j = (k + 1) % seg
            bm.faces.new((ra[k], ra[j], rb[j], rb[k]))
    bm.faces.new(rings[0][::-1])
    bm.faces.new(rings[-1])
    return finish(name, bm, mat, True)


# ---------- dimensions (meters) ----------
TX = 0.62          # tower center x (left = -TX, right = +TX)
H = 2.05           # tower top
ARCH_Z = 2.16

for side, s in (("L", -1), ("R", 1)):
    x = s * TX
    # base plate and splayed feet
    box(f"Base_{side}", (x, 0, 0.04), (0.32, 0.40, 0.08), "graphite")
    for tag, fy in (("Front", -1), ("Rear", 1)):
        a = (x, fy * 0.14, 0.07)
        b = (x + s * 0.22, fy * 0.62, 0.035)
        cyl(f"Foot_{tag}_{side}", a, b, 0.032, "silver")
        box(f"FootPad_{tag}_{side}", (b[0], b[1], 0.02), (0.12, 0.12, 0.04), "rubber")

    # tower: silver tapered body + dark inner panel + top housing
    tapered(f"Tower_{side}", 0.08, H, (0.26, 0.34, 0.0), (0.20, 0.24, 0.02), (x, 0), "silver")
    box(f"TowerPanel_{side}", (x - s * 0.105, 0.0, 1.05), (0.02, 0.22, 1.70), "shroud")
    box(f"Window_{side}", (x - s * 0.118, 0.0, 0.70), (0.008, 0.10, 0.80), "graphite")
    for i in range(10):  # weight stack visible through the window
        box(f"Weight_{side}_{i:02d}", (x - s * 0.123, 0.0, 0.34 + i * 0.075), (0.006, 0.085, 0.06), "plate")
    cyl(f"SelectorPin_{side}", (x - s * 0.125, 0.0, 0.64), (x - s * 0.18, 0.0, 0.64), 0.008, "pin", 10)
    box(f"TopHousing_{side}", (x, 0.0, H + 0.05), (0.24, 0.30, 0.10), "graphite")
    box(f"Accent_{side}", (x, -0.152, H - 0.10), (0.16, 0.006, 0.03), "amber")
    wheel(f"TopPulley_{side}", (x, -0.10, H + 0.05), (1, 0, 0), 0.045, 0.03, "rubber")

    # adjustable carriage on a front rail, pulley swivels outward
    rail_y = -0.21
    cyl(f"Rail_{side}", (x, rail_y, 0.18), (x, rail_y, 1.98), 0.018, "chrome")
    box(f"RailCapTop_{side}", (x, rail_y + 0.03, 1.99), (0.08, 0.09, 0.04), "graphite")
    box(f"RailCapBottom_{side}", (x, rail_y + 0.03, 0.17), (0.08, 0.09, 0.04), "graphite")
    cz = 1.15
    box(f"Carriage_{side}", (x, rail_y, cz), (0.10, 0.08, 0.20), "graphite")
    cyl(f"CarriagePin_{side}", (x, rail_y - 0.04, cz + 0.06), (x, rail_y - 0.09, cz + 0.06), 0.012, "pin", 10)
    px = x + s * 0.11
    box(f"PulleyArm_{side}", (x + s * 0.06, rail_y, cz - 0.02), (0.10, 0.05, 0.04), "graphite")
    for k, dy in (("A", -0.025), ("B", 0.025)):
        box(f"PulleyPlate{k}_{side}", (px, rail_y + dy, cz - 0.03), (0.13, 0.008, 0.13), "silver")
    wheel(f"Pulley_{side}", (px, rail_y, cz - 0.03), (0, 1, 0), 0.05, 0.03, "rubber")

    # cable + D-handle hanging from the pulley
    hx = px + s * 0.03
    top, apex = cz - 0.10, 0.92
    cyl(f"Cable_{side}", (hx, rail_y, top), (hx, rail_y, apex), 0.004, "cable", 8)
    cyl(f"Carabiner_{side}", (hx, rail_y, apex), (hx, rail_y, apex - 0.04), 0.008, "chrome", 10)
    a, bl, br = Vector((hx, rail_y, apex - 0.04)), Vector((hx - 0.08, rail_y, 0.76)), Vector((hx + 0.08, rail_y, 0.76))
    cyl(f"HandleSideA_{side}", a, bl, 0.01, "chrome", 10)
    cyl(f"HandleSideB_{side}", a, br, 0.01, "chrome", 10)
    cyl(f"HandleGrip_{side}", bl, br, 0.017, "rubber", 16)

# ---------- connections between towers ----------
for tag, z in (("Upper", 0.98), ("Lower", 0.30)):
    box(f"CrossBeam_{tag}", (0, 0.06, z), (2 * TX - 0.20, 0.08, 0.07), "silver")
# attachment hub on the upper beam (center block in the reference)
box("Hub_Block", (0, -0.03, 1.08), (0.16, 0.12, 0.14), "shroud")
box("Hub_Cap", (0, -0.03, 1.16), (0.18, 0.14, 0.02), "graphite")
cyl("Hub_PegL", (-0.05, -0.09, 1.06), (-0.05, -0.17, 1.06), 0.012, "chrome", 10)
cyl("Hub_PegR", (0.05, -0.09, 1.06), (0.05, -0.17, 1.06), 0.012, "chrome", 10)

# ---------- curved top bars (pull-up bar) ----------
def arch(y, rise, n=24):
    pts = []
    for i in range(n + 1):
        t = i / n
        xx = -TX + 2 * TX * t
        z = ARCH_Z + rise * math.sin(math.pi * t) - 0.03 * math.cos(2 * math.pi * t)
        pts.append((xx, y, z))
    return pts


sweep("TopBar_Front", arch(-0.13, 0.04), 0.024, "silver")
sweep("TopBar_Rear", arch(0.13, 0.02), 0.024, "silver")
for i, xx in enumerate((-0.30, 0.0, 0.30)):
    cyl(f"TopBar_Brace_{i}", (xx, -0.13, ARCH_Z + 0.03), (xx, 0.13, ARCH_Z + 0.03), 0.015, "graphite", 10)
for side, s in (("L", -1), ("R", 1)):
    cyl(f"PullupGrip_{side}", (s * 0.42, -0.13, ARCH_Z + 0.02), (s * 0.24, -0.13, ARCH_Z + 0.045), 0.03, "rubber")

# ---------- export ----------
bpy.ops.export_scene.fbx(
    filepath=os.path.abspath(OUT),
    use_selection=False,
    object_types={"EMPTY", "MESH"},
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_UNITS",
    axis_forward="-Z",
    axis_up="Y",
    bake_space_transform=True,
    mesh_smooth_type="FACE",
    add_leaf_bones=False,
)
print("WROTE", os.path.abspath(OUT), len(ROOT.children), "parts")
