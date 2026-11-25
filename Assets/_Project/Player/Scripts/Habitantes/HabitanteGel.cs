// HU-38 / HU-39: habitante de gel de la isla (NPC). Va en la raíz del prefab Habitante_Gel, que es una variante
// de Player_Gel sin controles, sin cámara, sin HUD y sin el anillo de luz (eso distingue al jugador).
//   - Le da a AnimacionGel su "movimiento" (IMovimientoGel), así se anima igual que el jugador.
//   - Máquina de estados (HU-39, CA4): Pasear, Detenido y Reaccionar (HU-40, ver HabitanteGel.Reacciones.cs).
//     Quedan listos para las siguientes historias: UsarObjeto HU-41, Conversar HU-42, DeLaMano HU-43, Perseguir HU-44.
//       Pasear   -> camina (a veces corre) con NavMeshAgent hasta un punto de interés, prefiriendo los caminos.
//       Detenido -> se queda unos segundos mirando alrededor y vuelve a pasear.
//       Reaccionar -> hace un emote en respuesta al jugador (saludar, bailar, enojarse, marearse...).
//   - Rendimiento (HU-38, CA5): si está lejos de la cámara apaga su animación (sigue caminando).
using UnityEngine;
using UnityEngine.AI;

public enum EstadoHabitante { Pasear, Detenido, Reaccionar, Conversar, UsarObjeto, DeLaMano, Perseguir }

[RequireComponent(typeof(NavMeshAgent))]
public partial class HabitanteGel : MonoBehaviour, IMovimientoGel
{
    [Header("Estado (solo lectura en Play)")]
    [SerializeField] EstadoHabitante estado = EstadoHabitante.Detenido;
    public EstadoHabitante Estado => estado;

    [Header("Pasear")]
    public Vector2 velocidadCaminar = new Vector2(1.4f, 2.1f);   // cada habitante elige la suya en este rango
    public float velocidadCorrer = 4.2f;
    [Range(0f, 1f)] public float probabilidadCorrer = 0.15f;      // por cada paseo
    public float segundosAtascado = 4f;                           // si no avanza, busca otro destino

    [Header("Detenido")]
    public Vector2 pausa = new Vector2(2f, 7f);                   // segundos quieto entre paseos
    public float giroMaximo = 70f;                                // al mirar alrededor
    public float rapidezGiro = 2.5f;

    [Header("Rendimiento")]
    public float distanciaAnimacion = 70f;                        // más lejos que esto no se anima

    // ---- IMovimientoGel
    public bool Activo => true;
    public Vector3 VelocidadHorizontal { get; private set; }
    public bool EnSuelo => true;
    public bool Corriendo { get; private set; }
    public bool SaltoEsteFrame => false;
    public float VelocidadVertical => 0f;
    public bool ModoCharco { get; set; }
    public float VelocidadCharco => 1.5f;

    NavMeshAgent agente;
    AnimacionGel animacion;
    float miVelocidad, finEstado, siguienteGiro, siguienteRevision, tiempoSinAvanzar;
    float rumboBase, rumboObjetivo;

    void Awake()
    {
        agente = GetComponent<NavMeshAgent>();
        animacion = GetComponent<AnimacionGel>();
        miVelocidad = Random.Range(velocidadCaminar.x, velocidadCaminar.y);
        siguienteRevision = Time.time + Random.Range(0f, 0.5f);
        IniciarReacciones();
    }

    void Start()
    {
        // el agente viene apagado en el prefab: se enciende cuando la navegación ya está cargada
        if (NavMesh.SamplePosition(transform.position, out var hit, 3f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            agente.enabled = true;
            agente.Warp(hit.position);
        }
        else Debug.LogWarning($"[Habitantes] {name} no está sobre la malla de navegación: se queda quieto.");
        // empiezan quietos un rato distinto cada uno, para que no arranquen todos a la vez
        Detener(Random.Range(0.5f, pausa.y));
    }

    void Update()
    {
        float t = Time.time;
        RevisarAnimacion(t);
        ActualizarReacciones(t);          // HU-40

        switch (estado)
        {
            case EstadoHabitante.Pasear: ActualizarPaseo(t); break;
            case EstadoHabitante.Detenido: ActualizarDetenido(t); break;
            case EstadoHabitante.Reaccionar: ActualizarReaccion(t); break;
            default: ActualizarDetenido(t); break;     // estados de las próximas HU: por ahora, quieto
        }

        VelocidadHorizontal = agente.enabled ? new Vector3(agente.velocity.x, 0f, agente.velocity.z) : Vector3.zero;
    }

    // ------------------------------------------------------------------ estados
    public void CambiarEstado(EstadoHabitante nuevo) { estado = nuevo; }

    void Pasear()
    {
        var nav = NavegacionIsla.Instancia;
        if (!agente.enabled || !agente.isOnNavMesh || nav == null || !nav.DestinoAleatorio(transform.position, out var destino))
        { Detener(Random.Range(pausa.x, pausa.y)); return; }

        Corriendo = Random.value < probabilidadCorrer;
        agente.speed = Corriendo ? velocidadCorrer : miVelocidad;
        agente.isStopped = false;
        agente.SetDestination(destino);
        tiempoSinAvanzar = 0f;
        estado = EstadoHabitante.Pasear;
    }

    void ActualizarPaseo(float t)
    {
        if (!agente.enabled || !agente.isOnNavMesh) { Detener(pausa.y); return; }
        if (agente.pathPending) return;
        if (agente.remainingDistance <= agente.stoppingDistance + 0.1f) { Detener(Random.Range(pausa.x, pausa.y)); return; }

        // atascado (otro habitante en un pasillo, un rincón...): busca otro destino
        tiempoSinAvanzar = agente.velocity.sqrMagnitude < 0.05f ? tiempoSinAvanzar + Time.deltaTime : 0f;
        if (tiempoSinAvanzar > segundosAtascado) Pasear();
    }

    void Detener(float segundos)
    {
        estado = EstadoHabitante.Detenido;
        Corriendo = false;
        if (agente.enabled && agente.isOnNavMesh) { agente.isStopped = true; agente.ResetPath(); }
        finEstado = Time.time + segundos;
        rumboBase = rumboObjetivo = transform.eulerAngles.y;
        siguienteGiro = Time.time + Random.Range(0.8f, 2.5f);
    }

    void ActualizarDetenido(float t)
    {
        // mira alrededor
        if (t >= siguienteGiro)
        {
            siguienteGiro = t + Random.Range(1.5f, 3.5f);
            rumboObjetivo = rumboBase + Random.Range(-giroMaximo, giroMaximo);
        }
        var objetivo = Quaternion.Euler(0f, rumboObjetivo, 0f);
        if (Quaternion.Angle(transform.rotation, objetivo) > 0.5f)
            transform.rotation = Quaternion.Slerp(transform.rotation, objetivo, rapidezGiro * Time.deltaTime);

        if (estado == EstadoHabitante.Detenido && t >= finEstado) Pasear();
    }

    // ------------------------------------------------------------------ rendimiento
    void RevisarAnimacion(float t)
    {
        if (t < siguienteRevision) return;
        siguienteRevision = t + 0.5f;
        var cam = Camera.main;
        if (animacion == null || cam == null) return;
        bool cerca = (cam.transform.position - transform.position).sqrMagnitude < distanciaAnimacion * distanciaAnimacion;
        if (animacion.enabled != cerca) animacion.enabled = cerca;
    }
}
