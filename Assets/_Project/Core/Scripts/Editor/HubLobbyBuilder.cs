using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace GlossyHorizons.Core.EditorTools
{
    /// <summary>
    /// Arma el lobby Frutiger Aero dentro de la escena Hub a partir de Lobby_Cupula.fbx.
    /// Menú: Glossy Horizons > Hub > Construir lobby Frutiger Aero.
    /// Se puede ejecutar varias veces: borra el lobby anterior y lo vuelve a colocar.
    /// Los materiales solo se crean si no existen, así que los ajustes hechos a mano se conservan.
    /// </summary>
    public static class HubLobbyBuilder
    {
        const string ModelPath = "Assets/_Project/Core/Models/Lobby_Cupula.fbx";
        const string ScenePath = "Assets/_Project/Core/Scenes/Hub.unity";
        const string MaterialsDir = "Assets/_Project/Core/Materials/Lobby";
        const string SettingsDir = "Assets/_Project/Core/Settings";
        const string VolumeProfilePath = SettingsDir + "/Hub_VolumeProfile.asset";
        const string SkyMaterialPath = MaterialsDir + "/Sky_FrutigerAero.mat";
        const string LobbyRootName = "Lobby_Cupula";
        const string ProbeName = "Lobby_ReflectionProbe";

        // Colores tomados del archivo de Blender (espacio lineal), brillo (Shininess de FBX),
        // emisión, opacidad, metálico y si se ve por ambas caras.
        struct Spec
        {
            public Color color; public float shininess, emission, alpha, metallic; public bool doubleSided;
            public Spec(float r, float g, float b, float sh, float e = 0f, float a = 1f, float m = 0f, bool ds = false)
            { color = new Color(r, g, b); shininess = sh; emission = e; alpha = a; metallic = m; doubleSided = ds; }
        }

        static readonly Dictionary<string, Spec> Palette = new Dictionary<string, Spec>
        {
            { "M_VidrioCupula",    new Spec(0.62f, 0.86f, 1.00f, 96, a: 0.18f, ds: true) },
            { "M_VidrioPuerta",    new Spec(0.52f, 0.79f, 0.96f, 96, a: 0.30f, ds: true) },
            { "M_Agua",            new Spec(0.01f, 0.33f, 0.75f, 94, e: 0.5f, a: 0.60f) },
            { "M_Chorros",         new Spec(0.35f, 0.79f, 1.00f, 100, e: 0.8f, a: 0.55f, ds: true) },
            { "M_Orbe",            new Spec(0.21f, 0.67f, 1.00f, 100, e: 1.2f, a: 0.65f) },
            { "M_Neon",            new Spec(0.02f, 0.69f, 1.00f, 49, e: 3f) },
            { "M_NeonAqua",        new Spec(0.02f, 0.48f, 0.81f, 49, e: 3f) },
            { "M_NeonVerde",       new Spec(0.33f, 0.75f, 0.11f, 49, e: 2.5f) },
            { "M_LineaPiso",       new Spec(0.16f, 0.48f, 0.70f, 49, e: 0.6f) },
            { "M_Pantalla",        new Spec(0.52f, 0.79f, 0.96f, 64, e: 0.8f) },
            { "M_TextoBlanco",     new Spec(1.00f, 1.00f, 1.00f, 49, e: 1.5f) },
            { "M_Cromo",           new Spec(0.58f, 0.67f, 0.75f, 72, m: 1f) },
            { "M_Nervaduras",      new Spec(0.81f, 0.89f, 0.94f, 56, m: 0.5f) },
            { "M_Candado",         new Spec(0.76f, 0.92f, 1.00f, 64, m: 0.6f) },
            { "M_MarcoBlanco",     new Spec(0.90f, 0.94f, 0.96f, 72) },
            { "M_PisoBlanco",      new Spec(0.85f, 0.91f, 0.95f, 77) },
            { "M_Camino",          new Spec(0.72f, 0.81f, 0.86f, 64) },
            { "M_Pasto",           new Spec(0.11f, 0.58f, 0.10f, 30) },
            { "M_Hojas",           new Spec(0.03f, 0.39f, 0.06f, 56) },
            { "M_Placa",           new Spec(0.00f, 0.03f, 0.07f, 64) },
            { "M_Portal_Aqua",     new Spec(0.05f, 0.46f, 0.91f, 36, e: 0.3f) },
            { "M_Portal_DarkAero", new Spec(0.01f, 0.02f, 0.09f, 36) },
            { "M_Portal_Evil",     new Spec(0.01f, 0.02f, 0.01f, 36) },
        };

        // Partes decorativas o atravesables: no llevan collider.
        static readonly string[] NoColliderKeywords =
            { "Agua", "Chorros", "Orbe", "Neon", "Linea_Piso", "Texto", "Pantalla", "Placa", "Arbusto" };

        // Si existe este archivo (Temp/ no se sube a git), el lobby se construye solo al recompilar.
        const string AutoRunMarker = "Temp/HubLobbyBuilder.run";
        static bool autoRun;

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
                catch (System.Exception e) { Debug.LogError("[HubLobbyBuilder] " + e); }
                finally { autoRun = false; }
            };
        }

        [MenuItem("Glossy Horizons/Hub/Construir lobby Frutiger Aero")]
        public static void Build()
        {
            if (autoRun)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).isDirty)
                    {
                        Debug.LogWarning("[HubLobbyBuilder] Hay una escena con cambios sin guardar; no se ejecutó automáticamente. Usa el menú.");
                        return;
                    }
            }

            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
            {
                EditorUtility.DisplayDialog("Lobby", "No se encontró el modelo en:\n" + ModelPath, "OK");
                return;
            }
            if (!autoRun && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(MaterialsDir);
            EnsureFolder(SettingsDir);

            ConfigureImporter(importer);

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var old in scene.GetRootGameObjects().Where(g => g.name == LobbyRootName || g.name == ProbeName))
                Object.DestroyImmediate(old);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var lobby = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            lobby.name = LobbyRootName;
            lobby.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var bounds = ComputeBounds(lobby);
            AddColliders(lobby);
            MarkStatic(lobby);
            AddZoneLights(lobby, bounds);
            SetupSunAndCamera(scene, lobby, bounds);
            SetupEnvironment(bounds);
            SetupVolume(scene);
            AddReflectionProbe(scene, bounds);
            if (HubGrassBuilder.AssetsAvailable) HubGrassBuilder.BuildInScene(scene, lobby);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"[HubLobbyBuilder] Lobby listo en {ScenePath}. Tamaño aprox.: {bounds.size}");
            if (!autoRun) EditorUtility.DisplayDialog("Lobby Frutiger Aero",
                "Lobby colocado en la escena Hub y guardado.\n\nMateriales: " + MaterialsDir, "OK");
        }

        // ---------- Importación ----------

        static void ConfigureImporter(ModelImporter importer)
        {
            importer.bakeAxisConversion = true;      // evita la rotación de -90° de Blender
            importer.importCameras = false;
            importer.importLights = false;           // las luces se crean en Unity con valores correctos
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;

            foreach (var kv in Palette)
            {
                var mat = GetOrCreateMaterial(kv.Key, kv.Value);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), mat);
            }
            importer.SaveAndReimport();
        }

        static Material GetOrCreateMaterial(string name, Spec spec)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            Color c = spec.color.gamma; // Blender guarda colores lineales
            mat.SetColor("_BaseColor", new Color(c.r, c.g, c.b, spec.alpha));
            mat.SetFloat("_Smoothness", Mathf.Clamp(Mathf.Sqrt(spec.shininess) / 10f, 0.3f, 0.98f));
            mat.SetFloat("_Metallic", spec.metallic);

            if (spec.emission > 0f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(c.r, c.g, c.b) * spec.emission);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            if (spec.alpha < 1f) MakeTransparent(mat);
            if (spec.doubleSided)
            {
                mat.SetFloat("_Cull", (float)CullMode.Off);
                mat.doubleSidedGI = true;
            }

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void MakeTransparent(Material mat)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_CastShadows", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetShaderPassEnabled("ShadowCaster", false);
            mat.renderQueue = (int)RenderQueue.Transparent;
        }

        // ---------- Escena ----------

        static void AddColliders(GameObject lobby)
        {
            foreach (var mf in lobby.GetComponentsInChildren<MeshFilter>(true))
            {
                string n = mf.gameObject.name;
                if (n.StartsWith("Portal_") && n.EndsWith("_Panel"))
                {
                    // Zona de entrada del portal (el script de portal la usará como trigger).
                    var box = mf.gameObject.AddComponent<BoxCollider>();
                    box.isTrigger = true;
                    continue;
                }
                if (NoColliderKeywords.Any(k => n.Contains(k))) continue;
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            }
        }

        static void MarkStatic(GameObject lobby)
        {
            const StaticEditorFlags flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic;
            foreach (var t in lobby.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("Puerta_")) continue; // las puertas se podrán animar
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
            }
        }

        static void AddZoneLights(GameObject lobby, Bounds bounds)
        {
            var zones = new (string light, string neon, Color fallback)[]
            {
                ("Luz_FrutigerAqua", "Portal_FrutigerAqua_Neon", new Color(0.2f, 0.85f, 1f)),
                ("Luz_DarkAero",     "Portal_DarkAero_Neon",     new Color(0.35f, 0.3f, 1f)),
                ("Luz_FrutigerEvil", "Portal_FrutigerEvil_Neon", new Color(0.5f, 1f, 0.3f)),
            };
            float range = Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.4f;
            foreach (var z in zones)
            {
                var t = FindDeep(lobby.transform, z.light);
                if (t == null) continue;
                var light = t.GetComponent<Light>();
                if (light == null) light = t.gameObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = NeonColor(lobby, z.neon) ?? z.fallback;
                light.intensity = 3f;
                light.range = Mathf.Max(range, 4f);
                light.shadows = LightShadows.None;
            }
        }

        static Color? NeonColor(GameObject lobby, string rendererName)
        {
            var t = FindDeep(lobby.transform, rendererName);
            var r = t != null ? t.GetComponent<Renderer>() : null;
            if (r == null || r.sharedMaterial == null || !r.sharedMaterial.HasProperty("_EmissionColor")) return null;
            var c = r.sharedMaterial.GetColor("_EmissionColor");
            float max = c.maxColorComponent;
            return max > 0f ? new Color(c.r / max, c.g / max, c.b / max) : (Color?)null;
        }

        static void SetupSunAndCamera(Scene scene, GameObject lobby, Bounds bounds)
        {
            var roots = scene.GetRootGameObjects();

            var sun = roots.SelectMany(g => g.GetComponentsInChildren<Light>(true))
                           .FirstOrDefault(l => l.type == LightType.Directional);
            if (sun != null)
            {
                sun.color = new Color(1f, 0.97f, 0.9f);
                sun.intensity = 1.3f;
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
                RenderSettings.sun = sun;
            }

            var cam = roots.SelectMany(g => g.GetComponentsInChildren<Camera>(true)).FirstOrDefault();
            if (cam == null) return;

            var spawn = FindDeep(lobby.transform, "SpawnPoint");
            var focus = FindDeep(lobby.transform, "Fuente");
            Vector3 target = (focus != null ? focus.position : bounds.center) + Vector3.up * 1.2f;
            Vector3 start = spawn != null ? spawn.position : bounds.center - Vector3.forward * bounds.extents.z * 0.8f;

            Vector3 dir = target - start; dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            cam.transform.position = start - dir.normalized * 3f + Vector3.up * 1.8f; // vista de tercera persona
            cam.transform.LookAt(target);
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, bounds.size.magnitude * 6f);
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }

        static void SetupEnvironment(Bounds bounds)
        {
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural")) { name = "Sky_FrutigerAero" };
                sky.SetFloat("_SunSize", 0.04f);
                sky.SetFloat("_SunSizeConvergence", 5f);
                sky.SetFloat("_AtmosphereThickness", 0.7f);
                sky.SetColor("_SkyTint", new Color(0.45f, 0.75f, 1f));
                sky.SetColor("_GroundColor", new Color(0.55f, 0.8f, 0.5f));
                sky.SetFloat("_Exposure", 1.35f);
                AssetDatabase.CreateAsset(sky, SkyMaterialPath);
            }

            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.1f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 1f;

            float size = Mathf.Max(bounds.extents.x, bounds.extents.z, 10f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.8f, 0.92f, 1f);
            RenderSettings.fogStartDistance = size * 2f;
            RenderSettings.fogEndDistance = size * 12f;

            DynamicGI.UpdateEnvironment();
        }

        static void SetupVolume(Scene scene)
        {
            var volume = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Volume>(true)).FirstOrDefault();
            if (volume == null) return;

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                string src = volume.sharedProfile != null ? AssetDatabase.GetAssetPath(volume.sharedProfile) : null;
                if (!string.IsNullOrEmpty(src) && AssetDatabase.CopyAsset(src, VolumeProfilePath))
                    profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
                else
                {
                    profile = ScriptableObject.CreateInstance<VolumeProfile>();
                    AssetDatabase.CreateAsset(profile, VolumeProfilePath);
                }
            }

            if (!profile.TryGet(out Bloom bloom))
            {
                bloom = profile.Add<Bloom>(true);
                bloom.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(bloom, profile);
            }
            bloom.active = true;
            bloom.intensity.Override(0.9f);
            bloom.threshold.Override(1f);
            bloom.scatter.Override(0.7f);
            bloom.tint.Override(new Color(0.85f, 0.95f, 1f));

            EditorUtility.SetDirty(bloom);
            EditorUtility.SetDirty(profile);
            volume.sharedProfile = profile;
        }

        static void AddReflectionProbe(Scene scene, Bounds bounds)
        {
            var go = new GameObject(ProbeName);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.position = bounds.center;
            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.size = bounds.size;
            probe.boxProjection = true;
        }

        // ---------- Utilidades ----------

        static Bounds ComputeBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.one * 20f);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        static Transform FindDeep(Transform parent, string name)
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
