#!/usr/bin/env python3
"""Step 2/3: texture-space analysis of the head parts.

usage: python3 02_analyze.py <orig.glb> <renders_before_dir> <out_dir>

For every head-related part it computes
  * per-triangle "thickness": distance from the triangle centroid along the
    inward normal to the next surface of the whole model. Hair locks are thin
    closed shells (< ~0.012 model units), the head shell (skull/face surface)
    is not (the ray crosses the whole head, > 0.05).
  * a second, normal-independent cue: distance along a horizontal ray from the
    centroid toward the head's vertical axis (x=-0.0025, z=0.01; toward the
    head centre above y=0.93). Tripo's face surface is crumpled around the
    eyes, so the normal ray alone misfires there; a hair lock is only accepted
    when BOTH rays hit another surface within 0.012 / 0.02.
  * per-texel visibility (pixel hits over all BEFORE aux renders).
  * skin-coloured texels (CIELAB L*>30, a*>2.5, b*>2.5) and their connected
    components inside each UV island.
A component on a thin (hair-lock) surface = "skin colour on hair".
Outputs: analysis.npz, components.json and UV overlay PNGs.
"""
import json
import os
import sys

import cv2
import numpy as np
import trimesh

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from uvmaps import load_parts  # noqa: E402
from exr_util import read_aux  # noqa: E402

HEAD_PARTS = [8, 10, 14, 19, 21, 24, 28, 11, 18]
HAIR_PARTS = [8, 10, 21, 24, 28]   # parts that contain hair geometry (21 = hair + left eye shell)
THIN = 0.012
AXIS_THIN = 0.02


def lab_img(rgb):
    L = cv2.cvtColor(rgb, cv2.COLOR_RGB2LAB).astype(np.float32)
    L[..., 0] *= 100.0 / 255.0
    L[..., 1:] -= 128.0
    return L


def skin_mask(rgb):
    L = lab_img(rgb)
    return (L[..., 0] > 30) & (L[..., 1] > 2.5) & (L[..., 2] > 2.5)


def full_mesh(glb):
    sc = trimesh.load(glb, process=False)
    return trimesh.util.concatenate([sc.geometry[sc.graph[n][1]] for n in sc.graph.nodes_geometry])


def triangle_thickness(part, M):
    tri = part.pos[part.tris]
    cen = tri.mean(1)
    fn = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0])
    fn /= np.linalg.norm(fn, axis=1, keepdims=True) + 1e-15
    vn = part.nrm[part.tris].mean(1)
    fn[(fn * vn).sum(1) < 0] *= -1          # orient like the vertex normals
    res = {}
    for sgn, key in ((-1, "in"), (1, "out")):
        o = cen + sgn * fn * 2e-5
        d = sgn * fn
        loc, ri, _ = M.ray.intersects_location(o, d, multiple_hits=False)
        dist = np.full(len(o), np.inf)
        if len(ri):
            dist[ri] = np.linalg.norm(loc - o[ri], axis=1)
        res[key] = dist
    return res["in"], res["out"]


HEAD_AXIS_XZ = (-0.0025, 0.01)
HEAD_CENTRE = np.array([-0.0025, 0.89, 0.01])


def axis_distance(part, M):
    cen = part.pos[part.tris].mean(1)
    d = np.stack([HEAD_AXIS_XZ[0] - cen[:, 0], np.zeros(len(cen)), HEAD_AXIS_XZ[1] - cen[:, 2]], 1)
    top = cen[:, 1] > 0.93
    d[top] = HEAD_CENTRE - cen[top]
    d /= np.linalg.norm(d, axis=1, keepdims=True) + 1e-12
    o = cen + d * 2e-5
    loc, ri, _ = M.ray.intersects_location(o, d, multiple_hits=False)
    dist = np.full(len(o), np.inf)
    if len(ri):
        dist[ri] = np.linalg.norm(loc - o[ri], axis=1)
    return dist


def visibility(src, parts):
    tot = {k: np.zeros((p.H, p.W), np.float32) for k, p in parts.items()}
    views = sorted({f[:-8] for f in os.listdir(src) if f.endswith("_aux.exr")})
    for v in views:
        u, vv, part = read_aux(os.path.join(src, f"{v}_aux.exr"))
        for k, p in parts.items():
            m = part == k
            if not m.any():
                continue
            x = np.clip((u[m] * p.W).astype(int), 0, p.W - 1)
            y = np.clip(((1 - vv[m]) * p.H).astype(int), 0, p.H - 1)
            np.add.at(tot[k], (y, x), 1)
    return tot, views


def main():
    glb, src, out = sys.argv[1:4]
    os.makedirs(out, exist_ok=True)
    js, binc, parts = load_parts(glb, HEAD_PARTS)
    M = full_mesh(glb)
    vis, views = visibility(src, parts)
    save = {}
    comps = []
    for k, p in parts.items():
        m = p.maps()
        tin, tout = triangle_thickness(p, M)
        tax = axis_distance(p, M)
        save[f"tri_in_{k}"] = tin
        save[f"tri_out_{k}"] = tout
        save[f"tri_axis_{k}"] = tax
        tri_lock = (tin < THIN) & (tax < AXIS_THIN)
        din = np.full((p.H, p.W), np.nan, np.float32)
        lock = np.zeros((p.H, p.W), bool)
        t = m["tri"]
        din[t >= 0] = tin[t[t >= 0]]
        lock[t >= 0] = tri_lock[t[t >= 0]]
        save[f"din_{k}"] = din
        save[f"lock_{k}"] = lock
        save[f"vis_{k}"] = vis[k]
        if k not in HAIR_PARTS:
            continue
        skin = m["cov"] & skin_mask(p.rgb)
        for isl in np.unique(m["isl"][skin]):
            n, lab = cv2.connectedComponents((skin & (m["isl"] == isl)).astype(np.uint8), connectivity=8)
            for c in range(1, n):
                cm = lab == c
                if cm.sum() < 3:
                    continue
                d = din[cm]
                d = d[np.isfinite(d)]
                thin_frac = float(lock[cm].mean())
                ys, xs = np.nonzero(cm)
                comps.append(dict(part=k, island=int(isl), comp=int(c), texels=int(cm.sum()),
                                  thin_frac=round(thin_frac, 3),
                                  din_median=round(float(np.median(d)), 4) if len(d) else None,
                                  vis_hits=int(vis[k][cm].sum()),
                                  centroid_gltf=[round(float(x), 4) for x in m["pos"][cm].mean(0)],
                                  bbox_texel=[int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())],
                                  kind="hair_lock" if thin_frac > 0.5 else "head_shell"))
                save[f"comp_{k}_{isl}_{c}"] = cm
    np.savez_compressed(os.path.join(out, "analysis.npz"), **save)
    json.dump({"views": views, "components": comps}, open(os.path.join(out, "components.json"), "w"), indent=1)
    lock = [c for c in comps if c["kind"] == "hair_lock"]
    print(f"{len(comps)} skin components on hair parts, {len(lock)} on thin hair-lock geometry")
    for c in sorted(comps, key=lambda c: -c["vis_hits"]):
        print(c)


if __name__ == "__main__":
    main()
