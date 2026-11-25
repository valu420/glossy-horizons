using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Cada vez que se da Play con el Hub abierto, justo antes de entrar al juego se vuelve a generar todo
/// el fondo: cielo, ciudad, árboles de la isla y auroras (LookFrutiger.GenerarTodo).
/// APAGADO por defecto: el Hub ya se guarda completo dentro de Hub.unity, así que Play no regenera nada.
/// Se enciende (por si se quiere regenerar el fondo al dar Play) en: Glossy Horizons > Hub > Generar todo al dar Play.
/// </summary>
[InitializeOnLoad]
public static class GenerarAlDarPlay
{
    const string RutaHub = "Assets/_Project/Core/Scenes/Hub.unity";
    const string Clave = "GlossyHorizons.GenerarAlDarPlay";
    const string Menu_ = "Glossy Horizons/Hub/Generar todo al dar Play";

    static GenerarAlDarPlay()
    {
        EditorApplication.playModeStateChanged += estado =>
        {
            if (estado != PlayModeStateChange.ExitingEditMode || !EditorPrefs.GetBool(Clave, false)) return;
            var escena = EditorSceneManager.GetActiveScene();
            if (!escena.IsValid() || !escena.isLoaded || escena.path != RutaHub) return;
            LookFrutiger.GenerarTodo(escena);
        };
    }

    [MenuItem(Menu_)]
    static void Alternar() => EditorPrefs.SetBool(Clave, !EditorPrefs.GetBool(Clave, false));

    [MenuItem(Menu_, true)]
    static bool AlternarValidar()
    {
        Menu.SetChecked(Menu_, EditorPrefs.GetBool(Clave, false));
        return true;
    }
}
