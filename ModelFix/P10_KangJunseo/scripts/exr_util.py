"""Helpers to read the aux EXR renders (R=u, G=v, B=(part+1)/64, A=coverage)."""
import numpy as np
import OpenEXR


def read_aux(path):
    with OpenEXR.File(path) as f:
        ch = f.channels()
        if "RGBA" in ch:
            px = ch["RGBA"].pixels
            r, g, b, a = px[..., 0], px[..., 1], px[..., 2], px[..., 3]
        else:
            r, g, b, a = (ch[k].pixels for k in ("R", "G", "B", "A"))
    part = np.rint(b * 64.0).astype(np.int32) - 1
    part[a < 0.5] = -1
    return r.astype(np.float32), g.astype(np.float32), part
