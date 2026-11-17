using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace GlossyHorizons.Core.EditorTools
{
    /// <summary>
    /// Coloca el pasto Frutiger afuera de la cúpula en la escena Hub.
    /// Menú: Glossy Horizons > Hub > Colocar pasto exterior.
    /// El terreno es una malla normal; las briznas, matas y flores se dibujan con GrassField (GPU instancing).
    /// </summary>
    public static class HubGrassBuilder
    {
        const string ModelsDir = "Assets/_Project/Core/Models/Pasto";
        const string TerrainPath = ModelsDir + "/Pasto_Terreno.fbx";
        const string ProtosPath = ModelsDir + "/Pasto_Prototipos.fbx";
        const string TerrainTexPath = ModelsDir + "/Pasto_Terreno_Color.png";
        const string BladeTexPath = ModelsDir + "/Pasto_Hoja_Color.png";
        const string MaterialsDir = "Assets/_Project/Core/Materials/Pasto";
        const string ScenePath = "Assets/_Project/Core/Scenes/Hub.unity";
        const string RootName = "Pasto_Exterior";
        const string AutoRunMarker = "Temp/HubGrassBuilder.run";

        static bool autoRun;

        public static bool AssetsAvailable => AssetDatabase.LoadAssetAtPath<GameObject>(TerrainPath) != null
                                           && AssetDatabase.LoadAssetAtPath<GameObject>(ProtosPath) != null;

        [InitializeOnLoadMethod]
        static void AutoRunIfRequested()
        {
            if (!System.IO.File.Exists(AutoRunMarker)) return;
            EditorApplication.delayCall += () =>
            {
                if (!System.IO.File.Exists(AutoRunMarker)) return;
                System.IO.File.Delete(AutoRunMarker);
                autoRun = true;
                try { Build(); }
                catch (System.Exception e) { Debug.LogError("[HubGrassBuilder] " + e); }
                finally { autoRun = false; }
            };
        }

        [MenuItem("Glossy Horizons/Hub/Colocar pasto exterior")]
        public static void Build()
        {
            if (!AssetsAvailable)
            {
                Debug.LogError("[HubGrassBuilder] Faltan los modelos de pasto en " + ModelsDir);
                return;
            }
            if (autoRun)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).isDirty)
                    {
                        Debug.LogWarning("[HubGrassBuilder] Hay una escena con cambios sin guardar; usa el menú.");
                        return;
                    }
            }
            else if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var lobby = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Lobby_Cupula");
            BuildInScene(scene, lobby);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Coloca el pasto en una escena ya abierta (también lo llama HubLobbyBuilder).</summary>
        public static void BuildInScene(Scene scene, GameObject lobby)
        {
            ConfigureImports();
            var mats = CreateMaterials();
            RemapMaterials(TerrainPath, mats, readable: true);
            RemapMaterials(ProtosPath, mats, readable: false);

            foreach (var old in scene.GetRootGameObjects().Where(g => g.name == RootName))
                Object.DestroyImmediate(old);

            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = Vector3.zero;

            var terrain = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TerrainPath), scene);
            terrain.transform.SetParent(root.transform, false);
            var groundFilter = terrain.GetComponentInChildren<MeshFilter>();
            groundFilter.gameObject.AddComponent<MeshCollider>().sharedMesh = groundFilter.sharedMesh;
            GameObjectUtility.SetStaticEditorFlags(groundFilter.gameObject,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);

            var grass = root.AddComponent<GrassField>();
            grass.ground = groundFilter;

            var protos = AssetDatabase.LoadAssetAtPath<GameObject>(ProtosPath);
            grass.layers = new[]
            {
                MakeLayer(protos, "Pasto_Hoja", 8f, new Vector2(0.8f, 1.4f)),
                MakeLayer(protos, "Pasto_Mata", 1.5f, new Vector2(0.8f, 1.2f)),
                MakeLayer(protos, "Pasto_Flor", 0.35f, new Vector2(0.9f, 1.2f)),
            }.Where(l => l != null).ToArray();

            if (lobby != null)
            {
                grass.domeCenter = lobby.transform.position;
                var glass = Find(lobby.transform, "Cupula_Vidrio");
                var glassRenderer = glass != null ? glass.GetComponent<Renderer>() : null;
                if (glassRenderer != null) grass.innerRadius = glassRenderer.bounds.extents.x + 0.3f;

                // Caminos, marcos, arcos y puertas de salida: sin pasto encima.
                string[] prefixes = { "Camino_", "Marco_", "Vidrio_Arco_", "Puerta_" };
                grass.exclusions = lobby.GetComponentsInChildren<Renderer>(true)
                    .Where(r => prefixes.Any(p => r.name.StartsWith(p)))
                    .Select(r => { var b = r.bounds; b.Expand(0.8f); return b; })
                    .ToArray();

                // El terreno nuevo reemplaza la plataforma verde del lobby (se deja su collider).
                var platform = Find(lobby.transform, "Plataforma_Pasto");
                var platformRenderer = platform != null ? platform.GetComponent<Renderer>() : null;
                if (platformRenderer != null) platformRenderer.enabled = false;
            }

            grass.Generate();
            Debug.Log($"[HubGrassBuilder] Pasto exterior listo: {grass.InstanceCount} instancias, radio interior {grass.innerRadius:0.0}.");
        }

        static GrassField.Layer MakeLayer(GameObject protos, string child, float density, Vector2 scale)
        {
            var t = Find(protos.transform, child);
            if (t == null) { Debug.LogWarning("[HubGrassBuilder] No se encontró " + child); return null; }
            var mf = t.GetComponent<MeshFilter>();
            var mr = t.GetComponent<MeshRenderer>();
            return new GrassField.Layer
            {
                name = child.Replace("Pasto_", ""),
                mesh = mf.sharedMesh,
                materials = mr.sharedMaterials,
                density = density,
                scaleRange = scale,
                castShadows = false,
            };
        }

        // ---------- Importación y materiales ----------

        static void ConfigureImports()
        {
            foreach (var path in new[] { TerrainPath, TerrainTexPath, BladeTexPath })
            {
                if (AssetImporter.GetAtPath(path) is TextureImporter ti && ti.wrapMode != TextureWrapMode.Clamp)
                {
                    ti.wrapMode = TextureWrapMode.Clamp;
                    ti.maxTextureSize = 1024;
                    ti.SaveAndReimport();
                }
            }
        }

        static void RemapMaterials(string modelPath, Dictionary<string, Material> mats, bool readable)
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            mi.bakeAxisConversion = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.isReadable = readable;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
            foreach (var kv in mats)
                mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
            mi.SaveAndReimport();
        }

        static Dictionary<string, Material> CreateMaterials()
        {
            EnsureFolder(MaterialsDir);
            var terrainTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TerrainTexPath);
            var bladeTex = AssetDatabase.LoadAssetAtPath<Texture2D>(BladeTexPath);
            // Colores de las rampas de Blender (lineales).
            var midGreen = new Color(0.028f, 0.479f, 0.037f);
            return new Dictionary<string, Material>
            {
                { "M_PastoFrutiger", Mat("M_PastoTerreno", Color.white, 0.55f, terrainTex, doubleSided: false) },
                { "M_PastoHoja",     Mat("M_PastoHoja", Color.white, 0.72f, bladeTex) },
                { "M_PastoMata",     Mat("M_PastoMata", midGreen, 0.72f) },
                { "M_FlorBlanca",    Mat("M_FlorBlanca", Color.white, 0.7f) },
                { "M_FlorCentro",    Mat("M_FlorCentro", new Color(1f, 0.64f, 0.05f), 0.75f) },
                { "M_FlorTallo",     Mat("M_FlorTallo", midGreen, 0.6f) },
            };
        }

        static Material Mat(string name, Color linearColor, float smoothness, Texture2D tex = null, bool doubleSided = true)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            mat.SetColor("_BaseColor", linearColor == Color.white ? Color.white : linearColor.gamma);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0f);
            if (tex != null) mat.SetTexture("_BaseMap", tex);
            if (doubleSided)
            {
                mat.SetFloat("_Cull", (float)CullMode.Off);
                mat.doubleSidedGI = true;
            }
            mat.enableInstancing = true;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // ---------- Utilidades ----------

        static Transform Find(Transform parent, string name)
        {
            foreach (var t in parent.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
