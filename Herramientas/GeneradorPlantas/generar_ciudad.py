"""Ciudad futurista Frutiger Aero (fondo) y nubes esponjosas, hechas en Blender sin interfaz.
  blender -b -P generar_ciudad.py -- <carpeta_salida>
Torres: pisos de losa blanca + bandas de vidrio azul, coronas, halos, antenas y terrazas verdes.
Materiales por nombre: "Blanco", "Vidrio", "Verde". Nubes: un solo material "Nube".
Z arriba; base de cada torre en el origen; alturas reales (40 a 150 m)."""
import bpy, bmesh, math, random, sys, os
from mathutils import Vector

SALIDA = sys.argv[sys.argv.index("--") + 1:][0]
os.makedirs(SALIDA, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)

def material(nombre, color, rug, metal=0.0):
    m = bpy.data.materials.new(nombre); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = color; b.inputs["Roughness"].default_value = rug; b.inputs["Metallic"].default_value = metal
    return m
BLANCO = material("Blanco", (0.92, 0.95, 0.97, 1), 0.25)
VIDRIO = material("Vidrio", (0.25, 0.55, 0.9, 1), 0.05, 0.6)
VERDE = material("Verde", (0.25, 0.65, 0.2, 1), 0.6)
NUBE = material("Nube", (1, 1, 1, 1), 0.9)

partes = []
def cil(r, h, z, mat, verts=40, r2=None):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r, radius2=r if r2 is None else r2, depth=h, location=(0, 0, z + h / 2))
    o = bpy.context.object; o.data.materials.append(mat); partes.append(o); return o
def caja(sx, sy, h, z, mat, x=0, y=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, z + h / 2)); o = bpy.context.object
    o.scale = (sx, sy, h); o.data.materials.append(mat); partes.append(o); return o
def toro(R, r, z, mat):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, location=(0, 0, z), major_segments=64, minor_segments=10)
    o = bpy.context.object; o.data.materials.append(mat); partes.append(o); return o

def pisos(r, z, n, alto_vidrio=2.6, losa=0.7, verde_cada=0, rr=None, cuadrado=False):
    for i in range(n):
        rad = r if rr is None else rr(i)
        if cuadrado:
            caja(rad * 2, rad * 2, alto_vidrio, z, VIDRIO); caja(rad * 2.1, rad * 2.1, losa, z + alto_vidrio, VERDE if verde_cada and i % verde_cada == 0 else BLANCO)
        else:
            cil(rad, alto_vidrio, z, VIDRIO); cil(rad * 1.05, losa, z + alto_vidrio, VERDE if verde_cada and i % verde_cada == 0 else BLANCO)
        z += alto_vidrio + losa
    return z

def terminar(nombre):
    bpy.ops.object.select_all(action="DESELECT")
    for o in partes: o.select_set(True)
    bpy.context.view_layer.objects.active = partes[0]
    bpy.ops.object.join()
    ob = bpy.context.object; ob.name = nombre
    bpy.ops.object.shade_smooth()
    bpy.ops.export_scene.fbx(filepath=os.path.join(SALIDA, nombre + ".fbx"), use_selection=True, apply_unit_scale=True,
                             bake_space_transform=True, axis_forward="-Z", axis_up="Y", path_mode="STRIP")
    print("exportado", nombre, len(ob.data.polygons))
    bpy.ops.object.delete(); partes.clear()

# 1. cilindro alto con halo arriba
z = pisos(9, 0, 30); cil(7, 6, z, BLANCO); toro(13, 0.5, z + 9, BLANCO); cil(0.4, 22, z + 6, BLANCO, 12); terminar("Torre_Halo")
# 2. escalonada (tres cuerpos que se angostan) con terrazas verdes
z = pisos(13, 0, 12, verde_cada=4); z = pisos(10, z, 12, verde_cada=4); z = pisos(7, z, 10, verde_cada=5); cil(0.35, 18, z, BLANCO, 12); terminar("Torre_Escalonada")
# 3. aguja que se afina hacia arriba
z = pisos(10, 0, 34, rr=lambda i: 10 - i * 0.18); cil(4, 25, z, VIDRIO, 32, 0.3); terminar("Torre_Aguja")
# 4. dos torres unidas por puentes
z0 = 0
for dx in (-9, 9):
    zz = 0
    for i in range(26):
        caja(10, 10, 2.6, zz, VIDRIO, dx); caja(10.6, 10.6, 0.7, zz + 2.6, BLANCO, dx); zz += 3.3
for zz in (25, 50, 75): caja(8, 3, 3, zz, BLANCO)
cil(0.35, 14, 3.3 * 26, BLANCO, 12); terminar("Torre_Doble")
# 5. ancha y redondeada con cúpula de vidrio
z = pisos(16, 0, 14, alto_vidrio=3, verde_cada=3); bpy.ops.mesh.primitive_uv_sphere_add(radius=15, location=(0, 0, z)); o = bpy.context.object
o.scale = (1, 1, 0.6); o.data.materials.append(VIDRIO); partes.append(o); terminar("Torre_Cupula")
# 6. cuadrada alta con corona
z = pisos(8, 0, 38, cuadrado=True); caja(14, 14, 3, z, BLANCO); cil(5, 5, z + 3, VIDRIO, 24); cil(0.4, 20, z + 8, BLANCO, 12); terminar("Torre_Cuadrada")

# Nubes: grupos de esferas fusionadas (metaballs) -> malla suave, base algo plana
def nube(nombre, semilla, largo):
    rnd = random.Random(semilla)
    mb = bpy.data.metaballs.new(nombre); mb.resolution = 1.2; mb.render_resolution = 1.2
    ob = bpy.data.objects.new(nombre, mb); bpy.context.scene.collection.objects.link(ob)
    for k in range(rnd.randint(14, 20)):
        e = mb.elements.new(); x = rnd.uniform(-largo / 2, largo / 2)
        e.co = (x, rnd.uniform(-largo * 0.18, largo * 0.18), rnd.uniform(0, largo * 0.12) * (1 - abs(x) / largo))
        e.radius = rnd.uniform(largo * 0.12, largo * 0.22) * (1.2 - abs(x) / largo)
    bpy.context.view_layer.objects.active = ob; ob.select_set(True)
    bpy.ops.object.convert(target="MESH")
    m = bpy.context.object; m.data.materials.append(NUBE)
    for v in m.data.vertices:
        if v.co.z < 0: v.co.z *= 0.35                       # base plana
    partes.append(m); terminar(nombre)
for i, (s, l) in enumerate(((11, 40), (23, 55), (37, 34), (49, 70))):
    nube(f"Nube_{i + 1}", s, l)
print("ok")
