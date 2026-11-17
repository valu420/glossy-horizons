using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static GlossyHorizons.Core.EditorTools.HubPlaygroundBuilder;

namespace GlossyHorizons.Core.EditorTools
{
    /// <summary>
    /// Crea el mirador al mar en el lóbulo este de la isla: camino recto desde la puerta Este del lobby,
    /// plataforma de madera semicircular con barandilla de cromo y vidrio, telescopios, bancas, faro,
    /// glorieta, faroles y arbustos. Todo son primitivas generadas por código.
    /// Menú: Glossy Horizons > Hub > Crear mirador (este). Es idempotente (reconstruye "Zona_Mirador").
    /// Si ya existe la isla, la regenera al final para que el pasto no atraviese el mirador.
    /// </summary>
    public static class HubViewpointBuilder
    {
        const string ScenePath = "Assets/_Project/Core/Scenes/Hub.unity";
        const string RootName = "Zona_Mirador";

        public const float DeckX = 84f;   // centro de la plataforma
        public const float DeckR = 10f;   // radio de la plataforma
        const float DeckH = 0.5f;         // altura de la plataforma sobre el suelo

        static Material mWood;

        [MenuItem("Glossy Horizons/Hub/Crear mirador (este)")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            foreach (var old in scene.GetRootGameObjects().Where(g => g.name == RootName).ToList())
                Object.DestroyImmediate(old);

            if (!HubIslandBuilder.TryTop(scene, out float top))
            {
                Debug.LogError("[HubViewpoint] No encontré la altura del suelo.");
                return;
            }

            LoadMaterials();
            mWood = Candy("Mirador_Madera", new Color(0.66f, 0.46f, 0.28f), 0.25f);

            var root = new GameObject(RootName);
            root.transform.position = Vector3.zero;

            var deck = Group("Plataforma", root.transform, new Vector3(DeckX, top + 0.02f, 0f));
            BuildDeck(deck);
            BuildRailing(deck);
            BuildTelescopes(deck);
            BuildBenches(deck);
            BuildEntranceArch(deck);
            BuildLighthouse(root.transform, new Vector3(82f, top + 0.02f, -17f));
            BuildGazebo(root.transform, new Vector3(82f, top + 0.02f, 17f));
            BuildLamps(root.transform, top);
            BuildBubbles(deck);
            BuildBushes(root.transform, top);

            // Camino recto desde la puerta Este del lobby hasta la plataforma
            var pts = new List<Vector3>(); var pb = new List<Bounds>();
            var holder = Group("Caminos_Mirador", root.transform, Vector3.zero);
            Path(holder, "Camino_Este_Mirador", Vector3.zero, top, pts, pb,
                 new Vector3(20.5f, 0f, 0f), new Vector3(36f, 0f, 0f), new Vector3(56f, 0f, 0f), new Vector3(DeckX - DeckR + 1f, 0f, 0f));

            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[HubViewpoint] Mirador creado: {root.GetComponentsInChildren<Transform>().Length - 1} objetos.");

            // La isla toma los caminos y plazas al generar su pasto: se regenera para integrarlo
            if (HubIslandBuilder.Exists(scene)) HubIslandBuilder.Build();
            else Selection.activeGameObject = root;
        }


        // ---------- Delfines ----------

        [MenuItem("Glossy Horizons/Hub/Crear delfines (mirador)")]
        public static void BuildDolphins()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var zona = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
            if (zona == null) { Debug.LogError("[HubViewpoint] Primero crea el mirador."); return; }
            foreach (var old in zona.transform.Cast<Transform>().Where(c => c.name == "Delfines").ToList())
                Object.DestroyImmediate(old.gameObject);

            LoadMaterials();
            HubIslandBuilder.TryTop(scene, out float top);
            float seaY = top - 6f;
            var isla = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Isla");
            var mar = isla != null ? isla.transform.Find("Mar") : null;
            if (mar != null) seaY = mar.position.y + 0.02f;

            var mBody = Candy("Delfin_Cuerpo", new Color(0.42f, 0.60f, 0.78f), 0.8f);
            var mBelly = Candy("Delfin_Vientre", new Color(0.88f, 0.94f, 0.98f), 0.8f);
            var mDark = Candy("Delfin_Ojo", new Color(0.04f, 0.06f, 0.10f), 0.9f);

            var pod = Group("Delfines", zona.transform, Vector3.zero);
            // centro XZ, radio, sentido horario, altura inicial sobre el mar, cabeceo inicial, desfase
            var cfg = new (Vector2 c, float r, bool cw, float h, float pitch, float ph)[]
            {
                (new Vector2(116f, -4f), 12f, false, 2.4f, -38f, 0.00f),
                (new Vector2(122f,  6f),  9f, true,  3.1f,   0f, 0.35f),
                (new Vector2(112f, 10f), 14f, false, 1.4f,  42f, 0.68f),
            };
            for (int i = 0; i < cfg.Length; i++)
            {
                var k = cfg[i];
                var d = BuildDolphin($"Delfin_{i + 1}", pod, mBody, mBelly, mDark);
                d.position = new Vector3(k.c.x + k.r, seaY + k.h, k.c.y);
                d.rotation = Quaternion.Euler(k.pitch, k.cw ? 180f : 0f, 0f);
                var sw = d.gameObject.AddComponent<GlossyHorizons.Core.DolphinSwim>();
                sw.center = new Vector3(k.c.x, 0f, k.c.y); sw.radius = k.r; sw.clockwise = k.cw;
                sw.seaY = seaY; sw.phase = k.ph;
                sw.speed = 6f + i * 1.2f; sw.jumpHeight = 2.8f + i * 0.5f; sw.period = 6.5f + i * 1.3f;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = pod.gameObject;
            Debug.Log($"[HubViewpoint] {cfg.Length} delfines creados (mar y={seaY:0.00}).");
        }

        // Modelo con primitivas; mira hacia +Z, mide ~6 m de largo
        static Transform BuildDolphin(string name, Transform parent, Material body, Material belly, Material dark)
        {
            var d = Group(name, parent, Vector3.zero);
            void S(string n, Vector3 pos, Vector3 scale, Material m, Vector3? euler = null)
                => Prim(PrimitiveType.Sphere, n, d, pos, scale, m, euler, collider: false);

            S("Cuerpo", new Vector3(0f, 0f, 0f), new Vector3(1.0f, 0.95f, 3.4f), body);
            S("Vientre", new Vector3(0f, -0.2f, 0.1f), new Vector3(0.85f, 0.6f, 3.0f), belly);
            S("Melon", new Vector3(0f, 0.05f, 1.55f), new Vector3(0.85f, 0.8f, 1.1f), body);
            S("Hocico", new Vector3(0f, -0.08f, 2.3f), new Vector3(0.32f, 0.28f, 1.0f), body);
            S("Hocico_Inferior", new Vector3(0f, -0.2f, 2.25f), new Vector3(0.26f, 0.16f, 0.9f), belly);
            S("Cola_1", new Vector3(0f, 0.02f, -1.95f), new Vector3(0.55f, 0.55f, 1.7f), body);
            S("Cola_2", new Vector3(0f, 0.06f, -3.0f), new Vector3(0.32f, 0.36f, 1.3f), body);
            S("Aleta_Cola_Izq", new Vector3(-0.55f, 0.08f, -3.75f), new Vector3(1.1f, 0.09f, 0.55f), body, new Vector3(0f, -22f, 0f));
            S("Aleta_Cola_Der", new Vector3(0.55f, 0.08f, -3.75f), new Vector3(1.1f, 0.09f, 0.55f), body, new Vector3(0f, 22f, 0f));
            S("Aleta_Dorsal", new Vector3(0f, 0.62f, -0.35f), new Vector3(0.12f, 0.9f, 0.85f), body, new Vector3(-28f, 0f, 0f));
            S("Aleta_Izq", new Vector3(-0.62f, -0.32f, 0.95f), new Vector3(0.75f, 0.09f, 0.38f), body, new Vector3(0f, 25f, -28f));
            S("Aleta_Der", new Vector3(0.62f, -0.32f, 0.95f), new Vector3(0.75f, 0.09f, 0.38f), body, new Vector3(0f, -25f, 28f));
            S("Ojo_Izq", new Vector3(-0.38f, 0.12f, 1.75f), Vector3.one * 0.14f, dark);
            S("Ojo_Der", new Vector3(0.38f, 0.12f, 1.75f), Vector3.one * 0.14f, dark);
            return d;
        }

        static Vector3 Arc(float deg, float r, float y = 0f) { return Ring(deg, r, y); } // 0° = este (+X)

        static void BuildDeck(Transform deck)
        {
            Prim(PrimitiveType.Cylinder, "Base_Piedra", deck, new Vector3(0f, 0.12f, 0f), new Vector3(DeckR * 2f + 1.6f, 0.12f, DeckR * 2f + 1.6f), mWhite);
            Prim(PrimitiveType.Cylinder, "Cubierta_Madera", deck, new Vector3(0f, DeckH * 0.5f, 0f), new Vector3(DeckR * 2f, DeckH * 0.5f, DeckR * 2f), mWood);
            Prim(PrimitiveType.Cylinder, "Medallon", deck, new Vector3(0f, DeckH + 0.01f, 0f), new Vector3(7f, 0.012f, 7f), mFrame, collider: false);
            Prim(PrimitiveType.Cylinder, "Medallon_Aqua", deck, new Vector3(0f, DeckH + 0.02f, 0f), new Vector3(4.2f, 0.012f, 4.2f), mAqua, collider: false);

            var neon = Group("Neon_Borde", deck, Vector3.zero);
            for (int i = 0; i < 48; i++)
            {
                float a = i * 7.5f;
                var p = Arc(a, DeckR - 0.15f, DeckH + 0.03f);
                var s = Prim(PrimitiveType.Cube, $"Neon_{i:00}", neon, p, new Vector3(0.35f, 0.04f, 1.0f), mAqua, collider: false);
                s.transform.localRotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(a * Mathf.Deg2Rad), 0f, Mathf.Cos(a * Mathf.Deg2Rad)), Vector3.up);
            }
        }

        static void BuildRailing(Transform deck)
        {
            var g = Group("Barandilla", deck, Vector3.zero);
            const float r = DeckR - 0.5f;
            var angles = new List<float>();
            for (float a = -84f; a <= 84.1f; a += 12f) angles.Add(a);
            for (int i = 0; i < angles.Count; i++)
            {
                var p = Arc(angles[i], r, DeckH);
                Prim(PrimitiveType.Cylinder, $"Poste_{i:00}", g, p + Vector3.up * 0.6f, new Vector3(0.12f, 0.6f, 0.12f), mChrome, collider: false);
                Prim(PrimitiveType.Sphere, $"Perilla_{i:00}", g, p + Vector3.up * 1.25f, Vector3.one * 0.2f, mAqua, collider: false);
                if (i == 0) continue;
                var q = Arc(angles[i - 1], r, DeckH);
                Vector3 mid = (p + q) * 0.5f, seg = p - q;
                var rot = Quaternion.LookRotation(seg.normalized, Vector3.up);
                var rail = Prim(PrimitiveType.Cube, $"Pasamanos_{i:00}", g, mid + Vector3.up * 1.2f, new Vector3(0.1f, 0.08f, seg.magnitude + 0.05f), mChrome);
                rail.transform.localRotation = rot;
                var glass = Prim(PrimitiveType.Cube, $"Vidrio_{i:00}", g, mid + Vector3.up * 0.7f, new Vector3(0.04f, 0.9f, seg.magnitude - 0.1f), mGlass);
                glass.transform.localRotation = rot;
            }
        }

        static void BuildTelescopes(Transform deck)
        {
            var g = Group("Telescopios", deck, Vector3.zero);
            int n = 0;
            foreach (var a in new[] { -24f, 24f })
            {
                var t = Group($"Telescopio_{++n}", g, Arc(a, 7.6f, DeckH));
                Prim(PrimitiveType.Cylinder, "Pedestal", t, new Vector3(0f, 0.55f, 0f), new Vector3(0.22f, 0.55f, 0.22f), mChrome);
                Prim(PrimitiveType.Sphere, "Rotula", t, new Vector3(0f, 1.15f, 0f), Vector3.one * 0.32f, mAqua, collider: false);
                var tube = Prim(PrimitiveType.Cylinder, "Tubo", t, new Vector3(0.45f, 1.3f, 0f), new Vector3(0.26f, 0.6f, 0.26f), mWhite, new Vector3(0f, 0f, 78f), collider: false);
                Prim(PrimitiveType.Cylinder, "Lente_Frontal", t, new Vector3(1.0f, 1.42f, 0f), new Vector3(0.34f, 0.05f, 0.34f), mAqua, new Vector3(0f, 0f, 78f), collider: false);
                Prim(PrimitiveType.Cylinder, "Ocular", t, new Vector3(-0.12f, 1.18f, 0f), new Vector3(0.15f, 0.12f, 0.15f), mChrome, new Vector3(0f, 0f, 78f), collider: false);
            }
        }

        static void BuildBenches(Transform deck)
        {
            var g = Group("Bancas", deck, Vector3.zero);
            int n = 0;
            foreach (var z in new[] { -5.2f, 0f, 5.2f })
            {
                var b = Group($"Banca_{++n}", g, new Vector3(-3.2f, DeckH, z));
                b.localRotation = Quaternion.LookRotation(Vector3.right, Vector3.up); // mira al mar
                Prim(PrimitiveType.Cube, "Asiento", b, new Vector3(0f, 0.45f, 0f), new Vector3(2.4f, 0.14f, 0.7f), mWhite);
                Prim(PrimitiveType.Cube, "Respaldo", b, new Vector3(0f, 0.9f, -0.3f), new Vector3(2.4f, 0.55f, 0.1f), mAqua, collider: false);
                foreach (var sx in new[] { -1f, 1f })
                    Prim(PrimitiveType.Cube, "Pata", b, new Vector3(sx, 0.22f, 0f), new Vector3(0.14f, 0.44f, 0.6f), mChrome, collider: false);
            }
        }

        static void BuildEntranceArch(Transform deck)
        {
            var g = Group("Arco_Entrada", deck, new Vector3(-DeckR + 0.8f, DeckH, 0f));
            foreach (var z in new[] { -2.6f, 2.6f })
                Prim(PrimitiveType.Cylinder, "Pilar", g, new Vector3(0f, 1.9f, z), new Vector3(0.2f, 1.9f, 0.2f), mChrome);
            Prim(PrimitiveType.Cube, "Viga", g, new Vector3(0f, 3.8f, 0f), new Vector3(0.25f, 0.22f, 5.6f), mWhite, collider: false);
            Prim(PrimitiveType.Cube, "Viga_Neon", g, new Vector3(0f, 3.8f, 0f), new Vector3(0.28f, 0.08f, 5.2f), mAqua, collider: false);
            Prim(PrimitiveType.Sphere, "Orbe", g, new Vector3(0f, 4.25f, 0f), Vector3.one * 0.7f, mOrb, collider: false);
        }

        static void BuildLighthouse(Transform root, Vector3 pos)
        {
            var g = Group("Faro", root, pos);
            Prim(PrimitiveType.Cylinder, "Zocalo", g, new Vector3(0f, 0.3f, 0f), new Vector3(4.4f, 0.3f, 4.4f), mFrame);
            float y = 0.6f;
            for (int i = 0; i < 6; i++)
            {
                float w = Mathf.Lerp(3.2f, 2.0f, i / 5f);
                Prim(PrimitiveType.Cylinder, $"Tramo_{i + 1}", g, new Vector3(0f, y + 0.75f, 0f), new Vector3(w, 0.75f, w), i % 2 == 0 ? mWhite : mAqua);
                y += 1.5f;
            }
            Prim(PrimitiveType.Cylinder, "Balcon", g, new Vector3(0f, y + 0.08f, 0f), new Vector3(2.9f, 0.08f, 2.9f), mChrome);
            Prim(PrimitiveType.Cylinder, "Cabina_Vidrio", g, new Vector3(0f, y + 0.85f, 0f), new Vector3(1.6f, 0.75f, 1.6f), mGlass, collider: false);
            Prim(PrimitiveType.Sphere, "Linterna", g, new Vector3(0f, y + 0.85f, 0f), Vector3.one * 0.95f, mOrb, collider: false);
            Prim(PrimitiveType.Cylinder, "Techo", g, new Vector3(0f, y + 1.75f, 0f), new Vector3(2.0f, 0.12f, 2.0f), mChrome, collider: false);
            Prim(PrimitiveType.Sphere, "Remate", g, new Vector3(0f, y + 2.0f, 0f), Vector3.one * 0.35f, mAqua, collider: false);
            var l = new GameObject("Luz").AddComponent<Light>();
            l.transform.SetParent(g, false); l.transform.localPosition = new Vector3(0f, y + 0.85f, 0f);
            l.type = LightType.Point; l.range = 22f; l.intensity = 2.5f; l.color = new Color(0.7f, 0.95f, 1f);
        }

        static void BuildGazebo(Transform root, Vector3 pos)
        {
            var g = Group("Glorieta", root, pos);
            Prim(PrimitiveType.Cylinder, "Piso", g, new Vector3(0f, 0.15f, 0f), new Vector3(7f, 0.15f, 7f), mWood);
            for (int i = 0; i < 6; i++)
            {
                var p = Arc(i * 60f, 3.1f, 0f);
                Prim(PrimitiveType.Cylinder, $"Columna_{i + 1}", g, p + new Vector3(0f, 1.9f, 0f), new Vector3(0.16f, 1.6f, 0.16f), mChrome);
            }
            Prim(PrimitiveType.Cylinder, "Anillo_Techo", g, new Vector3(0f, 3.55f, 0f), new Vector3(7.0f, 0.1f, 7.0f), mFrame, collider: false);
            // Techo cónico aproximado con tres discos decrecientes
            Prim(PrimitiveType.Cylinder, "Techo_1", g, new Vector3(0f, 3.8f, 0f), new Vector3(6.4f, 0.1f, 6.4f), mAqua, collider: false);
            Prim(PrimitiveType.Cylinder, "Techo_2", g, new Vector3(0f, 4.15f, 0f), new Vector3(4.6f, 0.1f, 4.6f), mWhite, collider: false);
            Prim(PrimitiveType.Cylinder, "Techo_3", g, new Vector3(0f, 4.45f, 0f), new Vector3(2.6f, 0.1f, 2.6f), mAqua, collider: false);
            Prim(PrimitiveType.Sphere, "Remate", g, new Vector3(0f, 4.8f, 0f), Vector3.one * 0.5f, mOrb, collider: false);
            Prim(PrimitiveType.Cube, "Mesa", g, new Vector3(0f, 0.85f, 0f), new Vector3(1.4f, 0.1f, 1.4f), mWhite);
            Prim(PrimitiveType.Cylinder, "Mesa_Pata", g, new Vector3(0f, 0.5f, 0f), new Vector3(0.2f, 0.35f, 0.2f), mChrome, collider: false);
        }

        static void BuildLamps(Transform root, float top)
        {
            var g = Group("Faroles", root, Vector3.zero);
            int n = 0;
            for (float x = 30f; x <= 70f; x += 10f)
                foreach (var z in new[] { -3.2f, 3.2f })
                {
                    n++;
                    Prim(PrimitiveType.Cylinder, $"Farol_{n}_Poste", g, new Vector3(x, top + 1.8f, z), new Vector3(0.14f, 1.8f, 0.14f), mFrame, collider: false);
                    Prim(PrimitiveType.Sphere, $"Farol_{n}_Luz", g, new Vector3(x, top + 3.9f, z), Vector3.one * 0.75f, mAqua, collider: false);
                }
        }

        static void BuildBubbles(Transform deck)
        {
            var g = Group("Burbujas", deck, Vector3.zero);
            var rng = new System.Random(77);
            for (int i = 0; i < 9; i++)
            {
                float a = Mathf.Lerp(-70f, 70f, (float)rng.NextDouble());
                float r = Mathf.Lerp(2f, 9f, (float)rng.NextDouble());
                float s = Mathf.Lerp(0.6f, 1.8f, (float)rng.NextDouble());
                Prim(PrimitiveType.Sphere, $"Burbuja_{i + 1}", g, Arc(a, r, Mathf.Lerp(3f, 8f, (float)rng.NextDouble())), Vector3.one * s, mGlass, collider: false);
            }
        }

        static void BuildBushes(Transform root, float top)
        {
            var g = Group("Arbustos", root, Vector3.zero);
            var rng = new System.Random(5);
            var mats = new[] { mBush1, mBush2, mBush3 };
            var placed = new List<Vector3>(); // x, z, diámetro
            for (int tries = 0; tries < 400 && placed.Count < 22; tries++)
            {
                float x = Mathf.Lerp(50f, 76f, (float)rng.NextDouble());
                float side = rng.Next(2) == 0 ? -1f : 1f;
                float z = side * Mathf.Lerp(7f, 21f, (float)rng.NextDouble());
                if (new Vector2(x - 70f, z).magnitude > 21f) continue; // dentro del lóbulo este
                float d = Mathf.Lerp(1.4f, 2.6f, (float)rng.NextDouble());
                bool ok = true;
                foreach (var q in placed)
                    if (new Vector2(x - q.x, z - q.y).magnitude < (d + q.z) * 0.5f + 2.2f) { ok = false; break; } // separados: 2.2 m libres
                if (!ok) continue;
                placed.Add(new Vector3(x, z, d));
                // Enterrados un 45%: nada flotando
                Prim(PrimitiveType.Sphere, $"Arbusto_{placed.Count}", g, new Vector3(x, top + d * 0.5f - d * 0.45f, z),
                     new Vector3(d, d * 0.8f, d), mats[placed.Count % 3], collider: false);
            }
        }
    }
}
