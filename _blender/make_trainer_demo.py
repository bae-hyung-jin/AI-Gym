"""Build a seated, bar-gripping Lat Pulldown demonstration animation on TrainerHuman in Blender 5.2.
Run: blender -b --python _blender/make_trainer_demo.py
Produces:
  - Assets/Characters/TrainerHuman_Demo.fbx (Humanoid + baked 'LatPulldown_Demo' 150-frame action matching LatPulldown_Pull)
  - Optional preview PNGs when DEMO_PREVIEW_DIR is set (shows reference seat, thigh pads, floor, and angled lat bar)
"""
import os, math
import bpy
from mathutils import Vector, Matrix, Quaternion

ROOT = r"G:\Unity_Gym\AI-Gym"
IN_BLEND = os.path.join(ROOT, "_blender", "TrainerHuman.blend")
OUT_FBX = os.path.join(ROOT, "Assets", "Characters", "TrainerHuman_Demo.fbx")
OUT_BLEND = os.path.join(ROOT, "_blender", "TrainerHuman_Demo.blend")
PREVIEW_DIR = os.environ.get("DEMO_PREVIEW_DIR", "")

# Unity TrainerHuman import uses globalScale = 1.12 so shoulder-to-wrist reach matches LatPulldown (2) comfortably.
UNI_SCALE = 1.12
S = 1.0 / UNI_SCALE

# Machine world coordinates relative to TrainerSeat center X=-1.653, Z=5.754, Floor=0.004
# Blender trainer space: -Y = forward (Unity -X), +X = trainer Left (Unity +Z), +Z = Up (Unity +Y)
SEAT_TOP_Z = (0.790 - 0.004) * S       # 0.702
PLATE_TOP_Z = (0.838 - 0.004) * S      # 0.745
PAD_BOT_Z = (0.977 - 0.004) * S        # 0.869
PAD_FWD_Y = -(2.092 - 1.653) * S       # -0.392
FLOOR_Z = (0.040 - 0.004) * S          # 0.032 (gym mat top)

BAR_FWD_Y = -(1.849 - 1.653) * S       # -0.175
BAR_GRIP_X = 0.377 * S                 # +-0.3366
BAR_RAD = 0.032 * S                    # 0.0286 (black foam radius)
# Bar_Bend -> Bar_Grip tilt: dx_lat=0.206, dz=-0.135 -> ~33.2 deg outward-downward
BAR_SLOPE_L = Vector((0.206, 0.0, -0.135)).normalized()
BAR_SLOPE_R = Vector((-0.206, 0.0, -0.135)).normalized()

# Bar height across cycle with -0.11m demo barSlide offset (matches DemoTrainerFix / comfortable collarbone pull)
BAR_TOP_Z = (1.906 - 0.110 - 0.004) * S   # 1.600
BAR_BOT_Z = (1.517 - 0.110 - 0.004) * S   # 1.256

bpy.ops.wm.open_mainfile(filepath=IN_BLEND)
scene = bpy.context.scene
col = scene.collection
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
bpy.context.view_layer.objects.active = arm

# Clean existing actions
for act in list(bpy.data.actions):
    bpy.data.actions.remove(act)

# ---------- Reference geometry (for visual inspection in Blender only, hidden from FBX export) ----------
ref_col = bpy.data.collections.new("REF_Machine")
col.children.link(ref_col)

def add_ref_box(name, center, size, rgb):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=center)
    ob = bpy.context.active_object
    ob.name = name
    ob.scale = size
    m = bpy.data.materials.new(name + "_m")
    m.diffuse_color = (*rgb, 1.0)
    ob.data.materials.append(m)
    for c in ob.users_collection:
        c.objects.unlink(ob)
    ref_col.objects.link(ob)
    return ob

def add_ref_cyl(name, center, radius, length, rot_euler, rgb):
    bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=radius, depth=length, location=center, rotation=rot_euler)
    ob = bpy.context.active_object
    ob.name = name
    m = bpy.data.materials.new(name + "_m")
    m.diffuse_color = (*rgb, 1.0)
    ob.data.materials.append(m)
    for c in ob.users_collection:
        c.objects.unlink(ob)
    ref_col.objects.link(ob)
    return ob

add_ref_box("REF_Seat", (0.0, 0.0, SEAT_TOP_Z - 0.045), (0.61 * S, 0.58 * S, 0.09), (0.12, 0.12, 0.14))
add_ref_box("REF_Floor", (0.0, -0.2, FLOOR_Z - 0.02), (1.4, 1.4, 0.04), (0.20, 0.24, 0.30))
add_ref_cyl("REF_ThighPad_L", (0.176 * S, PAD_FWD_Y, (1.057 - 0.004) * S), 0.08 * S, 0.22 * S, (0, math.pi / 2, 0), (0.12, 0.12, 0.14))
add_ref_cyl("REF_ThighPad_R", (-0.176 * S, PAD_FWD_Y, (1.057 - 0.004) * S), 0.08 * S, 0.22 * S, (0, math.pi / 2, 0), (0.12, 0.12, 0.14))

bar_tilt_ang = math.atan2(0.135, 0.206)  # 33.24 deg
ref_bar_center = add_ref_cyl("REF_BarCenter", (0.0, BAR_FWD_Y, BAR_TOP_Z + 0.091 * S), 0.020 * S, 0.52 * S, (0, math.pi / 2, 0), (0.85, 0.85, 0.88))
ref_grip_l = add_ref_cyl("REF_GripL", (BAR_GRIP_X, BAR_FWD_Y, BAR_TOP_Z), BAR_RAD, 0.22 * S, (0, math.pi / 2 + bar_tilt_ang, 0), (0.08, 0.08, 0.09))
ref_grip_r = add_ref_cyl("REF_GripR", (-BAR_GRIP_X, BAR_FWD_Y, BAR_TOP_Z), BAR_RAD, 0.22 * S, (0, math.pi / 2 - bar_tilt_ang, 0), (0.08, 0.08, 0.09))

def set_ref_bar_z(z_grip):
    ref_grip_l.location.z = z_grip
    ref_grip_r.location.z = z_grip
    ref_bar_center.location.z = z_grip + 0.091 * S

# ---------- Pose helpers ----------
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='POSE')

def pb(suffix):
    return next(b for b in arm.pose.bones if b.name.endswith(':' + suffix) or b.name == suffix)

# Capture rest pose local directions in world space for fingers & hand
bpy.ops.pose.select_all(action='SELECT')
bpy.ops.pose.transforms_clear()
bpy.context.view_layer.update()

REST_WORLD = {}
for b in arm.pose.bones:
    REST_WORLD[b.name] = (arm.matrix_world @ b.matrix).copy()

def set_world_rot(pbone, target_rot_3x3):
    """Set pose bone rotation so its evaluated world 3x3 matrix equals target_rot_3x3."""
    pbone.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    no_basis_3x3 = (arm.matrix_world @ pbone.matrix).to_3x3()
    delta = no_basis_3x3.inverted() @ target_rot_3x3
    pbone.rotation_mode = 'QUATERNION'
    pbone.rotation_quaternion = delta.to_quaternion()
    bpy.context.view_layer.update()

def set_world_pos_rot(pbone, world_pos, target_rot_3x3):
    pbone.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    no_basis = arm.matrix_world @ pbone.matrix
    delta_mat = no_basis.inverted() @ Matrix.Translation(world_pos) @ target_rot_3x3.to_4x4()
    pbone.location = delta_mat.to_translation()
    pbone.rotation_mode = 'QUATERNION'
    pbone.rotation_quaternion = delta_mat.to_3x3().to_quaternion()
    bpy.context.view_layer.update()

def rot_bone_along(pbone, want_y, hint_z, blend_hint=1.0):
    """Orient pbone so +Y_bone == want_y and +Z_bone tracks hint_z cleanly."""
    y = want_y.normalized()
    z_proj = hint_z - y * hint_z.dot(y)
    if z_proj.length < 1e-5:
        z_proj = Vector((0, -1, 0)) - y * Vector((0, -1, 0)).dot(y)
    z = z_proj.normalized()
    x = y.cross(z).normalized()
    z = x.cross(y).normalized()
    R = Matrix((x, y, z)).transposed()  # columns = x, y, z
    set_world_rot(pbone, R)

def solve_2bone(a_pos, target, pole, l1, l2):
    to_t = target - a_pos
    dist = to_t.length
    min_d = abs(l1 - l2) + 0.005
    max_d = (l1 + l2) - 0.003
    clamped = max(min_d, min(max_d, dist))
    dir_t = to_t.normalized() if dist > 1e-6 else Vector((0, 0, -1))
    cos_a = max(-1.0, min(1.0, (l1 * l1 + clamped * clamped - l2 * l2) / (2.0 * l1 * clamped)))
    sin_a = math.sqrt(max(0.0, 1.0 - cos_a * cos_a))
    bend = (pole - a_pos)
    bend -= dir_t * bend.dot(dir_t)
    if bend.length < 1e-5:
        bend = Vector((1, 0, 0)).cross(dir_t)
    bend.normalize()
    mid = a_pos + dir_t * (l1 * cos_a) + bend * (l1 * sin_a)
    end = a_pos + dir_t * clamped
    return mid, end

# Cache rest hand axes relative to bone's own 3x3 world frame
def hand_rest_info(side):
    hb = pb(side + "Hand")
    mb = pb(side + "HandMiddle1")
    ib = pb(side + "HandIndex1")
    pk = pb(side + "HandPinky1")
    Rw = REST_WORLD[hb.name].to_3x3()
    h_pos = REST_WORLD[hb.name].to_translation()
    fd = (REST_WORLD[mb.name].to_translation() - h_pos).normalized()
    across = (REST_WORLD[ib.name].to_translation() - REST_WORLD[pk.name].to_translation()).normalized()
    palm_out = (fd.cross(across) if side == "Left" else across.cross(fd)).normalized()
    side_idx = across
    ortho_p = palm_out - fd * palm_out.dot(fd)
    ortho_p.normalize()
    lat = fd.cross(ortho_p).normalized()
    # Frame where columns = (lat, fd, ortho_p) mapped into hand bone's local axes
    F_world = Matrix((lat, fd, ortho_p)).transposed()
    local_offset = F_world.inverted() @ Rw
    fb = pb(side + "ForeArm")
    forearm_local_offset = F_world.inverted() @ REST_WORLD[fb.name].to_3x3()
    ub = pb(side + "Arm")
    upperarm_local_offset = F_world.inverted() @ REST_WORLD[ub.name].to_3x3()
    return local_offset, forearm_local_offset, upperarm_local_offset, (REST_WORLD[mb.name].to_translation() - h_pos).length

HAND_INFO = {"Left": hand_rest_info("Left"), "Right": hand_rest_info("Right")}

def curl_fingers(side, wrist_world_pos, target_fd, target_palm, slope_dir):
    """Curl all 5 fingers cleanly around a 0.032m*S handle cylinder whose axis is slope_dir."""
    # Axis perpendicular to cylinder and finger direction:
    for fn in ("Index", "Middle", "Ring", "Pinky"):
        curls = (60.0, 74.0, 58.0)
        for j, deg in enumerate(curls, start=1):
            b = pb(f"{side}Hand{fn}{j}")
            b.matrix_basis = Matrix.Identity(4)
            bpy.context.view_layer.update()
            M0 = (arm.matrix_world @ b.matrix).to_3x3()
            # Curled fingers rotate around -slope_dir (for Left) or +slope_dir (for Right) so tips curl into target_palm!
            # Universal curl axis = finger_y.cross(target_palm)
            f_dir = M0.col[1].normalized()
            curl_axis = f_dir.cross(target_palm).normalized()
            if curl_axis.length < 1e-5:
                curl_axis = slope_dir
            # Cumulative angle ensures smooth arc even without rest alignment ambiguity
            R_rot = Quaternion(curl_axis, math.radians(deg)).to_matrix()
            set_world_rot(b, R_rot @ M0)

    # Thumb curls slightly opposite/across under the bar handle
    thumb_curls = (28.0, 42.0, 38.0)
    for j, deg in enumerate(thumb_curls, start=1):
        b = pb(f"{side}HandThumb{j}")
        b.matrix_basis = Matrix.Identity(4)
        bpy.context.view_layer.update()
        M0 = (arm.matrix_world @ b.matrix).to_3x3()
        f_dir = M0.col[1].normalized()
        curl_axis = f_dir.cross(target_palm).normalized()
        R_rot = Quaternion(curl_axis, math.radians(deg)).to_matrix()
        set_world_rot(b, R_rot @ M0)

def pose_frame(pull_t):
    """Pose entire TrainerHuman body at pull fraction 0.0 (top) .. 1.0 (bottom)."""
    # Clear pose cleanly first
    bpy.ops.pose.select_all(action='SELECT')
    bpy.ops.pose.transforms_clear()
    bpy.context.view_layer.update()

    # Smooth easing inside pose
    p = pull_t * pull_t * (3.0 - 2.0 * pull_t)

    # 1. Hips seated solidly ABOVE Seat & Plates (glutes/thighs rest cleanly on top of Seat)
    # y=+0.08 keeps torso back behind bar; z=0.805 puts thigh undersides right on Seat top (0.702) and clear of Plate top (0.745)
    hips = pb("Hips")
    hips_pos = Vector((0.0, 0.085, 0.805))
    # Hips pitch slightly back (+X rot leans +Z toward +Y) as you pull down
    hips_back_deg = 4.0 + 6.0 * p
    R_hips = Matrix.Rotation(math.radians(-hips_back_deg), 3, 'X')
    # Rotate pelvis so +Z_world stays Up and -Y_world stays Forward (resting matrix has +Y_bone = Up, +Z_bone = Forward(-Y))
    R_hips_base = REST_WORLD[hips.name].to_3x3()
    set_world_pos_rot(hips, hips_pos, R_hips @ R_hips_base)

    # 2. Spine extension leaning slightly BACK (away from bar!) so bar passes cleanly in front of chin/chest
    lean_spine = (4.0 + 8.0 * p) / 3.0  # per spine bone
    for sname in ("Spine", "Spine1", "Spine2"):
        sb = pb(sname)
        M0 = (arm.matrix_world @ sb.matrix).to_3x3()
        # Leaning back in Blender = rotating +Z toward +Y = NEGATIVE rotation around +X
        set_world_rot(sb, Matrix.Rotation(math.radians(-lean_spine), 3, 'X') @ M0)

    # Counter-tilt Neck & Head forward (+X rot) so trainer looks straight forward horizontally
    neck = pb("Neck")
    M_n = (arm.matrix_world @ neck.matrix).to_3x3()
    set_world_rot(neck, Matrix.Rotation(math.radians(lean_spine * 1.4), 3, 'X') @ M_n)
    head = pb("Head")
    M_h = (arm.matrix_world @ head.matrix).to_3x3()
    set_world_rot(head, Matrix.Rotation(math.radians(lean_spine * 1.1), 3, 'X') @ M_h)

    # 3. Seated legs: thighs clear Seat & tuck right under Thigh_Roller pads, shins angle down to floor/rail
    for side, sgn in (("Left", 1.0), ("Right", -1.0)):
        upleg = pb(side + "UpLeg")
        loleg = pb(side + "Leg")
        foot = pb(side + "Foot")
        toe = pb(side + "ToeBase")
        hip_pos = (arm.matrix_world @ upleg.matrix).to_translation()
        l1 = upleg.bone.length * 0.01  # arm.scale = 0.01
        l2 = loleg.bone.length * 0.01

        # Knee slightly outward under ThighPad center (x=+-0.176*S, y=PAD_FWD_Y+0.02, z=PAD_BOT_Z - 0.085)
        knee_target = Vector((sgn * 0.16 * S, PAD_FWD_Y + 0.03, PAD_BOT_Z - 0.085))
        # Ankle angles gently back-down toward the floor/base so shins don't clip Thigh_Post
        ankle_target = Vector((sgn * 0.17 * S, PAD_FWD_Y + 0.10, max(FLOOR_Z + 0.09, knee_target.z - l2 + 0.01)))
        mid_k, end_a = solve_2bone(hip_pos, ankle_target, knee_target + Vector((0, -0.5, 0.3)), l1, l2)

        rot_bone_along(upleg, mid_k - hip_pos, Vector((0, 0, 1)))
        k_pos = (arm.matrix_world @ loleg.matrix).to_translation()
        rot_bone_along(loleg, end_a - k_pos, Vector((0, -1, 0)))

        # Foot parallel to floor facing forward (-Y)
        R_foot_rest = REST_WORLD[foot.name].to_3x3()
        foot_fwd = Vector((0.0, -0.92, -0.39)).normalized()
        foot_up = Vector((0.0, -0.39, 0.92)).normalized()
        # Keep foot horizontal (-Y forward, +Z up)
        rot_bone_along(foot, Vector((0.0, -0.92, -0.39)).normalized(), Vector((0, 0, 1)))
        rot_bone_along(toe, Vector((0.0, -1.0, 0.0)), Vector((0, 0, 1)))

    # 4. Clavicles, Arms, Pronated Forearms, Overhand Palms & Curled Fingers gripping Bar_Grip_L/R
    z_grip = BAR_TOP_Z + (BAR_BOT_Z - BAR_TOP_Z) * p
    set_ref_bar_z(z_grip)

    for side, sgn in (("Left", 1.0), ("Right", -1.0)):
        clav = pb(side + "Shoulder")
        uparm = pb(side + "Arm")
        loarm = pb(side + "ForeArm")
        hand = pb(side + "Hand")

        # Gentle natural clavicle elevation at top (+12 deg), depression at bottom (-8 deg)
        clav_base = (arm.matrix_world @ clav.matrix).to_3x3()
        clav_elev = 12.0 - 20.0 * p
        clav_rot = Matrix.Rotation(math.radians(sgn * clav_elev), 3, 'Y')
        set_world_rot(clav, clav_rot @ clav_base)

        sh_pos = (arm.matrix_world @ uparm.matrix).to_translation()
        l1 = uparm.bone.length * 0.01
        l2 = loarm.bone.length * 0.01

        # Exact bar foam cylinder center & outward-downward axis
        grip_c = Vector((sgn * BAR_GRIP_X, BAR_FWD_Y, z_grip))
        slope_dir = BAR_SLOPE_L if side == "Left" else BAR_SLOPE_R
        # Grip forward direction perpendicular to bar slope (pointing toward front of machine -Y, dipping over front of bar)
        horiz_fwd = Vector((0.0, -1.0, 0.0))
        bar_perp_fwd = (horiz_fwd - slope_dir * horiz_fwd.dot(slope_dir)).normalized()
        bar_perp_up = bar_perp_fwd.cross(slope_dir * sgn).normalized()
        if bar_perp_up.z < 0:
            bar_perp_up = -bar_perp_up

        # Overhand wrap: palm center sits on top-back of cylinder, fingers curve forward-down around cylinder front!
        # Hand forward direction (wrist -> knuckle): tilted ~32 deg down-forward over the top of the foam
        target_fd = (bar_perp_fwd * 0.85 - bar_perp_up * 0.53).normalized()
        # Outward palm normal points DOWN-BACK against cylinder surface
        target_palm = (-bar_perp_up * 0.85 - bar_perp_fwd * 0.53).normalized()
        # Re-orthogonalize cleanly
        lat_axis = target_fd.cross(target_palm).normalized()
        target_palm = lat_axis.cross(target_fd).normalized()

        # Place wrist so middle of palm (0.044m along target_fd) wraps snugly onto cylinder surface (BAR_RAD + 0.003m)!
        palm_half_len = 0.044
        surf_offset = BAR_RAD + 0.003
        wrist_target = grip_c - target_fd * palm_half_len - target_palm * surf_offset

        # Natural elbow pole: out to the side and down as bar descends, keeping elbows tucked near rib line at bottom
        elbow_pole = sh_pos + Vector((sgn * (0.45 - 0.12 * p), -0.10 - 0.06 * p, 0.05 - 0.45 * p))
        elbow_pos, wrist_solved = solve_2bone(sh_pos, wrist_target, elbow_pole, l1, l2)

        # Orient UpperArm & ForeArm sharing the same lateral bend plane so elbow has ZERO twist pinch!
        F_target = Matrix((lat_axis, target_fd, target_palm)).transposed()
        local_off, forearm_off, upperarm_off, _ = HAND_INFO[side]
        arm_dir = (elbow_pos - sh_pos).normalized()
        fa_dir = (wrist_solved - elbow_pos).normalized()
        bend_n = arm_dir.cross(fa_dir)
        # In Mixamo rest pose, Arm bend axis is rest ortho_p (+Y/+Z); keep arm_lat aligned with lat_axis
        arm_lat = (lat_axis - arm_dir * lat_axis.dot(arm_dir))
        if arm_lat.length < 1e-4:
            arm_lat = Vector((0, 0, 1)) if side == "Left" else Vector((0, 0, -1))
        arm_lat.normalize()
        arm_palm = arm_lat.cross(arm_dir).normalized()
        arm_lat = arm_dir.cross(arm_palm).normalized()
        set_world_rot(uparm, Matrix((arm_lat, arm_dir, arm_palm)).transposed() @ upperarm_off)

        el_actual = (arm.matrix_world @ loarm.matrix).to_translation()
        fa_dir = (wrist_solved - el_actual).normalized()
        fa_lat = (lat_axis - fa_dir * lat_axis.dot(fa_dir))
        if fa_lat.length < 1e-4:
            fa_lat = arm_lat
        fa_lat.normalize()
        fa_palm = fa_lat.cross(fa_dir).normalized()
        fa_lat = fa_dir.cross(fa_palm).normalized()
        set_world_rot(loarm, Matrix((fa_lat, fa_dir, fa_palm)).transposed() @ forearm_off)

        # Snap Wrist/Hand frame directly to bar cylinder frame!
        R_hand = F_target @ local_off
        set_world_rot(hand, R_hand)

        # Curl all 5 fingers smoothly around the cylinder
        curl_fingers(side, wrist_solved, target_fd, target_palm, slope_dir)

# ---------- Keyframe entire 5.0-second cycle (150 frames @ 30fps) matching LatPulldown_Pull ----------
scene.frame_start = 0
scene.frame_end = 150
scene.render.fps = 30

action = bpy.data.actions.new("LatPulldown_Demo")
arm.animation_data_create()
arm.animation_data.action = action

# Sample 9 keyframes across cycle: 0 (top) -> 75 (bottom) -> 150 (top)
SAMPLES = [
    (0,   0.00),
    (18,  0.14),
    (38,  0.50),
    (57,  0.86),
    (75,  1.00),
    (93,  0.86),
    (112, 0.50),
    (132, 0.14),
    (150, 0.00),
]

BONES_TO_KEY = [b.name for b in arm.pose.bones]
for frame, pt in SAMPLES:
    scene.frame_set(frame)
    pose_frame(pt)
    for bname in BONES_TO_KEY:
        b = arm.pose.bones[bname]
        if bname.endswith("Hips"):
            b.keyframe_insert(data_path="location", frame=frame)
        b.keyframe_insert(data_path="rotation_quaternion", frame=frame)

# Smooth bezier interpolation (in Blender 5.2 actions can store fcurves in layered strips/slots)
fcurves = getattr(action, "fcurves", None)
if fcurves is None and hasattr(action, "layers"):
    fcurves = []
    for lay in action.layers:
        for st in lay.strips:
            for ch in st.channelbags:
                fcurves.extend(ch.fcurves)
if fcurves:
    for fc in fcurves:
        for kp in fc.keyframe_points:
            kp.interpolation = 'BEZIER'
            kp.handle_left_type = 'AUTO_CLAMPED'
            kp.handle_right_type = 'AUTO_CLAMPED'

# ---------- Render Blender visual verification shots if PREVIEW_DIR is set ----------
if PREVIEW_DIR:
    os.makedirs(PREVIEW_DIR, exist_ok=True)
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.color_type = 'MATERIAL'
    scene.display.shading.light = 'STUDIO'
    scene.render.resolution_x, scene.render.resolution_y = 900, 720
    cam_d = bpy.data.cameras.new("p_cam")
    cam = bpy.data.objects.new("p_cam", cam_d)
    col.objects.link(cam)
    scene.camera = cam

    def aim(loc, target):
        cam.location = Vector(loc)
        direction = Vector(target) - Vector(loc)
        cam.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()

    def snap(fname, frame, pt, loc, target):
        scene.frame_set(frame)
        set_ref_bar_z(BAR_TOP_Z + (BAR_BOT_Z - BAR_TOP_Z) * (pt * pt * (3.0 - 2.0 * pt)))
        bpy.context.view_layer.update()
        aim(loc, target)
        scene.render.filepath = os.path.join(PREVIEW_DIR, fname)
        bpy.ops.render.render(write_still=True)

    # 3/4 side view matching user's uploaded angle (shows full body from shoes up to overhead bar!)
    snap("demo_3q_top.png", 0, 0.0, (-1.95, -2.15, 1.32), (0.0, -0.05, 0.96))
    snap("demo_3q_bot.png", 75, 1.0, (-1.95, -2.15, 1.32), (0.0, -0.05, 0.96))
    # Close-up of Right & Left hands gripping the angled black foam cylinder at bottom
    snap("demo_hand_r.png", 75, 1.0, (-0.58, -0.46, 1.28), (-BAR_GRIP_X, BAR_FWD_Y, BAR_BOT_Z))
    snap("demo_hand_l.png", 75, 1.0, (0.58, -0.46, 1.28), (BAR_GRIP_X, BAR_FWD_Y, BAR_BOT_Z))
    bpy.data.objects.remove(cam)
    bpy.data.cameras.remove(cam_d)

# Clean up reference objects before saving & exporting FBX (keep them out of Unity!)
for ob in list(ref_col.objects):
    bpy.data.objects.remove(ob, do_unlink=True)
bpy.data.collections.remove(ref_col)

bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)

for ob in bpy.context.view_layer.objects:
    ob.select_set(False)
for ob in bpy.data.objects:
    if ob.type in {'ARMATURE', 'MESH'}:
        ob.select_set(True)
bpy.context.view_layer.objects.active = arm

bpy.ops.export_scene.fbx(
    filepath=OUT_FBX,
    use_selection=True,
    object_types={'ARMATURE', 'MESH'},
    add_leaf_bones=False,
    bake_anim=True,
    bake_anim_use_all_actions=False,
    bake_anim_use_nla_strips=False,
    bake_anim_force_startend_keying=True,
    bake_anim_simplify_factor=0.0,
    apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z',
    axis_up='Y',
    path_mode='COPY',
    embed_textures=True,
)
print("EXPORTED DEMO FBX:", OUT_FBX, os.path.getsize(OUT_FBX))
