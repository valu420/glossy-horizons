// HU-04: movimiento del jugador (y datos para las animaciones de HU-05).
// Va en la raíz del prefab Player_Gel, junto con CamaraJugador.
//   WASD / palanca izquierda  -> moverse (según hacia dónde mira la cámara)
//   Shift / clic palanca izq. -> correr
//   Espacio / botón A (Cross) -> saltar
// El CharacterController hace que el personaje choque con el escenario.
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class ControladorJugador : MonoBehaviour
{
    [Header("Movimiento")]
    public float velocidadCaminar = 3f;
    public float velocidadCorrer = 6f;
    public float velocidadGiro = 12f;

    [Header("Salto")]
    public float alturaSalto = 1.4f;
    public float gravedad = -11f;   // baja = salto más flotante

    // Datos que pueden leer otros scripts (por ejemplo, las animaciones de HU-05)
    public Vector3 VelocidadHorizontal { get; private set; }
    public bool EnSuelo => cc != null && cc.isGrounded;
    public bool Corriendo { get; private set; }
    public bool SaltoEsteFrame { get; private set; }
    public float VelocidadVertical => velocidadVertical;   // > 0 subiendo, < 0 cayendo

    CharacterController cc;
    CamaraJugador camara;
    float velocidadVertical;
    InputAction mover, saltar, correr;

    // Se ejecuta al agregar el componente en el editor: tamaño del "cuerpo" del jugador
    void Reset()
    {
        var c = GetComponent<CharacterController>();
        c.height = 1.6f;
        c.radius = 0.3f;
        c.center = new Vector3(0f, 0.8f, 0f);
        c.stepOffset = 0.3f;
    }

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        camara = GetComponent<CamaraJugador>();

        mover = new InputAction("Mover");
        mover.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
        mover.AddBinding("<Gamepad>/leftStick");

        saltar = new InputAction("Saltar", binding: "<Keyboard>/space");
        saltar.AddBinding("<Gamepad>/buttonSouth");

        correr = new InputAction("Correr", binding: "<Keyboard>/leftShift");
        correr.AddBinding("<Gamepad>/leftStickPress");
    }

    void OnEnable() { mover.Enable(); saltar.Enable(); correr.Enable(); }
    void OnDisable() { mover.Disable(); saltar.Disable(); correr.Disable(); }

    void Update()
    {
        // Dirección relativa a la cámara (solo su giro horizontal)
        Vector2 entrada = mover.ReadValue<Vector2>();
        float yaw = camara != null ? camara.Yaw : transform.eulerAngles.y;
        Quaternion giroCamara = Quaternion.Euler(0f, yaw, 0f);
        Vector3 direccion = giroCamara * new Vector3(entrada.x, 0f, entrada.y);
        if (direccion.sqrMagnitude > 1f) direccion.Normalize();

        bool moviendose = direccion.sqrMagnitude > 0.01f;
        Corriendo = moviendose && correr.IsPressed();
        Vector3 horizontal = direccion * (Corriendo ? velocidadCorrer : velocidadCaminar);

        // Salto y gravedad
        SaltoEsteFrame = false;
        if (cc.isGrounded)
        {
            if (velocidadVertical < 0f) velocidadVertical = -2f;   // lo mantiene pegado al suelo
            if (saltar.WasPressedThisFrame())
            {
                velocidadVertical = Mathf.Sqrt(alturaSalto * -2f * gravedad);
                SaltoEsteFrame = true;
            }
        }
        velocidadVertical += gravedad * Time.deltaTime;

        cc.Move((horizontal + Vector3.up * velocidadVertical) * Time.deltaTime);
        VelocidadHorizontal = new Vector3(cc.velocity.x, 0f, cc.velocity.z);

        // Hacia dónde mira el personaje
        bool primeraPersona = camara != null && camara.PrimeraPersona;
        if (primeraPersona)
            transform.rotation = giroCamara;   // en 1.ª persona mira hacia donde mira la cámara
        else if (moviendose)
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(direccion), velocidadGiro * Time.deltaTime);
    }
}
