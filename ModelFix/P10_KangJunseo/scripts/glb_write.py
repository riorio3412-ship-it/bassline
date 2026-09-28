"""Rewrite a GLB, replacing selected embedded images (and optionally accessor data)
while keeping every other byte of JSON/binary content as it was.

replace_images: {image_index: new_bytes}
replace_accessors: {accessor_index: numpy array with identical dtype/shape}
The bufferViews are re-packed in their original order with 4-byte alignment,
all other JSON fields are left untouched (only byteOffset/byteLength of the
buffer views, buffer byteLength and accessor min/max of replaced accessors).
"""
import json
import struct

import numpy as np


def read_glb(path):
    d = open(path, "rb").read()
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


def write_glb(src, dst, replace_images=None, replace_accessors=None):
    js, binc = read_glb(src)
    replace_images = replace_images or {}
    replace_accessors = replace_accessors or {}
    bv_new = {}
    for ii, data in replace_images.items():
        bv_new[js["images"][ii]["bufferView"]] = bytes(data)
    for ai, arr in replace_accessors.items():
        a = js["accessors"][ai]
        assert a.get("byteOffset", 0) == 0, "accessor with byteOffset not supported"
        bv = js["bufferViews"][a["bufferView"]]
        old = binc[bv.get("byteOffset", 0):bv.get("byteOffset", 0) + bv["byteLength"]]
        new = np.ascontiguousarray(arr).tobytes()
        assert len(new) == len(old), "accessor replacement must keep the byte size"
        bv_new[a["bufferView"]] = new
        if "min" in a:
            a["min"] = np.asarray(arr).reshape(a["count"], -1).min(0).tolist()
            a["max"] = np.asarray(arr).reshape(a["count"], -1).max(0).tolist()
    out = bytearray()
    for i, bv in enumerate(js["bufferViews"]):
        start = bv.get("byteOffset", 0)
        data = bv_new.get(i, binc[start:start + bv["byteLength"]])
        while len(out) % 4:
            out += b"\x00"
        bv["byteOffset"] = len(out)
        bv["byteLength"] = len(data)
        out += data
    while len(out) % 4:
        out += b"\x00"
    js["buffers"][0]["byteLength"] = len(out)
    jb = json.dumps(js, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
    while len(jb) % 4:
        jb += b" "
    total = 12 + 8 + len(jb) + 8 + len(out)
    with open(dst, "wb") as f:
        f.write(struct.pack("<4sII", b"glTF", 2, total))
        f.write(struct.pack("<I4s", len(jb), b"JSON"))
        f.write(jb)
        f.write(struct.pack("<I4s", len(out), b"BIN\x00"))
        f.write(out)
    return total
