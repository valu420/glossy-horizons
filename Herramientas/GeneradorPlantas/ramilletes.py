"""
Textura de ramilletes para árboles (T_RamilleteHojas.png, 2048x2048 RGBA, 4 cuadrantes):
  arriba-izq   ramillete de hojas anchas (tono medio)
  arriba-der   ramillete de hojas anchas (más claro, con sol)
  abajo-izq    rama de pino (agujas)
  abajo-der    rama de pino (variante)
Cada ramillete se arma "estampando" muchas hojas del atlas de plantas (T_AtlasPlantas) sobre
ramitas finas: así un árbol de 12 m se hace con ~250 tarjetas en vez de 50 000 hojas sueltas.
Uso: python ramilletes.py
"""
import math
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageEnhance

RAIZ = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
ATLAS = os.path.join(RAIZ, "Assets", "_Project", "Shared", "Textures", "T_AtlasPlantas.png")
SALIDA = os.path.join(RAIZ, "Assets", "_Project", "Shared", "Textures", "T_RamilleteHojas.png")
Q = 1024  # tamaño de cada cuadrante

atlas = Image.open(ATLAS).convert("RGBA")
A = atlas.size[0] // 2
hoja_fina = atlas.crop((0, 0, A, A))          # arriba-izquierda del atlas
hoja_ancha = atlas.crop((0, A, A, 2 * A))     # abajo-izquierda del atlas


def tono(img, brillo, color):
    img = ImageEnhance.Brightness(img).enhance(brillo)
    r, g, b, a = img.split()
    r = r.point(lambda v: min(255, int(v * color[0])))
    g = g.point(lambda v: min(255, int(v * color[1])))
    b = b.point(lambda v: min(255, int(v * color[2])))
    return Image.merge("RGBA", (r, g, b, a))


def ramillete(semilla, claro):
    rnd = random.Random(semilla)
    lienzo = Image.new("RGBA", (Q, Q), (0, 0, 0, 0))
    dib = ImageDraw.Draw(lienzo)
    cx, cy = Q / 2, Q * 0.55
    # ramitas: salen de abajo al centro y se abren
    puntas = []
    for k in range(7):
        ang = math.radians(-90 + rnd.uniform(-70, 70))
        largo = rnd.uniform(0.28, 0.42) * Q
        x0, y0 = cx + rnd.uniform(-20, 20), Q * 0.95
        x1, y1 = cx + math.cos(ang) * largo * 0.9, cy + math.sin(ang) * largo * 0.9
        dib.line((x0, y0, x1, y1), fill=(70, 48, 30, 255), width=rnd.randint(6, 10))
        puntas.append((x1, y1))
    # hojas: muchas, primero las de adentro (oscuras) y luego las de afuera (claras)
    n = 420
    for i in range(n):
        t = i / n
        px, py = rnd.choice(puntas)
        r = (0.04 + 0.36 * math.sqrt(rnd.random())) * Q
        a = rnd.uniform(0, 2 * math.pi)
        x, y = cx + (px - cx) * 0.35 + math.cos(a) * r, cy + (py - cy) * 0.35 + math.sin(a) * r * 0.85
        tam = int(rnd.uniform(0.07, 0.11) * Q)
        base = hoja_ancha if rnd.random() < 0.6 else hoja_fina
        brillo = 0.8 + 0.55 * t + (0.18 if claro else 0) + rnd.uniform(-0.08, 0.08)
        color = (rnd.uniform(0.95, 1.15), rnd.uniform(1.0, 1.12), rnd.uniform(0.35, 0.55))  # verde lima Frutiger
        h = tono(base.resize((tam, tam), Image.LANCZOS), brillo, color)
        h = h.rotate(math.degrees(-a) - 90 + rnd.uniform(-40, 40), resample=Image.BICUBIC, expand=True)
        lienzo.alpha_composite(h, (int(x - h.size[0] / 2), int(y - h.size[1] / 2)))
    return lienzo


def rama_pino(semilla):
    rnd = random.Random(semilla)
    lienzo = Image.new("RGBA", (Q, Q), (0, 0, 0, 0))
    dib = ImageDraw.Draw(lienzo)
    # eje de la rama (de abajo hacia arriba) con ramitas laterales cubiertas de agujas
    dib.line((Q / 2, Q * 0.98, Q / 2, Q * 0.06), fill=(70, 50, 32, 255), width=9)
    for k in range(26):
        yb = Q * (0.92 - 0.82 * k / 26) + rnd.uniform(-8, 8)
        ancho = Q * 0.42 * (1 - 0.75 * k / 26) * rnd.uniform(0.8, 1.1)
        for lado in (-1, 1):
            xe, ye = Q / 2 + lado * ancho, yb - Q * rnd.uniform(0.02, 0.09)
            dib.line((Q / 2, yb, xe, ye), fill=(60, 45, 30, 255), width=4)
            for s in range(int(ancho / 7)):
                t = s / max(1, int(ancho / 7))
                bx, by = Q / 2 + (xe - Q / 2) * t, yb + (ye - yb) * t
                for _ in range(4):
                    ang = math.radians(rnd.uniform(-160, -20) if rnd.random() < 0.5 else rnd.uniform(20, 160))
                    l = rnd.uniform(16, 40)
                    v = rnd.uniform(0.6, 1.25)
                    col = (int(28 * v), int(92 * v), int(48 * v), 255)
                    dib.line((bx, by, bx + math.cos(ang) * l, by + math.sin(ang) * l), fill=col, width=3)
    return lienzo


final = Image.new("RGBA", (2 * Q, 2 * Q), (0, 0, 0, 0))
final.paste(ramillete(3, False), (0, 0))
final.paste(ramillete(9, True), (Q, 0))
final.paste(rama_pino(5), (0, Q))
final.paste(rama_pino(13), (Q, Q))
# sangrado de color bajo las zonas transparentes (evita bordes oscuros con mipmaps)
arr = np.array(final)
arr[arr[..., 3] == 0, :3] = (60, 110, 45)
Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8)).save(SALIDA)
print("Escrito", SALIDA)
