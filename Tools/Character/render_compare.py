# Renders a player FBX like the game shows it: palette textures per part (body/hands tintable, head palette), tinted with
# the default outfit (Ocean body, Sunny hands, White head), from front, side, back and 3/4, plus close-ups.
# Parts come from material slot names (Body/Hands/Head) when present, else from the bone each face follows (old model).
# Usage: blender --background --factory-startup --python blender_render.py -- <fbx> <out_prefix> [pose_action_fbx]
import bpy, sys, math
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
fbx, prefix = argv[0], argv[1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
scene = bpy.context.scene
base = "C:/Data/Projects/Unity/chaos-challenges/Assets/_Project/Models/Charachters/"
tex = {"head": bpy.data.images.load(base + "1024 palette.png"), "tint": bpy.data.images.load(base + "palette_tintable.png")}
TINT = {"Body": (0.24, 0.52, 0.95), "Hands": (1.0, 0.84, 0.22), "Head": (0.96, 0.96, 0.96)}

def make(name):
    m = bpy.data.materials.new("R_" + name)
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    t = nt.nodes.new("ShaderNodeTexImage"); t.image = tex["head" if name == "Head" else "tint"]; t.interpolation = 'Closest'
    mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = 'RGBA'; mix.blend_type = 'MULTIPLY'; mix.inputs[0].default_value = 1.0
    nt.links.new(t.outputs[0], mix.inputs[6]); mix.inputs[7].default_value = (*TINT[name], 1)
    nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.5
    return m
mats = {n: make(n) for n in TINT}

for ob in [o for o in scene.objects if o.type == 'MESH']:
    names = [s.material.name if s.material else "" for s in ob.material_slots]
    by_slot = [next((p for p in TINT if p.lower() in n.lower()), None) for n in names]
    groups = {g.index: g.name for g in ob.vertex_groups}
    slot_of = [poly.material_index for poly in ob.data.polygons]
    ob.data.materials.clear()
    for p in TINT: ob.data.materials.append(mats[p])
    order = list(TINT)
    for poly in ob.data.polygons:
        si = slot_of[poly.index]
        part = by_slot[si] if si < len(by_slot) and by_slot[si] else None
        if part is None:
            v = ob.data.vertices[poly.vertices[0]]
            bone = max(v.groups, key=lambda g: g.weight) if v.groups else None
            bone = groups[bone.group] if bone else ""
            part = "Head" if bone.startswith("Head") else "Hands" if bone.startswith("Hand") else "Body"
        poly.material_index = order.index(part)

# Optional test pose ("pose" as the third argument): bent elbows and knees, one arm raised, head turned, torso twisted.
arm = next((o for o in scene.objects if o.type == 'ARMATURE'), None)
POSED = len(argv) > 2 and argv[2] == "pose"
if POSED and arm:
    from mathutils import Matrix
    names = {b.name for b in arm.pose.bones}
    def find(*cands):
        return next((c for c in cands if c in names), None)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='POSE')
    def turn(bone, axis, deg):
        if bone is None: return
        pb = arm.pose.bones[bone]
        head = pb.head.copy()
        R = Matrix.Rotation(math.radians(deg), 4, axis)
        pb.matrix = Matrix.Translation(head) @ R @ Matrix.Translation(-head) @ pb.matrix
        bpy.context.view_layer.update()
    # World axes: X right, Y forward (the character faces +Y), Z up.
    turn(find("Spine1", "Spine"), 'Z', 15)
    turn(find("Head"), 'Z', -20)
    for side, sx in (("Right", 1), ("Left", -1)):
        old_side = "R" if side == "Right" else "L"
        turn(find(side + "Arm", "UpperArm." + old_side), 'Y', sx * 60)          # arms down by the sides
        turn(find(side + "Arm", "UpperArm." + old_side), 'Z', sx * 25)
        turn(find(side + "ForeArm", "LowerArm." + old_side), 'X', 95)          # forearms bent forward
    turn(find("RightUpLeg", "UpperLeg.R"), 'X', 75)                             # right knee raised
    turn(find("RightLeg", "LowerLeg.R"), 'X', -105)
    turn(find("LeftLeg", "LowerLeg.L"), 'X', -30)
    for f in ("Index", "Middle", "Ring"):
        for k in (1, 2, 3): turn(find(f"RightHand{f}{k}"), 'X', 30)
    bpy.context.view_layer.update()
    ELBOW = arm.matrix_world @ arm.pose.bones[find("RightForeArm", "LowerArm.R")].head
    KNEE = arm.matrix_world @ arm.pose.bones[find("RightLeg", "LowerLeg.R")].head
    bpy.ops.object.mode_set(mode='OBJECT')

world = bpy.data.worlds.new("W"); scene.world = world
world.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.6, 0.7, 1); world.node_tree.nodes["Background"].inputs[1].default_value = 0.9
sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", 'SUN')); scene.collection.objects.link(sun)
sun.data.energy = 3.0; sun.rotation_euler = (math.radians(50), 0, math.radians(30))
scene.render.engine = 'BLENDER_EEVEE'
scene.render.resolution_x, scene.render.resolution_y = 600, 800
scene.render.film_transparent = False
cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")); scene.collection.objects.link(cam); scene.camera = cam

def shoot(name, target, yaw, dist, ortho=None, pitch=8, res=(600, 800)):
    scene.render.resolution_x, scene.render.resolution_y = res
    cam.data.type = 'ORTHO' if ortho else 'PERSP'
    if ortho: cam.data.ortho_scale = ortho
    else: cam.data.lens = 50
    # Character faces +Y in Blender (FBX import); yaw 0 = looking at its face.
    d = Vector((math.sin(math.radians(yaw)) * math.cos(math.radians(pitch)), math.cos(math.radians(yaw)) * math.cos(math.radians(pitch)), math.sin(math.radians(pitch))))
    cam.location = Vector(target) + d * dist
    cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = f"{prefix}_{name}.png"
    bpy.ops.render.render(write_still=True)

if POSED:
    shoot("posed_front", (0, 0, 0.92), 0, 6, ortho=2.3)
    shoot("posed_34", (0, 0, 0.92), 50, 5.2, pitch=12)
    shoot("posed_elbow", tuple(ELBOW), 75, 1.3, pitch=15, res=(600, 600))
    shoot("posed_knee", tuple(KNEE), 90, 1.3, pitch=5, res=(600, 600))
    sys.exit(0)
shoot("front", (0, 0, 0.92), 0, 6, ortho=2.3)
shoot("side", (0, 0, 0.92), 90, 6, ortho=2.3)
shoot("back", (0, 0, 0.92), 180, 6, ortho=2.3)
shoot("34", (0, 0, 0.92), 35, 5.2, pitch=12)
shoot("head", (0, 0.05, 1.53), 25, 1.6, pitch=8, res=(600, 600))
shoot("hand", (0.72, 0.0, 1.18), 20, 1.0, pitch=35, res=(600, 600))
shoot("foot", (0.13, 0.05, 0.12), 40, 1.0, pitch=25, res=(600, 600))
