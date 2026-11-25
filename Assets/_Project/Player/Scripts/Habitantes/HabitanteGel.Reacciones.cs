// HU-40: los habitantes reaccionan al jugador con los mismos emotes que él (HU-06).
//   - Si el jugador saluda y el habitante lo ve        -> se gira y lo saluda (con un pequeño retraso natural).
//   - Si el jugador baila y lo ve                      -> baila con él mientras el jugador siga bailando.
//   - Si el jugador se vuelve gelatina y lo ve         -> se sorprende.
//   - Si el jugador lo empuja caminando                -> se desplaza hacia donde lo empujan y se enoja.
//   - Si el jugador lo empuja corriendo                -> sale más lejos y se marea.
//   - Algunos habitantes (no todos) son enamoradizos: a veces, al pasar el jugador cerca, se enamoran.
//   - Estando quietos, a veces siguen al jugador con la mirada cuando pasa cerca.
// "Lo ve" = está a menos de radioVision, delante de él y sin paredes en medio.
using UnityEngine;

public partial class HabitanteGel
{
    const int Saludar = 0, Bailar = 1, Amor = 4, Mareado = 5, Enojado = 6, Sorpresa = 7;

    [Header("Reacciones al jugador (HU-40)")]
    public float radioVision = 10f;
    public Vector2 retrasoReaccion = new Vector2(0.3f, 0.8f);
    [Range(0f, 1f)] public float probabilidadEnamoradizo = 0.2f;   // parte de los habitantes que se enamoran
    public float radioEnamorarse = 3.5f;
    public float esperaEntreAmores = 45f;
    [Range(0f, 1f)] public float probabilidadMirarAlJugador = 0.5f;

    // jugador (uno solo para todos los habitantes)
    static ControladorJugador jugador;
    static AnimacionGel animJugador;

    bool enamoradizo, curioso;
    int emoteVistoDelJugador = -1;
    bool jugadorEnCharco;
    float siguienteAmor;

    // reacción en curso
    int emotePendiente = -1;
    float inicioEmote;
    bool reaccionConJugador;          // se queda mirando al jugador mientras reacciona

    // empujones
    float empujando, sinEmpuje;
    bool empujeCorriendo;
    float esperaEmpuje;

    void IniciarReacciones()
    {
        enamoradizo = Random.value < probabilidadEnamoradizo;
        curioso = Random.value < probabilidadMirarAlJugador;
        siguienteAmor = Time.time + Random.Range(5f, esperaEntreAmores);
    }

    static bool BuscarJugador()
    {
        if (jugador != null) return true;
        jugador = FindAnyObjectByType<ControladorJugador>();
        animJugador = jugador != null ? jugador.GetComponent<AnimacionGel>() : null;
        return jugador != null;
    }

    void ActualizarReacciones(float t)
    {
        if (!BuscarJugador() || animJugador == null) return;
        var pj = jugador.transform.position;
        var rel = transform.position - pj; rel.y = 0f;
        float dist = rel.magnitude;

        // ---- empujones (no hace falta que lo vea: lo siente)
        Empujones(t, rel, dist);

        // ---- emotes del jugador: reacciona cuando el jugador EMPIEZA uno
        int emoteJugador = animJugador.EmoteActual;
        if (emoteJugador != emoteVistoDelJugador)
        {
            emoteVistoDelJugador = emoteJugador;
            if (emoteJugador == Saludar && LoVe(pj, dist)) Reaccionar(Saludar, true, true);
            else if (emoteJugador == Bailar && LoVe(pj, dist)) Reaccionar(Bailar, true, true);
        }
        // baile: si sigue bailando y yo ya terminé, vuelvo a bailar con él
        if (emoteJugador == Bailar && estado != EstadoHabitante.Reaccionar && dist < radioVision && LoVe(pj, dist))
            Reaccionar(Bailar, false, true);

        bool charco = animJugador.EnCharco;
        if (charco && !jugadorEnCharco && LoVe(pj, dist)) Reaccionar(Sorpresa, true, true);
        jugadorEnCharco = charco;

        // ---- enamorarse al pasar cerca (solo algunos, y de vez en cuando)
        if (enamoradizo && t >= siguienteAmor && dist < radioEnamorarse && estado != EstadoHabitante.Reaccionar && LoVe(pj, dist))
        {
            siguienteAmor = t + esperaEntreAmores * Random.Range(0.7f, 1.5f);
            if (Random.value < 0.6f) Reaccionar(Amor, true, true);
        }

        // ---- quieto: a veces sigue con la mirada al jugador que pasa
        if (curioso && estado == EstadoHabitante.Detenido && dist < 6f && dist > 0.8f && LoVe(pj, dist))
            rumboObjetivo = Quaternion.LookRotation(-rel).eulerAngles.y;
    }

    // ------------------------------------------------------------------ empujones
    void Empujones(float t, Vector3 rel, float dist)
    {
        var vel = jugador.enabled ? jugador.VelocidadDeseada : Vector3.zero;   // la real se frena al chocar con él
        float rapidez = vel.magnitude;
        bool contacto = dist < 0.85f && dist > 0.01f && rapidez > 0.5f && Vector3.Dot(vel / rapidez, rel / dist) > 0.35f;
        if (contacto)
        {
            empujando += Time.deltaTime; sinEmpuje = 0f;
            empujeCorriendo |= jugador.Corriendo;
            // lo desplaza hacia donde lo empujan (por la malla de navegación: no lo saca de la isla)
            float fuerza = jugador.Corriendo ? 1.6f : 1.05f;
            if (agente.enabled && agente.isOnNavMesh) agente.Move(rel / dist * rapidez * fuerza * Time.deltaTime);
            if (estado == EstadoHabitante.Pasear) { agente.isStopped = true; agente.ResetPath(); estado = EstadoHabitante.Detenido; finEstado = t + 1f; }
            if (animacion != null && animacion.EnEmote && emotePendiente < 0) animacion.CancelarEmote();
        }
        else if (empujando > 0f)
        {
            sinEmpuje += Time.deltaTime;
            // terminó el empujón: reacciona según cómo lo empujaron
            if (sinEmpuje > 0.15f || empujando > 0.5f)
            {
                if (t >= esperaEmpuje)
                {
                    Reaccionar(empujeCorriendo ? Mareado : Enojado, false, !empujeCorriendo);
                    esperaEmpuje = t + 2.5f;
                }
                empujando = 0f; empujeCorriendo = false;
            }
        }
    }

    // ------------------------------------------------------------------ ver al jugador
    bool LoVe(Vector3 posJugador, float dist)
    {
        if (dist > radioVision) return false;
        var dir = posJugador - transform.position; dir.y = 0f;
        if (dist > 1.2f && Vector3.Dot(transform.forward, dir.normalized) < -0.1f) return false;   // detrás de él
        var ojos = transform.position + Vector3.up * 1.3f;
        var objetivo = posJugador + Vector3.up * 1.0f;
        if (Physics.Linecast(ojos, objetivo, out var hit, ~0, QueryTriggerInteraction.Ignore))
        {
            var c = hit.collider;
            if (c.transform.IsChildOf(transform)) return true;                         // su propia cápsula
            if (c.GetComponentInParent<ControladorJugador>() == null) return false;    // algo en medio
        }
        return true;
    }

    // ------------------------------------------------------------------ reaccionar con un emote
    void Reaccionar(int emote, bool conRetraso, bool mirarJugador)
    {
        if (estado == EstadoHabitante.Reaccionar && emotePendiente < 0 && animacion != null && animacion.EnEmote
            && emote != Mareado && emote != Enojado) return;                           // ya está en una reacción
        estado = EstadoHabitante.Reaccionar;
        Corriendo = false;
        if (agente.enabled && agente.isOnNavMesh) { agente.isStopped = true; agente.ResetPath(); agente.velocity = Vector3.zero; }
        VelocidadHorizontal = Vector3.zero;
        emotePendiente = emote;
        reaccionConJugador = mirarJugador;
        inicioEmote = Time.time + (conRetraso ? Random.Range(retrasoReaccion.x, retrasoReaccion.y) : 0.05f);
        finEstado = inicioEmote + AnimacionGel.Duracion(emote) + 0.3f;
    }

    void ActualizarReaccion(float t)
    {
        if (reaccionConJugador && jugador != null)
        {
            var dir = jugador.transform.position - transform.position; dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 6f * Time.deltaTime);
        }
        if (emotePendiente >= 0 && t >= inicioEmote)
        {
            if (animacion != null && animacion.enabled) animacion.HacerEmote(emotePendiente);
            emotePendiente = -1;
        }
        if (t >= finEstado) Detener(Random.Range(1f, 3f));
    }
}
