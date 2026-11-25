// Globo aerostático decorativo del Hub: da vueltas lentas alrededor de un centro (la isla), con un recorrido
// que no es un círculo perfecto, sube y baja suave, se mece como si lo empujara el viento y gira despacio.
// Cada cierto tiempo "enciende el quemador": la llama crece y titila, la luz se aviva y el globo gana un poco
// de altura; al apagarse vuelve a bajar despacio. Así cada globo parece tener vida propia.
// Solo se mueve en Play; en el editor queda donde lo dejó el constructor (LookFrutiger.Globos).
using UnityEngine;

public class GloboAerostatico : MonoBehaviour
{
    [Header("Recorrido")]
    public Vector3 centro;                       // centro de la órbita (solo XZ)
    public float radio = 120f;
    public float altura = 60f;                   // altura media (Y del mundo)
    public float velocidad = 3f;                 // m/s a lo largo del recorrido
    public bool horario;
    [Range(0f, 0.4f)] public float ondulacion = 0.18f;   // cuánto se aleja/acerca del círculo
    public float fase;                           // 0..1, para que no vayan sincronizados

    [Header("Flotación")]
    public float subeBaja = 4f;                  // amplitud del sube y baja (m)
    public float periodoFlotacion = 22f;         // segundos
    public float meceo = 2.5f;                   // grados de balanceo
    public float giro = 3f;                      // grados por segundo sobre su eje

    [Header("Quemador")]
    public Transform fuego;                      // llama (pivote en su base)
    public Light luz;
    public Vector2 pausaQuemador = new Vector2(6f, 14f);
    public Vector2 duracionQuemador = new Vector2(1.2f, 2.6f);
    public float empujeQuemador = 3f;            // metros que gana en cada encendido
    public float intensidadLuz = 6f;

    float ang, sigEncendido, finEncendido, ganancia, llama;
    Vector3 escalaFuego = Vector3.one;
    Quaternion rotacionModelo = Quaternion.identity;   // la que trae el FBX al importarse
    System.Random azar;

    void Start()
    {
        rotacionModelo = Quaternion.Euler(0f, -fase * 360f, 0f) * transform.rotation;
        azar = new System.Random(name.GetHashCode() ^ Mathf.RoundToInt(fase * 100000f));
        ang = fase * Mathf.PI * 2f;
        if (fuego != null) escalaFuego = fuego.localScale;
        sigEncendido = Time.time + Rango(0.5f, pausaQuemador.y);
        transform.position = Posicion(Time.time);
    }

    float Rango(float a, float b) => a + (float)azar.NextDouble() * (b - a);

    Vector3 Posicion(float t)
    {
        float r = radio * (1f + ondulacion * Mathf.Sin(ang * 2f + fase * 9f) + ondulacion * 0.5f * Mathf.Sin(ang * 5f + fase * 3f));
        float y = altura + ganancia + subeBaja * Mathf.Sin((t / periodoFlotacion + fase) * Mathf.PI * 2f)
                  + subeBaja * 0.35f * Mathf.Sin((t / (periodoFlotacion * 0.43f) + fase * 2f) * Mathf.PI * 2f);
        return new Vector3(centro.x + Mathf.Cos(ang) * r, y, centro.z + Mathf.Sin(ang) * r);
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        float t = Time.time, dt = Time.deltaTime;

        // --- quemador: encendidos cada tanto ---
        bool encendido = t < finEncendido;
        if (!encendido && t >= sigEncendido)
        {
            finEncendido = t + Rango(duracionQuemador.x, duracionQuemador.y);
            sigEncendido = finEncendido + Rango(pausaQuemador.x, pausaQuemador.y);
            encendido = true;
        }
        // gana altura mientras quema y la pierde despacio después
        ganancia += (encendido ? empujeQuemador / 2f : -empujeQuemador / 12f) * dt;
        ganancia = Mathf.Clamp(ganancia, 0f, empujeQuemador * 1.5f);

        llama = Mathf.MoveTowards(llama, encendido ? 1f : 0f, dt * (encendido ? 4f : 1.5f));
        float titila = 1f + 0.12f * Mathf.Sin(t * 31f + fase * 10f) + 0.08f * Mathf.Sin(t * 17.3f);
        if (fuego != null)
        {
            float s = Mathf.Lerp(0.35f, 1.6f, llama) * titila;
            fuego.localScale = new Vector3(escalaFuego.x * Mathf.Lerp(s, 1f, 0.4f), escalaFuego.y * s, escalaFuego.z * Mathf.Lerp(s, 1f, 0.4f));
        }
        if (luz != null) luz.intensity = intensidadLuz * Mathf.Lerp(0.15f, 1f, llama) * titila;

        // --- recorrido ---
        float sentido = horario ? -1f : 1f;
        ang += sentido * velocidad / Mathf.Max(1f, radio) * dt;
        var p = Posicion(t);
        var avance = p - transform.position;
        transform.position = p;

        // --- meceo y giro lento: el viento lo empuja en la dirección en que avanza ---
        var dir = new Vector3(avance.x, 0f, avance.z);
        float inclinaAvance = dir.sqrMagnitude > 1e-8f ? meceo * 0.6f : 0f;
        dir = dir.sqrMagnitude > 1e-8f ? dir.normalized : Vector3.forward;
        var eje = Vector3.Cross(Vector3.up, dir);
        float bamboleo = meceo * Mathf.Sin(t * 0.9f + fase * 7f);
        float lateral = meceo * 0.6f * Mathf.Sin(t * 0.63f + fase * 4f);
        var inclinacion = Quaternion.AngleAxis(inclinaAvance + bamboleo, eje) * Quaternion.AngleAxis(lateral, dir);
        var yaw = Quaternion.Euler(0f, (t * giro + fase * 360f) % 360f, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, inclinacion * yaw * rotacionModelo, 2f * dt);
    }
}
