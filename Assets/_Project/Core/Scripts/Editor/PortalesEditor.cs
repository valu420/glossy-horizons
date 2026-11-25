using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GlossyHorizons.Core.EditorTools
{
    /// <summary>
    /// Portales entre escenas.
    ///  - "Configurar portales del Hub": pone PortalZona + trigger en los 3 paneles del lobby.
    ///  - "Crear escenas base de zonas": arma Zone_Aqua / Zone_DarkAero / Zone_Evil con jugador,
    ///    suelo, ambiente propio y un portal de regreso al Hub (solo si la escena está vacía,
    ///    o si se confirma el reemplazo).
    /// </summary>
    public static class PortalesEditor
    {
        const string Raiz = "Assets/_Project";
        const string CarpetaMat = Raiz + "/Core/Materials/Portales";
        const string PrefabJugador = Raiz + "/Player/Prefabs/Player_Gel.prefab";

        class Zona
        {
            public string panel, id, escena, titulo;
            public bool bloqueado;
            public Color cielo, niebla, luz, suelo, neon;
            public string ruta;
        }

        static readonly Zona[] Zonas =
        {
            new Zona { panel = "Portal_FrutigerAqua_Panel", id = "Aqua", escena = "Zone_Aqua", titulo = "Frutiger Aqua",
                       ruta = Raiz + "/Zones/Aqua/Scenes/Zone_Aqua.unity",
                       cielo = new Color(0.45f, 0.78f, 1f), niebla = new Color(0.7f, 0.9f, 1f), luz = new Color(1f, 0.97f, 0.9f),
                       suelo = new Color(0.85f, 0.95f, 1f), neon = new Color(0.2f, 0.85f, 1f) },
            new Zona { panel = "Portal_DarkAero_Panel", id = "DarkAero", escena = "Zone_DarkAero", titulo = "Dark Aero",
                       ruta = Raiz + "/Zones/DarkAero/Scenes/Zone_DarkAero.unity",
                       cielo = new Color(0.02f, 0.03f, 0.12f), niebla = new Color(0.03f, 0.04f, 0.16f), luz = new Color(0.45f, 0.5f, 1f),
                       suelo = new Color(0.07f, 0.09f, 0.24f), neon = new Color(0.35f, 0.3f, 1f) },
            new Zona { panel = "Portal_FrutigerEvil_Panel", id = "Evil", escena = "Zone_Evil", titulo = "Frutiger Evil",
                       ruta = Raiz + "/Zones/Evil/Scenes/Zone_Evil.unity",
                       cielo = new Color(0.02f, 0.06f, 0.02f), niebla = new Color(0.05f, 0.12f, 0.04f), luz = new Color(0.6f, 1f, 0.4f),
                       suelo = new Color(0.06f, 0.12f, 0.05f), neon = new Color(0.5f, 1f, 0.3f) },
        };

        // ---------------------------------------------------------------- Hub

        [MenuItem("Glossy Horizons/Portales/Configurar portales del Hub")]
        static void ConfigurarHub()
        {
            var escena = EditorSceneManager.GetActiveScene();
            if (escena.name != "Hub") { Debug.LogWarning("[Portales] Abre la escena Hub primero."); return; }
            int n = 0;
            foreach (var z in Zonas)
            {
                Transform panel = null;
                foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                    if (t.name == z.panel && t.gameObject.scene == escena) { panel = t; break; }
                if (panel == null) { Debug.LogWarning($"[Portales] No encontré {z.panel}."); continue; }

                foreach (var c in panel.GetComponents<Collider>()) Undo.DestroyObjectImmediate(c);
                var box = Undo.AddComponent<BoxCollider>(panel.gameObject);
                box.isTrigger = true;
                if (panel.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null)
                {
                    box.center = mf.sharedMesh.bounds.center;
                    box.size = mf.sharedMesh.bounds.size;
                    // el panel es casi plano: se engorda su eje más delgado para que no se pueda saltar
                    var s = panel.lossyScale;
                    int fino = 0; float menor = float.MaxValue;
                    for (int i = 0; i < 3; i++)
                    {
                        float mundo = box.size[i] * Mathf.Abs(s[i]);
                        if (mundo < menor) { menor = mundo; fino = i; }
                    }
                    var tam = box.size;
                    tam[fino] = Mathf.Max(tam[fino], 1.2f / Mathf.Max(0.0001f, Mathf.Abs(s[fino])));
                    box.size = tam;
                }
                var portal = panel.GetComponent<PortalZona>();
                if (portal == null) portal = Undo.AddComponent<PortalZona>(panel.gameObject);
                portal.escenaDestino = z.escena; portal.idPortal = z.id; portal.bloqueado = z.bloqueado;
                EditorUtility.SetDirty(portal);
                n++;
            }
            // Frutiger Evil ya está abierto: se oculta el candado del portal
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                if (t.name == "Portal_FrutigerEvil_Candado" && t.gameObject.scene == escena && t.gameObject.activeSelf)
                { Undo.RecordObject(t.gameObject, "Quitar candado"); t.gameObject.SetActive(false); }
            EditorSceneManager.MarkSceneDirty(escena);
            Debug.Log($"[Portales] {n} portales configurados en el Hub (los tres abiertos). Guarda la escena (Ctrl+S).");
        }

        // ---------------------------------------------------------------- zonas

        [MenuItem("Glossy Horizons/Portales/Crear escenas base de zonas")]
        static void CrearZonas()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string abierta = SceneManager.GetActiveScene().path;
            foreach (var z in Zonas)
            {
                if (File.Exists(z.ruta) && !EsVacia(z.ruta))
                {
                    if (!EditorUtility.DisplayDialog("Zona con contenido",
                        $"{z.escena} ya tiene objetos propios. ¿Reemplazarla por la escena base?", "Reemplazar", "Saltar")) continue;
                }
                ConstruirZona(z);
            }
            if (!string.IsNullOrEmpty(abierta)) EditorSceneManager.OpenScene(abierta);
            AsegurarBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[Portales] Escenas de zona creadas. Prueba: abre Hub y dale Play.");
        }

        static bool EsVacia(string ruta)
        {
            var e = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);
            bool vacia = true;
            foreach (var go in e.GetRootGameObjects())
                if (go.name != "Main Camera" && go.name != "Directional Light" && go.name != "Global Volume") vacia = false;
            return vacia;
        }

        static void ConstruirZona(Zona z)
        {
            var e = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Ambiente
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = z.niebla; RenderSettings.fogStartDistance = 60f; RenderSettings.fogEndDistance = 300f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(z.cielo, Color.white, 0.35f);

            var luz = new GameObject("Directional Light").AddComponent<Light>();
            luz.type = LightType.Directional; luz.color = z.luz; luz.intensity = 1.1f;
            luz.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = z.cielo; cam.farClipPlane = 400f;
            camGO.AddComponent<AudioListener>();
            camGO.transform.position = new Vector3(0, 3, -6);

            // Suelo
            var mSuelo = Mat("Zona_" + z.id + "_Suelo", z.suelo, 0.65f, Color.black);
            var mNeon = Mat("Zona_" + z.id + "_Neon", z.neon * 0.4f, 0.4f, z.neon * 2.2f);
            var suelo = GameObject.CreatePrimitive(PrimitiveType.Cylinder); suelo.name = "Suelo";
            suelo.transform.localScale = new Vector3(120f, 0.5f, 120f); suelo.transform.position = new Vector3(0, -0.5f, 0);
            suelo.GetComponent<Renderer>().sharedMaterial = mSuelo;
            // el collider de cilindro de Unity es una cápsula (con este tamaño sería una esfera enorme): se usa la malla real
            Object.DestroyImmediate(suelo.GetComponent<Collider>());
            suelo.AddComponent<MeshCollider>().sharedMesh = suelo.GetComponent<MeshFilter>().sharedMesh;
            var aro = GameObject.CreatePrimitive(PrimitiveType.Cylinder); aro.name = "Aro_Neon";
            aro.transform.localScale = new Vector3(120.6f, 0.45f, 120.6f); aro.transform.position = new Vector3(0, -0.52f, 0);
            aro.GetComponent<Renderer>().sharedMaterial = mNeon; Object.DestroyImmediate(aro.GetComponent<Collider>());

            // Anillos de neón y camino al portal (para leer el espacio y no ver un vacío)
            foreach (float d in new[] { 20f, 44f, 80f })
            {
                var borde = GameObject.CreatePrimitive(PrimitiveType.Cylinder); borde.name = "Anillo_Neon";
                borde.transform.localScale = new Vector3(d, 0.005f, d); borde.transform.position = new Vector3(0, 0.004f, 0);
                borde.GetComponent<Renderer>().sharedMaterial = mNeon; Object.DestroyImmediate(borde.GetComponent<Collider>());
                var centro = GameObject.CreatePrimitive(PrimitiveType.Cylinder); centro.name = "Anillo_Relleno";
                centro.transform.localScale = new Vector3(d - 0.5f, 0.005f, d - 0.5f); centro.transform.position = new Vector3(0, 0.008f, 0);
                centro.GetComponent<Renderer>().sharedMaterial = mSuelo; Object.DestroyImmediate(centro.GetComponent<Collider>());
            }
            var camino = GameObject.CreatePrimitive(PrimitiveType.Cube); camino.name = "Camino_Neon";
            camino.transform.localScale = new Vector3(0.35f, 0.02f, 16f); camino.transform.position = new Vector3(0f, 0.02f, 8f);
            camino.GetComponent<Renderer>().sharedMaterial = mNeon; Object.DestroyImmediate(camino.GetComponent<Collider>());

            // Aparición y jugador
            var ap = new GameObject("PuntoAparicion"); ap.AddComponent<PuntoAparicion>();
            ap.transform.position = new Vector3(0f, 0.1f, 0f); ap.transform.rotation = Quaternion.identity;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabJugador);
            if (prefab != null)
            {
                var jugador = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                jugador.transform.position = ap.transform.position;
            }
            else Debug.LogWarning("[Portales] No encontré Player_Gel.prefab: la zona queda sin jugador.");

            // Portal de regreso
            ConstruirPortalRegreso(z, new Vector3(0f, 0f, 16f));

            // Cartel
            var txt = new GameObject("Cartel").AddComponent<TextMesh>();
            txt.text = z.titulo + "\n(en construcción)"; txt.anchor = TextAnchor.MiddleCenter; txt.alignment = TextAlignment.Center;
            txt.characterSize = 0.35f; txt.fontSize = 48; txt.color = z.neon;
            txt.transform.position = new Vector3(0f, 6f, 24f); txt.transform.rotation = Quaternion.identity;

            EditorSceneManager.SaveScene(e, z.ruta);
        }

        static void ConstruirPortalRegreso(Zona z, Vector3 pos)
        {
            var raiz = new GameObject("Portal_Volver_Hub");
            raiz.transform.position = pos;
            var mMarco = Mat("Portal_Marco_" + z.id, new Color(0.85f, 0.88f, 0.95f), 0.8f, Color.black);
            var mNeon = Mat("Portal_Neon_" + z.id, z.neon * 0.4f, 0.4f, z.neon * 2.5f);
            var mPanel = Mat("Portal_Panel_" + z.id, z.neon * 0.5f, 0.9f, z.neon * 1.2f);

            GameObject Cubo(string n, Vector3 p, Vector3 s, Material m)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = n;
                g.transform.SetParent(raiz.transform, false); g.transform.localPosition = p; g.transform.localScale = s;
                g.GetComponent<Renderer>().sharedMaterial = m; return g;
            }
            Cubo("Pilar_Izq", new Vector3(-2.2f, 2.2f, 0), new Vector3(0.8f, 4.4f, 1f), mMarco);
            Cubo("Pilar_Der", new Vector3(2.2f, 2.2f, 0), new Vector3(0.8f, 4.4f, 1f), mMarco);
            Cubo("Dintel", new Vector3(0, 4.6f, 0), new Vector3(5.2f, 0.8f, 1f), mMarco);
            Cubo("Neon_Izq", new Vector3(-1.75f, 2.1f, -0.52f), new Vector3(0.1f, 4.2f, 0.05f), mNeon);
            Cubo("Neon_Der", new Vector3(1.75f, 2.1f, -0.52f), new Vector3(0.1f, 4.2f, 0.05f), mNeon);
            Cubo("Neon_Sup", new Vector3(0, 4.2f, -0.52f), new Vector3(3.6f, 0.1f, 0.05f), mNeon);

            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube); panel.name = "Panel";
            panel.transform.SetParent(raiz.transform, false);
            panel.transform.localPosition = new Vector3(0, 2.1f, 0); panel.transform.localScale = new Vector3(3.4f, 4.2f, 0.1f);
            panel.GetComponent<Renderer>().sharedMaterial = mPanel;
            Object.DestroyImmediate(panel.GetComponent<Collider>());
            var box = panel.AddComponent<BoxCollider>(); box.isTrigger = true;
            box.size = new Vector3(1f, 1f, 12f);   // grosor de ~1.2 m
            var p = panel.AddComponent<PortalZona>();
            p.escenaDestino = TransicionEscenasHub; p.idPortal = z.id;

            var t = new GameObject("Texto").AddComponent<TextMesh>();
            t.transform.SetParent(raiz.transform, false); t.transform.localPosition = new Vector3(0, 5.6f, -0.6f);
            t.text = "Volver al Lobby"; t.anchor = TextAnchor.MiddleCenter; t.characterSize = 0.18f; t.fontSize = 48; t.color = z.neon;
            // el cubo mira hacia -Z; el portal está en +Z mirando al origen
        }

        const string TransicionEscenasHub = "Hub";

        static Material Mat(string nombre, Color baseColor, float suavidad, Color emision)
        {
            if (!AssetDatabase.IsValidFolder(CarpetaMat))
            {
                if (!AssetDatabase.IsValidFolder(Raiz + "/Core/Materials")) AssetDatabase.CreateFolder(Raiz + "/Core", "Materials");
                AssetDatabase.CreateFolder(Raiz + "/Core/Materials", "Portales");
            }
            string ruta = $"{CarpetaMat}/{nombre}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, ruta);
            }
            m.SetColor("_BaseColor", baseColor); m.SetFloat("_Smoothness", suavidad);
            if (emision.maxColorComponent > 0.001f) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emision); }
            EditorUtility.SetDirty(m);
            return m;
        }

        static void AsegurarBuildSettings()
        {
            var lista = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var ruta in new[] { Raiz + "/Core/Scenes/Hub.unity", Zonas[0].ruta, Zonas[1].ruta, Zonas[2].ruta })
                if (!lista.Exists(s => s.path == ruta)) lista.Add(new EditorBuildSettingsScene(ruta, true));
            EditorBuildSettings.scenes = lista.ToArray();
        }
    }
}
