// HU-02: animación del halo que marca el color elegido.
// Gira despacio (se notan los dos destellos del aro) y "respira" con un pulso suave.
// Usa tiempo sin escala para animarse aunque el juego esté en pausa.
using UnityEngine;

public class HaloAnimado : MonoBehaviour
{
    public float velocidadGiro = 40f;      // grados por segundo
    public float pulso = 0.05f;            // cuánto crece y se encoge
    public float velocidadPulso = 2.5f;

    Vector3 escalaBase;

    void Awake() { escalaBase = transform.localScale; }

    void OnEnable() { transform.localScale = escalaBase * 0.6f; }   // aparece con un pequeño "pop"

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        transform.Rotate(0f, 0f, -velocidadGiro * dt);
        float objetivo = 1f + pulso * Mathf.Sin(Time.unscaledTime * velocidadPulso);
        transform.localScale = Vector3.Lerp(transform.localScale, escalaBase * objetivo, 1f - Mathf.Exp(-12f * dt));
    }
}
