// Look Frutiger: mejoras OPCIONALES de la escena (no cambian ningún modelo ni archivo de otros:
// cambian qué material usa cada objeto, ocultan o agregan objetos, y se pueden deshacer con "Quitar").
// Menú: Glossy Horizons > Look Frutiger > Mejoras opcionales >
//   Piso de baldosas      -> los pisos ("piso", "azulejo") usan baldosas Frutiger blanco perla y aqua
//   Vidrio limpio         -> la cúpula y los vidrios casi invisibles, con brillo solo en los bordes
//   Plantas reales y jardín -> los arbustos y las plantas de las macetas se cambian por modelos de plantas
//                            hechos en Blender (arbustos redondos y con flores, helechos, plantas de interior)
//   Luz de día            -> cielo azul de día y sol claro (en vez del atardecer)
//   Aplicar todas
// Todo lo que cambia queda anotado en el objeto "LookFrutiger_Respaldo" de la escena.
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class LookFrutiger
{
    const string CarpetaMateriales = "Assets/_Project/Shared/Materials";
    const string RutaMatBaldosas = CarpetaMateriales + "/M_BaldosasFrutiger.mat";
    const string RutaMatVidrio = CarpetaMateriales + "/M_VidrioFrutiger.mat";
    const string RutaTexBaldosas = "Assets/_Project/Shared/Textures/T_BaldosasFrutiger.png";
    const string RutaTexBaldosasN = "Assets/_Project/Shared/Textures/T_BaldosasFrutiger_N.png";
    const string NombreRespaldo = "LookFrutiger_Respaldo";
    const string NombreArbustos = "LookFrutiger_Arbustos";
    const string Submenu = "Glossy Horizons/Look Frutiger/Mejoras opcionales/";

    // ---------- Menús ----------
    [MenuItem(Submenu + "Piso de baldosas")]
    static void MenuPiso() { Avisar("pisos con baldosas", PisoDeBaldosas()); }

    [MenuItem(Submenu + "Vidrio limpio (cúpula y ventanas)")]
    static void MenuVidrio() { Avisar("vidrios cambiados", VidrioLimpio()); }

    [MenuItem(Submenu + "Plantas reales y jardín")]
    static void MenuArbustos() { Avisar("plantas cambiadas por modelos reales", ArbustosNaturales()); }

    [MenuItem(Submenu + "Luz de día")]
    static void MenuLuz() { Avisar("luz de día", LuzDeDia() ? 1 : 0); }

    [MenuItem(Submenu + "Aplicar todas")]
    static void MenuTodas()
    {
        int a = PisoDeBaldosas(), b = VidrioLimpio(), c = ArbustosNaturales();
        bool d = LuzDeDia();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"Mejoras Frutiger: {a} pisos con baldosas, {b} vidrios limpios, {c} plantas reales" +
                  (d ? ", luz de día." : ".") + " Se deshacen con Look Frutiger > Quitar de la escena abierta.");
    }

    static void Avisar(string que, int cuantos)
    {
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"Look Frutiger: {cuantos} {que}. Se deshace con Look Frutiger > Quitar de la escena abierta.");
    }

    // ---------- Respaldo ----------
    static LookFrutigerRespaldo Respaldo(bool crear)
    {
        var r = Object.FindAnyObjectByType<LookFrutigerRespaldo>(FindObjectsInactive.Include);
        if (r != null || !crear) return r;
        var go = new GameObject(NombreRespaldo);
        Undo.RegisterCreatedObjectUndo(go, "Look Frutiger");
        return go.AddComponent<LookFrutigerRespaldo>();
    }

    static void Guardar(LookFrutigerRespaldo resp, Renderer r)
    {
        if (resp.YaGuardado(r)) return;
        Undo.RecordObject(resp, "Look Frutiger");
        resp.materiales.Add(new LookFrutigerRespaldo.MaterialesOriginales { renderer = r, materiales = r.sharedMaterials });
    }

    static void QuitarMejoras()
    {
        var resp = Respaldo(false);
        if (resp != null)
        {
            foreach (var m in resp.materiales)
                if (m.renderer != null) { Undo.RecordObject(m.renderer, "Look Frutiger"); m.renderer.sharedMaterials = m.materiales; }
            foreach (var r in resp.ocultos)
                if (r != null) { Undo.RecordObject(r, "Look Frutiger"); r.enabled = true; }
            if (resp.guardoLuz)
            {
                RenderSettings.skybox = resp.cielo;
                if (resp.sol != null)
                {
                    Undo.RecordObject(resp.sol, "Look Frutiger"); Undo.RecordObject(resp.sol.transform, "Look Frutiger");
                    resp.sol.color = resp.colorSol; resp.sol.intensity = resp.intensidadSol; resp.sol.transform.rotation = resp.rotacionSol;
                }
                DynamicGI.UpdateEnvironment();
            }
            Undo.DestroyObjectImmediate(resp.gameObject);
        }
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t != null && t.parent == null && t.name == NombreArbustos) Undo.DestroyObjectImmediate(t.gameObject);
    }

    // ---------- Materiales nuevos (se crean una vez en Shared/Materials) ----------
    static Material MaterialNuevo(string ruta, string shader)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat != null) return mat;
        var sh = Shader.Find(shader);
        if (sh == null) { Debug.LogWarning("No se encontró el shader " + shader); return null; }
        if (!AssetDatabase.IsValidFolder(CarpetaMateriales)) AssetDatabase.CreateFolder("Assets/_Project/Shared", "Materials");
        mat = new Material(sh);
        AssetDatabase.CreateAsset(mat, ruta);
        return mat;
    }

    static Material MaterialBaldosas()
    {
        var imp = AssetImporter.GetAtPath(RutaTexBaldosasN) as TextureImporter;
        if (imp != null && imp.textureType != TextureImporterType.NormalMap) { imp.textureType = TextureImporterType.NormalMap; imp.SaveAndReimport(); }
        var mat = MaterialNuevo(RutaMatBaldosas, "Glossy/BaldosasMundo");
        if (mat == null) return null;
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(RutaTexBaldosas));
        mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(RutaTexBaldosasN));
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ---------- Piso de baldosas ----------
    static int PisoDeBaldosas()
    {
        var mat = MaterialBaldosas();
        if (mat == null) return 0;
        var resp = Respaldo(true);
        int cambiados = 0;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            var b = r.bounds;
            if (b.size.y > 0.35f * Mathf.Max(b.size.x, b.size.z)) continue;      // solo superficies planas (pisos)
            var mats = r.sharedMaterials;
            bool cambia = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                string n = mats[i].name.ToLower();
                if ((n.Contains("piso") || n.Contains("azulejo") || n.Contains("baldosa")) && !n.Contains("linea"))
                { if (!cambia) Guardar(resp, r); mats[i] = mat; cambia = true; }
            }
            if (!cambia) continue;
            Undo.RecordObject(r, "Look Frutiger");
            r.sharedMaterials = mats;
            cambiados++;
        }
        return cambiados;
    }

    // ---------- Vidrio limpio ----------
    static int VidrioLimpio()
    {
        var mat = MaterialNuevo(RutaMatVidrio, "Glossy/VidrioFresnel");
        if (mat == null) return 0;
        mat.SetFloat("_Destello", 0.35f);        // destello del sol pequeño (antes encandilaba)
        mat.SetFloat("_FuerzaBorde", 0.55f);
        mat.SetFloat("_Reflejo", 0.25f);
        EditorUtility.SetDirty(mat);
        var resp = Respaldo(true);
        int cambiados = 0;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            var mats = r.sharedMaterials;
            bool cambia = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                string n = mats[i].name.ToLower();
                if (n.Contains("vidrio") || n.Contains("cristal") || n.Contains("glass"))
                { if (!cambia) Guardar(resp, r); mats[i] = mat; cambia = true; }
            }
            if (!cambia) continue;
            Undo.RecordObject(r, "Look Frutiger");
            r.sharedMaterials = mats;
            cambiados++;
        }
        return cambiados;
    }

    // ---------- Plantas reales (modelos hechos en Blender) ----------
    // Modelos propios generados con un script de Blender (ramas que se dividen + cientos de hojas):
    //   Arbusto_Redondo_1/2, Arbusto_Flores_1/2, Helecho_1/2, Planta_Interior_1/2  (Shared/Models/Plantas)
    // Afuera: cada arbusto ("Arbusto_N", esferas "Arbusto" de la isla) se cambia por un arbusto
    //         redondo o con flores, y a su alrededor crecen 1-2 helechos -> jardín más vivo.
    // Adentro: la planta de cada maceta ("Maceta_N") se cambia por una planta de interior o un helecho.
    // Las hojas usan el shader Glossy/FollajeNatural (luz que atraviesa, viento, sombras).
    const string CarpetaPlantas = "Assets/_Project/Shared/Models/Plantas";
    const string RutaAtlasPlantas = "Assets/_Project/Shared/Textures/T_AtlasPlantas.png";
    const string RutaMatHojas = CarpetaMateriales + "/M_HojasPlantas.mat";
    const string RutaMatMadera = CarpetaMateriales + "/M_TalloMadera.mat";
    const string RutaMatTalloVerde = CarpetaMateriales + "/M_TalloVerde.mat";

    static int ArbustosNaturales()
    {
        var hojas = MaterialHojas();
        if (hojas == null) return 0;
        var madera = MaterialTallo(RutaMatMadera, new Color(0.3f, 0.19f, 0.11f));
        var verde = MaterialTallo(RutaMatTalloVerde, new Color(0.22f, 0.5f, 0.15f));
        GameObject M(string n) => AssetDatabase.LoadAssetAtPath<GameObject>($"{CarpetaPlantas}/{n}.fbx");
        var redondos = new[] { M("Arbusto_Redondo_1"), M("Arbusto_Redondo_2") };
        var flores = new[] { M("Arbusto_Flores_1"), M("Arbusto_Flores_2") };
        var helechos = new[] { M("Helecho_1"), M("Helecho_2") };
        var interior = new[] { M("Planta_Interior_1"), M("Planta_Interior_2") };
        if (redondos[0] == null) { Debug.LogWarning("No están los modelos de plantas en " + CarpetaPlantas); return 0; }

        var resp = Respaldo(true);
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t != null && t.parent == null && t.name == NombreArbustos) Undo.DestroyObjectImmediate(t.gameObject);
        var raiz = new GameObject(NombreArbustos);
        Undo.RegisterCreatedObjectUndo(raiz, "Look Frutiger");

        var usados = new HashSet<Renderer>();
        var grupos = new List<(List<Renderer> rs, bool maceta)>();
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            string n = t.name.ToLower();
            bool maceta = n.StartsWith("maceta");
            if (!(n.StartsWith("arbusto_") || maceta)) continue;
            var lista = new List<Renderer>();
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                if (!usados.Contains(r) && EsFollaje(r)) lista.Add(r);
            if (lista.Count == 0) continue;
            foreach (var r in lista) usados.Add(r);
            grupos.Add((lista, maceta));
        }
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            if (!usados.Contains(r) && EsFollaje(r) && EsArbustoSuelto(r)) { usados.Add(r); grupos.Add((new List<Renderer> { r }, false)); }

        int hechos = 0;
        foreach (var (rs, maceta) in grupos)
        {
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            if (b.size.x > 6f || b.size.z > 6f || b.size.y > 6f || b.size.magnitude < 0.15f) continue;   // setos largos: no
            foreach (var r in rs)
            {
                if (!resp.ocultos.Contains(r)) { Undo.RecordObject(resp, "Look Frutiger"); resp.ocultos.Add(r); }
                Undo.RecordObject(r, "Look Frutiger");
                r.enabled = false;
            }
            int h = Mathf.Abs(Mathf.RoundToInt(b.center.x * 73f + b.center.z * 131f));
            if (maceta)
            {
                var modelo = interior[h % 2];
                Plantar(raiz.transform, modelo, b, 1.05f, h, hojas, madera, verde);
            }
            else
            {
                var modelo = h % 3 == 0 ? flores[h % 2] : redondos[h % 2];
                Plantar(raiz.transform, modelo, b, 1.15f, h, hojas, madera, verde);
                // jardín más vivo: 1 o 2 helechos al pie del arbusto
                int extra = 1 + h % 2;
                for (int k = 0; k < extra; k++)
                {
                    float ang = (h * 0.37f + k * 2.4f) % (Mathf.PI * 2f);
                    float d = Mathf.Max(b.extents.x, b.extents.z) * 1.05f;
                    var c = new Vector3(b.center.x + Mathf.Cos(ang) * d, b.min.y, b.center.z + Mathf.Sin(ang) * d);
                    var bh = new Bounds(c + Vector3.up * 0.2f, new Vector3(0.7f, 0.4f, 0.7f) * Mathf.Clamp(b.size.y, 0.6f, 1.4f));
                    Plantar(raiz.transform, helechos[(h + k) % 2], bh, 0.9f, h + k * 17, hojas, madera, verde);
                }
            }
            hechos++;
        }
        return hechos;
    }

    static bool EsFollaje(Renderer r)
    {
        if (r is ParticleSystemRenderer) return false;
        string n = r.name.ToLower();
        if (n.Contains("tronco") || n.Contains("maceta") || n.Contains("tierra") || n.Contains("borde")) return false;
        if (n == "hojas" || n.StartsWith("arbusto")) return true;
        foreach (var m in r.sharedMaterials)
        {
            if (m == null) continue;
            string mn = m.name.ToLower();
            if (mn.Contains("arbusto") || mn.Contains("hojas") || mn.Contains("pastomata") || mn.Contains("pastohoja")) return true;
        }
        return false;
    }

    static bool EsArbustoSuelto(Renderer r)
    {
        bool arbusto = r.name.ToLower().StartsWith("arbusto");
        foreach (var m in r.sharedMaterials) if (m != null && m.name.ToLower().Contains("arbusto")) arbusto = true;
        // esferas verdes lisas (arbustos simples de la isla y los juegos)
        if (!arbusto && r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null && mf.sharedMesh.name.Contains("Sphere"))
            foreach (var m in r.sharedMaterials)
                if (m != null && (m.name.ToLower().Contains("verde") || m.name.ToLower().Contains("hoja"))) arbusto = true;
        if (!arbusto) return false;
        for (var p = r.transform.parent; p != null; p = p.parent)
        {
            string n = p.name.ToLower();
            if (n.Contains("arbol") || n.Contains("palmera") || n.Contains("tree")) return false;
        }
        return true;
    }

    // Pone un modelo de planta ocupando el lugar (y el tamaño) de 'b'
    static void Plantar(Transform padre, GameObject modelo, Bounds b, float holgura, int semilla,
                        Material hojas, Material madera, Material verde)
    {
        if (modelo == null) return;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
        go.transform.SetParent(padre, false);
        Undo.RegisterCreatedObjectUndo(go, "Look Frutiger");
        // tamaño real del modelo
        var rm = go.GetComponentInChildren<Renderer>();
        var tam = rm != null ? rm.bounds.size : Vector3.one;
        float ancho = Mathf.Max(b.size.x, b.size.z) / Mathf.Max(0.01f, Mathf.Max(tam.x, tam.z));
        float alto = b.size.y / Mathf.Max(0.01f, tam.y);
        float escala = Mathf.Clamp(Mathf.Max(ancho, alto * 0.8f), 0.2f, 6f) * holgura;
        go.transform.localScale = Vector3.one * escala;
        go.transform.rotation = Quaternion.Euler(0f, semilla % 360, 0f);
        go.transform.position = new Vector3(b.center.x, b.min.y, b.center.z);
        // materiales según el nombre que traen del modelo
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                string n = mats[i] != null ? mats[i].name.ToLower() : "";
                mats[i] = n.Contains("hoja") ? hojas : n.Contains("madera") ? madera : verde;
            }
            r.sharedMaterials = mats;
        }
    }

    static Material MaterialHojas()
    {
        var mat = MaterialNuevo(RutaMatHojas, "Glossy/FollajeNatural");
        if (mat == null) return null;
        var imp = AssetImporter.GetAtPath(RutaAtlasPlantas) as TextureImporter;
        if (imp != null && (!imp.alphaIsTransparency || !imp.mipmapEnabled))
        {
            imp.alphaIsTransparency = true; imp.mipmapEnabled = true;
            imp.SaveAndReimport();
        }
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(RutaAtlasPlantas));
        mat.SetColor("_Color", new Color(1.05f, 1.1f, 0.95f));        // un poco más vivo (verde Frutiger)
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material MaterialTallo(string ruta, Color color)
    {
        var mat = MaterialNuevo(ruta, "Universal Render Pipeline/Lit");
        if (mat == null) return null;
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", 0.2f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ---------- Luz de día ----------
    // Cielo azul de día (el "Sky_FrutigerAero" del proyecto, si existe) y un sol blanco y claro.
    static bool LuzDeDia()
    {
        var resp = Respaldo(true);
        Light sol = RenderSettings.sun;
        if (sol == null)
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sol = l; break; }

        Undo.RecordObject(resp, "Look Frutiger");
        if (!resp.guardoLuz)
        {
            resp.guardoLuz = true;
            resp.cielo = RenderSettings.skybox;
            resp.sol = sol;
            if (sol != null) { resp.colorSol = sol.color; resp.intensidadSol = sol.intensity; resp.rotacionSol = sol.transform.rotation; }
        }

        var cielo = CieloDeDia();
        if (cielo != null) RenderSettings.skybox = cielo;
        if (sol != null)
        {
            Undo.RecordObject(sol, "Look Frutiger"); Undo.RecordObject(sol.transform, "Look Frutiger");
            sol.color = new Color(1f, 0.97f, 0.92f);          // blanco cálido de mediodía
            sol.intensity = 1.25f;
            sol.transform.rotation = Quaternion.Euler(62f, -35f, 0f);   // sol alto: casi nunca queda de frente a la cámara
        }
        DynamicGI.UpdateEnvironment();
        foreach (var rp in Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None))
            if (rp.mode == UnityEngine.Rendering.ReflectionProbeMode.Realtime) rp.RenderProbe();   // reflejos con el cielo nuevo
        return true;
    }

    // Copia propia del cielo de día de Valentina (Sky_FrutigerAero) con un sol más pequeño y suave:
    // el disco del sol original es tan intenso que con el bloom tapaba media pantalla.
    static Material CieloDeDia()
    {
        const string ruta = CarpetaMateriales + "/Sky_FrutigerDia.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat == null)
        {
            Material original = null;
            foreach (var guid in AssetDatabase.FindAssets("Sky_FrutigerAero t:Material"))
            { original = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)); if (original != null) break; }
            if (original == null) return null;
            if (!AssetDatabase.IsValidFolder(CarpetaMateriales)) AssetDatabase.CreateFolder("Assets/_Project/Shared", "Materials");
            mat = new Material(original);
            AssetDatabase.CreateAsset(mat, ruta);
        }
        if (mat.HasProperty("_SunDisk")) mat.SetFloat("_SunDisk", 1f);           // sol simple (no el de alta calidad, más intenso)
        if (mat.HasProperty("_SunSize")) mat.SetFloat("_SunSize", 0.025f);
        if (mat.HasProperty("_Exposure")) mat.SetFloat("_Exposure", 1.2f);
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
