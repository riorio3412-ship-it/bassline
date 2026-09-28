"""Blender re-import check: blender -b --python verify_blender.py -- <model.glb> <out.json>
Imports the GLB with Blender's glTF importer and records object / triangle
counts and, per material, the linked base-colour image and its resolution."""
import json
import sys

import bpy

argv = sys.argv[sys.argv.index("--") + 1:]
glb, out = argv[0], argv[1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb)
objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
tris = 0
verts = 0
mats = {}
for o in objs:
    me = o.data
    me.calc_loop_triangles()
    tris += len(me.loop_triangles)
    verts += len(me.vertices)
    for slot in o.material_slots:
        m = slot.material
        img = None
        if m and m.use_nodes:
            for n in m.node_tree.nodes:
                if n.type == "TEX_IMAGE" and n.image is not None:
                    img = n.image
                    # make sure the pixels really decode
                    _ = img.pixels[0]
                    break
        mats[m.name if m else "none"] = [img.name, list(img.size)] if img else None
res = {"mesh_objects": len(objs), "triangles": tris, "vertices_merged_by_importer": verts,
       "materials": len(mats), "materials_without_image": [k for k, v in mats.items() if v is None],
       "image_sizes": sorted({tuple(v[1]) for v in mats.values() if v})}
res["image_sizes"] = [list(s) for s in res["image_sizes"]]
json.dump(res, open(out, "w"), indent=1)
print("BLENDER_CHECK", json.dumps(res))
