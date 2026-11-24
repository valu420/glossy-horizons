// "Hub final": aplica TODAS las mejoras de una sola vez y GUARDA la escena del Hub.
// AUTOMÁTICO: al abrir Hub.unity en el editor, si todavía no tiene la versión actual de las mejoras
// (objeto marcador "HubFrutiger_vN"), se aplican solas y se guarda. No hay que ejecutar nada a mano.
// Se puede apagar en Glossy Horizons > Hub > Mejorar Hub automáticamente.
// Menú: Glossy Horizons > Hub > Mejorar Hub completo y guardar
//   1. Look Frutiger (post-procesado, reflejos, neones, materiales naturales, MSAA)
//   2. Piso de baldosas, vidrio limpio, luz de día y plantas reales en lugar de los arbustos
//   3. Jardín Frutiger alrededor del lobby (camino de piedras, flores, rocas, arbustos)
//   4. Pasto natural (más tupido y alto, con viento y degradado de raíz a punta)
//   5. Guarda Hub.unity
// Se puede deshacer con git (descartar los cambios de Hub.unity) antes de subirlo.
using GlossyHorizons.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static partial class LookFrutiger
{
    const string RutaHub = "Assets/_Project/Core/Scenes/Hub.unity";
    const int VersionHub = 15;                                   // se sube cuando cambian las mejoras
    const string Marcador = "HubFrutiger_v";
    const string ClaveAuto = "GlossyHorizons.HubAutoFrutiger";
    const string MenuAuto = "Glossy Horizons/Hub/Mejorar Hub automáticamente";
    static bool aplicando;

    static LookFrutiger()
    {
        EditorSceneManager.sceneOpened += (escena, modo) => EditorApplication.delayCall += () => Revisar(escena);
        EditorApplication.delayCall += () => Revisar(EditorSceneManager.GetActiveScene());
        // también al volver de Play (por si se le dio Play justo después de compilar)
        EditorApplication.playModeStateChanged += estado =>
        {
            if (estado == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += () => Revisar(EditorSceneManager.GetActiveScene());
        };
    }

    static void Revisar(UnityEngine.SceneManagement.Scene escena)
    {
        if (aplicando || !escena.IsValid() || !escena.isLoaded || escena.path != RutaHub) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || !EditorPrefs.GetBool(ClaveAuto, true)) return;
        foreach (var go in escena.GetRootGameObjects())
            if (go.name == Marcador + VersionHub) return;            // ya está al día
        Debug.Log("Hub: aplicando las mejoras Frutiger automáticamente (versión " + VersionHub + ")...");
        AplicarTodo(escena);
    }

    [MenuItem(MenuAuto)]
    static void AlternarAuto() => EditorPrefs.SetBool(ClaveAuto, !EditorPrefs.GetBool(ClaveAuto, true));
    [MenuItem(MenuAuto, true)]
    static bool AlternarAutoValidar() { Menu.SetChecked(MenuAuto, EditorPrefs.GetBool(ClaveAuto, true)); return true; }

    [MenuItem("Glossy Horizons/Hub/Mejorar Hub completo y guardar")]
    static void MejorarHubCompleto()
    {
        if (!EditorUtility.DisplayDialog("Mejorar Hub completo",
                "Se abrirá Hub.unity, se aplicarán todas las mejoras (look, piso, vidrio, luz de día, plantas, " +
                "jardín y pasto) y se GUARDARÁ la escena.\n\nAvísales a Valentina y Juan antes de subirla.",
                "Mejorar y guardar", "Cancelar"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        AplicarTodo(EditorSceneManager.OpenScene(RutaHub, OpenSceneMode.Single));
    }

    static void AplicarTodo(UnityEngine.SceneManagement.Scene escena)
    {
        aplicando = true;
        try
        {
            EditorUtility.DisplayProgressBar("Mejorar Hub", "Look Frutiger...", 0.1f);
            Aplicar();
            EditorUtility.DisplayProgressBar("Mejorar Hub", "Piso, vidrio, luz y plantas...", 0.3f);
            QuitarMejoras();
            int a = PisoDeBaldosas(), b = VidrioLimpio(), c = ArbustosNaturales();
            LuzDeDia();
            EditorUtility.DisplayProgressBar("Mejorar Hub", "Jardín Frutiger...", 0.55f);
            CrearJardin();
            EditorUtility.DisplayProgressBar("Mejorar Hub", "Pasto natural...", 0.8f);
            radioDomo = -1f;
            int p = MejorarPasto();
            RocasConTextura();
            EditorUtility.DisplayProgressBar("Mejorar Hub", "Rocas, montículos y flores reales...", 0.88f);
            int e = ReemplazarEsferas();
            EditorUtility.DisplayProgressBar("Mejorar Hub", "Fuente de agua...", 0.94f);
            AguaAnimada();
            bool fuente = FuenteAnimada(escena);
            EditorUtility.DisplayProgressBar("Mejorar Hub", "Caminos, pasto y burbujas...", 0.97f);
            int caminos = CaminosDeAdoquines();
            int celdas = CubrirTerreno(escena);
            Burbujas(escena);
            EditorUtility.DisplayProgressBar("Mejorar Hub", "Ciudad y nubes...", 0.99f);
            int torres = Ciudad(escena), nubes = Nubes(escena);
            EditorUtility.DisplayProgressBar("Mejorar Hub", "Árboles de la isla y auroras...", 0.995f);
            int arboles = ArbolesIsla(escena), auroras = Auroras(escena);
            AguaCristalina();
            Debug.Log($"Hub: ciudad de fondo con {torres} torres, {arboles} árboles en la isla, {auroras} auroras, cielo Frutiger y agua cristalina.");
            Debug.Log($"Hub: {caminos} caminos con adoquines, pasto tupido en {celdas} celdas de terreno, burbujas flotando.");
            Debug.Log($"Hub: {e} esferas cambiadas por modelos reales; fuente animada: {(fuente ? "sí" : "no encontrada")}.");
            if (Object.FindAnyObjectByType<PuertasAutomaticas>() == null)
            {
                var puertas = new GameObject("PuertasAutomaticas");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(puertas, escena);
                puertas.AddComponent<PuertasAutomaticas>();
            }
            foreach (var go in escena.GetRootGameObjects())
                if (go.name.StartsWith(Marcador)) Object.DestroyImmediate(go);
            var marca = new GameObject(Marcador + VersionHub) { tag = "EditorOnly" };
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(marca, escena);
            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
            AssetDatabase.SaveAssets();
            Debug.Log($"Hub mejorado y guardado: {a} pisos con baldosas, {b} vidrios, {c} plantas reales, jardín y {p} campos de pasto natural.");
        }
        finally { aplicando = false; EditorUtility.ClearProgressBar(); }
    }

    [MenuItem("Glossy Horizons/Hub/Pasto natural (escena abierta)")]
    static void MenuPasto() { Avisar("campos de pasto natural", MejorarPasto()); }

    // Pasto natural: las briznas originales (hojas anchas) se cambian por MATAS propias de briznas finas,
    // altas y curvas (Shared/Models/PastoMata.asset), con el shader Glossy/PastoNatural. El suelo bajo el
    // pasto se oscurece a verde oliva para que los huecos entre matas no se vean verde plano.
    const string RutaMallaPasto = "Assets/_Project/Shared/Models/PastoMata_v4.asset";

    static int MejorarPasto()
    {
        var malla = MallaPasto();
        var mat = MaterialNuevo($"{CarpetaMateriales}/M_PastoNatural.mat", "Glossy/PastoNatural");
        if (mat == null || malla == null) return 0;
        mat.enableInstancing = true;
        mat.SetTexture("_BaseMap", null);
        mat.SetFloat("_Alto", malla.bounds.max.y);
        mat.SetColor("_ColorRaiz", new Color(0.16f, 0.34f, 0.04f));
        mat.SetColor("_ColorPunta", new Color(0.66f, 0.9f, 0.16f));     // puntas verde-amarillo brillante
        mat.SetFloat("_Variacion", 0.5f);
        mat.SetFloat("_Viento", 0.28f);                                // olas de viento bien visibles
        mat.SetFloat("_VelocidadViento", 1.6f);
        mat.SetFloat("_Translucidez", 1.1f);
        EditorUtility.SetDirty(mat);

        int hechos = 0;
        foreach (var campo in Object.FindObjectsByType<GrassField>(FindObjectsSortMode.None))
        {
            Undo.RecordObject(campo, "Pasto natural");
            foreach (var capa in campo.layers)
            {
                if (capa == null || capa.mesh == null) continue;
                string n = (capa.name + " " + capa.mesh.name).ToLower();
                if (n.Contains("hoja") || n.Contains("pastomata"))
                {
                    capa.mesh = malla;
                    capa.materials = new[] { mat };
                    capa.density = 11f;                             // 11 matas x 30 briznas = ~330 briznas por m²
                    capa.scaleRange = new Vector2(0.85f, 1.25f);
                    capa.castShadows = false;
                }
                else if (n.Contains("mata")) capa.density = 0f;     // las hojas anchas acostadas ya no se usan
            }
            campo.Generate();
            EditorUtility.SetDirty(campo);
            hechos++;
        }
        SueloConTextura();
        return hechos;
    }

    // Rocas lisas de la escena (materiales "roca", piedras pequeñas) -> material de roca con textura
    static void RocasConTextura()
    {
        var roca = MaterialConTextura("M_RocaJardin", "Universal Render Pipeline/Lit", "T_Roca.png", 0.25f, false);
        if (roca == null) return;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (r.bounds.size.magnitude > 12f) continue;                      // acantilados y montañas: no
            var lista = r.sharedMaterials; bool cambia = false;
            for (int i = 0; i < lista.Length; i++)
                if (lista[i] != null && lista[i] != roca && (lista[i].name.ToLower().Contains("roca") || lista[i].name.ToLower().Contains("piedra")))
                { lista[i] = roca; cambia = true; }
            if (!cambia) continue;
            Undo.RecordObject(r, "Rocas");
            r.sharedMaterials = lista;
        }
    }

    // Suelo con textura de pasto (en coordenadas del mundo, cada 4 m) en vez de un color plano:
    // todo objeto con material de césped/terreno ("Juego_Cesped", "M_PastoTerreno", "M_Pasto"...)
    static void SueloConTextura()
    {
        var ms = MaterialNuevo($"{CarpetaMateriales}/M_SueloPasto.mat", "Glossy/BaldosasMundo");
        if (ms == null) return;
        ms.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Shared/Textures/T_SueloPasto.png"));
        ms.SetTexture("_BumpMap", null);
        ms.SetFloat("_Tamano", 4f);
        ms.SetFloat("_Smoothness", 0.12f);
        ms.SetFloat("_BrilloJunta", 0.08f);
        EditorUtility.SetDirty(ms);
        int n = 0;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            var lista = r.sharedMaterials; bool cambia = false;
            for (int i = 0; i < lista.Length; i++)
            {
                if (lista[i] == null) continue;
                string m = lista[i].name.ToLower();
                bool suelo = m.Contains("cesped") || m == "m_pasto" || m.Contains("pastoterreno") || m.Contains("grass_ground");
                if (suelo) { lista[i] = ms; cambia = true; }
            }
            if (!cambia) continue;
            Undo.RecordObject(r, "Suelo de pasto");
            r.sharedMaterials = lista;
            n++;
        }
        Debug.Log($"Suelo de pasto con textura en {n} objetos.");
    }

    // Una "mata" de alfombra: 30 briznas LARGAS y finas (0.4 a 0.7 m) repartidas en 0.6 m, muy dobladas
    // y acostadas casi todas hacia el mismo lado: juntas forman una alfombra continua que ondula.
    static Mesh MallaPasto()
    {
        var m = AssetDatabase.LoadAssetAtPath<Mesh>(RutaMallaPasto);
        if (m != null) return m;
        var azar = new System.Random(77);
        float Az(float a, float b) => a + (float)azar.NextDouble() * (b - a);
        var v = new System.Collections.Generic.List<Vector3>(); var n = new System.Collections.Generic.List<Vector3>();
        var uv = new System.Collections.Generic.List<Vector2>(); var tri = new System.Collections.Generic.List<int>();
        const int Segmentos = 3;
        for (int b = 0; b < 30; b++)
        {
            float ang = Az(0f, Mathf.PI * 2f), r = Mathf.Sqrt(Az(0f, 1f)) * 0.3f;
            var raiz = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
            float alto = Az(0.38f, 0.62f), ancho = Az(0.018f, 0.028f), curva = Az(0.6f, 1.2f);
            float giro = 0.6f + Az(-0.6f, 0.6f);                        // casi todas hacia el mismo lado
            var dirCurva = new Vector3(Mathf.Cos(giro), 0f, Mathf.Sin(giro));
            var lado = Vector3.Cross(Vector3.up, dirCurva).normalized;
            int b0 = v.Count;
            for (int s = 0; s <= Segmentos; s++)
            {
                float t = s / (float)Segmentos;
                // la brizna sube y se arquea hasta quedar casi acostada (alto real ~0.25-0.4 m)
                var c = raiz + Vector3.up * (alto * 0.9f * t * (1f - 0.28f * t)) + dirCurva * (curva * 0.4f * alto * t * t);
                float w = ancho * (1f - t * 0.92f);                // termina en punta
                v.Add(c - lado * w); v.Add(c + lado * w);
                n.Add(Vector3.up); n.Add(Vector3.up);
                uv.Add(new Vector2(0f, t)); uv.Add(new Vector2(1f, t));
            }
            for (int s = 0; s < Segmentos; s++)
            {
                int a = b0 + s * 2;
                tri.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
            }
        }
        m = new Mesh { name = "PastoMata" };
        m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
        m.RecalculateBounds();
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Shared/Models")) AssetDatabase.CreateFolder("Assets/_Project/Shared", "Models");
        AssetDatabase.CreateAsset(m, RutaMallaPasto);
        AssetDatabase.SaveAssets();
        return m;
    }
}
