"""
Árboles realistas para la isla (Blender, sin interfaz):
  blender -b --factory-startup -P generar_arboles.py
Crea en Assets/_Project/Shared/Models/Plantas:
  Arbol_Frondoso_1/2/3  árbol de hoja ancha (copa redonda, ovalada y abierta), 9-13 m
Técnica de juegos: tronco y ramas de verdad + "ramilletes" (tarjetas grandes con muchas hojas
dibujadas, textura T_RamilleteHojas). Las tarjetas llevan normales esféricas (la copa se ilumina
como un volumen suave) y color de vértice (tinte + alfa = cuánto se mece con el viento),
igual que los arbustos de generar_plantas.py, para el shader Glossy/FollajeNatural.
Materiales: "Tallo_Madera" (tronco) y "HojasArbol" (ramilletes). Z arriba, base en el origen.
"""
import math
import os
import random

import bmesh
import bpy
from mathutils import Quaternion, Vector

RAIZ = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SALIDA = os.path.join(RAIZ, "Assets", "_Project", "Shared", "Models", "Plantas")

# cuadrantes de T_RamilleteHojas (u0, v0, u1, v1), origen abajo-izquierda
REG = {"hojas_a": (0.0, 0.5, 0.5, 1.0), "hojas_b": (0.5, 0.5, 1.0, 1.0),
       "pino_a": (0.0, 0.0, 0.5, 0.5), "pino_b": (0.5, 0.0, 1.0, 0.5)}


class Arbol:
    def __init__(self, centro_copa, alto):
        self.bm = bmesh.new()
        self.col = self.bm.loops.layers.color.new("Col")
        self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.normales = {}
        self.centro = Vector(centro_copa)
        self.alto = alto

    def _cara(self, pts, uvs, mat, tinte, normales=None):
        vs = [self.bm.verts.new(p) for p in pts]
        f = self.bm.faces.new(vs)
        f.material_index = mat
        for k, loop in enumerate(f.loops):
            loop[self.uv].uv = uvs[k]
            z = loop.vert.co.z
            # alfa = cuánto se mece: nada en el tronco bajo, más arriba y en la copa
            loop[self.col] = (tinte[0], tinte[1], tinte[2], max(0.0, min(1.0, z / self.alto)) ** 1.5)
        if normales:
            self.normales[f] = normales
        return f

    def tubo(self, p0, p1, r0, r1, lados=7):
        eje = p1 - p0
        if eje.length < 1e-4:
            return
        d = eje.normalized()
        a = d.orthogonal().normalized()
        b = d.cross(a)
        ang = [k / lados * 2 * math.pi for k in range(lados)]
        an0 = [p0 + (a * math.cos(t) + b * math.sin(t)) * r0 for t in ang]
        an1 = [p1 + (a * math.cos(t) + b * math.sin(t)) * r1 for t in ang]
        for k in range(lados):
            k2 = (k + 1) % lados
            pts = [an0[k], an0[k2], an1[k2], an1[k]]
            nrm = [(q - (p0 if i < 2 else p1)).normalized() for i, q in enumerate(pts)]
            self._cara(pts, [(k / lados, 0), ((k + 1) / lados, 0), ((k + 1) / lados, 1), (k / lados, 1)],
                       0, (1, 1, 1), nrm)

    def tarjeta(self, base, arriba, cara, ancho, alto, region, tinte):
        """Ramillete: la base (donde nacen las ramitas) en 'base', crece hacia 'arriba', mira a 'cara'."""
        u0, v0, u1, v1 = REG[region]
        arriba = arriba.normalized()
        lado = cara.cross(arriba)
        if lado.length < 1e-4:
            lado = arriba.orthogonal()
        lado.normalize()
        pts = [base - lado * ancho / 2, base + lado * ancho / 2,
               base + lado * ancho / 2 + arriba * alto, base - lado * ancho / 2 + arriba * alto]
        nrm = [((q - self.centro).normalized() * 0.85 + Vector((0, 0, 0.35))).normalized() for q in pts]
        self._cara(pts, [(u0, v0), (u1, v0), (u1, v1), (u0, v1)], 1, tinte, nrm)

    def objeto(self, nombre, mats):
        me = bpy.data.meshes.new(nombre)
        self.bm.to_mesh(me)
        normales = []
        for f in self.bm.faces:
            normales.extend(self.normales.get(f, [f.normal.copy()] * len(f.verts)))
        me.normals_split_custom_set([tuple(n) for n in normales])
        for m in mats:
            me.materials.append(m)
        ob = bpy.data.objects.new(nombre, me)
        bpy.context.scene.collection.objects.link(ob)
        self.bm.free()
        return ob


def tinte(rnd, claro=0.0):
    return (min(1.0, rnd.uniform(0.92, 1.0) + claro * 0.1), min(1.0, rnd.uniform(0.96, 1.0) + claro * 0.08), rnd.uniform(0.8, 0.95))


def frondoso(nombre, semilla, alto, ancho_copa, forma):
    """forma: 'redonda', 'ovalada' o 'abierta'."""
    rnd = random.Random(semilla)
    H, R = alto, ancho_copa
    base_copa = H * (0.27 if forma != "ovalada" else 0.22)
    centro = Vector((0, 0, base_copa + (H - base_copa) * 0.5))
    rz = (H - base_copa) * 0.55
    ar = Arbol(centro, H)

    def dentro(p):
        return (p.x / R) ** 2 + (p.y / R) ** 2 + ((p.z - centro.z) / rz) ** 2 <= 1.0

    # tronco con leve curva
    puntos = [Vector((0, 0, 0))]
    for k in range(1, 5):
        t = k / 4
        puntos.append(Vector((rnd.uniform(-0.25, 0.25) * t, rnd.uniform(-0.25, 0.25) * t, base_copa * 1.1 * t)))
    r_tronco = H * 0.028
    for k in range(4):
        ar.tubo(puntos[k], puntos[k + 1], r_tronco * (1 - 0.18 * k), r_tronco * (1 - 0.18 * (k + 1)))
    cima = puntos[-1]

    puntas = []

    def crecer(p, d, largo, radio, nivel):
        fin = p + d * largo
        while not dentro(fin) and largo > 0.3:
            largo *= 0.85
            fin = p + d * largo
        ar.tubo(p, fin, radio, radio * 0.65, lados=6 if nivel < 2 else 4)
        if nivel >= 2:
            puntas.append((p, fin, d))
        if nivel == 3:
            return
        for _ in range(3 if nivel < 2 else 2):
            eje = d.orthogonal().normalized()
            eje.rotate(Quaternion(d, rnd.uniform(0, 2 * math.pi)))
            nd = Quaternion(eje, math.radians(rnd.uniform(22, 45))) @ d
            afuera = fin - centro
            afuera.z *= 0.4
            nd = (nd + afuera.normalized() * 0.4 + Vector((0, 0, 0.15))).normalized()
            crecer(fin, nd, largo * rnd.uniform(0.62, 0.76), radio * 0.6, nivel + 1)

    ramas = 5 if forma != "ovalada" else 4
    for k in range(ramas):
        ang = k / ramas * 2 * math.pi + rnd.uniform(-0.35, 0.35)
        abre = {"redonda": 0.75, "ovalada": 0.45, "abierta": 1.05}[forma]
        d = Vector((math.cos(ang) * abre, math.sin(ang) * abre, 1)).normalized()
        crecer(cima, d, (H - base_copa) * 0.45, r_tronco * 0.62, 1)
    if forma == "ovalada":  # guía central que sube
        crecer(cima, Vector((0, 0, 1)), (H - base_copa) * 0.6, r_tronco * 0.55, 1)

    # copa en "bultos": en cada punta de rama, una bola de ramilletes que miran hacia afuera
    # (así la copa se ve llena y redondeada, con masas de hojas como los árboles de las fotos Frutiger)
    tam = H * 0.2
    oro = math.pi * (3 - math.sqrt(5))
    for p, fin, d in puntas:
        rc = tam * rnd.uniform(0.55, 0.8)
        cb = fin + Vector((0, 0, rc * 0.25))
        n = 11
        for k in range(n):
            zf = 1 - 2 * (k + 0.5) / n                      # puntos repartidos en la esfera
            rr = math.sqrt(max(0.0, 1 - zf * zf))
            nk = Vector((math.cos(k * oro) * rr, math.sin(k * oro) * rr, zf * 0.8 + 0.15)).normalized()
            if nk.z < -0.55:
                continue                                    # la parte de abajo del bulto casi no se ve
            arriba = (Vector((0, 0, 1)) - nk * nk.z)
            arriba = arriba.normalized() if arriba.length > 1e-3 else nk.orthogonal().normalized()
            s = tam * rnd.uniform(0.85, 1.15)
            base = cb + nk * rc * 0.55 - arriba * s * 0.45
            luz = max(0.0, nk.z) * 0.7 + max(0.0, nk.dot(Vector((0.4, -0.5, 0.75)).normalized())) * 0.6
            region = "hojas_b" if luz > 0.55 else "hojas_a"
            ar.tarjeta(base, arriba, nk, s, s, region, tinte(rnd, luz - 0.2))
    # relleno interior oscuro para que la copa no se vea hueca
    for _ in range(int(len(puntas) * 0.8)):
        a, z = rnd.uniform(0, 2 * math.pi), rnd.uniform(-0.5, 0.5)
        r = rnd.uniform(0.15, 0.55)
        pos = centro + Vector((math.cos(a) * R * r, math.sin(a) * R * r, z * rz))
        ar.tarjeta(pos, Vector((rnd.gauss(0, 0.3), rnd.gauss(0, 0.3), 1)), Vector((math.cos(a), math.sin(a), 0)),
                   tam * 1.2, tam * 1.2, "hojas_a", tinte(rnd, -0.45))
    return ar.objeto(nombre, [MAT["Tallo_Madera"], MAT["HojasArbol"]])


def pino(nombre, semilla, alto):
    rnd = random.Random(semilla)
    H = alto
    ar = Arbol(Vector((0, 0, H * 0.45)), H)
    r_tronco = H * 0.022
    ar.tubo(Vector((0, 0, 0)), Vector((0, 0, H * 0.55)), r_tronco, r_tronco * 0.6)
    ar.tubo(Vector((0, 0, H * 0.55)), Vector((0, 0, H)), r_tronco * 0.6, r_tronco * 0.08)
    z = H * 0.16
    giro = 0.0
    while z < H * 0.97:
        t = (z - H * 0.16) / (H * 0.84)
        largo = H * 0.33 * (1 - t) ** 0.9 + H * 0.04
        n = 7 if t < 0.6 else 5
        giro += rnd.uniform(0.4, 0.9)
        for k in range(n):
            ang = giro + k / n * 2 * math.pi + rnd.uniform(-0.2, 0.2)
            afuera = Vector((math.cos(ang), math.sin(ang), 0))
            caida = rnd.uniform(-0.35, -0.1) * (1 - t) + 0.15 * t
            d = (afuera + Vector((0, 0, caida))).normalized()
            base = Vector((0, 0, z))
            ar.tubo(base, base + d * largo * 0.85, r_tronco * 0.3 * (1 - t) + 0.02, 0.02, lados=4)
            # dos tarjetas cruzadas a lo largo de la rama (la rama de la textura va de abajo arriba)
            region = "pino_a" if rnd.random() < 0.5 else "pino_b"
            cara1 = Vector((0, 0, 1)).cross(d).normalized()
            ar.tarjeta(base, d, Vector((0, 0, 1)), largo * 0.9, largo, region, tinte(rnd, -0.2))
            ar.tarjeta(base, d, cara1, largo * 0.6, largo, region, tinte(rnd, -0.3))
        z += H * rnd.uniform(0.055, 0.075)
    # copa de la punta
    for k in range(3):
        ar.tarjeta(Vector((0, 0, H * 0.88)), Vector((0, 0, 1)), Vector((math.cos(k), math.sin(k), 0)),
                   H * 0.12, H * 0.14, "pino_a", tinte(rnd, -0.1))
    return ar.objeto(nombre, [MAT["Tallo_Madera"], MAT["HojasArbol"]])


# ----------------------------------------------------------------------------- escena y exportación
bpy.ops.wm.read_factory_settings(use_empty=True)
MAT = {}
for nombre, color in (("Tallo_Madera", (0.25, 0.16, 0.09, 1)), ("HojasArbol", (0.3, 0.6, 0.2, 1))):
    m = bpy.data.materials.new(nombre)
    m.diffuse_color = color
    MAT[nombre] = m

arboles = [
    ("Arbol_Frondoso_1", lambda n: frondoso(n, 21, 11.0, 5.4, "redonda")),
    ("Arbol_Frondoso_2", lambda n: frondoso(n, 37, 13.0, 3.6, "ovalada")),
    ("Arbol_Frondoso_3", lambda n: frondoso(n, 52, 9.5, 6.0, "abierta")),
]
for nombre, f in arboles:
    ob = f(nombre)
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=os.path.join(SALIDA, nombre + ".fbx"), use_selection=True,
                             apply_unit_scale=True, bake_space_transform=True, axis_forward="-Z", axis_up="Y",
                             mesh_smooth_type="OFF", colors_type="SRGB", path_mode="STRIP", embed_textures=False)
    print("exportado", nombre, len(ob.data.polygons), "caras")
print("LISTO")
