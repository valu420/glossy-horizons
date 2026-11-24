"""
Remodelado limpio de los 11 edificios generados con Tripo.

Los modelos de Tripo son orgánicos de origen (muros que ondulan, ventanas torcidas): ningún filtro
automático los vuelve arquitectura nítida. Aquí se usan como CONCEPTO y se remodelan con geometría
limpia, copiando los rasgos de cada uno (proporciones, giros, marcos blancos, bandas, puentes, coronas,
jardines). Mismos nombres y alturas que antes, así Unity no cambia nada.

Materiales (por nombre): Vidrio, VidrioVerde, Marco (blanco), Acento (cian luminoso), Jardin.
Las UV de las fachadas van en metros / 12 (la textura T_Fachada cubre 12 x 12 m).

Uso (desde la carpeta del proyecto):
  blender -b --factory-startup -P Herramientas/GeneradorCiudad/remodelar_tripo.py
Salida: Assets/_Project/Shared/Models/CiudadFrutiger/Tripo_<Nombre>.fbx
"""
import math
import os

import bmesh
import bpy
from mathutils import Vector

RAIZ = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SALIDA = os.path.join(RAIZ, "Assets", "_Project", "Shared", "Models", "CiudadFrutiger")
os.makedirs(SALIDA, exist_ok=True)
MATS = ["Vidrio", "VidrioVerde", "Marco", "Acento", "Jardin"]
VIDRIO, VERDE, MARCO, ACENTO, JARDIN = range(5)
TILE = 12.0


# ----------------------------------------------------------------------------- perfiles (planta, CCW)
def rect(w, d):
    return [(-w / 2, -d / 2), (w / 2, -d / 2), (w / 2, d / 2), (-w / 2, d / 2)]


def elipse(rx, ry=None, n=48):
    ry = ry or rx
    return [(rx * math.cos(2 * math.pi * i / n), ry * math.sin(2 * math.pi * i / n)) for i in range(n)]


def redondeado(w, d, r, seg=6):
    r = max(0.05, min(r, w / 2 - 0.01, d / 2 - 0.01))
    pts = []
    for cx, cy, a0 in ((w / 2 - r, -d / 2 + r, -90), (w / 2 - r, d / 2 - r, 0),
                       (-w / 2 + r, d / 2 - r, 90), (-w / 2 + r, -d / 2 + r, 180)):
        for k in range(seg + 1):
            a = math.radians(a0 + 90 * k / seg)
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def chaflan(w, d, c):
    return [(-w / 2 + c, -d / 2), (w / 2 - c, -d / 2), (w / 2, -d / 2 + c), (w / 2, d / 2 - c),
            (w / 2 - c, d / 2), (-w / 2 + c, d / 2), (-w / 2, d / 2 - c), (-w / 2, -d / 2 + c)]


def arco(rx, ry, a0, a1, grosor, n=12):
    """Concha: banda de un arco de elipse con grosor (polígono cerrado CCW)."""
    fuera = [((rx + grosor) * math.cos(a), (ry + grosor) * math.sin(a))
             for a in [math.radians(a0 + (a1 - a0) * k / n) for k in range(n + 1)]]
    dentro = [(rx * math.cos(a), ry * math.sin(a))
              for a in [math.radians(a1 - (a1 - a0) * k / n) for k in range(n + 1)]]
    return fuera + dentro


def mover(perfil, dx, dy):
    return [(x + dx, y + dy) for x, y in perfil]


# ----------------------------------------------------------------------------- constructor
class Edificio:
    def __init__(self, nombre):
        self.nombre = nombre
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")

    def _cara(self, verts, mat, liso, uvs):
        f = self.bm.faces.new(verts)
        f.material_index = mat
        f.smooth = liso
        for loop, uv in zip(f.loops, uvs):
            loop[self.uv].uv = uv
        return f

    def loft(self, perfil, niveles, mat, tapa_arriba=MARCO, tapa_abajo=None, liso=None):
        """Fachada por niveles (z, escala_x, escala_y, giro_grados, dx, dy): giros, afinamientos, curvas."""
        if liso is None:
            liso = len(perfil) > 8
        n = len(perfil)
        anillos = []
        for z, sx, sy, giro, dx, dy in niveles:
            c, s = math.cos(math.radians(giro)), math.sin(math.radians(giro))
            anillos.append([self.bm.verts.new((dx + x * sx * c - y * sy * s, dy + x * sx * s + y * sy * c, z))
                            for x, y in perfil])
        largos = [math.dist(perfil[i], perfil[(i + 1) % n]) for i in range(n)]
        for k in range(len(niveles) - 1):
            z0, z1 = niveles[k][0], niveles[k + 1][0]
            s0 = 0.0
            for i in range(n):
                j = (i + 1) % n
                a, b, c_, d = anillos[k][i], anillos[k][j], anillos[k + 1][j], anillos[k + 1][i]
                if not liso:   # aristas vivas: vértices propios por cara
                    a, b, c_, d = [self.bm.verts.new(v.co) for v in (a, b, c_, d)]
                self._cara((a, b, c_, d), mat, liso, [(s0 / TILE, z0 / TILE), ((s0 + largos[i]) / TILE, z0 / TILE),
                                                      ((s0 + largos[i]) / TILE, z1 / TILE), (s0 / TILE, z1 / TILE)])
                s0 += largos[i]
        for anillo, mat_t, arriba in ((anillos[-1], tapa_arriba, True), (anillos[0], tapa_abajo, False)):
            if mat_t is None:
                continue
            vs = [self.bm.verts.new(v.co) for v in (anillo if arriba else anillo[::-1])]
            self._cara(vs, mat_t, False, [(v.co.x / TILE, v.co.y / TILE) for v in vs])
        return anillos

    def prisma(self, perfil, z0, z1, mat, escala_arriba=1.0, tapa_arriba=MARCO, tapa_abajo=None, dx=0.0, dy=0.0):
        return self.loft(perfil, [(z0, 1, 1, 0, 0, 0), (z1, escala_arriba, escala_arriba, 0, dx, dy)], mat,
                         tapa_arriba, tapa_abajo)

    def losa(self, perfil, z, alto, mat=MARCO):
        self.prisma(perfil, z, z + alto, mat, tapa_arriba=mat, tapa_abajo=mat)

    def caja(self, cx, cy, w, d, z0, z1, mat, giro=0.0):
        p = rect(w, d)
        c, s = math.cos(math.radians(giro)), math.sin(math.radians(giro))
        p = [(cx + x * c - y * s, cy + x * s + y * c) for x, y in p]
        self.prisma(p, z0, z1, mat, tapa_arriba=mat, tapa_abajo=mat)

    def viga(self, p0, p1, ancho, grosor, mat, normal):
        """Barra recta de p0 a p1 (3D); 'ancho' en el plano de la fachada, 'grosor' hacia afuera."""
        p0, p1, normal = Vector(p0), Vector(p1), Vector(normal).normalized()
        eje = (p1 - p0)
        if eje.length < 1e-3:
            return
        eje.normalize()
        lado = eje.cross(normal).normalized() * ancho / 2
        fuera = normal * grosor
        esq = [-lado, lado, lado + fuera, -lado + fuera]
        a = [self.bm.verts.new(p0 + e) for e in esq]
        b = [self.bm.verts.new(p1 + e) for e in esq]
        largo = (p1 - p0).length
        for i in range(4):
            j = (i + 1) % 4
            self._cara((a[i], a[j], b[j], b[i]), mat, False, [(0, 0), (0.1, 0), (0.1, largo / TILE), (0, largo / TILE)])
        self._cara(a[::-1], mat, False, [(0, 0)] * 4)
        self._cara(b, mat, False, [(0, 0)] * 4)

    def anillo(self, R, r, z, mat, seg=48, lados=8, cx=0.0, cy=0.0, ry=None):
        ry = ry or R
        filas = []
        for i in range(seg):
            a = 2 * math.pi * i / seg
            fila = []
            for k in range(lados):
                b = 2 * math.pi * k / lados
                fila.append(self.bm.verts.new((cx + (R + r * math.cos(b)) * math.cos(a),
                                               cy + (ry + r * math.cos(b)) * math.sin(a), z + r * math.sin(b))))
            filas.append(fila)
        for i in range(seg):
            for k in range(lados):
                a, b = filas[i], filas[(i + 1) % seg]
                self._cara((a[k], b[k], b[(k + 1) % lados], a[(k + 1) % lados]), mat, True, [(0, 0)] * 4)

    def cupula(self, rx, z0, alto, mat, ry=None, anillos=8, seg=32, cx=0.0, cy=0.0):
        ry = ry or rx
        filas = []
        for k in range(anillos):
            t = k / anillos * math.pi / 2
            filas.append([self.bm.verts.new((cx + rx * math.cos(t) * math.cos(2 * math.pi * i / seg),
                                             cy + ry * math.cos(t) * math.sin(2 * math.pi * i / seg),
                                             z0 + alto * math.sin(t))) for i in range(seg)])
        tope = self.bm.verts.new((cx, cy, z0 + alto))
        for k in range(anillos - 1):
            for i in range(seg):
                j = (i + 1) % seg
                self._cara((filas[k][i], filas[k][j], filas[k + 1][j], filas[k + 1][i]), mat, True,
                           [(i / 4, k / 4), ((i + 1) / 4, k / 4), ((i + 1) / 4, (k + 1) / 4), (i / 4, (k + 1) / 4)])
        for i in range(seg):
            j = (i + 1) % seg
            self._cara((filas[-1][i], filas[-1][j], tope), mat, True, [(0, 0), (0.1, 0), (0.05, 0.1)])

    def antena(self, z0, alto, cx=0.0, cy=0.0, r=0.35):
        self.prisma(mover(elipse(r, n=8), cx, cy), z0, z0 + alto, MARCO)
        self.cupula(r * 2.4, z0 + alto - r, r * 2.4, ACENTO, cx=cx, cy=cy, anillos=4, seg=10)

    def aguja(self, r, z0, alto, cx=0.0, cy=0.0):
        self.prisma(mover(elipse(r, n=12), cx, cy), z0, z0 + alto, MARCO, escala_arriba=0.03, tapa_arriba=None)

    def exportar(self):
        malla = bpy.data.meshes.new(self.nombre)
        self.bm.normal_update()
        self.bm.to_mesh(malla)
        self.bm.free()
        for m in MATS:
            malla.materials.append(bpy.data.materials.get(m) or bpy.data.materials.new(m))
        obj = bpy.data.objects.new(self.nombre, malla)
        bpy.context.scene.collection.objects.link(obj)
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        usados = {p.material_index for p in malla.polygons}
        for idx in sorted(range(len(MATS)), reverse=True):
            if idx not in usados:
                obj.active_material_index = idx
                bpy.ops.object.material_slot_remove()
        bpy.ops.export_scene.fbx(filepath=os.path.join(SALIDA, self.nombre + ".fbx"), use_selection=True,
                                 object_types={"MESH"}, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z",
                                 axis_up="Y", bake_space_transform=True, path_mode="STRIP", mesh_smooth_type="FACE")
        alto = max(v.co.z for v in malla.vertices)
        print(f"  {self.nombre}: alto {alto:.0f} m, {sum(len(p.vertices) - 2 for p in malla.polygons)} tris")


def niveles_torcidos(H, pasos, giro=0.0, lean=0.0, curva=0.0, afina=1.0, z0=0.0):
    """Niveles para loft: giro total (grados), inclinación (m en x arriba), curva en S (m) y afinamiento."""
    out = []
    for k in range(pasos + 1):
        t = k / pasos
        s = 1 - (1 - afina) * t
        dx = lean * t + curva * math.sin(math.pi * t)
        out.append((z0 + (H - z0) * t, s, s, giro * t, dx, 0.0))
    return out


def marco_torcido(e, w, d, niveles, grosor=1.4, mat=MARCO):
    """Aristas blancas en las esquinas que siguen el giro / inclinación del loft."""
    for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
        p = mover(rect(grosor, grosor), sx * (w / 2 - grosor * 0.3), sy * (d / 2 - grosor * 0.3))
        e.loft(p, niveles, mat, tapa_arriba=mat, liso=False)


def banda_torcida(e, perfil, nivel, alto, mat=MARCO, crece=0.6):
    z, sx, sy, giro, dx, dy = nivel
    p = [(x * (1 + crece / max(abs(x), 1)), y * (1 + crece / max(abs(y), 1))) for x, y in perfil]
    e.loft(p, [(z - alto / 2, sx, sy, giro, dx, dy), (z + alto / 2, sx, sy, giro, dx, dy)], mat, tapa_arriba=mat,
           tapa_abajo=mat, liso=False)


# ----------------------------------------------------------------------------- los 11 diseños
def torcida_a():
    """Torre que gira e inclina, con marco blanco en las aristas y 3 bandas que la dividen en 4 paneles."""
    e = Edificio("Tripo_Torre_TorcidaA")
    w, d, H = 24.0, 17.0, 98.0
    niv = niveles_torcidos(H, 20, giro=22, lean=6, afina=0.92)
    e.loft(chaflan(w, d, 2.0), niv, VIDRIO, tapa_arriba=None)
    marco_torcido(e, w, d, niv)
    for k in (5, 10, 15):
        banda_torcida(e, chaflan(w, d, 2.0), niv[k], 1.4)
    z, sx, sy, g, dx, dy = niv[-1]
    e.loft(redondeado(w * sx + 1.2, d * sy + 1.2, 4), [(H, 1, 1, g, dx, dy), (H + 3.5, 0.9, 0.9, g, dx, dy)], MARCO,
           tapa_arriba=MARCO)
    e.loft(chaflan(w + 4, d + 4, 3), [(0, 1, 1, 0, 0, 0), (6, 0.88, 0.88, 0, 0, 0)], MARCO, tapa_arriba=MARCO)
    return e


def torcida_b():
    """Torre en S con gran vidrio, bandas blancas y tirantes diagonales en una cara."""
    e = Edificio("Tripo_Torre_TorcidaB")
    w, d, H = 26.0, 18.0, 93.0
    niv = niveles_torcidos(H, 20, giro=-18, curva=5, afina=0.95)
    e.loft(chaflan(w, d, 2.0), niv, VIDRIO, tapa_arriba=None)
    marco_torcido(e, w, d, niv)
    for k in (7, 14):
        banda_torcida(e, chaflan(w, d, 2.0), niv[k], 1.4)
    # tirantes diagonales sobre la cara lateral (siguen la curva)
    for k in range(0, 20, 4):
        a, b = niv[k], niv[k + 4]
        def punto(nv, frac):
            z, sx, sy, g, dx, dy = nv
            x, y = w / 2 * sx + 0.3, (frac - 0.5) * d * sy
            c, s = math.cos(math.radians(g)), math.sin(math.radians(g))
            return (dx + x * c - y * s, dy + x * s + y * c, z), (c, s, 0)
        p0, n0 = punto(a, 0.1)
        p1, _ = punto(b, 0.9)
        e.viga(p0, p1, 0.9, 0.5, MARCO, n0)
    z, sx, sy, g, dx, dy = niv[-1]
    e.loft(redondeado(w * sx + 1.2, d * sy + 1.2, 5), [(H, 1, 1, g, dx, dy), (H + 3, 0.92, 0.92, g, dx, dy)], MARCO,
           tapa_arriba=MARCO)
    return e


def vela():
    """Núcleo de vidrio y conchas blancas en capas, con punta inclinada (como una vela)."""
    e = Edificio("Tripo_Torre_Vela")
    A, B, H = 13.0, 9.0, 112.0
    nucleo = []
    for k in range(17):
        t = k / 16
        sx = 1 - 0.75 * t ** 2
        nucleo.append((H * t, sx, 1 - 0.3 * t, 0, -A * (1 - sx) * 0.9, 0))
    e.loft(elipse(A, B), nucleo, VIDRIO, tapa_arriba=MARCO)
    # conchas blancas: bandas de arco con distintas alturas y aperturas
    for a0, a1, alto, gro in ((100, 250, H + 10, 1.6), (-60, 60, H * 0.72, 1.4), (200, 300, H * 0.55, 1.2),
                              (30, 120, H * 0.38, 1.2)):
        niv = []
        for k in range(13):
            t = k / 12
            sx = 1 - 0.7 * t ** 2
            niv.append((alto * t, sx, 1 - 0.3 * t, 0, -A * (1 - sx) * 0.9, 0))
        e.loft(arco(A + 0.2, B + 0.2, a0, a1, gro), niv, MARCO, tapa_arriba=MARCO, tapa_abajo=MARCO, liso=True)
    e.loft(elipse(A + 5, B + 5), [(0, 1, 1, 0, 0, 0), (5, 0.9, 0.9, 0, 0, 0)], MARCO, tapa_arriba=MARCO)
    return e


def torres_puente():
    """Losa blanca con columna de vidrio + torre con ranuras y antenas gemelas, unidas por dos puentes."""
    e = Edificio("Tripo_Torres_Puente")
    # torre izquierda: losa blanca con media columna de vidrio al frente y aleta superior
    e.prisma(rect(16, 15), 0, 92, MARCO, tapa_arriba=MARCO)
    e.prisma(mover(elipse(6.5, 4.5), 0, -7.5), 0, 76, VIDRIO, tapa_arriba=MARCO)
    e.prisma(mover(rect(3, 15), 3, 0), 92, 112, MARCO, escala_arriba=0.3, dx=2)
    for z in range(12, 92, 12):
        e.losa(mover(rect(16.8, 15.8), 0, 0), z - 0.3, 0.6, MARCO)
    # torre derecha: vidrio con pilastras blancas, ranuras y dos antenas
    cx = 30
    e.prisma(mover(rect(16, 15), cx, 0), 0, 86, VIDRIO, tapa_arriba=MARCO)
    for x in (-8, -2.5, 2.5, 8):
        e.caja(cx + x, -7.6, 1.3, 1.2, 0, 87, MARCO)
        e.caja(cx + x, 7.6, 1.3, 1.2, 0, 87, MARCO)
    e.caja(cx, 0, 17, 16, 86, 90, MARCO)
    for k in range(5):
        e.caja(cx, -7.9, 6, 0.8, 60 + k * 4.5, 62 + k * 4.5, ACENTO)
    e.antena(90, 22, cx - 3, 0, r=0.5)
    e.antena(90, 17, cx + 3, 0, r=0.5)
    # dos puentes de cristal
    for z in (30, 52):
        e.caja(cx / 2, 0, cx - 16, 7, z, z + 5, VIDRIO)
        e.caja(cx / 2, 0, cx - 16, 7.8, z - 0.5, z, MARCO)
        e.caja(cx / 2, 0, cx - 16, 7.8, z + 5, z + 5.5, MARCO)
    # el pivote queda en el centro del conjunto
    for v in e.bm.verts:
        v.co.x -= cx / 2
    return e


def diagonal():
    """Losa blanca con ventanas inclinadas en diagonal, columna de vidrio lateral y marquesina."""
    e = Edificio("Tripo_Torre_Diagonal")
    w, d, H = 30.0, 12.0, 72.0
    e.prisma(redondeado(w, d, 2.0), 0, H, MARCO, tapa_arriba=MARCO)
    e.prisma(mover(rect(4, d + 0.4), w / 2 - 3, 0), 2, H - 2, VIDRIO, tapa_arriba=MARCO)
    import random
    rnd = random.Random(4)
    for cara in (-1, 1):
        y = cara * (d / 2 + 0.05)
        for k in range(46):
            x = rnd.uniform(-w / 2 + 3, w / 2 - 7)
            z = rnd.uniform(8, H - 6)
            largo = rnd.uniform(3.5, 7.5)
            dx, dz = math.cos(math.radians(55)) * largo / 2, math.sin(math.radians(55)) * largo / 2
            e.viga((x - dx, y, z - dz), (x + dx, y, z + dz), rnd.uniform(1.1, 1.7), 0.25, VIDRIO, (0, cara, 0))
    e.losa(redondeado(w + 10, d + 12, 6), 4, 1.0, MARCO)        # marquesina
    e.prisma(mover(rect(w * 0.5, d - 2), -2, 0), 0, 4, VIDRIO, tapa_arriba=None)
    e.antena(H, 14, -w / 4, 0, r=0.45)
    return e


def escalonada():
    """Torre con pilastras blancas y franjas de vidrio, anexo bajo y remate blanco."""
    e = Edificio("Tripo_Torre_Escalonada")
    e.prisma(rect(20, 17), 0, 58, VIDRIO, tapa_arriba=MARCO)
    for x in (-10, -3.5, 3.5, 10):
        e.caja(x, -8.6, 1.6, 1.4, 0, 58, MARCO)
        e.caja(x, 8.6, 1.6, 1.4, 0, 58, MARCO)
    for z in (20, 38):
        e.losa(rect(21, 18), z, 1.0, MARCO)
    e.prisma(rect(16, 14), 58, 62, MARCO, tapa_arriba=MARCO)
    e.caja(0, -7.1, 9, 0.4, 59, 61, VIDRIO)
    e.prisma(mover(rect(30, 12), 16, 4), 0, 12, VIDRIO, tapa_arriba=MARCO)       # anexo
    e.losa(mover(rect(31, 13), 16, 4), 12, 1.0, MARCO)
    for x in range(4, 30, 6):
        e.caja(x + 2, -2.1, 1.0, 0.8, 0, 12, MARCO)
    for v in e.bm.verts:
        v.co.x -= 8
    return e


def espiral():
    """Cilindro de vidrio con nervios blancos helicoidales, anillos en base y remate, y antena."""
    e = Edificio("Tripo_Torre_Espiral")
    R, H = 9.0, 72.0
    e.prisma(elipse(R), 3, H, VIDRIO, tapa_arriba=MARCO)
    e.losa(elipse(R + 1.4), 0, 3.0, MARCO)
    e.losa(elipse(R + 1.0), H, 2.0, MARCO)
    pasos = 60
    for fase in range(6):
        prev = None
        for k in range(pasos + 1):
            t = k / pasos
            a = 2 * math.pi * fase / 6 + t * math.pi * 1.6
            p = (math.cos(a) * (R + 0.05), math.sin(a) * (R + 0.05), 3 + (H - 3) * t)
            if prev:
                e.viga(prev, p, 0.9, 0.45, MARCO, (math.cos(a), math.sin(a), 0))
            prev = p
    e.prisma(rect(6, 5), H + 2, H + 6, MARCO, tapa_arriba=MARCO)
    e.antena(H + 6, 10, r=0.45)
    return e


def terrazas():
    """Losas blancas apiladas con vidrio entre ellas; la de arriba se ondula hacia un lado."""
    e = Edificio("Tripo_Edificio_Terrazas")
    w, d = 58.0, 26.0
    for k in range(5):
        z = k * 4.2
        sal = [0.0, -2.0, 1.5, -1.0, 0.5][k]
        e.losa(redondeado(w + sal * 2, d + sal, 10), z, 0.9, MARCO)
        if k < 4:
            e.prisma(redondeado(w - 4, d - 4, 8), z + 0.9, z + 4.2, VIDRIO, tapa_arriba=None)
    # techo ondulado: se levanta hacia un extremo
    p = redondeado(w + 2, d + 2, 10)
    abajo = [e.bm.verts.new((x, y, 17.7 + 4.5 * max(0.0, x / (w / 2)) ** 2)) for x, y in p]
    arriba = [e.bm.verts.new((x, y, v.co.z + 1.0)) for (x, y), v in zip(p, abajo)]
    n = len(p)
    for i in range(n):
        j = (i + 1) % n
        e._cara((abajo[i], abajo[j], arriba[j], arriba[i]), MARCO, True, [(0, 0)] * 4)
    e._cara(arriba, MARCO, False, [(0, 0)] * n)
    e._cara(abajo[::-1], MARCO, False, [(0, 0)] * n)
    return e


def mirador():
    """Fuste de vidrio con aristas blancas y corona-mirador más ancha, con antena."""
    e = Edificio("Tripo_Torre_Mirador")
    e.prisma(redondeado(16, 14, 3), 0, 72, VIDRIO, tapa_arriba=None)
    for sx in (-1, 1):
        e.caja(sx * 8, 0, 1.4, 14.5, 0, 72, MARCO)
    for z in (24, 48):
        e.losa(redondeado(17, 15, 3.5), z, 1.0, MARCO)
    e.prisma(redondeado(16, 14, 3), 72, 74, MARCO, escala_arriba=1.3, tapa_arriba=None)
    e.prisma(redondeado(21, 18, 4), 74, 80, VIDRIO, tapa_arriba=None)
    e.losa(redondeado(22, 19, 4.5), 80, 1.6, MARCO)
    e.losa(redondeado(21.4, 18.4, 4.2), 76.5, 0.5, ACENTO)
    e.prisma(redondeado(12, 10, 2), 81.6, 86, MARCO, tapa_arriba=MARCO)
    e.antena(86, 12, r=0.5)
    return e


def aguja():
    """Columna de vidrio entre pilares blancos; arriba fuste de vidrio y corona puntiaguda; jardines al pie."""
    e = Edificio("Tripo_Torre_Aguja")
    e.prisma(rect(10, 16), 0, 92, VIDRIO, tapa_arriba=MARCO)
    for sx in (-1, 1):
        e.prisma(mover(rect(7, 18), sx * 8.5, 0), 0, 90, MARCO, tapa_arriba=MARCO)
        for z in range(8, 90, 6):
            e.caja(sx * 8.5, -9.05, 5.6, 0.2, z, z + 3.2, VIDRIO)
            e.caja(sx * 8.5, 9.05, 5.6, 0.2, z, z + 3.2, VIDRIO)
    e.caja(0, -8.1, 1.2, 0.4, 2, 92, ACENTO)                     # raya de luz de la columna
    e.losa(rect(26, 20), 90, 2, MARCO)
    e.prisma(elipse(8.5, 7.5), 92, 128, VIDRIO, tapa_arriba=None)
    e.losa(elipse(9.6, 8.6), 128, 2.0, MARCO)
    e.prisma(elipse(9.0, 8.0), 130, 146, MARCO, escala_arriba=0.25, tapa_arriba=MARCO)
    for k in range(6):
        a = k * math.pi / 3
        e.caja(math.cos(a) * 6.6, math.sin(a) * 5.8, 1.8, 0.6, 133, 139, VIDRIO, giro=math.degrees(a) + 90)
    e.aguja(1.0, 146, 19)
    # jardines al pie: maceteros blancos con arbustos
    for x, y, r, h in ((-17, -6, 5.5, 9), (14, -9, 6, 7), (-4, -16, 4, 5), (16, 8, 4.5, 6)):
        e.prisma(mover(elipse(r, n=24), x, y), 0, h * 0.35, MARCO, tapa_arriba=None)
        e.prisma(mover(elipse(r - 0.3, n=24), x, y), 0, h * 0.35 + 0.1, JARDIN, tapa_arriba=JARDIN)
        e.cupula(r * 0.95, h * 0.35, h * 0.65, JARDIN, cx=x, cy=y)
    return e


def curva():
    """Torre de vidrio que se curva al subir, con franja blanca lateral y columna de ventanas oscuras."""
    e = Edificio("Tripo_Torre_Curva")
    H = 134.0
    niv = niveles_torcidos(H, 24, giro=8, lean=10, curva=-3, afina=0.97)
    e.loft(elipse(12, 10), niv, VIDRIO, tapa_arriba=MARCO)
    franja = mover(rect(3.0, 14), -12.2, 0)
    e.loft(franja, niv, MARCO, tapa_arriba=MARCO, liso=False)
    for k in (8, 16):
        z, sx, sy, g, dx, dy = niv[k]
        e.loft(elipse(12.6, 10.6), [(z - 0.4, sx, sy, g, dx, dy), (z + 0.4, sx, sy, g, dx, dy)], MARCO,
               tapa_arriba=MARCO, tapa_abajo=MARCO)
    e.loft(redondeado(26, 20, 7), [(0, 1, 1, 0, 0, 0), (7, 1, 1, 0, 0, 0)], MARCO, tapa_arriba=MARCO)
    z, sx, sy, g, dx, dy = niv[-1]
    e.antena(H, 10, dx, dy, r=0.45)
    return e


for o in list(bpy.data.objects):
    bpy.data.objects.remove(o)
for diseno in (torcida_a, torcida_b, vela, torres_puente, diagonal, escalonada, espiral, terrazas, mirador, aguja, curva):
    diseno().exportar()
print("LISTO")
