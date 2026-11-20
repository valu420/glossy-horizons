// Herramienta de editor: mantiene la pestaña Game siempre en Full HD (1920x1080),
// con la escala al mínimo para que se vea la pantalla completa (sin zoom ni recortes).
// Se aplica sola al abrir Unity y cada vez que se le da Play.
// Menú: Glossy Horizons > Pantalla > Fijar en Full HD (activar / desactivar)
//       Glossy Horizons > Pantalla > Aplicar ahora
// Solo cambia la vista del editor de cada uno; no toca el proyecto ni las builds.
// Unity no tiene una función pública para esto, así que usa reflexión sobre la GameView;
// si en otra versión de Unity algo cambia, solo muestra un aviso en la consola.
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class FijarPantallaJuego
{
    const int Ancho = 1920, Alto = 1080;
    const string Clave = "GlossyHorizons.FijarFullHD";
    const string MenuFijar = "Glossy Horizons/Pantalla/Fijar en Full HD";
    const BindingFlags Todo = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    static bool Activo
    {
        get => EditorPrefs.GetBool(Clave, true);
        set => EditorPrefs.SetBool(Clave, value);
    }

    static FijarPantallaJuego()
    {
        EditorApplication.delayCall += () => { if (Activo) Aplicar(false); };
        EditorApplication.playModeStateChanged += estado =>
        {
            if (Activo && (estado == PlayModeStateChange.ExitingEditMode || estado == PlayModeStateChange.EnteredPlayMode))
                Aplicar(false);
        };
    }

    [MenuItem(MenuFijar)]
    static void Alternar()
    {
        Activo = !Activo;
        if (Activo) Aplicar(true);
    }

    [MenuItem(MenuFijar, true)]
    static bool AlternarValidar()
    {
        Menu.SetChecked(MenuFijar, Activo);
        return true;
    }

    [MenuItem("Glossy Horizons/Pantalla/Aplicar ahora")]
    static void AplicarAhora() => Aplicar(true);

    static void Aplicar(bool avisar)
    {
        try
        {
            var asm = typeof(Editor).Assembly;
            var tipoVista = asm.GetType("UnityEditor.GameView");
            var tipoTamanos = asm.GetType("UnityEditor.GameViewSizes");
            if (tipoVista == null || tipoTamanos == null) throw new Exception("no se encontró GameView");

            // Grupo de tamaños actual (Standalone en PC)
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(tipoTamanos);
            var tamanos = singleton.GetProperty("instance", Todo).GetValue(null);
            var grupoActual = tipoTamanos.GetProperty("currentGroup", Todo)?.GetValue(tamanos);
            if (grupoActual == null) throw new Exception("no se encontró el grupo de tamaños");

            int indice = BuscarOCrear(asm, grupoActual);

            // Todas las pestañas Game abiertas (sin abrir una nueva ni robar el foco)
            var vistas = Resources.FindObjectsOfTypeAll(tipoVista);
            foreach (var vista in vistas)
            {
                var prop = tipoVista.GetProperty("selectedSizeIndex", Todo);
                if (prop != null) prop.SetValue(vista, indice);
                else tipoVista.GetMethod("SizeSelectionCallback", Todo)?.Invoke(vista, new object[] { indice, null });

                // Escala al mínimo = se ve completa dentro de la pestaña
                var minimo = tipoVista.GetProperty("minScale", Todo)?.GetValue(vista);
                var ajustar = tipoVista.GetMethod("SnapZoom", Todo);
                if (minimo is float m && ajustar != null) ajustar.Invoke(vista, new object[] { m });
                ((EditorWindow)vista).Repaint();
            }
            if (avisar) Debug.Log("Pestaña Game fijada en Full HD (1920x1080).");
        }
        catch (Exception e)
        {
            Debug.LogWarning("No se pudo fijar la pestaña Game en Full HD (" + e.Message +
                             "). Elige 'Full HD (1920x1080)' a mano en la barra de la pestaña Game.");
        }
    }

    // Busca "Full HD (1920x1080)" en la lista; si no está, la agrega
    static int BuscarOCrear(Assembly asm, object grupo)
    {
        var tipoGrupo = grupo.GetType();
        int total = (int)tipoGrupo.GetMethod("GetTotalCount", Todo).Invoke(grupo, null);
        var obtener = tipoGrupo.GetMethod("GetGameViewSize", Todo);
        for (int i = 0; i < total; i++)
        {
            var t = obtener.Invoke(grupo, new object[] { i });
            var tt = t.GetType();
            int w = (int)tt.GetProperty("width", Todo).GetValue(t);
            int h = (int)tt.GetProperty("height", Todo).GetValue(t);
            string tipo = tt.GetProperty("sizeType", Todo).GetValue(t).ToString();
            if (w == Ancho && h == Alto && tipo == "FixedResolution") return i;
        }
        var tipoTam = asm.GetType("UnityEditor.GameViewSize");
        var tipoEnum = asm.GetType("UnityEditor.GameViewSizeType");
        var nuevo = Activator.CreateInstance(tipoTam, Enum.Parse(tipoEnum, "FixedResolution"), Ancho, Alto, "Full HD");
        tipoGrupo.GetMethod("AddCustomSize", Todo).Invoke(grupo, new[] { nuevo });
        return total;
    }
}
