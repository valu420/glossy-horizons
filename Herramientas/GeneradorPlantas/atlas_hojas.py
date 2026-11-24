"""Atlas de hojas y flores para las plantas generadas (2048 x 2048, 4 cuadrantes):
  arriba-izq:  hoja pequeña de arbusto       arriba-der: folíolo de helecho
  abajo-izq:   hoja grande de planta interior abajo-der:  flor (5 pétalos)
Cada elemento apunta hacia ARRIBA en su cuadrante, con la base abajo al centro.
Uso: python atlas_hojas.py <salida.png>"""
import sys, math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
N = 2048; Q = N // 2; S = 2
rng = np.random.default_rng(5)

def lienzo(): return Image.new("RGBA", (Q * S, Q * S), (0, 0, 0, 0))

def sombreado(img, claro, oscuro, venas=None, borde_osc=0.75):
    """Rellena la silueta (alfa) con degradado base->punta, borde más oscuro y nervaduras."""
    a = np.array(img)[..., 3] / 255.0
    H, W = a.shape
    yy, xx = np.mgrid[0:H, 0:W] / H
    t = np.clip(1 - yy, 0, 1)                                        # 0 base (abajo), 1 punta (arriba)
    col = np.array(oscuro, float) * (1 - t[..., None]) + np.array(claro, float) * t[..., None]
    from scipy import ndimage as nd
    dist = nd.distance_transform_edt(a > 0.5)
    borde = np.clip(dist / (H * 0.02), 0, 1)
    col *= (borde_osc + (1 - borde_osc) * borde)[..., None]
    ruido = nd.gaussian_filter(rng.normal(0, 1, (H, W)), H * 0.004) * 10
    col += ruido[..., None]
    if venas is not None:
        v = np.array(venas)[..., 3] / 255.0
        col = col * (1 - v[..., None] * 0.5) + np.array(claro, float) * 1.25 * v[..., None] * 0.5
    out = np.dstack([np.clip(col, 0, 255), a * 255]).astype(np.uint8)
    return Image.fromarray(out)

def P(x, y): return (x * Q * S, y * Q * S)

# ---------- 1. hoja pequeña de arbusto (ovalada con punta) ----------
def hoja(largo=0.86, ancho=0.3, punta=1.0):
    img = lienzo(); d = ImageDraw.Draw(img); pts = []
    for k in range(60):
        u = k / 59
        w = (math.sin(math.pi * u) ** 0.75) * ancho * (1 - 0.35 * u ** 3 * punta)
        pts.append((0.5 + w / 2, 0.95 - u * largo))
    pts += [(1 - x, y) for x, y in reversed(pts)]
    d.polygon([P(*p) for p in pts], fill=(255, 255, 255, 255))
    return img
img = hoja()
venas = lienzo(); dv = ImageDraw.Draw(venas)
dv.line([P(0.5, 0.95), P(0.5, 0.12)], fill=(0, 0, 0, 255), width=10)
for k in range(7):
    y = 0.85 - k * 0.1
    for s in (-1, 1): dv.line([P(0.5, y), P(0.5 + s * 0.12, y - 0.08)], fill=(0, 0, 0, 200), width=5)
hoja_arbusto = sombreado(img, (120, 200, 70), (40, 110, 35), venas)

# ---------- 2. folíolo de helecho (fronda pequeña con lóbulos) ----------
img = lienzo(); d = ImageDraw.Draw(img)
d.line([P(0.5, 0.97), P(0.5, 0.05)], fill=(255, 255, 255, 255), width=12)
for k in range(14):
    u = k / 13; y = 0.92 - u * 0.84; tam = 0.17 * (1 - u * 0.75) + 0.02
    for s in (-1, 1):
        cx = 0.5 + s * tam * 0.55
        d.ellipse([P(cx - tam * 0.55, y - tam * 0.22), P(cx + tam * 0.55, y + tam * 0.22)], fill=(255, 255, 255, 255))
helecho = sombreado(img, (140, 210, 80), (50, 120, 40), None, 0.8)

# ---------- 3. hoja grande de planta de interior (lanceolada brillante con nervios) ----------
img = hoja(largo=0.9, ancho=0.46, punta=1.4)
venas = lienzo(); dv = ImageDraw.Draw(venas)
dv.line([P(0.5, 0.96), P(0.5, 0.08)], fill=(0, 0, 0, 255), width=14)
for k in range(10):
    y = 0.88 - k * 0.075
    for s in (-1, 1): dv.line([P(0.5, y), P(0.5 + s * 0.2, y - 0.12)], fill=(0, 0, 0, 170), width=6)
hoja_grande = sombreado(img, (90, 185, 70), (25, 95, 35), venas)

# ---------- 4. flor de 5 pétalos (rosa con centro amarillo) ----------
img = lienzo(); d = ImageDraw.Draw(img)
for k in range(5):
    ang = k / 5 * 2 * math.pi - math.pi / 2
    cx, cy = 0.5 + math.cos(ang) * 0.2, 0.5 + math.sin(ang) * 0.2
    d.ellipse([P(cx - 0.2, cy - 0.2), P(cx + 0.2, cy + 0.2)], fill=(255, 255, 255, 255))
a = np.array(img)[..., 3] / 255.0
yy, xx = np.mgrid[0:Q * S, 0:Q * S] / (Q * S)
r = np.hypot(xx - 0.5, yy - 0.5)
col = np.array([255, 120, 190.0]) * (1 - np.clip(r / 0.42, 0, 1)[..., None]) + np.array([255, 205, 230.0]) * np.clip(r / 0.42, 0, 1)[..., None]
centro = np.clip(1 - r / 0.08, 0, 1)[..., None]
col = col * (1 - centro) + np.array([255, 210, 60.0]) * centro
flor = Image.fromarray(np.dstack([col, a * 255]).clip(0, 255).astype(np.uint8))

atlas = Image.new("RGBA", (N, N), (0, 0, 0, 0))
for img, (x, y) in ((hoja_arbusto, (0, 0)), (helecho, (Q, 0)), (hoja_grande, (0, Q)), (flor, (Q, Q))):
    atlas.alpha_composite(img.resize((Q, Q), Image.LANCZOS), (x, y))
# color de fondo transparente = promedio (bordes limpios con mipmaps)
arr = np.array(atlas).astype(float); al = arr[..., 3:4] / 255
prom = (arr[..., :3] * al).sum((0, 1)) / max(1, al.sum())
arr[..., :3] = arr[..., :3] * al + prom * (1 - al)
Image.fromarray(arr.astype(np.uint8)).save(sys.argv[1])
print("ok")
