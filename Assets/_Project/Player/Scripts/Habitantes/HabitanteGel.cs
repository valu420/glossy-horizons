// HU-38: habitante de gel de la isla (NPC). Va en la raíz del prefab Habitante_Gel, que es una variante de
// Player_Gel sin controles, sin cámara, sin HUD y sin el anillo de luz (eso distingue al jugador).
//   - Le da a AnimacionGel su "movimiento" (IMovimientoGel): por ahora está de pie en su sitio; en HU-39
//     este mismo componente lo hará pasear.
//   - De vez en cuando gira despacio a mirar hacia otro lado, para que se vea vivo.
//   - Rendimiento (CA5): si está lejos de la cámara apaga su animación y la vuelve a encender al acercarse.
using UnityEngine;

public class HabitanteGel : MonoBehaviour, IMovimientoGel
{
    [Header("Mirar alrededor")]
    public Vector2 pausaGiro = new Vector2(4f, 10f);       // segundos entre un giro y otro
    public float giroMaximo = 70f;                         // grados respecto a su dirección inicial
    public float rapidezGiro = 2.5f;

    [Header("Rendimiento")]
    public float distanciaAnimacion = 70f;                  // más lejos que esto no se anima

    // IMovimientoGel (HU-39 los llenará al caminar)
    public bool Activo => true;
    public Vector3 VelocidadHorizontal { get; private set; }
    public bool EnSuelo => true;
    public bool Corriendo { get; private set; }
    public bool SaltoEsteFrame => false;
    public float VelocidadVertical => 0f;
    public bool ModoCharco { get; set; }
    public float VelocidadCharco => 1.5f;

    AnimacionGel animacion;
    float rumboBase, rumboObjetivo, siguienteGiro, siguienteRevision;

    void Awake()
    {
        animacion = GetComponent<AnimacionGel>();
        rumboBase = rumboObjetivo = transform.eulerAngles.y;
        // cada habitante empieza en un momento distinto, para que no giren todos a la vez
        siguienteGiro = Time.time + Random.Range(0.5f, pausaGiro.y);
        siguienteRevision = Time.time + Random.Range(0f, 0.5f);
    }

    void Update()
    {
        float t = Time.time;

        if (t >= siguienteRevision)
        {
            siguienteRevision = t + 0.5f;
            var cam = Camera.main;
            if (animacion != null && cam != null)
            {
                bool cerca = (cam.transform.position - transform.position).sqrMagnitude < distanciaAnimacion * distanciaAnimacion;
                if (animacion.enabled != cerca) animacion.enabled = cerca;
            }
        }

        if (t >= siguienteGiro)
        {
            siguienteGiro = t + Random.Range(pausaGiro.x, pausaGiro.y);
            rumboObjetivo = rumboBase + Random.Range(-giroMaximo, giroMaximo);
        }
        var objetivo = Quaternion.Euler(0f, rumboObjetivo, 0f);
        if (Quaternion.Angle(transform.rotation, objetivo) > 0.5f)
            transform.rotation = Quaternion.Slerp(transform.rotation, objetivo, rapidezGiro * Time.deltaTime);
    }
}
