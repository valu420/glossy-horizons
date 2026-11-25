// Puertas eléctricas del lobby: cuando el jugador se acerca, las dos hojas de cada puerta se deslizan
// hacia los lados; cuando se aleja, se cierran solas. Funciona con cualquier par de objetos llamados
// "Puerta_<Nombre>_Izq" y "Puerta_<Nombre>_Der" (por ejemplo Puerta_Este_Izq / Puerta_Este_Der).
// Va en un objeto cualquiera de la escena (la herramienta del Hub lo agrega sola).
using System.Collections.Generic;
using UnityEngine;

public class PuertasAutomaticas : MonoBehaviour
{
    public float distancia = 4.5f;          // a cuántos metros se abren
    public float velocidad = 3f;            // qué tan rápido se deslizan
    [Range(0.5f, 1f)] public float apertura = 0.95f;   // cuánto se corre cada hoja (en anchos de hoja)

    class Hoja { public Transform t; public Vector3 cerrada, abierta; }
    class Puerta { public Vector3 centro; public List<Hoja> hojas = new List<Hoja>(); public float abierto; }

    readonly List<Puerta> puertas = new List<Puerta>();
    Transform jugador;

    void Start()
    {
        var pares = new Dictionary<string, List<Transform>>();
        foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            string n = t.name;
            if (!n.StartsWith("Puerta_") || !(n.EndsWith("_Izq") || n.EndsWith("_Der"))) continue;
            string clave = n.Substring(0, n.Length - 4);
            if (!pares.TryGetValue(clave, out var lista)) pares[clave] = lista = new List<Transform>();
            lista.Add(t);
        }
        foreach (var par in pares.Values)
        {
            var p = new Puerta();
            foreach (var t in par) p.centro += Centro(t);
            p.centro /= par.Count;
            foreach (var t in par)
            {
                // cada hoja se corre alejándose del centro de la puerta, tanto como su propio ancho
                Vector3 dir = Centro(t) - p.centro; dir.y = 0f;
                if (dir.sqrMagnitude < 1e-4f) dir = t.right;
                dir.Normalize();
                var r = t.GetComponent<Renderer>();
                float ancho = r != null ? Mathf.Abs(Vector3.Dot(r.bounds.size, new Vector3(Mathf.Abs(dir.x), 0f, Mathf.Abs(dir.z)))) : 1f;
                p.hojas.Add(new Hoja { t = t, cerrada = t.position, abierta = t.position + dir * ancho * apertura });
            }
            puertas.Add(p);
        }
    }

    static Vector3 Centro(Transform t)
    {
        var r = t.GetComponent<Renderer>();
        return r != null ? r.bounds.center : t.position;
    }

    void Update()
    {
        if (jugador == null)
        {
            var c = FindAnyObjectByType<ControladorJugador>();
            jugador = c != null ? c.transform : (Camera.main != null ? Camera.main.transform : null);
            if (jugador == null) return;
        }
        foreach (var p in puertas)
        {
            Vector3 d = jugador.position - p.centro; d.y = 0f;
            float objetivo = d.magnitude < distancia ? 1f : 0f;
            p.abierto = Mathf.MoveTowards(p.abierto, objetivo, Time.deltaTime * velocidad * 0.5f);
            float suave = p.abierto * p.abierto * (3f - 2f * p.abierto);   // arranca y frena suave
            foreach (var h in p.hojas) h.t.position = Vector3.Lerp(h.cerrada, h.abierta, suave);
        }
    }
}
