// Herramienta de editor (HU-01): crea el prefab del jugador con un clic.
// Menú de Unity: Glossy Horizons > Crear prefab del jugador
// Estructura que genera:
//   Player_Gel            (raíz vacía en 0,0,0: aquí van los scripts de control)
//   ├── Personaje_Gel     (modelo de gel con sus ojos)
//   └── Anillo_Luz        (aro cian en el suelo)
using UnityEditor;
using UnityEngine;

public static class CrearPrefabJugador
{
    const string RutaModelo = "Assets/_Project/Player/Models/Personaje_Gel.fbx";
    const string RutaAnillo = "Assets/_Project/Player/Models/Anillo_Luz.fbx";
    const string RutaPrefab = "Assets/_Project/Player/Prefabs/Player_Gel.prefab";

    [MenuItem("Glossy Horizons/Crear prefab del jugador")]
    static void Crear()
    {
        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(RutaModelo);
        var anillo = AssetDatabase.LoadAssetAtPath<GameObject>(RutaAnillo);
        if (modelo == null || anillo == null)
        {
            EditorUtility.DisplayDialog("Crear prefab del jugador",
                "No encontré el modelo o el anillo en Player/Models.", "OK");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefab) != null &&
            !EditorUtility.DisplayDialog("Crear prefab del jugador",
                "Ya existe Player_Gel.prefab. ¿Reemplazarlo?", "Reemplazar", "Cancelar"))
            return;

        var raiz = new GameObject("Player_Gel");
        try
        {
            var personaje = (GameObject)PrefabUtility.InstantiatePrefab(modelo, raiz.transform);
            personaje.transform.localPosition = Vector3.zero;

            var aro = (GameObject)PrefabUtility.InstantiatePrefab(anillo, raiz.transform);
            aro.transform.localPosition = Vector3.zero;

            var prefab = PrefabUtility.SaveAsPrefabAsset(raiz, RutaPrefab, out bool exito);
            if (exito)
            {
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
                Debug.Log("Prefab creado: " + RutaPrefab);
            }
            else
            {
                Debug.LogError("No se pudo crear el prefab en " + RutaPrefab);
            }
        }
        finally
        {
            Object.DestroyImmediate(raiz);
        }
    }
}
