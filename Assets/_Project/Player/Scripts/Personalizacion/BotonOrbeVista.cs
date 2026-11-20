// HUD: botón orbe de vidrio con animación estilo Windows Vista.
// Va en el botón del HUD (Image invisible que marca la zona de clic + Button con Transition = None).
// Hijos (en este orden, de atrás hacia adelante):
//   Brillo    -> Image con T_BotonOrbeBrillo (se ilumina al pasar el mouse, queda detrás del personaje)
//   Burbuja   -> la burbuja de vidrio translúcida
//   Personaje -> el personaje y sus detalles (crece un poco al seleccionar)
// Animación:
//   reposo        -> "respira" con un brillo muy suave
//   mouse encima  -> el brillo aparece suave y el orbe crece un poco
//   clic          -> se hunde
//   panel abierto -> se queda encendido y el personaje se queda grande
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BotonOrbeVista : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public Image brillo;
    [Tooltip("Opcional: si está asignado, el orbe queda encendido mientras el panel está abierto")]
    public PanelColoresHUD panel;
    [Tooltip("Opcional: capa del personaje; crece al pasar el mouse o con el panel abierto")]
    public RectTransform personaje;

    public float escalaEncima = 1.06f;
    public float escalaClic = 0.92f;
    public float brilloReposo = 0.15f;
    public float rapidez = 10f;
    public float escalaPersonaje = 1.15f;

    bool encima, presionado;
    float brilloActual;
    Vector3 escalaBase;

    void Awake()
    {
        escalaBase = transform.localScale;
        PonerBrillo(0f);
    }

    public void OnPointerEnter(PointerEventData e) { encima = true; }
    public void OnPointerExit(PointerEventData e) { encima = false; presionado = false; }
    public void OnPointerDown(PointerEventData e) { presionado = true; }
    public void OnPointerUp(PointerEventData e) { presionado = false; }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;   // sigue animando aunque el juego esté en pausa
        bool abierto = panel != null && panel.Abierto;

        float respiro = brilloReposo * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2f));
        float objetivo = (encima || abierto) ? 1f : respiro;
        if (presionado) objetivo = 0.6f;
        brilloActual = Mathf.Lerp(brilloActual, objetivo, 1f - Mathf.Exp(-rapidez * dt));
        PonerBrillo(brilloActual);

        float escala = presionado ? escalaClic : (encima ? escalaEncima : 1f);
        transform.localScale = Vector3.Lerp(transform.localScale, escalaBase * escala, 1f - Mathf.Exp(-14f * dt));

        if (personaje != null)
        {
            float ep = (encima || abierto) ? escalaPersonaje : 1f;
            personaje.localScale = Vector3.Lerp(personaje.localScale, Vector3.one * ep, 1f - Mathf.Exp(-12f * dt));
        }
    }

    void PonerBrillo(float a)
    {
        if (brillo == null) return;
        var c = brillo.color; c.a = a; brillo.color = c;
    }
}
