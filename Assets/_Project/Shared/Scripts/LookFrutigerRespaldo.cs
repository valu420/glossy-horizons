// Respaldo de las "mejoras opcionales" del Look Frutiger (herramienta de editor LookFrutiger).
// Guarda cómo estaba la escena antes de aplicarlas (materiales originales, objetos ocultos,
// cielo y sol), para que "Quitar" la deje exactamente igual. No hace nada en el juego.
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("")]
public class LookFrutigerRespaldo : MonoBehaviour
{
    [System.Serializable]
    public class MaterialesOriginales
    {
        public Renderer renderer;
        public Material[] materiales;
    }

    public List<MaterialesOriginales> materiales = new List<MaterialesOriginales>();
    public List<Renderer> ocultos = new List<Renderer>();

    [Header("Cielo y sol originales")]
    public bool guardoLuz;
    public Material cielo;
    public Light sol;
    public Color colorSol;
    public float intensidadSol;
    public Quaternion rotacionSol;

    public bool YaGuardado(Renderer r)
    {
        foreach (var m in materiales) if (m.renderer == r) return true;
        return false;
    }
}
