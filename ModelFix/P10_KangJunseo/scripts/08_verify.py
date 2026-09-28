#!/usr/bin/env python3
"""Verify the fixed GLB against the original.

usage: python3 08_verify.py <orig.glb> <fixed.glb> [<out.json>]
Checks: glTF JSON identical except image bufferView offsets/lengths; every
non-image buffer view (positions, normals, UVs, indices) byte-identical;
same image count, mime types and resolutions; trimesh loads it with the same
vertex/triangle count; pygltflib parses it; a Blender re-import (run
separately by verify_blender.py) is summarised if its JSON is present.
"""
import io
import json
import os
import sys

import numpy as np
import pygltflib
import trimesh
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from uvmaps import read_glb  # noqa: E402


def bv_bytes(js, binc, i):
    bv = js["bufferViews"][i]
    return binc[bv.get("byteOffset", 0):bv.get("byteOffset", 0) + bv["byteLength"]]


def main():
    orig, fixed = sys.argv[1:3]
    out = sys.argv[3] if len(sys.argv) > 3 else None
    j0, b0 = read_glb(orig)
    j1, b1 = read_glb(fixed)
    res = {"orig_size": os.path.getsize(orig), "fixed_size": os.path.getsize(fixed)}
    img_views = {im["bufferView"] for im in j0["images"]}
    same_geom = all(bv_bytes(j0, b0, i) == bv_bytes(j1, b1, i)
                    for i in range(len(j0["bufferViews"])) if i not in img_views)
    res["geometry_buffers_byte_identical"] = bool(same_geom)
    k0 = {k: v for k, v in j0.items() if k not in ("bufferViews", "buffers")}
    k1 = {k: v for k, v in j1.items() if k not in ("bufferViews", "buffers")}
    res["gltf_json_identical_except_buffer_layout"] = k0 == k1
    changed, sizes = [], []
    for ii, im in enumerate(j0["images"]):
        d0, d1 = bv_bytes(j0, b0, im["bufferView"]), bv_bytes(j1, b1, j1["images"][ii]["bufferView"])
        i0, i1 = Image.open(io.BytesIO(d0)), Image.open(io.BytesIO(d1))
        sizes.append(i0.size == i1.size and i0.format == i1.format == "JPEG")
        if d0 != d1:
            changed.append({"image": ii, "name": im.get("name"), "size": list(i1.size),
                            "bytes_before": len(d0), "bytes_after": len(d1)})
    res["all_images_same_resolution_and_jpeg"] = all(sizes)
    res["changed_images"] = changed
    g = pygltflib.GLTF2().load(fixed)
    res["pygltflib"] = {"meshes": len(g.meshes), "materials": len(g.materials), "images": len(g.images),
                        "textures": len(g.textures), "nodes": len(g.nodes)}
    counts = []
    for path in (orig, fixed):
        sc = trimesh.load(path, process=False)
        tris = sum(len(sc.geometry[sc.graph[n][1]].faces) for n in sc.graph.nodes_geometry)
        verts = sum(len(sc.geometry[sc.graph[n][1]].vertices) for n in sc.graph.nodes_geometry)
        tex_ok = all(getattr(sc.geometry[sc.graph[n][1]].visual, "material", None) is not None
                     for n in sc.graph.nodes_geometry)
        b = sc.bounds
        counts.append({"triangles": int(tris), "vertices": int(verts), "materials_with_texture": tex_ok,
                       "bounds": np.round(b, 5).tolist()})
    res["trimesh_orig"], res["trimesh_fixed"] = counts
    bj = os.path.splitext(fixed)[0] + "_blender_check.json"
    if os.path.exists(bj):
        res["blender_reimport"] = json.load(open(bj))
    txt = json.dumps(res, indent=1, ensure_ascii=False)
    print(txt)
    if out:
        open(out, "w").write(txt)


if __name__ == "__main__":
    main()
