// Dirigible decorativo del Hub: rodea la isla a lo lejos, a gran altura, inclinándose un poco hacia dentro
// de la curva (como un avión al girar) y subiendo y bajando suave. Las puntas de sus aletas laterales llevan
// TrailRenderers que dejan estelas largas y finas en el cielo, como las de un avión.
// Solo se mueve en Play; el constructor (LookFrutiger.Dirigible) lo deja colocado en el editor.
using UnityEngine;

public class DirigibleVuelo : MonoBehaviour
{
    [Header("Recorrido")]
    public Vector3 centro;                  // centro de la órbita (solo XZ)
    public float radio = 145f;
    public float altura = 140f;
    public float velocidad = 5f;            // m/s
    public bool horario;
    public float fase;                      // 0..1: dónde empieza la vuelta

    [Header("Movimiento")]
    public float subeBaja = 4f;             // amplitud (m)
    public float periodoSubeBaja = 40f;     // segundos
    public float alabeo = 6f;               // grados de inclinación hacia dentro de la curva
    public Vector3 frenteLocal = Vector3.forward;   // hacia dónde apunta la nariz en el espacio del modelo

    [Header("Estelas")]
    public TrailRenderer[] estelas;

    float ang;
    Quaternion corregirModelo = Quaternion.identity;

    void Start()
    {
        ang = fase * Mathf.PI * 2f;
        // el modelo no necesariamente mira a +Z: esta rotación lleva su nariz al +Z local
        var f = frenteLocal.sqrMagnitude > 1e-6f ? frenteLocal.normalized : Vector3.forward;
        corregirModelo = Quaternion.Inverse(Quaternion.LookRotation(f, Vector3.up));
        Colocar(Time.time, 0f);
        if (estelas != null) foreach (var e in estelas) if (e != null) e.Clear();   // sin raya desde el origen
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        ang += (horario ? -1f : 1f) * velocidad / Mathf.Max(1f, radio) * Time.deltaTime;
        Colocar(Time.time, Time.deltaTime);
    }

    void Colocar(float t, float dt)
    {
        float w = (t / periodoSubeBaja + fase) * Mathf.PI * 2f;
        var p = new Vector3(centro.x + Mathf.Cos(ang) * radio, altura + subeBaja * Mathf.Sin(w), centro.z + Mathf.Sin(ang) * radio);
        float s = horario ? -1f : 1f;
        var tangente = new Vector3(-Mathf.Sin(ang) * s, 0f, Mathf.Cos(ang) * s);
        // cabeceo suave según si va subiendo o bajando
        float subida = subeBaja * Mathf.Cos(w) * Mathf.PI * 2f / periodoSubeBaja;
        float cabeceo = Mathf.Atan2(subida, velocidad) * Mathf.Rad2Deg;
        var rumbo = Quaternion.LookRotation(tangente, Vector3.up);
        var inclinacion = Quaternion.Euler(-cabeceo, 0f, s * alabeo + 1.2f * Mathf.Sin(t * 0.37f + fase * 5f));
        transform.position = p;
        var objetivo = rumbo * inclinacion * corregirModelo;
        transform.rotation = dt > 0f ? Quaternion.Slerp(transform.rotation, objetivo, 3f * dt) : objetivo;
    }
}
