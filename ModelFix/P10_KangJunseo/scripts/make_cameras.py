#!/usr/bin/env python3
"""Generate the fixed camera set (glTF coordinates: Y up, +Z = character front,
+X = character's LEFT). Feature positions were measured by ray casting the
original mesh (see REPORT.md). Output: cameras.json next to this script."""
import json, math, os

C = (-0.0025, 0.893, 0.03)          # head centre
EYE_R = (-0.0236, 0.889, 0.072)     # character's right eye (image left in front view)
EYE_L = (0.0189, 0.889, 0.0734)     # character's left eye
MOUTH = (-0.0026, 0.853, 0.0764)
HAIRLINE = (-0.002, 0.915, 0.086)
EAR_L = (0.038, 0.878, 0.03)
EAR_R = (-0.038, 0.878, 0.03)
NECK = (0.0, 0.82, 0.045)


def add(a, b):
    return [round(a[i] + b[i], 5) for i in range(3)]


def orbit(t, dist, yaw_deg, pitch_deg=0.0):
    y, p = math.radians(yaw_deg), math.radians(pitch_deg)
    d = (dist * math.cos(p) * math.sin(y), dist * math.sin(p), dist * math.cos(p) * math.cos(y))
    return add(t, d)


cams = {}
BODY_T = (0.0, 0.49, 0.0)
for name, yaw in (("body_front", 0), ("body_34L", 45), ("body_34R", -45), ("body_back", 180)):
    cams[name] = {"eye": orbit(BODY_T, 3.0, yaw), "target": list(BODY_T), "lens": 85, "res": [1024, 1536]}
FACE = [("face_front", 0, 0), ("face_34L", 45, 0), ("face_34R", -45, 0), ("face_profL", 90, 0),
        ("face_profR", -90, 0), ("face_high", 0, 40), ("face_low", 0, -25), ("head_back", 180, 0)]
for name, yaw, pitch in FACE:
    cams[name] = {"eye": orbit(C, 0.5, yaw, pitch), "target": list(C), "lens": 85, "res": [1024, 1024]}
cams["face_top"] = {"eye": add(C, (0, 0.5, 0)), "target": list(C), "up": [0, 0, -1], "lens": 85, "res": [1024, 1024]}
cams["eyes_both"] = {"eye": add(((EYE_R[0] + EYE_L[0]) / 2, 0.889, 0.073), (0, 0, 0.2)),
                     "target": [(EYE_R[0] + EYE_L[0]) / 2, 0.889, 0.073], "lens": 85, "res": [1600, 1024]}
cams["eye_R_close"] = {"eye": add(EYE_R, (0, 0, 0.13)), "target": list(EYE_R), "lens": 85, "res": [1024, 1024]}
cams["eye_L_close"] = {"eye": add(EYE_L, (0, 0, 0.13)), "target": list(EYE_L), "lens": 85, "res": [1024, 1024]}
cams["hairline_close"] = {"eye": add(HAIRLINE, (0, 0.03, 0.18)), "target": list(HAIRLINE), "lens": 85, "res": [1024, 1024]}
cams["mouth_close"] = {"eye": add(MOUTH, (0, 0, 0.12)), "target": list(MOUTH), "lens": 85, "res": [1024, 1024]}
cams["ear_L_close"] = {"eye": add(EAR_L, (0.15, 0, 0.02)), "target": list(EAR_L), "lens": 85, "res": [1024, 1024]}
cams["ear_R_close"] = {"eye": add(EAR_R, (-0.15, 0, 0.02)), "target": list(EAR_R), "lens": 85, "res": [1024, 1024]}
cams["neck_close"] = {"eye": add(NECK, (0, -0.02, 0.17)), "target": list(NECK), "lens": 85, "res": [1024, 1024]}

out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "cameras.json")
json.dump(cams, open(out, "w"), indent=1)
print(out, len(cams), "views")
