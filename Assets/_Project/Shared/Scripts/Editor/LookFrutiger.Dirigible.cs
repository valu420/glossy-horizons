// Hub final, parte 5: el dirigible Glossy Horizons rodea la isla a lo lejos, por encima de la ciudad y de los
// globos, dejando estelas finas desde las puntas de sus aletas laterales (como la estela de un avión).
//   - Modelo: Shared/Models/Dirigible/Dirigible.fbx (exportado tal cual de zepeli_final.blend) y su logo.
//   - Materiales URP propios (Shared/Materials/Dirigible); el logo usa recorte por alfa.
//   - Componente DirigibleVuelo: órbita, alabeo y sube y baja. Las estelas son TrailRenderers.
// Se regenera con el resto del fondo (GenerarTodo) o con: Glossy Horizons > Hub > Dirigible.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static partial class LookFrutiger
{
    const string CarpetaDirigible = "Assets/_Project/Shared/Models/Dirigible";
    const string RaizDirigible = "Dirigible_GlossyHorizons";
    const float LargoDirigible = 50f;      // metros de punta a cola
    const float RadioDirigible = 145f;     // órbita alrededor de la isla, sobre el mar, cerca de la orilla
    const float AlturaDirigible = 140f;    // entre los globos bajos (≤125 m) y los de la ciudad (≥154 m en su base)

    [MenuItem("Glossy Horizons/Hub/Dirigible")]
    static void MenuDirigible()
    {
        var escena = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        int n = Dirigible(escena);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(escena);
        var raiz = System.Array.Find(escena.GetRootGameObjects(), g => g.name == RaizDirigible);
        if (raiz != null)
        {
            Selection.activeGameObject = raiz;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.Frame(new Bounds(raiz.transform.position, Vector3.one * 120f), false);
        }
        Debug.Log(n > 0 ? "Dirigible creado. Dale Play para verlo volar con sus estelas y guarda la escena (Ctrl+S)." : "No se pudo crear el dirigible.");
    }

    static Dictionary<string, Material> MaterialesDirigible()
    {
        string carpeta = CarpetaMateriales + "/Dirigible";
        if (!AssetDatabase.IsValidFolder(carpeta)) AssetDatabase.CreateFolder(CarpetaMateriales, "Dirigible");
        Material Lit(string nombre, string hex, float suave, float metal)
        {
            var m = MaterialNuevo($"{carpeta}/M_{nombre}.mat", "Universal Render Pipeline/Lit");
            if (m == null) return null;
            m.SetColor("_BaseColor", Hex(hex)); m.SetFloat("_Smoothness", suave); m.SetFloat("_Metallic", metal);
            m.SetFloat("_EnvironmentReflections", 1f); m.SetFloat("_SpecularHighlights", 1f);
            EditorUtility.SetDirty(m);
            return m;
        }
        // logo: textura con alfa, recortada (sin problemas de orden de transparencias)
        string rutaLogo = $"{CarpetaDirigible}/T_Dirigible_Logo.png";
        var imp = AssetImporter.GetAtPath(rutaLogo) as TextureImporter;
        if (imp != null && (!imp.alphaIsTransparency || imp.wrapMode != TextureWrapMode.Clamp))
        {
            imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp; imp.mipmapEnabled = true;
            imp.maxTextureSize = 2048; imp.SaveAndReimport();
        }
        var logo = Lit("Dirigible_logo", "#ffffff", 0.85f, 0f);
        logo.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(rutaLogo));
        logo.SetFloat("_AlphaClip", 1f); logo.SetFloat("_Cutoff", 0.35f); logo.EnableKeyword("_ALPHATEST_ON");
        logo.SetOverrideTag("RenderType", "TransparentCutout");
        logo.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        EditorUtility.SetDirty(logo);

        var vidrio = MaterialTransparente($"{carpeta}/M_Dirigible_vidrio.mat", null, true);
        vidrio.SetColor("_BaseColor", new Color(0.75f, 0.93f, 1f, 0.25f));
        EditorUtility.SetDirty(vidrio);

        return new Dictionary<string, Material>
        {
            ["Dirigible_cuerpo"] = Lit("Dirigible_cuerpo", "#f2f6fa", 0.85f, 0f),
            ["Dirigible_azul"] = Lit("Dirigible_azul", "#1e7fe0", 0.85f, 0f),
            ["Dirigible_lima"] = Lit("Dirigible_lima", "#8fe03a", 0.85f, 0f),
            ["Dirigible_aletas"] = Lit("Dirigible_aletas", "#1e7fe0", 0.85f, 0f),
            ["Dirigible_cabina"] = Lit("Dirigible_cabina", "#ffffff", 0.8f, 0f),
            ["Dirigible_marco"] = Lit("Dirigible_marco", "#c9d6e0", 0.8f, 1f),
            ["Dirigible_vidrio"] = vidrio,
            ["Dirigible_logo"] = logo,
        };
    }

    // estela: blanca y celeste, translúcida, con los bordes suaves (textura generada una vez)
    static Material MaterialEstela()
    {
        string carpeta = CarpetaMateriales + "/Dirigible";
        string rutaTex = $"{carpeta}/T_Estela.png";
        if (!File.Exists(rutaTex))
        {
            var t = new Texture2D(8, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
            {
                float v = (y + 0.5f) / 64f * 2f - 1f;              // -1..1 a lo ancho de la estela
                float a = Mathf.Exp(-v * v * 5f);                  // centro brillante, bordes que se desvanecen
                for (int x = 0; x < 8; x++) t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            File.WriteAllBytes(rutaTex, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(rutaTex);
            var ti = (TextureImporter)AssetImporter.GetAtPath(rutaTex);
            ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.mipmapEnabled = false;
            ti.SaveAndReimport();
        }
        var m = MaterialNuevo($"{carpeta}/M_Estela.mat", "Universal Render Pipeline/Particles/Unlit");
        if (m == null) return null;
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(rutaTex));
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f); m.SetFloat("_ZWrite", 0f); m.SetFloat("_Cull", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        return m;
    }

    static TrailRenderer Estela(string nombre, Transform padre, Vector3 posicion, Material mat)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, true);
        go.transform.position = posicion;
        var tr = go.AddComponent<TrailRenderer>();
        tr.sharedMaterial = mat;
        tr.time = 45f;                      // ~225 m de estela a 5 m/s
        tr.minVertexDistance = 2.5f;
        tr.widthMultiplier = 0.9f;
        tr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(0.08f, 1f), new Keyframe(1f, 2.2f));   // se abre al alejarse
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.82f, 0.95f, 1f), 0.5f), new GradientColorKey(new Color(0.7f, 0.9f, 1f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.03f), new GradientAlphaKey(0.45f, 0.55f), new GradientAlphaKey(0f, 1f) });
        tr.colorGradient = g;
        tr.textureMode = LineTextureMode.Stretch;
        tr.alignment = LineAlignment.View;
        tr.numCapVertices = 2;
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tr.receiveShadows = false;
        tr.emitting = true;
        return tr;
    }

    static int Dirigible(UnityEngine.SceneManagement.Scene escena)
    {
        foreach (var go in escena.GetRootGameObjects()) if (go.name == RaizDirigible) Object.DestroyImmediate(go);
        var modelo = CargarModelo($"{CarpetaDirigible}/Dirigible.fbx", MaterialesDirigible(), out _);
        if (modelo == null) return 0;
        var matEstela = MaterialEstela();

        var raiz = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
        raiz.name = RaizDirigible;
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(raiz, escena);
        raiz.transform.position = Vector3.zero;
        foreach (var r in raiz.GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // está muy alto: su sombra no se vería
            r.receiveShadows = false;
        }

        var cuerpo = Buscar(raiz.transform, "Cuerpo");
        var aletaIzq = Buscar(raiz.transform, "Aleta_izquierda");
        var aletaDer = Buscar(raiz.transform, "Aleta_derecha");
        if (cuerpo == null || aletaIzq == null || aletaDer == null)
        {
            Debug.LogError("[Dirigible] Al FBX le falta Cuerpo, Aleta_izquierda o Aleta_derecha.");
            Object.DestroyImmediate(raiz); return 0;
        }

        // frente del modelo: de las aletas (cola) hacia el centro del cuerpo
        var cajaCuerpo = cuerpo.GetComponent<Renderer>().bounds;
        var cola = (aletaIzq.GetComponent<Renderer>().bounds.center + aletaDer.GetComponent<Renderer>().bounds.center) * 0.5f;
        var frenteMundo = cajaCuerpo.center - cola; frenteMundo.y = 0f; frenteMundo.Normalize();
        float largo = Vector3.Scale(cajaCuerpo.size, new Vector3(Mathf.Abs(frenteMundo.x), 0f, Mathf.Abs(frenteMundo.z))).magnitude;
        raiz.transform.localScale *= LargoDirigible / Mathf.Max(largo, 1e-3f);

        // punta exterior de cada aleta lateral = el vértice más alejado del eje del dirigible
        Vector3 Punta(Transform aleta)
        {
            var mf = aleta.GetComponent<MeshFilter>();
            var centro = cuerpo.GetComponent<Renderer>().bounds.center;
            Vector3 mejor = aleta.position; float dMax = -1f;
            foreach (var v in mf.sharedMesh.vertices)
            {
                var w = aleta.TransformPoint(v);
                var rel = w - centro;
                float d = (rel - Vector3.Dot(rel, frenteMundo) * frenteMundo).sqrMagnitude;
                if (d > dMax) { dMax = d; mejor = w; }
            }
            return mejor;
        }
        float sep = 0.9f;   // separación entre las dos líneas de cada aleta (m)
        var estelas = new List<TrailRenderer>();
        foreach (var (aleta, lado) in new[] { (aletaIzq, "Izq"), (aletaDer, "Der") })
        {
            var p = Punta(aleta) - frenteMundo * 1.5f;
            estelas.Add(Estela($"Estela_{lado}_A", raiz.transform, p + Vector3.up * sep * 0.5f, matEstela));
            estelas.Add(Estela($"Estela_{lado}_B", raiz.transform, p - Vector3.up * sep * 0.5f, matEstela));
        }

        // colocarlo en su órbita (el componente lo mueve en Play)
        var vuelo = raiz.AddComponent<DirigibleVuelo>();
        vuelo.frenteLocal = raiz.transform.InverseTransformDirection(frenteMundo).normalized;
        vuelo.centro = CentroIsla; vuelo.radio = RadioDirigible; vuelo.altura = AlturaDirigible;
        vuelo.velocidad = 5f; vuelo.horario = false; vuelo.fase = 0.15f;
        vuelo.estelas = estelas.ToArray();
        float a0 = vuelo.fase * Mathf.PI * 2f;
        var tangente = new Vector3(-Mathf.Sin(a0), 0f, Mathf.Cos(a0));
        raiz.transform.rotation = Quaternion.FromToRotation(frenteMundo, tangente) * raiz.transform.rotation;
        raiz.transform.position = CentroIsla + new Vector3(Mathf.Cos(a0) * RadioDirigible, AlturaDirigible, Mathf.Sin(a0) * RadioDirigible);

        Debug.Log($"[Dirigible] largo {LargoDirigible} m, órbita r={RadioDirigible} m a {AlturaDirigible} m, {estelas.Count} estelas.");
        return 1;
    }
}
