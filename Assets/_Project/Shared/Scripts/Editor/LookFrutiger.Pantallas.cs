// Hub final, parte 6: las dos pantallas de los kioscos del lobby muestran galerías de imágenes Frutiger.
//   - Kiosco Aeropedia (oeste): Shared/Textures/Aeropedia  (Frutiger Family y frutiger 1 a 3).
//   - Kiosco Vestidor (este):   Shared/Textures/Vestidor   (frutiger 4 a 8).
//   Las imágenes se muestran en orden alfabético (por eso llevan 01_, 02_...).
//   - Sobre la cara frontal de cada pantalla (la que mira al centro del lobby) se pone un lienzo (quad URP Unlit)
//     que la cubre entera, y el componente PantallaGaleria (aviso "E  Ver", vista cercana y botones ‹ ›).
// Se regeneran con el resto del fondo (GenerarTodo) o con: Glossy Horizons > Hub > Pantallas de los kioscos.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static partial class LookFrutiger
{
    const string SpritesHUD = "Assets/_Project/Player/UI/Sprites/";

    // malla de la pantalla en el lobby, objeto que se crea, carpeta de imágenes y material del lienzo
    static readonly (string malla, string raiz, string carpeta, string material)[] Kioscos =
    {
        ("Kiosco_Aeropedia_Pantalla", "Pantalla_Aeropedia", "Assets/_Project/Shared/Textures/Aeropedia", "M_PantallaAeropedia"),
        ("Kiosco_Vestidor_Pantalla",  "Pantalla_Vestidor",  "Assets/_Project/Shared/Textures/Vestidor",  "M_PantallaVestidor"),
    };

    [MenuItem("Glossy Horizons/Hub/Pantallas de los kioscos")]
    static void MenuPantallas()
    {
        var escena = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        int n = PantallasKioscos(escena);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(escena);
        Debug.Log(n > 0 ? $"Pantallas listas con {n} imágenes en total. Dale Play, acércate a un kiosco y pulsa E." : "No se pudieron preparar las pantallas.");
    }

    static int PantallasKioscos(UnityEngine.SceneManagement.Scene escena)
    {
        int total = 0;
        foreach (var k in Kioscos) total += PantallaKiosco(escena, k.malla, k.raiz, k.carpeta, k.material);
        return total;
    }

    static int PantallaKiosco(UnityEngine.SceneManagement.Scene escena, string nombreMalla, string nombreRaiz, string carpeta, string nombreMaterial)
    {
        foreach (var go in escena.GetRootGameObjects()) if (go.name == nombreRaiz) Object.DestroyImmediate(go);

        Transform pantalla = null;
        foreach (var go in escena.GetRootGameObjects()) if ((pantalla = Buscar(go.transform, nombreMalla)) != null) break;
        if (pantalla == null) { Debug.LogWarning($"[Pantallas] No está {nombreMalla} en la escena (¿falta el lobby?)."); return 0; }
        var mf = pantalla.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) { Debug.LogWarning($"[Pantallas] {nombreMalla} no tiene malla."); return 0; }

        // --- cara frontal: la de más área que mira hacia el centro del lobby ---
        var malla = mf.sharedMesh;
        var v = malla.vertices.Select(p => pantalla.TransformPoint(p)).ToArray();
        var tris = malla.triangles;
        var centroPantalla = pantalla.GetComponent<Renderer>().bounds.center;
        var haciaLobby = new Vector3(-centroPantalla.x, 0f, -centroPantalla.z).normalized;
        var caras = new Dictionary<Vector3Int, (Vector3 normal, float area, List<Vector3> puntos)>();
        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 a = v[tris[i]], b = v[tris[i + 1]], c = v[tris[i + 2]];
            var cr = Vector3.Cross(b - a, c - a);
            if (cr.sqrMagnitude < 1e-10f) continue;
            var n = cr.normalized;
            var clave = Vector3Int.RoundToInt(n * 20f);
            if (!caras.TryGetValue(clave, out var cara)) cara = (n, 0f, new List<Vector3>());
            cara.area += cr.magnitude * 0.5f; cara.puntos.Add(a); cara.puntos.Add(b); cara.puntos.Add(c);
            caras[clave] = cara;
        }
        var frontal = caras.Values.Where(c => Vector3.Dot(c.normal, haciaLobby) > 0.2f).OrderByDescending(c => c.area).FirstOrDefault();
        if (frontal.puntos == null) { Debug.LogWarning($"[Pantallas] No encontré la cara frontal de {nombreMalla}."); return 0; }
        var normal = frontal.normal;
        var arriba = (Vector3.up - Vector3.Dot(Vector3.up, normal) * normal).normalized;
        var derecha = Vector3.Cross(arriba, normal);
        Vector3 centro = Vector3.zero; foreach (var p in frontal.puntos) centro += p; centro /= frontal.puntos.Count;
        float ancho = 0f, alto = 0f;
        foreach (var p in frontal.puntos)
        {
            ancho = Mathf.Max(ancho, Mathf.Abs(Vector3.Dot(p - centro, derecha)) * 2f);
            alto = Mathf.Max(alto, Mathf.Abs(Vector3.Dot(p - centro, arriba)) * 2f);
        }

        // --- imágenes ---
        var rutas = AssetDatabase.FindAssets("t:Texture2D", new[] { carpeta })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(r => r).ToArray();
        var imagenes = new List<Texture2D>();
        foreach (var ruta in rutas)
        {
            var imp = AssetImporter.GetAtPath(ruta) as TextureImporter;
            if (imp != null && (imp.wrapMode != TextureWrapMode.Clamp || imp.maxTextureSize != 2048 || !imp.mipmapEnabled))
            {
                imp.textureType = TextureImporterType.Default; imp.wrapMode = TextureWrapMode.Clamp;
                imp.maxTextureSize = 2048; imp.mipmapEnabled = true; imp.anisoLevel = 4;
                imp.npotScale = TextureImporterNPOTScale.None;
                imp.textureCompression = TextureImporterCompression.CompressedHQ;
                imp.SaveAndReimport();
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
            if (tex != null) imagenes.Add(tex);
        }
        if (imagenes.Count == 0) { Debug.LogWarning($"[Pantallas] No hay imágenes en {carpeta}."); return 0; }

        // --- lienzo sobre la pantalla ---
        var raiz = new GameObject(nombreRaiz);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(raiz, escena);
        raiz.transform.SetPositionAndRotation(centro + normal * 0.006f, Quaternion.LookRotation(-normal, arriba));
        var lienzo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        lienzo.name = "Lienzo";
        Object.DestroyImmediate(lienzo.GetComponent<Collider>());
        lienzo.transform.SetParent(raiz.transform, false);
        var rend = lienzo.GetComponent<MeshRenderer>();
        var mat = MaterialNuevo($"{CarpetaMateriales}/{nombreMaterial}.mat", "Universal Render Pipeline/Unlit");
        mat.SetTexture("_BaseMap", imagenes[0]); mat.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(mat);
        rend.sharedMaterial = mat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;

        var galeria = raiz.AddComponent<PantallaGaleria>();
        galeria.imagenes = imagenes.ToArray();
        galeria.lienzo = rend;
        galeria.tamanoPantalla = new Vector2(ancho * 0.995f, alto * 0.995f);   // toda la cara: sin borde
        galeria.orbe = AssetDatabase.LoadAssetAtPath<Sprite>(SpritesHUD + "T_BotonColor.png");
        galeria.brillo = AssetDatabase.LoadAssetAtPath<Sprite>(SpritesHUD + "T_BotonOrbeBrillo.png");
        galeria.cerrar = AssetDatabase.LoadAssetAtPath<Sprite>(SpritesHUD + "T_VistaCerrar.png");
        lienzo.transform.localScale = new Vector3(galeria.tamanoPantalla.x, galeria.tamanoPantalla.y, 1f);

        Debug.Log($"[Pantallas] {nombreRaiz}: {ancho:0.00} x {alto:0.00} m en {centro:F1}, mirando a {normal:F2}; {imagenes.Count} imágenes.");
        return imagenes.Count;
    }
}
