#!/usr/bin/env python3
"""Fix step H: skin colour painted on hair-lock geometry -> hair colour.

usage: python3 03_fix_hair.py <orig.glb> <analysis_dir> <out_dir>

Target texels (per hair-carrying part 8/10/21/24/28):
  * skin-coloured components (CIELAB L*>30, a*>2.5, b*>2.5, connected inside
    one UV island) of which at least half lies on hair-lock geometry
    (02_analyze.py: both thickness rays < 0.012 / 0.02) is taken whole (the
    few texels the thickness test misses are the same painted blob at the lock
    root/edge), plus
  * the warm/light transition ring (<=2 texels) around them in the same island,
  * light-grey remnants (L* > 24) on lock geometry within 4 texels of them in
    the same island (they became isolated light specks once the blob was dark).
Texels enclosed by such a blob or in its ring that are already dark (brown
painted strands) keep their lightness but lose the brown tint (a*=b*=0,
L* clamped to <= 24, the hair range), so the strand structure survives as neutral hair.
Skin on the head shell (forehead, right-eye socket fused into part 8, temples,
behind the ears, nape) is NOT touched - that is real face skin.

Fill: every target texel gets the inverse-distance weighted mean colour of its
24 nearest clean hair texels in 3D (all hair parts pooled, weighted by normal
agreement), so the fill is continuous across UV seams and parts instead of
being inpainted island by island.
Writes out_dir/hair_step.npz (edited RGB per part + target masks).

Option: environment variable P10_KEEP_CENTER_LOCK=1 leaves the big central
bang lock (the lock that was painted as forehead, centroid |x+0.0025|<0.016,
y>0.878, z>0.085) skin-coloured, i.e. the old front-view look, while all other
fixes stay. Default (unset) = the lock is recoloured to hair.
"""
import json
import os
import sys

import cv2
import numpy as np
from scipy.spatial import cKDTree

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from uvmaps import load_parts  # noqa: E402
from fixlib import lab_img, warm_light_mask  # noqa: E402

HAIR_PARTS = [8, 10, 21, 24, 28]
WHOLE_COMPONENT_FRAC = 0.5   # every component of the list that is >= 50 % on lock geometry is recoloured whole
HAIR_L_MAX = 24.0            # neutralised strand texels are clamped to the hair lightness range (hair L* 7-24)
NEUTRAL_BLEND = float(os.environ.get("P10_NEUTRAL_BLEND", "0.5"))  # 0 = keep strand lightness, 1 = plain fill


KEEP_CENTER_LOCK = os.environ.get("P10_KEEP_CENTER_LOCK", "") == "1"


def is_center_lock(c):
    x, y, z = c["centroid_gltf"]
    return abs(x + 0.0025) < 0.016 and y > 0.878 and z > 0.085


def hair_targets(p, no, A, comps):
    """Returns (replace, neutralise, core):
    replace    - skin-coloured blob texels -> replaced by the 3D hair fill
    neutralise - texels enclosed by / bordering such a blob in the same island
                 (painted brown strands, warm transition texels): keep their
                 lightness (strand structure) but remove the skin/brown tint
    """
    from scipy.ndimage import binary_fill_holes
    m = p.maps()
    lockgeo = A[f"lock_{no}"]
    Lab = lab_img(p.rgb)
    loose = warm_light_mask(p.rgb, l_min=22, ab_min=1.0) | (Lab[..., 0] > 28)
    k5 = np.ones((5, 5), np.uint8)
    core = np.zeros(lockgeo.shape, bool)
    replace = np.zeros(lockgeo.shape, bool)
    neutral = np.zeros(lockgeo.shape, bool)
    for c in comps:
        if c["part"] != no or c["thin_frac"] < WHOLE_COMPONENT_FRAC:
            continue
        if KEEP_CENTER_LOCK and is_center_lock(c):
            continue
        cm = A[f"comp_{no}_{c['island']}_{c['comp']}"]
        same_isl = (m["isl"] == c["island"]) & m["cons"]
        core |= cm
        ring = cv2.dilate(cm.astype(np.uint8), k5).astype(bool) & same_isl
        replace |= cm | (ring & loose)
        # texels enclosed by the blob or in its ring: strands painted into it
        filled = binary_fill_holes(cm | (ring & loose)) & same_isl
        neutral |= (filled | ring) & ~replace
    # light remnants: light-grey texels (L* > HAIR_L_MAX = 24, above the hair range) left on
    # hair-lock geometry within 4 texels of a replaced blob, in the same island.
    # Once the blob is dark they read as isolated light specks at the lock edge.
    # Texels of components classified as head shell (real skin) are excluded.
    if replace.any():
        shell = np.zeros(lockgeo.shape, bool)
        for c in comps:
            if c["part"] == no and c["kind"] == "head_shell":
                shell |= A[f"comp_{no}_{c['island']}_{c['comp']}"]
        hosts = np.isin(m["isl"], np.unique(m["isl"][replace & m["cov"]]))
        near = cv2.dilate(replace.astype(np.uint8), np.ones((9, 9), np.uint8)).astype(bool)
        remnant = near & hosts & m["cons"] & lockgeo & (Lab[..., 0] > HAIR_L_MAX) & ~shell & ~replace
        replace |= remnant
        neutral &= ~remnant
    return replace, neutral, core


def main():
    glb, adir, out = sys.argv[1:4]
    os.makedirs(out, exist_ok=True)
    A = np.load(os.path.join(adir, "analysis.npz"))
    comps = json.load(open(os.path.join(adir, "components.json")))["components"]
    js, binc, parts = load_parts(glb, HAIR_PARTS)
    targets, cores, neutrals = {}, {}, {}
    sp, sc, sn = [], [], []
    for no, p in parts.items():
        m = p.maps()
        t, nz, c = hair_targets(p, no, A, comps)
        targets[no], cores[no] = t, c
        neutrals[no] = nz
        Lab = lab_img(p.rgb)
        hair = m["cov"] & ~t & ~nz & (Lab[..., 0] < 30) & (np.abs(Lab[..., 1]) < 3) & (np.abs(Lab[..., 2]) < 3)
        sp.append(m["pos"][hair]); sc.append(p.rgb[hair].astype(np.float32)); sn.append(m["nrm"][hair])
    sp, sc, sn = np.concatenate(sp), np.concatenate(sc), np.concatenate(sn)
    tree = cKDTree(sp)
    save = {}
    for no, p in parts.items():
        t = targets[no]
        img = p.rgb.copy()
        if t.any():
            m = p.maps()
            P, N = m["pos"][t], m["nrm"][t]
            d, i = tree.query(P, k=24)
            w = 1.0 / (d + 2e-4) ** 2
            w *= np.clip((sn[i] * N[:, None, :]).sum(-1), 0.05, 1.0)
            col = (sc[i] * w[..., None]).sum(1) / w.sum(1, keepdims=True)
            img[t] = np.clip(np.rint(col), 0, 255).astype(np.uint8)
        nz = neutrals[no]
        if nz.any():
            lab = lab_img(img)
            Ln = np.minimum(lab[..., 0][nz], HAIR_L_MAX)
            lab8 = np.stack([Ln * 255.0 / 100.0, np.full_like(Ln, 128.0), np.full_like(Ln, 128.0)], -1)
            rgbn = cv2.cvtColor(np.clip(np.rint(lab8), 0, 255).astype(np.uint8).reshape(-1, 1, 3),
                                cv2.COLOR_LAB2RGB).reshape(-1, 3).astype(np.float32)
            if NEUTRAL_BLEND > 0:
                # pull the strand texels part-way toward the local 3D hair fill so
                # 1-texel painted lines do not read as dotted stair-steps
                m = p.maps()
                d, i = tree.query(m["pos"][nz], k=24)
                w = 1.0 / (d + 2e-4) ** 2
                w *= np.clip((sn[i] * m["nrm"][nz][:, None, :]).sum(-1), 0.05, 1.0)
                fill = (sc[i] * w[..., None]).sum(1) / w.sum(1, keepdims=True)
                rgbn = rgbn * (1 - NEUTRAL_BLEND) + fill * NEUTRAL_BLEND
            img[nz] = np.clip(np.rint(rgbn), 0, 255).astype(np.uint8)
        save[f"rgb_{no}"] = img
        save[f"target_{no}"] = t
        save[f"neutral_{no}"] = neutrals[no]
        save[f"core_{no}"] = cores[no]
        print(f"part {no}: replaced {int(t.sum())} texels ({int(cores[no].sum())} skin core), "
              f"neutralised {int(neutrals[no].sum())} brownish strand/ring texels")
    np.savez_compressed(os.path.join(out, "hair_step.npz"), **save)


if __name__ == "__main__":
    main()
