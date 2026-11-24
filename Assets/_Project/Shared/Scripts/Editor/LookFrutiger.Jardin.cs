// Jardín Frutiger alrededor del lobby (mejora real de la escena, no un filtro).
// Menú: Glossy Horizons > Hub > Jardín Frutiger > Crear      (se puede volver a usar: rehace el jardín)
//                                              > Quitar
// Qué construye (todo dentro del objeto "Jardin_Frutiger"):
//   - Un camino de piedras que da la vuelta al domo, ondulado, sobre una franja de grava clara.
//   - A los lados: canteros de flores (rosa, amarilla, blanca y lila), helechos y rocas.
//   - Más afuera: arbustos redondos y con flores, y arbustos grandes de fondo.
//   - Adentro del lobby: la planta de cada maceta se cambia por una planta de interior o un helecho.
// Los modelos son propios (Shared/Models/Plantas, hechos en Blender). Solo se planta sobre el pasto
// y nunca encima de objetos que ya existen (juegos, piscina, bancas, arbustos de la escena).
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class LookFrutiger
{
    const string NombreJardin = "Jardin_Frutiger";
    const float RadioCamino = 37f;          // el domo mide 27 m de radio

    static List<Bounds> suelos, obstaculos;
    // superficies planas que no son pasto (caminos, plazas, bordes de piscina): se revisa su forma real
    static List<(Bounds b, List<Vector2> tri)> planos;
    static List<(Vector3 p, float r)> ocupados;

    [MenuItem("Glossy Horizons/Hub/Jardín Frutiger/Crear (alrededor del lobby)")]
    static void CrearJardin()
    {
        QuitarJardin(false);
        GameObject M(string n) => AssetDatabase.LoadAssetAtPath<GameObject>($"{CarpetaPlantas}/{n}.fbx");
        var redondos = new[] { M("Arbusto_Redondo_1"), M("Arbusto_Redondo_2"), M("Arbusto_Flores_1"), M("Arbusto_Flores_2") };
        var grandes = new[] { M("Arbusto_Grande_1"), M("Arbusto_Grande_Flores_1") };
        var helechos = new[] { M("Helecho_1"), M("Helecho_2") };
        var interior = new[] { M("Planta_Interior_1"), M("Planta_Interior_2") };
        var flores = new[] { M("Flores_Rosa"), M("Flores_Amarillo"), M("Flores_Blanco"), M("Flores_Lila") };
        var rocas = new[] { M("Roca_1"), M("Roca_2"), M("Roca_3") };
        var piedras = new[] { M("Piedra_Camino_1"), M("Piedra_Camino_2"), M("Piedra_Camino_3") };
        if (redondos[0] == null || piedras[0] == null)
        {
            EditorUtility.DisplayDialog("Jardín Frutiger", "No encontré los modelos en " + CarpetaPlantas, "OK");
            return;
        }

        var mats = new Dictionary<string, Material>
        {
            { "hoja", MaterialHojas() },
            { "flor", MaterialConTextura("M_FloresJardin", "Glossy/FollajeNatural", "T_AtlasFlores.png", 0f, true) },
            { "madera", MaterialTallo(RutaMatMadera, new Color(0.3f, 0.19f, 0.11f)) },
            { "roca", MaterialConTextura("M_RocaJardin", "Universal Render Pipeline/Lit", "T_Roca.png", 0.25f, false) },
            { "verde", MaterialTallo(RutaMatTalloVerde, new Color(0.22f, 0.5f, 0.15f)) },
        };
        var grava = MaterialConTextura("M_GravaCamino", "Universal Render Pipeline/Lit", "T_Grava.png", 0.15f, false);

        var raiz = new GameObject(NombreJardin);
        var resp = raiz.AddComponent<LookFrutigerRespaldo>();
        var gCamino = Hijo(raiz, "Camino_Piedras"); var gPlantas = Hijo(raiz, "Plantas");
        var gRocas = Hijo(raiz, "Rocas"); var gMacetas = Hijo(raiz, "Macetas_Lobby");
        PrepararTerreno(raiz.transform);
        var azar = new System.Random(2026);
        float Az(float a, float b) => a + (float)azar.NextDouble() * (b - a);
        T Uno<T>(T[] arr) => arr[azar.Next(arr.Length)];

        // ---------- camino (curva cerrada alrededor del domo) ----------
        const int Muestras = 900;
        var centro = new Vector3[Muestras];
        for (int i = 0; i < Muestras; i++)
        {
            float t = i / (float)Muestras * Mathf.PI * 2f;
            float r = RadioCamino + 2.5f * Mathf.Sin(3f * t) + 1.2f * Mathf.Sin(7f * t + 1f);
            centro[i] = new Vector3(Mathf.Cos(t) * r, 0f, Mathf.Sin(t) * r);
        }
        var validos = new bool[Muestras]; var alturas = new float[Muestras];
        for (int i = 0; i < Muestras; i++) validos[i] = Libre(centro[i], 0.8f, out alturas[i]);
        ConstruirGrava(gCamino.transform, centro, validos, alturas, 2.2f, grava);

        int nPiedras = 0, nPlantas = 0, nRocas = 0;
        float recorrido = 0f, sigPiedra = 0f, sigFlor = 0f, sigArbusto = 0f, sigGrande = 4f, sigRoca = 3f;
        for (int i = 0; i < Muestras; i++)
        {
            var a = centro[i]; var b = centro[(i + 1) % Muestras];
            recorrido += Vector3.Distance(a, b);
            var dir = (b - a).normalized;
            var afuera = new Vector3(a.x, 0f, a.z).normalized;              // lado opuesto al domo
            if (!validos[i]) continue;

            // piedras del camino
            if (recorrido >= sigPiedra)
            {
                sigPiedra = recorrido + Az(0.75f, 0.95f);
                var p = a + Vector3.Cross(Vector3.up, dir) * Az(-0.35f, 0.35f);
                p.y = alturas[i] + 0.01f;
                Colocar(gCamino.transform, Uno(piedras), p, Az(0.9f, 1.25f), Az(0, 360), mats, false);
                nPiedras++;
            }
            // canteros de flores y helechos a los dos lados
            if (recorrido >= sigFlor)
            {
                sigFlor = recorrido + Az(1.8f, 2.6f);
                foreach (float lado in new[] { -1f, 1f })
                {
                    var modelo = azar.NextDouble() < 0.3 ? Uno(helechos) : Uno(flores);
                    int grupo = modelo.name.StartsWith("Flores") ? 3 : 1;
                    var baseP = a + afuera * lado * Az(1.6f, 2.3f);
                    for (int k = 0; k < grupo; k++)
                    {
                        var p = baseP + new Vector3(Az(-0.5f, 0.5f), 0f, Az(-0.5f, 0.5f));
                        if (Plantar(gPlantas.transform, modelo, p, 0.45f, grupo > 1 ? Az(1.2f, 1.6f) : Az(1.2f, 1.6f), mats, false)) nPlantas++;
                    }
                }
            }
            // arbustos medianos (afuera y algunos hacia el domo)
            if (recorrido >= sigArbusto)
            {
                sigArbusto = recorrido + Az(4.5f, 6.5f);
                if (Plantar(gPlantas.transform, Uno(redondos), a + afuera * Az(3.4f, 4.8f), 1.1f, Az(1.4f, 2.0f), mats, false)) nPlantas++;
                if (azar.NextDouble() < 0.5 &&
                    Plantar(gPlantas.transform, Uno(redondos), a - afuera * Az(3.2f, 4f), 1.0f, Az(1.2f, 1.6f), mats, false)) nPlantas++;
            }
            // arbustos grandes de fondo
            if (recorrido >= sigGrande)
            {
                sigGrande = recorrido + Az(8f, 11f);
                if (Plantar(gPlantas.transform, Uno(grandes), a + afuera * Az(6.5f, 9f), 1.8f, Az(1.4f, 1.9f), mats, false)) nPlantas++;
            }
            // rocas al borde del camino (a veces en pareja)
            if (recorrido >= sigRoca)
            {
                sigRoca = recorrido + Az(6f, 9f);
                float lado = azar.NextDouble() < 0.5 ? -1f : 1f;
                var p = a + afuera * lado * Az(1.4f, 1.8f);
                if (Plantar(gRocas.transform, Uno(rocas), p, 0.8f, Az(0.7f, 1.4f), mats, true)) nRocas++;
                if (azar.NextDouble() < 0.4 &&
                    Plantar(gRocas.transform, Uno(rocas), p + dir * Az(1f, 1.4f), 0.6f, Az(0.5f, 0.8f), mats, true)) nRocas++;
            }
        }

        // ---------- plantas reales en las macetas del lobby ----------
        int nMacetas = 0;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (!(t.name.StartsWith("Maceta_") && t.name.EndsWith("_Arbusto"))) continue;
            var r = t.GetComponent<Renderer>();
            if (r == null || !r.enabled) continue;                  // ya tiene una planta real
            var b = r.bounds;
            Undo.RecordObject(r, "Jardín Frutiger");
            r.enabled = false;
            resp.ocultos.Add(r);
            bool helecho = nMacetas % 3 == 2;
            var modelo = helecho ? helechos[nMacetas % 2] : interior[nMacetas % 2];
            Colocar(gMacetas.transform, modelo, new Vector3(b.center.x, b.min.y + 0.05f, b.center.z),
                    helecho ? 1.7f : 1.9f, nMacetas * 47f, mats, false);
            nMacetas++;
        }

        Undo.RegisterCreatedObjectUndo(raiz, "Jardín Frutiger");
        EditorSceneManager.MarkSceneDirty(raiz.scene);
        Selection.activeObject = raiz;
        Debug.Log($"Jardín Frutiger: {nPiedras} piedras de camino, {nPlantas} plantas, {nRocas} rocas, " +
                  $"{nMacetas} macetas del lobby con plantas reales. Guarda la escena (Ctrl+S) para conservarlo.");
        if (nPiedras == 0)
            Debug.LogWarning("No se pudo poner el camino: no encontré pasto libre alrededor del domo.");
    }

    [MenuItem("Glossy Horizons/Hub/Jardín Frutiger/Quitar")]
    static void QuitarJardinMenu() => QuitarJardin(true);

    static void QuitarJardin(bool avisar)
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t == null || t.parent != null || t.name != NombreJardin) continue;
            var resp = t.GetComponent<LookFrutigerRespaldo>();
            if (resp != null)
                foreach (var r in resp.ocultos)
                    if (r != null) { Undo.RecordObject(r, "Jardín Frutiger"); r.enabled = true; }
            Undo.DestroyObjectImmediate(t.gameObject);
        }
        if (avisar)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Jardín Frutiger quitado.");
        }
    }

    // ---------- dónde se puede plantar ----------
    static void PrepararTerreno(Transform jardin)
    {
        Physics.SyncTransforms();
        suelos = new List<Bounds>(); obstaculos = new List<Bounds>(); ocupados = new List<(Vector3, float)>();
        planos = new List<(Bounds, List<Vector2>)>();
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (!r.enabled || r.transform.IsChildOf(jardin) || r is ParticleSystemRenderer) continue;
            string n = r.name.ToLower();
            foreach (var m in r.sharedMaterials) if (m != null) n += " " + m.name.ToLower();
            var b = r.bounds;
            if (n.Contains("pasto") || n.Contains("cesped") || n.Contains("grass") || n.Contains("terreno") || n.Contains("superficie"))
                suelos.Add(b);
            else if (b.size.y > 0.25f && b.size.x < 30f && b.size.z < 30f && b.max.y > 0.1f && b.min.y < 2f)
                obstaculos.Add(b);
            else if (b.size.y <= 0.6f && b.max.y < 1.5f && b.max.y > -1f && b.size.x < 120f && b.size.z < 120f
                     && !n.Contains("agua") && !n.Contains("mar") && !n.Contains("water") && !n.Contains("sea")
                     && r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null)
            {
                // camino o plaza plana: se guardan sus triángulos vistos desde arriba
                var malla = mf.sharedMesh;
                if (!malla.isReadable) { obstaculos.Add(b); continue; }    // sin acceso a la malla: se evita su caja
                var vs = malla.vertices; var ts = malla.triangles; var m = r.transform.localToWorldMatrix;
                var tri = new List<Vector2>(ts.Length);
                foreach (int i in ts) { var w = m.MultiplyPoint3x4(vs[i]); tri.Add(new Vector2(w.x, w.z)); }
                planos.Add((b, tri));
            }
        }
    }

    static bool Libre(Vector3 p, float margen, out float y)
    {
        y = 0f;
        if (new Vector2(p.x, p.z).magnitude < 29.5f) return false;              // nada dentro ni pegado al domo
        bool enSuelo = suelos.Count == 0;
        foreach (var s in suelos)
            if (p.x >= s.min.x && p.x <= s.max.x && p.z >= s.min.z && p.z <= s.max.z) { enSuelo = true; break; }
        if (!enSuelo) return false;
        foreach (var o in obstaculos)
            if (p.x > o.min.x - margen && p.x < o.max.x + margen && p.z > o.min.z - margen && p.z < o.max.z + margen) return false;
        float mg = Mathf.Max(margen, 0.6f);
        foreach (var (b, tri) in planos)
        {
            if (p.x < b.min.x - mg || p.x > b.max.x + mg || p.z < b.min.z - mg || p.z > b.max.z + mg) continue;
            foreach (var q in new[] { new Vector2(p.x, p.z), new Vector2(p.x + mg, p.z), new Vector2(p.x - mg, p.z),
                                      new Vector2(p.x, p.z + mg), new Vector2(p.x, p.z - mg) })
                for (int i = 0; i + 2 < tri.Count; i += 3)
                    if (DentroTriangulo(q, tri[i], tri[i + 1], tri[i + 2])) return false;   // encima de un camino
        }
        if (Physics.Raycast(new Vector3(p.x, 40f, p.z), Vector3.down, out var hit, 80f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.point.y > 1.5f) return false;                               // cayó encima de algo alto
            y = hit.point.y;
        }
        return true;
    }

    static bool DentroTriangulo(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
        float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
        float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }

    // Planta un modelo si el lugar está libre y no choca con otra planta ya puesta
    static bool Plantar(Transform padre, GameObject modelo, Vector3 p, float radio, float escala,
                        Dictionary<string, Material> mats, bool colision)
    {
        if (modelo == null || !Libre(p, radio * 0.5f, out float y)) return false;
        foreach (var (q, rq) in ocupados)
            if ((new Vector2(q.x - p.x, q.z - p.z)).magnitude < (radio + rq) * 0.7f) return false;
        ocupados.Add((p, radio));
        p.y = y;
        Colocar(padre, modelo, p, escala, (p.x * 37f + p.z * 91f) % 360f, mats, colision);
        return true;
    }

    static GameObject Colocar(Transform padre, GameObject modelo, Vector3 pos, float escala, float giro,
                              Dictionary<string, Material> mats, bool colision)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
        go.transform.SetParent(padre, false);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, giro, 0f);
        go.transform.localScale = Vector3.one * escala;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var lista = r.sharedMaterials;
            for (int i = 0; i < lista.Length; i++)
            {
                string n = lista[i] != null ? lista[i].name.ToLower() : "";
                Material m = mats["verde"];
                foreach (var par in mats) if (n.Contains(par.Key)) { m = par.Value; break; }
                lista[i] = m;
            }
            r.sharedMaterials = lista;
            if (colision && r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null)
            {
                var c = r.gameObject.AddComponent<MeshCollider>();
                c.sharedMesh = mf.sharedMesh; c.convex = true;
            }
        }
        return go;
    }

    static GameObject Hijo(GameObject padre, string nombre)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre.transform, false);
        return go;
    }

    // Franja de grava bajo las piedras (una tira que sigue el camino, sin pedazos donde no hay pasto)
    static void ConstruirGrava(Transform padre, Vector3[] centro, bool[] validos, float[] alturas, float ancho, Material mat)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        float largo = 0f; int n = centro.Length;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            var dir = (centro[j] - centro[i]).normalized;
            var lado = Vector3.Cross(Vector3.up, dir) * (ancho * 0.5f);
            var c = centro[i] + Vector3.up * (alturas[i] + 0.025f);
            v.Add(c - lado); v.Add(c + lado);
            uv.Add(new Vector2(0f, largo / 1.5f)); uv.Add(new Vector2(ancho / 1.5f, largo / 1.5f));
            largo += Vector3.Distance(centro[i], centro[j]);
        }
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            if (!validos[i] || !validos[j]) continue;
            int a = i * 2, b = j * 2;
            tri.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
        }
        var malla = new Mesh { name = "GravaCamino", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        malla.SetVertices(v); malla.SetUVs(0, uv); malla.SetTriangles(tri, 0);
        malla.RecalculateNormals(); malla.RecalculateBounds();
        var go = new GameObject("Grava");
        go.transform.SetParent(padre, false);
        go.AddComponent<MeshFilter>().sharedMesh = malla;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static Material MaterialConTextura(string nombre, string shader, string textura, float suavidad, bool transparencia)
    {
        string rutaTex = "Assets/_Project/Shared/Textures/" + textura;
        var imp = AssetImporter.GetAtPath(rutaTex) as TextureImporter;
        if (imp != null && transparencia && !imp.alphaIsTransparency) { imp.alphaIsTransparency = true; imp.SaveAndReimport(); }
        var mat = MaterialNuevo($"{CarpetaMateriales}/{nombre}.mat", shader);
        if (mat == null) return null;
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(rutaTex));
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", suavidad);
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
