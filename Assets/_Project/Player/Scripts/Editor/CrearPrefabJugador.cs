// Herramientas de editor del jugador (HU-01 y HU-04).
// Menú de Unity: Glossy Horizons
//   > Crear prefab del jugador          -> Player/Prefabs/Player_Gel.prefab
//   > Crear escena de prueba del jugador -> Player/Pruebas/Test_Jugador.unity
// Estructura del prefab:
//   Player_Gel            (raíz: CharacterController + ControladorJugador + CamaraJugador)
//   ├── Personaje_Gel     (modelo de gel con sus ojos)
//   └── Anillo_Luz        (aro cian en el suelo)
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CrearPrefabJugador
{
    const string RutaModelo = "Assets/_Project/Player/Models/Personaje_Gel.fbx";
    const string RutaAnillo = "Assets/_Project/Player/Models/Anillo_Luz.fbx";
    const string RutaPrefab = "Assets/_Project/Player/Prefabs/Player_Gel.prefab";
    const string CarpetaPruebas = "Assets/_Project/Player/Pruebas";
    const string RutaEscenaPrueba = CarpetaPruebas + "/Test_Jugador.unity";

    [MenuItem("Glossy Horizons/Crear prefab del jugador")]
    static void Crear()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefab) != null &&
            !EditorUtility.DisplayDialog("Crear prefab del jugador",
                "Ya existe Player_Gel.prefab. ¿Reemplazarlo?", "Reemplazar", "Cancelar"))
            return;
        CrearPrefab();
    }

    static GameObject CrearPrefab()
    {
        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(RutaModelo);
        var anillo = AssetDatabase.LoadAssetAtPath<GameObject>(RutaAnillo);
        if (modelo == null || anillo == null)
        {
            EditorUtility.DisplayDialog("Crear prefab del jugador",
                "No encontré el modelo o el anillo en Player/Models.", "OK");
            return null;
        }

        var raiz = new GameObject("Player_Gel");
        try
        {
            var personaje = (GameObject)PrefabUtility.InstantiatePrefab(modelo, raiz.transform);
            personaje.transform.localPosition = Vector3.zero;

            var aro = (GameObject)PrefabUtility.InstantiatePrefab(anillo, raiz.transform);
            aro.transform.localPosition = Vector3.zero;

            // HU-04: cuerpo físico, movimiento y cámara
            var cc = raiz.AddComponent<CharacterController>();
            cc.height = 1.6f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0f, 0.8f, 0f);
            cc.stepOffset = 0.3f;
            raiz.AddComponent<ControladorJugador>();
            var camara = raiz.AddComponent<CamaraJugador>();
            foreach (Transform t in personaje.GetComponentsInChildren<Transform>(true))
                if (t.name == "Cabeza") { camara.cabeza = t; break; }

            var prefab = PrefabUtility.SaveAsPrefabAsset(raiz, RutaPrefab, out bool exito);
            if (exito)
            {
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
                Debug.Log("Prefab creado: " + RutaPrefab);
                return prefab;
            }
            Debug.LogError("No se pudo crear el prefab en " + RutaPrefab);
            return null;
        }
        finally
        {
            Object.DestroyImmediate(raiz);
        }
    }

    [MenuItem("Glossy Horizons/Crear escena de prueba del jugador")]
    static void CrearEscenaPrueba()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefab);
        if (prefab == null || prefab.GetComponent<ControladorJugador>() == null)
            prefab = CrearPrefab();   // crea o actualiza el prefab con los scripts de HU-04
        if (prefab == null) return;

        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var piso = GameObject.CreatePrimitive(PrimitiveType.Plane);
        piso.name = "Piso";
        piso.transform.localScale = new Vector3(4f, 1f, 4f);   // 40 x 40 m

        Caja("Pared", new Vector3(0f, 1f, 6f), new Vector3(8f, 2f, 0.5f));
        Caja("Pared lateral", new Vector3(-6f, 1f, 0f), new Vector3(0.5f, 2f, 8f));
        Caja("Caja para saltar", new Vector3(3f, 0.4f, 2f), new Vector3(1.5f, 0.8f, 1.5f));
        Caja("Escalon", new Vector3(-2f, 0.15f, 3f), new Vector3(2f, 0.3f, 1f));

        var jugador = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        jugador.transform.position = Vector3.zero;

        if (!AssetDatabase.IsValidFolder(CarpetaPruebas))
            AssetDatabase.CreateFolder("Assets/_Project/Player", "Pruebas");
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), RutaEscenaPrueba);
        Debug.Log("Escena de prueba creada: " + RutaEscenaPrueba + "  (dale Play)");
    }

    static void Caja(string nombre, Vector3 posicion, Vector3 tamano)
    {
        var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
        c.name = nombre;
        c.transform.position = posicion;
        c.transform.localScale = tamano;
    }
}
