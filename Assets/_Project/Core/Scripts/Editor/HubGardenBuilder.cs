using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static GlossyHorizons.Core.EditorTools.HubPlaygroundBuilder;

namespace GlossyHorizons.Core.EditorTools
{
    /// <summary>
    /// Crea el jardín botánico bajo cristal al oeste (-X) de la cúpula del Hub, fuera del domo principal.
    /// Invernadero de cristal con costillas, estanque central con puente y árbol de cristal, cascada,
    /// camas de flores, plantas tropicales, palmeras, mariposas y orbes flotantes.
    /// Todo son primitivas de Unity. Menú: Glossy Horizons > Hub > Crear jardín botánico (oeste).
    /// Es idempotente: si ya existe "Zona_Jardin", la reconstruye.
    /// </summary>
    public static class HubGardenBuilder
    {
        const string ScenePath = "Assets/_Project/Core/Scenes/Hub.unity";
        const string RootName = "Zona_Jardin";
        const float GardenX = -84f;  // centro del invernadero
        const float R = 22f;         // radio del invernadero
        const float MarkerY = 77.9f; // marca las cajas de exclusión de pasto de esta zona

        static Material mRock, mViolet, mOrange, mTrunk;
        static System.Random rng;

        [MenuItem("Glossy Horizons/Hub/Crear jardín botánico (oeste)")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var old in scene.GetRootGameObjects().Where(o => o.name == RootName))
                Object.DestroyImmediate(old);

            var lobby = scene.GetRootGameObjects().FirstOrDefault(o => o.name == "Lobby_Cupula");
            Vector3 origin = lobby != null ? lobby.transform.position : Vector3.zero;
            origin.y = 0f;
            var hubGrass = scene.GetRootGameObjects().FirstOrDefault(o => o.name == "Pasto_Hub");
            var juegos = scene.GetRootGameObjects().FirstOrDefault(o => o.name == "Zona_Juegos");
            if (hubGrass == null || !HubIslandBuilder.TryTop(scene, out float top))
            {
                Debug.LogError("[HubGarden] Faltan Pasto_Hub y Zona_Juegos. Ejecuta antes 'Unificar pasto (suelo plano)'.");
                return;
            }

            LoadMaterials();
            mRock = Candy("Roca_Jardin", new Color(0.58f, 0.62f, 0.68f), 0.35f);
            mViolet = Candy("Flor_Violeta", new Color(0.65f, 0.40f, 0.95f), 0.7f);
            mOrange = Candy("Juego_Naranja", new Color(1.00f, 0.55f, 0.15f));
            mTrunk = Candy("Palmera_Tronco", new Color(0.55f, 0.38f, 0.22f), 0.25f);
            var mWater = HubPoolBuilder.Water();
            rng = new System.Random(2024);

            var root = new GameObject(RootName);
            root.transform.position = origin;

            // Suelo verde que continúa el del resto del Hub
            bool hasIsland = HubIslandBuilder.Exists(scene);
            if (!hasIsland) Prim(PrimitiveType.Cube, "Terreno_Jardin", root.transform, new Vector3(-90f, top - 0.1f, 0f), new Vector3(60f, 0.2f, 104f), mGrass);

            var g = Group("Jardin", root.transform, new Vector3(GardenX, top + 0.02f, 0f));
            BuildShell(g);
            BuildFloor(g);
            BuildPond(g, mWater);
            BuildBridge(g);
            BuildCrystalTree(g, mWater);
            BuildWaterfall(g, mWater);
            BuildBeds(g);
            BuildTropicals(g);
            BuildPalms(g);
            BuildBenches(g);
            BuildFloating(g);
            BuildButterflies(g);
            BuildOutsideBushes(g);

            // Camino recto desde la puerta Oeste del lobby hasta la entrada del invernadero
            var pts = new List<Vector3>(); var pb = new List<Bounds>();
            var gp = Group("Caminos_Jardin", root.transform, Vector3.zero);
            Path(gp, "Camino_Oeste_Jardin", origin, top, pts, pb,
                 new Vector3(-20.5f, 0f, 0f), new Vector3(-32f, 0f, 0f), new Vector3(-50f, 0f, 0f), new Vector3(GardenX + R + 1.5f, 0f, 0f));

            // Que las briznas de las otras zonas no atraviesen el camino nuevo
            Exclude(hubGrass.GetComponent<GrassField>(), pb);
            Exclude(juegos.transform.Find("Pasto_Suelo_Fuente")?.GetComponent<GrassField>(), pb);
            var piscina = scene.GetRootGameObjects().FirstOrDefault(x => x.name == "Zona_Piscina");
            if (piscina != null) Exclude(piscina.transform.Find("Pasto_Suelo_Fuente_Sur")?.GetComponent<GrassField>(), pb);

            // Briznas sobre el suelo del jardín (fuera del invernadero)
            var src = hubGrass.GetComponent<GrassField>();
            if (!hasIsland)
            {
                var cells = new GameObject("Pasto_Suelo_Jardin");
                cells.transform.SetParent(root.transform, false);
                var mf = cells.AddComponent<MeshFilter>();
                mf.sharedMesh = BuildCells(top);
                AddGrass(cells, mf, src, 0.6f, origin, 0f, new List<Bounds>());
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            Debug.Log($"[HubGarden] Jardín botánico creado: {root.GetComponentsInChildren<Transform>().Length - 1} objetos, centro en {g.position}.");
        }

        // ---------- Utilidades ----------

        static float Rf(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

        static GameObject PrimQ(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, Quaternion rot, bool collider = false)
        {
            var go = Prim(type, name, parent, pos, scale, mat, null, collider);
            go.transform.localRotation = rot;
            return go;
        }

        static GameObject Beam(Transform parent, string name, Vector3 a, Vector3 b, float t, Material mat, bool collider = false)
        {
            var d = b - a;
            var go = Prim(PrimitiveType.Cube, name, parent, (a + b) * 0.5f, new Vector3(t, t, d.magnitude + t * 0.5f), mat, null, collider);
            go.transform.localRotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            return go;
        }

        static Vector3 Dome(float phiDeg, float elDeg, float r)
        {
            float p = phiDeg * Mathf.Deg2Rad, e = elDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(e) * Mathf.Cos(p), Mathf.Sin(e), Mathf.Cos(e) * Mathf.Sin(p)) * r;
        }

        static void NoShadow(GameObject go) { go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }

        static void Exclude(GrassField gf, List<Bounds> bounds)
        {
            if (gf == null) return;
            var list = (gf.exclusions ?? new Bounds[0]).Where(b => Mathf.Abs(b.size.y - MarkerY) > 0.01f).ToList();
            foreach (var b in bounds) list.Add(new Bounds(b.center, new Vector3(b.size.x, MarkerY, b.size.z)));
            gf.exclusions = list.ToArray();
            EditorUtility.SetDirty(gf);
            gf.Generate();
        }

        static Mesh BuildCells(float y)
        {
            const float cell = 2f;
            var verts = new List<Vector3>(); var tris = new List<int>();
            var dc = new Vector2(GardenX, 0f);
            for (float x = -120f; x < -60f; x += cell)
                for (float z = -52f; z < 52f; z += cell)
                {
                    var c = new Vector2(x + cell * 0.5f, z + cell * 0.5f);
                    if ((c - dc).magnitude < R + 2.5f) continue; // sin pasto bajo el invernadero ni pegado a él
                    int i0 = verts.Count;
                    verts.Add(new Vector3(x, y, z)); verts.Add(new Vector3(x, y, z + cell));
                    verts.Add(new Vector3(x + cell, y, z + cell)); verts.Add(new Vector3(x + cell, y, z));
                    tris.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3 });
                }
            var m = new Mesh { name = "Pasto_Jardin_Celdas" };
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(verts); m.SetTriangles(tris, 0); m.RecalculateBounds();
            return m;
        }

        // ---------- Invernadero ----------

        static void BuildShell(Transform g)
        {
            var s = Group("Invernadero", g, Vector3.zero);
            var glass = new GameObject("Cristal");
            glass.transform.SetParent(s, false);
            glass.AddComponent<MeshFilter>().sharedMesh = HemiMesh(R);
            glass.AddComponent<MeshRenderer>().sharedMaterial = mGlass;
            NoShadow(glass);

            // Meridianos (dejan libre la entrada en +X)
            for (int k = 0; k < 12; k++)
            {
                float phi = 15f + 30f * k;
                for (int i = 0; i < 12; i++)
                {
                    float e0 = i * 7.5f, e1 = (i + 1) * 7.5f;
                    var seg = Beam(s, $"Costilla_{k}_{i}", Dome(phi, e0, R), Dome(phi, e1, R), 0.35f, mFrame);
                    NoShadow(seg);
                }
            }
            // Anillos horizontales
            foreach (var (el, n) in new[] { (20f, 48), (40f, 40), (60f, 28), (78f, 14) })
                for (int i = 0; i < n; i++)
                {
                    float p0 = i * 360f / n, p1 = (i + 1) * 360f / n;
                    float mid = Mathf.DeltaAngle(0f, (p0 + p1) * 0.5f);
                    if (el <= 40f && Mathf.Abs(mid) < 15f) continue;
                    var seg = Beam(s, $"Anillo_{el:0}_{i}", Dome(p0, el, R), Dome(p1, el, R), 0.3f, mFrame);
                    NoShadow(seg);
                }
            // Corona superior
            Prim(PrimitiveType.Sphere, "Remate", s, new Vector3(0f, R + 0.2f, 0f), Vector3.one * 1.6f, mChrome, null, false);

            // Entrada: viga cromada y letrero entre las costillas de ±15°
            Beam(s, "Viga_Entrada", Dome(-15f, 24f, R), Dome(15f, 24f, R), 0.5f, mChrome);
            var sign = Dome(0f, 24f, R - 0.2f);
            Prim(PrimitiveType.Cube, "Letrero", s, sign + Vector3.up * 1.4f, new Vector3(0.3f, 1.5f, 6f), mAqua, null, false);
            foreach (var sz in new[] { -1f, 1f })
                Prim(PrimitiveType.Cylinder, "Columna_Entrada", s, Dome(sz * 15f, 0f, R + 0.2f) + Vector3.up * 3.6f, new Vector3(0.6f, 3.6f, 0.6f), mChrome, null, true);

            // Zócalo cromado + neón aqua en la base
            for (int i = 0; i < 48; i++)
            {
                float a0 = i * 360f / 48f, a1 = (i + 1) * 360f / 48f;
                if (Mathf.Abs(Mathf.DeltaAngle(0f, (a0 + a1) * 0.5f)) < 15f) continue;
                Beam(s, $"Zocalo_{i}", Dome(a0, 0f, R + 0.1f) + Vector3.up * 0.15f, Dome(a1, 0f, R + 0.1f) + Vector3.up * 0.15f, 0.4f, mChrome);
                Beam(s, $"Neon_{i}", Dome(a0, 0f, R - 0.8f) + Vector3.up * 0.1f, Dome(a1, 0f, R - 0.8f) + Vector3.up * 0.1f, 0.18f, mAqua);
            }
        }

        /// <summary>Media esfera de cristal (solo la parte sobre el suelo), caras hacia afuera.</summary>
        static Mesh HemiMesh(float r)
        {
            const int L = 24, M = 48;
            var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i <= L; i++)
                for (int j = 0; j <= M; j++)
                    v.Add(Dome(j * 360f / M, i * 90f / L, r));
            int W = M + 1;
            for (int i = 0; i < L; i++)
                for (int j = 0; j < M; j++)
                {
                    int a = i * W + j, b = i * W + j + 1, c = (i + 1) * W + j, d = (i + 1) * W + j + 1;
                    t.AddRange(new[] { a, c, b, b, c, d });
                }
            var m = new Mesh { name = "Cristal_Mesh" };
            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        static void BuildFloor(Transform g)
        {
            var disc = Prim(PrimitiveType.Cylinder, "Piso", g, new Vector3(0f, 0f, 0f), new Vector3(R * 2f, 0.05f, R * 2f), mWhite, null, false);
            var mc = disc.AddComponent<MeshCollider>();
            mc.sharedMesh = disc.GetComponent<MeshFilter>().sharedMesh;
        }

        // ---------- Estanque, puente, árbol ----------

        static void BuildPond(Transform g, Material water)
        {
            var p = Group("Estanque", g, Vector3.zero);
            Prim(PrimitiveType.Cylinder, "Borde", p, new Vector3(0f, 0.25f, 0f), new Vector3(12.8f, 0.25f, 12.8f), mFrame, null, true);
            Prim(PrimitiveType.Cylinder, "Agua", p, new Vector3(0f, 0.52f, 0f), new Vector3(12.0f, 0.01f, 12.0f), water, null, false);
            Prim(PrimitiveType.Cylinder, "Isla", p, new Vector3(0f, 0.34f, 0f), new Vector3(5.6f, 0.22f, 5.6f), mGrass, null, true);

            // Nenúfares con flores
            var pads = new[] { new Vector2(-4f, 3.2f), new Vector2(3.5f, 4f), new Vector2(-3.2f, -4.2f), new Vector2(4.2f, -3.2f),
                               new Vector2(-1.2f, 4.8f), new Vector2(1.5f, -4.9f), new Vector2(-4.8f, 0.2f), new Vector2(5f, 0.6f) };
            for (int i = 0; i < pads.Length; i++)
            {
                Prim(PrimitiveType.Cylinder, $"Nenufar_{i}", p, new Vector3(pads[i].x, 0.56f, pads[i].y), new Vector3(1.3f, 0.02f, 1.3f), mLeaf, null, false);
                if (i % 2 == 0)
                    Prim(PrimitiveType.Sphere, $"Flor_Nenufar_{i}", p, new Vector3(pads[i].x, 0.7f, pads[i].y), Vector3.one * 0.38f, i % 4 == 0 ? mPink : mFlowerW, null, false);
            }
        }

        static void BuildBridge(Transform g)
        {
            var b = Group("Puente", g, Vector3.zero);
            const float L = 8.5f, H = 2.0f;
            const int N = 16;
            var pts = new Vector3[N + 1];
            for (int i = 0; i <= N; i++)
            {
                float x = Mathf.Lerp(-L, L, i / (float)N);
                float t = x / L;
                pts[i] = new Vector3(x, 0.55f + H * (1f - t * t), 0f);
            }
            for (int i = 0; i < N; i++)
            {
                var a = pts[i]; var c = pts[i + 1];
                var d = c - a;
                var deck = Prim(PrimitiveType.Cube, $"Tablon_{i}", b, (a + c) * 0.5f, new Vector3(2.4f, 0.18f, d.magnitude + 0.1f), mWhite, null, true);
                deck.transform.localRotation = Quaternion.LookRotation(d.normalized, Vector3.up);
                foreach (var sz in new[] { -1.15f, 1.15f })
                {
                    var rail = Beam(b, $"Baranda_{i}", a + new Vector3(0f, 1.0f, sz), c + new Vector3(0f, 1.0f, sz), 0.1f, mChrome);
                    if (i % 2 == 0)
                        Prim(PrimitiveType.Cylinder, "Poste", b, a + new Vector3(0f, 0.5f, sz), new Vector3(0.08f, 0.5f, 0.08f), mChrome, null, false);
                }
            }
            // Rampas de acceso en los extremos
            foreach (var sx in new[] { -1f, 1f })
                Prim(PrimitiveType.Cube, "Zapata", b, new Vector3(sx * (L + 0.6f), 0.3f, 0f), new Vector3(1.8f, 0.6f, 2.6f), mWhite, null, true);
        }

        static void BuildCrystalTree(Transform g, Material glassTint)
        {
            var t = Group("Arbol_Cristal", g, new Vector3(0f, 0.45f, -1.7f));
            Prim(PrimitiveType.Cylinder, "Tronco", t, new Vector3(0f, 1.6f, 0f), new Vector3(0.7f, 1.6f, 0.7f), mChrome, null, false);
            for (int i = 0; i < 4; i++)
            {
                var yq = Quaternion.Euler(0f, i * 90f + 20f, 0f);
                PrimQ(PrimitiveType.Cylinder, $"Rama_{i}", t, new Vector3(0f, 3.4f, 0f) + yq * new Vector3(0.8f, 0.5f, 0f), new Vector3(0.25f, 1.1f, 0.25f), mChrome, yq * Quaternion.Euler(0f, 0f, -35f));
            }
            var data = new[] { new Vector4(0f, 6.4f, 0f, 4.4f), new Vector4(2.4f, 5.6f, 0.8f, 3.2f), new Vector4(-2.3f, 5.8f, -0.6f, 3.4f),
                               new Vector4(0.6f, 7.8f, 1.6f, 3.0f), new Vector4(-0.8f, 7.2f, -2.0f, 2.8f) };
            for (int i = 0; i < data.Length; i++)
            {
                var s = Prim(PrimitiveType.Sphere, $"Copa_{i}", t, new Vector3(data[i].x, data[i].y, data[i].z), Vector3.one * data[i].w, glassTint, null, false);
                NoShadow(s);
            }
            for (int i = 0; i < 9; i++)
                Prim(PrimitiveType.Sphere, $"Orbe_{i}", t, new Vector3(Rf(-2.5f, 2.5f), Rf(5f, 8.5f), Rf(-2.2f, 2.2f)), Vector3.one * Rf(0.3f, 0.6f), mOrb, null, false);
        }

        // ---------- Cascada ----------

        static void BuildWaterfall(Transform g, Material water)
        {
            var w = Group("Cascada", g, new Vector3(-17.6f, 0f, 0f));
            // Muro de roca
            for (int i = 0; i < 22; i++)
            {
                float z = Rf(-7f, 7f), h = Rf(0.5f, 7.5f), x = Rf(-1.6f, 1.2f);
                float sz = Rf(2.2f, 4.2f);
                Prim(PrimitiveType.Sphere, $"Roca_{i}", w, new Vector3(x - 1.5f, h, z), new Vector3(sz, sz * Rf(0.8f, 1.3f), sz), mRock, null, i < 8);
            }
            // Lámina de agua que cae y pozo
            Prim(PrimitiveType.Cube, "Chorro", w, new Vector3(1.1f, 4.1f, 0f), new Vector3(0.4f, 7.2f, 3.6f), water, null, false);
            Prim(PrimitiveType.Cylinder, "Pozo_Borde", w, new Vector3(4.2f, 0.15f, 0f), new Vector3(7.6f, 0.15f, 7.6f), mFrame, null, true);
            Prim(PrimitiveType.Cylinder, "Pozo_Agua", w, new Vector3(4.2f, 0.32f, 0f), new Vector3(7.0f, 0.01f, 7.0f), water, null, false);
            // Espuma
            for (int i = 0; i < 6; i++)
                Prim(PrimitiveType.Sphere, $"Espuma_{i}", w, new Vector3(Rf(1.8f, 3f), 0.45f, Rf(-1.4f, 1.4f)), Vector3.one * Rf(0.5f, 0.9f), mWhite, null, false);
            // Plantas en las rocas
            for (int i = 0; i < 7; i++)
                Prim(PrimitiveType.Sphere, $"Musgo_{i}", w, new Vector3(Rf(-1.2f, 0.4f), Rf(1.5f, 7f), Rf(-6f, 6f)), new Vector3(1.2f, 0.7f, 1.2f), mBush2, null, false);
        }

        // ---------- Flores y plantas ----------

        static void Bed(Transform parent, string name, Vector3 pos, Material[] flowerMats)
        {
            var b = Group(name, parent, pos);
            float size = Rf(2.6f, 3.6f);
            Prim(PrimitiveType.Sphere, "Monticulo", b, new Vector3(0f, 0.1f, 0f), new Vector3(size, 0.9f, size), mBush2, null, false);
            int n = 9 + rng.Next(0, 5);
            for (int i = 0; i < n; i++)
            {
                float a = Rf(0f, Mathf.PI * 2f), r = Rf(0f, size * 0.42f);
                float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r, h = Rf(0.5f, 1.1f);
                Prim(PrimitiveType.Cylinder, "Tallo", b, new Vector3(x, 0.4f + h * 0.5f, z), new Vector3(0.04f, h * 0.5f, 0.04f), mLeaf, null, false);
                Prim(PrimitiveType.Sphere, "Flor", b, new Vector3(x, 0.4f + h + 0.08f, z), Vector3.one * Rf(0.28f, 0.45f), flowerMats[rng.Next(flowerMats.Length)], null, false);
            }
        }

        static void BuildBeds(Transform g)
        {
            var beds = Group("Camas", g, Vector3.zero);
            var mats = new[] { mPink, mYellow, mBlue, mFlowerW, mOrange, mViolet };
            int n = 0;
            for (float a = 20f; a < 360f; a += 24f)
            {
                if (Mathf.Abs(Mathf.DeltaAngle(a, 0f)) < 22f || Mathf.Abs(Mathf.DeltaAngle(a, 180f)) < 30f) continue;
                Bed(beds, $"Cama_{++n}", Ring(a, 16.5f), mats);
            }
            for (float a = 45f; a < 360f; a += 45f)
            {
                if (Mathf.Abs(Mathf.DeltaAngle(a, 180f)) < 40f) continue; // cascada
                Bed(beds, $"Cama_{++n}", Ring(a + 22f, 11.8f), mats);
            }
        }

        static void BuildTropicals(Transform g)
        {
            var parent = Group("Plantas_Tropicales", g, Vector3.zero);
            int n = 0;
            foreach (var (a, r) in new[] { (8f, 13f), (55f, 18.5f), (100f, 12.5f), (132f, 18.8f), (-100f, 12.8f), (-58f, 19f), (-135f, 17.5f), (30f, 9.5f), (-30f, 9.5f), (150f, 9f), (-150f, 9f), (90f, 19f) })
            {
                var b = Ring(a, r);
                var pl = Group($"Planta_{++n}", parent, b);
                float h = Rf(2.2f, 3.4f);
                Prim(PrimitiveType.Cylinder, "Tallo", pl, new Vector3(0f, h * 0.5f, 0f), new Vector3(0.18f, h * 0.5f, 0.18f), mLeaf, null, false);
                int leaves = 6;
                for (int i = 0; i < leaves; i++)
                {
                    var yq = Quaternion.Euler(0f, i * 360f / leaves + n * 13f, 0f);
                    PrimQ(PrimitiveType.Sphere, $"Hoja_{i}", pl, new Vector3(0f, h, 0f) + yq * new Vector3(1.1f, 0.35f, 0f), new Vector3(2.4f, 0.07f, 1.2f), i % 2 == 0 ? mLeaf : mBush1, yq * Quaternion.Euler(0f, 0f, 32f));
                }
            }
        }

        static void BuildPalms(Transform g)
        {
            var parent = Group("Palmeras", g, Vector3.zero);
            int n = 0;
            foreach (var a in new[] { 70f, 118f, -70f, -118f, 40f, -40f })
            {
                n++;
                var b = Ring(a, 19.4f);
                var q = Quaternion.Euler(Rf(-7f, 7f), 0f, Rf(-7f, 7f));
                float h = Rf(7f, 9f);
                var top = b + q * (Vector3.up * h);
                PrimQ(PrimitiveType.Cylinder, $"Tronco_{n}", parent, b + q * (Vector3.up * h * 0.5f), new Vector3(0.45f, h * 0.5f, 0.45f), mTrunk, q, true);
                for (int f = 0; f < 8; f++)
                {
                    var yq = Quaternion.Euler(0f, f * 45f + n * 11f, 0f);
                    PrimQ(PrimitiveType.Sphere, $"Fronda_{n}_{f}", parent, top + yq * new Vector3(1.8f, -0.3f, 0f), new Vector3(3.8f, 0.1f, 1.0f), mLeaf, yq * Quaternion.Euler(0f, 0f, -22f));
                }
                for (int c = 0; c < 3; c++)
                    Prim(PrimitiveType.Sphere, $"Coco_{n}_{c}", parent, top + new Vector3(Mathf.Cos(c * 2.1f) * 0.4f, -0.45f, Mathf.Sin(c * 2.1f) * 0.4f), Vector3.one * 0.4f, mTrunk, null, false);
            }
        }

        static void BuildBenches(Transform g)
        {
            var benches = Group("Bancas", g, Vector3.zero);
            int i = 0;
            foreach (var a in new[] { 45f, 135f, 225f, 315f })
            {
                var p = Ring(a, 9.2f);
                var b = Group($"Banca_{++i}", benches, p);
                b.localRotation = Quaternion.LookRotation(-p.normalized, Vector3.up);
                Prim(PrimitiveType.Cube, "Asiento", b, new Vector3(0f, 0.5f, 0f), new Vector3(2.4f, 0.15f, 0.7f), mBlue, null, true);
                Prim(PrimitiveType.Cube, "Respaldo", b, new Vector3(0f, 0.95f, -0.3f), new Vector3(2.4f, 0.6f, 0.12f), mBlue, null, false);
                foreach (var sx in new[] { -1f, 1f })
                    Prim(PrimitiveType.Cube, "Pata", b, new Vector3(sx, 0.25f, 0f), new Vector3(0.15f, 0.5f, 0.6f), mFrame, null, false);
            }
        }

        // ---------- Ambiente ----------

        static void BuildFloating(Transform g)
        {
            var f = Group("Flotantes", g, Vector3.zero);
            for (int i = 0; i < 14; i++)
            {
                float a = Rf(0f, 360f), r = Rf(3f, 17f);
                var p = Ring(a, r, Rf(4f, 13f));
                Prim(PrimitiveType.Sphere, $"Orbe_{i}", f, p, Vector3.one * Rf(0.35f, 0.7f), mOrb, null, false);
            }
            for (int i = 0; i < 8; i++)
            {
                float a = Rf(0f, 360f), r = Rf(4f, 16f);
                var s = Prim(PrimitiveType.Sphere, $"Burbuja_{i}", f, Ring(a, r, Rf(3f, 12f)), Vector3.one * Rf(0.9f, 2.0f), mGlass, null, false);
                NoShadow(s);
            }
        }

        static void BuildButterflies(Transform g)
        {
            var parent = Group("Mariposas", g, Vector3.zero);
            var mats = new[] { mPink, mYellow, mBlue, mViolet, mOrange };
            for (int i = 0; i < 10; i++)
            {
                var p = Ring(Rf(0f, 360f), Rf(5f, 17f), Rf(1.2f, 4.5f));
                var m = mats[i % mats.Length];
                var b = Group($"Mariposa_{i}", parent, p);
                b.localRotation = Quaternion.Euler(0f, Rf(0f, 360f), 0f);
                Prim(PrimitiveType.Sphere, "Cuerpo", b, Vector3.zero, new Vector3(0.1f, 0.1f, 0.4f), mFrame, null, false);
                foreach (var s in new[] { -1f, 1f })
                    PrimQ(PrimitiveType.Sphere, "Ala", b, new Vector3(s * 0.22f, 0.08f, 0f), new Vector3(0.5f, 0.03f, 0.36f), m, Quaternion.Euler(0f, 0f, s * -28f));
            }
        }

        static void BuildOutsideBushes(Transform g)
        {
            var parent = Group("Arbustos", g, Vector3.zero);
            var mats = new[] { mBush1, mBush2, mBush3 };
            const float Rb = 25.2f;
            int n = Mathf.RoundToInt(2f * Mathf.PI * Rb / 3.3f), count = 0;
            for (int i = 0; i < n; i++)
            {
                float a = i * 360f / n;
                if (Mathf.Abs(Mathf.DeltaAngle(a, 0f)) < 16f) continue; // libre la entrada
                var pos = Ring(a + Rf(-2f, 2f), Rb + Rf(-0.6f, 0.6f));
                var cl = Group($"Arbusto_{++count}", parent, pos);
                int blobs = 2 + rng.Next(0, 2);
                for (int b = 0; b < blobs; b++)
                {
                    float d = Rf(0.9f, 1.7f);
                    Prim(PrimitiveType.Sphere, "Hojas", cl, new Vector3(Rf(-0.55f, 0.55f), d * 0.32f, Rf(-0.55f, 0.55f)), new Vector3(d, d * 0.8f, d), mats[rng.Next(mats.Length)], null, false);
                }
                if (rng.NextDouble() < 0.35)
                    Prim(PrimitiveType.Sphere, "Flor", cl, new Vector3(Rf(-0.4f, 0.4f), 0.75f, Rf(-0.4f, 0.4f)), Vector3.one * 0.24f, rng.NextDouble() < 0.5 ? mPink : mFlowerW, null, false);
            }
        }
    }
}
