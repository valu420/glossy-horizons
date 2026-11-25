# Generador de plantas (Glossy Horizons)

Scripts para volver a crear las plantas de `Assets/_Project/Shared/Models/Plantas` (modelos 100 % propios).

1. Atlas de hojas y flores (necesita Python con Pillow, numpy y scipy):
   `python atlas_hojas.py T_AtlasPlantas.png`
2. Modelos (Blender 4.5, sin abrir la interfaz):
   `"C:/Program Files/Blender Foundation/Blender 4.5/blender.exe" -b -P generar_plantas.py -- salida T_AtlasPlantas.png`

Crea Arbusto_Redondo_1/2, Arbusto_Flores_1/2, Helecho_1/2 y Planta_Interior_1/2 en FBX y una vista previa.
Copia los .fbx a `Assets/_Project/Shared/Models/Plantas` y el atlas a `Assets/_Project/Shared/Textures`.
Para cambiar una especie, edita su función en `generar_plantas.py` (cantidad de hojas, tamaño, semilla).
