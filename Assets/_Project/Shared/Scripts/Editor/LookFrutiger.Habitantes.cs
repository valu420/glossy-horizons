// Hub final, parte 7 (HU-38 y HU-39): habitantes de gel repartidos por la isla, que pasean por ella.
//   - Prefab: Player/Prefabs/Habitante_Gel.prefab (variante de Player_Gel; si falta, se crea solo).
//   - Un punto vale si, al tirar un rayo desde arriba, lo primero que toca es suelo plano a la altura de la isla
//     (pasto, caminos, plazas): nunca el mar, la piscina, techos, bancas ni el interior de la cúpula del lobby.
//     Además debe haber espacio libre alrededor y estar lejos de otros habitantes y del jugador.
//   - Colores de la paleta de HU-02; dos vecinos cercanos nunca tienen el mismo.
//   - HU-39: malla de navegación de la isla horneada aquí (sin el paquete AI Navigation) en
//     Shared/Navegacion/NavMesh_Hub.asset. Caminos, plazas y baldosas son el área 3 (más barata que el pasto)
//     y el tronco de cada árbol es no caminable. Solo se hornea si falta el asset o con el menú
//     Glossy Horizons > Hub > Hornear navegación de la isla (hazlo si cambias la isla o sus zonas).
// Se regeneran con el resto del fondo (GenerarTodo) o con: Glossy Horizons > Hub > Habitantes de la isla.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static partial class LookFrutiger
{
    const string RaizHabitantes = "Habitantes_Isla";
    const int CantidadHabitantes = 50;          // HU-38: la isla se siente habitada
    const float SeparacionHabitantes = 4.5f;    // metros mínimos entre dos habitantes
    const float RadioVecinosColor = 16f;        // dentro de este radio no se repite color
    const string RutaNavMesh = "Assets/_Project/Shared/Navegacion/NavMesh_Hub.asset";

    // puntos de interés (centro XZ y radio): lobby, zona de juegos, piscina, jardín botánico y mirador
    static readonly (float x, float z, float r)[] PuntosInteres =
        { (0f, 0f, 22f), (0f, 70f, 20f), (0f, -76f, 18f), (-84f, 0f, 20f), (84f, 0f, 8f) };
    static readonly string[] MaterialesCamino = { "Camino", "Grava", "Adoquin", "Baldosa", "Plaza", "PisoBlanco" };

    [MenuItem("Glossy Horizons/Hub/Hornear navegación de la isla")]
    static void MenuHornear()
    {
        var escena = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        if (!GlossyHorizons.Core.EditorTools.HubIslandBuilder.TryTop(escena, out float alto)) { Debug.LogWarning("[Habitantes] No encuentro la isla."); return; }
        var datos = HornearNavegacion(escena, alto, LimitesIsla(alto));
        if (datos != null) Debug.Log("[Habitantes] Navegación horneada. Vuelve a generar los habitantes (o dale Play).");
    }

    static Bounds LimitesIsla(float alturaSuelo)
    {
        var suelo = GameObject.Find("Isla_Superficie");
        return suelo != null && suelo.GetComponent<Renderer>() != null
            ? suelo.GetComponent<Renderer>().bounds
            : new Bounds(new Vector3(-12f, alturaSuelo, -4f), new Vector3(230f, 10f, 230f));
    }

    static bool EsDeFondo(Transform t)
    {
        string raiz = t.root.name;
        return raiz == RaizHabitantes || raiz == "Globos_Aerostaticos" || raiz == "Dirigible_GlossyHorizons"
            || raiz.StartsWith("Pantalla_") || raiz == "Ciudad_Fondo" || raiz == "Auroras" || raiz.StartsWith("Nubes");
    }

    static NavMeshData HornearNavegacion(UnityEngine.SceneManagement.Scene escena, float alturaSuelo, Bounds limites)
    {
        try
        {
            EditorUtility.DisplayProgressBar("Glossy Horizons", "Horneando la navegación de la isla...", 0.5f);
            Physics.SyncTransforms();
            var zona = new Bounds(new Vector3(limites.center.x, alturaSuelo + 12f, limites.center.z),
                                  new Vector3(limites.size.x + 10f, 30f, limites.size.z + 10f));
            var fuentes = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(zona, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), fuentes);
            // fuera: el jugador, los propios habitantes, triggers y lo decorativo del fondo
            fuentes.RemoveAll(f => f.component != null && (f.component is CharacterController
                || (f.component is Collider c && c.isTrigger) || EsDeFondo(f.component.transform)
                || f.component.GetComponentInParent<HabitanteGel>() != null || f.component.GetComponentInParent<ControladorJugador>() != null));

            // caminos, plazas y baldosas: área 3 (en la malla gana el área de número mayor)
            int caminos = 0;
            foreach (var mr in Object.FindObjectsByType<MeshRenderer>())
            {
                if (mr.gameObject.scene != escena || EsDeFondo(mr.transform) || mr.sharedMaterial == null) continue;
                string mat = mr.sharedMaterial.name;
                if (!System.Array.Exists(MaterialesCamino, k => mat.Contains(k))) continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || !zona.Intersects(mr.bounds)) continue;
                fuentes.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Mesh, sourceObject = mf.sharedMesh,
                                                     transform = mf.transform.localToWorldMatrix, area = NavegacionIsla.AreaCamino });
                caminos++;
            }
            // troncos de los árboles (no tienen collider): no caminables
            int troncos = 0;
            var arboles = System.Array.Find(escena.GetRootGameObjects(), g => g.name == "Arboles_Isla");
            if (arboles != null)
                foreach (Transform a in arboles.transform)
                {
                    fuentes.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, size = new Vector3(0.9f, 3f, 0.9f),
                                                         transform = Matrix4x4.TRS(a.position + Vector3.up * 1.5f, Quaternion.identity, Vector3.one), area = 1 });
                    troncos++;
                }

            var ajustes = NavMesh.GetSettingsByID(0);
            ajustes.agentRadius = 0.35f; ajustes.agentHeight = 1.6f; ajustes.agentClimb = 0.35f; ajustes.agentSlope = 40f;
            ajustes.overrideVoxelSize = true; ajustes.voxelSize = 0.15f;
            ajustes.overrideTileSize = true; ajustes.tileSize = 256;
            var t0 = EditorApplication.timeSinceStartup;
            var datos = NavMeshBuilder.BuildNavMeshData(ajustes, fuentes, zona, Vector3.zero, Quaternion.identity);
            if (datos == null) { Debug.LogError("[Habitantes] No se pudo hornear la navegación."); return null; }
            datos.name = "NavMesh_Hub";
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Shared/Navegacion")) AssetDatabase.CreateFolder("Assets/_Project/Shared", "Navegacion");
            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(RutaNavMesh) != null) AssetDatabase.DeleteAsset(RutaNavMesh);
            AssetDatabase.CreateAsset(datos, RutaNavMesh);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Habitantes] Navegación horneada en {EditorApplication.timeSinceStartup - t0:0.0} s: {fuentes.Count} fuentes, {caminos} caminos, {troncos} troncos.");
            return datos;
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    [MenuItem("Glossy Horizons/Hub/Habitantes de la isla")]
    static void MenuHabitantes()
    {
        var escena = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        int n = Habitantes(escena);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(escena);
        var raiz = System.Array.Find(escena.GetRootGameObjects(), g => g.name == RaizHabitantes);
        if (raiz != null) Selection.activeGameObject = raiz;
        Debug.Log($"{n} habitantes en la isla. Dale Play para verlos y guarda la escena (Ctrl+S).");
    }

    static int Habitantes(UnityEngine.SceneManagement.Scene escena)
    {
        foreach (var go in escena.GetRootGameObjects()) if (go.name == RaizHabitantes) Object.DestroyImmediate(go);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrearPrefabHabitante.RutaHabitante);
        if (prefab == null || prefab.GetComponent<NavMeshAgent>() == null) prefab = CrearPrefabHabitante.Crear();   // el de HU-38 no tenía agente
        if (prefab == null) return 0;

        if (!GlossyHorizons.Core.EditorTools.HubIslandBuilder.TryTop(escena, out float alturaSuelo))
        { Debug.LogWarning("[Habitantes] No encuentro la isla: créala primero."); return 0; }
        var limites = LimitesIsla(alturaSuelo);
        var navMesh = AssetDatabase.LoadAssetAtPath<NavMeshData>(RutaNavMesh);
        if (navMesh == null) navMesh = HornearNavegacion(escena, alturaSuelo, limites);
        if (navMesh == null) return 0;
        var navTemporal = NavMesh.AddNavMeshData(navMesh);   // para comprobar que cada punto sea caminable

        // el jugador aparece en el centro: los habitantes no se ponen encima
        var jugador = Object.FindAnyObjectByType<ControladorJugador>();
        var inicioJugador = jugador != null ? jugador.transform.position : Vector3.zero;

        // colliders temporales en lo que no tiene, para que los rayos "vean" edificios, bancas, árboles...
        string[] ignorar = { "Pasto", "Flor", "Cesped", "Hierba", "Burbuja", "Nube", "Ciudad", "Cielo", "Auroras",
                             "Globos_Aerostaticos", "Dirigible", RaizHabitantes, "Pantalla_" };
        var temporales = new List<MeshCollider>();
        foreach (var mf in Object.FindObjectsByType<MeshFilter>())
        {
            if (mf.sharedMesh == null || mf.GetComponent<Collider>() != null || mf.gameObject.scene != escena) continue;
            string ruta = mf.transform.root.name + "/" + mf.name;
            bool saltar = false;
            foreach (var t in ignorar) if (ruta.Contains(t)) { saltar = true; break; }
            if (saltar) continue;
            var mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            temporales.Add(mc);
        }
        Physics.SyncTransforms();

        var puestos = new List<Vector3>();
        var colores = new List<int>();
        var azar = new System.Random(38);
        float Az(float a, float b) => a + (float)azar.NextDouble() * (b - a);

        bool Valido(Vector3 xz, out Vector3 punto)
        {
            punto = default;
            if (!Physics.Raycast(new Vector3(xz.x, alturaSuelo + 120f, xz.z), Vector3.down, out var hit, 300f, ~0, QueryTriggerInteraction.Ignore)) return false;
            // suelo plano a la altura de la isla (pasto, caminos o plazas); no techos, bancas, agua ni mar
            if (hit.normal.y < 0.9f || Mathf.Abs(hit.point.y - alturaSuelo) > 0.3f) return false;
            if ((hit.point - inicioJugador).sqrMagnitude < 6f * 6f) return false;
            if (!NavMesh.SamplePosition(hit.point, out var nav, 0.5f, NavMesh.AllAreas) || Mathf.Abs(nav.position.y - alturaSuelo) > 0.6f) return false;
            foreach (var q in puestos) if ((q - hit.point).sqrMagnitude < SeparacionHabitantes * SeparacionHabitantes) return false;
            // espacio para el cuerpo: nada alrededor salvo el suelo que pisa
            foreach (var c in Physics.OverlapCapsule(hit.point + Vector3.up * 0.55f, hit.point + Vector3.up * 1.6f, 0.5f, ~0, QueryTriggerInteraction.Ignore))
                if (c != hit.collider && c.bounds.max.y > hit.point.y + 0.25f) return false;
            punto = hit.point;
            return true;
        }

        var raiz = new GameObject(RaizHabitantes);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(raiz, escena);
        var navegacion = raiz.AddComponent<NavegacionIsla>();
        navegacion.datos = navMesh;
        navegacion.alturaSuelo = alturaSuelo;
        navegacion.puntosInteres = System.Array.ConvertAll(PuntosInteres, q => new Vector3(q.x, alturaSuelo, q.z));
        navegacion.radiosInteres = System.Array.ConvertAll(PuntosInteres, q => q.r);
        try
        {
            for (int k = 0; k < 4000 && puestos.Count < CantidadHabitantes; k++)
            {
                var xz = new Vector3(Az(limites.min.x, limites.max.x), 0f, Az(limites.min.z, limites.max.z));
                if (!Valido(xz, out var p)) continue;

                // color: uno de la paleta que no tenga ningún vecino cercano
                var prohibidos = new HashSet<int>();
                for (int i = 0; i < puestos.Count; i++)
                    if ((puestos[i] - p).sqrMagnitude < RadioVecinosColor * RadioVecinosColor) prohibidos.Add(colores[i]);
                var libres = new List<int>();
                for (int c = 0; c < ColorPersonaje.Paleta.Length; c++) if (!prohibidos.Contains(c)) libres.Add(c);
                int color = libres.Count > 0 ? libres[azar.Next(libres.Count)] : azar.Next(ColorPersonaje.Paleta.Length);

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, escena);
                go.name = $"Habitante_{puestos.Count + 1:00}_{ColorPersonaje.Paleta[color].nombre.Replace(' ', '_')}";
                go.transform.SetParent(raiz.transform, true);
                go.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, Az(0f, 360f), 0f));
                var cp = go.GetComponent<ColorPersonaje>();
                if (cp != null) { cp.sigueAlJugador = false; cp.colorPropio = color; }

                puestos.Add(p); colores.Add(color);
            }
        }
        finally
        {
            foreach (var mc in temporales) if (mc != null) Object.DestroyImmediate(mc);
            navTemporal.Remove();
        }

        if (puestos.Count < CantidadHabitantes)
            Debug.LogWarning($"[Habitantes] Solo cupieron {puestos.Count} de {CantidadHabitantes}: baja la separación o la cantidad.");
        else Debug.Log($"[Habitantes] {puestos.Count} habitantes repartidos por la isla.");
        return puestos.Count;
    }
}
