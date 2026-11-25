// HUD: aviso corto que aparece y se desvanece sobre un botón.
// Sirve para botones que todavía no tienen función (por ejemplo, Emotes: "¡Próximamente!")
// y para avisos que otro script muestra con Mostrar() (por ejemplo, "no puedes hacer esto en el aire").
using UnityEngine;
using UnityEngine.UI;

public class BotonProximamente : MonoBehaviour
{
    public Text aviso;                 // texto hijo que se muestra al hacer clic
    public float duracion = 1.6f;
    [Tooltip("Activado: el aviso aparece al hacer clic en el botón. Desactivado: solo cuando otro script llama a Mostrar().")]
    public bool mostrarAlHacerClic = true;

    float tiempo = 999f;
    Vector2 posBase;

    void Start()
    {
        var b = GetComponent<Button>();
        if (b != null && mostrarAlHacerClic) b.onClick.AddListener(Mostrar);
        if (aviso != null) posBase = aviso.rectTransform.anchoredPosition;
        Poner(0f);
    }

    public void Mostrar() { tiempo = 0f; }

    void Update()
    {
        tiempo += Time.unscaledDeltaTime;
        float alfa = tiempo < duracion ? Mathf.Clamp01(tiempo * 6f) * Mathf.Clamp01((duracion - tiempo) * 3f) : 0f;
        Poner(alfa);
        if (aviso != null)
            aviso.rectTransform.anchoredPosition = posBase + new Vector2(0f, 12f * Mathf.Clamp01(tiempo * 4f));   // sube un poco al aparecer
    }

    void Poner(float alfa)
    {
        if (aviso == null) return;
        var c = aviso.color; c.a = alfa; aviso.color = c;
        var sombra = aviso.GetComponent<Outline>();
        if (sombra != null) { var s = sombra.effectColor; s.a = alfa; sombra.effectColor = s; }
    }
}
