#!/usr/bin/env python3
"""Fix step N: skin colour painted on the shirt collar (part 19).

usage: python3 05_fix_collar.py <orig.glb> <prev_step.npz> <out_dir>

Part 19 is the black turtleneck/shirt collar (fused with the strap/buckle). Its
top rim, right under the jaw, received skin / muddy brown / whitish colour from
the frontal texture projection (the jaw and neck are directly behind it), which
reads as a dirty, jagged neck line in front and 3/4 views. The collar cloth is
very dark everywhere else (L* 8-13), so every texel of the collar top
(glTF y > 0.822, i.e. above the strap and buckle) that is lighter than L* 20 is
re-coloured with the 3D nearest-neighbour mean of the clean dark collar texels
(L* < 18). The neck/jaw skin itself lives in part 14 and is not touched; the
buckle and strap (y < 0.81) are excluded. Output: out_dir/collar_step.npz
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from uvmaps import load_parts  # noqa: E402
from fixlib import knn_fill, lab_img  # noqa: E402

PART = 19


def main():
    glb, prev, out = sys.argv[1:4]
    os.makedirs(out, exist_ok=True)
    P0 = np.load(prev)
    js, binc, parts = load_parts(glb, [PART])
    p = parts[PART]
    m = p.maps()
    rgb = P0[f"rgb_{PART}"].copy() if f"rgb_{PART}" in P0.files else p.rgb.copy()
    L = lab_img(rgb)
    y = m["pos"][..., 1]
    target = m["cons"] & (y > 0.822) & (L[..., 0] > 20)
    source = m["cov"] & ~target & (y > 0.80) & (L[..., 0] < 18) & (np.abs(L[..., 1]) < 4) & (np.abs(L[..., 2]) < 4)
    col = knn_fill(m["pos"][source], rgb[source].astype(np.float32), m["nrm"][source],
                   m["pos"][target], m["nrm"][target])
    rgb[target] = np.clip(np.rint(col), 0, 255).astype(np.uint8)
    print(f"part {PART}: collar texels re-coloured: {int(target.sum())}")
    save = {k: P0[k] for k in P0.files}
    save[f"rgb_{PART}"] = rgb
    save[f"collar_target_{PART}"] = target
    np.savez_compressed(os.path.join(out, "collar_step.npz"), **save)


if __name__ == "__main__":
    main()
