using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace GlossyHorizons.EditorTools
{
    /// <summary>
    /// Aplica un cielo panoramico (equirectangular) a la escena Hub.
    /// No toca Sky_FrutigerAero.mat: crea su propio material.
    /// Menu: Glossy Horizons > Hub > Aplicar cielo Atardecer Dorado
    /// </summary>
    public static class HubSkyApplier
    {
        const string HubScenePath = "Assets/_Project/Core/Scenes/Hub.unity";
        const string TexturePath  = "Assets/_Project/Core/Textures/Sky/Cielo_Atardecer_Dorado.png";
        const string MaterialPath = "Assets/_Project/Core/Materials/Lobby/Sky_AtardecerDorado.mat";

        [MenuItem("Glossy Horizons/Hub/Aplicar cielo Atardecer Dorado")]
        public static void Apply()
        {
            // 1. Ajustes de importacion de la textura
            var importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("[HubSky] No encuentro la textura: " + TexturePath);
                return;
            }
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);

            // 2. Material skybox panoramico
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Skybox/Panoramic")) { name = "Sky_AtardecerDorado" };
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            mat.SetTexture("_MainTex", tex);
            mat.SetFloat("_Mapping", 1f);        // Latitude Longitude Layout
            mat.SetFloat("_ImageType", 0f);      // 360 grados
            mat.SetFloat("_Exposure", 0.6f);
            mat.SetFloat("_Rotation", 0f);
            mat.SetColor("_Tint", Color.white);
            EditorUtility.SetDirty(mat);

            // 3. Escena Hub
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != HubScenePath)
                scene = EditorSceneManager.OpenScene(HubScenePath, OpenSceneMode.Single);

            RenderSettings.skybox = mat;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            // Menos brillo: el cielo ya es muy luminoso
            RenderSettings.ambientIntensity = 0.55f;
            // Niebla durazno suave, mas lejos para no lavar el lobby
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.95f, 0.78f, 0.58f);
            RenderSettings.fogStartDistance = 140f;
            RenderSettings.fogEndDistance = 700f;
            DynamicGI.UpdateEnvironment();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[HubSky] Cielo Atardecer Dorado aplicado a Hub.");
        }
    }
}
