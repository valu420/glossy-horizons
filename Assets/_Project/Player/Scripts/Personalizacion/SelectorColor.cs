// HU-02: pantalla de personalización (uGUI).
// Crea un botón por cada color de la paleta a partir de una plantilla,
// marca el color elegido (como un elemento seleccionado en el Explorador de Windows)
// y maneja los botones "Restablecer" y "Listo".
// La plantilla puede tener un hijo "Orbe" (la bolita que se pinta del color) y un hijo
// "Seleccion" (se muestra en el color elegido). Si no tiene "Orbe", se pinta la plantilla.
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class SelectorColor : MonoBehaviour
{
    [Header("Referencias (las asigna la herramienta del menú)")]
    public Transform contenedor;          // cuadrícula de botones (Grid Layout Group)
    public Button plantillaBoton;         // botón de ejemplo; se duplica por cada color
    public Button botonListo;
    public Button botonRestablecer;       // vuelve al color Original (opcional)
    public Text textoNombre;              // nombre del color elegido (barra de direcciones)
    public Text textoNombreDetalle;       // nombre grande en el panel de detalles (opcional)
    public Image vistaPrevia;             // bolita grande del color elegido (opcional)

    [Header("Al presionar Listo")]
    public UnityEvent alConfirmar = new UnityEvent();   // el HUD conecta aquí "cerrar panel"

    [Header("Animación")]
    public float escalaElegido = 1.12f;   // el orbe elegido crece un poco

    Button[] botones = new Button[0];
    GameObject[] marcas = new GameObject[0];
    Transform[] orbes = new Transform[0];
    int elegido;

    void Start()
    {
        var paleta = ColorPersonaje.Paleta;
        plantillaBoton.gameObject.SetActive(false);
        botones = new Button[paleta.Length];
        marcas = new GameObject[paleta.Length];
        orbes = new Transform[paleta.Length];

        for (int i = 0; i < paleta.Length; i++)
        {
            var b = Instantiate(plantillaBoton, contenedor);
            b.gameObject.SetActive(true);
            b.name = "Color_" + paleta[i].nombre;
            var orbe = b.transform.Find("Orbe");
            var img = orbe != null ? orbe.GetComponent<Image>() : b.GetComponent<Image>();
            img.color = ColorDeMuestra(i);
            orbes[i] = orbe;
            var marca = b.transform.Find("Seleccion");
            marcas[i] = marca != null ? marca.gameObject : null;
            int indice = i;
            b.onClick.AddListener(() => Elegir(indice));
            botones[i] = b;
        }

        if (botonListo != null) botonListo.onClick.AddListener(Confirmar);
        if (botonRestablecer != null) botonRestablecer.onClick.AddListener(() => Elegir(0));
        Marcar(ColorPersonaje.ColorActual);
    }

    static Color ColorDeMuestra(int i)
    {
        var c = ColorPersonaje.Paleta[i];
        return Color.Lerp(c.bordes, c.centro, 0.45f);
    }

    void Elegir(int indice)
    {
        ColorPersonaje.Elegir(indice);   // cambia el color en todos los personajes y lo guarda
        Marcar(indice);
    }

    void Update()
    {
        // el orbe elegido crece suave; los demás vuelven a su tamaño
        float k = 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime);
        for (int i = 0; i < orbes.Length; i++)
            if (orbes[i] != null)
                orbes[i].localScale = Vector3.Lerp(orbes[i].localScale, Vector3.one * (i == elegido ? escalaElegido : 1f), k);
    }

    void Marcar(int indice)
    {
        elegido = indice;
        for (int k = 0; k < marcas.Length; k++)
            if (marcas[k] != null) marcas[k].SetActive(k == indice);
        string nombre = ColorPersonaje.Paleta[indice].nombre;
        if (textoNombre != null) textoNombre.text = nombre;
        if (textoNombreDetalle != null) textoNombreDetalle.text = nombre;
        if (vistaPrevia != null) vistaPrevia.color = ColorDeMuestra(indice);
    }

    void Confirmar()
    {
        Debug.Log("Color guardado: " + ColorPersonaje.Paleta[ColorPersonaje.ColorActual].nombre);
        alConfirmar?.Invoke();
    }
}
