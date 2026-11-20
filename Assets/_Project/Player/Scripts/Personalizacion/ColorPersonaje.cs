// HU-02: color del gel del personaje.
// Va en la raíz del jugador (Player_Gel) y en el personaje de la vista previa.
// Al iniciar aplica el color guardado y se actualiza solo cuando el jugador elige otro.
//   ColorPersonaje.Elegir(i)  -> cambia y guarda el color (se mantiene entre escenas)
//   ColorPersonaje.ColorActual -> índice del color guardado
// Por ahora se guarda con PlayerPrefs; cuando exista el guardado general (HU-31)
// basta con cambiar las dos líneas marcadas con [GUARDADO].
using System;
using UnityEngine;

public class ColorPersonaje : MonoBehaviour
{
    [Serializable]
    public struct ColorGel
    {
        public string nombre;
        public Color bordes;   // _ColorBase del shader GelMatcap
        public Color centro;   // _ColorClaro del shader GelMatcap

        public ColorGel(string nombre, string bordesHex, string centroHex)
        {
            this.nombre = nombre;
            ColorUtility.TryParseHtmlString("#" + bordesHex, out bordes);
            ColorUtility.TryParseHtmlString("#" + centroHex, out centro);
        }
    }

    public static readonly ColorGel[] Paleta =
    {
        //            nombre                bordes    centro       color base de la paleta
        new ColorGel("Original",           "195FD7", "78E1FF"),  // el azul con el que se diseñó el gel (color inicial)
        new ColorGel("Azul Cielo",         "0077C7", "83CBFC"),  // #0099FF
        new ColorGel("Cian Acuático",      "0099C7", "83E0FC"),  // #00C4FF
        new ColorGel("Aguamarina Suave",   "22B99F", "A0EBDE"),  // #3EDBC0
        new ColorGel("Verde Hierba",       "478C13", "ABEA7B"),  // #5BB318
        new ColorGel("Verde Lima",         "80AD02", "D7FA75"),  // #A4DE02
        new ColorGel("Amarillo Solar",     "C7A600", "FCE883"),  // #FFD500
        new ColorGel("Naranja Solar",      "C75A00", "FCB983"),  // #FF7300
        new ColorGel("Rojo Frutiger",      "BE141B", "F19599"),  // #E8262E
        new ColorGel("Magenta",            "C70063", "FC83BF"),  // #FF007F
        new ColorGel("Púrpura Eléctrico",  "6204F2", "C5A1FB"),  // #8A3FFC
    };

    const string Clave = "ColorPersonaje";

    // Aviso para todos los personajes en escena cuando cambia el color
    public static event Action<int> AlCambiarColor;

    public static int ColorActual =>
        Mathf.Clamp(PlayerPrefs.GetInt(Clave, 0), 0, Paleta.Length - 1);          // [GUARDADO] leer

    public static void Elegir(int indice)
    {
        indice = Mathf.Clamp(indice, 0, Paleta.Length - 1);
        PlayerPrefs.SetInt(Clave, indice); PlayerPrefs.Save();                      // [GUARDADO] escribir
        AlCambiarColor?.Invoke(indice);
    }

    Material instancia;

    void Awake()
    {
        // Una copia del material de gel para este personaje (no modifica el archivo M_Gel)
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            var m = r.sharedMaterial;
            if (m == null || m.shader == null || m.shader.name != "Glossy/GelMatcap") continue;
            if (instancia == null) instancia = new Material(m) { name = m.name + " (color)" };
            r.sharedMaterial = instancia;
        }
        Aplicar(ColorActual);
    }

    void OnEnable() { AlCambiarColor += Aplicar; }
    void OnDisable() { AlCambiarColor -= Aplicar; }
    void OnDestroy() { if (instancia != null) Destroy(instancia); }

    public void Aplicar(int indice)
    {
        if (instancia == null) return;
        var c = Paleta[Mathf.Clamp(indice, 0, Paleta.Length - 1)];
        instancia.SetColor("_ColorBase", c.bordes);
        instancia.SetColor("_ColorClaro", c.centro);
    }
}
