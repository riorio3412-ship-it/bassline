#!/usr/bin/env python3
"""Final step: write the fixed GLB.

usage: python3 09_build_glb.py <orig.glb> <step.npz> <out.glb> [<work_dir>]

For every part whose base colour was edited by the fix steps:
  1. edge padding: gutter texels (outside the UV-island interiors) within
     PAD_BAND texels of an island get the colour of the nearest island texel,
     where the original gutter colour differs from it by more than PAD_DE
     (CIELAB dE) - e.g. skin-coloured gutter next to a hair island, which bleeds
     into the hair at lower mip levels in Unity.
  2. DCT-domain JPEG patching (fixlib.jpeg_patch): only the 16x16 MCUs that
     contain an edited texel or a changed padding texel are re-encoded, with the
     original quantisation tables (quality 95), 4:2:0 layout and Huffman tables.
     Every other MCU keeps its original compressed data, so untouched parts of
     the texture are bit-identical to the original (no second JPEG generation
     loss), resolution and format stay the same, file size stays ~ the same.
Parts that were not edited keep their original image bytes. All geometry /
material / UV data of the GLB is copied unchanged (glb_write.write_glb).
"""
import os
import sys

import cv2
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from uvmaps import load_parts  # noqa: E402
from fixlib import decode, island_core, jpeg_patch, lab_img, pad_gutters  # noqa: E402
from glb_write import write_glb  # noqa: E402

PAD_BAND = 4      # texels of gutter around each island that are padded
PAD_DE = 12.0     # only where the original gutter colour disagrees this much


def main():
    glb, step, out_glb = sys.argv[1:4]
    work = sys.argv[4] if len(sys.argv) > 4 else os.path.dirname(os.path.abspath(out_glb))
    os.makedirs(work, exist_ok=True)
    S = np.load(step)
    nos = sorted({int(k.split("_")[1]) for k in S.files if k.startswith("rgb_")})
    js, binc, parts = load_parts(glb, nos)
    rep = {}
    masks = {}
    for no in nos:
        p = parts[no]
        rgb = S[f"rgb_{no}"]
        if np.array_equal(rgb, p.rgb):
            continue
        core = island_core(p)
        edited = np.any(rgb != p.rgb, -1)
        padded = pad_gutters(rgb, core)
        band = cv2.dilate(core.astype(np.uint8), np.ones((2 * PAD_BAND + 1,) * 2, np.uint8)).astype(bool) & ~core
        de = np.linalg.norm(lab_img(padded) - lab_img(p.rgb), axis=-1)
        pad_change = band & (de > PAD_DE)
        target = rgb.copy()
        target[pad_change] = padded[pad_change]
        change = edited | pad_change
        data, reenc = jpeg_patch(p.img_bytes, target, change, os.path.join(work, f"tmp_part{no}"))
        rep[p.image_index] = data
        dec = decode(data).astype(int)
        o = p.rgb.astype(int)
        keep = ~reenc
        untouched_core_in_reenc = core & reenc & ~edited
        err = np.abs(dec - o).max(-1)
        err_t = np.abs(dec - target.astype(int)).max(-1)
        print(f"part {no}: {len(p.img_bytes)} -> {len(data)} bytes | MCUs re-encoded "
              f"{int(reenc.sum() // 256)}/{int(reenc.size // 256)} | edited texels {int(edited.sum())}, "
              f"padding texels {int(pad_change.sum())} | outside re-encoded MCUs: max diff {int(err[keep].max(initial=0))} | "
              f"unedited island texels inside re-encoded MCUs: mean {err[untouched_core_in_reenc].mean():.2f} "
              f"max {int(err[untouched_core_in_reenc].max(initial=0))} | edited texels vs target: mean "
              f"{err_t[edited].mean():.2f}")
        masks[f"pad_{no}"] = pad_change
        masks[f"reenc_{no}"] = reenc
    np.savez_compressed(os.path.join(work, "build_masks.npz"), **masks)
    n = write_glb(glb, out_glb, replace_images=rep)
    print(out_glb, n, "bytes")


if __name__ == "__main__":
    main()
