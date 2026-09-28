#!/usr/bin/env python3
"""Diagnostic images that explain the model structure / problem classes.

usage: python3 10_diagnostics.py <orig.glb> <renders_before> <analysis_dir> <hair_step.npz> <work_dir> <out_dir>
Writes into out_dir:
  diag_part_ids.png              which Tripo part is where (head, 4 views)
  diag_face_without_hair.png     parts 8/10/24/28 hidden: part 14 has a hole where the
                                 upper half of the right eye is -> that eye is painted on part 8
  diag_seams_eyes.png            UV-island seams (magenta) / part borders (yellow) over the eyes
  diag_skin_on_hair_classes.png  red = skin paint on hair-lock geometry (recoloured),
                                 green = skin on the head shell (real skin, kept)
"""
import io
import os
import subprocess
import sys

import cv2
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from exr_util import read_aux  # noqa: E402
from uvmaps import load_parts  # noqa: E402
from glb_write import write_glb  # noqa: E402

PNG = [cv2.IMWRITE_PNG_COMPRESSION, 9]


def blender(glb, out, mode, views, extra=()):
    cmd = ["blender", "-b", "--python", os.path.join(HERE, "render_views.py"), "--", "--glb", glb,
           "--cams", os.path.join(HERE, "cameras.json"), "--out", out, "--mode", mode, "--views", ",".join(views)]
    subprocess.run(cmd + list(extra), check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)


def title(img, text):
    bar = np.full((40, img.shape[1], 3), 30, np.uint8)
    cv2.putText(bar, text, (10, 28), cv2.FONT_HERSHEY_SIMPLEX, 0.8, (255, 255, 255), 2, cv2.LINE_AA)
    return np.vstack([bar, img])


def main():
    glb, rb, adir, hair_npz, work, out = sys.argv[1:7]
    os.makedirs(out, exist_ok=True)
    os.makedirs(work, exist_ok=True)
    rng = np.random.default_rng(3)
    cols = (rng.random((64, 3)) * 200 + 55).astype(np.uint8)

    # 1 part ids
    tiles = []
    for v in ("face_front", "face_profL", "face_profR", "face_top"):
        u, vv, part = read_aux(os.path.join(rb, f"{v}_aux.exr"))
        img = np.full(part.shape + (3,), 90, np.uint8)
        m = part >= 0
        img[m] = cols[part[m]]
        for p in np.unique(part[m]):
            ys, xs = np.nonzero(part == p)
            if len(ys) < 1500:
                continue
            cy, cx = int(np.median(ys)), int(np.median(xs))
            cv2.putText(img, str(p), (cx - 12, cy + 10), cv2.FONT_HERSHEY_SIMPLEX, 1.1, (0, 0, 0), 5)
            cv2.putText(img, str(p), (cx - 12, cy + 10), cv2.FONT_HERSHEY_SIMPLEX, 1.1, (255, 255, 255), 2)
        tiles.append(title(cv2.resize(img, (560, 560), interpolation=cv2.INTER_NEAREST), v))
    cv2.imwrite(os.path.join(out, "diag_part_ids.png"), np.hstack(tiles), PNG)

    # 2 face without the hair parts
    blender(glb, work, "unlit", ["eyes_both", "face_front"], ["--hide", "8,10,24,28", "--suffix", "_nohair"])
    a = cv2.imread(os.path.join(rb, "eyes_both_unlit.png"))
    b = cv2.imread(os.path.join(work, "eyes_both_unlit_nohair.png"))
    cv2.imwrite(os.path.join(out, "diag_face_without_hair.png"),
                np.hstack([title(a, "original (all parts)"), title(b, "hair parts 8/10/24/28 hidden")]), PNG)

    # 3 seams over the eyes
    _, _, parts = load_parts(glb, [8, 10, 11, 14, 18, 19, 21, 24, 28])
    u, vv, part = read_aux(os.path.join(rb, "eyes_both_aux.exr"))
    gid = np.full(part.shape, -1, np.int64)
    for no, p in parts.items():
        m = part == no
        x = np.clip((u[m] * p.W).astype(int), 0, p.W - 1)
        y = np.clip(((1 - vv[m]) * p.H).astype(int), 0, p.H - 1)
        gid[m] = no * 1000 + np.maximum(p.maps()["isl"][y, x], 0)
    img = cv2.imread(os.path.join(rb, "eyes_both_unlit.png"))
    bi = np.zeros(part.shape, bool)
    bp = np.zeros(part.shape, bool)
    bi[:, 1:] |= gid[:, 1:] != gid[:, :-1]
    bi[1:, :] |= gid[1:, :] != gid[:-1, :]
    bp[:, 1:] |= part[:, 1:] != part[:, :-1]
    bp[1:, :] |= part[1:, :] != part[:-1, :]
    o = img.copy()
    o[cv2.dilate(bi.astype(np.uint8), np.ones((2, 2), np.uint8)).astype(bool)] = (255, 0, 255)
    o[cv2.dilate(bp.astype(np.uint8), np.ones((2, 2), np.uint8)).astype(bool)] = (0, 255, 255)
    cv2.imwrite(os.path.join(out, "diag_seams_eyes.png"),
                np.vstack([title(img, "before"), title(o, "magenta = UV island seam, yellow = part border")]), PNG)

    # 4 skin-on-hair classes
    A = np.load(os.path.join(adir, "analysis.npz"))
    Hs = np.load(hair_npz)
    import json
    comps = json.load(open(os.path.join(adir, "components.json")))["components"]
    rep = {}
    for no in (8, 10, 21):
        p = parts[no]
        img = p.rgb.copy()
        shell = np.zeros((p.H, p.W), bool)
        for c in comps:
            if c["part"] == no and c["kind"] == "head_shell":
                shell |= A[f"comp_{no}_{c['island']}_{c['comp']}"]
        img[shell] = (img[shell] * 0.3 + np.array([0, 230, 0]) * 0.7).astype(np.uint8)
        t = Hs[f"target_{no}"]
        img[t] = (img[t] * 0.3 + np.array([255, 0, 0]) * 0.7).astype(np.uint8)
        b = io.BytesIO()
        Image.fromarray(img).save(b, "PNG")
        rep[p.image_index] = b.getvalue()
    dbg = os.path.join(work, "diag_classes.glb")
    write_glb(glb, dbg, replace_images=rep)
    views = ["face_front", "face_34L", "face_34R", "face_high", "face_low", "ear_R_close"]
    blender(dbg, work, "unlit", views, ["--suffix", "_classes"])
    tiles = [title(cv2.resize(cv2.imread(os.path.join(work, f"{v}_unlit_classes.png")), (520, 520),
                              interpolation=cv2.INTER_AREA), v) for v in views]
    cv2.imwrite(os.path.join(out, "diag_skin_on_hair_classes.png"),
                np.vstack([np.hstack(tiles[:3]), np.hstack(tiles[3:])]), PNG)
    print("diagnostics written to", out)


if __name__ == "__main__":
    main()
