#!/bin/bash
# Render every camera in cameras.json in unlit, aux and lit modes.
# usage: render_all.sh <model.glb> <out_dir>
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"
GLB="$1"; OUT="$2"
mkdir -p "$OUT"
for MODE in unlit aux lit; do
  blender -b --python "$HERE/render_views.py" -- --glb "$GLB" --cams "$HERE/cameras.json" --out "$OUT" --mode $MODE > "$OUT/render_$MODE.log" 2>&1
  grep -c RENDERED "$OUT/render_$MODE.log"
done
