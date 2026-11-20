// Fondo animado (aurora + burbujas) para cualquier interfaz del juego.
// Uso: un Image SIN sprite con el material M_AuroraUI y este componente.
// Crea una copia del material para esta imagen y le pasa la proporción del rectángulo,
// así las burbujas se ven redondas sin importar el tamaño de la ventana.
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class FondoVivo : MonoBehaviour
{
    Image img;
    Material copia;
    static readonly int IdAspecto = Shader.PropertyToID("_Aspecto");
    static readonly int IdLocal = Shader.PropertyToID("_Local");

    void Start()
    {
        img = GetComponent<Image>();
        if (img.material == null || img.material == img.defaultMaterial) return;
        copia = new Material(img.material) { name = img.material.name + " (FondoVivo)" };
        copia.SetFloat(IdLocal, 1f);
        img.material = copia;
    }

    void Update()
    {
        if (copia == null) return;
        var r = ((RectTransform)transform).rect;
        if (r.height > 0f) copia.SetFloat(IdAspecto, r.width / r.height);
    }

    void OnDestroy()
    {
        if (copia != null) Destroy(copia);
    }
}
