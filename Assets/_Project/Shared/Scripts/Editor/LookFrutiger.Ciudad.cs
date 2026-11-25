// Hub final, parte 3: ciudad futurista de fondo, árboles de la isla, cielo Frutiger Aero y agua cristalina.
//   - Ciudad: los 11 diseños generados con Tripo, remodelados con geometría limpia
//     (Herramientas/GeneradorCiudad/remodelar_tripo.py): muros rectos, marcos blancos, vidrio cian con
//     ventanas alineadas. Se ordenan en 3 distritos al otro lado del mar, sin suelo ni árboles: salen
//     directo del agua (fondo que da profundidad). Torres altas al centro, bajas al frente.
//     El este queda libre (vista al mar del mirador).
//   - Árboles de la isla: árboles realistas (Herramientas/GeneradorPlantas/generar_arboles.py) en bosquecillos
//     sobre el pasto, sin tapar caminos, edificios ni el lobby.
//   - Cielo: componente CieloFrutiger (degradado azul + nubes fotográficas que se mueven).
//   - Agua: cristalina (clara, transparente, con cáusticas) y siempre por detrás de los chorros.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static partial class LookFrutiger
{
    const string CarpetaCiudad = "Assets/_Project/Shared/Models/CiudadFrutiger";
    const string CarpetaArboles = "Assets/_Project/Shared/Models/Plantas";
    const string RutaRamilletes = "Assets/_Project/Shared/Textures/T_RamilleteHojas.png";

    // nombre, alto real (m) y categoría: 2 = torre alta, 1 = torre media, 0 = edificio bajo
    static readonly (string nombre, float alto, int cat)[] Edificios =
    {
        ("Tripo_Torre_Aguja",       165f, 2),   // única: centro del distrito principal
        ("Tripo_Torre_Curva",       140f, 2),
        ("Tripo_Torre_Vela",        122f, 2),
        ("Tripo_Torres_Puente",     104f, 2),
        ("Tripo_Torre_TorcidaA",    102f, 1),
        ("Tripo_Torre_TorcidaB",     96f, 1),
        ("Tripo_Torre_Mirador",      90f, 1),
        ("Tripo_Torre_Espiral",      80f, 1),
        ("Tripo_Torre_Diagonal",     76f, 1),
        ("Tripo_Torre_Escalonada",   62f, 0),
        ("Tripo_Edificio_Terrazas",  22f, 0),
    };
    static readonly string[] ArbolesFrondosos = { "Arbol_Frondoso_1", "Arbol_Frondoso_2", "Arbol_Frondoso_3" };
    static readonly string[] Pinos = { };   // sin coníferas: no van con el estilo Frutiger

    // distritos: ángulo (grados, 0 = +X este, 90 = +Z norte), distancia del centro, semiancho, semifondo,
    // separación entre manzanas y escala de alturas
    static readonly (float ang, float dist, float a, float b, float paso, float escala)[] Distritos =
    {
        (100f, 255f, 150f, 80f, 38f, 1.0f),   // centro: el más grande y alto
        (195f, 240f,  88f, 52f, 40f, 0.85f),
        (275f, 236f,  84f, 50f, 40f, 0.85f),
    };

    [MenuItem("Glossy Horizons/Hub/Construir ciudad y árboles")]
    static void ConstruirCiudad()
    {
        var escena = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        GenerarTodo(escena);
        Debug.Log("Guarda la escena (Ctrl+S) para conservarlo.");
    }

    [MenuItem("Glossy Horizons/Hub/Plantar árboles en la isla")]
    static void MenuArbolesIsla()
    {
        var escena = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        int a = ArbolesIsla(escena);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(escena);
        Debug.Log($"Isla: {a} árboles plantados. Guarda la escena (Ctrl+S).");
    }

    /// <summary>Genera de nuevo todo lo de fondo del Hub: cielo, ciudad, árboles de la isla y auroras.
    /// Lo usa GenerarAlDarPlay (antes de cada Play) y el menú "Construir ciudad y árboles".</summary>
    internal static void GenerarTodo(UnityEngine.SceneManagement.Scene escena)
    {
        try
        {
            EditorUtility.DisplayProgressBar("Glossy Horizons", "Cielo...", 0.1f);
            Nubes(escena);
            EditorUtility.DisplayProgressBar("Glossy Horizons", "Ciudad...", 0.3f);
            int n = Ciudad(escena);
            EditorUtility.DisplayProgressBar("Glossy Horizons", "Árboles de la isla...", 0.6f);
            int a = ArbolesIsla(escena);
            EditorUtility.DisplayProgressBar("Glossy Horizons", "Auroras...", 0.8f);
            int au = Auroras(escena);
            EditorUtility.DisplayProgressBar("Glossy Horizons", "Globos aerostáticos...", 0.9f);
            int g = Globos(escena);
            EditorUtility.DisplayProgressBar("Glossy Horizons", "Dirigible...", 0.95f);
            int dg = Dirigible(escena);
            int im = PantallasKioscos(escena);
            EditorUtility.DisplayProgressBar("Glossy Horizons", "Habitantes de la isla...", 0.97f);
            int hb = Habitantes(escena);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(escena);
            Debug.Log($"Hub generado: cielo, {n} edificios, {a} árboles en la isla, {au} auroras, {g} globos, {dg} dirigible, {im} imágenes en los kioscos y {hb} habitantes.");
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    // ============================================================================ ciudad
    static int Ciudad(UnityEngine.SceneManagement.Scene escena)
    {
        foreach (var go in escena.GetRootGameObjects()) if (go.name == "Ciudad_Fondo") Object.DestroyImmediate(go);
        var mats = MaterialesCiudad();
        var modelos = new GameObject[Edificios.Length];
        var altoModelo = new float[Edificios.Length];
        for (int i = 0; i < Edificios.Length; i++)
        {
            modelos[i] = CargarModelo($"{CarpetaCiudad}/{Edificios[i].nombre}.fbx", mats, out altoModelo[i]);
            if (modelos[i] == null) return 0;
        }

        var raiz = new GameObject("Ciudad_Fondo");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(raiz, escena);
        var azar = new System.Random(2004);
        float Az(float a, float b) => a + (float)azar.NextDouble() * (b - a);
        const float suelo = -6.5f;   // sin suelo propio: los edificios salen directo del mar (que está en -6)
        int total = 0;

        for (int nd = 0; nd < Distritos.Length; nd++)
        {
            var d = Distritos[nd];
            var distrito = new GameObject($"Distrito_{nd + 1}");
            distrito.transform.SetParent(raiz.transform, false);
            float ang = d.ang * Mathf.Deg2Rad;
            var centro = new Vector3(Mathf.Cos(ang) * d.dist, suelo, Mathf.Sin(ang) * d.dist);
            var alHub = new Vector3(-centro.x, 0f, -centro.z).normalized;   // "frente" del distrito
            var lado = Vector3.Cross(Vector3.up, alHub);
            var giro = Quaternion.LookRotation(alHub);
            Vector3 Lugar(float x, float z) => centro + lado * x + alHub * z;

            // diseños por categoría, mezclados para que no se repitan vecinos iguales (la torre aguja es única)
            var porCat = new List<int>[3];
            for (int c = 0; c < 3; c++)
            {
                porCat[c] = new List<int>();
                for (int i = 1; i < Edificios.Length; i++) if (Edificios[i].cat == c) porCat[c].Add(i);
                for (int i = porCat[c].Count - 1; i > 0; i--) { int j = azar.Next(i + 1); (porCat[c][i], porCat[c][j]) = (porCat[c][j], porCat[c][i]); }
            }
            var turno = new int[3];

            int nI = Mathf.FloorToInt(d.a / d.paso), nJ = Mathf.FloorToInt(d.b / d.paso);
            for (int j = -nJ; j <= nJ; j++)
                for (int i = -nI; i <= nI; i++)
                {
                    float x = (i + (j % 2 != 0 ? 0.5f : 0f)) * d.paso;   // filas alternadas: no queda en "pared"
                    float z = j * d.paso;
                    float dn = Mathf.Sqrt((x / d.a) * (x / d.a) + (z / d.b) * (z / d.b));
                    if (dn > 1.08f) continue;
                    var p = Lugar(x + Az(-3f, 3f), z + Az(-3f, 3f));
                    bool centroDistrito = i == 0 && j == 0;
                    if (!centroDistrito && azar.NextDouble() < 0.08) continue;   // huecos: la silueta no queda en bloque
                    // torres altas al centro, bajas hacia el frente (el lado que mira al Hub) y los bordes
                    float puntaje = dn + (z > 0 ? 0.35f * z / d.b : 0f);
                    int cat = puntaje < 0.4f ? 2 : puntaje < 0.8f ? 1 : 0;
                    int idx = centroDistrito && nd == 0 ? 0 : porCat[cat][turno[cat]++ % porCat[cat].Count];
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(modelos[idx]);
                    go.transform.SetParent(distrito.transform, false);
                    float h = Edificios[idx].alto * d.escala * Az(0.88f, 1.06f);
                    go.transform.localScale = go.transform.localScale * (h / altoModelo[idx]);
                    go.transform.SetPositionAndRotation(p, giro * Quaternion.Euler(0f, 90f * azar.Next(4), 0f));
                    Preparar(go, false);
                    total++;
                }
        }
        return total;
    }

    // ============================================================================ auroras
    // Por distrito, dos arcoíris de luz azul amplios y sutiles que pasan POR ENCIMA de los edificios, de un
    // costado de la ciudad al otro: suben a los lados y quedan casi planos arriba. Cada uno con su ancho y
    // su altura, cruzándose (no van pegados). Muy translúcidos. Shader Glossy/AuroraArco.
    [MenuItem("Glossy Horizons/Hub/Auroras sobre la ciudad")]
    static void MenuAuroras()
    {
        var escena = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        int n = Auroras(escena);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(escena);
        Debug.Log($"{n} auroras creadas. Guarda la escena (Ctrl+S).");
    }

    static int Auroras(UnityEngine.SceneManagement.Scene escena)
    {
        foreach (var go in escena.GetRootGameObjects()) if (go.name == "Auroras") Object.DestroyImmediate(go);
        var mat = MaterialNuevo($"{CarpetaMateriales}/M_Aurora.mat", "Glossy/AuroraArco");
        if (mat == null) return 0;
        mat.SetFloat("_Intensidad", 1.1f);
        mat.SetFloat("_Ondulacion", 4.0f);
        mat.SetFloat("_Hebras", 3f);
        EditorUtility.SetDirty(mat);
        var raiz = new GameObject("Auroras");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(raiz, escena);
        var azar = new System.Random(31);
        int n = 0;
        foreach (var d in Distritos)
        {
            float ang = d.ang * Mathf.Deg2Rad;
            var afuera = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            var alHub = -afuera;
            var lado = Vector3.Cross(Vector3.up, alHub);
            var centro = afuera * d.dist + Vector3.up * -6f;
            float techo = 165f * d.escala;   // la torre más alta del distrito
            // (desde, hasta) en "lado" (m), profundidad hacia el Hub (m), altura sobre el mar (m)
            // UNA aurora por distrito: un arco amplio que pasa por encima de la ciudad
            var arcos = new (float desde, float hasta, float prof, float alto)[]
            {
                (-(d.a + 40f), d.a + 40f, 0f, techo + 25f),
            };
            foreach (var arco in arcos)
            {
                var desde = centro + lado * arco.desde + alHub * arco.prof;
                var hasta = centro + lado * arco.hasta + alHub * arco.prof;
                var go = new GameObject($"Aurora_{++n}");
                go.transform.SetParent(raiz.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = MallaAurora(desde, hasta, arco.alto, azar);
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }
        }
        return n;
    }

    // Arco de 'a' a 'b' (al nivel del mar) con forma de arcoíris amplio: sube a los lados y queda casi
    // plano arriba (seno elevado a 0.5). Una sola cinta suave; "a lo ancho" va hacia afuera del arco.
    static Mesh MallaAurora(Vector3 a, Vector3 b, float alto, System.Random azar)
    {
        // Una aurora = 3 hebras de luz de distinto ancho (como las primeras), que pasan juntas por encima
        // de la ciudad. Forma de arcoíris amplio (casi plano arriba).
        const int N = 160;
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var uv2 = new List<Vector2>();
        var nrm = new List<Vector3>(); var tri = new List<int>();
        var plano = Vector3.Cross(b - a, Vector3.up).normalized;
        (float ancho, float desfase, float escalaAlto)[] hebras = { (20f, 0f, 1f), (10f, 13f, 1.04f), (7f, -11f, 0.97f) };
        foreach (var h in hebras)
        {
            float fase = (float)azar.NextDouble() * 20f;
            int b0 = v.Count;
            Vector3 Punto(float t) => Vector3.Lerp(a, b, t) +
                Vector3.up * (alto * h.escalaAlto * Mathf.Sqrt(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Clamp01(t)))));
            for (int i = 0; i <= N; i++)
            {
                float t = i / (float)N;
                var tangente = (Punto(Mathf.Min(1f, t + 0.004f)) - Punto(Mathf.Max(0f, t - 0.004f))).normalized;
                var ancho = Vector3.Cross(plano, tangente).normalized;
                if (ancho.y < 0f) ancho = -ancho;
                var c = Punto(t) + ancho * h.desfase;
                v.Add(c - ancho * h.ancho * 0.5f); v.Add(c + ancho * h.ancho * 0.5f);
                uv.Add(new Vector2(t, 0f)); uv.Add(new Vector2(t, 1f));
                uv2.Add(new Vector2(fase, 0f)); uv2.Add(new Vector2(fase, 0f));
                nrm.Add(ancho); nrm.Add(ancho);
                if (i < N)
                {
                    int k = b0 + i * 2;
                    tri.AddRange(new[] { k, k + 1, k + 2, k + 1, k + 3, k + 2 });
                }
            }
        }
        var malla = new Mesh { name = "Aurora" };
        malla.SetVertices(v); malla.SetUVs(0, uv); malla.SetUVs(1, uv2); malla.SetNormals(nrm); malla.SetTriangles(tri, 0);
        malla.RecalculateBounds();
        var limites = malla.bounds;
        limites.Expand(20f);   // margen para la ondulación del shader
        malla.bounds = limites;
        return malla;
    }

    // ============================================================================ árboles de la isla
    // Bosquecillos sobre el pasto: un punto vale solo si al tirar un rayo desde arriba lo PRIMERO que toca es
    // el suelo de la isla (no caminos, plazas, edificios, agua ni la isla del jardín) y si la copa tiene espacio.
    static int ArbolesIsla(UnityEngine.SceneManagement.Scene escena)
    {
        foreach (var go in escena.GetRootGameObjects()) if (go.name == "Arboles_Isla") Object.DestroyImmediate(go);
        var suelo = GameObject.Find("Isla_Superficie");
        if (suelo == null || suelo.GetComponent<Collider>() == null)
        {
            Debug.LogWarning("No encuentro 'Isla_Superficie' con collider: crea la isla primero.");
            return 0;
        }
        var frondosos = CargarArboles(ArbolesFrondosos);
        var pinos = CargarArboles(Pinos);
        if (frondosos.Count == 0) { Debug.LogWarning("Faltan los árboles (corre generar_arboles.py)."); return 0; }

        // colliders temporales en todo lo que no sea pasto, para que los rayos "vean" caminos y edificios
        string[] ignorar = { "Pasto", "Flor", "Cesped", "Hierba", "Burbuja", "Nube", "Ciudad", "Arboles_Isla", "Cielo" };
        var temporales = new List<MeshCollider>();
        foreach (var mf in Object.FindObjectsByType<MeshFilter>())
        {
            if (mf.sharedMesh == null || mf.GetComponent<Collider>() != null) continue;
            string ruta = mf.transform.root.name + "/" + mf.name;
            bool saltar = false;
            foreach (var t in ignorar) if (ruta.Contains(t)) { saltar = true; break; }
            if (saltar) continue;
            var mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            temporales.Add(mc);
        }
        Physics.SyncTransforms();

        var raiz = new GameObject("Arboles_Isla");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(raiz, escena);
        var azar = new System.Random(77);
        float Az(float a, float b) => a + (float)azar.NextDouble() * (b - a);
        var limites = suelo.GetComponent<Renderer>().bounds;
        var puestos = new List<Vector3>();
        int plantados = 0;

        bool Valido(Vector3 xz, out Vector3 punto)
        {
            punto = default;
            if (new Vector2(xz.x, xz.z).magnitude < 28f) return false;            // el centro (lobby) libre
            if (!Physics.Raycast(new Vector3(xz.x, 300f, xz.z), Vector3.down, out var hit, 600f, ~0, QueryTriggerInteraction.Ignore)) return false;
            if (hit.collider.gameObject != suelo || hit.normal.y < 0.85f) return false;   // solo pasto plano
            foreach (var q in puestos) if ((q - hit.point).sqrMagnitude < 8.5f * 8.5f) return false;
            // espacio para el tronco y la copa (nada que no sea el suelo cerca)
            foreach (var c in Physics.OverlapCapsule(hit.point + Vector3.up * 1.2f, hit.point + Vector3.up * 9f, 3.2f, ~0, QueryTriggerInteraction.Ignore))
                if (c.gameObject != suelo) return false;
            punto = hit.point;
            return true;
        }

        try
        {
            // bosquecillos: centros en la periferia de la isla + árboles sueltos
            var centros = new List<Vector3>();
            for (int k = 0; k < 400 && centros.Count < 12; k++)
            {
                var c = new Vector3(Az(limites.min.x, limites.max.x), 0f, Az(limites.min.z, limites.max.z));
                if (Valido(c, out var pc) && centros.TrueForAll(o => (o - pc).sqrMagnitude > 35f * 35f)) centros.Add(pc);
            }
            foreach (var c in centros)
            {
                int quiere = azar.Next(4, 8);
                for (int k = 0, n = 0; k < 60 && n < quiere; k++)
                {
                    var xz = c + new Vector3(Az(-16f, 16f), 0f, Az(-16f, 16f));
                    if (!Valido(xz, out var p)) continue;
                    bool pino = pinos.Count > 0 && azar.NextDouble() < 0.3;
                    PonerArbol(raiz.transform, pino ? pinos : frondosos, p, azar, 1f, true);
                    puestos.Add(p); n++; plantados++;
                }
            }
            for (int k = 0, n = 0; k < 600 && n < 22; k++)   // sueltos, repartidos
            {
                var xz = new Vector3(Az(limites.min.x, limites.max.x), 0f, Az(limites.min.z, limites.max.z));
                if (!Valido(xz, out var p)) continue;
                PonerArbol(raiz.transform, frondosos, p, azar, 1f, true);
                puestos.Add(p); n++; plantados++;
            }
        }
        finally
        {
            foreach (var mc in temporales) if (mc != null) Object.DestroyImmediate(mc);
        }
        return plantados;
    }

    static List<(GameObject modelo, float alto)> CargarArboles(string[] nombres)
    {
        var mats = new Dictionary<string, Material>
        {
            ["HojasArbol"] = MaterialHojasArbol(),
            ["Tallo_Madera"] = MaterialTallo(RutaMatMadera, new Color(0.3f, 0.19f, 0.11f)),
        };
        var lista = new List<(GameObject, float)>();
        foreach (var n in nombres)
        {
            var m = CargarModelo($"{CarpetaArboles}/{n}.fbx", mats, out float alto);
            if (m != null) lista.Add((m, alto));
        }
        return lista;
    }

    static void PonerArbol(Transform padre, List<(GameObject modelo, float alto)> arboles, Vector3 p,
                           System.Random azar, float escala, bool sombras)
    {
        if (arboles.Count == 0) return;
        var (modelo, alto) = arboles[azar.Next(arboles.Count)];
        var t = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
        t.transform.SetParent(padre, false);
        float s = escala * (0.8f + (float)azar.NextDouble() * 0.45f);
        t.transform.localScale = t.transform.localScale * s;
        t.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, (float)azar.NextDouble() * 360f, 0f));
        // las tarjetas de hojas dan una sombra real enorme y dura: se apaga y se pone una sombra suave
        // y más pequeña al pie del árbol
        Preparar(t, false);
        if (sombras)
        {
            float copa = 0f;
            foreach (var r in t.GetComponentsInChildren<Renderer>()) copa = Mathf.Max(copa, Mathf.Max(r.bounds.extents.x, r.bounds.extents.z));
            SombraSuave(t.transform, p, copa * 0.75f);
        }
    }

    static Texture2D TexturaGenerada(string ruta, int ancho, int alto, System.Func<float, float, Color> f)
    {
        var existente = AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
        if (existente != null) return existente;
        var tex = new Texture2D(ancho, alto, TextureFormat.RGBA32, false);
        for (int y = 0; y < alto; y++)
            for (int x = 0; x < ancho; x++)
                tex.SetPixel(x, y, f((x + 0.5f) / ancho, (y + 0.5f) / alto));
        System.IO.File.WriteAllBytes(ruta, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(ruta);
        var imp = (TextureImporter)AssetImporter.GetAtPath(ruta);
        imp.alphaIsTransparency = true;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
    }

    // URP/Unlit transparente (mezcla alfa), opcionalmente visible por los dos lados
    static Material MaterialTransparente(string ruta, Texture2D tex, bool dosLados)
    {
        var m = MaterialNuevo(ruta, "Universal Render Pipeline/Unlit");
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_Cull", dosLados ? 0f : 2f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 10;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material matSombra;
    static void SombraSuave(Transform arbol, Vector3 p, float radio)
    {
        if (matSombra == null) matSombra = MaterialSombra();
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = "Sombra";
        Object.DestroyImmediate(q.GetComponent<Collider>());
        q.transform.SetParent(arbol, true);
        q.transform.position = p + Vector3.up * 0.06f;
        q.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        q.transform.localScale = Vector3.one;
        var s = q.transform.lossyScale;
        q.transform.localScale = new Vector3(radio * 2f / s.x, radio * 2f / s.y, 1f / s.z);
        var r = q.GetComponent<MeshRenderer>();
        r.sharedMaterial = matSombra;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    // círculo oscuro con borde difuso (URP/Unlit transparente)
    static Material MaterialSombra()
    {
        var tex = TexturaGenerada("Assets/_Project/Shared/Textures/T_SombraArbol.png", 128, 128, (u, v) =>
        {
            float d = new Vector2(u - 0.5f, v - 0.5f).magnitude * 2f;
            float a = Mathf.Clamp01(1f - d);
            return new Color(0.05f, 0.12f, 0.03f, a * a * (3f - 2f * a) * 0.45f);
        });
        return MaterialTransparente($"{CarpetaMateriales}/M_SombraArbol.mat", tex, false);
    }

    static Material MaterialHojasArbol()
    {
        var mat = MaterialNuevo($"{CarpetaMateriales}/M_HojasArbol.mat", "Glossy/FollajeNatural");
        if (mat == null) return null;
        var imp = AssetImporter.GetAtPath(RutaRamilletes) as TextureImporter;
        if (imp != null && (!imp.alphaIsTransparency || !imp.mipMapsPreserveCoverage))
        {
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = true;
            imp.mipMapsPreserveCoverage = true;     // las copas no se vuelven ralas a lo lejos
            imp.alphaTestReferenceValue = 0.45f;
            imp.maxTextureSize = 2048;
            imp.SaveAndReimport();
        }
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(RutaRamilletes));
        mat.SetColor("_Color", new Color(1.05f, 1.12f, 0.95f));
        mat.SetFloat("_Cutoff", 0.45f);
        mat.SetFloat("_Viento", 0.025f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void Preparar(GameObject go, bool sombras)
    {
        foreach (var rend in go.GetComponentsInChildren<Renderer>())
        {
            rend.shadowCastingMode = sombras ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = sombras;
        }
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
    }

    // FBX generado: sus materiales (por nombre) se reemplazan por los de URP; sin colisiones.
    // 'alto' = alto real del modelo (el pivote está en la base), por si el FBX viene en otra unidad.
    static GameObject CargarModelo(string ruta, Dictionary<string, Material> mats, out float alto)
    {
        alto = 1f;
        var imp = AssetImporter.GetAtPath(ruta) as ModelImporter;
        if (imp == null) { Debug.LogWarning("Falta el modelo " + ruta); return null; }
        bool cambiar = imp.materialImportMode != ModelImporterMaterialImportMode.ImportStandard || imp.importAnimation;
        var mapa = imp.GetExternalObjectMap();
        foreach (var kv in mats)
        {
            if (kv.Value == null) continue;
            var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key);
            if (!mapa.TryGetValue(id, out var actual) || actual != kv.Value) { imp.AddRemap(id, kv.Value); cambiar = true; }
        }
        if (cambiar)
        {
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
            imp.addCollider = false;
            imp.importAnimation = false;
            imp.importCameras = false;
            imp.importLights = false;
            imp.SaveAndReimport();
        }
        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        var mf = modelo != null ? modelo.GetComponentInChildren<MeshFilter>() : null;
        if (mf != null) alto = Mathf.Max(mf.sharedMesh.bounds.max.y * mf.transform.lossyScale.y, 1e-3f);
        return modelo;
    }

    // materiales URP de la ciudad (la clave = nombre del material en el FBX)
    static Dictionary<string, Material> MaterialesCiudad()
    {
        string tex = CarpetaCiudad + "/Texturas/";
        Texture2D Tex(string archivo, bool normal, bool srgb)
        {
            string ruta = tex + archivo;
            var imp = AssetImporter.GetAtPath(ruta) as TextureImporter;
            if (imp != null && (imp.sRGBTexture != srgb || normal != (imp.textureType == TextureImporterType.NormalMap) || imp.anisoLevel < 8))
            {
                imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                imp.sRGBTexture = srgb;
                imp.maxTextureSize = 1024;
                imp.anisoLevel = 8;            // ventanas nítidas aunque se vean de lado y lejos
                imp.mipmapEnabled = true;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.textureCompression = TextureImporterCompression.CompressedHQ;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
        }
        Material Vidrio(string nombre, Color tinte)
        {
            var m = MaterialNuevo($"{CarpetaMateriales}/{nombre}.mat", "Universal Render Pipeline/Lit");
            m.SetTexture("_BaseMap", Tex("T_Fachada_Color.png", false, true));
            m.SetColor("_BaseColor", tinte);
            m.SetTexture("_BumpMap", Tex("T_Fachada_Normal.png", true, false));
            m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_MetallicGlossMap", Tex("T_Fachada_MS.png", false, false));
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetFloat("_Smoothness", 1f);
            // brillo propio: el vidrio se ve cian luminoso aunque esté en sombra (como en las torres Frutiger)
            m.SetTexture("_EmissionMap", Tex("T_Fachada_Emision.png", false, true));
            m.SetColor("_EmissionColor", tinte * 0.75f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetFloat("_EnvironmentReflections", 1f);
            m.SetFloat("_SpecularHighlights", 1f);
            EditorUtility.SetDirty(m);
            return m;
        }
        var acento = MaterialLit("M_Edif_Acento", new Color(0.55f, 0.95f, 1f), 0.8f, 0f);
        acento.EnableKeyword("_EMISSION");
        acento.SetColor("_EmissionColor", new Color(0.25f, 0.85f, 1f) * 2.2f);
        acento.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(acento);
        return new Dictionary<string, Material>
        {
            ["Vidrio"] = Vidrio("M_Edif_Vidrio", Color.white),
            ["VidrioVerde"] = Vidrio("M_Edif_VidrioVerde", new Color(0.6f, 1f, 0.85f)),
            ["Marco"] = MaterialLit("M_Edif_Marco", new Color(0.93f, 0.97f, 1f), 0.8f, 0f),
            ["Acento"] = acento,
            ["Jardin"] = MaterialLit("M_Edif_Jardin", new Color(0.30f, 0.68f, 0.22f), 0.2f, 0f),
            ["Copa"] = MaterialLit("M_Arbol_Copa", new Color(0.30f, 0.70f, 0.20f), 0.25f, 0f),
            ["Tronco"] = MaterialLit("M_Arbol_Tronco", new Color(0.40f, 0.27f, 0.16f), 0.15f, 0f),
        };
    }

    // Aplica solo el cielo a la escena abierta, sin reconstruir el resto del Hub.
    // (En el Hub ya se agrega solo al abrir la escena; esto sirve para otras escenas o para reaplicarlo.)
    [MenuItem("Glossy Horizons/Hub/Aplicar cielo realista")]
    static void AplicarCieloRealista()
    {
        var escena = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        Nubes(escena);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(escena);
        foreach (var rp in Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None))
            if (rp.mode == UnityEngine.Rendering.ReflectionProbeMode.Realtime) rp.RenderProbe();
        Debug.Log("Cielo Frutiger aplicado (degradado azul + nubes en movimiento + niebla celeste). Guarda la escena.");
    }

    // Cielo realista: shader Glossy/CieloFrutiger (degradado azul, sol en la Directional Light y nubes
    // fotográficas reales que se mueven), puesto por el componente CieloFrutiger de la escena.
    static int Nubes(UnityEngine.SceneManagement.Scene escena)
    {
        foreach (var go in escena.GetRootGameObjects()) if (go.name == "Nubes_Cielo") Object.DestroyImmediate(go);
        var mat = CieloFrutigerEditor.AsegurarMaterial();
        if (mat == null) return 0;
        CieloFrutiger cielo = null;
        foreach (var raiz in escena.GetRootGameObjects())
            if ((cielo = raiz.GetComponentInChildren<CieloFrutiger>(true)) != null) break;
        if (cielo == null)
        {
            var go = new GameObject("CieloFrutiger");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, escena);
            Undo.RegisterCreatedObjectUndo(go, "Cielo");
            cielo = go.AddComponent<CieloFrutiger>();
        }
        cielo.cielo = mat;
        cielo.Aplicar();
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            if (cam.farClipPlane < 1500f) { Undo.RecordObject(cam, "Cielo"); cam.farClipPlane = 1500f; }
        return 1;
    }

    // agua clara y transparente, dibujada antes que los chorros (para que nunca los tape)
    static void AguaCristalina()
    {
        var agua = AssetDatabase.LoadAssetAtPath<Material>($"{CarpetaMateriales}/M_AguaAnimada.mat");
        if (agua != null)
        {
            agua.SetColor("_ColorProfundo", new Color(0.18f, 0.66f, 0.82f));
            agua.SetColor("_ColorClaro", new Color(0.72f, 0.98f, 1f));
            agua.SetFloat("_Opacidad", 0.38f);
            agua.SetFloat("_Reflejo", 0.4f);
            agua.SetFloat("_Fuerza", 0.25f);
            agua.renderQueue = 2980;
            EditorUtility.SetDirty(agua);
        }
        var chorro = AssetDatabase.LoadAssetAtPath<Material>($"{CarpetaMateriales}/M_ChorroAgua.mat");
        if (chorro != null) { chorro.renderQueue = 3010; EditorUtility.SetDirty(chorro); }
    }

    static Material MaterialLit(string nombre, Color color, float suavidad, float metal)
    {
        var mat = MaterialNuevo($"{CarpetaMateriales}/{nombre}.mat", "Universal Render Pipeline/Lit");
        if (mat == null) return null;
        mat.SetColor("_BaseColor", color); mat.SetFloat("_Smoothness", suavidad); mat.SetFloat("_Metallic", metal);
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
