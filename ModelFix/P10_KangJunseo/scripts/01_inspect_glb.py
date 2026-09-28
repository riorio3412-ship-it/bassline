#!/usr/bin/env python3
"""Step 1: inspect the P10 GLB (structure, meshes, materials, textures, UVs).

Usage: python3 01_inspect_glb.py <input.glb> <out_dir>
Writes:
  <out_dir>/inspect_summary.json   machine-readable summary
  <out_dir>/inspect_summary.txt    human-readable table
  <out_dir>/tex_orig/part_XX.jpg   the embedded base-colour images, byte-exact
"""
import io
import json
import os
import struct
import sys

import numpy as np
from PIL import Image

COMP = {5120: np.int8, 5121: np.uint8, 5122: np.int16, 5123: np.uint16,
        5125: np.uint32, 5126: np.float32}
NCOMP = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}


def read_glb(path):
    d = open(path, "rb").read()
    magic, ver, length = struct.unpack("<4sII", d[:12])
    assert magic == b"glTF" and ver == 2
    off = 12
    js = binc = None
    while off < len(d):
        clen, ctype = struct.unpack("<I4s", d[off:off + 8])
        if ctype == b"JSON":
            js = json.loads(d[off + 8:off + 8 + clen])
        elif ctype == b"BIN\x00":
            binc = d[off + 8:off + 8 + clen]
        off += 8 + clen
    return js, binc


def accessor(js, binc, idx):
    a = js["accessors"][idx]
    bv = js["bufferViews"][a["bufferView"]]
    dt = COMP[a["componentType"]]
    n = NCOMP[a["type"]]
    start = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
    stride = bv.get("byteStride")
    if stride and stride != np.dtype(dt).itemsize * n:
        raise NotImplementedError("strided accessor")
    arr = np.frombuffer(binc, dtype=dt, count=a["count"] * n, offset=start)
    return arr.reshape(a["count"], n) if n > 1 else arr


def main():
    src, out = sys.argv[1], sys.argv[2]
    os.makedirs(os.path.join(out, "tex_orig"), exist_ok=True)
    js, binc = read_glb(src)
    summary = {
        "file": os.path.basename(src),
        "file_size": os.path.getsize(src),
        "generator": js.get("asset", {}).get("generator"),
        "extensionsUsed": js.get("extensionsUsed", []),
        "has_skins": "skins" in js,
        "has_animations": "animations" in js,
        "n_nodes": len(js["nodes"]),
        "n_meshes": len(js["meshes"]),
        "n_materials": len(js["materials"]),
        "n_images": len(js.get("images", [])),
        "samplers": js.get("samplers"),
        "parts": [],
    }
    allpos = []
    for ni, node in enumerate(js["nodes"]):
        if "mesh" not in node:
            continue
        for k in ("matrix", "translation", "rotation", "scale"):
            if k in node:
                summary.setdefault("node_transforms", []).append((node["name"], k, node[k]))
        mesh = js["meshes"][node["mesh"]]
        for prim in mesh["primitives"]:
            pos = accessor(js, binc, prim["attributes"]["POSITION"])
            uv = accessor(js, binc, prim["attributes"]["TEXCOORD_0"])
            idx = accessor(js, binc, prim["indices"])
            allpos.append(pos)
            mat = js["materials"][prim["material"]]
            texi = mat["pbrMetallicRoughness"]["baseColorTexture"]["index"]
            imgi = js["textures"][texi]["source"]
            img = js["images"][imgi]
            bv = js["bufferViews"][img["bufferView"]]
            raw = binc[bv.get("byteOffset", 0):bv.get("byteOffset", 0) + bv["byteLength"]]
            im = Image.open(io.BytesIO(raw))
            ext = "jpg" if img["mimeType"] == "image/jpeg" else "png"
            part_no = int(node["name"].split("_")[-1])
            open(os.path.join(out, "tex_orig", f"part_{part_no:02d}.{ext}"), "wb").write(raw)
            q = None
            if ext == "jpg" and hasattr(im, "quantization") and im.quantization:
                q = {k: int(np.mean(v)) for k, v in im.quantization.items()}
            tris = idx.reshape(-1, 3)
            # UV coverage: rasterise triangles coarsely to estimate used texture area
            uvc = np.clip(uv, 0, 1)
            summary["parts"].append({
                "node": node["name"],
                "mesh": node["mesh"],
                "vertices": int(pos.shape[0]),
                "triangles": int(tris.shape[0]),
                "bbox_min": pos.min(0).round(4).tolist(),
                "bbox_max": pos.max(0).round(4).tolist(),
                "centroid": pos.mean(0).round(4).tolist(),
                "uv_min": uv.min(0).round(4).tolist(),
                "uv_max": uv.max(0).round(4).tolist(),
                "uv_out_of_range": int(((uv < 0) | (uv > 1)).any(1).sum()),
                "material": mat["name"],
                "pbr": {k: v for k, v in mat["pbrMetallicRoughness"].items() if k != "baseColorTexture"},
                "doubleSided": mat.get("doubleSided", False),
                "alphaMode": mat.get("alphaMode", "OPAQUE"),
                "mat_extensions": list(mat.get("extensions", {}).keys()),
                "other_textures": [k for k in mat if k.endswith("Texture")] +
                                  [k for k in mat["pbrMetallicRoughness"] if k.endswith("Texture") and k != "baseColorTexture"],
                "image_name": img.get("name"),
                "image_mime": img["mimeType"],
                "image_size": list(im.size),
                "image_mode": im.mode,
                "image_bytes": len(raw),
                "jpeg_mean_quant": q,
                "prim_extensions": list(prim.get("extensions", {}).keys()),
                "morph_targets": len(prim.get("targets", [])),
                "attributes": sorted(prim["attributes"].keys()),
            })
    P = np.concatenate(allpos)
    summary["scene_bbox_min"] = P.min(0).round(4).tolist()
    summary["scene_bbox_max"] = P.max(0).round(4).tolist()
    summary["total_vertices"] = int(sum(p["vertices"] for p in summary["parts"]))
    summary["total_triangles"] = int(sum(p["triangles"] for p in summary["parts"]))
    json.dump(summary, open(os.path.join(out, "inspect_summary.json"), "w"), indent=1, ensure_ascii=False)

    lines = []
    lines.append(f"file={summary['file']} size={summary['file_size']} generator={summary['generator']}")
    lines.append(f"extensionsUsed={summary['extensionsUsed']} skins={summary['has_skins']} anims={summary['has_animations']}")
    lines.append(f"meshes={summary['n_meshes']} materials={summary['n_materials']} images={summary['n_images']}")
    lines.append(f"total verts={summary['total_vertices']} tris={summary['total_triangles']}")
    lines.append(f"scene bbox {summary['scene_bbox_min']} .. {summary['scene_bbox_max']}")
    lines.append(f"node transforms: {summary.get('node_transforms')}")
    lines.append("part  verts  tris   bbox_min                  bbox_max                  tex      bytes   q  matext  alpha")
    for p in summary["parts"]:
        lines.append(f"{p['node']:14s} {p['vertices']:6d} {p['triangles']:6d} {str(p['bbox_min']):26s} {str(p['bbox_max']):26s} "
                     f"{p['image_size'][0]}x{p['image_size'][1]} {p['image_bytes']:7d} {p['jpeg_mean_quant']} {p['mat_extensions']} {p['alphaMode']} {p['prim_extensions']} morph={p['morph_targets']}")
    open(os.path.join(out, "inspect_summary.txt"), "w").write("\n".join(lines) + "\n")
    print("\n".join(lines))


if __name__ == "__main__":
    main()
