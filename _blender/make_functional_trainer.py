"""Functional trainer (dual adjustable pulley / 다용도 케이블 머신) generator.

Run with Blender:      blender -b --python make_functional_trainer.py
or with the bpy module: python make_functional_trainer.py   [--out path.fbx]
Output: Assets/Gym/FunctionalTrainer.fbx  (meters, floor at z=0, front = -Y in Blender)

Layout (per tower):
  - side body: front panel with a sloped top + ribbed weight-stack shroud at the back
  - rear post (rounded square tube) on the inner side that bends forward into a top side rail
  - front: vertical carriage rail, pill-shaped carriage, round swivel pulley housing, D-handle
  - long splayed front foot, short rear foot
Between towers: arched pull-up bar on top, two rear cross beams with socket collars,
an attachment pad + hook bracket on the upper beam.
Materials share names/colors with the other Assets/Gym machines.
"""
import math
import os
import sys

import bpy  # bpy must be imported before bmesh when run as a module
import bmesh
from mathutils import Euler, Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "Assets", "Gym", "FunctionalTrainer.fbx")
if "--out" in sys.argv:
    OUT = sys.argv[sys.argv.index("--out") + 1]

# ---------- scene reset ----------
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
    "shroud": make_mat("M_Shroud", (0.1, 0.105, 0.115), 0.2, 0.4),
    "amber": make_mat("M_Accent_Amber", (1.0, 0.62, 0.04), 0.3, 0.3),
    "pin": make_mat("M_SelectorPin", (0.85, 0.08, 0.06), 0.3, 0.3),
    "pad": make_mat("M_Upholstery", (0.035, 0.035, 0.04), 0.0, 0.55),
    "cable": make_mat("M_Seam", (0.12, 0.12, 0.13), 0.0, 0.7),
}

ROOT = bpy.data.objects.new("FunctionalTrainer", None)
bpy.context.scene.collection.objects.link(ROOT)


def finish(name, bm, mat, smooth=False, bevel=0.0):
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
        mod.segments = 2
        mod.limit_method = "ANGLE"
    return ob


def box(name, center, size, mat, rot=(0, 0, 0), bevel=0.006):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    m = Matrix.Translation(center) @ Euler([math.radians(a) for a in rot]).to_matrix().to_4x4() @ Matrix.Diagonal((*size, 1))
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    return finish(name, bm, mat, bevel=bevel)


def prism_x(name, profile_yz, x0, x1, mat, bevel=0.008):
    """Convex polygon in the YZ plane extruded along X."""
    bm = bmesh.new()
    a = [bm.verts.new((x0, y, z)) for y, z in profile_yz]
    b = [bm.verts.new((x1, y, z)) for y, z in profile_yz]
    bm.faces.new(a[::-1])
    bm.faces.new(b)
    n = len(profile_yz)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((a[i], a[j], b[j], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return finish(name, bm, mat, bevel=bevel)


def _frame(d):
    d = d.normalized()
    up = Vector((0, 0, 1)) if abs(d.z) < 0.95 else Vector((1, 0, 0))
    x = up.cross(d).normalized()
    return Matrix((x, d.cross(x), d)).transposed()


def cyl(name, p1, p2, r, mat, seg=16, smooth=True):
    p1, p2 = Vector(p1), Vector(p2)
    d = p2 - p1
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r, depth=d.length)
    bmesh.ops.transform(bm, matrix=Matrix.Translation((p1 + p2) / 2) @ _frame(d).to_4x4(), verts=bm.verts)
    return finish(name, bm, mat, smooth)


def disc(name, center, axis, r, width, mat, seg=28):
    c, a = Vector(center), Vector(axis).normalized()
    return cyl(name, c - a * width / 2, c + a * width / 2, r, mat, seg)


def circle(r, seg=14):
    return [(math.cos(2 * math.pi * k / seg) * r, math.sin(2 * math.pi * k / seg) * r) for k in range(seg)]


def rrect(w, h, r, seg=3):
    """Rounded rectangle profile (w along the 'side' axis, h along the normal)."""
    pts = []
    for cx, cy, a0 in ((w / 2 - r, h / 2 - r, 0), (-w / 2 + r, h / 2 - r, 90), (-w / 2 + r, -h / 2 + r, 180), (w / 2 - r, -h / 2 + r, 270)):
        for k in range(seg + 1):
            a = math.radians(a0 + 90 * k / seg)
            pts.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
    return pts


def sweep(name, pts, side, profile, mat, smooth=True):
    """Extrude a 2D profile along a planar path. `side` is a fixed axis lying in the
    profile plane (normally the path plane's normal), which keeps the tube from twisting."""
    pts = [Vector(p) for p in pts]
    side = Vector(side).normalized()
    bm = bmesh.new()
    rings = []
    for i, p in enumerate(pts):
        t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        s = (side - t * side.dot(t)).normalized()
        n = t.cross(s)
        rings.append([bm.verts.new(p + s * u + n * v) for u, v in profile])
    k = len(profile)
    for ra, rb in zip(rings, rings[1:]):
        for i in range(k):
            j = (i + 1) % k
            bm.faces.new((ra[i], ra[j], rb[j], rb[i]))
    bm.faces.new(rings[0][::-1])
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return finish(name, bm, mat, smooth)


def bend_path(start, corner, end, radius, n=8):
    """Polyline start -> corner -> end with the corner rounded by `radius`."""
    s, c, e = Vector(start), Vector(corner), Vector(end)
    d1, d2 = (c - s).normalized(), (e - c).normalized()
    p1, p2 = c - d1 * radius, c + d2 * radius
    pts = [s]
    for i in range(n + 1):  # quadratic Bezier through the corner
        t = i / n
        pts.append(p1 * (1 - t) ** 2 + c * 2 * (1 - t) * t + p2 * t * t)
    pts.append(e)
    return pts


# ---------- dimensions (meters) ----------
TX = 0.58            # tower center x
TW = 0.22            # tower width (x)
Y_FRONT, Y_BACK = -0.40, 0.30
STACK_Y = 0.02       # where the ribbed weight-stack shroud starts
STACK_TOP = 1.90
FRONT_TOP = 2.12
POST_X = 0.44        # rear post center x (inner side of the tower)
POST_Y = 0.25
RAIL_Z = 2.18        # top side rail height
TUBE = rrect(0.075, 0.075, 0.02)

for side, s in (("L", -1), ("R", 1)):
    x = s * TX
    xi, xo = x - s * TW / 2, x + s * TW / 2  # inner / outer face x
    x0, x1 = sorted((xi, xo))

    # ----- base -----
    box(f"BaseRail_{side}", (x, (Y_FRONT + Y_BACK) / 2, 0.035), (TW + 0.02, Y_BACK - Y_FRONT + 0.04, 0.07), "graphite")
    fa = Vector((x + s * 0.02, Y_FRONT + 0.06, 0.05))
    fb = Vector((x + s * 0.24, Y_FRONT - 0.42, 0.04))
    sweep(f"FrontFoot_{side}", [fa, fb], (0, 0, 1), rrect(0.07, 0.05, 0.012), "silver")
    box(f"FrontFootPad_{side}", (fb.x, fb.y, 0.022), (0.14, 0.12, 0.044), "rubber", rot=(0, 0, s * 28))
    ra = Vector((xo, Y_BACK - 0.02, 0.05))
    rb = Vector((xo + s * 0.14, Y_BACK + 0.12, 0.04))
    sweep(f"RearFoot_{side}", [ra, rb], (0, 0, 1), rrect(0.06, 0.045, 0.01), "silver")
    box(f"RearFootPad_{side}", (rb.x, rb.y, 0.02), (0.10, 0.09, 0.04), "rubber", rot=(0, 0, -s * 40))
    box(f"PostFoot_{side}", (s * POST_X, POST_Y, 0.02), (0.13, 0.13, 0.04), "graphite")

    # ----- tower body -----
    prism_x(f"FrontPanel_{side}", [(Y_FRONT + 0.02, 0.07), (STACK_Y + 0.01, 0.07), (STACK_Y + 0.01, STACK_TOP), (Y_FRONT, FRONT_TOP)],
            x0 + 0.01, x1 - 0.01, "shroud")
    box(f"StackShroud_{side}", (x, (STACK_Y + Y_BACK) / 2, (0.07 + STACK_TOP) / 2), (TW, Y_BACK - STACK_Y, STACK_TOP - 0.07), "silver")
    for i in range(5):  # vertical flutes on the outer face and the rear face
        fy = STACK_Y + 0.03 + i * (Y_BACK - STACK_Y - 0.06) / 4
        box(f"Flute_{side}_{i}", (xo + s * 0.006, fy, (0.10 + STACK_TOP - 0.03) / 2), (0.012, 0.018, STACK_TOP - 0.13), "graphite", bevel=0.003)
    for i in range(3):
        fx = x + (i - 1) * 0.06
        box(f"RearFlute_{side}_{i}", (fx, Y_BACK + 0.006, (0.10 + STACK_TOP - 0.03) / 2), (0.018, 0.012, STACK_TOP - 0.13), "graphite", bevel=0.003)
    box(f"StackCap_{side}", (x, (STACK_Y + Y_BACK) / 2, STACK_TOP + 0.015), (TW + 0.01, Y_BACK - STACK_Y + 0.01, 0.03), "graphite")
    box(f"Accent_{side}", (xo + s * 0.002, (STACK_Y + Y_BACK) / 2, STACK_TOP - 0.06), (0.004, Y_BACK - STACK_Y - 0.04, 0.025), "amber", bevel=0)

    # top pulley bracket (plate between the rail bend and the sloped panel)
    box(f"TopBracket_{side}", (x, -0.02, 2.04), (0.05, 0.26, 0.16), "graphite")
    disc(f"TopPulley_{side}", (x + s * 0.03, -0.02, 2.04), (1, 0, 0), 0.06, 0.03, "rubber")

    # ----- rear post bending forward into the top side rail -----
    sweep(f"Post_{side}", bend_path((s * POST_X, POST_Y, 0.04), (s * POST_X, POST_Y, RAIL_Z), (s * POST_X, Y_FRONT - 0.04, RAIL_Z), 0.22),
          (1, 0, 0), TUBE, "silver")
    cyl(f"RailCap_{side}", (s * POST_X, Y_FRONT - 0.04, RAIL_Z), (s * POST_X, Y_FRONT - 0.06, RAIL_Z), 0.042, "rubber", 16)
    for tag, z in (("Upper", 1.05), ("Lower", 0.30)):  # socket collars for the cross beams
        box(f"Collar{tag}_{side}", (s * POST_X, POST_Y, z), (0.10, 0.10, 0.15), "graphite")

    # ----- front carriage + swivel pulley + handle -----
    ry = Y_FRONT - 0.035
    cyl(f"CarriageRail_{side}", (x, ry, 0.12), (x, ry, 2.02), 0.016, "chrome")
    box(f"RailBlockTop_{side}", (x, ry + 0.02, 2.03), (0.07, 0.07, 0.04), "graphite")
    box(f"RailBlockBottom_{side}", (x, ry + 0.02, 0.11), (0.07, 0.07, 0.04), "graphite")
    cz = 1.45
    box(f"Carriage_{side}", (x - s * 0.01, ry - 0.06, cz), (0.12, 0.16, 0.11), "graphite")
    disc(f"CarriageNose_{side}", (x - s * 0.01, ry - 0.14, cz), (1, 0, 0), 0.055, 0.12, "graphite")
    box(f"CarriageStripe_{side}", (x - s * 0.01, ry - 0.06, cz + 0.057), (0.08, 0.12, 0.004), "amber", bevel=0)
    lever = [Vector((x - s * 0.07, ry - 0.10, cz)), Vector((x - s * 0.10, ry - 0.12, cz - 0.03)), Vector((x - s * 0.11, ry - 0.13, cz - 0.11))]
    sweep(f"PopPinLever_{side}", lever, (0, 1, 0), circle(0.011, 10), "chrome")
    hx = xo + s * 0.04
    disc(f"SwivelHousing_{side}", (hx, ry - 0.02, cz + 0.03), (1, 0, 0), 0.10, 0.05, "graphite")
    disc(f"SwivelPulley_{side}", (hx + s * 0.03, ry - 0.02, cz + 0.03), (1, 0, 0), 0.06, 0.02, "rubber")
    box(f"SwivelArm_{side}", (hx + s * 0.02, ry - 0.07, cz - 0.07), (0.04, 0.05, 0.10), "graphite", rot=(-20, 0, 0))
    hy = ry - 0.10
    top, apex = cz - 0.13, cz - 0.24
    cyl(f"Cable_{side}", (hx + s * 0.02, hy, top), (hx + s * 0.02, hy, apex), 0.004, "cable", 8)
    cyl(f"CableDown_{side}", (hx, ry - 0.005, cz - 0.05), (hx, ry - 0.005, 0.16), 0.003, "cable", 6)
    disc(f"HandleRing_{side}", (hx + s * 0.02, hy, apex - 0.02), (0, 1, 0), 0.02, 0.006, "chrome", 12)
    a = Vector((hx + s * 0.02, hy, apex - 0.04))
    bl, br = a + Vector((-0.08, 0, -0.17)), a + Vector((0.08, 0, -0.17))
    cyl(f"HandleSideA_{side}", a, bl, 0.009, "chrome", 10)
    cyl(f"HandleSideB_{side}", a, br, 0.009, "chrome", 10)
    cyl(f"HandleGrip_{side}", bl, br, 0.017, "rubber", 16)

# ---------- between the towers ----------
for tag, z in (("Upper", 1.05), ("Lower", 0.30)):
    sweep(f"CrossBeam_{tag}", [(-POST_X + 0.05, POST_Y, z), (POST_X - 0.05, POST_Y, z)], (0, 0, 1), rrect(0.10, 0.07, 0.015), "silver")

# arched pull-up bar resting on both side rails
bar = []
for i in range(25):
    t = i / 24
    bx = -(TX + 0.06) + 2 * (TX + 0.06) * t
    bar.append((bx, -0.24, RAIL_Z + 0.06 + 0.035 * math.sin(math.pi * t)))
sweep("PullupBar", bar, (0, 1, 0), circle(0.022, 14), "silver")
for side, s in (("L", -1), ("R", 1)):
    box(f"PullupMount_{side}", (s * POST_X, -0.24, RAIL_Z + 0.04), (0.06, 0.06, 0.05), "graphite")
    cyl(f"PullupCap_{side}", (s * (TX + 0.06), -0.24, RAIL_Z + 0.06), (s * (TX + 0.08), -0.24, RAIL_Z + 0.06), 0.026, "rubber")

# attachment pad + hook bracket on the upper beam
box("Hub_Bracket", (0, POST_Y - 0.02, 1.12), (0.10, 0.10, 0.08), "graphite")
box("Hub_PadBase", (0, POST_Y - 0.10, 1.17), (0.26, 0.18, 0.025), "graphite")
box("Hub_Pad", (0, POST_Y - 0.08, 1.24), (0.28, 0.06, 0.20), "pad", rot=(-25, 0, 0), bevel=0.015)
cyl("Hub_Knob", (-0.05, POST_Y - 0.02, 1.12), (-0.11, POST_Y - 0.02, 1.12), 0.012, "pin", 10)
hook = bend_path((0, POST_Y + 0.02, 1.16), (0, POST_Y + 0.02, 1.42), (0, POST_Y - 0.10, 1.42), 0.06)
hook.append(Vector((0, POST_Y - 0.10, 1.36)))
sweep("Hub_Hook", hook, (1, 0, 0), circle(0.012, 10), "chrome")

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
