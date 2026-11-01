# Rebuilds the player character from the old model (player_default.fbx) with clean, light topology and a standard
# humanoid (Mixamo-named) rig. Every part is retopologised: a quad sphere / capsule / torus is fitted to the old piece
# and shrink-wrapped onto it, so silhouette and proportions stay the same. Colours: each new face takes the palette cell
# the old surface had at the same spot (same textures, same tints). Parts stay the three tintable material slots
# (Body, Hands, Head) that PlayerLook colours.
#
# Usage (from the repository root):
#   blender --background --factory-startup --python Tools/Character/build_player.py -- <old.fbx> <out.fbx> [out.blend]
import bpy, bmesh, sys, math
import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

argv = [a for a in sys.argv[sys.argv.index("--") + 1:] if not a.startswith("--")]
OLD_FBX, OUT_FBX = argv[0], argv[1]
OUT_BLEND = argv[2] if len(argv) > 2 else None

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=OLD_FBX)
scene = bpy.context.scene
old_arm = next(o for o in scene.objects if o.type == 'ARMATURE')
old = next(o for o in scene.objects if o.type == 'MESH')

# ---------------------------------------------------------------- the old surface, in world space, split into pieces
old_bm = bmesh.new()
old_bm.from_mesh(old.data)
old_bm.transform(old.matrix_world)
old_bm.verts.ensure_lookup_table(); old_bm.faces.ensure_lookup_table()
uv_old = old_bm.loops.layers.uv.active
old_groups = {g.index: g.name for g in old.vertex_groups}

def old_bone(face):
    v = old.data.vertices[face.verts[0].index]
    g = max(v.groups, key=lambda g: g.weight) if v.groups else None
    return old_groups[g.group] if g else ""

islands = []
seen = set()
for v in old_bm.verts:
    if v.index in seen: continue
    isl = []; stack = [v]
    while stack:
        x = stack.pop()
        if x.index in seen: continue
        seen.add(x.index); isl.append(x.index)
        stack.extend(e.other_vert(x) for e in x.link_edges)
    islands.append(isl)

def centre(isl):
    co = np.array([old_bm.verts[i].co[:] for i in isl])
    return (co.min(0) + co.max(0)) / 2

# Named pieces (centres measured from the old model; +X is the character's right, it faces +Y).
NAMED = {
    "head": (0, 0, 1.524), "torso": (0, 0, 0.943),
    "eyeR": (0.115, 0.242, 1.578), "pupilR": (0.114, 0.293, 1.571), "lidRU": (0.114, 0.240, 1.632), "lidRL": (0.119, 0.250, 1.522),
    "eyeL": (-0.079, 0.286, 1.566), "pupilL": (-0.075, 0.327, 1.561), "lidLU": (-0.076, 0.282, 1.616), "lidLL": (-0.084, 0.296, 1.514),
    "shoulderR": (0.186, 0, 1.145), "shoulderL": (-0.186, 0, 1.145),
    "upperArmR": (0.332, 0, 1.169), "upperArmL": (-0.332, 0, 1.169), "lowerArmR": (0.518, 0, 1.183), "lowerArmL": (-0.518, 0, 1.183),
    "cuffR": (0.652, 0, 1.180), "cuffL": (-0.652, 0, 1.180), "gloveR": (0.861, 0.044, 1.199), "gloveL": (-0.836, 0.044, 1.199),
    "legR": (0.128, 0, 0.359), "legL": (-0.128, 0, 0.359), "footR": (0.132, 0.042, 0.053), "footL": (-0.140, 0.042, 0.053),
}
piece = {}
for name, c in NAMED.items():
    best = min(islands, key=lambda isl: np.linalg.norm(centre(isl) - np.array(c)))
    piece[name] = best
assert len({id(v) for v in piece.values()}) == len(NAMED), "two names matched the same piece"

def target_object(names):
    """A plain mesh object holding just these old pieces (shrink-wrap target)."""
    keep = set(i for n in names for i in piece[n])
    bm = old_bm.copy()
    bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.index not in keep], context='VERTS')
    me = bpy.data.meshes.new("T_" + "_".join(names)); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(me.name, me); scene.collection.objects.link(ob)
    return ob

def pca(names):
    co = np.array([old_bm.verts[i].co[:] for n in names for i in piece[n]])
    mean = co.mean(0)
    _, vecs = np.linalg.eigh(np.cov((co - mean).T))
    proj = (co - mean) @ vecs
    lo, hi = proj.min(0), proj.max(0)
    c = mean + vecs @ ((lo + hi) / 2)
    return Vector(c), Matrix([vecs[:, 0], vecs[:, 1], vecs[:, 2]]).transposed(), Vector((hi - lo) / 2)

# ---------------------------------------------------------------- primitives (quads only)
def quad_sphere(cuts):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=2.0)
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=cuts, use_grid_fill=True)
    for v in bm.verts:
        x, y, z = v.co
        v.co = Vector((x * math.sqrt(max(0, 1 - y * y / 2 - z * z / 2 + y * y * z * z / 3)),
                       y * math.sqrt(max(0, 1 - z * z / 2 - x * x / 2 + z * z * x * x / 3)),
                       z * math.sqrt(max(0, 1 - x * x / 2 - y * y / 2 + x * x * y * y / 3))))
    return bm

def ellipsoid(names, cuts, grow=1.08):
    c, rot, ext = pca(names)
    bm = quad_sphere(cuts)
    m = Matrix.Translation(c) @ rot.to_4x4() @ Matrix.Diagonal((ext.x * grow, ext.y * grow, ext.z * grow, 1))
    bm.transform(m)
    return bm

def capsule(points, radius, segments=12, cap_rings=2):
    """Rings along a polyline (the given points), hemispherical-ish ends closed with quads."""
    bm = bmesh.new()
    pts = [Vector(p) for p in points]
    axis = (pts[-1] - pts[0]).normalized()
    side = axis.cross(Vector((0, 0, 1)) if abs(axis.z) < 0.9 else Vector((1, 0, 0))).normalized()
    up = side.cross(axis).normalized()
    def ring(centre, r):
        return [bm.verts.new(centre + (side * math.cos(a) + up * math.sin(a)) * r)
                for a in (2 * math.pi * k / segments for k in range(segments))]
    rings = []
    for k in range(cap_rings, 0, -1):   # start cap
        f = k / (cap_rings + 1)
        rings.append(ring(pts[0] - axis * radius * math.sin(f * math.pi / 2) * 0.8, radius * math.cos(f * math.pi / 2)))
    rings += [ring(p, radius) for p in pts]
    for k in range(1, cap_rings + 1):   # end cap
        f = k / (cap_rings + 1)
        rings.append(ring(pts[-1] + axis * radius * math.sin(f * math.pi / 2) * 0.8, radius * math.cos(f * math.pi / 2)))
    for a, b in zip(rings, rings[1:]):
        for i in range(segments):
            bm.faces.new((a[i], a[(i + 1) % segments], b[(i + 1) % segments], b[i]))
    for r in (rings[0], rings[-1]):     # 12-gon closed by five quads
        for q in ((0, 1, 2, 3), (3, 4, 5, 6), (6, 7, 8, 9), (9, 10, 11, 0), (0, 3, 6, 9)):
            bm.faces.new([r[i] for i in q])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm

def torus(names, major_segments=16, minor_segments=8):
    c, rot, ext = pca(names)
    # The thin axis is the one with the smallest extent: the ring's axis.
    order = sorted(range(3), key=lambda i: ext[i])
    axis = rot.col[order[0]].normalized(); u = rot.col[order[1]].normalized(); w = axis.cross(u)
    tube = ext[order[0]]
    major = (ext[order[1]] + ext[order[2]]) / 2 - tube
    bm = bmesh.new()
    grid = []
    for i in range(major_segments):
        a = 2 * math.pi * i / major_segments
        radial = u * math.cos(a) + w * math.sin(a)
        grid.append([bm.verts.new(c + radial * (major + tube * math.cos(b)) + axis * tube * math.sin(b))
                     for b in (2 * math.pi * j / minor_segments for j in range(minor_segments))])
    for i in range(major_segments):
        for j in range(minor_segments):
            bm.faces.new((grid[i][j], grid[(i + 1) % major_segments][j], grid[(i + 1) % major_segments][(j + 1) % minor_segments], grid[i][(j + 1) % minor_segments]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm

def puck(names, cuts=6, exponent=0.55):
    """A rounded disc (superellipsoid) on the piece's thin axis: flat faces, rounded rim. The old cuff is a ring closed
    by discs, which reads as exactly this from outside (wrapping would cling to its hidden inner wall)."""
    c, rot, ext = pca(names)
    order = sorted(range(3), key=lambda i: ext[i])
    axis = rot.col[order[0]].normalized()
    half = ext[order[0]]; radius = (ext[order[1]] + ext[order[2]]) / 2
    bm = quad_sphere(cuts)
    for v in bm.verts:
        a = v.co.dot(axis); radial = v.co - axis * a; rho = radial.length
        new_a = half * math.copysign(abs(a) ** exponent, a)
        new_r = radial.normalized() * radius * rho ** exponent if rho > 1e-6 else Vector()
        v.co = c + axis * new_a + new_r
    return bm

def wrap(bm, names, passes=2, relax=0.5, radial=False):
    """Shrink-wraps the new piece onto the old one, relaxing in between so the quads stay even. Radial: each vertex
    moves along its direction from the piece's centre to the closest surface that way (no sliding, for small pieces)."""
    tgt = target_object(names)
    bvh = BVHTree.FromObject(tgt, bpy.context.evaluated_depsgraph_get())
    c = pca(names)[0]
    for p in range(passes):
        for v in bm.verts:
            if radial:
                d = (v.co - c).normalized()
                hits = [h for h in (bvh.ray_cast(v.co, d), bvh.ray_cast(v.co, -d)) if h[0] is not None]
                if hits:
                    v.co = min(hits, key=lambda h: (h[0] - v.co).length)[0]
                    continue
            hit = bvh.find_nearest(v.co)
            if hit[0] is not None: v.co = hit[0]
        if p < passes - 1:
            bmesh.ops.smooth_vert(bm, verts=bm.verts[:], factor=relax, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    # Two-sided fit: snapping alone leaves thin or bumpy regions of the old piece uncovered (lid edges, the shoe's
    # sole, the cuff's rim). Each old surface point pulls its nearest new vertex toward it; then relax and snap again.
    from mathutils.kdtree import KDTree
    # Even samples over the old surface (by area: old pieces are UV spheres whose poles bunch vertices, which pulled
    # lumps into the result when vertices were used).
    import random
    rng = random.Random(1)
    keep = set(i for n in names for i in piece[n])
    tris = []
    for f in old_bm.faces:
        if f.verts[0].index not in keep: continue
        vs = [v.co for v in f.verts]
        for k in range(1, len(vs) - 1):
            a_, b_, c_ = vs[0], vs[k], vs[k + 1]
            tris.append((a_, b_, c_, ((b_ - a_).cross(c_ - a_)).length / 2))
    total = sum(t[3] for t in tris)
    old_pts = []
    for a_, b_, c_, area in tris:
        n = area / total * 3000
        count = int(n) + (1 if rng.random() < n - int(n) else 0)
        for _ in range(count):
            r1, r2 = rng.random(), rng.random()
            if r1 + r2 > 1: r1, r2 = 1 - r1, 1 - r2
            old_pts.append(a_ + (b_ - a_) * r1 + (c_ - a_) * r2)
    for _ in range(6):
        kd = KDTree(len(bm.verts))
        for v in bm.verts: kd.insert(v.co, v.index)
        kd.balance()
        bm.verts.ensure_lookup_table()
        pull = {}
        for o in old_pts:
            co, i, dist = kd.find(o)
            if dist > 0.001:
                acc = pull.setdefault(i, [Vector(), 0]); acc[0] += o - co; acc[1] += 1
        for i, (d, n) in pull.items():
            bm.verts[i].co += d / n * 0.6
        bmesh.ops.smooth_vert(bm, verts=bm.verts[:], factor=0.25, use_axis_x=True, use_axis_y=True, use_axis_z=True)
        for v in bm.verts:
            hit = bvh.find_nearest(v.co)
            if hit[0] is not None: v.co = hit[0]
    bpy.data.objects.remove(tgt)
    return bm

# ---------------------------------------------------------------- build the parts
parts = {}   # name -> bmesh (world space)
parts["head"] = wrap(ellipsoid(["head"], 8), ["head"])
parts["torso"] = wrap(ellipsoid(["torso"], 10), ["torso"], passes=3)
for s in "RL":
    for piece_name, cuts, radial in (("eye" + s, 6, False), ("pupil" + s, 4, True), ("lid%sU" % s, 5, False), ("lid%sL" % s, 5, False)):
        parts[piece_name] = wrap(ellipsoid([piece_name], cuts), [piece_name], passes=1, radial=radial)
    parts["shoulder" + s] = wrap(ellipsoid(["shoulder" + s], 5), ["shoulder" + s])
    parts["foot" + s] = wrap(ellipsoid(["foot" + s], 6), ["foot" + s], passes=3)
    parts["cuff" + s] = puck(["cuff" + s])
    sx = 1 if s == "R" else -1
    # Arm: one capsule from inside the shoulder ball to the cuff, loops gathered round the elbow.
    arm_x = [0.235, 0.27, 0.31, 0.35, 0.38, 0.40, 0.42, 0.44, 0.47, 0.51, 0.56, 0.61]
    upper = pca(["upperArm" + s])[0]; lower = pca(["lowerArm" + s])[0]
    def arm_point(x):
        # Along the old tubes: upper arm rises slightly to the elbow, forearm runs level.
        t = (abs(x) - 0.235) / (0.425 - 0.235)
        return Vector((sx * x, 0.0, (1.150 + (1.183 - 1.150) * min(1, max(0, t)))))
    parts["arm" + s] = wrap(capsule([arm_point(x) for x in arm_x], 0.052, cap_rings=1), ["upperArm" + s, "lowerArm" + s])
    # Leg: hip to ankle, loops gathered round the knee.
    leg = pca(["leg" + s])[0]
    leg_z = [0.645, 0.60, 0.54, 0.48, 0.44, 0.415, 0.39, 0.365, 0.33, 0.27, 0.20, 0.14, 0.10, 0.075]
    parts["leg" + s] = wrap(capsule([Vector((leg.x, leg.y, z)) for z in leg_z], 0.065, cap_rings=1), ["leg" + s])

# Glove: the old one is already clean low-poly quads (palm, three fingers, thumb); copy it and add knuckle loops.
for s in "RL":
    sx = 1 if s == "R" else -1
    bm = bmesh.new()
    src = old_bm.copy(); src.verts.ensure_lookup_table()
    keep = set(piece["glove" + s])
    bmesh.ops.delete(src, geom=[v for v in src.verts if v.index not in keep], context='VERTS')
    for plane_x in (0.975,):
        geom = src.verts[:] + src.edges[:] + src.faces[:]
        bmesh.ops.bisect_plane(src, geom=geom, plane_co=(sx * plane_x, 0, 0), plane_no=(1, 0, 0))
    thumb = [v for v in src.verts if v.co.y > 0.09]
    geom = src.verts[:] + src.edges[:] + src.faces[:]
    bmesh.ops.bisect_plane(src, geom=geom, plane_co=(0, 0.15, 0), plane_no=(0, 1, 0))
    parts["glove" + s] = src

# ---------------------------------------------------------------- how far each part is from the old surface
if "--report" in sys.argv:
    for name, pb in parts.items():
        if name.startswith("glove"): continue
        new_tree = BVHTree.FromBMesh(pb)
        old_ids = [i for n in (["upperArm" + name[-1], "lowerArm" + name[-1]] if name.startswith("arm") else [name]) for i in piece[n]]
        step = max(1, len(old_ids) // 1500)
        d_old = sorted((new_tree.find_nearest(old_bm.verts[i].co)[3] or 0) for i in old_ids[::step])
        polys = [[v.index for v in f.verts] for f in old_bm.faces if f.verts[0].index in set(old_ids)]
        old_tree = BVHTree.FromPolygons([v.co for v in old_bm.verts], polys)
        d_new = sorted((old_tree.find_nearest(v.co)[3] or 0) for v in pb.verts)
        print(f"DEV {name:10s} old->new p95={d_old[int(len(d_old) * 0.95)] * 1000:.1f}mm max={d_old[-1] * 1000:.1f}mm"
              f"   new->old p95={d_new[int(len(d_new) * 0.95)] * 1000:.1f}mm max={d_new[-1] * 1000:.1f}mm")

# ---------------------------------------------------------------- one mesh: materials, UVs from the old surface
# Each new part takes colours only from the old piece(s) it was built from (the head's faces next to an eye must not
# pick up the eyeball's or lid's cell).
def sources(name):
    return ["upperArm" + name[-1], "lowerArm" + name[-1]] if name.startswith("arm") else [name]
_bvh_cache = {}
def source_bvh(name):
    key = tuple(sources(name))
    if key not in _bvh_cache:
        keep = set(i for n in key for i in piece[n])
        polys = [[v.index for v in f.verts] for f in old_bm.faces if f.verts[0].index in keep]
        face_ids = [f.index for f in old_bm.faces if f.verts[0].index in keep]
        _bvh_cache[key] = (BVHTree.FromPolygons([v.co for v in old_bm.verts], polys), face_ids)
    return _bvh_cache[key]
SLOTS = ["Body", "Hands", "Head"]
def part_slot(name):
    if name.startswith(("head", "eye", "pupil", "lid")): return 2
    if name.startswith(("cuff", "glove")): return 1
    return 0

me = bpy.data.meshes.new("Body")
bm = bmesh.new()
uv = bm.loops.layers.uv.new("UVMap")
part_of_face = []
vpart = []   # part name of each vertex, in creation order (= mesh vertex index)
for name, pb in parts.items():
    pb.verts.ensure_lookup_table()
    mapping = {}
    for v in pb.verts:
        mapping[v] = bm.verts.new(v.co); vpart.append(name)
    for f in pb.faces:
        try:
            nf = bm.faces.new([mapping[v] for v in f.verts])
        except ValueError:
            continue
        nf.material_index = part_slot(name)
        nf.smooth = not name.startswith("glove")
        part_of_face.append((nf, name))
bm.faces.ensure_lookup_table()
bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
for f, name in part_of_face:
    tree, face_ids = source_bvh(name)
    loc, _, idx, _ = tree.find_nearest(f.calc_center_median())
    of = old_bm.faces[face_ids[idx]]
    u = sum(l[uv_old].uv.x for l in of.loops) / len(of.loops)
    w = sum(l[uv_old].uv.y for l in of.loops) / len(of.loops)
    u = (math.floor(u * 32) + 0.5) / 32; w = (math.floor(w * 32) + 0.5) / 32   # centre of the 32x32 palette cell
    for l in f.loops: l[uv].uv = (u, w)
bm.to_mesh(me); bm.free()
for s in SLOTS:
    me.materials.append(bpy.data.materials.new(s))
body = bpy.data.objects.new("Body", me)

# ---------------------------------------------------------------- the rig (Mixamo names, the old joint positions)
for o in list(scene.objects): bpy.data.objects.remove(o)
arm_data = bpy.data.armatures.new("Armature")
rig = bpy.data.objects.new("Armature", arm_data)
scene.collection.objects.link(rig); scene.collection.objects.link(body)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='EDIT')
B = {}
def bone(name, head, tail, parent=None, connect=False):
    b = arm_data.edit_bones.new(name); b.head = Vector(head); b.tail = Vector(tail)
    if parent: b.parent = B[parent]; b.use_connect = connect
    B[name] = b
    return b
bone("Hips", (0, 0, 0.633), (0, 0, 0.78))
bone("Spine", (0, 0, 0.78), (0, 0, 0.93), "Hips", True)
bone("Spine1", (0, 0, 0.93), (0, 0, 1.06), "Spine", True)
bone("Spine2", (0, 0, 1.06), (0, 0, 1.20), "Spine1", True)
bone("Neck", (0, 0, 1.20), (0, 0, 1.267), "Spine2", True)
bone("Head", (0, 0, 1.267), (0, 0, 1.80), "Neck", True)
for side, sx in (("Right", 1), ("Left", -1)):
    bone(side + "Shoulder", (sx * 0.064, 0.012, 1.191), (sx * 0.217, 0.012, 1.162), "Spine2")
    bone(side + "Arm", (sx * 0.217, 0.012, 1.162), (sx * 0.404, -0.014, 1.173), side + "Shoulder", True)
    bone(side + "ForeArm", (sx * 0.404, -0.014, 1.173), (sx * 0.659, 0.023, 1.177), side + "Arm", True)
    bone(side + "Hand", (sx * 0.659, 0.023, 1.177), (sx * 0.88, 0.0, 1.205), side + "ForeArm", True)
    for finger, y in (("Index", 0.056), ("Middle", -0.011), ("Ring", -0.073)):
        pts = [(sx * 0.88, y, 1.207), (sx * 0.94, y, 1.200), (sx * 0.975, y, 1.18), (sx * 1.015, y, 1.16)]
        for k in range(3):
            bone(f"{side}Hand{finger}{k + 1}", pts[k], pts[k + 1], side + "Hand" if k == 0 else f"{side}Hand{finger}{k}", k > 0)
    tp = [(sx * 0.75, 0.085, 1.21), (sx * 0.75, 0.12, 1.205), (sx * 0.75, 0.155, 1.20), (sx * 0.75, 0.195, 1.19)]
    for k in range(3):
        bone(f"{side}HandThumb{k + 1}", tp[k], tp[k + 1], side + "Hand" if k == 0 else f"{side}HandThumb{k}", k > 0)
    leg_x = sx * 0.132
    bone(side + "UpLeg", (leg_x, -0.027, 0.626), (leg_x, 0.033, 0.403), "Hips")
    bone(side + "Leg", (leg_x, 0.033, 0.403), (leg_x, -0.043, 0.122), side + "UpLeg", True)
    bone(side + "Foot", (leg_x, -0.043, 0.122), (leg_x, 0.10, 0.03), side + "Leg", True)
    bone(side + "ToeBase", (leg_x, 0.10, 0.03), (leg_x, 0.19, 0.03), side + "Foot", True)
for b in arm_data.edit_bones:   # rolls: Z up for the spine/legs, Z forward-ish otherwise; humanoid import ignores them
    b.roll = 0.0
bpy.ops.object.mode_set(mode='OBJECT')
body.parent = rig
mod = body.modifiers.new("Armature", 'ARMATURE'); mod.object = rig

# ---------------------------------------------------------------- skin weights
def smooth01(x): x = max(0.0, min(1.0, x)); return x * x * (3 - 2 * x)
groups = {}
def vg(name):
    if name not in groups: groups[name] = body.vertex_groups.new(name=name)
    return groups[name]
def blend(t, edge, width):
    """0 below edge-width, 1 above edge+width."""
    return smooth01((t - (edge - width)) / (2 * width))

for v in me.vertices:
    name = vpart[v.index]; p = v.co
    side = "Right" if p.x > 0 else "Left"; ax = abs(p.x)
    w = {}
    if name.startswith(("head", "eye", "pupil", "lid")):
        w["Head"] = 1
    elif name == "torso":
        z = p.z
        heights = [("Hips", 0.633), ("Spine", 0.80), ("Spine1", 0.95), ("Spine2", 1.10)]
        for (a, za), (b, zb) in zip(heights, heights[1:]):
            if za <= z <= zb:
                t = smooth01((z - za) / (zb - za)); w[a] = 1 - t; w[b] = t
        if z < 0.633: w = {"Hips": 1}
        if z > 1.10: w = {"Spine2": 1}
    elif name.startswith("shoulder"):
        w[side + "Shoulder"] = 1
    elif name.startswith("arm"):
        t_elbow = blend(ax, 0.412, 0.05); t_wrist = blend(ax, 0.62, 0.015)   # blend about one tube radius each way
        w[side + "Arm"] = 1 - t_elbow
        w[side + "ForeArm"] = t_elbow * (1 - t_wrist)
        w[side + "Hand"] = t_wrist
    elif name.startswith("cuff"):
        w[side + "Hand"] = 1
    elif name.startswith("glove"):
        if p.y > 0.09 and ax < 0.80:   # thumb
            k = 1 if p.y < 0.12 else 2 if p.y < 0.155 else 3
            w[f"{side}HandThumb{k}"] = 1 if k > 1 or p.y > 0.10 else 0.5
            if p.y <= 0.10: w[side + "Hand"] = 0.5
        elif ax > 0.88:
            finger = "Index" if p.y > 0.02 else "Middle" if p.y > -0.04 else "Ring"
            k = 1 if ax < 0.94 else 2 if ax < 0.975 else 3
            w[f"{side}Hand{finger}{k}"] = 1
        else:
            w[side + "Hand"] = 1
    elif name.startswith("leg"):
        t_knee = blend(-p.z, -0.403, 0.06); t_ankle = blend(-p.z, -0.12, 0.02)
        w[side + "UpLeg"] = 1 - t_knee
        w[side + "Leg"] = t_knee * (1 - t_ankle)
        w[side + "Foot"] = t_ankle
    elif name.startswith("foot"):
        t = blend(p.y, 0.10, 0.04) * 0.6
        w[side + "Foot"] = 1 - t; w[side + "ToeBase"] = t
    for g, x in w.items():
        if x > 0.001: vg(g).add([v.index], x, 'REPLACE')

# ---------------------------------------------------------------- export
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True); body.select_set(True)
bpy.context.view_layer.objects.active = rig
if OUT_BLEND:
    bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
bpy.ops.export_scene.fbx(filepath=OUT_FBX, use_selection=True, object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False,
                         bake_anim=False, mesh_smooth_type='FACE', use_armature_deform_only=True,
                         apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y')
tris = sum(len(p.vertices) - 2 for p in me.polygons)
print(f"BUILD verts={len(me.vertices)} faces={len(me.polygons)} tris={tris} bones={len(arm_data.bones)} "
      f"sides={sorted(set(len(p.vertices) for p in me.polygons))} unweighted={sum(1 for v in me.vertices if not v.groups)}")
