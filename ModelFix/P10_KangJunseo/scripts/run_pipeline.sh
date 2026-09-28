#!/bin/bash
# Reproduce the whole P10 cleanup from the untouched original.
#   bash run_pipeline.sh                 (work files go to $WORK, default /tmp/p10_work)
# Requirements: blender 4.x (apt, + python3-numpy for its python),
#   pip: numpy pillow opencv-contrib-python-headless scipy trimesh rtree pygltflib OpenEXR jpegio matplotlib
# Existing BEFORE renders in $WORK/renders_before are reused (delete the folder to re-render).
# Optional env: P10_NEUTRAL_BLEND (default 0.5), P10_KEEP_CENTER_LOCK=1 (see 03_fix_hair.py).
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
OUT="$(cd "$HERE/.." && pwd)"
WORK="${WORK:-/tmp/p10_work}"
ORIG="$OUT/original/강준서 2차 수정본.glb"
FIXED="$OUT/fixed/강준서 2차 수정본_보정1.glb"
VARIANT="$OUT/fixed/variant_keep_center_lock/강준서 2차 수정본_보정1_앞머리가운데원본색.glb"
mkdir -p "$WORK" "$OUT/fixed" "$OUT/before" "$OUT/after" "$OUT/compare" "$OUT/textures"

cp "$ORIG" "$WORK/p10_orig.glb"                      # ASCII working copy, original untouched
python3 "$HERE/01_inspect_glb.py" "$WORK/p10_orig.glb" "$WORK/inspect"
python3 "$HERE/make_cameras.py"

if [ ! -f "$WORK/renders_before/face_front_lit.png" ]; then
  bash "$HERE/render_all.sh" "$WORK/p10_orig.glb" "$WORK/renders_before"
fi
python3 "$HERE/02_analyze.py" "$WORK/p10_orig.glb" "$WORK/renders_before" "$WORK/analysis"
# frontal projection lookup (part / UV per pixel) - geometry only, so from the original
blender -b --python "$HERE/render_views.py" -- --glb "$WORK/p10_orig.glb" --cams "$HERE/cameras_proj.json" \
  --out "$WORK/proj" --mode aux > "$WORK/proj_aux.log" 2>&1

run_fix () {   # $1 = step dir, $2 = output glb
  python3 "$HERE/03_fix_hair.py"  "$WORK/p10_orig.glb" "$WORK/analysis" "$1"                     # H
  python3 "$HERE/04_fix_face.py"  "$WORK/p10_orig.glb" "$WORK/analysis" "$1/hair_step.npz" "$WORK/proj" "$1"   # E1 + S1
  python3 "$HERE/05_fix_collar.py" "$WORK/p10_orig.glb" "$1/face_step.npz" "$1"                   # N
  python3 "$HERE/09_build_glb.py" "$WORK/p10_orig.glb" "$1/collar_step.npz" "$2" "$1"             # P + JPEG patch + GLB
}

# ---- default fix
run_fix "$WORK/steps" "$WORK/p10_fixed.glb"
cp "$WORK/p10_fixed.glb" "$FIXED"

bash "$HERE/render_all.sh" "$WORK/p10_fixed.glb" "$WORK/renders_after"
cp "$WORK"/renders_before/*.png "$OUT/before/"
cp "$WORK"/renders_after/*.png "$OUT/after/"
python3 "$HERE/06_compare.py" "$OUT/before" "$OUT/after" "$OUT/compare"
python3 "$HERE/07_texture_report.py" "$WORK/p10_orig.glb" "$WORK/p10_fixed.glb" "$WORK/analysis" \
  "$WORK/steps/collar_step.npz" "$OUT/textures" "$WORK/steps/build_masks.npz"
python3 "$HERE/10_diagnostics.py" "$WORK/p10_orig.glb" "$WORK/renders_before" "$WORK/analysis" \
  "$WORK/steps/hair_step.npz" "$WORK/diag" "$OUT/before"

blender -b --python "$HERE/verify_blender.py" -- "$WORK/p10_fixed.glb" "$WORK/p10_fixed_blender_check.json" \
  > "$WORK/verify_blender.log" 2>&1
python3 "$HERE/08_verify.py" "$WORK/p10_orig.glb" "$WORK/p10_fixed.glb" "$OUT/scripts/verify_result.json"

# ---- optional variant: centre bang lock keeps its original (skin) paint, everything else identical
P10_KEEP_CENTER_LOCK=1 run_fix "$WORK/variant" "$WORK/variant/p10_variant.glb"
mkdir -p "$(dirname "$VARIANT")"
cp "$WORK/variant/p10_variant.glb" "$VARIANT"
blender -b --python "$HERE/render_views.py" -- --glb "$WORK/variant/p10_variant.glb" --cams "$HERE/cameras.json" \
  --out "$WORK/variant/r" --mode lit --views face_front,face_34L,face_34R,hairline_close,face_high > "$WORK/variant/render.log" 2>&1
python3 "$HERE/11_variant_compare.py" "$WORK/renders_before" "$WORK/renders_after" "$WORK/variant/r" \
  "$OUT/compare/variant_keep_center_lock.png" face_front_lit face_34L_lit face_34R_lit hairline_close_lit face_high_lit
blender -b --python "$HERE/verify_blender.py" -- "$WORK/variant/p10_variant.glb" \
  "$WORK/variant/p10_variant_blender_check.json" > "$WORK/variant/verify_blender.log" 2>&1
python3 "$HERE/08_verify.py" "$WORK/p10_orig.glb" "$WORK/variant/p10_variant.glb" "$OUT/scripts/verify_result_variant.json"
echo "done: $FIXED"
