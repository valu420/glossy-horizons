using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Prepara el cielo Frutiger automáticamente en el editor:
///   - crea Resources/Sky_FrutigerNubes.mat (shader Glossy/CieloFrutiger + T_NubesFrutiger) si falta,
///   - deja la textura de nubes bien importada (4096, sin mipmaps, repetida en horizontal),
///   - al abrir el Hub, si no tiene el componente CieloFrutiger, lo agrega (luego solo hay que guardar).
/// La textura de nubes sale de Herramientas/GeneradorCielo/gradear_cielo.py.
/// </summary>
[InitializeOnLoad]
public static class CieloFrutigerEditor
{
    const string RutaShader = "Glossy/CieloFrutiger";
    const string RutaTextura = "Assets/_Project/Shared/Textures/Sky/T_NubesFrutiger.png";
    const string CarpetaRecursos = "Assets/_Project/Shared/Resources";
    const string RutaMaterial = CarpetaRecursos + "/" + CieloFrutiger.RutaRecurso + ".mat";

    static CieloFrutigerEditor()
    {
        EditorApplication.delayCall += () =>
        {
            AsegurarMaterial();
            AsegurarEnEscena(EditorSceneManager.GetActiveScene());
        };
        EditorSceneManager.sceneOpened += (escena, modo) => AsegurarEnEscena(escena);
    }

    static void AsegurarEnEscena(UnityEngine.SceneManagement.Scene escena)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !escena.IsValid() || !escena.isLoaded) return;
        bool tenia = false;
        foreach (var raiz in escena.GetRootGameObjects())
            if (raiz.GetComponentInChildren<CieloFrutiger>(true) != null) { tenia = true; break; }
        if (tenia) return;
        var mat = AsegurarMaterial(); // antes de crear el componente: su OnEnable lo carga de Resources
        var cielo = CieloFrutiger.Asegurar(escena);
        if (cielo == null) return;
        cielo.cielo = mat;
        cielo.Aplicar();
        EditorSceneManager.MarkSceneDirty(escena);
        Debug.Log($"[CieloFrutiger] Cielo Frutiger agregado a '{escena.name}'. Guarda la escena (Ctrl+S).");
    }

    public static Material AsegurarMaterial()
    {
        ImportarTextura();
        var mat = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterial);
        if (mat != null) return mat;
        var shader = Shader.Find(RutaShader);
        if (shader == null) { Debug.LogWarning("[CieloFrutiger] No encuentro el shader " + RutaShader); return null; }
        if (!AssetDatabase.IsValidFolder(CarpetaRecursos))
            AssetDatabase.CreateFolder("Assets/_Project/Shared", "Resources");
        mat = new Material(shader) { name = CieloFrutiger.RutaRecurso };
        mat.SetTexture("_Nubes", AssetDatabase.LoadAssetAtPath<Texture2D>(RutaTextura));
        AssetDatabase.CreateAsset(mat, RutaMaterial);
        AssetDatabase.SaveAssets();
        return mat;
    }

    static void ImportarTextura()
    {
        var imp = AssetImporter.GetAtPath(RutaTextura) as TextureImporter;
        if (imp == null) return;
        if (imp.maxTextureSize == 4096 && !imp.mipmapEnabled && imp.wrapModeU == TextureWrapMode.Repeat
            && imp.alphaIsTransparency && imp.textureCompression == TextureImporterCompression.CompressedHQ) return;
        imp.textureType = TextureImporterType.Default;
        imp.sRGBTexture = true;
        imp.alphaSource = TextureImporterAlphaSource.FromInput;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.wrapModeU = TextureWrapMode.Repeat;
        imp.wrapModeV = TextureWrapMode.Clamp;
        imp.filterMode = FilterMode.Bilinear;
        imp.maxTextureSize = 4096;
        imp.textureCompression = TextureImporterCompression.CompressedHQ;
        imp.SaveAndReimport();
    }
}
