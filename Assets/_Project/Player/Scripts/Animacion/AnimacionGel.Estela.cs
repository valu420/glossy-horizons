// HU-06: estela musical del emote Bailar.
// Una cinta con un pentagrama de luz, clave de sol y notas azules brillantes (textura
// T_EstelaMusical, estilo Frutiger Aero) se enrolla en espiral alrededor del personaje,
// desde la cabeza hasta los pies. La tira se desliza por la cinta (shader Glossy/EstelaMusical),
// así las notas viajan alrededor del gel mientras la espiral gira y ondula.
// Aparece creciendo desde la cabeza y se desvanece al terminar el baile.
using UnityEngine;

public partial class AnimacionGel
{
    [Header("Estela musical (emote Bailar)")]
    public Material materialEstela;              // M_EstelaMusical (lo asigna la herramienta del prefab)
    public float radioEstela = 0.5f;
    public float vueltasEstela = 2.2f;
    public float anchoEstela = 0.26f;            // alto de la cinta (pentagrama + notas)
    public float largoTira = 2.1f;               // metros que ocupa una tira completa de la textura

    const int PuntosEstela = 120;

    GameObject estela;
    Mesh mallaEstela;
    Vector3[] vertsEstela;
    Vector2[] uvsEstela;
    Color[] coloresEstela;
    float giroEstela, crecerEstela, pesoEstela;

    void CrearEstela()
    {
        if (materialEstela == null) return;
        estela = new GameObject("EstelaMusical");
        estela.transform.SetParent(transform, false);
        var mr = estela.AddComponent<MeshRenderer>();
        mr.sharedMaterial = materialEstela;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mallaEstela = new Mesh { name = "EstelaMusical" };
        mallaEstela.MarkDynamic();
        vertsEstela = new Vector3[PuntosEstela * 2];
        uvsEstela = new Vector2[PuntosEstela * 2];
        coloresEstela = new Color[PuntosEstela * 2];
        var tri = new int[(PuntosEstela - 1) * 6];
        for (int i = 0; i < PuntosEstela - 1; i++)
        {
            int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
            tri[i * 6 + 0] = a; tri[i * 6 + 1] = c; tri[i * 6 + 2] = b;
            tri[i * 6 + 3] = b; tri[i * 6 + 4] = c; tri[i * 6 + 5] = d;
        }
        mallaEstela.vertices = vertsEstela;
        mallaEstela.uv = uvsEstela;
        mallaEstela.colors = coloresEstela;
        mallaEstela.triangles = tri;
        estela.AddComponent<MeshFilter>().sharedMesh = mallaEstela;
        estela.SetActive(false);
    }

    // Punto de la espiral en el espacio del jugador: u = 0 junto a la cabeza, 1 a los pies
    Vector3 PuntoEstela(float u, float t)
    {
        float ang = u * vueltasEstela * Mathf.PI * 2f + giroEstela;
        float r = radioEstela + 0.05f * Mathf.Sin(u * Mathf.PI * 2f + t);
        float y = 1.6f - u * 1.3f + 0.05f * Mathf.Sin(u * 9f + t * 3f);
        return new Vector3(Mathf.Cos(ang) * r, y, Mathf.Sin(ang) * r);
    }

    // Se llama cada frame desde ActualizarEmote
    void ActualizarEstela(float dt)
    {
        if (estela == null) return;
        bool bailando = emote == 1;
        bool primeraPersona = camara != null && camara.PrimeraPersona;
        pesoEstela = Suavizar(pesoEstela, bailando && !primeraPersona ? 1f : 0f, bailando ? 6f : 4f, dt);
        if (bailando) crecerEstela = Mathf.Min(1f, crecerEstela + dt / 1.2f);
        bool ver = pesoEstela > 0.01f;
        if (estela.activeSelf != ver) estela.SetActive(ver);
        if (!ver) { crecerEstela = 0f; return; }

        float t = Time.time;
        giroEstela += dt * 0.7f;                    // gira despacio

        // Cinta: en cada punto, un borde arriba y otro abajo (la cinta queda "de pie", mirando hacia afuera)
        float distancia = 0f;
        Vector3 anterior = PuntoEstela(0f, t);
        for (int i = 0; i < PuntosEstela; i++)
        {
            float f = i / (PuntosEstela - 1f);
            float u = f * crecerEstela;
            Vector3 p = PuntoEstela(u, t);
            distancia += Vector3.Distance(p, anterior);
            anterior = p;
            vertsEstela[i * 2] = p + Vector3.up * (anchoEstela * 0.5f);
            vertsEstela[i * 2 + 1] = p - Vector3.up * (anchoEstela * 0.5f);
            float x = distancia / largoTira;
            uvsEstela[i * 2] = new Vector2(x, 1f);
            uvsEstela[i * 2 + 1] = new Vector2(x, 0f);
            // se desvanece en las puntas (y al aparecer / terminar)
            float puntas = Mathf.Clamp01(f / 0.12f) * Mathf.Clamp01((1f - f) / 0.15f);
            var c = new Color(1f, 1f, 1f, puntas * pesoEstela);
            coloresEstela[i * 2] = c; coloresEstela[i * 2 + 1] = c;
        }
        mallaEstela.vertices = vertsEstela;
        mallaEstela.uv = uvsEstela;
        mallaEstela.colors = coloresEstela;
        mallaEstela.RecalculateBounds();
    }
}
