#!/usr/bin/env python3
"""Fix step F: face / eye texture artifacts, edited through a fixed frontal
orthographic projection so that texels of different parts/UV islands that meet
on the face (parts 8, 14, 21) receive CONSISTENT colours across their seams.

usage: python3 04_fix_face.py <orig.glb> <analysis_dir> <prev_step.npz> <proj_dir> <out_dir>
  proj_dir holds proj_front_aux.exr (per-pixel part + UV lookup) rendered with
  render_views.py --mode aux and cameras_proj.json (camera "proj_front"). It only
  depends on geometry/UVs, so it is rendered from the original GLB.

Operations (all in proj_front pixel coordinates, 2048x2048, ortho 0.11 units):
  E1  right eye (character's right, image left): the painted eye is split over
      part 8 (upper half, fused into the hair part) and part 14 (lower half)
      with non-matching content across the seams (iris cut by a skin wedge,
      smeared eye-white, broken lash line). It is replaced, inside a feathered
      eye-shaped mask, by the character's own LEFT eye mirrored about the face
      mid-line (x=1020.5 px) and aligned on the iris (dy=+4 px). Colours are
      read straight from the left-eye texels (no re-render blur). Iris colour,
      size and shape therefore stay the owner's design.
  S1  seam levelling in texture space: border texels of the face-shell UV
      islands (parts 8/14/21, not hair locks, face region only) are paired with
      the nearest border texel of the neighbouring island in 3D. Where the two
      colours disagree (CIELAB dE > 10) both are pulled to their mean and the
      correction fades out over 2 texels into each island. Matching features
      that cross a seam (lash lines, brows) are left alone; only the hard
      "shard" steps between islands whose paint does not line up are softened.
Only head-shell texels (not hair-lock geometry, see 02_analyze.py) that face
the camera are written. Output: out_dir/face_step.npz (edited RGB + per-operation
texel masks) and out_dir/face_step_log.json (texel counts).
"""
import json
import os
import sys

import cv2
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from uvmaps import load_parts  # noqa: E402
from exr_util import read_aux  # noqa: E402

FACE_PARTS = [8, 14, 21]
CX, CY, SCALE, RES = -0.0025, 0.8825, 0.11, 2048   # must match cameras_proj.json

# ---- E1 parameters -------------------------------------------------------
MIRROR_X = 1020.5        # face mid-line in proj px (mean of both iris centroids)
MIRROR_DY = 4.0          # right iris sits 4 px lower than the left one
EYE_R_CENTER = (626.0, 924.0)
EYE_R_AXES = (205.0, 88.0)   # ellipse covering lashes .. lower lid
EYE_FEATHER = (0.80, 1.08)   # alpha 1 inside 0.80*ellipse, 0 outside 1.08*ellipse


def project(P):
    px = (P[..., 0] - (CX - SCALE / 2)) / SCALE * RES - 0.5
    py = ((CY + SCALE / 2) - P[..., 1]) / SCALE * RES - 0.5
    return px, py


def bilinear(img, x, y):
    H, W = img.shape[:2]
    x = np.clip(x, 0, W - 1.001)
    y = np.clip(y, 0, H - 1.001)
    x0 = np.floor(x).astype(int)
    y0 = np.floor(y).astype(int)
    fx = (x - x0)[:, None]
    fy = (y - y0)[:, None]
    a = img[y0, x0].astype(np.float32)
    b = img[y0, x0 + 1].astype(np.float32)
    c = img[y0 + 1, x0].astype(np.float32)
    d = img[y0 + 1, x0 + 1].astype(np.float32)
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def ellipse_alpha(px, py, center, axes, feather):
    r = np.sqrt(((px - center[0]) / axes[0]) ** 2 + ((py - center[1]) / axes[1]) ** 2)
    a = (feather[1] - r) / (feather[1] - feather[0])
    return np.clip(a, 0, 1)


def smoothstep(a):
    return a * a * (3 - 2 * a)


SEAM_REGION_Y = (0.835, 0.935)   # forehead .. chin (glTF y)
SEAM_REGION_ZMIN = 0.03          # front half of the head only
SEAM_DE = 10.0
SEAM_FADE = 2


def seam_level(parts, cur, A):
    from scipy.spatial import cKDTree
    recs = []   # (part, y, x, island_gid, pos)
    border = {}
    for no, p in parts.items():
        m = p.maps()
        core = m["cov"]
        isl = m["isl"]
        nb = np.zeros_like(core)
        # a core texel is on the border if a 4-neighbour is not core of the same island
        for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
            sh_core = np.roll(core, (dy, dx), (0, 1))
            sh_isl = np.roll(isl, (dy, dx), (0, 1))
            nb |= core & (~sh_core | (sh_isl != isl))
        pos = m["pos"]
        ok = nb & ~A[f"lock_{no}"] & (pos[..., 1] > SEAM_REGION_Y[0]) & (pos[..., 1] < SEAM_REGION_Y[1]) \
            & (pos[..., 2] > SEAM_REGION_ZMIN)
        border[no] = ok
        ys, xs = np.nonzero(ok)
        for y, x in zip(ys, xs):
            recs.append((no, y, x, no * 1000 + isl[y, x]))
    if not recs:
        return {}
    rp = np.array([parts[r[0]].maps()["pos"][r[1], r[2]] for r in recs])
    gid = np.array([r[3] for r in recs])
    tree = cKDTree(rp)
    # texel size in 3D (median distance to the 4th nearest border texel of the same island)
    d4, _ = tree.query(rp, k=5)
    tex3d = float(np.median(d4[:, 1]))
    pairs = tree.query_ball_point(rp, r=1.5 * tex3d)
    col = np.array([cur[r[0]][r[1], r[2]] for r in recs]).astype(np.float32)
    lab = cv2.cvtColor(col.reshape(-1, 1, 3).astype(np.uint8), cv2.COLOR_RGB2LAB).reshape(-1, 3).astype(np.float32)
    lab[:, 0] *= 100 / 255.0
    lab[:, 1:] -= 128
    delta = np.zeros_like(col)
    wsum = np.zeros(len(recs), np.float32)
    for i, js_ in enumerate(pairs):
        other = [j for j in js_ if gid[j] != gid[i]]
        if not other:
            continue
        dist = np.linalg.norm(rp[other] - rp[i], axis=1)
        j = other[int(np.argmin(dist))]
        de = float(np.linalg.norm(lab[i] - lab[j]))
        if de <= SEAM_DE:
            continue
        mean = (col[i] + col[j]) / 2
        delta[i] += mean - col[i]
        wsum[i] += 1
    changed = {}
    for no, p in parts.items():
        m = p.maps()
        dfield = np.zeros((p.H, p.W, 3), np.float32)
        have = np.zeros((p.H, p.W), bool)
        for k, r in enumerate(recs):
            if r[0] == no and wsum[k] > 0:
                dfield[r[1], r[2]] = delta[k] / wsum[k]
                have[r[1], r[2]] = True
        if not have.any():
            changed[no] = 0
            continue
        # fade the correction into the island interior (same island only)
        acc = dfield.copy()
        w = have.astype(np.float32)
        cover = have.copy()
        frontier = have.copy()
        for step in range(1, SEAM_FADE + 1):
            grow = cv2.dilate(frontier.astype(np.uint8), np.ones((3, 3), np.uint8)).astype(bool) & m["cov"] & ~cover
            if not grow.any():
                break
            # value = mean of already-set 8-neighbours in the same island, scaled
            num = cv2.blur(acc * cover[..., None], (3, 3))
            den = cv2.blur(cover.astype(np.float32), (3, 3))[..., None]
            val = np.where(den > 0, num / np.maximum(den, 1e-6), 0) * (1 - step / (SEAM_FADE + 1))
            acc[grow] = val[grow]
            cover |= grow
            frontier = grow
        img = cur[no].astype(np.float32) + acc
        new = np.clip(np.rint(img), 0, 255).astype(np.uint8)
        changed[no] = int(np.any(new != cur[no], -1).sum())
        cur[no] = new
    return changed


# (log key, ellipse centre (px), semi-axes (px), feather (inner, outer), max alpha, dy)
MIRROR_PATCHES = [
    ("E1_right_eye_from_mirrored_left_eye", EYE_R_CENTER, EYE_R_AXES, EYE_FEATHER, 1.0, MIRROR_DY),
    # Tried and REVERTED (no visible improvement, see REPORT.md): mirrored patches
    # for the right mouth corner (centre (1165,1548)) and the right nostril wedge
    # (centre (1068,1362)). The defect there is an island boundary that cuts the
    # lip/nostril shading at 256 px texture resolution; a mirrored copy did not
    # remove the step in perspective close-ups, so the owner's original paint stays.
]


def mirror_patch(parts, cur, src, A, u, v, apart, center, axes, feather, strength, dy, masks):
    """Write mirrored (about MIRROR_X, shifted by MIRROR_DY) colours into the
    shell texels whose frontal projection falls in the feathered ellipse."""
    edits = {}
    for no, p in parts.items():
        m = p.maps()
        lock = A[f"lock_{no}"]
        cand = m["cons"] & ~lock & (m["nrm"][..., 2] > 0.1)
        ys, xs = np.nonzero(cand)
        px, py = project(m["pos"][ys, xs])
        al = smoothstep(ellipse_alpha(px, py, center, axes, feather)) * strength
        sel = al > 0
        ys, xs, px, py, al = ys[sel], xs[sel], px[sel], py[sel], al[sel]
        mx = 2 * MIRROR_X - px
        my = py - dy
        ix = np.clip(np.rint(mx).astype(int), 0, RES - 1)
        iy = np.clip(np.rint(my).astype(int), 0, RES - 1)
        sp = apart[iy, ix]
        colour = np.zeros((len(ys), 3), np.float32)
        ok = np.zeros(len(ys), bool)
        for sno in FACE_PARTS:
            s = sp == sno
            if not s.any():
                continue
            q = parts[sno]
            tx = u[iy[s], ix[s]] * q.W - 0.5
            ty = (1 - v[iy[s], ix[s]]) * q.H - 0.5
            lk = A[f"lock_{sno}"][np.clip(np.rint(ty).astype(int), 0, q.H - 1),
                                  np.clip(np.rint(tx).astype(int), 0, q.W - 1)]
            colour[s] = bilinear(src[sno], tx, ty)
            ok[np.nonzero(s)[0][~lk]] = True
        ys, xs, al, colour = ys[ok], xs[ok], al[ok], colour[ok]
        new = cur[no][ys, xs].astype(np.float32) * (1 - al[:, None]) + colour * al[:, None]
        cur[no][ys, xs] = np.clip(np.rint(new), 0, 255).astype(np.uint8)
        edits[no] = int((al > 0.01).sum())
        mk = masks.setdefault(no, np.zeros((p.H, p.W), bool))
        mk[ys[al > 0.01], xs[al > 0.01]] = True
    return edits


def main():
    glb, adir, prev, pdir, out = sys.argv[1:6]
    os.makedirs(out, exist_ok=True)
    A = np.load(os.path.join(adir, "analysis.npz"))
    P0 = np.load(prev)
    js, binc, parts = load_parts(glb, FACE_PARTS)
    cur = {no: (P0[f"rgb_{no}"].copy() if f"rgb_{no}" in P0.files else parts[no].rgb.copy()) for no in FACE_PARTS}
    src = {no: cur[no].copy() for no in FACE_PARTS}     # read-only source for mirroring
    u, v, apart = read_aux(os.path.join(pdir, "proj_front_aux.exr"))
    log = {}

    # ---------------------------------------------------------------- E1 / M1 / N1
    eye_masks = {}
    for key, center, axes, feather, strength, dy in MIRROR_PATCHES:
        edits = mirror_patch(parts, cur, src, A, u, v, apart, center, axes, feather, strength, dy, eye_masks)
        log[key] = edits
        print(key, "texels written:", edits)

    # ---------------------------------------------------------------- S1
    before_s1 = {no: cur[no].copy() for no in FACE_PARTS}
    log["S1_seam_level_texels"] = seam_level(parts, cur, A)
    print("S1 seam levelling texels changed:", log["S1_seam_level_texels"])

    save = {f"rgb_{no}": cur[no] for no in FACE_PARTS}
    for no in FACE_PARTS:
        save[f"mask_eye_{no}"] = eye_masks.get(no, np.zeros(cur[no].shape[:2], bool))
        save[f"mask_seam_{no}"] = np.any(cur[no] != before_s1[no], -1)
    # carry over untouched parts of the previous step
    for k in P0.files:
        if k not in save:
            save[k] = P0[k]
    np.savez_compressed(os.path.join(out, "face_step.npz"), **save)
    json.dump(log, open(os.path.join(out, "face_step_log.json"), "w"), indent=1)


if __name__ == "__main__":
    main()
