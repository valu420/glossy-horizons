// HU-02: abre y cierra el panel de colores dentro del juego.
// Va en el Canvas del HUD. El botón del HUD llama a Alternar(), y el botón
// "Listo" del panel (SelectorColor > Al Confirmar) llama a Cerrar().
//   Tecla C  -> abrir / cerrar el panel
//   Esc      -> cerrar el panel
// Solo se puede abrir con el personaje en el suelo; en el aire muestra un aviso.
// Mientras el panel está abierto se pausa el movimiento, se suelta el mouse y la
// cámara pasa a "modo vitrina": se pone de frente al personaje para verlo bien.
using UnityEngine;
using UnityEngine.InputSystem;

public class PanelColoresHUD : MonoBehaviour
{
    public GameObject panel;          // el panel de colores (empieza oculto)
    public Key tecla = Key.C;
    [Tooltip("Aviso que aparece si se intenta abrir el panel en el aire")]
    public BotonProximamente avisoEnElAire;

    public bool Abierto => panel != null && panel.activeSelf;

    void Start()
    {
        if (panel != null) panel.SetActive(false);
    }

    void Update()
    {
        var teclado = Keyboard.current;
        if (teclado == null) return;
        if (teclado[tecla].wasPressedThisFrame) Alternar();
        else if (Abierto && teclado.escapeKey.wasPressedThisFrame) Cerrar();
    }

    public void Alternar()
    {
        if (Abierto) Cerrar(); else Abrir();
    }

    public void Abrir()
    {
        if (panel == null) return;
        var jugador = FindAnyObjectByType<ControladorJugador>();
        if (jugador != null && !jugador.EnSuelo)
        {
            // En el aire no se puede: el personaje se quedaría flotando
            if (avisoEnElAire != null) avisoEnElAire.Mostrar();
            return;
        }
        var emotes = FindAnyObjectByType<PanelEmotesHUD>();
        if (emotes != null && emotes.Abierto) emotes.Cerrar();      // una ventana a la vez
        panel.SetActive(true);
        PausarJugador(jugador, true);
    }

    public void Cerrar()
    {
        if (panel == null) return;
        panel.SetActive(false);
        PausarJugador(FindAnyObjectByType<ControladorJugador>(), false);
    }

    static void PausarJugador(ControladorJugador jugador, bool pausar)
    {
        if (jugador == null)
        {
            // Sin jugador en la escena: solo mostrar/ocultar el cursor
            Cursor.lockState = pausar ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = pausar;
            return;
        }
        jugador.enabled = !pausar;                                // deja de moverse
        var camara = jugador.GetComponent<CamaraJugador>();
        if (camara != null) camara.ModoVitrina(pausar);          // la cámara se pone de frente al personaje
        var anim = jugador.GetComponent<AnimacionGel>();
        if (anim != null) anim.MirarACamara(pausar);             // se voltea a mirar a la pantalla
    }
}
