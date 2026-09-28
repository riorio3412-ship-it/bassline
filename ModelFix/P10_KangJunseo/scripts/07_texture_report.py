#!/usr/bin/env python3
"""Export original / fixed base-colour textures and UV-space overlays that show
where the problems were and which texels each fix touched.

usage: python3 07_texture_report.py <orig.glb> <fixed.glb> <analysis_dir> <final_step.npz> <textures_dir> [<build_masks.npz>]
Writes
  textures/original/part_XX_basecolor.jpg   byte-exact copies of all 29 embedded images
  textures/fixed/part_XX_basecolor_fixed.jpg the patched images of the edited parts
  textures/uv_overlay_part_XX.png          original | overlay | fixed (edited parts)
"""
import io
import json
import os
import sys

import cv2
import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from uvmaps import load_parts, read_glb  # noqa: E402

LEGEND = [
    ((255, 0, 255), "skin colour on hair lock -> hair (step H)"),
    ((0, 200, 255), "brown strand tint neutralised (step H)"),
    ((0, 255, 0), "skin on head shell = real face skin, kept"),
    ((255, 220, 0), "right eye rebuilt from mirrored left eye (E1)"),
    ((255, 90, 0), "seam levelling across UV islands (S1)"),
    ((255, 60, 60), "skin/brown smear on collar -> collar (N)"),
    ((120, 120, 255), "gutter edge padding (P, outside UV islands)"),
]


def image_bytes(js, binc, idx):
    bv = js["bufferViews"][js["images"][idx]["bufferView"]]
    return binc[bv.get("byteOffset", 0):bv.get("byteOffset", 0) + bv["byteLength"]]


def main():
    orig, fixed, adir, step, tdir = sys.argv[1:6]
    B = np.load(sys.argv[6]) if len(sys.argv) > 6 else None
    os.makedirs(os.path.join(tdir, "original"), exist_ok=True)
    os.makedirs(os.path.join(tdir, "fixed"), exist_ok=True)
    js0, b0 = read_glb(orig)
    js1, b1 = read_glb(fixed)
    for node in js0["nodes"]:
        if "mesh" not in node:
            continue
        no = int(node["name"].split("_")[-1])
        mat = js0["materials"][js0["meshes"][node["mesh"]]["primitives"][0]["material"]]
        idx = js0["textures"][mat["pbrMetallicRoughness"]["baseColorTexture"]["index"]]["source"]
        d0, d1 = image_bytes(js0, b0, idx), image_bytes(js1, b1, idx)
        open(os.path.join(tdir, "original", f"part_{no:02d}_basecolor.jpg"), "wb").write(d0)
        if d0 != d1:
            open(os.path.join(tdir, "fixed", f"part_{no:02d}_basecolor_fixed.jpg"), "wb").write(d1)

    A = np.load(os.path.join(adir, "analysis.npz"))
    comps = json.load(open(os.path.join(adir, "components.json")))["components"]
    S = np.load(step)
    edited = sorted({int(k.split("_")[1]) for k in S.files if k.startswith("rgb_")})
    _, _, parts = load_parts(orig, edited)
    _, _, fparts = load_parts(fixed, edited)
    for no in edited:
        p = parts[no]
        if np.array_equal(fparts[no].rgb, p.rgb):
            continue
        H, W = p.H, p.W
        ov = (p.rgb.astype(np.float32) * 0.45).astype(np.uint8)
        layers = []
        shell = np.zeros((H, W), bool)
        for c in comps:
            if c["part"] == no and c["kind"] == "head_shell":
                shell |= A[f"comp_{no}_{c['island']}_{c['comp']}"]
        layers.append((shell, LEGEND[2][0]))
        if B is not None and f"pad_{no}" in B.files:
            layers.append((B[f"pad_{no}"].astype(bool), LEGEND[6][0]))
        for key, col in ((f"mask_seam_{no}", LEGEND[4][0]), (f"mask_eye_{no}", LEGEND[3][0]),
                         (f"neutral_{no}", LEGEND[1][0]), (f"target_{no}", LEGEND[0][0]),
                         (f"collar_target_{no}", LEGEND[5][0])):
            if key in S.files:
                layers.append((S[key].astype(bool), col))
        for mk, col in layers:
            if mk.any():
                ov[mk] = (ov[mk] * 0.25 + np.array(col) * 0.75).astype(np.uint8)
        scale = max(1, 768 // W)
        up = lambda z: cv2.resize(z, (W * scale, H * scale), interpolation=cv2.INTER_NEAREST)
        panel = np.hstack([up(p.rgb), np.full((H * scale, 6, 3), 255, np.uint8), up(ov),
                           np.full((H * scale, 6, 3), 255, np.uint8), up(fparts[no].rgb)])
        head = np.full((40, panel.shape[1], 3), 30, np.uint8)
        for i, t in enumerate(("original", "problem / fix mask", "fixed")):
            cv2.putText(head, f"part {no} {t}", (10 + i * (W * scale + 6), 28), cv2.FONT_HERSHEY_SIMPLEX, 0.8,
                        (255, 255, 255), 2, cv2.LINE_AA)
        leg = np.full((34 * len(LEGEND) + 10, panel.shape[1], 3), 30, np.uint8)
        for i, (col, txt) in enumerate(LEGEND):
            cv2.rectangle(leg, (10, 8 + i * 34), (40, 34 + i * 34), col, -1)
            cv2.putText(leg, txt, (52, 30 + i * 34), cv2.FONT_HERSHEY_SIMPLEX, 0.65, (230, 230, 230), 1, cv2.LINE_AA)
        out = np.vstack([head, panel, leg])
        Image.fromarray(out).save(os.path.join(tdir, f"uv_overlay_part_{no:02d}.png"))
        print("overlay part", no)


if __name__ == "__main__":
    main()
