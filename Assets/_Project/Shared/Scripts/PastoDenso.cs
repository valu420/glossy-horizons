// Pasto tupido para los terrenos que no tienen pasto propio (zonas del Hub).
// Reparte matas de pasto sobre una malla "suelo" (en coordenadas del mundo) y las dibuja con
// GPU instancing, pero SOLO los bloques cercanos a la cámara (así puede haber mucho pasto sin
// ponerse lento). La herramienta del Hub crea la malla y lo configura.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
public class PastoDenso : MonoBehaviour
{
    public Mesh suelo;                         // dónde crece (coordenadas del mundo)
    public Mesh mata;                          // una mata de briznas
    public Material material;
    public float densidad = 9f;                // matas por m²
    public Vector2 escala = new Vector2(0.85f, 1.25f);
    public float distancia = 75f;              // más lejos de esto no se dibuja
    public float tamBloque = 8f;
    public int semilla = 7;

    class Bloque { public Bounds b; public Matrix4x4[] m; }
    readonly List<Bloque> bloques = new List<Bloque>();
    const int MaxPorLlamada = 1023;

    void OnEnable() => Generar();
    void OnValidate() { if (isActiveAndEnabled) Generar(); }

    public void Generar()
    {
        bloques.Clear();
        if (suelo == null || mata == null || !suelo.isReadable) return;
        var v = suelo.vertices; var t = suelo.triangles;
        var azar = new System.Random(semilla);
        var mapa = new Dictionary<Vector2Int, List<Matrix4x4>>();
        for (int i = 0; i < t.Length; i += 3)
        {
            Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
            float area = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
            float esperado = area * densidad;
            int n = (int)esperado + (azar.NextDouble() < esperado - (int)esperado ? 1 : 0);
            for (int k = 0; k < n; k++)
            {
                float u = (float)azar.NextDouble(), w = (float)azar.NextDouble();
                if (u + w > 1f) { u = 1f - u; w = 1f - w; }
                var p = a + (b - a) * u + (c - a) * w;
                var clave = new Vector2Int(Mathf.FloorToInt(p.x / tamBloque), Mathf.FloorToInt(p.z / tamBloque));
                if (!mapa.TryGetValue(clave, out var lista)) mapa[clave] = lista = new List<Matrix4x4>();
                float s = Mathf.Lerp(escala.x, escala.y, (float)azar.NextDouble());
                lista.Add(Matrix4x4.TRS(p, Quaternion.Euler(0f, (float)azar.NextDouble() * 360f, 0f), Vector3.one * s));
            }
        }
        foreach (var par in mapa)
        {
            var b = new Bounds(new Vector3((par.Key.x + 0.5f) * tamBloque, par.Value[0].m13, (par.Key.y + 0.5f) * tamBloque),
                               new Vector3(tamBloque + 2f, 3f, tamBloque + 2f));
            bloques.Add(new Bloque { b = b, m = par.Value.ToArray() });
        }
    }

    void Update()
    {
        if (material == null || mata == null) return;
        var cam = Camera.main;
        Vector3 desde = cam != null ? cam.transform.position : Vector3.zero;
        float d2 = distancia * distancia;
        var rp = new RenderParams(material) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true, layer = gameObject.layer };
        foreach (var bl in bloques)
        {
            if (cam != null && (bl.b.center - desde).sqrMagnitude > d2) continue;
            rp.worldBounds = bl.b;
            for (int s = 0; s < bl.m.Length; s += MaxPorLlamada)
                Graphics.RenderMeshInstanced(rp, mata, 0, bl.m, Mathf.Min(MaxPorLlamada, bl.m.Length - s), s);
        }
    }
}
