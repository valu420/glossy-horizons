// Herramienta compartida: "Look Frutiger Aero" para cualquier escena (Hub, zonas, menú).
// NO cambia ningún modelo: solo cómo se ve la escena.
// Menú: Glossy Horizons > Look Frutiger > Aplicar a la escena abierta
//                                       > Quitar de la escena abierta
// Qué hace al aplicar:
//   1. Post-procesado "Frutiger Aero" (perfil compartido Shared/Settings/VP_FrutigerAero):
//      bloom solo en los puntos de luz con tinte cian y "lente con burbujas", más saturación y contraste,
//      balance de blancos frío, destellos de lente y viñeta suave. Va en un Volume propio con
//      prioridad alta, así no hay que tocar el perfil de la escena.
//   2. Reflejos: agrega sondas de reflejo (en tiempo real, se actualizan al empezar) repartidas
//      por toda la escena + una grande de respaldo, y sube la resolución de las que ya existen.
//      Así el vidrio, el piso, el agua y el cromo reflejan el cielo y el entorno.
//   3. Neones: los materiales con "Neon" en el nombre brillan de verdad (emisión HDR -> bloom).
//   4. Calidad: activa las sondas en tiempo real y el antialiasing MSAA 4x (bordes limpios).
//   5. Materiales naturales: cada material recibe el brillo que tendría en la vida real según su tipo
//      (el pasto y las hojas casi mates, la madera y la roca mates, el piso pulido pero no espejo,
//      el plástico satinado, el vidrio, el agua y el cromo muy brillantes). Solo cambia valores,
//      nunca texturas ni modelos.
// "Quitar" borra el Volume y las sondas que agregó (los neones y la calidad se quedan) y deshace
// las mejoras opcionales (ver LookFrutiger.Mejoras.cs: piso de baldosas, vidrio, arbustos, luz de día).
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static partial class LookFrutiger
{
    const string CarpetaAjustes = "Assets/_Project/Shared/Settings";
    const string RutaPerfil = CarpetaAjustes + "/VP_FrutigerAero.asset";
    const string RutaLente = "Assets/_Project/Shared/Textures/T_LenteBurbujas.png";
    const string NombreVolume = "LookFrutiger_PostProcesado";
    const string NombreSondas = "LookFrutiger_Reflejos";

    [MenuItem("Glossy Horizons/Look Frutiger/Aplicar a la escena abierta")]
    static void Aplicar()
    {
        var escena = EditorSceneManager.GetActiveScene();
        Quitar(false);

        // 1) Post-procesado
        var perfil = CrearPerfil();
        var goV = new GameObject(NombreVolume);
        Undo.RegisterCreatedObjectUndo(goV, "Look Frutiger");
        var volumen = goV.AddComponent<Volume>();
        volumen.isGlobal = true;
        volumen.priority = 50;                 // por encima del perfil que ya tenga la escena
        volumen.sharedProfile = perfil;

        // 2) Reflejos
        int sondas = CrearSondas();

        // 3) Neones
        int neones = EncenderNeones();
        int naturales = AjustarMateriales();

        // 4) Calidad
        QualitySettings.realtimeReflectionProbes = true;
        // Protección: si algún material produce un píxel inválido (NaN), la cámara lo descarta
        // en vez de dejar que el bloom lo convierta en un brillo gigante.
        var camara = Camera.main;
        if (camara != null)
        {
            var datos = camara.GetUniversalAdditionalCameraData();
            Undo.RecordObject(datos, "Look Frutiger");
            datos.stopNaN = true;
            EditorUtility.SetDirty(datos);
        }
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp != null && urp.msaaSampleCount < 4)
        {
            Undo.RecordObject(urp, "Look Frutiger");
            urp.msaaSampleCount = 4;
            EditorUtility.SetDirty(urp);
        }

        EditorSceneManager.MarkSceneDirty(escena);
        AssetDatabase.SaveAssets();
        Debug.Log($"Look Frutiger aplicado a '{escena.name}': post-procesado, {sondas} sondas de reflejo nuevas, " +
                  $"{neones} materiales de neón encendidos, {naturales} materiales ajustados a un acabado natural, MSAA 4x. " +
                  "Guarda la escena (Ctrl+S) para conservarlo.");
    }

    [MenuItem("Glossy Horizons/Look Frutiger/Quitar de la escena abierta")]
    static void QuitarMenu()
    {
        QuitarMejoras();                       // piso, vidrio, arbustos y luz de día (si se aplicaron)
        Quitar(true);
    }

    static void Quitar(bool avisar)
    {
        int borrados = 0;
        foreach (var go in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go == null || go.parent != null) continue;
            if (go.name == NombreVolume || go.name == NombreSondas) { Undo.DestroyObjectImmediate(go.gameObject); borrados++; }
        }
        if (avisar)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log(borrados > 0 ? "Look Frutiger quitado de la escena." : "La escena no tenía el Look Frutiger.");
        }
    }

    // ---------- 1. Perfil de post-procesado ----------
    // Se vuelve a ajustar cada vez que se aplica (así los cambios de valores llegan al perfil ya creado).
    // Idea: brillo SOLO en los puntos de luz (sol, neones, reflejos fuertes), no en todo lo blanco;
    // blancos limpios, azules y verdes profundos y con contraste, como las referencias Frutiger Aero.
    static VolumeProfile CrearPerfil()
    {
        var perfil = AssetDatabase.LoadAssetAtPath<VolumeProfile>(RutaPerfil);
        if (perfil == null)
        {
            if (!AssetDatabase.IsValidFolder(CarpetaAjustes)) AssetDatabase.CreateFolder("Assets/_Project/Shared", "Settings");
            perfil = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(perfil, RutaPerfil);
        }

        var bloom = Obtener<Bloom>(perfil);
        bloom.threshold.Override(1.15f);                        // solo brilla lo que de verdad es luz (no el piso blanco)
        bloom.intensity.Override(0.55f);
        bloom.scatter.Override(0.62f);
        bloom.tint.Override(new Color(0.85f, 0.97f, 1f));       // tinte cian suave
        bloom.highQualityFiltering.Override(true);
        bloom.clamp.Override(4f);                               // ningún punto (el sol, un reflejo) puede encandilar
        var lente = AssetDatabase.LoadAssetAtPath<Texture2D>(RutaLente);
        if (lente != null) { bloom.dirtTexture.Override(lente); bloom.dirtIntensity.Override(0.8f); }

        var tono = Obtener<Tonemapping>(perfil);
        tono.mode.Override(TonemappingMode.Neutral);

        var color = Obtener<ColorAdjustments>(perfil);
        color.postExposure.Override(-0.25f);                    // baja un poco la luz general (quita lo "lavado")
        color.contrast.Override(22f);                           // blancos limpios y colores profundos
        color.saturation.Override(35f);                         // azules y verdes vivos
        color.colorFilter.Override(Color.white);

        var blancos = Obtener<WhiteBalance>(perfil);
        blancos.temperature.Override(-4f);                      // apenas más frío
        blancos.tint.Override(-2f);

        var vineta = Obtener<Vignette>(perfil);
        vineta.intensity.Override(0.2f);
        vineta.smoothness.Override(0.45f);
        vineta.color.Override(new Color(0.03f, 0.12f, 0.28f));  // viñeta azul profundo, no negra

        var destellos = Obtener<ScreenSpaceLensFlare>(perfil);
        destellos.intensity.Override(0.15f);
        destellos.tintColor.Override(new Color(0.75f, 0.95f, 1f));

        EditorUtility.SetDirty(perfil);
        AssetDatabase.SaveAssets();
        return perfil;
    }

    // Devuelve el efecto si el perfil ya lo tiene; si no, lo agrega
    static T Obtener<T>(VolumeProfile perfil) where T : VolumeComponent
    {
        if (perfil.TryGet<T>(out var existente)) return existente;
        var c = perfil.Add<T>(true);
        c.name = typeof(T).Name;
        AssetDatabase.AddObjectToAsset(c, perfil);
        return c;
    }

    // ---------- 2. Sondas de reflejo ----------
    static int CrearSondas()
    {
        // Las sondas que ya tenga la escena: más resolución y más importancia
        foreach (var rp in Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None))
        {
            Undo.RecordObject(rp, "Look Frutiger");
            rp.resolution = Mathf.Max(rp.resolution, 512);
            rp.hdr = true;
            rp.importance = Mathf.Max(rp.importance, 2);
        }

        // Límites de la escena (sin el cielo ni objetos gigantes)
        var limites = new Bounds(); bool hay = false;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (!r.enabled || r.bounds.size.magnitude > 2000f || r.bounds.center.magnitude > 180f) continue;   // sin la ciudad ni las nubes lejanas
            if (!hay) { limites = r.bounds; hay = true; } else limites.Encapsulate(r.bounds);
        }
        if (!hay) return 0;

        var raiz = new GameObject(NombreSondas);
        Undo.RegisterCreatedObjectUndo(raiz, "Look Frutiger");

        // Una sonda grande de respaldo para toda la escena
        Sonda(raiz.transform, "Sonda_General", limites.center, limites.size + Vector3.one * 10f, 0, 256);

        // Una cuadrícula de sondas sobre el suelo (cada ~40 m), donde haya algo debajo
        Physics.SyncTransforms();                                  // para que los rayos funcionen en el editor
        const float Paso = 40f;
        int creadas = 1;
        int nx = Mathf.Clamp(Mathf.CeilToInt(limites.size.x / Paso), 1, 8);
        int nz = Mathf.Clamp(Mathf.CeilToInt(limites.size.z / Paso), 1, 8);
        float alto = Mathf.Clamp(limites.size.y, 12f, 40f);
        for (int i = 0; i < nx; i++)
            for (int k = 0; k < nz; k++)
            {
                float x = limites.min.x + (i + 0.5f) * limites.size.x / nx;
                float z = limites.min.z + (k + 0.5f) * limites.size.z / nz;
                var desde = new Vector3(x, limites.max.y + 5f, z);
                if (!Physics.Raycast(desde, Vector3.down, out var golpe, limites.size.y + 50f, ~0, QueryTriggerInteraction.Ignore))
                    continue;                                       // no hay nada debajo (mar abierto, vacío)
                var centro = golpe.point + Vector3.up * 4f;
                var tam = new Vector3(limites.size.x / nx + 8f, alto, limites.size.z / nz + 8f);
                Sonda(raiz.transform, $"Sonda_{i}_{k}", centro + Vector3.up * (alto / 2f - 4f), tam, 1, 256);
                creadas++;
            }
        return creadas;
    }

    static void Sonda(Transform padre, string nombre, Vector3 centro, Vector3 tam, int importancia, int resolucion)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.position = centro;
        var rp = go.AddComponent<ReflectionProbe>();
        rp.mode = ReflectionProbeMode.Realtime;                    // no hace falta "hornear" la iluminación
        rp.refreshMode = ReflectionProbeRefreshMode.OnAwake;       // se actualiza una vez al empezar
        rp.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
        rp.resolution = resolucion;
        rp.hdr = true;
        rp.size = tam;
        // Sin "box projection": estas cajas no coinciden con las paredes reales y deformaban los
        // reflejos (rayas y manchas raras en el piso). Las sondas que ya trae la escena sí lo conservan.
        rp.boxProjection = false;
        rp.blendDistance = 6f;
        rp.importance = importancia;
        rp.RenderProbe();                                          // para verla ya en el editor
    }

    // ---------- 5. Materiales con acabado natural ----------
    // Todo con 0.8-0.98 de brillo parece de plástico mojado. En la vida real, y en los renders
    // Frutiger Aero, lo brillante es el vidrio, el agua, el cromo y el piso pulido; lo natural es mate.
    // Se reconoce el tipo por el nombre del material.
    struct Acabado { public string[] claves; public float suavidad; public float metal; public string nombre; }

    static readonly Acabado[] Acabados =
    {
        new Acabado { nombre = "pasto",    claves = new[] { "pasto", "cesped", "grass" },                 suavidad = 0.18f, metal = 0f },
        new Acabado { nombre = "hojas",    claves = new[] { "hoja", "arbusto", "leaf", "bush" },         suavidad = 0.3f,  metal = 0f },
        new Acabado { nombre = "flores",   claves = new[] { "flor", "flower", "nervadura" },             suavidad = 0.35f, metal = 0f },
        new Acabado { nombre = "madera",   claves = new[] { "madera", "tronco", "wood" },                suavidad = 0.22f, metal = 0f },
        new Acabado { nombre = "roca",     claves = new[] { "roca", "piedra", "acantilado", "rock" },    suavidad = 0.2f,  metal = 0f },
        new Acabado { nombre = "camino",   claves = new[] { "camino", "sendero", "path" },               suavidad = 0.35f, metal = 0f },
        new Acabado { nombre = "piso",     claves = new[] { "piso", "azulejo", "floor", "baldosa" },     suavidad = 0.78f, metal = 0f },
        new Acabado { nombre = "plastico", claves = new[] { "juego_", "marco", "placa", "pantalla", "texto", "candado" }, suavidad = 0.68f, metal = -1f },
        new Acabado { nombre = "piel",     claves = new[] { "delfin" },                                  suavidad = 0.65f, metal = 0f },
    };

    static int AjustarMateriales()
    {
        var hechos = new HashSet<Material>();
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            foreach (var m in r.sharedMaterials)
            {
                if (m == null || hechos.Contains(m) || !m.HasProperty("_Smoothness")) continue;
                string n = m.name.ToLower();
                // vidrio, agua, cromo, neones y orbes se quedan brillantes
                if (n.Contains("vidrio") || n.Contains("cristal") || n.Contains("glass") || n.Contains("agua") ||
                    n.Contains("chorro") || n.Contains("isla_mar") || n.Contains("cromo") || n.Contains("neon") || n.Contains("orbe"))
                    continue;
                foreach (var a in Acabados)
                {
                    bool coincide = false;
                    foreach (var c in a.claves) if (n.Contains(c)) { coincide = true; break; }
                    if (!coincide) continue;
                    Undo.RecordObject(m, "Look Frutiger");
                    m.SetFloat("_Smoothness", a.suavidad);
                    if (a.metal >= 0f && m.HasProperty("_Metallic")) m.SetFloat("_Metallic", a.metal);
                    EditorUtility.SetDirty(m);
                    hechos.Add(m);
                    break;
                }
            }
        return hechos.Count;
    }

    // ---------- 3. Neones que brillan ----------
    static int EncenderNeones()
    {
        var hechos = new HashSet<Material>();
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            foreach (var m in r.sharedMaterials)
            {
                if (m == null || hechos.Contains(m) || !m.name.ToLower().Contains("neon")) continue;
                if (!m.HasProperty("_EmissionColor")) continue;
                Color actual = m.IsKeywordEnabled("_EMISSION") ? m.GetColor("_EmissionColor") : Color.black;
                if (actual.maxColorComponent >= 1.5f) { hechos.Add(m); continue; }   // ya brilla
                Color baseColor = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                Undo.RecordObject(m, "Look Frutiger");
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor("_EmissionColor", baseColor * 2.5f);         // HDR: más de 1 -> resplandor con el bloom
                EditorUtility.SetDirty(m);
                hechos.Add(m);
            }
        return hechos.Count;
    }
}
