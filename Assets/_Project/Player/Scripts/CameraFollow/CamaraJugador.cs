// HU-04: cámara en tercera y primera persona.
// Va en la raíz del prefab Player_Gel. Mueve la cámara principal de la escena
// (Camera.main), así funciona en cualquier escena sin cambiar nada.
//   Clic derecho + mouse / palanca derecha -> girar la cámara
//   (el mouse queda libre para hacer clic en la interfaz; mientras se mantiene
//    el clic derecho se oculta y se usa para mirar alrededor)
//   V / botón Y (Triangle)  -> cambiar entre tercera y primera persona
//   Si "Girar Con Clic Derecho" está desactivado: clic bloquea el cursor y Esc lo libera
// Modo vitrina (HU-02, panel de colores): la cámara se pone de frente al personaje,
// a la altura del cuerpo y un poco corrida para que el panel no lo tape.
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class CamaraJugador : MonoBehaviour
{
    [Header("Tercera persona")]
    public float distancia = 3.5f;
    public float alturaPivote = 1.2f;
    public float radioColision = 0.2f;   // la cámara se acerca si hay una pared detrás
    public float inclinacionMinTercera = -30f;
    public float inclinacionMaxTercera = 70f;

    [Header("Primera persona")]
    [Tooltip("Pieza Cabeza del modelo. Si se deja vacío, se busca por nombre.")]
    public Transform cabeza;
    public float inclinacionMinPrimera = -80f;
    public float inclinacionMaxPrimera = 80f;

    [Header("Mouse")]
    [Tooltip("Activado: el mouse queda libre y la cámara gira manteniendo el clic derecho.")]
    public bool girarConClicDerecho = true;

    [Header("Modo vitrina (panel de colores)")]
    public float distanciaVitrina = 3.0f;
    public float alturaVitrina = 0.8f;        // altura a la que mira (centro del cuerpo)
    public float inclinacionVitrina = 6f;     // casi horizontal: lo ve de frente
    [Tooltip("Corre la cámara hacia un lado para que el personaje quede a la derecha del panel")]
    public float corrimientoVitrina = 0.6f;
    public float rapidezVitrina = 5f;

    [Header("Sensibilidad")]
    public float sensibilidadMouse = 0.12f;   // grados por píxel
    public float sensibilidadMando = 150f;    // grados por segundo

    public float Yaw { get; private set; }
    public bool PrimeraPersona { get; private set; }

    Camera cam;
    float inclinacion = 15f;
    float nearOriginal;
    bool arrastrando;
    bool vitrina, primeraAntesDeVitrina;
    float pesoVitrina;                         // 0 = cámara normal, 1 = vitrina (transición suave)
    float yawVitrina;
    Renderer[] piezasCabeza = new Renderer[0];
    InputAction mirarMouse, mirarMando, cambiarVista;

    void Awake()
    {
        cam = Camera.main;
        if (cam != null) nearOriginal = cam.nearClipPlane;
        if (cabeza == null) cabeza = BuscarHijo(transform, "Cabeza");
        if (cabeza != null) piezasCabeza = cabeza.GetComponentsInChildren<Renderer>();
        Yaw = transform.eulerAngles.y;

        mirarMouse = new InputAction("MirarMouse", binding: "<Mouse>/delta");
        mirarMando = new InputAction("MirarMando", binding: "<Gamepad>/rightStick");
        cambiarVista = new InputAction("CambiarVista", binding: "<Keyboard>/v");
        cambiarVista.AddBinding("<Gamepad>/buttonNorth");
    }

    void OnEnable()
    {
        mirarMouse.Enable(); mirarMando.Enable(); cambiarVista.Enable();
        arrastrando = false;
        BloquearCursor(!girarConClicDerecho);
    }

    void OnDisable()
    {
        mirarMouse.Disable(); mirarMando.Disable(); cambiarVista.Disable();
        BloquearCursor(false);
    }

    // La usa PanelColoresHUD al abrir y cerrar el panel de colores
    public void ModoVitrina(bool activar)
    {
        if (activar == vitrina) return;
        vitrina = activar;
        if (activar)
        {
            primeraAntesDeVitrina = PrimeraPersona;
            if (PrimeraPersona) CambiarVista(false);       // en primera persona no se vería el personaje
            yawVitrina = Yaw;
            arrastrando = false;
            BloquearCursor(false);                         // mouse libre para hacer clic en los colores
        }
        else
        {
            if (primeraAntesDeVitrina) CambiarVista(true);
            BloquearCursor(!girarConClicDerecho);
        }
    }

    void LateUpdate()
    {
        if (cam == null) return;
        pesoVitrina = Mathf.MoveTowards(pesoVitrina, vitrina ? 1f : 0f, Time.unscaledDeltaTime * rapidezVitrina * 0.5f);
        if (vitrina)
        {
            // Mientras está la vitrina no se lee el mouse ni el mando
            ColocarTercera(Suave(pesoVitrina));
            return;
        }

        bool sobreInterfaz = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        var mouse = Mouse.current;
        if (girarConClicDerecho)
        {
            // Mouse libre; mientras se mantiene el clic derecho se oculta y gira la cámara
            if (mouse != null && mouse.rightButton.wasPressedThisFrame && !sobreInterfaz) { arrastrando = true; BloquearCursor(true); }
            if (arrastrando && (mouse == null || !mouse.rightButton.isPressed)) { arrastrando = false; BloquearCursor(false); }
        }
        else
        {
            // Modo clásico: clic bloquea el cursor (salvo sobre la interfaz), Esc lo libera
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !sobreInterfaz) BloquearCursor(true);
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) BloquearCursor(false);
        }

        if (cambiarVista.WasPressedThisFrame()) CambiarVista(!PrimeraPersona);

        // Girar la cámara (el mouse solo cuenta si el cursor está bloqueado)
        Vector2 giro = mirarMando.ReadValue<Vector2>() * sensibilidadMando * Time.deltaTime;
        if (Cursor.lockState == CursorLockMode.Locked)
            giro += mirarMouse.ReadValue<Vector2>() * sensibilidadMouse;
        Yaw += giro.x;
        inclinacion -= giro.y;
        inclinacion = PrimeraPersona
            ? Mathf.Clamp(inclinacion, inclinacionMinPrimera, inclinacionMaxPrimera)
            : Mathf.Clamp(inclinacion, inclinacionMinTercera, inclinacionMaxTercera);
        Quaternion rotacion = Quaternion.Euler(inclinacion, Yaw, 0f);

        if (PrimeraPersona)
        {
            Vector3 ojos = cabeza != null ? cabeza.position : transform.position + Vector3.up * 1.35f;
            cam.transform.SetPositionAndRotation(ojos, rotacion);
        }
        else
        {
            ColocarTercera(Suave(pesoVitrina));   // al cerrar el panel vuelve suave a la cámara normal
        }
    }

    static float Suave(float t) => t * t * (3f - 2f * t);

    // Tercera persona, mezclada con la vitrina según "mezcla" (0 = normal, 1 = vitrina)
    void ColocarTercera(float mezcla)
    {
        float yaw = Mathf.LerpAngle(Yaw, yawVitrina, mezcla);
        float incl = Mathf.Lerp(inclinacion, inclinacionVitrina, mezcla);
        float dist = Mathf.Lerp(distancia, distanciaVitrina, mezcla);
        float altura = Mathf.Lerp(alturaPivote, alturaVitrina, mezcla);
        Quaternion rotacion = Quaternion.Euler(incl, yaw, 0f);

        // el corrimiento mueve la cámara a la izquierda: el personaje queda a la derecha del panel
        Vector3 pivote = transform.position + Vector3.up * altura
                       + (rotacion * Vector3.left) * corrimientoVitrina * mezcla;
        Vector3 haciaAtras = rotacion * Vector3.back;
        float d = dist;
        // Si hay una pared entre el personaje y la cámara, la cámara se acerca
        foreach (var choque in Physics.SphereCastAll(pivote, radioColision, haciaAtras, dist,
                     ~0, QueryTriggerInteraction.Ignore))
        {
            if (choque.collider.transform.IsChildOf(transform)) continue;   // ignorar al propio jugador
            d = Mathf.Min(d, choque.distance);
        }
        cam.transform.SetPositionAndRotation(pivote + haciaAtras * d, rotacion);
    }

    public void CambiarVista(bool primeraPersona)
    {
        PrimeraPersona = primeraPersona;
        // En 1.ª persona la cabeza (y los ojos) no se ven, pero siguen proyectando sombra
        foreach (var r in piezasCabeza)
            r.shadowCastingMode = primeraPersona ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
        if (cam != null) cam.nearClipPlane = primeraPersona ? 0.05f : nearOriginal;
    }

    static void BloquearCursor(bool bloquear)
    {
        Cursor.lockState = bloquear ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !bloquear;
    }

    static Transform BuscarHijo(Transform padre, string nombre)
    {
        foreach (Transform t in padre.GetComponentsInChildren<Transform>(true))
            if (t.name == nombre) return t;
        return null;
    }
}
