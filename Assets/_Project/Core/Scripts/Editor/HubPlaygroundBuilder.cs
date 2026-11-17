using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GlossyHorizons.Core.EditorTools
{
    /// <summary>
    /// Crea la zona de juegos al norte (+Z) de la cúpula del Hub, fuera del domo.
    /// Dos caminos salen de las puertas Este y Oeste, rodean la cúpula y llegan a la plaza.
    /// Todo son primitivas de Unity (sin modelos nuevos). Menú: Glossy Horizons > Hub > Crear zona de juegos (norte).
    /// Es idempotente: si ya existe "Zona_Juegos", la reconstruye.
    /// </summary>
    public static class HubPlaygroundBuilder
    {
        const string ScenePath = "Assets/_Project/Core/Scenes/Hub.unity";
        const string LobbyMats = "Assets/_Project/Core/Materials/Lobby";
        const string PlayMatsDir = "Assets/_Project/Core/Materials/Juegos";
        const string RootName = "Zona_Juegos";

        // Plaza redonda al norte de la cúpula (distancia desde el centro del lobby)
        const float PlazaZ = 70f;
        const float PlazaR = 24f;

        static Material mWhite, mAqua, mGlass, mPath, mOrb, mFrame, mLeaf, mChrome;
        static Material mPink, mYellow, mBlue, mGreen, mGrass;
        static Material mBush1, mBush2, mBush3, mFlowerW;

        [MenuItem("Glossy Horizons/Hub/Crear zona de juegos (norte)")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            foreach (var old in scene.GetRootGameObjects().Where(g => g.name == RootName))
                Object.DestroyImmediate(old);

            var lobby = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Lobby_Cupula");
            Vector3 origin = lobby != null ? lobby.transform.position : Vector3.zero;
            origin.y = 0f;

            LoadMaterials();
            DumpDiagnostics(lobby, origin);

            var root = new GameObject(RootName);
            root.transform.position = origin;

            string terrainInfo;
            float islandR;
            float groundTop = MeasureGrassTop(scene, origin, out terrainInfo, out islandR);
            System.IO.File.WriteAllText("Temp/terrain_info.txt", terrainInfo);
            Debug.Log("[HubPlayground] " + terrainInfo);
            BuildGround(root.transform, groundTop);
            BuildSkirt(root.transform, origin, islandR, groundTop);

            var plaza = new GameObject("Plaza").transform;
            plaza.SetParent(root.transform, false);
            plaza.localPosition = new Vector3(0f, 0f, PlazaZ);

            BuildPlazaBase(plaza);
            BuildEntrance(plaza);
            BuildCenterOrb(plaza);
            BuildSlide(plaza, new Vector3(-9f, 0f, 6f));
            BuildSwings(plaza, new Vector3(10f, 0f, 6f));
            BuildTrampolines(plaza);
            BuildSeesaw(plaza, new Vector3(4f, 0f, -6f));
            BuildBubbles(plaza);
            BuildBenchesAndLamps(plaza);
            BuildPlanters(plaza);
            var pathPts = new List<Vector3>();
            var pathBounds = new List<Bounds>();
            BuildRingPath(root.transform, pathBounds);
            BuildPaths(root.transform, lobby, origin, groundTop, pathPts, pathBounds);
            BuildBushes(root.transform, origin);
            var pastoExterior = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Pasto_Exterior");
            ExcludeFromIslandGrass(pastoExterior, pathBounds);
            BuildGrass(root.transform, pastoExterior, origin, islandR, groundTop, pathPts, pathBounds);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            Debug.Log($"[HubPlayground] Zona de juegos creada: {root.GetComponentsInChildren<Transform>().Length - 1} objetos, centro en {plaza.position}.");
        }

        /// <summary>Escribe Temp/hub_diag.txt con medidas del lobby y del montículo (solo diagnóstico).</summary>
        static void DumpDiagnostics(GameObject lobby, Vector3 origin)
        {
            var sb = new System.Text.StringBuilder();
            Physics.SyncTransforms();
            if (lobby != null)
                foreach (var r in lobby.GetComponentsInChildren<Renderer>(true))
                    if (r.name.StartsWith("Camino_") || r.name.StartsWith("Marco_") || r.name.StartsWith("Cupula_Vidrio")
                        || r.name.StartsWith("Salida_") || r.name.StartsWith("Anillo_Base") || r.name.StartsWith("Piso_Interior"))
                        sb.AppendLine($"{r.name}: center={r.bounds.center:0.00} size={r.bounds.size:0.00}");
            foreach (var ang in new[] { 0f, 90f, 180f, 270f, 45f, 135f })
            {
                sb.Append($"altura a {ang}°: ");
                for (float rad = 14f; rad <= 34f; rad += 2f)
                {
                    var d = new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad), 0f, Mathf.Sin(ang * Mathf.Deg2Rad));
                    sb.Append($"r{rad:0}={SampleY(origin + d * rad, -9f, "Pasto_Exterior"):0.00} ");
                }
                sb.AppendLine();
            }
            System.IO.File.WriteAllText("Temp/hub_diag.txt", sb.ToString());
        }

        // ---------- Materiales ----------

        static void LoadMaterials()
        {
            if (!AssetDatabase.IsValidFolder(PlayMatsDir))
                AssetDatabase.CreateFolder("Assets/_Project/Core/Materials", "Juegos");

            mWhite  = Lobby("M_PisoBlanco");
            mAqua   = Lobby("M_NeonAqua");
            mGlass  = Lobby("M_VidrioCupula");
            mPath   = Lobby("M_Camino");
            mOrb    = Lobby("M_Orbe");
            mFrame  = Lobby("M_MarcoBlanco");
            mLeaf   = Lobby("M_Hojas");
            mChrome = Lobby("M_Cromo");

            mPink   = Candy("Juego_Rosa",     new Color(1.00f, 0.55f, 0.75f));
            mYellow = Candy("Juego_Amarillo", new Color(1.00f, 0.86f, 0.30f));
            mBlue   = Candy("Juego_Azul",     new Color(0.25f, 0.62f, 1.00f));
            mGreen  = Candy("Juego_Verde",    new Color(0.45f, 0.88f, 0.40f));
            mBush1   = Candy("Arbusto_Verde1", new Color(0.16f, 0.58f, 0.17f), 0.45f);
            mBush2   = Candy("Arbusto_Verde2", new Color(0.26f, 0.70f, 0.22f), 0.45f);
            mBush3   = Candy("Arbusto_Verde3", new Color(0.10f, 0.46f, 0.16f), 0.45f);
            mFlowerW = Candy("Arbusto_Flor",   new Color(1.00f, 0.97f, 0.92f), 0.6f);
            mGrass  = Candy("Juego_Cesped",   AverageTerrainColor(), 0.55f);
        }

        /// <summary>Color promedio de la textura del terreno de pasto del Hub, para que todo el suelo combine.</summary>
        static Color AverageTerrainColor()
        {
            var fallback = new Color(0.38f, 0.74f, 0.30f);
            try
            {
                var bytes = System.IO.File.ReadAllBytes("Assets/_Project/Core/Models/Pasto/Pasto_Terreno_Color.png");
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                ImageConversion.LoadImage(t, bytes);
                var px = t.GetPixels32();
                long r = 0, g = 0, b = 0, cnt = 0;
                foreach (var c in px)
                {
                    if (c.r + c.g + c.b < 60) continue; // la textura es un círculo con borde negro: se ignora
                    r += c.r; g += c.g; b += c.b; cnt++;
                }
                Object.DestroyImmediate(t);
                float n = cnt * 255f;
                return n > 0 ? new Color(r / n, g / n, b / n) : fallback;
            }
            catch { return fallback; }
        }

        static Material Lobby(string name)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>($"{LobbyMats}/{name}.mat");
            if (m == null) Debug.LogWarning($"[HubPlayground] Falta {name}; se usará un material por defecto.");
            return m != null ? m : Candy("Juego_Respaldo", Color.white);
        }

        static Material Candy(string name, Color c, float smooth = 0.9f)
        {
            string path = $"{PlayMatsDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---------- Utilidades ----------

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale,
                               Material mat, Vector3? euler = null, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            if (euler.HasValue) go.transform.localEulerAngles = euler.Value;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static Transform Group(string name, Transform parent, Vector3 localPos)
        {
            var g = new GameObject(name).transform;
            g.SetParent(parent, false);
            g.localPosition = localPos;
            return g;
        }

        static Vector3 Ring(float angleDeg, float radius, float y = 0f)
        {
            float a = angleDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius);
        }

        // ---------- Terreno y plaza ----------

        static void BuildGround(Transform root, float top)
        {
            // Terreno verde grande; su cara superior queda a la altura del borde del pasto del lobby.
            Prim(PrimitiveType.Cube, "Terreno_Juegos", root, new Vector3(0f, top - 0.1f, 62f), new Vector3(120f, 0.2f, 104f), mGrass);
        }

        /// <summary>Altura del borde del terreno de pasto exterior (para que no haya escalón).</summary>
        static float MeasureGrassTop(UnityEngine.SceneManagement.Scene scene, Vector3 origin, out string info, out float islandRadius)
        {
            islandRadius = 30f;
            float top = -0.02f;
            info = "Pasto_Exterior no encontrado; terreno a y=-0.02";
            var pe = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Pasto_Exterior");
            var mf = pe != null ? pe.GetComponentInChildren<MeshFilter>() : null;
            if (mf == null || mf.sharedMesh == null) return top;
            var verts = mf.sharedMesh.vertices;
            if (verts.Length == 0) return top;
            var m = mf.transform.localToWorldMatrix;
            var pts = new Vector3[verts.Length];
            float maxR = 0f, minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < verts.Length; i++)
            {
                pts[i] = m.MultiplyPoint3x4(verts[i]);
                maxR = Mathf.Max(maxR, new Vector2(pts[i].x - origin.x, pts[i].z - origin.z).magnitude);
                minY = Mathf.Min(minY, pts[i].y); maxY = Mathf.Max(maxY, pts[i].y);
            }
            islandRadius = maxR;
            float rimSum = 0f; int rimN = 0;
            foreach (var p in pts)
                if (new Vector2(p.x - origin.x, p.z - origin.z).magnitude >= 0.93f * maxR) { rimSum += p.y; rimN++; }
            float rim = rimN > 0 ? rimSum / rimN : minY;
            info = $"Pasto terreno: verts={verts.Length} radioMax={maxR:0.0} y[min {minY:0.000}, max {maxY:0.000}] borde={rim:0.000} pos={pe.transform.position} lobbyOrigin={origin}";
            return minY - 0.01f; // suelo plano del pasto (las colinas suben mas)
        }

        static void BuildPlazaBase(Transform plaza)
        {
            var disc = Prim(PrimitiveType.Cylinder, "Plaza_Piso", plaza, new Vector3(0f, 0f, 0f),
                            new Vector3(PlazaR * 2f, 0.05f, PlazaR * 2f), mWhite, collider: false);
            var mc = disc.AddComponent<MeshCollider>();
            mc.sharedMesh = disc.GetComponent<MeshFilter>().sharedMesh;

            // Anillo de neón aqua en el borde
            const int n = 48;
            for (int i = 0; i < n; i++)
            {
                float a = i * 360f / n;
                var p = Ring(a, PlazaR - 0.5f, 0.07f);
                float len = 2f * Mathf.PI * (PlazaR - 0.5f) / n + 0.08f;
                var seg = Prim(PrimitiveType.Cube, $"Neon_{i:00}", plaza, p, new Vector3(0.3f, 0.1f, len), mAqua, collider: false);
                seg.transform.localRotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, p.normalized), Vector3.up);
            }
        }

        static void BuildEntrance(Transform plaza)
        {
            var g = Group("Arco_Entrada", plaza, new Vector3(0f, 0f, -PlazaR + 1.5f));
            Prim(PrimitiveType.Cylinder, "Columna_Izq", g, new Vector3(-5.5f, 2.75f, 0f), new Vector3(0.8f, 2.75f, 0.8f), mFrame);
            Prim(PrimitiveType.Cylinder, "Columna_Der", g, new Vector3(5.5f, 2.75f, 0f), new Vector3(0.8f, 2.75f, 0.8f), mFrame);
            Prim(PrimitiveType.Cube, "Viga", g, new Vector3(0f, 5.7f, 0f), new Vector3(12.4f, 0.7f, 0.8f), mAqua, collider: false);
            Prim(PrimitiveType.Cube, "Letrero", g, new Vector3(0f, 7.3f, 0f), new Vector3(6.4f, 1.6f, 0.25f), mFrame, collider: false);
            Prim(PrimitiveType.Sphere, "Esfera_Izq", g, new Vector3(-5.5f, 6.5f, 0f), Vector3.one * 1.4f, mPink, collider: false);
            Prim(PrimitiveType.Sphere, "Esfera_Der", g, new Vector3(5.5f, 6.5f, 0f), Vector3.one * 1.4f, mYellow, collider: false);
        }

        static void BuildCenterOrb(Transform plaza)
        {
            var g = Group("Orbe_Central", plaza, Vector3.zero);
            Prim(PrimitiveType.Cylinder, "Base", g, new Vector3(0f, 0.3f, 0f), new Vector3(4.4f, 0.3f, 4.4f), mFrame);
            Prim(PrimitiveType.Cylinder, "Pedestal", g, new Vector3(0f, 0.9f, 0f), new Vector3(1.6f, 0.45f, 1.6f), mChrome);
            Prim(PrimitiveType.Sphere, "Orbe", g, new Vector3(0f, 2.8f, 0f), Vector3.one * 2.2f, mOrb, collider: false);
        }

        // ---------- Juegos ----------

        static void BuildSlide(Transform plaza, Vector3 pos)
        {
            var g = Group("Tobogan", plaza, pos);
            Prim(PrimitiveType.Cube, "Plataforma", g, new Vector3(0f, 3f, 0f), new Vector3(3f, 0.2f, 3f), mBlue);
            foreach (var sx in new[] { -1.4f, 1.4f })
                foreach (var sz in new[] { -1.4f, 1.4f })
                    Prim(PrimitiveType.Cylinder, "Poste", g, new Vector3(sx, 1.5f, sz), new Vector3(0.24f, 1.5f, 0.24f), mFrame);

            // Rampa que baja hacia +X
            float run = 6.3f, rise = 3f;
            float len = Mathf.Sqrt(run * run + rise * rise);
            float ang = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            Vector3 mid = new Vector3(1.5f + run / 2f, rise / 2f, 0f);
            Prim(PrimitiveType.Cube, "Rampa", g, mid, new Vector3(len, 0.15f, 2.2f), mPink, new Vector3(0f, 0f, -ang));
            foreach (var sz in new[] { -1.1f, 1.1f })
                Prim(PrimitiveType.Cube, "Borde", g, mid + new Vector3(0f, 0.25f, sz), new Vector3(len, 0.35f, 0.1f), mYellow, new Vector3(0f, 0f, -ang), collider: false);

            // Escalera por el lado -Z
            for (int k = 1; k <= 5; k++)
            {
                float h = 0.5f * k;
                float z = -1.5f - (5 - k) * 0.6f - 0.3f;
                Prim(PrimitiveType.Cube, $"Escalon_{k}", g, new Vector3(0f, h / 2f, z), new Vector3(2f, h, 0.6f), mYellow);
            }
        }

        static void BuildSwings(Transform plaza, Vector3 pos)
        {
            var g = Group("Columpios", plaza, pos);
            Prim(PrimitiveType.Cube, "Viga", g, new Vector3(0f, 4.2f, 0f), new Vector3(7f, 0.3f, 0.3f), mPink);
            foreach (var sx in new[] { -3.3f, 3.3f })
                foreach (var side in new[] { -1f, 1f })
                    Prim(PrimitiveType.Cylinder, "Pata", g, new Vector3(sx, 2.1f, side * 0.8f),
                         new Vector3(0.2f, 2.15f, 0.2f), mFrame, new Vector3(side * -12f, 0f, 0f));
            foreach (var sx in new[] { -1.2f, 1.2f })
            {
                Prim(PrimitiveType.Cube, "Asiento", g, new Vector3(sx, 1.0f, 0f), new Vector3(1.0f, 0.12f, 0.45f), mBlue, collider: false);
                foreach (var d in new[] { -0.4f, 0.4f })
                    Prim(PrimitiveType.Cylinder, "Cadena", g, new Vector3(sx + d, 2.6f, 0f), new Vector3(0.04f, 1.6f, 0.04f), mChrome, collider: false);
            }
        }

        static void BuildTrampolines(Transform plaza)
        {
            var spots = new[] { new Vector3(-4f, 0f, -9f), new Vector3(1.5f, 0f, -13f), new Vector3(-10f, 0f, -12f) };
            var tops = new[] { mAqua, mGreen, mPink };
            for (int i = 0; i < spots.Length; i++)
            {
                var g = Group($"Trampolin_{i + 1}", plaza, spots[i]);
                Prim(PrimitiveType.Cylinder, "Aro", g, new Vector3(0f, 0.17f, 0f), new Vector3(3.6f, 0.17f, 3.6f), mFrame);
                Prim(PrimitiveType.Cylinder, "Superficie", g, new Vector3(0f, 0.36f, 0f), new Vector3(3.0f, 0.02f, 3.0f), tops[i]);
            }
        }

        static void BuildSeesaw(Transform plaza, Vector3 pos)
        {
            var g = Group("Balancin", plaza, pos);
            Prim(PrimitiveType.Cylinder, "Eje", g, new Vector3(0f, 0.45f, 0f), new Vector3(0.7f, 0.45f, 0.7f), mFrame);
            Prim(PrimitiveType.Cube, "Tabla", g, new Vector3(0f, 0.95f, 0f), new Vector3(6f, 0.2f, 0.9f), mYellow, new Vector3(0f, 0f, 12f));
            Prim(PrimitiveType.Sphere, "Mango_A", g, new Vector3(-2.8f, 1.7f, 0f), Vector3.one * 0.4f, mPink, collider: false);
            Prim(PrimitiveType.Sphere, "Mango_B", g, new Vector3(2.8f, 0.7f, 0f), Vector3.one * 0.4f, mPink, collider: false);
        }

        // ---------- Decoración ----------

        static void BuildBubbles(Transform plaza)
        {
            var g = Group("Burbujas", plaza, Vector3.zero);
            var data = new[]
            {
                new Vector4(-14f, 7f, 14f, 2.6f), new Vector4(15f, 9f, 10f, 3.4f), new Vector4(0f, 12f, 16f, 2.0f),
                new Vector4(-6f, 5f, -14f, 1.5f), new Vector4(12f, 6f, -12f, 2.2f), new Vector4(-18f, 10f, -2f, 1.8f),
                new Vector4(19f, 5f, 0f, 1.2f), new Vector4(5f, 15f, -4f, 2.8f),
            };
            for (int i = 0; i < data.Length; i++)
                Prim(PrimitiveType.Sphere, $"Burbuja_{i + 1}", g, new Vector3(data[i].x, data[i].y, data[i].z),
                     Vector3.one * data[i].w, mGlass, collider: false);
        }

        static void BuildBenchesAndLamps(Transform plaza)
        {
            var benches = Group("Bancas", plaza, Vector3.zero);
            int bi = 0;
            foreach (var a in new[] { 10f, 45f, 135f, 170f, 200f, 340f })
            {
                var p = Ring(a, 19f);
                var b = Group($"Banca_{++bi}", benches, p);
                b.localRotation = Quaternion.LookRotation(-p.normalized, Vector3.up); // mira al centro
                Prim(PrimitiveType.Cube, "Asiento", b, new Vector3(0f, 0.5f, 0f), new Vector3(2.4f, 0.15f, 0.7f), mBlue);
                Prim(PrimitiveType.Cube, "Respaldo", b, new Vector3(0f, 0.95f, -0.3f), new Vector3(2.4f, 0.6f, 0.12f), mBlue, collider: false);
                foreach (var sx in new[] { -1f, 1f })
                    Prim(PrimitiveType.Cube, "Pata", b, new Vector3(sx, 0.25f, 0f), new Vector3(0.15f, 0.5f, 0.6f), mFrame, collider: false);
            }

            var lamps = Group("Faroles", plaza, Vector3.zero);
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f;
                if (Mathf.Abs(Mathf.DeltaAngle(a, 270f)) < 20f) continue; // libre la entrada
                var p = Ring(a, PlazaR - 1.6f);
                Prim(PrimitiveType.Cylinder, $"Farol_{i + 1}_Poste", lamps, p + Vector3.up * 1.8f, new Vector3(0.14f, 1.8f, 0.14f), mFrame, collider: false);
                Prim(PrimitiveType.Sphere, $"Farol_{i + 1}_Luz", lamps, p + Vector3.up * 3.9f, Vector3.one * 0.75f, mAqua, collider: false);
            }
        }

        static void BuildPlanters(Transform plaza)
        {
            var g = Group("Macetas", plaza, Vector3.zero);
            int i = 0;
            foreach (var a in new[] { 60f, 120f, 25f, 155f, 215f, 325f })
            {
                var p = Ring(a, 14f);
                Prim(PrimitiveType.Cylinder, $"Maceta_{++i}", g, p + Vector3.up * 0.4f, new Vector3(1.4f, 0.4f, 1.4f), mFrame);
                Prim(PrimitiveType.Sphere, $"Arbusto_{i}", g, p + Vector3.up * 1.3f, Vector3.one * 1.6f, mLeaf, collider: false);
            }
        }

        // ---------- Camino anillo alrededor de la cúpula (por fuera) ----------

        static void BuildRingPath(Transform root, List<Bounds> pathBounds)
        {
            var holder = Group("Camino_Anillo", root, Vector3.zero);
            const int N = 72;
            const float R = 19f; // sobre el piso plano, justo antes de que empiece a subir la colina
            for (int i = 0; i < N; i++)
            {
                var p0 = Ring(i * 360f / N, R);
                var p1 = Ring((i + 1) * 360f / N, R);
                var seg = p1 - p0;
                var s = Prim(PrimitiveType.Cube, $"Anillo_{i:00}", holder, (p0 + p1) * 0.5f + Vector3.up * -0.17f,
                             new Vector3(3.0f, 0.3f, seg.magnitude + 0.25f), mPath);
                s.transform.rotation = Quaternion.LookRotation(seg.normalized, Vector3.up);
                var b = s.GetComponent<Renderer>().bounds;
                b.Expand(new Vector3(0.6f, 0f, 0.6f));
                pathBounds.Add(b);
            }
        }

        // ---------- Arbustos al pie de la colina ----------

        static void BuildBushes(Transform root, Vector3 origin)
        {
            Physics.SyncTransforms();
            var g = Group("Arbustos_Colina", root, Vector3.zero);
            var rng = new System.Random(77);
            var mats = new[] { mBush1, mBush2, mBush3 };
            int count = 0;
            foreach (float R in new[] { 22.0f, 24.8f })
            {
                float step = R < 23f ? 2.4f : 3.2f;
                int n = Mathf.RoundToInt(2f * Mathf.PI * R / step);
                for (int i = 0; i < n; i++)
                {
                    float a = (i + (R > 23f ? 0.5f : 0f)) * 360f / n;
                    // Se dejan libres las salidas Este (0°) y Oeste (180°) para los caminos.
                    if (Mathf.Abs(Mathf.DeltaAngle(a, 0f)) < 11f || Mathf.Abs(Mathf.DeltaAngle(a, 180f)) < 11f) continue;
                    var pos = Ring(a + ((float)rng.NextDouble() - 0.5f) * 4f, R + ((float)rng.NextDouble() - 0.5f) * 1.2f);
                    float y = SampleY(origin + pos, -0.2f, "Pasto_Exterior");
                    var cluster = Group($"Arbusto_{++count}", g, new Vector3(pos.x, y, pos.z));
                    int blobs = 2 + rng.Next(0, 2);
                    for (int b = 0; b < blobs; b++)
                    {
                        float d = 0.9f + (float)rng.NextDouble() * 0.9f;
                        var off = new Vector3(((float)rng.NextDouble() - 0.5f) * 1.1f, d * 0.32f, ((float)rng.NextDouble() - 0.5f) * 1.1f);
                        Prim(PrimitiveType.Sphere, "Hojas", cluster, off, new Vector3(d, d * 0.8f, d), mats[rng.Next(mats.Length)], collider: false);
                    }
                    if (rng.NextDouble() < 0.3)
                        Prim(PrimitiveType.Sphere, "Flor", cluster, new Vector3(((float)rng.NextDouble() - 0.5f) * 0.8f, 0.85f, ((float)rng.NextDouble() - 0.5f) * 0.8f),
                             Vector3.one * 0.22f, rng.NextDouble() < 0.5 ? mFlowerW : mPink, collider: false);
                }
            }
        }

        /// <summary>
        /// Evita que las briznas de Pasto_Exterior atraviesen los caminos nuevos.
        /// Las cajas propias se reconocen por su altura (77.7) para no duplicarlas al reconstruir.
        /// </summary>
        static void ExcludeFromIslandGrass(GameObject pastoExterior, List<Bounds> bounds)
        {
            var gf = pastoExterior != null ? pastoExterior.GetComponent<GrassField>() : null;
            if (gf == null) return;
            var list = (gf.exclusions ?? new Bounds[0]).Where(b => Mathf.Abs(b.size.y - 77.7f) > 0.01f).ToList();
            foreach (var b in bounds) list.Add(new Bounds(b.center, new Vector3(b.size.x, 77.7f, b.size.z)));
            gf.exclusions = list.ToArray();
            EditorUtility.SetDirty(gf);
            gf.Generate();
        }

        // ---------- Falda: baja suave del borde del montículo de pasto al suelo ----------

        static GameObject skirtGO;

        static float SampleY(Vector3 world, float fallback, params string[] roots)
        {
            var hits = Physics.RaycastAll(new Vector3(world.x, 100f, world.z), Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            foreach (var h in hits)
                if (roots.Contains(h.collider.transform.root.name) && h.point.y > best) best = h.point.y;
            return float.IsNegativeInfinity(best) ? fallback : best;
        }

        static void BuildSkirt(Transform root, Vector3 origin, float islandR, float groundTop)
        {
            Physics.SyncTransforms();
            const int A = 180, J = 12;
            float r0 = islandR - 1.5f, r1 = islandR + 15f; // termina antes de la plaza (borde sur en z=46)
            var verts = new Vector3[(A + 1) * (J + 1)];
            for (int i = 0; i <= A; i++)
            {
                float th = (i % A) * Mathf.PI * 2f / A;
                var dir = new Vector3(Mathf.Cos(th), 0f, Mathf.Sin(th));
                float h = SampleY(origin + dir * r0, groundTop + 0.02f, "Pasto_Exterior") - 0.05f; // un poco hundido para tapar la costura
                h = Mathf.Max(h, groundTop + 0.02f);
                for (int j = 0; j <= J; j++)
                {
                    float t = j / (float)J;
                    float y = Mathf.Lerp(h, groundTop + 0.02f, Mathf.SmoothStep(0f, 1f, t));
                    verts[i * (J + 1) + j] = dir * Mathf.Lerp(r0, r1, t) + Vector3.up * y;
                }
            }
            var tris = new System.Collections.Generic.List<int>();
            for (int i = 0; i < A; i++)
                for (int j = 0; j < J; j++)
                {
                    int a = i * (J + 1) + j, b = a + 1, c = (i + 1) * (J + 1) + j, d = c + 1;
                    tris.AddRange(new[] { a, c, b, c, d, b });
                }
            var mesh = new Mesh { name = "Falda_Pasto_Mesh" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = skirtGO = new GameObject("Falda_Pasto");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mGrass;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            Physics.SyncTransforms();
        }

        // ---------- Caminos desde las puertas Este y Oeste ----------

        static void BuildPaths(Transform root, GameObject lobby, Vector3 origin, float groundTop,
                               List<Vector3> pathPts, List<Bounds> pathBounds)
        {
            Physics.SyncTransforms();
            // Todo en coordenadas locales de la raíz (que está en el centro del lobby).
            float east = 20.5f, west = -20.5f; // arranca donde el camino del lobby todavía está a nivel del suelo
            var g = Group("Caminos", root, Vector3.zero);
            float zEnd = PlazaZ - Mathf.Sqrt(PlazaR * PlazaR - 17.5f * 17.5f) + 0.3f;

            // Este: sale por +X, sube al norte y entra por el borde de la plaza
            Path(g, "Camino_Este", origin, groundTop, pathPts, pathBounds, new Vector3(east, 0f, 0f), new Vector3(east + 17f, 0f, 0f),
                 new Vector3(46f, 0f, 34f), new Vector3(17.5f, 0f, zEnd));
            // Oeste: simétrico
            Path(g, "Camino_Oeste", origin, groundTop, pathPts, pathBounds, new Vector3(west, 0f, 0f), new Vector3(west - 17f, 0f, 0f),
                 new Vector3(-46f, 0f, 34f), new Vector3(-17.5f, 0f, zEnd));
        }

        static void Path(Transform parent, string name, Vector3 origin, float groundTop,
                         List<Vector3> pathPts, List<Bounds> pathBounds, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            const int N = 64; // más tramos y con solape: sin rendijas en las curvas
            var holder = Group(name, parent, Vector3.zero);
            var pts = new Vector3[N + 1];
            for (int i = 0; i <= N; i++)
            {
                float t = i / (float)N, u = 1f - t;
                var pt = u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
                pt.y = SampleY(origin + pt, groundTop + 0.02f, "Pasto_Exterior", "Zona_Juegos") + 0.05f;
                pts[i] = pt;
            }
            for (int i = 1; i <= N; i++)
            {
                Vector3 seg = pts[i] - pts[i - 1];
                var s = Prim(PrimitiveType.Cube, $"{name}_{i:00}", holder, (pts[i] + pts[i - 1]) * 0.5f,
                             new Vector3(3.4f, 0.08f, seg.magnitude + 0.3f), mPath);
                s.transform.rotation = Quaternion.LookRotation(seg.normalized, Vector3.up);
                var b = s.GetComponent<Renderer>().bounds;
                b.Expand(new Vector3(0.6f, 0f, 0.6f));
                pathBounds.Add(b);
            }
            pathPts.AddRange(pts);
        }

        // ---------- Pasto (briznas) sobre la falda y el suelo, igual al del Hub ----------

        static void BuildGrass(Transform root, GameObject pastoExterior, Vector3 origin, float islandR, float groundTop,
                               List<Vector3> pathPts, List<Bounds> pathBounds)
        {
            var src = pastoExterior != null ? pastoExterior.GetComponent<GrassField>() : null;
            if (src == null || src.layers == null || src.layers.Length == 0)
            {
                Debug.LogWarning("[HubPlayground] No encontré el GrassField de Pasto_Exterior; el suelo queda sin briznas.");
                return;
            }

            // 1) Falda: briznas a la misma densidad, desde un poco dentro del borde del montículo hacia afuera.
            if (skirtGO != null)
                AddGrass(skirtGO, skirtGO.GetComponent<MeshFilter>(), src, 1f, origin, islandR - 4f, pathBounds);

            // 2) Suelo plano: malla de celdas invisible (sin la plaza, los caminos ni la zona de la falda).
            var cells = new GameObject("Pasto_Suelo_Fuente");
            cells.transform.SetParent(root, false);
            var mf = cells.AddComponent<MeshFilter>();
            mf.sharedMesh = BuildGroundCells(groundTop, islandR + 15f + 0.5f, pathPts);
            AddGrass(cells, mf, src, 0.6f, origin, 0f, new List<Bounds>());
        }

        static Mesh BuildGroundCells(float y, float skipRadius, List<Vector3> pathPts)
        {
            const float cell = 2f;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var plazaC = new Vector2(0f, PlazaZ);
            for (float x = -60f; x < 60f; x += cell)
                for (float z = 10f; z < 114f; z += cell)
                {
                    var c = new Vector2(x + cell * 0.5f, z + cell * 0.5f);
                    if (c.magnitude < skipRadius) continue;                    // lo cubre la falda
                    if ((c - plazaC).magnitude < PlazaR + 1.2f) continue;     // sin pasto bajo la plaza
                    bool onPath = false;
                    foreach (var pp in pathPts)
                        if ((new Vector2(pp.x, pp.z) - c).sqrMagnitude < 2.8f * 2.8f) { onPath = true; break; }
                    if (onPath) continue;
                    int i0 = verts.Count;
                    verts.Add(new Vector3(x, y, z)); verts.Add(new Vector3(x, y, z + cell));
                    verts.Add(new Vector3(x + cell, y, z + cell)); verts.Add(new Vector3(x + cell, y, z));
                    tris.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3 });
                }
            var m = new Mesh { name = "Pasto_Suelo_Celdas" };
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        static void AddGrass(GameObject go, MeshFilter ground, GrassField src, float densityScale,
                             Vector3 domeCenter, float innerRadius, List<Bounds> exclusions)
        {
            var gf = go.AddComponent<GrassField>();
            gf.ground = ground;
            gf.seed = src.seed + 7;
            gf.chunkSize = src.chunkSize;
            gf.domeCenter = domeCenter;
            gf.innerRadius = innerRadius;
            gf.exclusions = exclusions.ToArray();
            gf.layers = src.layers.Select(l => new GrassField.Layer
            {
                name = l.name, mesh = l.mesh, materials = l.materials,
                density = l.density * densityScale, scaleRange = l.scaleRange, castShadows = l.castShadows,
            }).ToArray();
            gf.Generate();
            Debug.Log($"[HubPlayground] Pasto en {go.name}: {gf.InstanceCount} instancias.");
        }
    }
}
