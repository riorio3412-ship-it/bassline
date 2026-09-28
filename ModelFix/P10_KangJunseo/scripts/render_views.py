"""Blender (4.0, headless) render script for the P10 model inspection.

Usage:
  blender -b --python render_views.py -- --glb model.glb --cams cameras.json \
          --out out_dir --mode lit|unlit|aux [--views name1,name2] [--samples N]

Modes
  lit    : Cycles, Principled BSDF from the glTF (roughness 0.9, metallic 0),
           camera-relative soft key + fill + rim area lights and a neutral grey
           ambient world. Approximates a neutral in-game look.
  unlit  : Cycles, every material replaced by Emission(baseColor texture), strength 1,
           Standard view transform -> pixels show the texture colours as-is.
  aux    : 1 spp, no pixel filter, EXR float output encoding per pixel
           R = u, G = v, B = (part_index + 1) / 64, A = coverage.
           Used to map render pixels back to texture texels.

Cameras are given in glTF coordinates (Y up, +Z = character front) and converted
to Blender's Z-up frame (x, y, z)_gltf -> (x, -z, y)_blender, the same conversion
the glTF importer applies to the mesh.
"""
import json
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    args = {"views": None, "samples": None, "hide": None, "suffix": ""}
    i = 0
    while i < len(argv):
        k = argv[i].lstrip("-")
        args[k] = argv[i + 1]
        i += 2
    return args


def g2b(v):
    """glTF (Y-up) -> Blender (Z-up)."""
    return Vector((v[0], -v[2], v[1]))


def look_at_matrix(eye, target, up):
    eye, target, up = Vector(eye), Vector(target), Vector(up)
    f = (target - eye).normalized()
    r = f.cross(up).normalized()
    u = r.cross(f).normalized()
    m = Matrix.Identity(4)
    # camera looks down its local -Z, local +Y is image up, local +X is image right
    for i in range(3):
        m[i][0] = r[i]
        m[i][1] = u[i]
        m[i][2] = -f[i]
        m[i][3] = eye[i]
    return m


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def import_glb(path):
    bpy.ops.import_scene.gltf(filepath=path)
    parts = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    for o in parts:
        name = o.name
        # object names are tripo_part_N (possibly with .001 suffixes)
        try:
            o.pass_index = int(name.split("tripo_part_")[1].split(".")[0]) + 1
        except Exception:
            o.pass_index = 0
    return parts


def find_base_color_image(mat):
    if not mat or not mat.use_nodes:
        return None
    for n in mat.node_tree.nodes:
        if n.type == "TEX_IMAGE" and n.image is not None:
            return n
    return None


def setup_unlit(parts):
    done = set()
    for o in parts:
        for slot in o.material_slots:
            mat = slot.material
            if mat is None or mat.name in done:
                continue
            done.add(mat.name)
            tex = find_base_color_image(mat)
            nt = mat.node_tree
            out = [n for n in nt.nodes if n.type == "OUTPUT_MATERIAL"][0]
            em = nt.nodes.new("ShaderNodeEmission")
            em.inputs["Strength"].default_value = 1.0
            if tex is not None:
                nt.links.new(tex.outputs["Color"], em.inputs["Color"])
            nt.links.new(em.outputs["Emission"], out.inputs["Surface"])


def setup_aux(parts):
    for o in parts:
        mat = bpy.data.materials.new(f"aux_{o.name}")
        mat.use_nodes = True
        nt = mat.node_tree
        for n in list(nt.nodes):
            nt.nodes.remove(n)
        out = nt.nodes.new("ShaderNodeOutputMaterial")
        uvn = nt.nodes.new("ShaderNodeUVMap")
        sep = nt.nodes.new("ShaderNodeSeparateXYZ")
        comb = nt.nodes.new("ShaderNodeCombineXYZ")
        val = nt.nodes.new("ShaderNodeValue")
        val.outputs[0].default_value = o.pass_index / 64.0
        em = nt.nodes.new("ShaderNodeEmission")
        em.inputs["Strength"].default_value = 1.0
        nt.links.new(uvn.outputs["UV"], sep.inputs[0])
        nt.links.new(sep.outputs[0], comb.inputs[0])
        nt.links.new(sep.outputs[1], comb.inputs[1])
        nt.links.new(val.outputs[0], comb.inputs[2])
        nt.links.new(comb.outputs[0], em.inputs["Color"])
        nt.links.new(em.outputs["Emission"], out.inputs["Surface"])
        o.data.materials.clear()
        o.data.materials.append(mat)


def setup_world(mode):
    world = bpy.data.worlds.new("World")
    bpy.context.scene.world = world
    world.use_nodes = True
    nt = world.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputWorld")
    if mode == "aux":
        bg = nt.nodes.new("ShaderNodeBackground")
        bg.inputs["Color"].default_value = (0, 0, 0, 1)
        nt.links.new(bg.outputs[0], out.inputs[0])
        return
    # camera sees a flat neutral grey; lighting sees a dimmer neutral ambient
    lp = nt.nodes.new("ShaderNodeLightPath")
    cam_bg = nt.nodes.new("ShaderNodeBackground")
    cam_bg.inputs["Color"].default_value = (0.20, 0.20, 0.20, 1)  # linear -> ~sRGB 124
    amb = nt.nodes.new("ShaderNodeBackground")
    amb.inputs["Color"].default_value = (0.35, 0.35, 0.35, 1)
    amb.inputs["Strength"].default_value = 1.0 if mode == "lit" else 0.0
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(lp.outputs["Is Camera Ray"], mix.inputs[0])
    nt.links.new(amb.outputs[0], mix.inputs[1])
    nt.links.new(cam_bg.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs[0])


def add_area_light(name, size, power, color=(1, 1, 1)):
    ld = bpy.data.lights.new(name, "AREA")
    ld.shape = "DISK"
    ld.size = size
    ld.energy = power
    ld.color = color
    lo = bpy.data.objects.new(name, ld)
    bpy.context.scene.collection.objects.link(lo)
    return lo


def place_rig(lights, cam_mat, target_b, dist):
    """Camera-relative light rig: key upper-left, fill right, rim behind-above."""
    r = Vector((cam_mat[0][0], cam_mat[1][0], cam_mat[2][0]))
    u = Vector((cam_mat[0][1], cam_mat[1][1], cam_mat[2][1]))
    back = Vector((cam_mat[0][2], cam_mat[1][2], cam_mat[2][2]))  # from target toward camera
    key, fill, rim = lights
    specs = [
        (key, (-0.75 * r + 0.65 * u + 0.9 * back)),
        (fill, (0.95 * r + 0.10 * u + 0.8 * back)),
        (rim, (0.35 * r + 0.80 * u - 1.00 * back)),
    ]
    for lo, d in specs:
        d = d.normalized()
        pos = target_b + d * dist
        lo.matrix_world = look_at_matrix(pos, target_b, Vector((0, 0, 1)) if abs(d.z) < 0.95 else Vector((0, 1, 0)))


def main():
    a = parse_args()
    mode = a["mode"]
    cams = json.load(open(a["cams"]))
    views = a["views"].split(",") if a.get("views") else list(cams.keys())
    os.makedirs(a["out"], exist_ok=True)

    clear_scene()
    parts = import_glb(a["glb"])
    if a.get("hide"):
        hide = {int(x) for x in a["hide"].split(",")}
        for o in list(parts):
            if o.pass_index - 1 in hide:
                bpy.data.objects.remove(o, do_unlink=True)
                parts.remove(o)
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.view_settings.exposure = 0.0
    scene.view_settings.gamma = 1.0
    scene.display_settings.display_device = "sRGB"
    scene.cycles.use_adaptive_sampling = False
    scene.cycles.max_bounces = 4
    scene.cycles.diffuse_bounces = 2
    scene.cycles.glossy_bounces = 2
    scene.cycles.transmission_bounces = 2
    scene.cycles.transparent_max_bounces = 4

    if mode == "lit":
        # this Ubuntu Blender build has no OpenImageDenoise -> plain path tracing,
        # enough samples for a clean image (no denoiser blur on texture detail)
        scene.cycles.samples = int(a["samples"] or 128)
        scene.cycles.use_denoising = False
        scene.cycles.filter_width = 1.5
        scene.render.image_settings.file_format = "PNG"
        scene.render.image_settings.color_mode = "RGB"
        scene.render.image_settings.color_depth = "8"
    elif mode == "unlit":
        setup_unlit(parts)
        scene.cycles.samples = int(a["samples"] or 16)
        scene.cycles.use_denoising = False
        scene.cycles.filter_width = 1.5
        scene.render.image_settings.file_format = "PNG"
        scene.render.image_settings.color_mode = "RGB"
        scene.render.image_settings.color_depth = "8"
    else:  # aux
        setup_aux(parts)
        scene.cycles.samples = 1
        scene.cycles.use_denoising = False
        scene.cycles.filter_width = 0.01
        scene.cycles.max_bounces = 0
        scene.view_settings.view_transform = "Standard"
        scene.render.image_settings.file_format = "OPEN_EXR"
        scene.render.image_settings.color_mode = "RGBA"
        scene.render.image_settings.color_depth = "32"
        scene.render.image_settings.exr_codec = "ZIP"
        scene.render.film_transparent = True
    setup_world(mode)

    cam_data = bpy.data.cameras.new("Cam")
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    lights = None
    if mode == "lit":
        lights = [add_area_light("Key", 0.6, 1.0), add_area_light("Fill", 0.8, 1.0),
                  add_area_light("Rim", 0.4, 1.0)]

    for vname in views:
        c = cams[vname]
        eye_b, tgt_b, up_b = g2b(c["eye"]), g2b(c["target"]), g2b(c.get("up", [0, 1, 0]))
        m = look_at_matrix(eye_b, tgt_b, up_b)
        cam.matrix_world = m
        if c.get("ortho"):
            cam_data.type = "ORTHO"
            cam_data.ortho_scale = c["ortho"]
        else:
            cam_data.type = "PERSP"
            cam_data.lens = c.get("lens", 85)
            cam_data.sensor_fit = "AUTO"
            cam_data.sensor_width = 36
        cam_data.clip_start = 0.005
        cam_data.clip_end = 50
        scene.render.resolution_x = c["res"][0]
        scene.render.resolution_y = c["res"][1]
        scene.render.resolution_percentage = 100
        if lights:
            # light distance / power scale with the framing so exposure is consistent
            dist = 1.6
            key, fill, rim = lights
            key.data.energy, fill.data.energy, rim.data.energy = 11.0, 4.0, 4.0
            key.data.size, fill.data.size, rim.data.size = 1.2, 1.6, 0.8
            place_rig(lights, m, tgt_b, dist)
        ext = "exr" if mode == "aux" else "png"
        scene.render.filepath = os.path.join(a["out"], f"{vname}_{mode}{a['suffix']}.{ext}")
        bpy.ops.render.render(write_still=True)
        print("RENDERED", scene.render.filepath, flush=True)


main()
