"""Shared helpers for the P10 texture fixes."""
import io
import os

import cv2
import numpy as np
from PIL import Image

THIN = 0.012


def lab_img(rgb):
    L = cv2.cvtColor(rgb, cv2.COLOR_RGB2LAB).astype(np.float32)
    L[..., 0] *= 100.0 / 255.0
    L[..., 1:] -= 128.0
    return L


def skin_mask(rgb, l_min=30.0, ab_min=2.5):
    L = lab_img(rgb)
    return (L[..., 0] > l_min) & (L[..., 1] > ab_min) & (L[..., 2] > ab_min)


def warm_light_mask(rgb, l_min=24.0, ab_min=1.5):
    """Looser skin test used for the transition texels around a skin blob."""
    L = lab_img(rgb)
    return (L[..., 0] > l_min) & (L[..., 1] > ab_min) & (L[..., 2] > ab_min)


def encode_like(orig_pil, rgb):
    """JPEG with the original quantisation tables; 4:4:4 chroma so the fixed
    colour edges are not blurred again by re-subsampling."""
    b = io.BytesIO()
    Image.fromarray(rgb).save(b, "JPEG", qtables=orig_pil.quantization, subsampling=0, optimize=True)
    return b.getvalue()


def decode(data):
    return np.array(Image.open(io.BytesIO(data)).convert("RGB"))


def island_core(part, min_core=6):
    """Texels that belong to exactly one island's interior (centre inside a
    triangle). Tiny islands with (almost) no centre-covered texels keep their
    conservative raster so they are not overwritten by neighbours."""
    m = part.maps()
    core = m["cov"].copy()
    tri_isl = m["tri_isl"]
    counts = np.bincount(m["isl"][m["cov"]], minlength=tri_isl.max() + 1)
    small = np.nonzero(counts < min_core)[0]
    if len(small):
        uvp = part.uv * [part.W, part.H] - 0.5
        pts = (uvp * 16).round().astype(np.int32)
        for isl in small:
            mk = np.zeros((part.H, part.W), np.uint8)
            for a, b, c in part.tris[tri_isl == isl]:
                cv2.fillConvexPoly(mk, pts[[a, b, c]], 1, lineType=cv2.LINE_8, shift=4)
                cv2.polylines(mk, [pts[[a, b, c]]], True, 1, 1, cv2.LINE_8, shift=4)
            core |= mk.astype(bool)
    return core


def pad_gutters(rgb, island_footprint, iterations=None):
    """Fill every texel outside `island_footprint` with the colour of the
    nearest footprint texel (edge padding against mip-map bleeding)."""
    out = rgb.copy()
    known = island_footprint.astype(bool)
    if known.all():
        return out
    dist, lab = cv2.distanceTransformWithLabels((~known).astype(np.uint8), cv2.DIST_L2, 5,
                                                labelType=cv2.DIST_LABEL_PIXEL)
    ys, xs = np.nonzero(known)
    lut = np.zeros((lab.max() + 1, 2), np.int64)
    lut[lab[ys, xs]] = np.stack([ys, xs], 1)
    uy, ux = np.nonzero(~known)
    src = lut[lab[uy, ux]]
    out[uy, ux] = rgb[src[:, 0], src[:, 1]]
    return out


def inpaint_region(rgb, target, forbid, radius=4, method="telea"):
    """Inpaint `target` texels using only texels that are neither target nor
    `forbid` as sources. Returns full image with only `target` replaced."""
    unknown = (target | forbid).astype(np.uint8)
    if method == "telea":
        res = cv2.inpaint(rgb, unknown, radius, cv2.INPAINT_TELEA)
    elif method == "ns":
        res = cv2.inpaint(rgb, unknown, radius, cv2.INPAINT_NS)
    else:
        bgr = rgb[..., ::-1].copy()
        dst = np.zeros_like(bgr)
        cv2.xphoto.inpaint(bgr, (1 - unknown).astype(np.uint8), dst,
                           cv2.xphoto.INPAINT_SHIFTMAP if method == "shiftmap" else cv2.xphoto.INPAINT_FSR_FAST)
        res = dst[..., ::-1]
    out = rgb.copy()
    out[target] = res[target]
    return out


def knn_fill(src_pos, src_col, src_nrm, P, N, k=24):
    """Inverse-distance (and normal-agreement) weighted colour of the k nearest
    source texels in 3D - a fill that is continuous across UV seams/parts."""
    from scipy.spatial import cKDTree
    tree = cKDTree(src_pos)
    d, i = tree.query(P, k=k)
    w = 1.0 / (d + 2e-4) ** 2
    w *= np.clip((src_nrm[i] * N[:, None, :]).sum(-1), 0.05, 1.0)
    return (src_col[i] * w[..., None]).sum(1) / w.sum(1, keepdims=True)


# --------------------------------------------------------------------------
# DCT-domain JPEG patching: keep the ORIGINAL quantised coefficients of every
# MCU that does not need to change, re-encode only the MCUs that contain edits
# (same quantisation tables, same 4:2:0 layout, same Huffman tables).
# Untouched areas therefore stay bit-identical to the original texture instead
# of suffering a second round of JPEG loss.
# --------------------------------------------------------------------------
def _rgb_to_ycc(rgb):
    rgb = rgb.astype(np.float64)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    y = 0.299 * r + 0.587 * g + 0.114 * b
    cb = -0.168735892 * r - 0.331264108 * g + 0.5 * b + 128.0
    cr = 0.5 * r - 0.418687589 * g - 0.081312411 * b + 128.0
    return y, cb, cr


def jpeg_patch(orig_bytes, target_rgb, change_mask, tmp_path):
    """Return JPEG bytes equal to the original except for the MCUs that contain
    a True texel in change_mask; those are encoded from target_rgb.
    Also returns the MCU mask that was re-encoded (in texels)."""
    import jpegio
    from scipy.fft import dctn
    src = tmp_path + "_src.jpg"
    dst = tmp_path + "_dst.jpg"
    open(src, "wb").write(orig_bytes)
    j = jpegio.read(src)
    ci = [(c.h_samp_factor, c.v_samp_factor, c.quant_tbl_no) for c in j.comp_info]
    hmax = max(c[0] for c in ci)
    vmax = max(c[1] for c in ci)
    H, W = target_rgb.shape[:2]
    mh, mw = 8 * vmax, 8 * hmax                       # MCU size in pixels
    assert H % mh == 0 and W % mw == 0, "partial MCUs not supported"
    ych = _rgb_to_ycc(np.clip(target_rgb, 0, 255))
    mcu_change = change_mask.reshape(H // mh, mh, W // mw, mw).any(axis=(1, 3))
    reenc = np.zeros((H, W), bool)
    for my, mx in zip(*np.nonzero(mcu_change)):
        reenc[my * mh:(my + 1) * mh, mx * mw:(mx + 1) * mw] = True
        for comp, (h, v, qn) in enumerate(ci):
            plane = ych[comp][my * mh:(my + 1) * mh, mx * mw:(mx + 1) * mw]
            fy, fx = vmax // v, hmax // h                 # downsampling factors
            if fy > 1 or fx > 1:
                plane = plane.reshape(plane.shape[0] // fy, fy, plane.shape[1] // fx, fx).mean(axis=(1, 3))
            q = np.asarray(j.quant_tables[qn], dtype=np.float64)
            for by in range(v):
                for bx in range(h):
                    blk = plane[by * 8:(by + 1) * 8, bx * 8:(bx + 1) * 8] - 128.0
                    coef = np.rint(dctn(blk, norm="ortho") / q).astype(np.int32)
                    Y0 = (my * v + by) * 8
                    X0 = (mx * h + bx) * 8
                    j.coef_arrays[comp][Y0:Y0 + 8, X0:X0 + 8] = coef
    jpegio.write(j, dst)
    data = open(dst, "rb").read()
    for f in (src, dst):
        try:
            os.remove(f)
        except OSError:
            pass
    return data, reenc
