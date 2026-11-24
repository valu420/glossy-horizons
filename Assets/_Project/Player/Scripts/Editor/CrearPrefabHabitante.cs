// HU-38: prefab del habitante de gel (NPC) como VARIANTE de Player_Gel: si cambia el personaje (modelo, material,
// animación), los habitantes cambian con él.
// Menú: Glossy Horizons > Crear prefab del habitante  ->  Player/Prefabs/Habitante_Gel.prefab
// Diferencias con el jugador:
//   - Sin ControladorJugador, CharacterController, CamaraJugador ni CargarHUD (no se controla ni tiene HUD).
//   - Anillo_Luz desactivado: el aro cian del suelo es lo que distingue al jugador.
//   - ColorPersonaje con color propio (no sigue el color que elige el jugador).
//   - HabitanteGel (su "cerebro") + cápsula de colisión: el jugador choca con él y no lo puede empujar.
//   - No se duerme estando quieto (eso queda para el jugador).
using UnityEditor;
using UnityEngine;

public static class CrearPrefabHabitante
{
    const string RutaJugador = "Assets/_Project/Player/Prefabs/Player_Gel.prefab";
    public const string RutaHabitante = "Assets/_Project/Player/Prefabs/Habitante_Gel.prefab";

    [MenuItem("Glossy Horizons/Crear prefab del habitante")]
    static void Menu()
    {
        var p = Crear();
        if (p != null) { Selection.activeObject = p; EditorGUIUtility.PingObject(p); }
    }

    public static GameObject Crear()
    {
        var jugador = AssetDatabase.LoadAssetAtPath<GameObject>(RutaJugador);
        if (jugador == null) { Debug.LogError("[Habitantes] Falta " + RutaJugador + " (crea primero el prefab del jugador)."); return null; }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(jugador);
        try
        {
            go.name = "Habitante_Gel";
            // quitar lo que es solo del jugador (el orden importa: ControladorJugador exige el CharacterController)
            Quitar<CargarHUD>(go); Quitar<CamaraJugador>(go); Quitar<ControladorJugador>(go); Quitar<CharacterController>(go);

            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (t.name == "Anillo_Luz") t.gameObject.SetActive(false);

            var color = go.GetComponent<ColorPersonaje>();
            if (color != null) color.sigueAlJugador = false;

            var anim = go.GetComponent<AnimacionGel>();
            if (anim != null) { anim.segundosParaAdormecerse = 1e6f; anim.segundosParaDormir = 1e6f; }

            if (go.GetComponent<HabitanteGel>() == null) go.AddComponent<HabitanteGel>();
            var capsula = go.GetComponent<CapsuleCollider>();
            if (capsula == null) capsula = go.AddComponent<CapsuleCollider>();
            capsula.height = 1.6f; capsula.radius = 0.3f; capsula.center = new Vector3(0f, 0.8f, 0f);
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;          // lo mueve su código (HU-39), no la física; el jugador no lo empuja
            rb.useGravity = false;

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, RutaHabitante, out bool ok);
            if (!ok) { Debug.LogError("[Habitantes] No se pudo guardar " + RutaHabitante); return null; }
            Debug.Log("[Habitantes] Prefab creado: " + RutaHabitante + " (variante de Player_Gel).");
            return prefab;
        }
        finally { Object.DestroyImmediate(go); }
    }

    static void Quitar<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c != null) Object.DestroyImmediate(c);
    }
}
