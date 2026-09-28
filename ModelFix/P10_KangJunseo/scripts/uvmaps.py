"""UV-space helpers for the P10 GLB (pure numpy/OpenCV).

glTF UV convention: (u, v) = (0, 0) is the top-left of the image, texel (x, y)
has its centre at ((x + 0.5) / W, (y + 0.5) / H).

For one part (mesh primitive) this produces per-texel maps:
  cov   bool  texel centre inside some triangle (a "used" texel)
  tri   int   index of that triangle (-1 = none)
  isl   int   UV-island id (islands = triangles connected through shared UV verts)
  pos   f32x3 interpolated 3D position (glTF coordinates)
  nrm   f32x3 interpolated vertex normal (normalised)
"""
import io
import json
import struct

import cv2
import numpy as np
from PIL import Image

COMP = {5120: np.int8, 5121: np.uint8, 5122: np.int16, 5123: np.uint16,
        5125: np.uint32, 5126: np.float32}
NCOMP = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}


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


def accessor(js, binc, idx):
    a = js["accessors"][idx]
    bv = js["bufferViews"][a["bufferView"]]
    dt = COMP[a["componentType"]]
    n = NCOMP[a["type"]]
    start = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
    arr = np.frombuffer(binc, dtype=dt, count=a["count"] * n, offset=start)
    return arr.reshape(a["count"], n) if n > 1 else arr


class Part:
    def __init__(self, js, binc, part_no):
        node = [n for n in js["nodes"] if n.get("name") == f"tripo_part_{part_no}"][0]
        prim = js["meshes"][node["mesh"]]["primitives"][0]
        self.no = part_no
        self.pos = accessor(js, binc, prim["attributes"]["POSITION"]).astype(np.float64)
        self.nrm = accessor(js, binc, prim["attributes"]["NORMAL"]).astype(np.float64)
        self.uv = accessor(js, binc, prim["attributes"]["TEXCOORD_0"]).astype(np.float64)
        self.tris = accessor(js, binc, prim["indices"]).reshape(-1, 3).astype(np.int64)
        mat = js["materials"][prim["material"]]
        img = js["images"][js["textures"][mat["pbrMetallicRoughness"]["baseColorTexture"]["index"]]["source"]]
        self.image_index = js["textures"][mat["pbrMetallicRoughness"]["baseColorTexture"]["index"]]["source"]
        bv = js["bufferViews"][img["bufferView"]]
        self.img_bytes = binc[bv.get("byteOffset", 0):bv.get("byteOffset", 0) + bv["byteLength"]]
        self.mime = img["mimeType"]
        self.pil = Image.open(io.BytesIO(self.img_bytes))
        self.rgb = np.array(self.pil.convert("RGB"))
        self.H, self.W = self.rgb.shape[:2]
        self._maps = None

    # ------------------------------------------------------------------ islands
    def islands(self):
        """Triangle -> island id, connecting triangles that share a vertex index
        (Tripo splits UV seams into separate vertices, so vertex sharing == UV
        connectivity)."""
        n = len(self.pos)
        parent = np.arange(n)

        def find(x):
            root = x
            while parent[root] != root:
                root = parent[root]
            while parent[x] != root:
                parent[x], x = root, parent[x]
            return root

        for a, b, c in self.tris:
            ra, rb, rc = find(a), find(b), find(c)
            parent[rb] = ra
            parent[find(rc)] = ra
        roots = np.array([find(t[0]) for t in self.tris])
        _, tri_isl = np.unique(roots, return_inverse=True)
        return tri_isl

    # ---------------------------------------------------------------- raster
    def maps(self):
        if self._maps is not None:
            return self._maps
        H, W = self.H, self.W
        tri_map = np.full((H, W), -1, np.int64)
        bary = np.zeros((H, W, 3), np.float64)
        uvp = self.uv * [W, H] - 0.5  # texel-centre coordinates
        for ti, (a, b, c) in enumerate(self.tris):
            p = uvp[[a, b, c]]
            x0, y0 = np.floor(p.min(0)).astype(int)
            x1, y1 = np.ceil(p.max(0)).astype(int)
            x0, y0 = max(x0, 0), max(y0, 0)
            x1, y1 = min(x1, W - 1), min(y1, H - 1)
            if x1 < x0 or y1 < y0:
                continue
            xs, ys = np.meshgrid(np.arange(x0, x1 + 1), np.arange(y0, y1 + 1))
            P = np.stack([xs, ys], -1).astype(np.float64)
            v0, v1 = p[1] - p[0], p[2] - p[0]
            den = v0[0] * v1[1] - v1[0] * v0[1]
            if abs(den) < 1e-12:
                continue
            d = P - p[0]
            l1 = (d[..., 0] * v1[1] - v1[0] * d[..., 1]) / den
            l2 = (v0[0] * d[..., 1] - d[..., 0] * v0[1]) / den
            l0 = 1 - l1 - l2
            eps = -1e-6
            inside = (l0 >= eps) & (l1 >= eps) & (l2 >= eps)
            if not inside.any():
                continue
            yy, xx = ys[inside], xs[inside]
            tri_map[yy, xx] = ti
            bary[yy, xx] = np.stack([l0[inside], l1[inside], l2[inside]], -1)
        cov = tri_map >= 0
        # conservative coverage: also mark texels touched by triangle edges
        # (bilinear filtering reads them) using a polygon fill per triangle
        cov_cons = np.zeros((H, W), np.uint8)
        pts = (uvp * 16).round().astype(np.int32)
        for a, b, c in self.tris:
            cv2.fillConvexPoly(cov_cons, pts[[a, b, c]], 1, lineType=cv2.LINE_8, shift=4)
            cv2.polylines(cov_cons, [pts[[a, b, c]]], True, 1, 1, cv2.LINE_8, shift=4)
        tri_isl = self.islands()
        isl = np.full((H, W), -1, np.int64)
        isl[cov] = tri_isl[tri_map[cov]]
        pos = np.zeros((H, W, 3), np.float32)
        nrm = np.zeros((H, W, 3), np.float32)
        t = tri_map[cov]
        bw = bary[cov]
        vi = self.tris[t]
        pos[cov] = (self.pos[vi] * bw[..., None]).sum(1)
        nn = (self.nrm[vi] * bw[..., None]).sum(1)
        nn /= np.linalg.norm(nn, axis=1, keepdims=True) + 1e-12
        nrm[cov] = nn
        # island id for conservative-only texels: nearest covered texel's island
        cons = cov_cons.astype(bool) | cov
        extra = cons & ~cov
        if extra.any():
            dist, lab = cv2.distanceTransformWithLabels((~cov).astype(np.uint8), cv2.DIST_L2, 5,
                                                        labelType=cv2.DIST_LABEL_PIXEL)
            # map label -> coordinates of the zero (covered) pixel
            ys, xs = np.nonzero(~((~cov).astype(bool)))
            lab_to_yx = np.zeros((lab.max() + 1, 2), np.int64)
            lab_to_yx[lab[ys, xs]] = np.stack([ys, xs], 1)
            ey, ex = np.nonzero(extra)
            src = lab_to_yx[lab[ey, ex]]
            isl[ey, ex] = isl[src[:, 0], src[:, 1]]
            pos[ey, ex] = pos[src[:, 0], src[:, 1]]
            nrm[ey, ex] = nrm[src[:, 0], src[:, 1]]
            tri_map[ey, ex] = tri_map[src[:, 0], src[:, 1]]
        self._maps = dict(cov=cov, cons=cons, tri=tri_map, isl=isl, pos=pos, nrm=nrm, tri_isl=tri_isl)
        return self._maps


def load_parts(path, part_nos):
    js, binc = read_glb(path)
    return js, binc, {p: Part(js, binc, p) for p in part_nos}
