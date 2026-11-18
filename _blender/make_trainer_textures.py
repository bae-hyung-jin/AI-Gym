"""Create clean, youthful, friendly trainer face & clean hair alpha textures from Ch38 originals."""
import os
import numpy as np
from PIL import Image, ImageFilter

TEX_DIR = r"G:\Unity_Gym\AI-Gym\Assets\Characters\Textures"
src_body = os.path.join(TEX_DIR, "Ch38_1001_Diffuse.png")
dst_body = os.path.join(TEX_DIR, "Trainer_Diffuse.png")
src_hair = os.path.join(TEX_DIR, "Ch38_1002_Diffuse.png")
dst_hair = os.path.join(TEX_DIR, "Trainer_Hair_Diffuse.png")

# 1. Hair texture: keep exact hair strand alpha, replace white background bleed with dark espresso RGB so zero white halo appears
h_img = Image.open(src_hair).convert("RGBA")
h_arr = np.array(h_img)
h_arr[:, :, 0] = np.clip(h_arr[:, :, 0] * 0.15 + 26, 0, 255).astype(np.uint8)
h_arr[:, :, 1] = np.clip(h_arr[:, :, 1] * 0.14 + 22, 0, 255).astype(np.uint8)
h_arr[:, :, 2] = np.clip(h_arr[:, :, 2] * 0.13 + 20, 0, 255).astype(np.uint8)
Image.fromarray(h_arr, "RGBA").save(dst_hair)
print("Saved", dst_hair)

# 2. Body/face texture: clean stubble/blotches smoothly while preserving eyes, lips, nostrils, ears, hands
b_img = Image.open(src_body).convert("RGBA")
W, H = b_img.size  # 2048x2048
arr = np.array(b_img, dtype=np.float32)

# Blurred copy for subtle stubble/skin blemish smoothing
smooth_arr = np.array(b_img.filter(ImageFilter.GaussianBlur(radius=14)), dtype=np.float32)

# Clean peach skin target tone sampled from Ch38 forehead/upper cheek area
# Apply gentle smoothing ONLY on lower face/jaw/neck stubble + under-eye dark tone in top-right face island:
# Face region in Ch38_1001_Diffuse (2048x2048): x in [1024..2048], y in [0..600]
yy, xx = np.mgrid[0:H, 0:W]

# Jaw / beard stubble area (below nose/cheeks, keeping mouth/lips centered around x=1536, y=380..440 untouched!)
lip_dist = np.sqrt(((xx - 1536) / 65.0) ** 2 + ((yy - 405) / 36.0) ** 2)
eye_l = np.sqrt(((xx - 1445) / 48.0) ** 2 + ((yy - 275) / 38.0) ** 2)
eye_r = np.sqrt(((xx - 1625) / 48.0) ** 2 + ((yy - 275) / 38.0) ** 2)
nose_d = np.sqrt(((xx - 1536) / 45.0) ** 2 + ((yy - 335) / 42.0) ** 2)
feature_keep = np.clip(np.minimum(np.minimum(lip_dist, nose_d), np.minimum(eye_l, eye_r)) - 0.85, 0.0, 1.0)

# Cheek & jawline mask: warm peach smooth skin over dark beard stubble
jaw_mask = (
    (xx > 1180) & (xx < 1890) & (yy > 280) & (yy < 610)
).astype(np.float32)
# Detect skin pixels (not pitch black hair background)
is_skin = (arr[:, :, 0] > 70) & ((arr[:, :, 0] - arr[:, :, 2]) > 8)
blend = jaw_mask * feature_keep * is_skin * 0.88

# Compute clean cheek reference color (average around upper cheeks)
cheek_sample = arr[280:330, 1340:1410, :3].mean(axis=(0, 1))
target_skin = np.clip(cheek_sample * np.array([1.05, 1.03, 1.01]), 0, 255)

# Blend stubble toward smooth + clean warm skin color
clean_patch = smooth_arr[:, :, :3] * 0.55 + target_skin * 0.45
arr[:, :, :3] = arr[:, :, :3] * (1.0 - blend[:, :, None]) + clean_patch * blend[:, :, None]

# Gentle overall warm healthy skin lift on all skin/hand regions (keep clothes/cleats unaffected or clean)
skin_all = (arr[:, :, 0] > 95) & ((arr[:, :, 0] - arr[:, :, 2]) > 12) & (yy < 950) & (xx > 980)
arr[skin_all, 0] = np.clip(arr[skin_all, 0] * 1.06 + 6, 0, 255)
arr[skin_all, 1] = np.clip(arr[skin_all, 1] * 1.05 + 4, 0, 255)
arr[skin_all, 2] = np.clip(arr[skin_all, 2] * 1.03 + 2, 0, 255)

Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA").save(dst_body)
print("Saved", dst_body)
