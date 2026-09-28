#!/usr/bin/env python3
"""Before/after metric: skin-coloured screen pixels that lie on hair-lock
geometry (per 02_analyze.py lock mask), counted in the unlit renders of every
face view.

usage: python3 12_metrics.py <orig.glb> <analysis_dir> <renders_before> <renders_after> <out.json>
"""
import json
import os
import sys

import cv2
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from exr_util import read_aux  # noqa: E402
from uvmaps import load_parts  # noqa: E402
from fixlib import lab_img  # noqa: E402

VIEWS = ["face_front", "face_34L", "face_34R", "face_profL", "face_profR", "face_high", "face_low",
         "eyes_both", "hairline_close", "ear_L_close", "ear_R_close"]
HAIR = [8, 10, 21, 24, 28]


def count(render_dir, view, parts, A):
    u, v, part = read_aux(os.path.join(render_dir, f"{view}_aux.exr"))
    img = cv2.imread(os.path.join(render_dir, f"{view}_unlit.png"))[..., ::-1].copy()
    L = lab_img(img)
    skin = (L[..., 0] > 30) & (L[..., 1] > 2.5) & (L[..., 2] > 2.5)
    lock = np.zeros(part.shape, bool)
    for no in HAIR:
        p = parts[no]
        m = part == no
        if not m.any():
            continue
        x = np.clip((u[m] * p.W).astype(int), 0, p.W - 1)
        y = np.clip(((1 - v[m]) * p.H).astype(int), 0, p.H - 1)
        idx = np.nonzero(m)
        lock[idx[0], idx[1]] = A[f"lock_{no}"][y, x]
    return int((skin & lock).sum()), int(lock.sum())


def main():
    glb, adir, rb, ra, out = sys.argv[1:6]
    A = np.load(os.path.join(adir, "analysis.npz"))
    _, _, parts = load_parts(glb, HAIR)
    res = {}
    for v in VIEWS:
        b, nb = count(rb, v, parts, A)
        a, na = count(ra, v, parts, A)
        res[v] = {"skin_px_on_hair_locks_before": b, "after": a, "lock_px": nb}
        print(f"{v:15s} skin-coloured px on hair locks: before {b:7d}  after {a:7d}  (lock px {nb})")
    json.dump(res, open(out, "w"), indent=1)


if __name__ == "__main__":
    main()
