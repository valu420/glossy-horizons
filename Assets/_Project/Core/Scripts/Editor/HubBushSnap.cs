using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GlossyHorizons.Core.EditorTools
{
    /// <summary>
    /// Baja cada hoja de los arbustos de la colina (Zona_Juegos) y de la piscina hasta el terreno real,
    /// para que no queden flotando en las pendientes. Menú: Glossy Horizons > Hub > Ajustar arbustos al suelo.
    /// </summary>
    public static class HubBushSnap
    {
        const string ScenePath = "Assets/_Project/Core/Scenes/Hub.unity";
        static readonly string[] GroundNames = { "Falda_Pasto", "Terreno_Juegos", "Terreno_Piscina", "Terreno_Costado", "Terreno_Centro", "Isla_Superficie" };

        [MenuItem("Glossy Horizons/Hub/Ajustar arbustos al suelo")]
        static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log(Snap(scene));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        internal static string Snap(Scene scene)
        {
            Physics.SyncTransforms();
            var holders = new List<Transform>();
            foreach (var r in scene.GetRootGameObjects())
            {
                if (r.name == "Zona_Juegos") { var h = r.transform.Find("Arbustos_Colina"); if (h) holders.Add(h); }
                if (r.name == "Zona_Piscina") { var h = r.transform.Find("Piscina/Arbustos"); if (h) holders.Add(h); }
            }
            int moved = 0; float maxGap = 0f, sumGap = 0f;
            foreach (var holder in holders)
                foreach (Transform cluster in holder)
                    foreach (Transform b in cluster)
                    {
                        var p = b.position;
                        float gy = Ground(p);
                        if (float.IsNegativeInfinity(gy)) continue;
                        bool flower = b.name == "Flor";
                        float halfH = b.lossyScale.y * 0.5f;
                        if (!flower)
                        {
                            float gap = (p.y - halfH) - gy;
                            maxGap = Mathf.Max(maxGap, gap); sumGap += Mathf.Max(0f, gap);
                        }
                        float target = flower ? gy + 0.7f : gy + halfH * 0.55f; // 45 % de la hoja queda enterrada
                        b.position = new Vector3(p.x, target, p.z);
                        moved++;
                    }
            return $"[HubBushSnap] {moved} piezas ajustadas en {holders.Count} grupos; hueco máximo antes: {maxGap:0.00} m, promedio {(moved > 0 ? sumGap / moved : 0f):0.00} m.";
        }

        static float Ground(Vector3 p)
        {
            var hits = Physics.RaycastAll(new Vector3(p.x, 100f, p.z), Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            foreach (var h in hits)
                if ((h.collider.transform.root.name == "Pasto_Exterior" || GroundNames.Contains(h.collider.name)) && h.point.y > best)
                    best = h.point.y;
            return best;
        }
    }
}
