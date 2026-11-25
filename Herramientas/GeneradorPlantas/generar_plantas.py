"""Generador de plantas para Glossy Horizons (Blender, se ejecuta sin interfaz):
  blender -b -P generar_plantas.py -- <carpeta_salida> <atlas.png>
Crea, exporta a FBX y renderiza una vista previa de:
  Arbusto_Redondo_1/2  - ramas que se dividen (madera) y cientos de hojas sueltas, forma redondeada
  Arbusto_Flores_1/2   - igual pero más suelto y con flores rosadas en las puntas
  Helecho_1/2          - frondas que salen del centro y se arquean hacia afuera
  Planta_Interior_1/2  - tallos verdes con hojas grandes y curvas (tipo planta de oficina)
Cada planta: Z arriba, base en el origen, ~1 m de alto. Materiales: "Tallo_Madera"/"Tallo_Verde" y "Hojas".
Las hojas llevan normales "esféricas" (luz de volumen) y color de vértice (tinte + alfa = cuánto se mece)."""
import bpy, bmesh, math, random, sys, os
from mathutils import Vector, Matrix, Quaternion

argv = sys.argv[sys.argv.index("--") + 1:]
SALIDA, ATLAS = argv[0], argv[1]
os.makedirs(SALIDA, exist_ok=True)

# Regiones del atlas (u0, v0, u1, v1) con origen abajo-izquierda
REG = {"hoja": (0.0, 0.5, 0.5, 1.0), "helecho": (0.5, 0.5, 1.0, 1.0),
       "grande": (0.0, 0.0, 0.5, 0.5), "flor": (0.5, 0.0, 1.0, 0.5)}


class Planta:
    def __init__(self, centro_luz, alto):
        self.bm = bmesh.new()
        self.col = self.bm.loops.layers.color.new("Col")
        self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.normales = {}                       # cara -> normal por vértice (solo hojas)
        self.centro_luz = Vector(centro_luz)
        self.alto = alto

    def _cara(self, pts, uvs, mat, tinte, normales=None):
        vs = [self.bm.verts.new(p) for p in pts]
        f = self.bm.faces.new(vs)
        f.material_index = mat
        for k, loop in enumerate(f.loops):
            loop[self.uv].uv = uvs[k]
            z = loop.vert.co.z
            loop[self.col] = (tinte[0], tinte[1], tinte[2], max(0.0, min(1.0, z / self.alto)))
        if normales: self.normales[f] = normales
        return f

    def tubo(self, p0, p1, r0, r1, mat, lados=5):
        eje = (p1 - p0)
        if eje.length < 1e-4: return
        d = eje.normalized()
        a = d.orthogonal().normalized(); b = d.cross(a)
        anillo0 = [p0 + (a * math.cos(t) + b * math.sin(t)) * r0 for t in [k / lados * 2 * math.pi for k in range(lados)]]
        anillo1 = [p1 + (a * math.cos(t) + b * math.sin(t)) * r1 for t in [k / lados * 2 * math.pi for k in range(lados)]]
        for k in range(lados):
            k2 = (k + 1) % lados
            self._cara([anillo0[k], anillo0[k2], anillo1[k2], anillo1[k]],
                       [(0.5, 0.5)] * 4, mat, (1, 1, 1))

    def hoja(self, base, largo_dir, cara_n, largo, ancho, region, tinte, curva=0.0, segmentos=1):
        """Tarjeta de hoja: la base en 'base', la punta hacia 'largo_dir'. 'cara_n' = hacia dónde mira."""
        u0, v0, u1, v1 = REG[region]
        eje = largo_dir.normalized()
        lado = cara_n.cross(eje)
        if lado.length < 1e-4: lado = eje.orthogonal()
        lado.normalize()
        n_cara = eje.cross(lado).normalized()
        anteriores = None
        for s in range(segmentos + 1):
            t = s / segmentos
            # se curva hacia abajo de la cara a lo largo de la hoja
            centro = base + eje * (largo * t) - n_cara * (curva * largo * t * t)
            izq, der = centro - lado * (ancho * 0.5), centro + lado * (ancho * 0.5)
            vv = v0 + (v1 - v0) * t
            actuales = (izq, der, (u0, vv), (u1, vv))
            if anteriores:
                pts = [anteriores[0], anteriores[1], actuales[1], actuales[0]]
                uvs = [anteriores[2], anteriores[3], actuales[3], actuales[2]]
                nrm = [(p - self.centro_luz).normalized() for p in pts]
                self._cara(pts, uvs, 1, tinte, nrm)
            anteriores = actuales

    def objeto(self, nombre, mats):
        me = bpy.data.meshes.new(nombre)
        self.bm.to_mesh(me)
        # normales personalizadas: hojas esféricas, tallos según su cara
        normales = []
        caras = list(self.bm.faces)
        for f in caras:
            if f in self.normales: normales.extend(self.normales[f])
            else: normales.extend([f.normal.copy()] * len(f.verts))
        me.normals_split_custom_set([tuple(n) for n in normales])
        for m in mats: me.materials.append(m)
        ob = bpy.data.objects.new(nombre, me)
        bpy.context.scene.collection.objects.link(ob)
        self.bm.free()
        return ob


def tinte_hoja(rnd):
    return (rnd.uniform(0.82, 1.05), rnd.uniform(0.9, 1.08), rnd.uniform(0.8, 1.0))


# ---------- Arbusto (redondo o con flores) ----------
def arbusto(nombre, semilla, flores=False, grande=False):
    rnd = random.Random(semilla)
    R, H = (0.55, 0.9) if not flores else (0.6, 1.05)
    if grande: R, H = R * 1.6, H * 1.5
    centro = Vector((0, 0, H * 0.48))
    pl = Planta((0, 0, H * 0.35), H)

    def dentro(p):
        return (p.x / R) ** 2 + (p.y / R) ** 2 + ((p.z - centro.z) / (H * 0.52)) ** 2 <= 1.0

    puntas = []
    def crecer(p, d, largo, radio, nivel):
        fin = p + d * largo
        while not dentro(fin) and largo > 0.03:
            largo *= 0.8; fin = p + d * largo
        pl.tubo(p, fin, radio, radio * 0.7, 0)
        if nivel >= 3:
            puntas.append((p, fin, d))
        if nivel == 4:
            return
        for _ in range(3 if nivel < 3 else 2):
            eje = d.orthogonal().normalized()
            eje.rotate(Quaternion(d, rnd.uniform(0, 2 * math.pi)))
            nd = Quaternion(eje, math.radians(rnd.uniform(25, 48))) @ d
            afuera = (fin - centro); afuera.z *= 0.5
            nd = (nd + afuera.normalized() * 0.35).normalized()
            crecer(fin, nd, largo * rnd.uniform(0.66, 0.78), radio * 0.62, nivel + 1)

    for k in range(5):
        ang = k / 5 * 2 * math.pi + rnd.uniform(-0.3, 0.3)
        base = Vector((math.cos(ang) * 0.04, math.sin(ang) * 0.04, 0))
        d = Vector((math.cos(ang) * 0.35, math.sin(ang) * 0.35, 1)).normalized()
        crecer(base, d, H * 0.32, 0.022, 1)

    for (p, fin, d) in puntas:
        for _ in range(26 if not flores else 22):
            t = rnd.uniform(0.25, 1.0)
            pos = p.lerp(fin, t) + Vector((rnd.gauss(0, 0.035), rnd.gauss(0, 0.035), rnd.gauss(0, 0.03)))
            afuera = (pos - centro).normalized()
            direccion = (afuera * 0.8 + d * 0.5 + Vector((rnd.gauss(0, 0.5), rnd.gauss(0, 0.5), rnd.gauss(0, 0.4)))).normalized()
            cara = (afuera + Vector((0, 0, 0.6)) + Vector((rnd.gauss(0, 0.6), rnd.gauss(0, 0.6), 0))).normalized()
            largo = rnd.uniform(0.09, 0.14)
            pl.hoja(pos, direccion, cara, largo, largo, "hoja", tinte_hoja(rnd))
        if flores and rnd.random() < 0.7 and (fin - centro).length > R * 0.4:
            afuera = (fin - centro).normalized()
            tam = rnd.uniform(0.08, 0.12)
            # la flor mira hacia afuera: su "largo" va de abajo hacia arriba de la flor
            arriba = afuera.orthogonal().normalized()
            base = fin + afuera * 0.02 - arriba * (tam * 0.5)
            pl.hoja(base, arriba, afuera, tam, tam, "flor", (rnd.uniform(0.9, 1.0), rnd.uniform(0.85, 1.0), 1.0))
    return pl.objeto(nombre, [MAT["Tallo_Madera"], MAT["Hojas"]])


# ---------- Helecho ----------
def helecho(nombre, semilla):
    rnd = random.Random(semilla)
    H = 0.7
    pl = Planta((0, 0, 0.1), H)
    n = rnd.randint(20, 26)
    for k in range(n):
        ang = k / n * 2 * math.pi + rnd.uniform(-0.15, 0.15)
        alcance = rnd.uniform(0.5, 0.72)
        sube = rnd.uniform(0.45, 0.65)
        afuera = Vector((math.cos(ang), math.sin(ang), 0))
        segs = 8
        prev = None
        for s in range(segs + 1):
            t = s / segs
            p = afuera * (alcance * t) + Vector((0, 0, 0.03 + sube * t * (1.15 - t) * 1.6))
            if prev is not None:
                pass
            prev = p
        # tira curva: se arma como hoja segmentada que sigue el arco
        base = Vector((0, 0, 0.03))
        puntos = [afuera * (alcance * (s / segs)) + Vector((0, 0, 0.03 + sube * (s / segs) * (1.15 - s / segs) * 1.6)) for s in range(segs + 1)]
        u0, v0, u1, v1 = REG["helecho"]
        lado = Vector((0, 0, 1)).cross(afuera).normalized()
        for s in range(segs):
            t0, t1 = s / segs, (s + 1) / segs
            w0, w1 = 0.3 * (1 - 0.15 * t0), 0.3 * (1 - 0.15 * t1)
            a, b = puntos[s], puntos[s + 1]
            torsion = math.radians(rnd.uniform(-8, 8))
            l0 = (Quaternion(afuera, torsion) @ lado)
            pts = [a - l0 * w0 / 2, a + l0 * w0 / 2, b + l0 * w1 / 2, b - l0 * w1 / 2]
            uvs = [(u0, v0 + (v1 - v0) * t0), (u1, v0 + (v1 - v0) * t0), (u1, v0 + (v1 - v0) * t1), (u0, v0 + (v1 - v0) * t1)]
            nrm = [((q - pl.centro_luz).normalized() + Vector((0, 0, 0.8))).normalized() for q in pts]
            pl._cara(pts, uvs, 1, tinte_hoja(rnd), nrm)
    return pl.objeto(nombre, [MAT["Tallo_Verde"], MAT["Hojas"]])


# ---------- Planta de interior ----------
def planta_interior(nombre, semilla):
    rnd = random.Random(semilla)
    H = 0.95
    pl = Planta((0, 0, 0.35), H)
    n = rnd.randint(13, 16)
    for k in range(n):
        ang = k / n * 2 * math.pi + rnd.uniform(-0.3, 0.3)
        afuera = Vector((math.cos(ang), math.sin(ang), 0))
        alto = rnd.uniform(0.35, 0.68)
        abre = rnd.uniform(0.12, 0.3)
        # tallo curvo en 4 tramos
        pts = []
        for s in range(5):
            t = s / 4
            pts.append(afuera * (abre * t * t) + Vector((0, 0, alto * t)))
        for s in range(4):
            pl.tubo(pts[s], pts[s + 1], 0.009, 0.007, 0, lados=5)
        punta = pts[-1]
        direccion = (afuera * rnd.uniform(0.6, 1.0) + Vector((0, 0, rnd.uniform(0.2, 0.7)))).normalized()
        cara = (Vector((0, 0, 1)) + afuera * 0.3).normalized()
        largo = rnd.uniform(0.36, 0.5)
        pl.hoja(punta, direccion, cara, largo, largo, "grande", tinte_hoja(rnd), curva=0.28, segmentos=4)
    return pl.objeto(nombre, [MAT["Tallo_Verde"], MAT["Hojas"]])


# ---------- Mata de flores (tallitos verdes + flores de un color del atlas de flores) ----------
REG_FLOR = {"rosa": (0.0, 0.5, 0.5, 1.0), "amarillo": (0.5, 0.5, 1.0, 1.0),
            "blanco": (0.0, 0.0, 0.5, 0.5), "lila": (0.5, 0.0, 1.0, 0.5)}

def mata_flores(nombre, semilla, color):
    rnd = random.Random(semilla)
    H = 0.45
    pl = Planta((0, 0, 0.1), H)
    # follaje bajo: hojitas alrededor
    for _ in range(70):
        ang = rnd.uniform(0, 2 * math.pi); r = rnd.uniform(0, 0.28)
        base = Vector((math.cos(ang) * r, math.sin(ang) * r, rnd.uniform(0.0, 0.08)))
        d = Vector((math.cos(ang) * 0.6, math.sin(ang) * 0.6, rnd.uniform(0.4, 1.0))).normalized()
        largo = rnd.uniform(0.07, 0.11)
        pl.hoja(base, d, Vector((0, 0, 1)), largo, largo, "hoja", tinte_hoja(rnd))
    # tallos con flor arriba (la flor mira hacia arriba y un poco afuera)
    for _ in range(rnd.randint(16, 22)):
        ang = rnd.uniform(0, 2 * math.pi); r = rnd.uniform(0, 0.24)
        base = Vector((math.cos(ang) * r, math.sin(ang) * r, 0))
        alto = rnd.uniform(0.2, 0.42)
        punta = base + Vector((math.cos(ang) * 0.06, math.sin(ang) * 0.06, alto))
        pl.tubo(base, punta, 0.004, 0.003, 0, lados=4)
        tam = rnd.uniform(0.07, 0.1)
        cara = (Vector((0, 0, 1)) + Vector((math.cos(ang), math.sin(ang), 0)) * 0.35).normalized()
        arriba = cara.orthogonal().normalized()
        u0, v0, u1, v1 = REG_FLOR[color]
        c = punta; l = arriba.cross(cara).normalized()
        pts = [c - l * tam / 2 - arriba * tam / 2, c + l * tam / 2 - arriba * tam / 2, c + l * tam / 2 + arriba * tam / 2, c - l * tam / 2 + arriba * tam / 2]
        nrm = [(q - pl.centro_luz).normalized() for q in pts]
        pl._cara(pts, [(u0, v0), (u1, v0), (u1, v1), (u0, v1)], 2, (1, 1, 1), nrm)
    return pl.objeto(nombre, [MAT["Tallo_Verde"], MAT["Hojas"], MAT["Flores"]])


# ---------- Roca (esfera deformada con ruido, aplanada) ----------
def roca(nombre, semilla, ancho, alto, irregular=0.28):
    rnd = random.Random(semilla)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=5, radius=1.0)
    centros = [Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-1, 1))).normalized() for _ in range(9)]
    for v in bm.verts:
        n = v.co.normalized()
        bulto = sum(max(0.0, n.dot(c)) ** 6 * rnd.uniform(-1, 1) for c in centros) * 0 if False else 0
        f = 1.0
        for k, c in enumerate(centros):
            f += (max(0.0, n.dot(c)) ** 5) * (0.35 if k % 2 else -0.3) * irregular * 3
        f += math.sin(n.x * 7 + semilla) * math.sin(n.y * 6) * 0.04
        v.co = Vector((n.x * f * ancho, n.y * f * ancho * rnd.uniform(0.8, 1.0), n.z * f * alto))
    # apoyar en el suelo (lo que queda bajo z=-alto*0.25 se aplana)
    for v in bm.verts:
        if v.co.z < -alto * 0.25: v.co.z = -alto * 0.25 + (v.co.z + alto * 0.25) * 0.15
    for v in bm.verts: v.co.z += alto * 0.25
    bmesh.ops.smooth_vert(bm, verts=bm.verts, factor=0.3, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    me = bpy.data.meshes.new(nombre); bm.to_mesh(me); bm.free()
    for poly in me.polygons: poly.use_smooth = True
    me.materials.append(MAT["Roca"])
    # UV por proyección (triplanar simple) para la textura de roca
    me.uv_layers.new(name="UVMap")
    for poly in me.polygons:
        n = poly.normal
        for li in poly.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            if abs(n.z) > max(abs(n.x), abs(n.y)): uv = (co.x, co.y)
            elif abs(n.x) > abs(n.y): uv = (co.y, co.z)
            else: uv = (co.x, co.z)
            me.uv_layers[0].data[li].uv = (uv[0] * 0.5, uv[1] * 0.5)
    ob = bpy.data.objects.new(nombre, me)
    bpy.context.scene.collection.objects.link(ob)
    # relieve de roca real: grietas (voronoi) + bultos (nubes) + detalle fino
    for tipo, fuerza, tam in (("VORONOI", 0.16, 0.45), ("CLOUDS", 0.1, 0.6), ("CLOUDS", 0.025, 0.08)):
        tex = bpy.data.textures.new(nombre + tipo + str(tam), tipo)
        tex.noise_scale = tam
        if tipo == "VORONOI": tex.distance_metric = "DISTANCE"
        mod = ob.modifiers.new("Relieve", "DISPLACE")
        mod.texture = tex; mod.strength = fuerza * min(ancho, max(alto, 0.25)); mod.mid_level = 0.5
        mod.texture_coords = "LOCAL"
    return ob


# ---------- Escena, materiales, exportación y vista previa ----------
bpy.ops.wm.read_factory_settings(use_empty=True)
img = bpy.data.images.load(ATLAS)
MAT = {}
def material(nombre, color=None, textura=False):
    m = bpy.data.materials.new(nombre); m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    if textura:
        tex = m.node_tree.nodes.new("ShaderNodeTexImage"); tex.image = img
        m.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        m.node_tree.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
        try: m.surface_render_method = "DITHERED"
        except Exception: m.blend_method = "CLIP"
        m.use_backface_culling = False
        bsdf.inputs["Roughness"].default_value = 0.55
    else:
        bsdf.inputs["Base Color"].default_value = color
        bsdf.inputs["Roughness"].default_value = 0.8
    MAT[nombre] = m
MAT_ = material("Tallo_Madera", (0.22, 0.13, 0.07, 1)); material("Tallo_Verde", (0.18, 0.42, 0.12, 1)); material("Hojas", textura=True)
img_flores = bpy.data.images.load(os.path.join(os.path.dirname(ATLAS), "jardin", "T_AtlasFlores.png"))
mf = bpy.data.materials.new("Flores"); mf.use_nodes = True
_b = mf.node_tree.nodes["Principled BSDF"]; _t = mf.node_tree.nodes.new("ShaderNodeTexImage"); _t.image = img_flores
mf.node_tree.links.new(_t.outputs["Color"], _b.inputs["Base Color"]); mf.node_tree.links.new(_t.outputs["Alpha"], _b.inputs["Alpha"])
try: mf.surface_render_method = "DITHERED"
except Exception: mf.blend_method = "CLIP"
MAT["Flores"] = mf
img_roca = bpy.data.images.load(os.path.join(os.path.dirname(ATLAS), "jardin", "T_Roca.png"))
mr = bpy.data.materials.new("Roca"); mr.use_nodes = True
_b = mr.node_tree.nodes["Principled BSDF"]; _t = mr.node_tree.nodes.new("ShaderNodeTexImage"); _t.image = img_roca
mr.node_tree.links.new(_t.outputs["Color"], _b.inputs["Base Color"]); _b.inputs["Roughness"].default_value = 0.85
MAT["Roca"] = mr

plantas = [
    ("Arbusto_Redondo_1", lambda n: arbusto(n, 11)),
    ("Arbusto_Redondo_2", lambda n: arbusto(n, 27)),
    ("Arbusto_Flores_1", lambda n: arbusto(n, 43, flores=True)),
    ("Arbusto_Flores_2", lambda n: arbusto(n, 58, flores=True)),
    ("Helecho_1", lambda n: helecho(n, 71)),
    ("Helecho_2", lambda n: helecho(n, 88)),
    ("Planta_Interior_1", lambda n: planta_interior(n, 95)),
    ("Planta_Interior_2", lambda n: planta_interior(n, 104)),
    ("Arbusto_Grande_1", lambda n: arbusto(n, 131, grande=True)),
    ("Arbusto_Grande_Flores_1", lambda n: arbusto(n, 147, flores=True, grande=True)),
    ("Flores_Rosa", lambda n: mata_flores(n, 161, "rosa")),
    ("Flores_Amarillo", lambda n: mata_flores(n, 173, "amarillo")),
    ("Flores_Blanco", lambda n: mata_flores(n, 187, "blanco")),
    ("Flores_Lila", lambda n: mata_flores(n, 199, "lila")),
    ("Roca_1", lambda n: roca(n, 211, 0.55, 0.42)),
    ("Roca_2", lambda n: roca(n, 223, 0.8, 0.5)),
    ("Roca_3", lambda n: roca(n, 239, 0.4, 0.55)),
    ("Piedra_Camino_1", lambda n: roca(n, 251, 0.42, 0.07, 0.12)),
    ("Piedra_Camino_2", lambda n: roca(n, 263, 0.5, 0.07, 0.12)),
    ("Piedra_Camino_3", lambda n: roca(n, 277, 0.36, 0.06, 0.12)),
]

objetos = []
for nombre, f in plantas:
    ob = f(nombre)
    objetos.append(ob)
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=os.path.join(SALIDA, nombre + ".fbx"), use_selection=True,
                             apply_unit_scale=True, bake_space_transform=True, axis_forward="-Z", axis_up="Y",
                             mesh_smooth_type="OFF", use_mesh_modifiers=True, colors_type="SRGB",
                             path_mode="STRIP", embed_textures=False)
    print("exportado", nombre, len(ob.data.polygons), "caras")

# vista previa: las 8 plantas en fila, con sol y cielo
for i, ob in enumerate(objetos):
    ob.location = ((i % 5) * 1.7 - 3.4, (i // 5) * -1.9 + 1.5, 0)
sc = bpy.context.scene
for motor in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
    try: sc.render.engine = motor; break
    except Exception: pass
sc.render.resolution_x, sc.render.resolution_y = 1400, 800
sc.render.film_transparent = False
mundo = bpy.data.worlds.new("Cielo"); sc.world = mundo; mundo.use_nodes = True
mundo.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.78, 1.0, 1)
mundo.node_tree.nodes["Background"].inputs[1].default_value = 0.8
sol = bpy.data.objects.new("Sol", bpy.data.lights.new("Sol", "SUN")); sc.collection.objects.link(sol)
sol.data.energy = 3.5; sol.rotation_euler = (math.radians(50), 0, math.radians(30))
piso = bpy.data.meshes.new("Piso"); bm = bmesh.new()
bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=14); bm.to_mesh(piso); bm.free()
po = bpy.data.objects.new("Piso", piso); sc.collection.objects.link(po)
mp = bpy.data.materials.new("PisoMat"); mp.use_nodes = True
mp.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.75, 0.85, 0.8, 1); piso.materials.append(mp)
cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")); sc.collection.objects.link(cam); sc.camera = cam
cam.location = (0, -8.5, 4.6); cam.rotation_euler = (math.radians(66), 0, 0); cam.data.lens = 28
sc.render.filepath = os.path.join(SALIDA, "preview_plantas.png")
bpy.ops.render.render(write_still=True)
print("ok")
