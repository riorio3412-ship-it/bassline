#!/usr/bin/env python3
"""3-way comparison for the optional variant that keeps the big central bang
lock in its original (skin) paint:  ORIGINAL | FIXED (default) | VARIANT.

usage: python3 11_variant_compare.py <before_dir> <after_dir> <variant_dir> <out_png> view1 [view2 ...]
(views are file stems like face_front_lit)
"""
import sys

import cv2
import numpy as np

FONT = cv2.FONT_HERSHEY_SIMPLEX


def lab(img, t, sub):
    bar = np.full((64, img.shape[1], 3), 30, np.uint8)
    cv2.putText(bar, t, (10, 28), FONT, 0.8, (255, 255, 255), 2, cv2.LINE_AA)
    cv2.putText(bar, sub, (10, 54), FONT, 0.55, (200, 200, 200), 1, cv2.LINE_AA)
    return np.vstack([bar, img])


def main():
    bdir, adir, vdir, out = sys.argv[1:5]
    rows = []
    for v in sys.argv[5:]:
        ims = [cv2.imread(f"{d}/{v}.png") for d in (bdir, adir, vdir)]
        ims = [cv2.resize(i, (640, int(640 * i.shape[0] / i.shape[1])), interpolation=cv2.INTER_AREA) for i in ims]
        cells = [lab(ims[0], "ORIGINAL", v), lab(ims[1], "FIXED (default)", v),
                 lab(ims[2], "VARIANT: centre bang lock keeps original paint", v)]
        sep = np.full((cells[0].shape[0], 8, 3), 255, np.uint8)
        rows.append(np.hstack([cells[0], sep, cells[1], sep, cells[2]]))
    cv2.imwrite(out, np.vstack(rows), [cv2.IMWRITE_PNG_COMPRESSION, 9])
    print(out)


if __name__ == "__main__":
    main()
