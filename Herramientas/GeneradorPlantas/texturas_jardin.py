"""Texturas del jardín Frutiger: atlas de flores (4 colores), roca clara, tierra y grava.
Uso: python texturas_jardin.py <carpeta_salida>"""
import sys, os, math
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage as nd
out = sys.argv[1]; os.makedirs(out, exist_ok=True)
rng = np.random.default_rng(21)

def ruido(n, escala, oct=4):
    r = np.zeros((n, n))
    for o in range(oct):
        f = escala * 2 ** o
        base = rng.normal(0, 1, (int(n / f) + 2, int(n / f) + 2))
        img = np.array(Image.fromarray(base.astype(np.float32)).resize((n, n), Image.BICUBIC))
        r += img / 2 ** o
    return (r - r.min()) / (r.max() - r.min())

# ---------- Atlas de flores: 4 cuadrantes (rosa, amarillo, blanco, lila), flor de 5-6 pétalos ----------
N = 2048; Q = N // 2; S = 2
def flor(color_borde, color_centro, petalos, centro):
    img = Image.new("RGBA", (Q * S, Q * S), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    for k in range(petalos):
        ang = k / petalos * 2 * math.pi - math.pi / 2
        cx, cy = 0.5 + math.cos(ang) * 0.22, 0.5 + math.sin(ang) * 0.22
        d.ellipse([(cx - 0.2) * Q * S, (cy - 0.2) * Q * S, (cx + 0.2) * Q * S, (cy + 0.2) * Q * S], fill=(255, 255, 255, 255))
    a = np.array(img)[..., 3] / 255.0
    yy, xx = np.mgrid[0:Q * S, 0:Q * S] / (Q * S)
    r = np.hypot(xx - 0.5, yy - 0.5)
    t = np.clip(r / 0.44, 0, 1)[..., None]
    col = np.array(color_centro, float) * (1 - t) + np.array(color_borde, float) * t
    venas = (np.sin(np.arctan2(yy - 0.5, xx - 0.5) * petalos * 3) * 0.5 + 0.5) * 0.08
    col *= (1 - venas[..., None])
    c = np.clip(1 - r / 0.075, 0, 1)[..., None]
    col = col * (1 - c) + np.array(centro, float) * c
    return Image.fromarray(np.dstack([col, a * 255]).clip(0, 255).astype(np.uint8)).resize((Q, Q), Image.LANCZOS)
atlas = Image.new("RGBA", (N, N), (0, 0, 0, 0))
atlas.alpha_composite(flor((255, 150, 205), (255, 80, 165), 5, (255, 220, 70)), (0, 0))       # rosa
atlas.alpha_composite(flor((255, 230, 90), (255, 190, 30), 6, (240, 130, 30)), (Q, 0))        # amarillo
atlas.alpha_composite(flor((255, 255, 255), (235, 240, 255), 5, (255, 210, 60)), (0, Q))      # blanco
atlas.alpha_composite(flor((200, 170, 255), (140, 100, 240), 6, (255, 235, 120)), (Q, Q))     # lila
arr = np.array(atlas).astype(float); al = arr[..., 3:4] / 255
prom = (arr[..., :3] * al).sum((0, 1)) / max(1, al.sum()); arr[..., :3] = arr[..., :3] * al + prom * (1 - al)
Image.fromarray(arr.astype(np.uint8)).save(os.path.join(out, "T_AtlasFlores.png"))

# ---------- Roca clara (granito suave, tono crema-gris) ----------
n = 1024
r1, r2 = ruido(n, 64), ruido(n, 8, 3)
base = np.array([196, 192, 182.0]) * (0.78 + 0.3 * r1[..., None]) * (0.9 + 0.12 * r2[..., None])
motas = (rng.random((n, n)) < 0.02)
base[motas] *= 0.7
Image.fromarray(np.clip(base, 0, 255).astype(np.uint8)).save(os.path.join(out, "T_Roca.png"))

# ---------- Tierra de los canteros (oscura, húmeda) ----------
t = ruido(n, 32)
tierra = np.array([70, 50, 36.0]) * (0.75 + 0.4 * t[..., None])
grumos = nd.gaussian_filter(rng.random((n, n)), 1.2) > 0.55
tierra[grumos] *= 1.18
Image.fromarray(np.clip(tierra, 0, 255).astype(np.uint8)).save(os.path.join(out, "T_Tierra.png"))

# ---------- Grava del camino (piedritas claras) ----------
g = np.zeros((n, n, 3)) + np.array([205, 198, 182.0])
img = Image.fromarray(g.astype(np.uint8)); d = ImageDraw.Draw(img)
for k in range(5200):
    x, y = rng.uniform(0, n), rng.uniform(0, n); rr = rng.uniform(4, 11)
    tono = rng.uniform(0.72, 1.08); c = tuple(int(v * tono) for v in (215, 208, 192))
    for dx in (-n, 0, n):
        for dy in (-n, 0, n):
            d.ellipse([x + dx - rr, y + dy - rr * 0.8, x + dx + rr, y + dy + rr * 0.8], fill=c, outline=tuple(int(v * 0.8) for v in c))
img.save(os.path.join(out, "T_Grava.png"))
print("ok")
