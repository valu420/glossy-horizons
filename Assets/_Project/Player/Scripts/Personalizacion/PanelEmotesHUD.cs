// HU-06: ventana de emotes dentro del juego (estilo Frutiger Aero, como Personalización).
// Va en el Canvas del HUD.
//   Tecla G        -> abrir / cerrar la ventana
//   Teclas 1 a 9   -> con la ventana abierta, hacen el emote de ese número
//   Clic           -> también hace el emote
//   Esc            -> cerrar
//   Gelatina       -> el personaje queda derretido como charco; G lo vuelve a la normalidad
// Al elegir un emote la ventana se cierra y el personaje lo hace. En el aire o
// moviéndose no se puede: aparece un aviso sobre el botón de Emotes.
// No pausa al jugador: si se mueve, el emote se corta.
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PanelEmotesHUD : MonoBehaviour
{
    public GameObject panel;                    // la ventana (empieza oculta)
    public Key tecla = Key.G;
    public Button[] botones = new Button[0];    // los 9 emotes, en orden
    [Tooltip("Aviso que aparece si no se puede hacer el emote (en el aire o moviéndose)")]
    public BotonProximamente avisoNoSePuede;

    public bool Abierto => panel != null && panel.activeSelf;

    static readonly Key[] Numeros = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };
    static readonly Key[] Teclado = { Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9 };

    void Start()
    {
        if (panel != null) panel.SetActive(false);
        for (int i = 0; i < botones.Length; i++)
        {
            if (botones[i] == null) continue;
            int indice = i;
            botones[i].onClick.AddListener(() => Elegir(indice));
        }
    }

    void Update()
    {
        var teclado = Keyboard.current;
        if (teclado == null) return;
        if (teclado[tecla].wasPressedThisFrame)
        {
            var anim = AnimacionJugador();
            if (anim != null && anim.EnCharco) { anim.SalirDeCharco(); return; }   // G vuelve a la normalidad
            Alternar();
            return;
        }
        if (!Abierto) return;
        if (teclado.escapeKey.wasPressedThisFrame) { Cerrar(); return; }
        for (int i = 0; i < Numeros.Length; i++)
            if (teclado[Numeros[i]].wasPressedThisFrame || teclado[Teclado[i]].wasPressedThisFrame) { Elegir(i); return; }
    }

    // la animación del JUGADOR (no la de un habitante de la isla, que también tiene AnimacionGel)
    static AnimacionGel AnimacionJugador()
    {
        var jugador = FindAnyObjectByType<ControladorJugador>();
        return jugador != null ? jugador.GetComponent<AnimacionGel>() : null;
    }

    public void Alternar()
    {
        if (Abierto) Cerrar(); else Abrir();
    }

    public void Abrir()
    {
        if (panel == null) return;
        var colores = FindAnyObjectByType<PanelColoresHUD>();
        if (colores != null && colores.Abierto) colores.Cerrar();   // una ventana a la vez
        panel.SetActive(true);
    }

    public void Cerrar()
    {
        if (panel != null) panel.SetActive(false);
    }

    public void Elegir(int indice)
    {
        var anim = AnimacionJugador();
        if (anim != null && anim.HacerEmote(indice)) Cerrar();
        else if (avisoNoSePuede != null) avisoNoSePuede.Mostrar();
    }
}
