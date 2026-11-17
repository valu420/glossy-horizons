// HU-04: cámara en tercera y primera persona.
// Va en la raíz del prefab Player_Gel. Mueve la cámara principal de la escena
// (Camera.main), así funciona en cualquier escena sin cambiar nada.
//   Mouse / palanca derecha -> girar la cámara
//   V / botón Y (Triangle)  -> cambiar entre tercera y primera persona
//   Clic                    -> bloquear el cursor     Esc -> liberarlo
using UnityEngine;
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

    [Header("Sensibilidad")]
    public float sensibilidadMouse = 0.12f;   // grados por píxel
    public float sensibilidadMando = 150f;    // grados por segundo

    public float Yaw { get; private set; }
    public bool PrimeraPersona { get; private set; }

    Camera cam;
    float inclinacion = 15f;
    float nearOriginal;
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
        BloquearCursor(true);
    }

    void OnDisable()
    {
        mirarMouse.Disable(); mirarMando.Disable(); cambiarVista.Disable();
        BloquearCursor(false);
    }

    void LateUpdate()
    {
        if (cam == null) return;

        // Cursor: clic para bloquearlo, Esc para liberarlo
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) BloquearCursor(true);
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) BloquearCursor(false);

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
            Vector3 pivote = transform.position + Vector3.up * alturaPivote;
            Vector3 haciaAtras = rotacion * Vector3.back;
            float d = distancia;
            // Si hay una pared entre el personaje y la cámara, la cámara se acerca
            foreach (var choque in Physics.SphereCastAll(pivote, radioColision, haciaAtras, distancia,
                         ~0, QueryTriggerInteraction.Ignore))
            {
                if (choque.collider.transform.IsChildOf(transform)) continue;   // ignorar al propio jugador
                d = Mathf.Min(d, choque.distance);
            }
            cam.transform.SetPositionAndRotation(pivote + haciaAtras * d, rotacion);
        }
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
