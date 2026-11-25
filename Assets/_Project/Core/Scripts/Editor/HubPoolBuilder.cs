using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static GlossyHorizons.Core.EditorTools.HubPlaygroundBuilder;

namespace GlossyHorizons.Core.EditorTools
{
    /// <summary>
    /// Crea la zona de piscina al sur (-Z) de la cúpula del Hub, fuera del domo.
    /// Plaza redonda elevada con escalones, laguna circular con agua, trampolín, tobogán, palmeras,
    /// camastros, sombrillas, bar y flotadores. Dos caminos salen de las puertas Este y Oeste.
    /// Todo son primitivas y mallas generadas por código. Menú: Glossy Horizons > Hub > Crear zona de piscina (sur).
    /// Es idempotente: si ya existe "Zona_Piscina", la reconstruye. Requiere que exista "Zona_Juegos" (falda de pasto).
    /// </summary>
    public static class HubPoolBuilder
    {
        const string ScenePath = "Assets/_Project/Core/Scenes/Hub.unity";
        const string RootName = "Zona_Piscina";
        const string MatsDir = "Assets/_Project/Core/Materials/Juegos";

        const float PoolZ = -76f;   // centro de la plaza, al sur del lobby
        const float DeckR = 24f;    // radio de la cubierta (nivel alto)
        const float PoolR = 11f;    // radio de la laguna
        const float DeckH = 1.2f;   // altura de la cubierta sobre el suelo
        const float WaterY = 0.95f; // nivel del agua sobre el suelo
        const float MarkerY = 77.8f; // marca las cajas de exclusión de pasto de esta zona

        static Material mTile, mWater, mTrunk, mOrange;

        [MenuItem("Glossy Horizons/Hub/Crear zona de piscina (sur)")]
        public static void BuildPool()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            foreach (var old in scene.GetRootGameObjects().Where(g => g.name == RootName))
                Object.DestroyImmediate(old);

            var playground = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Zona_Juegos");
            var lobby = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Lobby_Cupula");
            Vector3 origin = lobby != null ? lobby.transform.position : Vector3.zero;
            origin.y = 0f;

            LoadMaterials();
            mTile = Candy("Piscina_Azulejo", new Color(0.55f, 0.90f, 1.00f), 0.75f);
            mTrunk = Candy("Palmera_Tronco", new Color(0.55f, 0.38f, 0.22f), 0.25f);
            mOrange = Candy("Juego_Naranja", new Color(1.00f, 0.55f, 0.15f));
            mWater = Water();

            var root = new GameObject(RootName);
            root.transform.position = origin;

            string info; float islandR;
            float groundTop = MeasureGrassTop(scene, origin, out info, out islandR);
            Physics.SyncTransforms();

            if (!HubIslandBuilder.Exists(scene)) BuildGround(root.transform, groundTop);

            var plaza = Group("Piscina", root.transform, new Vector3(0f, groundTop + 0.02f, PoolZ));
            var deck = Group("Cubierta", plaza, new Vector3(0f, DeckH, 0f));

            BuildDeck(plaza);
            BuildWater(plaza);
            BuildNeon(plaza);
            BuildEntrance(deck);
            BuildLadders(plaza);
            BuildDivingBoard(deck);
            BuildWaterSlide(deck, new Vector3(-17f, 0f, 3f));
            BuildPalms(deck);
            BuildLoungers(deck);
            BuildUmbrellas(deck);
            BuildBar(deck);
            BuildFloaties(plaza);
            BuildLamps(deck);
            BuildBubbles(plaza);
            BuildBushes(plaza);

            // Caminos desde las puertas Este y Oeste hacia el sur.
            var pathPts = new List<Vector3>();
            var pathBounds = new List<Bounds>();
            float zEnd = PoolZ + Mathf.Sqrt(28.8f * 28.8f - 17.5f * 17.5f);
            var gp = Group("Caminos_Piscina", root.transform, Vector3.zero);
            Path(gp, "Camino_Este_Sur", origin, groundTop, pathPts, pathBounds, new Vector3(20.5f, 0f, 0f), new Vector3(37.5f, 0f, 0f),
                 new Vector3(46f, 0f, -34f), new Vector3(17.5f, 0f, zEnd));
            Path(gp, "Camino_Oeste_Sur", origin, groundTop, pathPts, pathBounds, new Vector3(-20.5f, 0f, 0f), new Vector3(-37.5f, 0f, 0f),
                 new Vector3(-46f, 0f, -34f), new Vector3(-17.5f, 0f, zEnd));

            var pastoExterior = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Pasto_Exterior" || g.name == "Pasto_Hub");
            var src = pastoExterior != null ? pastoExterior.GetComponent<GrassField>() : null;
            Exclude(src, pathBounds);
            if (playground != null)
            {
                var skirt = playground.transform.Find("Falda_Pasto");
                if (skirt != null) Exclude(skirt.GetComponent<GrassField>(), pathBounds);
            }
            else Debug.LogWarning("[HubPool] No existe Zona_Juegos: la falda de pasto no está; crea primero la zona norte.");

            // Briznas sobre el suelo plano (igual que el resto del Hub)
            if (src != null && src.layers != null && src.layers.Length > 0 && !HubIslandBuilder.Exists(scene))
            {
                var cells = new GameObject("Pasto_Suelo_Fuente_Sur");
                cells.transform.SetParent(root.transform, false);
                var mf = cells.AddComponent<MeshFilter>();
                mf.sharedMesh = BuildCells(groundTop, islandR + 15f + 0.5f, pathPts);
                AddGrass(cells, mf, src, 0.6f, origin, 0f, new List<Bounds>());
            }

            Debug.Log(HubBushSnap.Snap(scene));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            Debug.Log($"[HubPool] Zona de piscina creada: {root.GetComponentsInChildren<Transform>().Length - 1} objetos, centro en {plaza.position}.");
        }

        // ---------- Materiales ----------

        internal static Material Water()
        {
            string path = MatsDir + "/Piscina_Agua.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { name = "Piscina_Agua" };
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            var c = new Color(0.25f, 0.85f, 1.00f, 0.60f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.95f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---------- Mallas ----------

        static GameObject MeshObj(string name, Transform parent, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        /// <summary>Anillo sólido: tapa superior plana + pared exterior hacia abajo.</summary>
        static GameObject RingSolid(string name, Transform parent, float rIn, float rOut, float yTop, float yBot, Material mat)
        {
            const int A = 96;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int i = 0; i <= A; i++)
            {
                float th = i * Mathf.PI * 2f / A;
                var d = new Vector3(Mathf.Cos(th), 0f, Mathf.Sin(th));
                var pi = d * rIn + Vector3.up * yTop; var po = d * rOut + Vector3.up * yTop;
                v.Add(pi); uv.Add(new Vector2(pi.x, pi.z) * 0.1f);
                v.Add(po); uv.Add(new Vector2(po.x, po.z) * 0.1f);
            }
            for (int i = 0; i < A; i++)
            {
                int a = 2 * i, b = a + 1, c = a + 2, d = a + 3;
                t.AddRange(new[] { a, c, b, c, d, b });
            }
            int o = v.Count;
            for (int i = 0; i <= A; i++)
            {
                float th = i * Mathf.PI * 2f / A;
                var d = new Vector3(Mathf.Cos(th), 0f, Mathf.Sin(th));
                v.Add(d * rOut + Vector3.up * yTop); uv.Add(new Vector2(i * 0.5f, yTop));
                v.Add(d * rOut + Vector3.up * yBot); uv.Add(new Vector2(i * 0.5f, yBot));
            }
            for (int i = 0; i < A; i++)
            {
                int A0 = o + 2 * i, B0 = o + 2 * (i + 1), C0 = o + 2 * i + 1, D0 = o + 2 * (i + 1) + 1;
                t.AddRange(new[] { A0, B0, C0, B0, D0, C0 });
            }
            var m = new Mesh { name = name + "_Mesh" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return MeshObj(name, parent, m, mat);
        }

        /// <summary>Pared cilíndrica vista desde dentro (pared de la piscina).</summary>
        static GameObject InnerWall(string name, Transform parent, float r, float yTop, float yBot, Material mat)
        {
            const int A = 96;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int i = 0; i <= A; i++)
            {
                float th = i * Mathf.PI * 2f / A;
                var d = new Vector3(Mathf.Cos(th), 0f, Mathf.Sin(th));
                v.Add(d * r + Vector3.up * yTop); uv.Add(new Vector2(i * 0.5f, yTop));
                v.Add(d * r + Vector3.up * yBot); uv.Add(new Vector2(i * 0.5f, yBot));
            }
            for (int i = 0; i < A; i++)
            {
                int A0 = 2 * i, B0 = 2 * (i + 1), C0 = 2 * i + 1, D0 = 2 * (i + 1) + 1;
                t.AddRange(new[] { A0, C0, B0, B0, C0, D0 });
            }
            var m = new Mesh { name = name + "_Mesh" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return MeshObj(name, parent, m, mat);
        }

        static GameObject PrimQ(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, Quaternion rot, bool collider = false)
        {
            var go = Prim(type, name, parent, pos, scale, mat, null, collider);
            go.transform.localRotation = rot;
            return go;
        }

        // ---------- Terreno ----------

        static void BuildGround(Transform root, float top)
        {
            Prim(PrimitiveType.Cube, "Terreno_Piscina", root, new Vector3(0f, top - 0.1f, -62f), new Vector3(120f, 0.2f, 104f), mGrass);
            // Rellena los costados entre la zona norte y esta (donde la falda ya no llega)
            foreach (var sx in new[] { -50.5f, 50.5f })
                Prim(PrimitiveType.Cube, "Terreno_Costado", root, new Vector3(sx, top - 0.1f, 0f), new Vector3(19f, 0.2f, 20f), mGrass);
        }

        static Mesh BuildCells(float y, float skipRadius, List<Vector3> pathPts)
        {
            const float cell = 2f;
            var verts = new List<Vector3>(); var tris = new List<int>();
            var plazaC = new Vector2(0f, PoolZ);
            for (float x = -60f; x < 60f; x += cell)
                for (float z = -114f; z < 10f; z += cell)
                {
                    var c = new Vector2(x + cell * 0.5f, z + cell * 0.5f);
                    if (c.magnitude < skipRadius) continue;
                    if ((c - plazaC).magnitude < 28.5f + 1.5f) continue;
                    bool onPath = false;
                    foreach (var pp in pathPts)
                        if ((new Vector2(pp.x, pp.z) - c).sqrMagnitude < 2.8f * 2.8f) { onPath = true; break; }
                    if (onPath) continue;
                    int i0 = verts.Count;
                    verts.Add(new Vector3(x, y, z)); verts.Add(new Vector3(x, y, z + cell));
                    verts.Add(new Vector3(x + cell, y, z + cell)); verts.Add(new Vector3(x + cell, y, z));
                    tris.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3 });
                }
            var m = new Mesh { name = "Pasto_Suelo_Celdas_Sur" };
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(verts); m.SetTriangles(tris, 0); m.RecalculateBounds();
            return m;
        }

        static void Exclude(GrassField gf, List<Bounds> bounds)
        {
            if (gf == null) return;
            var list = (gf.exclusions ?? new Bounds[0]).Where(b => Mathf.Abs(b.size.y - MarkerY) > 0.01f).ToList();
            foreach (var b in bounds) list.Add(new Bounds(b.center, new Vector3(b.size.x, MarkerY, b.size.z)));
            gf.exclusions = list.ToArray();
            EditorUtility.SetDirty(gf);
            gf.Generate();
        }

        // ---------- Cubierta, laguna y agua ----------

        static void BuildDeck(Transform plaza)
        {
            // Escalones concéntricos (0.3 m cada uno) para subir desde cualquier lado
            RingSolid("Escalon_1", plaza, DeckR, DeckR + 1.5f, 0.9f, 0f, mFrame);
            RingSolid("Escalon_2", plaza, DeckR + 1.5f, DeckR + 3f, 0.6f, 0f, mFrame);
            RingSolid("Escalon_3", plaza, DeckR + 3f, DeckR + 4.5f, 0.3f, 0f, mFrame);
            RingSolid("Cubierta_Piso", plaza, PoolR, DeckR, DeckH, 0f, mWhite);
            InnerWall("Pared_Piscina", plaza, PoolR, DeckH, 0.1f, mTile);

            var floor = Prim(PrimitiveType.Cylinder, "Fondo_Piscina", plaza, new Vector3(0f, 0f, 0f),
                             new Vector3(PoolR * 2f + 0.2f, 0.1f, PoolR * 2f + 0.2f), mTile, collider: false);
            var mc = floor.AddComponent<MeshCollider>();
            mc.sharedMesh = floor.GetComponent<MeshFilter>().sharedMesh;
        }

        static void BuildWater(Transform plaza)
        {
            Prim(PrimitiveType.Cylinder, "Agua", plaza, new Vector3(0f, WaterY, 0f),
                 new Vector3(PoolR * 2f - 0.05f, 0.01f, PoolR * 2f - 0.05f), mWater, collider: false);
            var vol = new GameObject("Volumen_Agua");
            vol.transform.SetParent(plaza, false);
            vol.transform.localPosition = new Vector3(0f, 0.5f * (0.1f + WaterY), 0f);
            var bc = vol.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(PoolR * 2f - 0.5f, WaterY - 0.1f, PoolR * 2f - 0.5f);
        }

        static void BuildNeon(Transform plaza)
        {
            var g = Group("Neon", plaza, Vector3.zero);
            foreach (var (r, n, y) in new[] { (PoolR + 0.2f, 64, DeckH + 0.03f), (DeckR - 0.5f, 48, DeckH + 0.03f) })
                for (int i = 0; i < n; i++)
                {
                    float a = i * 360f / n;
                    var p = Ring(a, r, y);
                    float len = 2f * Mathf.PI * r / n + 0.08f;
                    var seg = Prim(PrimitiveType.Cube, $"Neon_{r:0}_{i:00}", g, p, new Vector3(0.28f, 0.08f, len), mAqua, collider: false);
                    seg.transform.localRotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, p.normalized), Vector3.up);
                }
        }

        static void BuildEntrance(Transform deck)
        {
            var g = Group("Arco_Entrada", deck, new Vector3(0f, 0f, DeckR - 1.5f));
            Prim(PrimitiveType.Cylinder, "Columna_Izq", g, new Vector3(-5.5f, 2.75f, 0f), new Vector3(0.8f, 2.75f, 0.8f), mFrame);
            Prim(PrimitiveType.Cylinder, "Columna_Der", g, new Vector3(5.5f, 2.75f, 0f), new Vector3(0.8f, 2.75f, 0.8f), mFrame);
            Prim(PrimitiveType.Cube, "Viga", g, new Vector3(0f, 5.7f, 0f), new Vector3(12.4f, 0.7f, 0.8f), mAqua, collider: false);
            Prim(PrimitiveType.Cube, "Letrero", g, new Vector3(0f, 7.3f, 0f), new Vector3(6.4f, 1.6f, 0.25f), mFrame, collider: false);
            Prim(PrimitiveType.Sphere, "Esfera_Izq", g, new Vector3(-5.5f, 6.5f, 0f), Vector3.one * 1.4f, mBlue, collider: false);
            Prim(PrimitiveType.Sphere, "Esfera_Der", g, new Vector3(5.5f, 6.5f, 0f), Vector3.one * 1.4f, mYellow, collider: false);
        }

        // ---------- Elementos de la piscina ----------

        static void BuildLadders(Transform plaza)
        {
            var g = Group("Escaleras", plaza, Vector3.zero);
            foreach (var a in new[] { 45f, 225f })
            {
                var p = Ring(a, PoolR - 0.3f);
                var t = new Vector3(-Mathf.Sin(a * Mathf.Deg2Rad), 0f, Mathf.Cos(a * Mathf.Deg2Rad));
                foreach (var s in new[] { -0.4f, 0.4f })
                    Prim(PrimitiveType.Cylinder, "Riel", g, p + t * s + Vector3.up * 1.15f, new Vector3(0.08f, 1.05f, 0.08f), mChrome, collider: false);
                foreach (var y in new[] { 0.45f, 0.85f })
                {
                    var rung = Prim(PrimitiveType.Cube, "Peldano", g, p + Vector3.up * y, new Vector3(0.85f, 0.05f, 0.08f), mChrome, collider: false);
                    rung.transform.localRotation = Quaternion.LookRotation(t, Vector3.up) * Quaternion.Euler(0f, 90f, 0f);
                }
            }
        }

        static void BuildDivingBoard(Transform deck)
        {
            var g = Group("Trampolin_Salto", deck, new Vector3(0f, 0f, -(PoolR + 2f)));
            Prim(PrimitiveType.Cube, "Base", g, new Vector3(0f, 0.35f, 0f), new Vector3(2.4f, 0.7f, 2.2f), mBlue);
            Prim(PrimitiveType.Cube, "Tabla", g, new Vector3(0f, 0.78f, 3.9f), new Vector3(1.3f, 0.14f, 7.2f), mYellow);
            Prim(PrimitiveType.Cube, "Agarre", g, new Vector3(0f, 0.95f, -0.6f), new Vector3(1.2f, 0.4f, 0.12f), mChrome, collider: false);
        }

        static void BuildWaterSlide(Transform deck, Vector3 pos)
        {
            var g = Group("Tobogan_Agua", deck, pos);
            Prim(PrimitiveType.Cube, "Plataforma", g, new Vector3(0f, 4.2f, 0f), new Vector3(3f, 0.2f, 3f), mBlue);
            foreach (var sx in new[] { -1.4f, 1.4f })
                foreach (var sz in new[] { -1.4f, 1.4f })
                    Prim(PrimitiveType.Cylinder, "Poste", g, new Vector3(sx, 2.1f, sz), new Vector3(0.24f, 2.1f, 0.24f), mFrame);

            // Rampa que baja hacia +X y entra a la laguna
            float run = 8.5f, rise = 4.4f;
            float len = Mathf.Sqrt(run * run + rise * rise);
            float ang = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            var mid = new Vector3(1.5f + run / 2f, 2.0f, 0f);
            Prim(PrimitiveType.Cube, "Rampa", g, mid, new Vector3(len, 0.15f, 2.2f), mAqua, new Vector3(0f, 0f, -ang));
            foreach (var sz in new[] { -1.1f, 1.1f })
                Prim(PrimitiveType.Cube, "Borde", g, mid + new Vector3(0f, 0.25f, sz), new Vector3(len, 0.35f, 0.1f), mPink, new Vector3(0f, 0f, -ang), collider: false);

            // Escalera por el lado -Z (7 escalones de 0.6 m)
            for (int k = 1; k <= 7; k++)
            {
                float h = 0.6f * k;
                float z = -1.5f - (7 - k) * 0.7f - 0.35f;
                Prim(PrimitiveType.Cube, $"Escalon_{k}", g, new Vector3(0f, h / 2f, z), new Vector3(2f, h, 0.7f), mYellow);
            }
        }

        // ---------- Decoración ----------

        static void BuildPalms(Transform deck)
        {
            var g = Group("Palmeras", deck, Vector3.zero);
            var rng = new System.Random(12);
            int n = 0;
            foreach (var a in new[] { 28f, 62f, 118f, 205f, 245f, 330f })
            {
                n++;
                var b = Ring(a, DeckR - 3.4f);
                var q = Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 14f, 0f, ((float)rng.NextDouble() - 0.5f) * 14f);
                var top = b + q * (Vector3.up * 6f);
                PrimQ(PrimitiveType.Cylinder, $"Tronco_{n}", g, b + q * (Vector3.up * 3f), new Vector3(0.4f, 3f, 0.4f), mTrunk, q, true);
                for (int f = 0; f < 7; f++)
                {
                    float yaw = f * 360f / 7f + n * 17f;
                    var yq = Quaternion.Euler(0f, yaw, 0f);
                    PrimQ(PrimitiveType.Sphere, $"Hoja_{n}_{f}", g, top + yq * new Vector3(1.6f, -0.3f, 0f), new Vector3(3.4f, 0.1f, 1.0f), mLeaf, yq * Quaternion.Euler(0f, 0f, -22f));
                }
                for (int c = 0; c < 3; c++)
                    Prim(PrimitiveType.Sphere, $"Coco_{n}_{c}", g, top + new Vector3(Mathf.Cos(c * 2.1f) * 0.35f, -0.4f, Mathf.Sin(c * 2.1f) * 0.35f), Vector3.one * 0.38f, mTrunk, collider: false);
            }
        }

        static void BuildLoungers(Transform deck)
        {
            var g = Group("Camastros", deck, Vector3.zero);
            var cush = new[] { mPink, mYellow, mBlue, mGreen };
            int i = 0;
            foreach (var a in new[] { 15f, 40f, 140f, 215f, 150f })
            {
                var p = Ring(a, 15.5f);
                var l = Group($"Camastro_{++i}", g, p);
                l.localRotation = Quaternion.LookRotation(-p.normalized, Vector3.up);
                Prim(PrimitiveType.Cube, "Base", l, new Vector3(0f, 0.32f, 0f), new Vector3(1.0f, 0.24f, 2.4f), mFrame);
                Prim(PrimitiveType.Cube, "Colchoneta", l, new Vector3(0f, 0.5f, 0.2f), new Vector3(0.9f, 0.12f, 1.8f), cush[i % cush.Length], collider: false);
                PrimQ(PrimitiveType.Cube, "Respaldo", l, new Vector3(0f, 0.82f, -0.95f), new Vector3(0.9f, 0.12f, 0.9f), cush[i % cush.Length], Quaternion.Euler(-55f, 0f, 0f));
            }
        }

        static void BuildUmbrellas(Transform deck)
        {
            var g = Group("Sombrillas", deck, Vector3.zero);
            var mats = new[] { mPink, mYellow, mBlue, mAqua };
            int i = 0;
            foreach (var a in new[] { 27f, 178f, 222f, 345f })
            {
                var p = Ring(a, 18.2f);
                var u = Group($"Sombrilla_{++i}", g, p);
                Prim(PrimitiveType.Cylinder, "Base", u, new Vector3(0f, 0.12f, 0f), new Vector3(1.0f, 0.12f, 1.0f), mFrame, collider: false);
                Prim(PrimitiveType.Cylinder, "Mastil", u, new Vector3(0f, 1.7f, 0f), new Vector3(0.1f, 1.7f, 0.1f), mChrome, collider: false);
                Prim(PrimitiveType.Sphere, "Toldo", u, new Vector3(0f, 3.3f, 0f), new Vector3(4.4f, 1.0f, 4.4f), mats[i % mats.Length], collider: false);
            }
        }

        static void BuildBar(Transform deck)
        {
            var p = Ring(300f, 18f);
            var b = Group("Bar_Jugos", deck, p);
            b.localRotation = Quaternion.LookRotation(-p.normalized, Vector3.up);
            Prim(PrimitiveType.Cube, "Barra", b, new Vector3(0f, 0.55f, 0f), new Vector3(6f, 1.1f, 1.4f), mWhite);
            Prim(PrimitiveType.Cube, "Tapa_Barra", b, new Vector3(0f, 1.15f, 0f), new Vector3(6.3f, 0.12f, 1.7f), mAqua, collider: false);
            Prim(PrimitiveType.Cube, "Pared_Fondo", b, new Vector3(0f, 1.5f, -1.6f), new Vector3(6f, 3f, 0.3f), mFrame);
            Prim(PrimitiveType.Cube, "Techo", b, new Vector3(0f, 3.3f, -0.4f), new Vector3(7f, 0.25f, 3.6f), mPink, collider: false);
            foreach (var sx in new[] { -3.2f, 3.2f })
                Prim(PrimitiveType.Cylinder, "Poste", b, new Vector3(sx, 1.65f, 1.1f), new Vector3(0.14f, 1.65f, 0.14f), mFrame, collider: false);
            foreach (var sx in new[] { -2f, 0f, 2f })
                Prim(PrimitiveType.Cylinder, "Taburete", b, new Vector3(sx, 0.4f, 1.6f), new Vector3(0.5f, 0.4f, 0.5f), mYellow, collider: false);
        }

        static void BuildFloaties(Transform plaza)
        {
            var g = Group("Flotadores", plaza, new Vector3(0f, WaterY + 0.02f, 0f));
            // Pato
            Prim(PrimitiveType.Sphere, "Pato_Cuerpo", g, new Vector3(-3f, 0.4f, 2f), new Vector3(1.6f, 1.2f, 1.9f), mYellow, collider: false);
            Prim(PrimitiveType.Sphere, "Pato_Cabeza", g, new Vector3(-3f, 1.2f, 2.8f), Vector3.one * 0.85f, mYellow, collider: false);
            Prim(PrimitiveType.Cube, "Pato_Pico", g, new Vector3(-3f, 1.15f, 3.35f), new Vector3(0.35f, 0.15f, 0.45f), mOrange, collider: false);
            // Aros
            Prim(PrimitiveType.Cylinder, "Aro_Rosa", g, new Vector3(4f, 0.1f, -3f), new Vector3(2.4f, 0.12f, 2.4f), mPink, collider: false);
            Prim(PrimitiveType.Cylinder, "Aro_Blanco", g, new Vector3(-5f, 0.1f, -4f), new Vector3(2.2f, 0.12f, 2.2f), mWhite, collider: false);
            // Pelotas
            Prim(PrimitiveType.Sphere, "Pelota_Verde", g, new Vector3(2f, 0.5f, 5f), Vector3.one * 1.1f, mGreen, collider: false);
            Prim(PrimitiveType.Sphere, "Pelota_Azul", g, new Vector3(6f, 0.5f, 1f), Vector3.one * 1.0f, mBlue, collider: false);
        }

        static void BuildLamps(Transform deck)
        {
            var g = Group("Faroles", deck, Vector3.zero);
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f;
                if (Mathf.Abs(Mathf.DeltaAngle(a, 90f)) < 20f) continue; // libre la entrada
                var p = Ring(a, DeckR - 1.6f);
                Prim(PrimitiveType.Cylinder, $"Farol_{i + 1}_Poste", g, p + Vector3.up * 1.8f, new Vector3(0.14f, 1.8f, 0.14f), mFrame, collider: false);
                Prim(PrimitiveType.Sphere, $"Farol_{i + 1}_Luz", g, p + Vector3.up * 3.9f, Vector3.one * 0.75f, mAqua, collider: false);
            }
        }

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
                Prim(PrimitiveType.Sphere, $"Burbuja_{i + 1}", g, new Vector3(data[i].x, data[i].y, data[i].z), Vector3.one * data[i].w, mGlass, collider: false);
        }

        /// <summary>Arbustos al pie de los escalones, salvo donde llegan los caminos.</summary>
        static void BuildBushes(Transform plaza)
        {
            var g = Group("Arbustos", plaza, Vector3.zero);
            var rng = new System.Random(55);
            var mats = new[] { mBush1, mBush2, mBush3 };
            const float R = 31.2f;
            int n = Mathf.RoundToInt(2f * Mathf.PI * R / 3.4f), count = 0;
            for (int i = 0; i < n; i++)
            {
                float a = i * 360f / n;
                if (Mathf.Abs(Mathf.DeltaAngle(a, 52.6f)) < 9f || Mathf.Abs(Mathf.DeltaAngle(a, 127.4f)) < 9f) continue;
                var pos = Ring(a + ((float)rng.NextDouble() - 0.5f) * 4f, R + ((float)rng.NextDouble() - 0.5f) * 1.2f);
                var cl = Group($"Arbusto_{++count}", g, pos);
                int blobs = 2 + rng.Next(0, 2);
                for (int b = 0; b < blobs; b++)
                {
                    float d = 0.9f + (float)rng.NextDouble() * 0.9f;
                    var off = new Vector3(((float)rng.NextDouble() - 0.5f) * 1.1f, d * 0.32f, ((float)rng.NextDouble() - 0.5f) * 1.1f);
                    Prim(PrimitiveType.Sphere, "Hojas", cl, off, new Vector3(d, d * 0.8f, d), mats[rng.Next(mats.Length)], collider: false);
                }
                if (rng.NextDouble() < 0.3)
                    Prim(PrimitiveType.Sphere, "Flor", cl, new Vector3(((float)rng.NextDouble() - 0.5f) * 0.8f, 0.85f, ((float)rng.NextDouble() - 0.5f) * 0.8f),
                         Vector3.one * 0.22f, rng.NextDouble() < 0.5 ? mFlowerW : mPink, collider: false);
            }
        }
    }
}
