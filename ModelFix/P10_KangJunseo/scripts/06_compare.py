#!/usr/bin/env python3
"""Build labelled BEFORE | AFTER comparison images and a contact sheet.

usage: python3 06_compare.py <before_dir> <after_dir> <compare_dir>
For every <view>_<mode>.png present in both dirs writes
  compare/<view>_<mode>_compare.png   (before | after, labelled)
and compare/contact_sheet.png (face views, lit + unlit rows) plus
compare/contact_sheet_closeups.png (eye / hairline / mouth / ear / neck).
"""
import os
import sys

import cv2
import numpy as np

FONT = cv2.FONT_HERSHEY_SIMPLEX
PNG = [cv2.IMWRITE_PNG_COMPRESSION, 9]

VIEW_TITLE = {
    "body_front": "full body front", "body_34L": "full body 3/4 (char. left)",
    "body_34R": "full body 3/4 (char. right)", "body_back": "full body back",
    "face_front": "face front", "face_34L": "face 3/4 left", "face_34R": "face 3/4 right",
    "face_profL": "left profile", "face_profR": "right profile", "face_high": "from above-front",
    "face_low": "from below-front", "head_back": "head back", "face_top": "top-down (hair)",
    "eyes_both": "both eyes", "eye_R_close": "right eye (char.)", "eye_L_close": "left eye (char.)",
    "hairline_close": "hairline / bangs", "mouth_close": "mouth / nose", "ear_L_close": "left ear",
    "ear_R_close": "right ear", "neck_close": "neck / collar",
}


def label(img, text, sub=None):
    out = img.copy()
    h = 44 if sub is None else 70
    bar = np.full((h, out.shape[1], 3), 30, np.uint8)
    cv2.putText(bar, text, (12, 31), FONT, 0.95, (255, 255, 255), 2, cv2.LINE_AA)
    if sub:
        cv2.putText(bar, sub, (12, 60), FONT, 0.6, (200, 200, 200), 1, cv2.LINE_AA)
    return np.vstack([bar, out])


def pair(b, a, view, mode):
    title = VIEW_TITLE.get(view, view)
    left = label(b, "BEFORE (original)", f"{title} - {mode}")
    right = label(a, "AFTER (fixed)", f"{title} - {mode}")
    sep = np.full((left.shape[0], 8, 3), 255, np.uint8)
    return np.hstack([left, sep, right])


def fit(img, w):
    s = w / img.shape[1]
    return cv2.resize(img, (w, int(round(img.shape[0] * s))), interpolation=cv2.INTER_AREA)


def main():
    bdir, adir, cdir = sys.argv[1:4]
    os.makedirs(cdir, exist_ok=True)
    names = sorted(f for f in os.listdir(bdir) if f.endswith(".png") and os.path.exists(os.path.join(adir, f)))
    made = []
    for f in names:
        view, mode = f[:-4].rsplit("_", 1)
        b = cv2.imread(os.path.join(bdir, f))
        a = cv2.imread(os.path.join(adir, f))
        cv2.imwrite(os.path.join(cdir, f"{view}_{mode}_compare.png"), pair(b, a, view, mode), PNG)
        made.append((view, mode))

    def sheet(views, modes, cell, out):
        rows = []
        for v in views:
            cells = []
            for m in modes:
                fb, fa = os.path.join(bdir, f"{v}_{m}.png"), os.path.join(adir, f"{v}_{m}.png")
                if not (os.path.exists(fb) and os.path.exists(fa)):
                    continue
                b = fit(cv2.imread(fb), cell)
                a = fit(cv2.imread(fa), cell)
                hh = max(b.shape[0], a.shape[0])
                b = cv2.copyMakeBorder(b, 0, hh - b.shape[0], 0, 0, cv2.BORDER_CONSTANT, value=(128, 128, 128))
                a = cv2.copyMakeBorder(a, 0, hh - a.shape[0], 0, 0, cv2.BORDER_CONSTANT, value=(128, 128, 128))
                cv2.putText(b, "BEFORE", (8, 26), FONT, 0.7, (0, 255, 255), 2, cv2.LINE_AA)
                cv2.putText(a, "AFTER", (8, 26), FONT, 0.7, (0, 255, 0), 2, cv2.LINE_AA)
                cv2.putText(b, f"{VIEW_TITLE.get(v, v)} / {m}", (8, hh - 10), FONT, 0.5, (255, 255, 255), 1, cv2.LINE_AA)
                cells += [b, np.full((hh, 3, 3), 255, np.uint8), a, np.full((hh, 14, 3), 40, np.uint8)]
            if cells:
                rows.append(np.hstack(cells))
        if not rows:
            return
        W = max(r.shape[1] for r in rows)
        rows = [cv2.copyMakeBorder(r, 0, 6, 0, W - r.shape[1], cv2.BORDER_CONSTANT, value=(40, 40, 40)) for r in rows]
        cv2.imwrite(out, np.vstack(rows), PNG)

    sheet(["face_front", "face_34L", "face_34R", "face_profL", "face_profR", "face_high", "face_low", "head_back"],
          ["lit", "unlit"], 300, os.path.join(cdir, "contact_sheet.png"))
    sheet(["eyes_both", "eye_R_close", "eye_L_close", "hairline_close", "mouth_close", "ear_L_close", "ear_R_close",
           "neck_close"], ["lit", "unlit"], 300, os.path.join(cdir, "contact_sheet_closeups.png"))
    print(len(made), "comparison images written to", cdir)


if __name__ == "__main__":
    main()
