// Hub final, parte 2: cambia las piezas que todavía eran esferas lisas por modelos reales
// y convierte la fuente del lobby en una fuente de agua de verdad.
//   - "Roca" (esferas)      -> rocas de Blender con grietas y relieve
//   - "Monticulo" (cúpulas) -> grupo de arbustos grandes del mismo tamaño
//   - "Flor" (bolitas)      -> matas de flores del color más parecido
//   - "Arbusto"/"Hojas" que quedaran sin cambiar -> arbustos reales
//   - Fuente: chorros de partículas en arco (8 + uno al centro), salpicaduras al caer, neblina,
//     y el agua de la fuente y de la piscina con olas animadas.
// Lo llama LookFrutiger.AplicarTodo (al abrir el Hub). Los originales solo se ocultan (y se anotan).
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static partial class LookFrutiger
{
    const string NombreFuente = "Fuente_Animada";

    // ---------- esferas -> modelos reales ----------
    static int ReemplazarEsferas()
    {
        GameObject M(string n) => AssetDatabase.LoadAssetAtPath<GameObject>($"{CarpetaPlantas}/{n}.fbx");
        var rocas = new[] { M("Roca_1"), M("Roca_2"), M("Roca_3") };
        var grandes = new[] { M("Arbusto_Grande_1"), M("Arbusto_Grande_Flores_1") };
        var arbustos = new[] { M("Arbusto_Redondo_1"), M("Arbusto_Redondo_2"), M("Arbusto_Flores_1"), M("Arbusto_Flores_2") };
        if (rocas[0] == null) return 0;
        var roca = MaterialConTextura("M_RocaJardin", "Universal Render Pipeline/Lit", "T_Roca.png", 0.2f, false);
        roca.SetColor("_BaseColor", new Color(0.78f, 0.75f, 0.69f));
        var mats = new Dictionary<string, Material>
        {
            { "hoja", MaterialHojas() },
            { "flor", MaterialConTextura("M_FloresJardin", "Glossy/FollajeNatural", "T_AtlasFlores.png", 0f, true) },
            { "madera", MaterialTallo(RutaMatMadera, new Color(0.3f, 0.19f, 0.11f)) },
            { "roca", roca },
            { "verde", MaterialTallo(RutaMatTalloVerde, new Color(0.22f, 0.5f, 0.15f)) },
        };
        var resp = Respaldo(true);
        var raiz = GameObject.Find(NombreArbustos);
        if (raiz == null) { raiz = new GameObject(NombreArbustos); Undo.RegisterCreatedObjectUndo(raiz, "Look Frutiger"); }

        int hechos = 0;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (!r.enabled || r.transform.IsChildOf(raiz.transform)) continue;
            if (!r.TryGetComponent<MeshFilter>(out var mf) || mf.sharedMesh == null || !mf.sharedMesh.name.Contains("Sphere")) continue;
            string n = r.name.ToLower();
            string mat = r.sharedMaterial != null ? r.sharedMaterial.name.ToLower() : "";
            var b = r.bounds;
            int h = Mathf.Abs(Mathf.RoundToInt(b.center.x * 73f + b.center.z * 131f));
            bool hecho = true;
            if (n.StartsWith("roca") || mat.Contains("roca"))
                Ajustar(raiz.transform, rocas[h % 3], b, h, mats, 1.05f);
            else if (n.StartsWith("monticulo"))
            {
                // cúpula verde -> grupo de arbustos grandes que la cubre
                int cuantos = Mathf.Clamp(Mathf.RoundToInt(b.size.x * b.size.z / 6f), 2, 7);
                var azar = new System.Random(h);
                for (int k = 0; k < cuantos; k++)
                {
                    var c = new Vector3(b.center.x + (float)(azar.NextDouble() - 0.5) * b.size.x * 0.7f, b.min.y,
                                        b.center.z + (float)(azar.NextDouble() - 0.5) * b.size.z * 0.7f);
                    float tam = Mathf.Max(b.size.y * 1.1f, 1.2f);
                    Ajustar(raiz.transform, grandes[(h + k) % 2], new Bounds(c + Vector3.up * tam / 2, new Vector3(tam * 1.2f, tam, tam * 1.2f)), h + k * 31, mats, 1f);
                }
            }
            else if (n == "flor" && b.size.magnitude < 2f)
            {
                string color = mat.Contains("rosa") ? "Flores_Rosa" : mat.Contains("amarillo") || mat.Contains("naranja") ? "Flores_Amarillo"
                             : mat.Contains("violeta") || mat.Contains("azul") ? "Flores_Lila" : "Flores_Blanco";
                var c = new Bounds(new Vector3(b.center.x, b.min.y, b.center.z) + Vector3.up * 0.2f, new Vector3(0.55f, 0.4f, 0.55f));
                Ajustar(raiz.transform, M(color), c, h, mats, 1f);
            }
            else if ((n.StartsWith("arbusto") || n == "hojas") && b.size.magnitude < 14f)
                Ajustar(raiz.transform, arbustos[h % arbustos.Length], b, h, mats, 1.1f);
            else hecho = false;
            if (!hecho) continue;
            Undo.RecordObject(r, "Look Frutiger");
            r.enabled = false;                                 // el collider original se queda (no se atraviesa)
            if (!resp.ocultos.Contains(r)) { Undo.RecordObject(resp, "Look Frutiger"); resp.ocultos.Add(r); }
            hechos++;
        }
        return hechos;
    }

    // Pone un modelo ocupando el lugar y tamaño de 'b' (ancho y alto)
    static void Ajustar(Transform padre, GameObject modelo, Bounds b, int semilla, Dictionary<string, Material> mats, float holgura)
    {
        if (modelo == null) return;
        var go = Colocar(padre, modelo, new Vector3(b.center.x, b.min.y, b.center.z), 1f, semilla % 360, mats, false);
        var rm = go.GetComponentInChildren<Renderer>();
        if (rm == null) return;
        var t = rm.bounds.size;
        float esc = Mathf.Max(Mathf.Max(b.size.x, b.size.z) / Mathf.Max(0.01f, Mathf.Max(t.x, t.z)), b.size.y / Mathf.Max(0.01f, t.y) * 0.85f);
        go.transform.localScale = Vector3.one * Mathf.Clamp(esc * holgura, 0.05f, 20f);
    }

    // ---------- agua animada (fuente y piscina) ----------
    static void AguaAnimada()
    {
        var agua = MaterialNuevo($"{CarpetaMateriales}/M_AguaAnimada.mat", "Glossy/AguaAnimada");
        if (agua == null) return;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            var lista = r.sharedMaterials; bool cambia = false;
            for (int i = 0; i < lista.Length; i++)
            {
                string n = lista[i] != null ? lista[i].name.ToLower() : "";
                if (n == "m_agua" || n.Contains("piscina_agua")) { lista[i] = agua; cambia = true; }
            }
            if (!cambia) continue;
            var resp = Respaldo(true);
            if (!resp.YaGuardado(r)) { Undo.RecordObject(resp, "Agua"); resp.materiales.Add(new LookFrutigerRespaldo.MaterialesOriginales { renderer = r, materiales = r.sharedMaterials }); }
            Undo.RecordObject(r, "Agua");
            r.sharedMaterials = lista;
        }
    }

    // ---------- fuente con chorros de agua de verdad ----------
    static bool FuenteAnimada(UnityEngine.SceneManagement.Scene escena)
    {
        foreach (var go in escena.GetRootGameObjects()) if (go.name == NombreFuente) Object.DestroyImmediate(go);
        Renderer chorros = null, aguaF = null, pedestal = null, orbe = null;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (r.name.StartsWith("Fuente_Chorros")) chorros = r;
            else if (r.name.StartsWith("Fuente_Agua")) aguaF = r;
            else if (r.name.StartsWith("Fuente_Pedestal")) pedestal = r;
            else if (r.name.StartsWith("Fuente_Orbe")) orbe = r;
        }
        if (aguaF == null || (pedestal == null && orbe == null)) return false;

        // los arcos del modelo se quedan (su forma es la de la fuente) y el agua corre por ellos
        if (chorros != null)
        {
            var matChorro = MaterialNuevo($"{CarpetaMateriales}/M_ChorroAgua.mat", "Glossy/ChorroAgua");
            var resp = Respaldo(true);
            if (!resp.YaGuardado(chorros)) { Undo.RecordObject(resp, "Fuente"); resp.materiales.Add(new LookFrutigerRespaldo.MaterialesOriginales { renderer = chorros, materiales = chorros.sharedMaterials }); }
            if (resp.ocultos.Contains(chorros)) resp.ocultos.Remove(chorros);
            Undo.RecordObject(chorros, "Fuente");
            chorros.enabled = true;
            var lista = chorros.sharedMaterials;
            for (int i = 0; i < lista.Length; i++) lista[i] = matChorro;
            chorros.sharedMaterials = lista;
        }
        var mat = MaterialNuevo($"{CarpetaMateriales}/M_ParticulaAgua.mat", "Glossy/ParticulaAgua");
        var raiz = new GameObject(NombreFuente);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(raiz, escena);

        var ba = aguaF.bounds;
        float nivel = ba.max.y, radio = Mathf.Min(ba.extents.x, ba.extents.z);
        var bp = (pedestal != null ? pedestal : orbe).bounds;
        var origen = new Vector3(ba.center.x, bp.max.y, ba.center.z);
        float g = Mathf.Abs(Physics.gravity.y);

        // 8 arcos que caen dentro de la pileta (tiro parabólico: velocidad justa para caer a 'd')
        float d = radio * 0.78f, h = origen.y - nivel, ang = 58f * Mathf.Deg2Rad;
        float v = Mathf.Sqrt(g * d * d / (2f * Mathf.Cos(ang) * Mathf.Cos(ang) * (d * Mathf.Tan(ang) + h)));
        float vuelo = d / (v * Mathf.Cos(ang));
        // gotitas que saltan sueltas de cada arco (los arcos ya son agua animada)
        for (int k = 0; k < 8; k++)
        {
            float a = k / 8f * Mathf.PI * 2f;
            var dir = new Vector3(Mathf.Cos(a) * Mathf.Cos(ang), Mathf.Sin(ang), Mathf.Sin(a) * Mathf.Cos(ang));
            Chorro(raiz.transform, "Gotas_" + (k + 1), origen, dir, v, vuelo, 25f, 0.05f, mat, true, 4f);
        }
        // neblina suave sobre el agua
        Neblina(raiz.transform, new Vector3(ba.center.x, nivel + 0.1f, ba.center.z), radio * 0.8f, mat);
        return true;
    }

    static ParticleSystem Chorro(Transform padre, string nombre, Vector3 pos, Vector3 dir, float vel, float vida,
                                 float cantidad, float tam, Material mat, bool salpica, float apertura = 1.2f)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir));
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 1f; main.loop = true; main.prewarm = true; main.playOnAwake = true;
        main.startLifetime = vida;
        main.startSpeed = new ParticleSystem.MinMaxCurve(vel * 0.98f, vel * 1.02f);
        main.startSize = new ParticleSystem.MinMaxCurve(tam * 0.8f, tam * 1.2f);
        main.gravityModifier = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 1500;
        main.startColor = new Color(0.82f, 0.94f, 1f, 0.85f);
        var em = ps.emission; em.rateOverTime = cantidad;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = apertura; sh.radius = 0.02f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.8f, 0.95f, 1f), 1f) },
                   new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.85f, 0.85f), new GradientAlphaKey(0f, 1f) });
        col.color = gr;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;           // gotas estiradas: se ve como un chorro continuo
        r.velocityScale = 0.06f; r.lengthScale = 1.2f;
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (salpica)
        {
            var sgo = new GameObject("Salpicadura");
            sgo.transform.SetParent(go.transform, false);
            var sp = sgo.AddComponent<ParticleSystem>();
            sp.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var sm = sp.main;
            sm.loop = false; sm.playOnAwake = false; sm.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            sm.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f); sm.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            sm.gravityModifier = 1f; sm.simulationSpace = ParticleSystemSimulationSpace.World; sm.maxParticles = 800;
            sm.startColor = new Color(0.9f, 0.97f, 1f, 0.8f);
            var se = sp.emission; se.rateOverTime = 0f; se.SetBursts(new[] { new ParticleSystem.Burst(0f, 2) });
            var ss = sp.shape; ss.shapeType = ParticleSystemShapeType.Hemisphere; ss.radius = 0.02f;
            ss.rotation = new Vector3(-90f, 0f, 0f);
            var sr = sgo.GetComponent<ParticleSystemRenderer>(); sr.sharedMaterial = mat;
            var sub = ps.subEmitters; sub.enabled = true;
            sub.AddSubEmitter(sp, ParticleSystemSubEmitterType.Death, ParticleSystemSubEmitterProperties.InheritNothing);
        }
        return ps;
    }

    // ---------- caminos de adoquines ----------
    static int CaminosDeAdoquines()
    {
        foreach (var ruta in new[] { "Assets/_Project/Shared/Textures/T_Adoquines_N.png" })
        {
            var imp = AssetImporter.GetAtPath(ruta) as TextureImporter;
            if (imp != null && imp.textureType != TextureImporterType.NormalMap) { imp.textureType = TextureImporterType.NormalMap; imp.SaveAndReimport(); }
        }
        var mat = MaterialNuevo($"{CarpetaMateriales}/M_Adoquines.mat", "Glossy/BaldosasMundo");
        if (mat == null) return 0;
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Shared/Textures/T_Adoquines.png"));
        mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Shared/Textures/T_Adoquines_N.png"));
        mat.SetFloat("_Tamano", 3.5f); mat.SetFloat("_Smoothness", 0.55f); mat.SetFloat("_BrilloJunta", 0.1f); mat.SetFloat("_BumpScale", 1.2f);
        EditorUtility.SetDirty(mat);
        int n = 0;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            var lista = r.sharedMaterials; bool cambia = false;
            for (int i = 0; i < lista.Length; i++)
            {
                string m = lista[i] != null ? lista[i].name.ToLower() : "";
                if ((m.Contains("camino") || m.Contains("sendero")) && !m.Contains("grava") && lista[i] != mat) { lista[i] = mat; cambia = true; }
            }
            if (!cambia) continue;
            var resp = Respaldo(true);
            if (!resp.YaGuardado(r)) { Undo.RecordObject(resp, "Caminos"); resp.materiales.Add(new LookFrutigerRespaldo.MaterialesOriginales { renderer = r, materiales = r.sharedMaterials }); }
            Undo.RecordObject(r, "Caminos");
            r.sharedMaterials = lista; n++;
        }
        return n;
    }

    // ---------- pasto tupido en todo el terreno que no tenía pasto propio ----------
    static int CubrirTerreno(UnityEngine.SceneManagement.Scene escena)
    {
        foreach (var go in escena.GetRootGameObjects()) if (go.name == "Pasto_Denso") Object.DestroyImmediate(go);
        var mata = AssetDatabase.LoadAssetAtPath<Mesh>(RutaMallaPasto);
        var mat = AssetDatabase.LoadAssetAtPath<Material>($"{CarpetaMateriales}/M_PastoNatural.mat");
        if (mata == null || mat == null) return 0;
        // todo el pasto lo dibuja PastoDenso (que respeta la forma real de los caminos):
        // el pasto anterior (GrassField, que solo esquiva cajas) se apaga
        foreach (var gf in Object.FindObjectsByType<GlossyHorizons.Core.GrassField>(FindObjectsSortMode.None))
        { Undo.RecordObject(gf, "Pasto"); gf.enabled = false; EditorUtility.SetDirty(gf); }
        // Se recorre toda la isla en una cuadrícula y con un rayo hacia abajo se mira qué hay:
        // si es césped (cualquier material verde o de pasto/terreno/isla) y no es camino, crece pasto.
        PrepararTerreno(new GameObject("tmp").transform);
        // colliders temporales para los suelos y caminos visibles que no tienen uno (si no, los rayos
        // los atraviesan y el pasto queda escondido debajo, en el terreno de la isla)
        var temporales = new List<MeshCollider>();
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (!r.enabled || r.GetComponent<Collider>() != null || r.bounds.size.y > 0.6f) continue;   // solo suelos y caminos planos (no plantas)
            if (!r.TryGetComponent<MeshFilter>(out var mfc) || mfc.sharedMesh == null) continue;
            var mc = r.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mfc.sharedMesh;
            temporales.Add(mc);
        }
        Physics.SyncTransforms();
        var v = new List<Vector3>(); var tri = new List<int>();
        const float Celda = 1.0f, Alcance = 135f;
        for (float x = -Alcance; x < Alcance; x += Celda)
            for (float z = -Alcance; z < Alcance; z += Celda)
            {
                var c = new Vector3(x + Celda / 2, 60f, z + Celda / 2);
                if (new Vector2(c.x, c.z).magnitude < RadioDomo()) continue;     // dentro del domo de vidrio, no
                if (!CespedEn(c.x, c.z, out float y)) continue;
                // la celda entera (y medio metro más) debe ser césped: así no entra a caminos ni pisos
                const float M = 0.15f; float hc = Celda / 2 + M; bool libre = true;
                foreach (var o in new[] { new Vector2(hc, hc), new Vector2(-hc, hc), new Vector2(hc, -hc), new Vector2(-hc, -hc) })
                    if (!CespedEn(c.x + o.x, c.z + o.y, out _)) { libre = false; break; }
                if (!libre) continue;
                int k = v.Count;
                v.Add(new Vector3(x, y + 0.01f, z)); v.Add(new Vector3(x + Celda, y + 0.01f, z));
                v.Add(new Vector3(x + Celda, y + 0.01f, z + Celda)); v.Add(new Vector3(x, y + 0.01f, z + Celda));
                tri.AddRange(new[] { k, k + 2, k + 1, k, k + 3, k + 2 });
            }
        foreach (var mc in temporales) if (mc != null) Object.DestroyImmediate(mc);
        Physics.SyncTransforms();
        foreach (var go in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)) if (go != null && go.name == "tmp" && go.parent == null) Object.DestroyImmediate(go.gameObject);
        if (v.Count == 0) return 0;
        var malla = new Mesh { name = "SueloPasto_Hub", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        malla.SetVertices(v); malla.SetTriangles(tri, 0); malla.RecalculateBounds();
        const string ruta = "Assets/_Project/Shared/Models/SueloPasto_Hub.asset";
        AssetDatabase.DeleteAsset(ruta);
        AssetDatabase.CreateAsset(malla, ruta);
        var raiz = new GameObject("Pasto_Denso");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(raiz, escena);
        var pd = raiz.AddComponent<PastoDenso>();
        pd.suelo = malla; pd.mata = mata; pd.material = mat; pd.densidad = 11f;   // igual de tupido que antes
        pd.Generar();
        return tri.Count / 6;
    }

    // Radio real del vidrio del domo (+ medio metro); si no se encuentra, 17 m
    static float radioDomo = -1f;
    static float RadioDomo()
    {
        if (radioDomo > 0f) return radioDomo;
        radioDomo = 17f;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            if (r.name.StartsWith("Cupula_Vidrio")) { radioDomo = Mathf.Max(r.bounds.extents.x, r.bounds.extents.z) + 0.5f; break; }
        return radioDomo;
    }

    // Lo primero VISIBLE que hay en (x, z) mirando desde arriba, ¿es césped? (ignora colliders de
    // objetos ocultos, como los arbustos originales que se cambiaron por modelos reales)
    static bool CespedEn(float x, float z, out float y)
    {
        y = 0f;
        var hits = Physics.RaycastAll(new Vector3(x, 60f, z), Vector3.down, 120f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (h1, h2) => h1.distance.CompareTo(h2.distance));
        foreach (var h in hits)
        {
            var r = h.collider.GetComponent<Renderer>();
            if (r == null) r = h.collider.GetComponentInParent<Renderer>();
            if (r == null || !r.enabled) continue;
            if (h.normal.y < 0.7f || !EsCesped(h.collider)) return false;
            y = h.point.y; return true;
        }
        return false;
    }

    // ¿el suelo que tocó el rayo es césped?
    static bool EsCesped(Collider col)
    {
        var r = col.GetComponent<Renderer>();
        if (r == null) r = col.GetComponentInParent<Renderer>();
        if (r == null) return false;
        string n = r.name.ToLower();
        if (n.Contains("camino") || n.Contains("piso") || n.Contains("agua") || n.Contains("roca") || n.Contains("arena")) return false;
        foreach (var m in r.sharedMaterials)
        {
            if (m == null) continue;
            string mn = m.name.ToLower();
            if (mn.Contains("camino") || mn.Contains("adoquin") || mn.Contains("agua") || mn.Contains("arena") || mn.Contains("roca")) return false;
            if (mn.Contains("pasto") || mn.Contains("cesped") || mn.Contains("terreno") || mn.Contains("superficie") || mn.Contains("grass")) return true;
            if (m.HasProperty("_BaseColor")) { var cc = m.GetColor("_BaseColor"); if (cc.g > cc.r * 1.2f && cc.g > cc.b * 1.1f) return true; }
        }
        return n.Contains("pasto") || n.Contains("terreno") || n.Contains("superficie") || n.Contains("cesped");
    }

    // para pasto: fuera del domo y nunca sobre caminos/plazas planas. Alrededor de arbustos, rocas
    // y flores SÍ crece (así se ve natural y sin huecos).
    static bool LibreParaPasto(Vector3 p)
    {
        if (new Vector2(p.x, p.z).magnitude < RadioDomo()) return false;
        foreach (var (b, tri) in planos)
        {
            if (p.x < b.min.x - 1f || p.x > b.max.x + 1f || p.z < b.min.z - 1f || p.z > b.max.z + 1f) continue;
            if (b.max.y < p.y - 0.3f) continue;                       // está por debajo (el propio terreno)
            var q = new Vector2(p.x, p.z);
            for (int i = 0; i + 2 < tri.Count; i += 3) if (DentroTriangulo(q, tri[i], tri[i + 1], tri[i + 2])) return false;
        }
        return true;
    }

    // ---------- burbujas de vidrio flotando ----------
    static void Burbujas(UnityEngine.SceneManagement.Scene escena)
    {
        foreach (var go in escena.GetRootGameObjects()) if (go.name == "Burbujas_Ambiente") Object.DestroyImmediate(go);
        var mat = MaterialNuevo($"{CarpetaMateriales}/M_BurbujaAmbiente.mat", "Glossy/VidrioFresnel");
        if (mat == null) return;
        mat.SetColor("_Tinte", new Color(0.8f, 0.97f, 1f)); mat.SetFloat("_Opacidad", 0.04f);
        mat.SetFloat("_FuerzaBorde", 1.1f); mat.SetFloat("_PotenciaBorde", 2.6f); mat.SetFloat("_Reflejo", 0.6f); mat.SetFloat("_Destello", 1.2f);
        EditorUtility.SetDirty(mat);
        var raiz = new GameObject("Burbujas_Ambiente");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(raiz, escena);
        raiz.AddComponent<BurbujasAmbiente>().material = mat;
    }

    static void Neblina(Transform padre, Vector3 pos, float radio, Material mat)
    {
        var go = new GameObject("Neblina");
        go.transform.SetParent(padre, false);
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(-90f, 0f, 0f));
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true; main.prewarm = true; main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.35f); main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 200;
        main.startColor = new Color(0.9f, 0.97f, 1f, 0.12f);
        var em = ps.emission; em.rateOverTime = 25f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = radio;
        var col = ps.colorOverLifetime; col.enabled = true;
        var gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                   new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
        col.color = gr;
        var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}
