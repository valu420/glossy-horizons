using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Cielo Frutiger Aero automático: al cargar la escena pone el skybox "Sky_FrutigerNubes"
/// (degradado azul + nubes fotográficas que se mueven), la niebla celeste a juego y le dice
/// al shader dónde está el sol (la Directional Light), para que el sol del cielo y las sombras coincidan.
/// En la escena Hub se crea solo si no está; en otras escenas basta con agregar este componente.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class CieloFrutiger : MonoBehaviour
{
    public const string RutaRecurso = "Sky_FrutigerNubes"; // Assets/_Project/Shared/Resources/Sky_FrutigerNubes.mat
    static readonly string[] EscenasAutomaticas = { "Hub" };

    [Tooltip("Material con el shader Glossy/CieloFrutiger. Vacío = se carga de Resources.")]
    public Material cielo;
    [Tooltip("Luz del sol. Vacío = la Directional Light de la escena.")]
    public Light sol;

    [Header("Niebla (perspectiva aérea)")]
    public bool aplicarNiebla = true;
    public Color colorNiebla = new Color(0.74f, 0.87f, 0.98f);
    public float nieblaInicio = 120f;
    public float nieblaFin = 900f;

    static readonly int DirSol = Shader.PropertyToID("_CieloDirSol");

    void OnEnable() => Aplicar();

    void OnDisable() => Shader.SetGlobalVector(DirSol, Vector4.zero);

    void Update() => ActualizarSol();

    public void Aplicar()
    {
        if (cielo == null) cielo = Resources.Load<Material>(RutaRecurso);
        if (cielo == null)
        {
            Debug.LogWarning("[CieloFrutiger] No encuentro el material Resources/" + RutaRecurso);
            return;
        }
        RenderSettings.skybox = cielo;
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        if (aplicarNiebla)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = colorNiebla;
            RenderSettings.fogStartDistance = nieblaInicio;
            RenderSettings.fogEndDistance = nieblaFin;
        }
        ActualizarSol();
        DynamicGI.UpdateEnvironment();
    }

    void ActualizarSol()
    {
        if (sol == null) sol = BuscarSol();
        if (sol == null) return;
        Vector3 haciaSol = -sol.transform.forward;
        Shader.SetGlobalVector(DirSol, new Vector4(haciaSol.x, haciaSol.y, haciaSol.z, 1f));
        if (RenderSettings.sun == null) RenderSettings.sun = sol;
    }

    static Light BuscarSol()
    {
        if (RenderSettings.sun != null) return RenderSettings.sun;
        foreach (var l in FindObjectsByType<Light>())
            if (l.type == LightType.Directional) return l;
        return null;
    }

    // --- creación automática al cargar el Hub (en juego)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Iniciar()
    {
        SceneManager.sceneLoaded -= AlCargar;
        SceneManager.sceneLoaded += AlCargar;
        Asegurar(SceneManager.GetActiveScene());
    }

    static void AlCargar(Scene escena, LoadSceneMode modo) => Asegurar(escena);

    /// <summary>Crea el cielo en la escena si es una de las automáticas y aún no lo tiene.</summary>
    public static CieloFrutiger Asegurar(Scene escena)
    {
        if (System.Array.IndexOf(EscenasAutomaticas, escena.name) < 0) return null;
        foreach (var raiz in escena.GetRootGameObjects())
        {
            var existente = raiz.GetComponentInChildren<CieloFrutiger>(true);
            if (existente != null) return existente;
        }
        var go = new GameObject("CieloFrutiger");
        SceneManager.MoveGameObjectToScene(go, escena);
        return go.AddComponent<CieloFrutiger>();
    }
}
