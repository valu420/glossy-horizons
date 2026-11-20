// Herramientas de editor del jugador (HU-01 y HU-04).
// Menú de Unity: Glossy Horizons
//   > Crear prefab del jugador          -> Player/Prefabs/Player_Gel.prefab
//   > Crear escena de prueba del jugador -> Player/Pruebas/Test_Jugador.unity
// Estructura del prefab:
//   Player_Gel            (raíz: CharacterController + ControladorJugador + AnimacionGel + ColorPersonaje + CamaraJugador)
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
    const string RutaTexturaNube = "Assets/_Project/Player/Textures/T_BurbujaNube.png";
    const string RutaTexturaLetraZ = "Assets/_Project/Player/Textures/T_LetraZ.png";
    const string RutaTexturaExclamacion = "Assets/_Project/Player/Textures/T_BurbujaExclamacion.png";
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

            // El modelo debe mirar hacia adelante (+Z). Los ojos están en la cara:
            // si quedan detrás del centro de la cabeza, el modelo está al revés y se gira 180°.
            Transform cabezaModelo = null, ojoModelo = null;
            foreach (Transform t in personaje.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Cabeza") cabezaModelo = t;
                if (t.name == "Ojo_L") ojoModelo = t;
            }
            if (cabezaModelo != null && ojoModelo != null &&
                raiz.transform.InverseTransformPoint(ojoModelo.position).z < raiz.transform.InverseTransformPoint(cabezaModelo.position).z)
            {
                personaje.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * personaje.transform.localRotation;
                Debug.Log("El modelo miraba hacia atrás: se giró 180° para que mire hacia adelante.");
            }

            var aro = (GameObject)PrefabUtility.InstantiatePrefab(anillo, raiz.transform);
            aro.transform.localPosition = Vector3.zero;

            // HU-04: cuerpo físico, movimiento y cámara
            var cc = raiz.AddComponent<CharacterController>();
            cc.height = 1.6f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0f, 0.8f, 0f);
            cc.stepOffset = 0.3f;
            raiz.AddComponent<ControladorJugador>();
            var anim = raiz.AddComponent<AnimacionGel>();   // HU-05
            anim.texturaNube = AssetDatabase.LoadAssetAtPath<Texture2D>(RutaTexturaNube);
            anim.texturaLetraZ = AssetDatabase.LoadAssetAtPath<Texture2D>(RutaTexturaLetraZ);
            anim.texturaExclamacion = AssetDatabase.LoadAssetAtPath<Texture2D>(RutaTexturaExclamacion);
            raiz.AddComponent<ColorPersonaje>();   // HU-02: usa el color elegido
            // HUD con el botón orbe y el panel de colores: se crea solo en cualquier escena
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(CrearHUDJugador.RutaPrefab);
            if (hud == null) hud = CrearHUDJugador.Crear();
            raiz.AddComponent<CargarHUD>().prefabHUD = hud;
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
        var animPrefab = prefab != null ? prefab.GetComponent<AnimacionGel>() : null;
        if (prefab == null || prefab.GetComponent<ControladorJugador>() == null || prefab.GetComponent<ColorPersonaje>() == null || prefab.GetComponent<CargarHUD>() == null || animPrefab == null || animPrefab.texturaNube == null || animPrefab.texturaLetraZ == null)
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
