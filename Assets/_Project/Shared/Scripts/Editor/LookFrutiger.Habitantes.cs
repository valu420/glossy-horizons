// Hub final, parte 7 (HU-38): habitantes de gel repartidos por la isla.
//   - Prefab: Player/Prefabs/Habitante_Gel.prefab (variante de Player_Gel; si falta, se crea solo).
//   - Un punto vale si, al tirar un rayo desde arriba, lo primero que toca es suelo plano a la altura de la isla
//     (pasto, caminos, plazas): nunca el mar, la piscina, techos, bancas ni el interior de la cúpula del lobby.
//     Además debe haber espacio libre alrededor y estar lejos de otros habitantes y del jugador.
//   - Colores de la paleta de HU-02; dos vecinos cercanos nunca tienen el mismo.
// Se regeneran con el resto del fondo (GenerarTodo) o con: Glossy Horizons > Hub > Habitantes de la isla.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static partial class LookFrutiger
{
    const string RaizHabitantes = "Habitantes_Isla";
    const int CantidadHabitantes = 24;          // HU-38: entre 20 y 30
    const float SeparacionHabitantes = 6f;      // metros mínimos entre dos habitantes
    const float RadioVecinosColor = 16f;        // dentro de este radio no se repite color

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
        if (prefab == null) prefab = CrearPrefabHabitante.Crear();
        if (prefab == null) return 0;

        if (!GlossyHorizons.Core.EditorTools.HubIslandBuilder.TryTop(escena, out float alturaSuelo))
        { Debug.LogWarning("[Habitantes] No encuentro la isla: créala primero."); return 0; }
        var suelo = GameObject.Find("Isla_Superficie");
        var limites = suelo != null && suelo.GetComponent<Renderer>() != null
            ? suelo.GetComponent<Renderer>().bounds
            : new Bounds(new Vector3(-12f, alturaSuelo, -4f), new Vector3(230f, 10f, 230f));

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
            foreach (var q in puestos) if ((q - hit.point).sqrMagnitude < SeparacionHabitantes * SeparacionHabitantes) return false;
            // espacio para el cuerpo: nada alrededor salvo el suelo que pisa
            foreach (var c in Physics.OverlapCapsule(hit.point + Vector3.up * 0.55f, hit.point + Vector3.up * 1.6f, 0.5f, ~0, QueryTriggerInteraction.Ignore))
                if (c != hit.collider && c.bounds.max.y > hit.point.y + 0.25f) return false;
            punto = hit.point;
            return true;
        }

        var raiz = new GameObject(RaizHabitantes);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(raiz, escena);
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
        }

        if (puestos.Count < CantidadHabitantes)
            Debug.LogWarning($"[Habitantes] Solo cupieron {puestos.Count} de {CantidadHabitantes}: baja la separación o la cantidad.");
        else Debug.Log($"[Habitantes] {puestos.Count} habitantes repartidos por la isla.");
        return puestos.Count;
    }
}
