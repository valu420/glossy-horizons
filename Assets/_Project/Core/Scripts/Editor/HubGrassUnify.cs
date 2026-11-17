using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static GlossyHorizons.Core.EditorTools.HubPlaygroundBuilder;

namespace GlossyHorizons.Core.EditorTools
{
    /// <summary>
    /// Sustituye el pasto en colina "Pasto_Exterior" (y la falda de la zona de juegos) por suelo plano
    /// con las mismas briznas (GrassField) que usan las zonas de juegos y piscina.
    /// Menú: Glossy Horizons > Hub > Unificar pasto (suelo plano). Es idempotente.
    /// </summary>
    public static class HubGrassUnify
    {
        const string ScenePath = "Assets/_Project/Core/Scenes/Hub.unity";
        const float InnerR = 17.5f;  // dentro de este radio no hay pasto (cúpula y base)
        const float OuterR = 45.5f;  // desde aquí lo cubren las celdas de las zonas norte/sur

        [MenuItem("Glossy Horizons/Hub/Unificar pasto (suelo plano)")]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (HubIslandBuilder.Exists(scene)) { Debug.LogWarning("[HubGrassUnify] Ya existe la isla (Crear isla): ella genera el pasto. No hago nada."); return; }
            var roots = scene.GetRootGameObjects();

            var oldIsland = roots.FirstOrDefault(g => g.name == "Pasto_Exterior");
            var oldHub = roots.FirstOrDefault(g => g.name == "Pasto_Hub");
            var src = (oldIsland != null ? oldIsland : oldHub)?.GetComponent<GrassField>();
            if (src == null || src.layers == null || src.layers.Length == 0)
            {
                Debug.LogError("[HubGrassUnify] No encontré un GrassField de referencia (Pasto_Exterior o Pasto_Hub).");
                return;
            }
            var juegos = roots.FirstOrDefault(g => g.name == "Zona_Juegos");
            var terrenoJ = juegos != null ? juegos.transform.Find("Terreno_Juegos") : null;
            if (terrenoJ == null)
            {
                Debug.LogError("[HubGrassUnify] Falta Zona_Juegos/Terreno_Juegos (da la altura del suelo).");
                return;
            }
            float top = terrenoJ.position.y + terrenoJ.lossyScale.y * 0.5f;

            LoadMaterials(); // mGrass = Juego_Cesped

            // 1) Zonas de path (cajas) para no poner pasto encima
            var bounds = new List<Bounds>();
            foreach (var path in new[] { "Zona_Juegos/Caminos", "Zona_Juegos/Camino_Anillo", "Zona_Piscina/Caminos_Piscina", "Zona_Jardin/Caminos_Jardin" })
                foreach (var r in roots.SelectMany(x => x.GetComponentsInChildren<MeshRenderer>(true))
                                       .Where(rr => GetPath(rr.transform, 3).StartsWith(path)))
                {
                    var b = r.bounds; b.Expand(new Vector3(2.4f, 0f, 2.4f)); bounds.Add(b);
                }
            var lobby = roots.FirstOrDefault(g => g.name == "Lobby_Cupula");
            if (lobby != null)
                foreach (var r in lobby.GetComponentsInChildren<Renderer>(true).Where(rr => rr.name.StartsWith("Camino_")))
                { var b = r.bounds; b.Expand(new Vector3(2.4f, 0f, 2.4f)); bounds.Add(b); }

            // 2) Reconstruir Pasto_Hub
            var hub = new GameObject("Pasto_Hub_Nuevo");
            var mf = hub.AddComponent<MeshFilter>();
            mf.sharedMesh = BuildCells(top, bounds);
            Prim(PrimitiveType.Cube, "Terreno_Centro", hub.transform, new Vector3(0f, top - 0.1f, 0f), new Vector3(82f, 0.2f, 20f), mGrass);
            AddGrass(hub, mf, src, 0.6f, Vector3.zero, 0f, new List<Bounds>());

            // 3) Quitar la colina vieja, el Pasto_Hub anterior y la falda
            if (oldHub != null) Object.DestroyImmediate(oldHub);
            hub.name = "Pasto_Hub";
            if (oldIsland != null) Object.DestroyImmediate(oldIsland);
            var falda = juegos.transform.Find("Falda_Pasto");
            if (falda != null) Object.DestroyImmediate(falda.gameObject);

            // 4) Aplanar los caminos que subían la colina
            foreach (var holder in new[] { juegos.transform.Find("Caminos"),
                                           scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Zona_Piscina")?.transform.Find("Caminos_Piscina") })
            {
                if (holder == null) continue;
                foreach (var r in holder.GetComponentsInChildren<MeshRenderer>())
                {
                    var t = r.transform;
                    var f = t.forward; f.y = 0f;
                    if (f.sqrMagnitude < 1e-4f) continue;
                    t.rotation = Quaternion.LookRotation(f.normalized, Vector3.up);
                    var p = t.position; p.y = top + 0.07f; t.position = p;
                }
            }

            Physics.SyncTransforms();
            Debug.Log(HubBushSnap.Snap(scene));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = hub;
            Debug.Log($"[HubGrassUnify] Pasto unificado: suelo plano a y={top:0.00}, {bounds.Count} cajas de camino excluidas.");
        }

        static string GetPath(Transform t, int maxDepth)
        {
            var parts = new List<string>();
            while (t != null) { parts.Add(t.name); t = t.parent; }
            parts.Reverse();
            return string.Join("/", parts.Take(maxDepth + 1).Skip(0)).Replace("Hub/", "");
        }

        static Mesh BuildCells(float y, List<Bounds> exclude)
        {
            const float cell = 2f;
            var verts = new List<Vector3>(); var tris = new List<int>();
            for (float x = -48f; x < 48f; x += cell)
                for (float z = -48f; z < 48f; z += cell)
                {
                    var c = new Vector2(x + cell * 0.5f, z + cell * 0.5f);
                    if (c.magnitude < InnerR || c.magnitude >= OuterR) continue;
                    bool skip = false;
                    foreach (var b in exclude)
                        if (Mathf.Abs(c.x - b.center.x) < b.extents.x && Mathf.Abs(c.y - b.center.z) < b.extents.z) { skip = true; break; }
                    if (skip) continue;
                    int i0 = verts.Count;
                    verts.Add(new Vector3(x, y, z)); verts.Add(new Vector3(x, y, z + cell));
                    verts.Add(new Vector3(x + cell, y, z + cell)); verts.Add(new Vector3(x + cell, y, z));
                    tris.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3 });
                }
            var m = new Mesh { name = "Pasto_Hub_Celdas" };
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(verts); m.SetTriangles(tris, 0); m.RecalculateBounds();
            return m;
        }
    }
}
