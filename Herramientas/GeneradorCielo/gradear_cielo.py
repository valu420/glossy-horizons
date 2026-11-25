"""
Capa de nubes Frutiger Aero a partir de un cielo fotográfico HDRI (Poly Haven, CC0).

Toma el HDRI real (Assets/_Project/Shared/Textures/Sky/Cielo_Kloofendal_4k.hdr) y genera
T_NubesFrutiger.png (4096x2048, equirectangular, RGBA):
  - RGB = color de las nubes (blancas y limpias, sombras gris-azuladas suaves),
  - A   = cuánta nube hay (0 = cielo despejado).
El azul del cielo y el sol NO van en la textura: los dibuja el shader Glossy/CieloFrutiger
(degradado azul profundo arriba -> celeste abajo, sol donde esté la Directional Light),
y así las nubes pueden moverse sin arrastrar el sol ni el degradado.

Uso:  python gradear_cielo.py   [--densidad 1.0] [--exposicion 1.0]
"""
import argparse
import os

import numpy as np
from PIL import Image

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.normpath(os.path.join(AQUI, "..", ".."))
ENTRADA = os.path.join(RAIZ, "Assets", "_Project", "Shared", "Textures", "Sky", "Cielo_Kloofendal_4k.hdr")
SALIDA = os.path.join(RAIZ, "Assets", "_Project", "Shared", "Textures", "Sky", "T_NubesFrutiger.png")

LUM = np.array([0.2126, 0.7152, 0.0722], np.float32)


def leer_hdr(ruta):
    """Lector mínimo de Radiance .hdr (RGBE con RLE por canal)."""
    data = open(ruta, "rb").read()
    i = data.index(b"\n\n") + 2
    j = data.index(b"\n", i)
    dims = data[i:j].split()
    H, W = int(dims[1]), int(dims[3])
    buf = np.frombuffer(data, np.uint8, offset=j + 1)
    out = np.zeros((H, W, 4), np.uint8)
    pos = 0
    for y in range(H):
        pos += 4  # cabecera de la línea
        for c in range(4):
            x = 0
            while x < W:
                n = int(buf[pos])
                pos += 1
                if n > 128:
                    n -= 128
                    out[y, x:x + n, c] = buf[pos]
                    pos += 1
                else:
                    out[y, x:x + n, c] = buf[pos:pos + n]
                    pos += n
                x += n
    e = out[..., 3].astype(np.int32)
    escala = np.ldexp(1.0, e - 136).astype(np.float32) * (e > 0)
    return out[..., :3].astype(np.float32) * escala[..., None]


def suave(a, b, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def borroso(valor, peso, bloque=16, pasadas=4, radio=3):
    """Promedio ponderado muy borroso (valor/peso) en baja resolución; horizontal periódico."""
    H, W = peso.shape
    h, w = H // bloque, W // bloque
    v = valor.reshape(h, bloque, w, bloque, -1).sum((1, 3))
    p = peso.reshape(h, bloque, w, bloque).sum((1, 3))[..., None]
    for _ in range(pasadas):
        for eje in (0, 1):
            vs, ps = np.zeros_like(v), np.zeros_like(p)
            for k in range(-radio, radio + 1):
                if eje == 1:
                    vs += np.roll(v, k, 1); ps += np.roll(p, k, 1)
                else:
                    idx = np.clip(np.arange(h) + k, 0, h - 1)
                    vs += v[idx]; ps += p[idx]
            v, p = vs, ps
    r = v / np.maximum(p, 1e-6)
    return np.repeat(np.repeat(r, bloque, 0), bloque, 1)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--densidad", type=float, default=1.0, help="> 1 nubes más opacas, < 1 más tenues")
    ap.add_argument("--exposicion", type=float, default=1.0)
    ap.add_argument("--entrada", default=ENTRADA)
    ap.add_argument("--salida", default=SALIDA)
    a = ap.parse_args()

    print("Leyendo HDRI...")
    hdr = leer_hdr(a.entrada)
    H, W, _ = hdr.shape
    v = (np.arange(H, dtype=np.float32) + 0.5) / H
    elev = (0.5 - v) * np.pi  # radianes, + arriba
    u = (np.arange(W, dtype=np.float32) + 0.5) / W
    lon = (0.5 - u) * 2 * np.pi
    X = np.cos(elev)[:, None] * np.cos(lon)[None, :]
    Y = np.sin(elev)[:, None] * np.ones((1, W), np.float32)
    Z = np.cos(elev)[:, None] * np.sin(lon)[None, :]

    # --- sol de la foto (se borra: el shader dibuja el suyo)
    lum = hdr @ LUM
    fy, fx = np.unravel_index(np.argmax(lum[: H // 2]), lum[: H // 2].shape)
    sd = np.array([X[fy, fx], Y[fy, fx], Z[fy, fx]], np.float32)
    ang_sol = np.degrees(np.arccos(np.clip(X * sd[0] + Y * sd[1] + Z * sd[2], -1, 1)))
    print(f"Sol de la foto en elevación {np.degrees(elev[fy]):.1f}° (se quita de la capa)")

    # --- exposición + tonemap filmic suave (conserva el detalle de las nubes brillantes)
    cielo_ref = np.median(lum[int(H * 0.15): int(H * 0.35)])
    c = hdr * (0.42 / cielo_ref) * a.exposicion

    def filmic(x):
        A, B, C, D, E, F = 0.22, 0.30, 0.10, 0.20, 0.01, 0.30
        return ((x * (A * x + C * B) + D * E) / (x * (A * x + B) + D * F)) - E / F
    c = np.clip(filmic(c * 1.6) / filmic(4.5), 0, 1)

    # --- separar nubes del cielo azul
    # croma azul (b - r): alta en cielo despejado, ~0 en nube blanca
    croma = c[..., 2] - c[..., 0]
    # color de cielo despejado en 2D (varía con la altura Y con el lado del sol): promedio
    # borroso solo de los píxeles más azules de cada fila
    umbral = np.full(H, 1e9, np.float32)
    umbral[: H // 2] = np.percentile(croma[: H // 2], 70, axis=1)
    claro = (croma >= umbral[:, None]).astype(np.float32)
    claro[H // 2:] = 0
    sky = borroso(c * claro[..., None], claro)
    sky[H // 2 - 4:] = sky[H // 2 - 5]
    croma_cielo = np.maximum(sky[..., 2] - sky[..., 0], 0.02)

    alfa = np.clip(1 - croma / croma_cielo, 0, 1)
    # también cuenta lo que es más brillante que el cielo (bordes iluminados)
    brillo = np.clip((c @ LUM - sky @ LUM) / 0.35, 0, 1)
    alfa = np.maximum(alfa, brillo * 0.8)
    alfa = suave(0.22, 0.85, alfa) * a.densidad

    # color propio de la nube: luminancia "despremultiplicada" y casi neutra
    lum_c = c @ LUM
    lum_s = sky @ LUM
    L = np.clip((lum_c - lum_s * (1 - alfa)) / np.maximum(alfa, 0.15) * 1.06, 0.40, 1.0)
    L = np.where(alfa < 0.15, np.maximum(L, 0.85), L)  # bordes finos: blancos
    tinte_sombra = np.array([0.86, 0.91, 1.0], np.float32)  # sombras gris-azuladas
    t = (1 - L)[..., None] / 0.65
    nube = L[..., None] * (np.ones(3, np.float32) * (1 - t) + tinte_sombra * t)

    # quitar el sol y su halo, y todo lo que esté bajo el horizonte
    alfa = alfa * suave(5.0, 14.0, ang_sol)
    alfa = alfa * suave(-0.01, 0.015, Y)
    alfa = np.clip(alfa, 0, 1)

    rgb = np.clip(nube, 0, 1) ** (1 / 2.2)  # a sRGB
    img = np.concatenate([rgb, alfa[..., None]], -1)
    img += (np.random.default_rng(1).random(img.shape, dtype=np.float32) - 0.5) / 255.0
    img = (np.clip(img, 0, 1) * 255 + 0.5).astype(np.uint8)
    Image.fromarray(img).save(a.salida, optimize=True)
    print("Escrito", a.salida)


if __name__ == "__main__":
    main()
