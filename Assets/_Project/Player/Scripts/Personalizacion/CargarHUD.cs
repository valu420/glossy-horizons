// HUD: hace que el HUD del jugador aparezca solo en cualquier escena.
// Va en la raíz del prefab Player_Gel. Al empezar, si en la escena no hay un HUD,
// crea uno a partir del prefab HUD_Jugador (y un EventSystem si hace falta).
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public class CargarHUD : MonoBehaviour
{
    public GameObject prefabHUD;   // Player/UI/HUD_Jugador.prefab (lo asigna la herramienta del menú)

    void Start()
    {
        if (prefabHUD != null && FindAnyObjectByType<PanelColoresHUD>() == null)
            Instantiate(prefabHUD).name = prefabHUD.name;

        // Sin EventSystem los botones no reciben clics
        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }
}
