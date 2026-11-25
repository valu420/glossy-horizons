using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static GlossyHorizons.Core.EditorTools.HubPlaygroundBuilder;

namespace GlossyHorizons.Core.EditorTools
{
    /// <summary>
    /// Convierte el suelo rectangular del Hub en una isla: contorno orgánico (un lóbulo por zona), cara superior plana,
    /// acantilado rocoso que baja al mar, mar alrededor, espuma, rocas y arbustos en el borde y un muro invisible.
    /// También regenera TODO el pasto (briznas) recortado a la forma de la isla.
    /// Menú: Glossy Horizons > Hub > Crear isla. Es idempotente. Ejecútalo después de las zonas.
    /// Si se reconstruye una zona con su menú, vuelve a ejecutar "Crear isla" para refrescar el pasto.
    /// </summary>
    public static class HubIslandBuilder
    {
        const string ScenePath = "Assets/_Project/Core/Scenes/Hub.unity";

        // Forma: unión suave de lóbulos + círculos a lo largo de los caminos
        struct Lobe { public Vector2 c; public float r; public Lobe(float x, float z, float r) { c = new Vector2(x, z); this.r = r; } }
        static readonly Lobe[] Lobes =
        {
            new Lobe(0f, 0f, 44f),      // lobby
            new Lobe(0f, 70f, 36f),     // zona de juegos (norte)
            new Lobe(0f, -76f, 38f),    // piscina (sur)
            new Lobe(-84f, 0f, 36f),    // jardín botánico (oeste)
            new Lobe(70f, 0f, 26f),     // reservado para una zona futura (este)
        };
        const float K = 6f;         // suavidad de la unión
        const float PathR = 9f;     // ancho de tierra alrededor de los caminos
        static readonly List<Vector2> pathCircles = new List<Vector2>();

        // Acantilado y mar
        const float SeaDrop = 6f;       // el mar queda 6 m bajo la superficie
        const float CliffDepth = 11f;   // el acantilado llega 5 m bajo el mar
        const float CliffInset = 4.5f;  // cuánto se mete hacia dentro al bajar
        const float Step = 1.6f;

        internal static bool Exists(Scene scene) { return scene.GetRootGameObjects().Any(o => o.name == "Isla"); }

        /// <summary>Altura de la superficie del suelo (isla si existe; si no, el terreno plano de las zonas).</summary>
        internal static bool TryTop(Scene scene, out float top)
        {
            top = 0f;
            var roots = scene.GetRootGameObjects();
            var isla = roots.FirstOrDefault(o => o.name == "Isla");
            if (isla != null) { top = isla.transform.position.y; return true; }
            var juegos = roots.FirstOrDefault(o => o.name == "Zona_Juegos");
            var tj = juegos != null ? juegos.transform.Find("Terreno_Juegos") : null;
            if (tj != null) { top = tj.position.y + tj.lossyScale.y * 0.5f; return true; }
            var hub = roots.FirstOrDefault(o => o.name == "Pasto_Hub");
            var tc = hub != null ? hub.transform.Find("Terreno_Centro") : null;
            if (tc != null) { top = tc.position.y + tc.lossyScale.y * 0.5f; return true; }
            return false;
        }

        // ---------- Campo de distancia (negativo = dentro de la isla) ----------

        static float Field(Vector2 p)
        {
            float s = 0f;
            foreach (var l in Lobes) s += Mathf.Exp(-((p - l.c).magnitude - l.r) / K);
            float d = -K * Mathf.Log(s);
            foreach (var c in pathCircles)
            {
                float dx = p.x - c.x, dz = p.y - c.y;
                if (dx > 60f || dx < -60f || dz > 60f || dz < -60f) continue;
                d = Mathf.Min(d, Mathf.Sqrt(dx * dx + dz * dz) - PathR);
            }
            d += (Mathf.PerlinNoise(p.x * 0.045f + 100f, p.y * 0.045f + 100f) - 0.5f) * 5f
               + (Mathf.PerlinNoise(p.x * 0.16f + 7f, p.y * 0.16f + 3f) - 0.5f) * 1.6f;
            return d;
        }

        static Vector2 Grad(Vector2 p)
        {
            const float h = 0.3f;
            var g = new Vector2(Field(p + new Vector2(h, 0f)) - Field(p - new Vector2(h, 0f)),
                                Field(p + new Vector2(0f, h)) - Field(p - new Vector2(0f, h)));
            return g.sqrMagnitude < 1e-8f ? Vector2.right : g.normalized;
        }

        // ---------- Construcción ----------

        [MenuItem("Glossy Horizons/Hub/Crear isla")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!TryTop(scene, out float top))
            {
                Debug.LogError("[HubIsland] No encontré la altura del suelo (Terreno_Juegos o Isla).");
                return;
            }
            var roots = scene.GetRootGameObjects();
            var srcGO = roots.FirstOrDefault(o => o.name == "Pasto_Hub") ?? roots.FirstOrDefault(o => o.name == "Pasto_Exterior");
            var src = srcGO != null ? srcGO.GetComponent<GrassField>() : null;
            if (src == null || src.layers == null || src.layers.Length == 0)
            {
                Debug.LogError("[HubIsland] No encontré un GrassField de referencia (Pasto_Hub).");
                return;
            }

            LoadMaterials();
            var mRock = Candy("Isla_Roca", new Color(0.80f, 0.74f, 0.62f), 0.3f);
            // Acantilado: sin iluminación (Unlit) para que nunca salga negro
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit != null && mRock.shader != unlit) mRock.shader = unlit;
            mRock.SetColor("_BaseColor", new Color(0.62f, 0.50f, 0.37f));
            mRock.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(mRock);
            var mSea = Candy("Isla_Mar", new Color(0.12f, 0.62f, 0.88f), 0.95f);
            var mRockDeco = Candy("Roca_Jardin", new Color(0.58f, 0.62f, 0.68f), 0.35f);

            // 1) Caminos: círculos para el campo y cajas para excluir pasto
            Transform T(string root, string child) { var r = roots.FirstOrDefault(o => o.name == root); return r != null ? r.transform.Find(child) : null; }
            var holders = new[] { T("Zona_Juegos", "Caminos"), T("Zona_Piscina", "Caminos_Piscina"), T("Zona_Jardin", "Caminos_Jardin"), T("Zona_Mirador", "Caminos_Mirador") };
            pathCircles.Clear();
            for (float x = -36f; x >= -64f; x -= 4f) pathCircles.Add(new Vector2(x, 0f)); // puente al jardín
            var excl = new List<Bounds>();
            foreach (var h in holders)
            {
                if (h == null) continue;
                int n = 0;
                foreach (var r in h.GetComponentsInChildren<MeshRenderer>())
                {
                    if (n++ % 3 == 0) pathCircles.Add(new Vector2(r.bounds.center.x, r.bounds.center.z));
                    var b = r.bounds; b.Expand(new Vector3(2.4f, 0f, 2.4f)); excl.Add(b);
                }
            }
            var ring = T("Zona_Juegos", "Camino_Anillo");
            if (ring != null)
                foreach (var r in ring.GetComponentsInChildren<MeshRenderer>()) { var b = r.bounds; b.Expand(new Vector3(2.4f, 0f, 2.4f)); excl.Add(b); }
            var lobby = roots.FirstOrDefault(o => o.name == "Lobby_Cupula");
            if (lobby != null)
                foreach (var r in lobby.GetComponentsInChildren<Renderer>(true).Where(rr => rr.name.StartsWith("Camino_")))
                { var b = r.bounds; b.Expand(new Vector3(2.4f, 0f, 2.4f)); excl.Add(b); }

            // 2) Pasto nuevo (antes de destruir el viejo: sus capas vienen de él)
            var hub = new GameObject("Pasto_Hub_Nuevo");
            var mf = hub.AddComponent<MeshFilter>();
            mf.sharedMesh = BuildGrassCells(top, excl);
            AddGrass(hub, mf, src, 0.6f, Vector3.zero, 0f, new List<Bounds>());

            // 3) Quitar suelos y pastos rectangulares anteriores
            foreach (var o in scene.GetRootGameObjects().Where(oo => oo.name == "Isla" || oo.name == "Pasto_Hub").ToList()) Object.DestroyImmediate(o);
            hub.name = "Pasto_Hub";
            foreach (var (root, names) in new[]
            {
                ("Zona_Juegos", new[] { "Terreno_Juegos", "Pasto_Suelo_Fuente", "Falda_Pasto" }),
                ("Zona_Piscina", new[] { "Terreno_Piscina", "Terreno_Costado", "Pasto_Suelo_Fuente_Sur" }),
                ("Zona_Jardin", new[] { "Terreno_Jardin", "Pasto_Suelo_Jardin" }),
            })
            {
                var r = scene.GetRootGameObjects().FirstOrDefault(o => o.name == root);
                if (r == null) continue;
                foreach (var c in r.transform.Cast<Transform>().Where(cc => names.Contains(cc.name)).ToList())
                    Object.DestroyImmediate(c.gameObject);
            }

            // 4) Isla
            var isla = new GameObject("Isla");
            isla.transform.position = new Vector3(0f, top, 0f);
            BuildIsland(isla.transform, mRock, mSea, mRockDeco);

            Physics.SyncTransforms();
            Debug.Log(HubBushSnap.Snap(scene));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = isla;
            Debug.Log($"[HubIsland] Isla creada a y={top:0.00}: {isla.GetComponentsInChildren<Transform>().Length - 1} objetos.");
        }

        static Mesh BuildGrassCells(float y, List<Bounds> excl)
        {
            const float cell = 2f;
            var verts = new List<Vector3>(); var tris = new List<int>();
            var plazas = new[] { (new Vector2(0f, 70f), 25.5f), (new Vector2(0f, -76f), 30f), (new Vector2(-84f, 0f), 24.5f), (Vector2.zero, 17.5f), (new Vector2(HubViewpointBuilder.DeckX, 0f), 12f) };
            for (float x = -130f; x < 105f; x += cell)
                for (float z = -125f; z < 125f; z += cell)
                {
                    var c = new Vector2(x + cell * 0.5f, z + cell * 0.5f);
                    bool skip = false;
                    foreach (var (pc, pr) in plazas) if ((c - pc).magnitude < pr) { skip = true; break; }
                    if (skip || Field(c) > -2.5f) continue;
                    foreach (var b in excl)
                        if (Mathf.Abs(c.x - b.center.x) < b.extents.x && Mathf.Abs(c.y - b.center.z) < b.extents.z) { skip = true; break; }
                    if (skip) continue;
                    int i0 = verts.Count;
                    verts.Add(new Vector3(x, y, z)); verts.Add(new Vector3(x, y, z + cell));
                    verts.Add(new Vector3(x + cell, y, z + cell)); verts.Add(new Vector3(x + cell, y, z));
                    tris.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3 });
                }
            var m = new Mesh { name = "Pasto_Isla_Celdas" };
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(verts); m.SetTriangles(tris, 0); m.RecalculateBounds();
            return m;
        }

        static void BuildIsland(Transform isla, Material mRock, Material mSea, Material mRockDeco)
        {
            const float xmin = -130f, xmax = 105f, zmin = -125f, zmax = 125f;
            int nx = Mathf.CeilToInt((xmax - xmin) / Step) + 1, nz = Mathf.CeilToInt((zmax - zmin) / Step) + 1;
            var pos = new Vector2[nx * nz];
            var dist = new float[nx * nz];
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    var p = new Vector2(xmin + i * Step, zmin + j * Step);
                    pos[j * nx + i] = p; dist[j * nx + i] = Field(p);
                }

            // Celdas con algún vértice dentro; los vértices de fuera se proyectan al contorno (borde liso)
            var used = new bool[nx * nz];
            var quads = new List<int[]>();
            for (int j = 0; j < nz - 1; j++)
                for (int i = 0; i < nx - 1; i++)
                {
                    int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                    if (dist[a] < 0f || dist[b] < 0f || dist[c] < 0f || dist[d] < 0f)
                    {
                        quads.Add(new[] { a, b, c, d });
                        used[a] = used[b] = used[c] = used[d] = true;
                    }
                }
            for (int k = 0; k < used.Length; k++)
            {
                if (!used[k] || dist[k] < 0f) continue;
                var p = pos[k];
                for (int it = 0; it < 6; it++)
                {
                    float d = Field(p);
                    if (Mathf.Abs(d) < 0.02f) break;
                    p -= Grad(p) * d;
                }
                pos[k] = p;
            }

            // Malla superior
            var map = new Dictionary<int, int>();
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var nrm = new List<Vector3>();
            int V(int k)
            {
                if (map.TryGetValue(k, out int id)) return id;
                id = v.Count; map[k] = id;
                v.Add(new Vector3(pos[k].x, 0f, pos[k].y)); uv.Add(pos[k] * 0.1f); nrm.Add(Vector3.up);
                return id;
            }
            var tris = new List<int>();
            var edgeCount = new Dictionary<long, int>();
            void Edge(int p, int q) { long key = p < q ? (long)p * 10000000L + q : (long)q * 10000000L + p; edgeCount[key] = edgeCount.TryGetValue(key, out int c) ? c + 1 : 1; }
            foreach (var q in quads)
            {
                int a = V(q[0]), b = V(q[1]), c = V(q[2]), d = V(q[3]);
                tris.AddRange(new[] { a, c, b, b, c, d });
                Edge(q[0], q[1]); Edge(q[1], q[3]); Edge(q[3], q[2]); Edge(q[2], q[0]);
            }
            var top = new Mesh { name = "Isla_Superficie_Mesh" };
            top.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            top.SetVertices(v); top.SetUVs(0, uv); top.SetNormals(nrm); top.SetTriangles(tris, 0); top.RecalculateBounds();
            var topGO = new GameObject("Isla_Superficie");
            topGO.transform.SetParent(isla, false);
            topGO.AddComponent<MeshFilter>().sharedMesh = top;
            topGO.AddComponent<MeshRenderer>().sharedMaterial = mGrass;
            topGO.AddComponent<MeshCollider>().sharedMesh = top;

            // Borde: aristas que pertenecen a una sola celda
            var boundary = new List<(int a, int b)>();
            foreach (var kv in edgeCount)
                if (kv.Value == 1) boundary.Add(((int)(kv.Key / 10000000L), (int)(kv.Key % 10000000L)));
            var bverts = new Dictionary<int, int>(); // índice grilla -> índice en lista
            var blist = new List<int>();
            foreach (var (a, b) in boundary)
            {
                if (!bverts.ContainsKey(a)) { bverts[a] = blist.Count; blist.Add(a); }
                if (!bverts.ContainsKey(b)) { bverts[b] = blist.Count; blist.Add(b); }
            }
            var outward = blist.Select(k => Grad(pos[k])).ToArray();

            // Acantilado: capas que bajan y se meten hacia dentro
            const int J = 6;
            var cv = new List<Vector3>(); var cuv = new List<Vector2>(); var ct = new List<int>();
            for (int bi = 0; bi < blist.Count; bi++)
            {
                var p = pos[blist[bi]];
                for (int j = 0; j <= J; j++)
                {
                    float t = j / (float)J;
                    float wob = (Mathf.PerlinNoise(p.x * 0.35f + j * 5.3f, p.y * 0.35f + j * 1.7f) - 0.5f) * 1.8f * t;
                    float inset = CliffInset * t * t + wob + 0.3f * Mathf.Sin(t * Mathf.PI); // ligera panza al inicio
                    var q = p - outward[bi] * inset;
                    cv.Add(new Vector3(q.x, -CliffDepth * t, q.y));
                    cuv.Add(new Vector2((p.x + p.y) * 0.12f, -CliffDepth * t * 0.12f));
                }
            }
            foreach (var (a, b) in boundary)
            {
                int ia = bverts[a] * (J + 1), ib = bverts[b] * (J + 1);
                var o = (outward[bverts[a]] + outward[bverts[b]]).normalized;
                var outV = new Vector3(o.x, 0f, o.y);
                for (int j = 0; j < J; j++)
                {
                    int p0 = ia + j, p1 = ib + j, p2 = ia + j + 1, p3 = ib + j + 1;
                    AddOutward(ct, cv, p0, p1, p2, outV);
                    AddOutward(ct, cv, p1, p3, p2, outV);
                }
            }
            var cliff = new Mesh { name = "Isla_Acantilado_Mesh" };
            cliff.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            cliff.SetVertices(cv); cliff.SetUVs(0, cuv); cliff.SetTriangles(ct, 0);
            cliff.RecalculateNormals(); cliff.RecalculateBounds();
            var cliffGO = new GameObject("Isla_Acantilado");
            cliffGO.transform.SetParent(isla, false);
            cliffGO.AddComponent<MeshFilter>().sharedMesh = cliff;
            cliffGO.AddComponent<MeshRenderer>().sharedMaterial = mRock;
            cliffGO.AddComponent<MeshCollider>().sharedMesh = cliff;

            // Muro invisible un poco antes del borde (colisión por ambos lados)
            var wv = new List<Vector3>(); var wt = new List<int>();
            for (int bi = 0; bi < blist.Count; bi++)
            {
                var q = pos[blist[bi]] - outward[bi] * 1.3f;
                wv.Add(new Vector3(q.x, -1f, q.y)); wv.Add(new Vector3(q.x, 45f, q.y));
            }
            foreach (var (a, b) in boundary)
            {
                int i0 = bverts[a] * 2, i1 = bverts[b] * 2;
                wt.AddRange(new[] { i0, i1, i0 + 1, i1, i1 + 1, i0 + 1, i0, i0 + 1, i1, i1, i0 + 1, i1 + 1 });
            }
            var wall = new Mesh { name = "Muro_Invisible_Mesh" };
            wall.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            wall.SetVertices(wv); wall.SetTriangles(wt, 0); wall.RecalculateBounds();
            var wallGO = new GameObject("Muro_Invisible");
            wallGO.transform.SetParent(isla, false);
            wallGO.AddComponent<MeshCollider>().sharedMesh = wall;

            // Mar
            var sea = Prim(PrimitiveType.Cylinder, "Mar", isla, new Vector3(0f, -SeaDrop, 0f), new Vector3(1800f, 0.02f, 1800f), mSea, null, false);
            sea.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Decoración del borde: rocas, arbustos y espuma, separados por unos metros
            var rng = new System.Random(31);
            var deco = new GameObject("Borde_Decoracion").transform; deco.SetParent(isla, false);
            var foam = new GameObject("Espuma").transform; foam.SetParent(isla, false);
            var picked = new List<Vector2>();
            var order = Enumerable.Range(0, blist.Count).OrderBy(_ => rng.Next()).ToList();
            var bush = new[] { mBush1, mBush2, mBush3 };
            foreach (int bi in order)
            {
                var p = pos[blist[bi]];
                if (picked.Any(q => (q - p).sqrMagnitude < 4.5f * 4.5f)) continue;
                picked.Add(p);
                var inw = -outward[bi];
                var q2 = p + inw * 2.6f;
                if (picked.Count % 2 == 0)
                {
                    float s = 1.2f + (float)rng.NextDouble() * 1.6f;
                    var rock = Prim(PrimitiveType.Sphere, "Roca", deco, new Vector3(q2.x, s * 0.2f, q2.y), new Vector3(s * 1.3f, s * 0.8f, s), mRockDeco, null, true);
                    rock.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                }
                else
                {
                    for (int b = 0; b < 2; b++)
                    {
                        float d = 1.0f + (float)rng.NextDouble() * 0.9f;
                        Prim(PrimitiveType.Sphere, "Arbusto", deco, new Vector3(q2.x + (float)(rng.NextDouble() - 0.5) * 1.2f, d * 0.3f, q2.y + (float)(rng.NextDouble() - 0.5) * 1.2f),
                             new Vector3(d, d * 0.8f, d), bush[rng.Next(bush.Length)], null, false);
                    }
                }
                // Espuma al pie del acantilado, a nivel del mar
                var fp = p - outward[bi] * (CliffInset * Mathf.Pow(SeaDrop / CliffDepth, 2f) - 0.4f);
                Prim(PrimitiveType.Cylinder, "Espuma", foam, new Vector3(fp.x, -SeaDrop + 0.03f, fp.y), new Vector3(3.2f, 0.03f, 3.2f), mWhite, null, false);
            }
        }

        /// <summary>Añade un triángulo cuya normal apunta hacia 'outward' (invierte el orden si hace falta).</summary>
        static void AddOutward(List<int> tris, List<Vector3> verts, int a, int b, int c, Vector3 outward)
        {
            var n = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
            if (Vector3.Dot(n, outward) >= 0f) { tris.Add(a); tris.Add(b); tris.Add(c); }
            else { tris.Add(a); tris.Add(c); tris.Add(b); }
        }
    }
}
