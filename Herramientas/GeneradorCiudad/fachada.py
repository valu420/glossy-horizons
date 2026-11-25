"""
Textura de fachada de vidrio para los edificios Frutiger (repetible). 1 repetición = 12 x 12 m:
8 ventanas de 1.5 m de ancho x 4 pisos de 3 m (cuadrícula densa, como las torres de vidrio de los 2000).
Para URP/Lit:
  T_Fachada_Color.png  color base: vidrio azul-cian brillante con variación por panel,
                       parteluces blanco-cian y franjas claras entre pisos
  T_Fachada_MS.png     R = metálico, A = suavidad (vidrio pulido y reflejante, marcos satinados)
  T_Fachada_Normal.png bisel de parteluces y franjas (relieve sutil)
  T_Fachada_Emision.png brillo propio del vidrio (que no se vea oscuro de lejos)
Uso: python fachada.py
"""
import os

import numpy as np
from PIL import Image

RAIZ = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SALIDA = os.path.join(RAIZ, "Assets", "_Project", "Shared", "Models", "CiudadFrutiger", "Texturas")
N = 1024              # píxeles por repetición
TAM = 12.0            # metros por repetición
M = N / TAM           # píxeles por metro
ANCHO_VENTANA, ALTO_PISO = 1.5, 3.0
os.makedirs(SALIDA, exist_ok=True)

y, x = np.mgrid[0:N, 0:N].astype(np.float32)
xm, ym = x / M, (N - 1 - y) / M   # metros; v crece hacia arriba (como la UV)


def banda(coord, periodo, ancho, desfase=0.0):
    c = coord - desfase
    d = np.abs(((c + periodo / 2) % periodo) - periodo / 2)
    return d, d < ancho / 2


d_mv, parteluz = banda(xm, ANCHO_VENTANA, 0.11)
_, parteluz_grueso = banda(xm, ANCHO_VENTANA * 4, 0.22)          # cada 6 m, uno más marcado
d_losa, losa = banda(ym, ALTO_PISO, 0.55)                        # franja clara entre pisos
_, filete = banda(ym, ALTO_PISO, 0.08, 0.32)                      # línea blanca sobre la franja
marco = parteluz | parteluz_grueso | filete

# vidrio: azul-cian brillante, más claro arriba de cada panel (cielo reflejado), variación por panel
col_p = np.floor(xm / ANCHO_VENTANA).astype(int)
fil_p = np.floor(ym / ALTO_PISO).astype(int)
rng = np.random.default_rng(11)
var = rng.random((16, 16)).astype(np.float32)
v = var[fil_p % 16, col_p % 16]
dentro = np.clip(((ym % ALTO_PISO) - 0.28) / (ALTO_PISO - 0.56), 0, 1)
vidrio = np.stack([
    0.16 + 0.07 * v + 0.16 * dentro,
    0.52 + 0.08 * v + 0.16 * dentro,
    0.86 + 0.05 * v + 0.10 * dentro,
], -1)
# algunos paneles más claros (reflejo del cielo) y algunos más cian
claro = (var[(fil_p + 5) % 16, (col_p + 3) % 16] > 0.86)[..., None]
cian = (var[(fil_p + 9) % 16, (col_p + 7) % 16] > 0.9)[..., None]
vidrio = np.where(claro, vidrio * 0.55 + np.array([0.80, 0.93, 1.0]) * 0.45, vidrio)
vidrio = np.where(cian, vidrio * 0.6 + np.array([0.35, 0.92, 1.0]) * 0.4, vidrio)

blanco = np.array([0.90, 0.96, 1.0], np.float32)
franja = np.array([0.62, 0.84, 0.98], np.float32)
color = np.where(losa[..., None], franja, vidrio)
color = np.where(marco[..., None], blanco, color)

metal = np.where(marco, 0.0, np.where(losa, 0.2, 0.55))
suave = np.where(marco, 0.7, np.where(losa, 0.75, 0.95))
emision = np.where((marco | losa)[..., None], 0.0, vidrio * 0.55)

alto = np.clip(1 - d_mv / 0.055, 0, 1) ** 0.5 + np.clip(1 - d_losa / 0.275, 0, 1) ** 0.3 * 0.6
gx = (np.roll(alto, -1, 1) - np.roll(alto, 1, 1)) * 2.0
gy = (np.roll(alto, 1, 0) - np.roll(alto, -1, 0)) * 2.0
nrm = np.stack([-gx, -gy, np.ones_like(gx)], -1)
nrm /= np.linalg.norm(nrm, axis=-1, keepdims=True)


def guardar(arr, nombre):
    Image.fromarray((np.clip(arr, 0, 1) * 255 + 0.5).astype(np.uint8)).save(os.path.join(SALIDA, nombre))


guardar(color, "T_Fachada_Color.png")
guardar(np.stack([metal, metal, metal, suave], -1), "T_Fachada_MS.png")
guardar(nrm * 0.5 + 0.5, "T_Fachada_Normal.png")
guardar(emision, "T_Fachada_Emision.png")
print("Texturas de fachada en", SALIDA)
