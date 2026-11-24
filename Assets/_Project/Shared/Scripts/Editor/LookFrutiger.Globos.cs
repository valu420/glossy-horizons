// Hub final, parte 4: globos aerostáticos: unos pocos cerca de la isla y el resto dando vueltas sobre la ciudad.
//   - Modelos: Shared/Models/Globos/Globo_1..4.fbx, exportados de Blender (globo, globo2, globo3, globo4).
//     Cada FBX trae "Globo" (envoltura + cuerdas + cabina) y, dentro, "Fuego" (llama, pivote en su base).
//   - Materiales URP propios (Shared/Materials/Globos), brillantes como Frutiger; la llama es emisiva.
//   - Cada globo lleva GloboAerostatico: órbita lenta y ondulada, sube y baja, se mece, gira y enciende
//     el quemador cada tanto (la llama crece, la luz se aviva y gana altura).
// Se regeneran con el resto del fondo (GenerarTodo) o con: Glossy Horizons > Hub > Globos aerostáticos.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static partial class LookFrutiger
{
    const string CarpetaGlobos = "Assets/_Project/Shared/Models/Globos";
    const string RaizGlobos = "Globos_Aerostaticos";
    static readonly Vector3 CentroIsla = new Vector3(-8f, 0f, 0f);

    // zona (-1 = isla, 0..2 = distrito de la ciudad, ver Distritos), modelo (1..4), radio de la órbita alrededor
    // del centro de esa zona, altura media, velocidad (m/s), sentido horario, ondulación, desfase (0..1) y alto (m).
    // Alturas de la ciudad: la torre más alta llega a ~175 m (centro del distrito 1); las de los distritos 2 y 3 a
    // ~126 m. La base de cada globo queda unos 26 m bajo su altura media, así que pasan por encima de todo.
    // El dirigible (LookFrutiger.Dirigible) vuela a 140 m en un radio de 145 m: ningún globo se cruza con él.
    static readonly (int zona, int modelo, float radio, float altura, float vel, bool horario, float ond, float fase, float alto)[] ConfigGlobos =
    {
        // isla: pocos, para que no tapen la vista
        (-1, 1,  85f,  55f, 2.8f, false, 0.15f, 0.10f, 22f),
        (-1, 4, 120f,  75f, 2.4f, true,  0.12f, 0.60f, 22f),
        // sobre el mar, entre la isla y la ciudad
        (-1, 3, 165f,  95f, 2.0f, false, 0.06f, 0.35f, 26f),
        (-1, 2, 200f, 125f, 1.8f, true,  0.05f, 0.85f, 28f),
        // ciudad: dan vueltas sobre cada distrito, entre y por encima de las torres
        ( 0, 2,  60f, 215f, 1.6f, true,  0.10f, 0.00f, 30f),
        ( 0, 3, 120f, 195f, 1.8f, false, 0.08f, 0.50f, 30f),
        ( 1, 4,  55f, 165f, 1.5f, false, 0.10f, 0.25f, 28f),
        ( 1, 1,  95f, 185f, 1.7f, true,  0.08f, 0.75f, 26f),
        ( 2, 3,  55f, 168f, 1.5f, true,  0.10f, 0.40f, 28f),
        ( 2, 4,  95f, 185f, 1.7f, false, 0.08f, 0.90f, 26f),
    };

    static Vector3 CentroZona(int zona)
    {
        if (zona < 0 || zona >= Distritos.Length) return CentroIsla;
        float ang = Distritos[zona].ang * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(ang) * Distritos[zona].dist, 0f, Mathf.Sin(ang) * Distritos[zona].dist);
    }

    // color (sRGB) de cada material del FBX, tal como se pintó en Blender
    static readonly Dictionary<string, string> ColoresGlobos = new Dictionary<string, string>
    {
        ["G1_Material_001"] = "#e70a02", ["G1_Material_002"] = "#e7b200", ["G1_Material_003"] = "#0ae700",
        ["G1_Material_004"] = "#1630e7", ["G1_Material_005"] = "#e7870c", ["G1_Material_006"] = "#0ce761",
        ["G1_Material_007"] = "#e7836d", ["G1_Material_008"] = "#b261e7", ["G1_Material_009"] = "#e785a8",
        ["G1_Material_010"] = "#43e7e6",
        ["G2_Material_001"] = "#e70a02", ["G2_Material_002"] = "#c9ffd8", ["G2_Material_003"] = "#0ae700",
        ["G2_Material_004"] = "#1630e7", ["G2_Material_005"] = "#e7870c", ["G2_Material_006"] = "#0ce761",
        ["G2_Material_007"] = "#e7836d", ["G2_Material_008"] = "#b261e7", ["G2_Material_009"] = "#a0e768",
        ["G2_Material_010"] = "#c6e75a",
        ["G3_Globo3_azul_marino"] = "#1f2f5c", ["G3_Globo3_blanco"] = "#f4f4f2", ["G3_Globo3_rojo"] = "#c42a32",
        ["G4_Globo4_amarillo"] = "#ffd93b", ["G4_Globo4_naranja"] = "#ff8a3d", ["G4_Globo4_rosa"] = "#ff5fa2",
        ["G4_Globo4_morado"] = "#8e5bd6", ["G4_Globo4_azul"] = "#3d7be0", ["G4_Globo4_verde"] = "#5cc95a",
    };

    // piezas de la cabina: color, suavidad, metálico
    static readonly Dictionary<string, (string color, float suave, float metal)> PiezasGlobo = new Dictionary<string, (string, float, float)>
    {
        ["Metal_quemador"] = ("#c9d6e0", 0.75f, 1f),
        ["Dorado"] = ("#ffd36b", 0.8f, 1f),
        ["Mimbre_oscuro"] = ("#5a3418", 0.3f, 0f),
        ["Mimbre"] = ("#a8703a", 0.25f, 0f),
        ["Cuerda"] = ("#e8dcc0", 0.1f, 0f),
        ["Marco_puerta"] = ("#4a2a12", 0.4f, 0f),
        ["Puerta_madera"] = ("#c98b4e", 0.6f, 0f),
    };

    [MenuItem("Glossy Horizons/Hub/Globos aerostáticos")]
    static void MenuGlobos()
    {
        var escena = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        int n = Globos(escena);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(escena);
        Debug.Log($"{n} globos aerostáticos creados. Dale Play para verlos volar y guarda la escena (Ctrl+S).");
        // selecciona los globos y lleva la vista de escena hacia ellos
        var raiz = System.Array.Find(escena.GetRootGameObjects(), g => g.name == RaizGlobos);
        if (raiz != null)
        {
            Selection.activeGameObject = raiz;
            var caja = new Bounds(raiz.transform.position, Vector3.zero);
            foreach (var r in raiz.GetComponentsInChildren<Renderer>()) caja.Encapsulate(r.bounds);
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.Frame(caja, false);
        }
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    static Dictionary<string, Material> MaterialesGlobos()
    {
        string carpeta = CarpetaMateriales + "/Globos";
        if (!AssetDatabase.IsValidFolder(carpeta)) AssetDatabase.CreateFolder(CarpetaMateriales, "Globos");
        Material Lit(string nombre, Color c, float suave, float metal)
        {
            var m = MaterialNuevo($"{carpeta}/M_{nombre}.mat", "Universal Render Pipeline/Lit");
            if (m == null) return null;
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", suave); m.SetFloat("_Metallic", metal);
            m.SetFloat("_EnvironmentReflections", 1f); m.SetFloat("_SpecularHighlights", 1f);
            EditorUtility.SetDirty(m);
            return m;
        }
        Material Llama(string nombre, string color, float brillo)
        {
            var m = Lit(nombre, Hex(color), 0.5f, 0f);
            if (m == null) return null;
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Hex(color) * brillo);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(m);
            return m;
        }
        var mats = new Dictionary<string, Material>();
        // envoltura: lona brillante, casi lacada (reflejos tipo Frutiger Aero)
        foreach (var kv in ColoresGlobos) mats[kv.Key] = Lit(kv.Key, Hex(kv.Value), 0.82f, 0f);
        foreach (var kv in PiezasGlobo) mats[kv.Key] = Lit("Globo_" + kv.Key, Hex(kv.Value.color), kv.Value.suave, kv.Value.metal);
        mats["Fuego_exterior"] = Llama("Globo_Fuego_exterior", "#ff7a1a", 4f);
        mats["Fuego_interior"] = Llama("Globo_Fuego_interior", "#ffd23f", 6f);
        return mats;
    }

    // busca por prefijo: Blender exporta la llama como "Fuego.001"
    static Transform Buscar(Transform t, string nombre)
    {
        if (t.name == nombre || t.name.StartsWith(nombre + ".")) return t;
        foreach (Transform h in t) { var r = Buscar(h, nombre); if (r != null) return r; }
        return null;
    }

    static int Globos(UnityEngine.SceneManagement.Scene escena)
    {
        foreach (var go in escena.GetRootGameObjects()) if (go.name == RaizGlobos) Object.DestroyImmediate(go);
        var mats = MaterialesGlobos();
        var modelos = new GameObject[4];
        var altoModelo = new float[4];
        for (int i = 0; i < 4; i++)
        {
            modelos[i] = CargarModelo($"{CarpetaGlobos}/Globo_{i + 1}.fbx", mats, out _);
            if (modelos[i] == null) return 0;
            // alto real del globo completo (de la punta a la base de la canasta)
            var cuerpo = Buscar(modelos[i].transform, "Globo");
            var mf = cuerpo != null ? cuerpo.GetComponent<MeshFilter>() : modelos[i].GetComponentInChildren<MeshFilter>();
            altoModelo[i] = mf != null ? Mathf.Max(mf.sharedMesh.bounds.size.y * mf.transform.lossyScale.y, 1e-3f) : 1f;
        }

        var raiz = new GameObject(RaizGlobos);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(raiz, escena);
        int n = 0;
        foreach (var c in ConfigGlobos)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(modelos[c.modelo - 1]);
            go.name = $"Globo_{++n}";
            go.transform.SetParent(raiz.transform, false);
            go.transform.localScale = go.transform.localScale * (c.alto / altoModelo[c.modelo - 1]);
            float a = c.fase * Mathf.PI * 2f;
            go.transform.position = CentroZona(c.zona) + new Vector3(Mathf.Cos(a) * c.radio, c.altura, Mathf.Sin(a) * c.radio);
            // se conserva la rotación con la que Unity importó el FBX (si la hay) y se le suma el giro inicial
            go.transform.rotation = Quaternion.Euler(0f, c.fase * 360f, 0f) * go.transform.rotation;
            foreach (var rend in go.GetComponentsInChildren<Renderer>())
            {
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                rend.receiveShadows = false;
            }

            var globo = go.AddComponent<GloboAerostatico>();
            globo.centro = CentroZona(c.zona); globo.radio = c.radio; globo.altura = c.altura;
            globo.velocidad = c.vel; globo.horario = c.horario; globo.ondulacion = c.ond; globo.fase = c.fase;
            globo.subeBaja = 3f + c.alto * 0.12f;
            globo.periodoFlotacion = 18f + n * 2.3f;
            globo.giro = (n % 2 == 0 ? 1f : -1f) * (2f + n * 0.4f);
            globo.empujeQuemador = c.alto * 0.15f;

            var fuego = Buscar(go.transform, "Fuego");
            if (fuego != null)
            {
                globo.fuego = fuego;
                var luzGo = new GameObject("Luz_Fuego");
                luzGo.transform.SetParent(fuego.parent, false);
                luzGo.transform.position = fuego.position + Vector3.up * c.alto * 0.06f;
                var luz = luzGo.AddComponent<Light>();
                luz.type = LightType.Point;
                luz.color = new Color(1f, 0.6f, 0.25f);
                luz.range = c.alto * 0.45f;
                luz.intensity = 6f;
                luz.shadows = LightShadows.None;
                globo.luz = luz;
                globo.intensidadLuz = 6f;
            }
            else Debug.LogWarning($"[Globos] {modelos[c.modelo - 1].name} no tiene 'Fuego'.");

            var caja = new Bounds(go.transform.position, Vector3.zero);
            foreach (var rend in go.GetComponentsInChildren<Renderer>()) caja.Encapsulate(rend.bounds);
            Debug.Log($"[Globos] {go.name} ({modelos[c.modelo - 1].name}) en {go.transform.position:F0}, " +
                      $"tamaño real {caja.size:F1} m, fuego: {(fuego != null ? fuego.name : "NO")}");
        }
        return n;
    }
}
