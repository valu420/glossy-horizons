// Herramienta de editor (HU-02): arma el HUD del jugador con uGUI.
// Menú de Unity: Glossy Horizons > Crear HUD del jugador
// Crea Player/UI/HUD_Jugador.prefab con:
//   - cuatro botones orbe abajo a la derecha: Color (abre Personalización), Emotes (abre Emotes),
//     Cámara y Aeropedia (próximamente)
//   - la ventana de Emotes (HU-06): 9 emotes en 3 x 3, tecla G y números 1 a 9
//   - el cursor del mouse estilo Frutiger Aero
//   - el panel de colores como ventana de Windows Vista / 7 (vidrio Aero, barra de direcciones,
//     barra de herramientas, 11 colores, panel de detalles, Restablecer, Listo y cerrar)
// El jugador (Player_Gel) carga este prefab solo en cualquier escena (CargarHUD).
// Después se puede abrir el prefab y modificarlo a mano.
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

public static class CrearHUDJugador
{
    const string Sprites = "Assets/_Project/Player/UI/Sprites/";
    public const string RutaPrefab = "Assets/_Project/Player/UI/HUD_Jugador.prefab";
    static readonly Color AzulMarino = new Color(0.07f, 0.25f, 0.53f);
    static readonly Color Gris = new Color(0.36f, 0.42f, 0.47f);

    [MenuItem("Glossy Horizons/Crear HUD del jugador")]
    public static GameObject Crear()
    {
        var spBrillo = CargarSprite("T_BotonOrbeBrillo.png", Vector4.zero);
        // Arte de los orbes: con mipmaps para que se vea nítido al reducirlo a tamaño de botón
        var spColorBurbuja = CargarSprite("T_BotonColorBurbuja.png", Vector4.zero, true);
        var spColorPersonaje = CargarSprite("T_BotonColorPersonaje.png", Vector4.zero, true);
        var spEmotesBurbuja = CargarSprite("T_BotonEmotesBurbuja.png", Vector4.zero, true);
        var spEmotesPersonaje = CargarSprite("T_BotonEmotesPersonaje.png", Vector4.zero, true);
        var spCamaraBurbuja = CargarSprite("T_BotonCamaraBurbuja.png", Vector4.zero, true);
        var spCamaraPersonaje = CargarSprite("T_BotonCamaraPersonaje.png", Vector4.zero, true);
        var spAeropediaBurbuja = CargarSprite("T_BotonAeropediaBurbuja.png", Vector4.zero, true);
        var spAeropediaPersonaje = CargarSprite("T_BotonAeropediaPersonaje.png", Vector4.zero, true);
        var spColor = CargarSprite("T_BotonColor.png", Vector4.zero);
        // Ventana estilo Vista/7 (dibujada al doble: el borde está en píxeles del sprite)
        var spVentana = CargarSprite("T_VistaVentana.png", new Vector4(40, 40, 40, 40));
        var spReflejo = CargarSprite("T_VistaReflejo.png", Vector4.zero);
        var spCerrarVista = CargarSprite("T_VistaCerrar.png", new Vector4(16, 16, 16, 16));
        var spCampo = CargarSprite("T_VistaCampo.png", new Vector4(12, 12, 12, 12));
        var spCliente = CargarSprite("T_VistaCliente.png", new Vector4(12, 12, 12, 12));
        var spHerramientas = CargarSprite("T_VistaBarraHerramientas.png", new Vector4(14, 0, 14, 0));
        var spDetalles = CargarSprite("T_VistaDetalles.png", new Vector4(12, 12, 12, 12));
        var spBoton = CargarSprite("T_VistaBoton.png", new Vector4(14, 0, 14, 0));
        var spBotonAzul = CargarSprite("T_VistaBotonAzul.png", new Vector4(14, 0, 14, 0));
        var spHaloResalte = CargarSprite("T_HaloResalte.png", Vector4.zero, true);
        var spHaloSeleccion = CargarSprite("T_HaloSeleccion.png", Vector4.zero, true);
        var spIconosEmote = new Sprite[AnimacionGel.NombresEmotes.Length];
        string[] archivosEmote = { "T_Emote1_Saludar", "T_Emote2_Bailar", "T_Emote3_Aplaudir", "T_Emote4_Pensar", "T_Emote5_Amor",
                                   "T_Emote6_Mareado", "T_Emote7_Enojado", "T_Emote8_Sorpresa", "T_Emote9_Gelatina" };
        for (int i = 0; i < spIconosEmote.Length; i++)
            spIconosEmote[i] = CargarSprite("Emotes/" + archivosEmote[i] + ".png", Vector4.zero, true);
        if (spColorBurbuja == null || spColorPersonaje == null || spEmotesBurbuja == null || spEmotesPersonaje == null
            || spCamaraBurbuja == null || spCamaraPersonaje == null || spAeropediaBurbuja == null || spAeropediaPersonaje == null || spVentana == null || spColor == null || spBoton == null)
        {
            EditorUtility.DisplayDialog("Crear HUD", "Faltan imágenes en Player/UI/Sprites.", "OK");
            return null;
        }
        var fuente = CargarFuente();

        // ---------- Canvas ----------
        // Borrar HUDs a medio armar que hayan quedado en la escena por errores anteriores
        int borrados = 0;
        foreach (var viejo in Object.FindObjectsByType<PanelColoresHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (viejo.gameObject.scene.IsValid() && !PrefabUtility.IsPartOfPrefabInstance(viejo.gameObject))
            { Object.DestroyImmediate(viejo.gameObject); borrados++; }
        if (borrados > 0)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("Se borraron " + borrados + " HUD sobrantes de la escena.");
        }

        var hud = new GameObject("HUD_Jugador", typeof(RectTransform));
        try
        {
        var canvas = hud.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var escalador = hud.AddComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920f, 1080f);
        escalador.matchWidthOrHeight = 0.5f;
        hud.AddComponent<GraphicRaycaster>();
        var control = hud.AddComponent<PanelColoresHUD>();

        // Cursor del mouse estilo Frutiger Aero
        var cursor = hud.AddComponent<CursorFrutiger>();
        cursor.flecha = CargarCursor("T_CursorFlecha.png");
        cursor.activo = CargarCursor("T_CursorActivo.png");

        // ---------- Botones orbe (abajo a la derecha) ----------
        // Color (derecha): el personaje con ojos "><" pintado por un pincel -> abre el panel
        var orbe = Orbe("BotonColor", hud.transform, spColorBurbuja, spColorPersonaje, spBrillo, new Vector2(-115f, 105f));
        orbe.GetComponent<BotonOrbeVista>().panel = control;
        UnityEventTools.AddPersistentListener(orbe.onClick, control.Alternar);
        // Aviso si se intenta abrir el panel en el aire (lo muestra PanelColoresHUD, no el clic)
        var avisoAire = Txt("AvisoAire", orbe.transform, "¡No puedes cambiar de color en el aire!", fuente, 26, Color.white, TextAnchor.MiddleRight);
        Colocar(avisoAire.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-170f, 95f), new Vector2(520f, 44f));
        avisoAire.raycastTarget = false;
        var contornoAire = avisoAire.gameObject.AddComponent<Outline>();
        contornoAire.effectColor = AzulMarino; contornoAire.effectDistance = new Vector2(2f, -2f);
        var avisoAireComp = orbe.gameObject.AddComponent<BotonProximamente>();
        avisoAireComp.aviso = avisoAire;
        avisoAireComp.mostrarAlHacerClic = false;
        control.avisoEnElAire = avisoAireComp;

        // Emotes (HU-06): abre la ventana de emotes (tecla G)
        var controlEmotes = hud.AddComponent<PanelEmotesHUD>();
        var botonEmotes = Orbe("BotonEmotes", hud.transform, spEmotesBurbuja, spEmotesPersonaje, spBrillo, new Vector2(-245f, 105f));
        UnityEventTools.AddPersistentListener(botonEmotes.onClick, controlEmotes.Alternar);
        controlEmotes.avisoNoSePuede = AvisoFlotante(botonEmotes.transform, "¡Ahora no puedes hacer emotes!", fuente);

        // Los demás todavía no tienen función: muestran "próximamente"
        OrbeProximamente("BotonCamara", "¡Cámara próximamente!", hud.transform, fuente, spCamaraBurbuja, spCamaraPersonaje, spBrillo, new Vector2(-375f, 105f));
        OrbeProximamente("BotonAeropedia", "¡Aeropedia próximamente!", hud.transform, fuente, spAeropediaBurbuja, spAeropediaPersonaje, spBrillo, new Vector2(-505f, 105f));

        // ---------- Panel: ventana de vidrio Aero con fondo vivo ----------
        // Marco de vidrio Aero con el fondo vivo en la franja del título (aurora animada con cintas
        // de luz y burbujas: shader Glossy/UI/Aurora), barra de direcciones, barra de herramientas
        // oscura y brillante, área blanca con los colores y panel de detalles abajo.
        const float AnchoPanel = 700f, AltoPanel = 492f;
        var aurora = CargarMaterialAurora();
        // La raíz es invisible pero bloquea los clics (para no girar la cámara a través de la ventana)
        var panel = Img("PanelColores", hud.transform, spVentana, new Color(1f, 1f, 1f, aurora != null ? 0f : 1f));
        Rebanado(panel);
        Colocar(panel.rectTransform, new Vector2(0f, 0.5f), new Vector2(60f + AnchoPanel / 2f, 30f), new Vector2(AnchoPanel, AltoPanel));
        control.panel = panel.gameObject;
        if (aurora != null)
        {
            // "Marco": el vidrio de la ventana, que además recorta la aurora con sus esquinas redondas
            var marco = Img("Marco", panel.transform, spVentana, Color.white);
            Rebanado(marco); Estirar(marco.rectTransform, 0f); marco.raycastTarget = false;
            marco.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var fondoVivo = Img("FondoVivo", marco.transform, null, Color.white);
            fondoVivo.material = aurora; fondoVivo.raycastTarget = false;
            Franja(fondoVivo.rectTransform, 0f, 0f, 112f);                 // franja del título y la barra de direcciones
            fondoVivo.gameObject.AddComponent<FondoVivo>();
            var vidrio = Img("Vidrio", panel.transform, spVentana, new Color(1f, 1f, 1f, 0.3f));
            Rebanado(vidrio); Estirar(vidrio.rectTransform, 0f); vidrio.raycastTarget = false;
        }
        var reflejo = Img("Reflejo", panel.transform, spReflejo, new Color(1f, 1f, 1f, 0.9f));
        Estirar(reflejo.rectTransform, 2f); reflejo.raycastTarget = false;

        // Título blanco con resplandor oscuro (se lee bien sobre la aurora)
        var titulo = Txt("Titulo", panel.transform, "Personalización", fuente, 26, Color.white, TextAnchor.MiddleLeft);
        Colocar(titulo.rectTransform, new Vector2(0f, 1f), new Vector2(20f + 230f, -26f), new Vector2(460f, 40f));
        Resplandor(titulo, new Color(0f, 0.12f, 0.2f, 0.55f));
        var cerrar = Img("BotonCerrar", panel.transform, spCerrarVista, Color.white);
        Rebanado(cerrar);
        var rtC = cerrar.rectTransform;
        rtC.anchorMin = rtC.anchorMax = new Vector2(1f, 1f); rtC.pivot = new Vector2(1f, 1f);
        rtC.anchoredPosition = new Vector2(-12f, -2f); rtC.sizeDelta = new Vector2(52f, 24f);
        var botonCerrar = cerrar.gameObject.AddComponent<Button>();
        Tinte(botonCerrar, new Color(1f, 0.92f, 0.9f), new Color(0.8f, 0.75f, 0.75f));
        UnityEventTools.AddPersistentListener(botonCerrar.onClick, control.Cerrar);

        // Barra de direcciones: "Usuario  >  Colores  >  <color elegido>"
        var campo = Img("BarraDirecciones", panel.transform, spCampo, Color.white);
        Rebanado(campo); campo.raycastTarget = false;
        Franja(campo.rectTransform, 14f, -52f, 32f);
        var ruta = new GameObject("Ruta", typeof(RectTransform));
        ruta.transform.SetParent(campo.transform, false);
        Estirar((RectTransform)ruta.transform, 0f);
        ((RectTransform)ruta.transform).offsetMin = new Vector2(12f, 0f);
        var hl = ruta.AddComponent<HorizontalLayoutGroup>();
        hl.childAlignment = TextAnchor.MiddleLeft; hl.spacing = 8f;
        hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;
        foreach (var parte in new[] { "Usuario", "›", "Colores", "›" })
            Txt(parte.Length == 1 ? "Flecha" : parte, ruta.transform, parte, fuente, 19, new Color(0.15f, 0.18f, 0.22f), TextAnchor.MiddleLeft);
        var nombre = Txt("NombreColor", ruta.transform, "Original", fuente, 19, new Color(0.05f, 0.25f, 0.55f), TextAnchor.MiddleLeft);

        // Área de trabajo blanca
        var cliente = Img("AreaColores", panel.transform, spCliente, Color.white);
        Rebanado(cliente); cliente.raycastTarget = false;
        var rtCl = cliente.rectTransform;
        rtCl.anchorMin = Vector2.zero; rtCl.anchorMax = Vector2.one;
        rtCl.offsetMin = new Vector2(10f, 10f); rtCl.offsetMax = new Vector2(-10f, -94f);

        // Barra de herramientas oscura y brillante
        var herramientas = Img("BarraHerramientas", cliente.transform, spHerramientas, Color.white);
        Rebanado(herramientas); herramientas.raycastTarget = false;
        Franja(herramientas.rectTransform, 0f, 0f, 42f);
        var etiqueta = Txt("Etiqueta", herramientas.transform, "Colores", fuente, 21, Color.white, TextAnchor.MiddleLeft);
        Estirar(etiqueta.rectTransform, 0f); etiqueta.rectTransform.offsetMin = new Vector2(18f, 0f);
        Sombra(etiqueta);
        var cuenta = Txt("Cuenta", herramientas.transform, ColorPersonaje.Paleta.Length + " colores", fuente, 17, new Color(0.85f, 0.93f, 1f), TextAnchor.MiddleRight);
        Estirar(cuenta.rectTransform, 0f); cuenta.rectTransform.offsetMax = new Vector2(-18f, 0f);

        // Fondo vivo detrás de los colores: aurora animada con cintas de luz y burbujas
        if (aurora != null)
        {
            var fondoColores = Img("FondoVivoColores", cliente.transform, null, Color.white);
            fondoColores.material = aurora; fondoColores.raycastTarget = false;
            var rtF = fondoColores.rectTransform;
            rtF.anchorMin = Vector2.zero; rtF.anchorMax = Vector2.one;
            rtF.offsetMin = new Vector2(2f, 96f); rtF.offsetMax = new Vector2(-2f, -42f);   // entre la barra de herramientas y los detalles
            fondoColores.gameObject.AddComponent<FondoVivo>();
        }

        // Cuadrícula de colores (como los íconos del Explorador)
        var grilla = new GameObject("GrillaColores", typeof(RectTransform));
        grilla.transform.SetParent(cliente.transform, false);
        Franja((RectTransform)grilla.transform, 10f, -54f, 210f);
        var grid = grilla.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(98f, 98f);
        grid.spacing = new Vector2(8f, 8f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 6;
        grid.childAlignment = TextAnchor.UpperCenter;

        // Plantilla: celda invisible (zona de clic) + luz al pasar el mouse + halo animado del elegido + orbe
        var plantilla = Img("PlantillaColor", grilla.transform, null, new Color(1f, 1f, 1f, 0f));
        var resalte = Img("Resalte", plantilla.transform, spHaloResalte, Color.white);
        Colocar(resalte.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(112f, 112f)); resalte.raycastTarget = false;
        var marca = Img("Seleccion", plantilla.transform, spHaloSeleccion, Color.white);
        Colocar(marca.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f)); marca.raycastTarget = false;
        marca.gameObject.AddComponent<HaloAnimado>();
        marca.gameObject.SetActive(false);
        var orbeColor = Img("Orbe", plantilla.transform, spColor, Color.white);
        Colocar(orbeColor.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(74f, 74f)); orbeColor.raycastTarget = false;
        var botonPlantilla = plantilla.gameObject.AddComponent<Button>();
        botonPlantilla.targetGraphic = resalte;
        var cb = botonPlantilla.colors;
        cb.normalColor = new Color(1f, 1f, 1f, 0f); cb.highlightedColor = Color.white; cb.pressedColor = new Color(0.85f, 0.9f, 1f, 1f);
        cb.selectedColor = new Color(1f, 1f, 1f, 0f); cb.fadeDuration = 0.08f;
        botonPlantilla.colors = cb;

        // Panel de detalles (abajo): vista previa del color, nombre y botones
        var detalles = Img("PanelDetalles", cliente.transform, spDetalles, Color.white);
        Rebanado(detalles); detalles.raycastTarget = false;
        var rtD = detalles.rectTransform;
        rtD.anchorMin = new Vector2(0f, 0f); rtD.anchorMax = new Vector2(1f, 0f); rtD.pivot = new Vector2(0.5f, 0f);
        rtD.anchoredPosition = Vector2.zero; rtD.sizeDelta = new Vector2(0f, 96f);
        var previa = Img("VistaPrevia", detalles.transform, spColor, Color.white);
        Colocar(previa.rectTransform, new Vector2(0f, 0.5f), new Vector2(54f, 0f), new Vector2(66f, 66f)); previa.raycastTarget = false;
        var nombreGrande = Txt("NombreDetalle", detalles.transform, "Original", fuente, 24, new Color(0.08f, 0.1f, 0.14f), TextAnchor.LowerLeft);
        Colocar(nombreGrande.rectTransform, new Vector2(0f, 0.5f), new Vector2(100f + 120f, 12f), new Vector2(240f, 34f));
        var sub = Txt("Subtitulo", detalles.transform, "Color del gel", fuente, 17, new Color(0.38f, 0.43f, 0.5f), TextAnchor.UpperLeft);
        Colocar(sub.rectTransform, new Vector2(0f, 0.5f), new Vector2(100f + 120f, -14f), new Vector2(240f, 28f));
        var restablecer = BotonVista("BotonRestablecer", detalles.transform, spBoton, "Restablecer", fuente, new Color(0.1f, 0.12f, 0.15f),
                                     new Vector2(1f, 0.5f), new Vector2(-250f, 0f));
        var listo = BotonVista("BotonListo", detalles.transform, spBotonAzul, "Listo", fuente, new Color(0.05f, 0.18f, 0.35f),
                               new Vector2(1f, 0.5f), new Vector2(-88f, 0f));

        // Pista de teclas debajo de la ventana
        var pista = Txt("Pista", panel.transform, "C \u00B7 abrir / cerrar     Esc \u00B7 cerrar", fuente, 20, Color.white, TextAnchor.MiddleCenter);
        Colocar(pista.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -24f), new Vector2(520f, 34f));
        Sombra(pista);

        var selector = panel.gameObject.AddComponent<SelectorColor>();
        selector.contenedor = grilla.transform;
        selector.plantillaBoton = botonPlantilla;
        selector.botonListo = listo;
        selector.botonRestablecer = restablecer;
        selector.textoNombre = nombre;
        selector.textoNombreDetalle = nombreGrande;
        selector.vistaPrevia = previa;
        if (selector.alConfirmar == null) selector.alConfirmar = new UnityEngine.Events.UnityEvent();
        UnityEventTools.AddPersistentListener(selector.alConfirmar, control.Cerrar);

        panel.gameObject.SetActive(false);   // empieza cerrado

        // ---------- Ventana de Emotes (HU-06), mismo estilo que Personalización ----------
        const float AnchoE = 540f, AltoE = 690f;
        var ventanaE = Img("VentanaEmotes", hud.transform, spVentana, new Color(1f, 1f, 1f, aurora != null ? 0f : 1f));
        Rebanado(ventanaE);
        Colocar(ventanaE.rectTransform, new Vector2(0f, 0.5f), new Vector2(60f + AnchoE / 2f, 30f), new Vector2(AnchoE, AltoE));
        controlEmotes.panel = ventanaE.gameObject;
        botonEmotes.GetComponent<BotonOrbeVista>().ventana = ventanaE.gameObject;
        MarcoAero(ventanaE.transform, aurora, spVentana, spReflejo);
        var tituloE = Txt("Titulo", ventanaE.transform, "Emotes", fuente, 26, Color.white, TextAnchor.MiddleLeft);
        Colocar(tituloE.rectTransform, new Vector2(0f, 1f), new Vector2(20f + 230f, -26f), new Vector2(460f, 40f));
        Resplandor(tituloE, new Color(0f, 0.12f, 0.2f, 0.55f));
        var cerrarE = Img("BotonCerrar", ventanaE.transform, spCerrarVista, Color.white);
        Rebanado(cerrarE);
        var rtCE = cerrarE.rectTransform;
        rtCE.anchorMin = rtCE.anchorMax = new Vector2(1f, 1f); rtCE.pivot = new Vector2(1f, 1f);
        rtCE.anchoredPosition = new Vector2(-12f, -2f); rtCE.sizeDelta = new Vector2(52f, 24f);
        var botonCerrarE = cerrarE.gameObject.AddComponent<Button>();
        Tinte(botonCerrarE, new Color(1f, 0.92f, 0.9f), new Color(0.8f, 0.75f, 0.75f));
        UnityEventTools.AddPersistentListener(botonCerrarE.onClick, controlEmotes.Cerrar);

        // Barra de direcciones: "Usuario > Emotes"
        var campoE = Img("BarraDirecciones", ventanaE.transform, spCampo, Color.white);
        Rebanado(campoE); campoE.raycastTarget = false;
        Franja(campoE.rectTransform, 14f, -52f, 32f);
        var rutaE = new GameObject("Ruta", typeof(RectTransform));
        rutaE.transform.SetParent(campoE.transform, false);
        Estirar((RectTransform)rutaE.transform, 0f);
        ((RectTransform)rutaE.transform).offsetMin = new Vector2(12f, 0f);
        var hlE = rutaE.AddComponent<HorizontalLayoutGroup>();
        hlE.childAlignment = TextAnchor.MiddleLeft; hlE.spacing = 8f;
        hlE.childControlWidth = true; hlE.childControlHeight = true; hlE.childForceExpandWidth = false; hlE.childForceExpandHeight = true;
        Txt("Usuario", rutaE.transform, "Usuario", fuente, 19, new Color(0.15f, 0.18f, 0.22f), TextAnchor.MiddleLeft);
        Txt("Flecha", rutaE.transform, "\u203A", fuente, 19, new Color(0.15f, 0.18f, 0.22f), TextAnchor.MiddleLeft);
        Txt("Emotes", rutaE.transform, "Emotes", fuente, 19, new Color(0.05f, 0.25f, 0.55f), TextAnchor.MiddleLeft);

        // Área de trabajo, barra de herramientas y fondo vivo
        var clienteE = Img("AreaEmotes", ventanaE.transform, spCliente, Color.white);
        Rebanado(clienteE); clienteE.raycastTarget = false;
        var rtClE = clienteE.rectTransform;
        rtClE.anchorMin = Vector2.zero; rtClE.anchorMax = Vector2.one;
        rtClE.offsetMin = new Vector2(10f, 10f); rtClE.offsetMax = new Vector2(-10f, -94f);
        var herrE = Img("BarraHerramientas", clienteE.transform, spHerramientas, Color.white);
        Rebanado(herrE); herrE.raycastTarget = false;
        Franja(herrE.rectTransform, 0f, 0f, 42f);
        var etiquetaE = Txt("Etiqueta", herrE.transform, "Emotes", fuente, 21, Color.white, TextAnchor.MiddleLeft);
        Estirar(etiquetaE.rectTransform, 0f); etiquetaE.rectTransform.offsetMin = new Vector2(18f, 0f);
        Sombra(etiquetaE);
        var teclasE = Txt("Teclas", herrE.transform, "G \u00B7 abrir    1-9 \u00B7 elegir", fuente, 17, new Color(0.85f, 0.93f, 1f), TextAnchor.MiddleRight);
        Estirar(teclasE.rectTransform, 0f); teclasE.rectTransform.offsetMax = new Vector2(-18f, 0f);
        const float AltoPieE = 54f;
        if (aurora != null)
        {
            var fondoE = Img("FondoVivoEmotes", clienteE.transform, null, Color.white);
            fondoE.material = aurora; fondoE.raycastTarget = false;
            var rtFE = fondoE.rectTransform;
            rtFE.anchorMin = Vector2.zero; rtFE.anchorMax = Vector2.one;
            rtFE.offsetMin = new Vector2(2f, AltoPieE); rtFE.offsetMax = new Vector2(-2f, -42f);
            fondoE.gameObject.AddComponent<FondoVivo>();
        }

        // Cuadrícula 3 x 3: burbuja con el ícono, número (atajo) y nombre
        var grillaE = new GameObject("GrillaEmotes", typeof(RectTransform));
        grillaE.transform.SetParent(clienteE.transform, false);
        Franja((RectTransform)grillaE.transform, 10f, -54f, 3 * 150f + 2 * 8f);
        var gridE = grillaE.AddComponent<GridLayoutGroup>();
        gridE.cellSize = new Vector2(150f, 150f);
        gridE.spacing = new Vector2(16f, 8f);
        gridE.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridE.constraintCount = 3;
        gridE.childAlignment = TextAnchor.UpperCenter;
        var botonesE = new Button[AnimacionGel.NombresEmotes.Length];
        for (int i = 0; i < botonesE.Length; i++)
        {
            var celda = Img("Emote" + (i + 1) + "_" + AnimacionGel.NombresEmotes[i], grillaE.transform, null, new Color(1f, 1f, 1f, 0f));
            var luz = Img("Resalte", celda.transform, spHaloResalte, Color.white);
            Colocar(luz.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), new Vector2(132f, 132f)); luz.raycastTarget = false;
            const float OrbeE = 104f;
            var burb = Img("Burbuja", celda.transform, spEmotesBurbuja, Color.white);
            Colocar(burb.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), Vector2.one * (OrbeE / 0.626f)); burb.raycastTarget = false;
            var ico = Img("Icono", celda.transform, spIconosEmote[i], Color.white);
            Colocar(ico.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), Vector2.one * (OrbeE / 0.626f)); ico.raycastTarget = false;
            var insignia = Img("Numero", celda.transform, spColor, new Color(0.2f, 0.5f, 0.95f));
            Colocar(insignia.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-42f, 52f), new Vector2(32f, 32f)); insignia.raycastTarget = false;
            var num = Txt("Texto", insignia.transform, (i + 1).ToString(), fuente, 19, Color.white, TextAnchor.MiddleCenter);
            Estirar(num.rectTransform, 0f); Sombra(num);
            var nom = Txt("Nombre", celda.transform, AnimacionGel.NombresEmotes[i], fuente, 18, Color.white, TextAnchor.MiddleCenter);
            Colocar(nom.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(150f, 26f)); Sombra(nom);
            var b = celda.gameObject.AddComponent<Button>();
            b.targetGraphic = luz;
            var cbE = b.colors;
            cbE.normalColor = new Color(1f, 1f, 1f, 0f); cbE.highlightedColor = Color.white; cbE.pressedColor = new Color(0.85f, 0.9f, 1f, 1f);
            cbE.selectedColor = new Color(1f, 1f, 1f, 0f); cbE.fadeDuration = 0.08f;
            b.colors = cbE;
            botonesE[i] = b;
        }
        controlEmotes.botones = botonesE;

        // Pie con la ayuda
        var pieE = Img("PanelDetalles", clienteE.transform, spDetalles, Color.white);
        Rebanado(pieE); pieE.raycastTarget = false;
        var rtPE = pieE.rectTransform;
        rtPE.anchorMin = new Vector2(0f, 0f); rtPE.anchorMax = new Vector2(1f, 0f); rtPE.pivot = new Vector2(0.5f, 0f);
        rtPE.anchoredPosition = Vector2.zero; rtPE.sizeDelta = new Vector2(0f, AltoPieE);
        var ayudaE = Txt("Ayuda", pieE.transform, "Haz clic en un emote o presiona su n\u00FAmero", fuente, 18, new Color(0.3f, 0.36f, 0.43f), TextAnchor.MiddleCenter);
        Estirar(ayudaE.rectTransform, 0f);
        var pistaE = Txt("Pista", ventanaE.transform, "G \u00B7 abrir / cerrar     1-9 \u00B7 emote     Esc \u00B7 cerrar", fuente, 20, Color.white, TextAnchor.MiddleCenter);
        Colocar(pistaE.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -24f), new Vector2(560f, 34f));
        Sombra(pistaE);
        ventanaE.gameObject.SetActive(false);   // empieza cerrada

        // ---------- Guardar ----------
        var prefab = PrefabUtility.SaveAsPrefabAsset(hud, RutaPrefab, out bool ok);
        if (ok) { Selection.activeObject = prefab; EditorGUIUtility.PingObject(prefab); Debug.Log("HUD creado: " + RutaPrefab); }
        else Debug.LogError("No se pudo crear " + RutaPrefab);
        return prefab;
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Crear HUD", "No se pudo crear el HUD: " + e.Message, "OK");
            return null;
        }
        finally
        {
            Object.DestroyImmediate(hud);   // nunca deja un HUD a medio armar en la escena
        }
    }

    // El arte del orbe (imagen completa, sin fondo) ocupa el 62.6 % de su lienzo:
    // el resto es margen transparente para el pincel que sobresale.
    const float TamOrbe = 110f;
    const float TamArte = TamOrbe / 0.626f;

    static Button Orbe(string nombre, Transform padre, Sprite spBurbuja, Sprite spPersonaje, Sprite spBrillo, Vector2 pos)
    {
        // Raíz invisible: solo define la zona de clic (el círculo del orbe)
        var orbe = Img(nombre, padre, null, new Color(1f, 1f, 1f, 0f));
        Colocar(orbe.rectTransform, new Vector2(1f, 0f), pos, new Vector2(TamOrbe, TamOrbe));
        var boton = orbe.gameObject.AddComponent<Button>();
        boton.transition = Selectable.Transition.None;
        // De atrás hacia adelante: brillo, burbuja, personaje (así el brillo no tapa al personaje)
        var brillo = Img("Brillo", orbe.transform, spBrillo, new Color(1f, 1f, 1f, 0f));
        Estirar(brillo.rectTransform, 0f); brillo.raycastTarget = false;
        var burbuja = Img("Burbuja", orbe.transform, spBurbuja, Color.white);
        Colocar(burbuja.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(TamArte, TamArte));
        burbuja.raycastTarget = false;
        var personaje = Img("Personaje", orbe.transform, spPersonaje, Color.white);
        Colocar(personaje.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(TamArte, TamArte));
        personaje.raycastTarget = false;
        var vista = orbe.gameObject.AddComponent<BotonOrbeVista>();
        vista.brillo = brillo;
        vista.personaje = personaje.rectTransform;
        return boton;
    }

    // Orbe sin función todavía: al hacer clic aparece un aviso flotante
    static Button OrbeProximamente(string nombre, string textoAviso, Transform padre, Font fuente,
                                   Sprite spBurbuja, Sprite spPersonaje, Sprite spBrillo, Vector2 pos)
    {
        var b = Orbe(nombre, padre, spBurbuja, spPersonaje, spBrillo, pos);
        var aviso = Txt("Aviso", b.transform, textoAviso, fuente, 26, Color.white, TextAnchor.MiddleCenter);
        Colocar(aviso.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 95f), new Vector2(340f, 44f));
        aviso.raycastTarget = false;
        var contorno = aviso.gameObject.AddComponent<Outline>();
        contorno.effectColor = AzulMarino; contorno.effectDistance = new Vector2(2f, -2f);
        b.gameObject.AddComponent<BotonProximamente>().aviso = aviso;
        return b;
    }

    // Marco de vidrio Aero con la aurora animada en la franja del título (para ventanas nuevas)
    static void MarcoAero(Transform ventana, Material aurora, Sprite spVentana, Sprite spReflejo)
    {
        if (aurora != null)
        {
            var marco = Img("Marco", ventana, spVentana, Color.white);
            Rebanado(marco); Estirar(marco.rectTransform, 0f); marco.raycastTarget = false;
            marco.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var fondo = Img("FondoVivo", marco.transform, null, Color.white);
            fondo.material = aurora; fondo.raycastTarget = false;
            Franja(fondo.rectTransform, 0f, 0f, 112f);
            fondo.gameObject.AddComponent<FondoVivo>();
            var vidrio = Img("Vidrio", ventana, spVentana, new Color(1f, 1f, 1f, 0.3f));
            Rebanado(vidrio); Estirar(vidrio.rectTransform, 0f); vidrio.raycastTarget = false;
        }
        var reflejo = Img("Reflejo", ventana, spReflejo, new Color(1f, 1f, 1f, 0.9f));
        Estirar(reflejo.rectTransform, 2f); reflejo.raycastTarget = false;
    }

    // Aviso flotante sobre un botón que muestra otro script (no aparece al hacer clic)
    static BotonProximamente AvisoFlotante(Transform boton, string texto, Font fuente)
    {
        var t = Txt("AvisoNoSePuede", boton, texto, fuente, 26, Color.white, TextAnchor.MiddleRight);
        Colocar(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-170f, 95f), new Vector2(520f, 44f));
        t.raycastTarget = false;
        var o = t.gameObject.AddComponent<Outline>();
        o.effectColor = AzulMarino; o.effectDistance = new Vector2(2f, -2f);
        var aviso = boton.gameObject.AddComponent<BotonProximamente>();
        aviso.aviso = t;
        aviso.mostrarAlHacerClic = false;
        return aviso;
    }

    // Botón estilo Vista/7: degradado brillante, se ilumina en azul al pasar el mouse
    static Button BotonVista(string nombre, Transform padre, Sprite sp, string texto, Font fuente, Color colorTexto,
                             Vector2 ancla, Vector2 pos)
    {
        var img = Img(nombre, padre, sp, Color.white);
        Rebanado(img);
        Colocar(img.rectTransform, ancla, pos, new Vector2(150f, 40f));
        var b = img.gameObject.AddComponent<Button>();
        Tinte(b, new Color(0.86f, 0.95f, 1f), new Color(0.72f, 0.85f, 0.96f));
        var t = Txt("Texto", img.transform, texto, fuente, 20, colorTexto, TextAnchor.MiddleCenter);
        Estirar(t.rectTransform, 0f);
        return b;
    }

    static void Tinte(Button b, Color encima, Color presionado)
    {
        var c = b.colors;
        c.normalColor = Color.white; c.highlightedColor = encima; c.pressedColor = presionado;
        c.selectedColor = Color.white; c.colorMultiplier = 1.2f; c.fadeDuration = 0.08f;
        b.colors = c;
    }

    // Los sprites Vista están dibujados al doble de tamaño
    static void Rebanado(Image img)
    {
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 2f;
    }

    // Franja horizontal pegada arriba del padre: margen a los lados, posición Y y alto
    static void Franja(RectTransform rt, float margen, float y, float alto)
    {
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y); rt.sizeDelta = new Vector2(-2f * margen, alto);
    }

    static void Resplandor(Text t, Color color)
    {
        foreach (var d in new[] { new Vector2(1.5f, 1.5f), new Vector2(-1.5f, -1.5f) })
        {
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = color; o.effectDistance = d;
        }
    }

    static void Sombra(Text t)
    {
        var s = t.gameObject.AddComponent<Shadow>();
        s.effectColor = new Color(0f, 0.1f, 0.25f, 0.7f); s.effectDistance = new Vector2(1f, -1.5f);
    }

    // Material del fondo animado (aurora + burbujas). Lo crea la primera vez.
    const string RutaAurora = "Assets/_Project/Player/UI/M_AuroraUI.mat";
    static Material CargarMaterialAurora()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(RutaAurora);
        if (mat != null) { mat.SetFloat("_Local", 0f); EditorUtility.SetDirty(mat); return mat; }
        var shader = Shader.Find("Glossy/UI/Aurora");
        if (shader == null) { Debug.LogWarning("HUD: no se encontró el shader Glossy/UI/Aurora; la ventana queda sin fondo animado."); return null; }
        mat = new Material(shader);
        mat.SetFloat("_Local", 0f);      // el marco usa la pantalla; FondoVivo pasa sus copias a modo local
        AssetDatabase.CreateAsset(mat, RutaAurora);
        AssetDatabase.SaveAssets();
        return mat;
    }

    // Tipografía Frutiger Aero: usa la primera fuente que haya en Player/UI/Fuentes
    // (por ejemplo LD Verdant Leaves). Si no hay ninguna, usa la de Unity.
    const string CarpetaFuentes = "Assets/_Project/Player/UI/Fuentes";
    static bool fuentePropia;
    static Font CargarFuente()
    {
        fuentePropia = false;
        if (AssetDatabase.IsValidFolder(CarpetaFuentes))
            foreach (var guid in AssetDatabase.FindAssets("t:Font", new[] { CarpetaFuentes }))
            {
                var f = AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(guid));
                if (f != null) { fuentePropia = true; Debug.Log("HUD: usando la fuente " + f.name); return f; }
            }
        Debug.Log("HUD: no hay fuente en " + CarpetaFuentes + ", se usa la de Unity.");
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    static Texture2D CargarCursor(string archivo)
    {
        string ruta = "Assets/_Project/Player/UI/Cursores/" + archivo;
        var imp = AssetImporter.GetAtPath(ruta) as TextureImporter;
        if (imp == null) return null;
        if (imp.textureType != TextureImporterType.Cursor)
        {
            imp.textureType = TextureImporterType.Cursor;   // también la deja legible y sin mipmaps
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
    }

    static Sprite CargarSprite(string archivo, Vector4 borde, bool mipmaps = false)
    {
        string ruta = Sprites + archivo;
        var imp = AssetImporter.GetAtPath(ruta) as TextureImporter;
        if (imp == null)   // archivo recién copiado que Unity aún no importó
        {
            AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceSynchronousImport);
            imp = AssetImporter.GetAtPath(ruta) as TextureImporter;
        }
        if (imp == null) return null;
        if (imp.textureType != TextureImporterType.Sprite || imp.spriteBorder != borde || !imp.alphaIsTransparency
            || imp.mipmapEnabled != mipmaps)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = mipmaps;
            imp.filterMode = mipmaps ? FilterMode.Trilinear : FilterMode.Bilinear;
            imp.spriteBorder = borde;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
    }

    static Image Img(string nombre, Transform padre, Sprite sp, Color color)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var img = go.AddComponent<Image>();
        img.sprite = sp; img.color = color;
        return img;
    }

    static Text Txt(string nombre, Transform padre, string contenido, Font fuente, int tamano, Color color, TextAnchor alineacion)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var t = go.AddComponent<Text>();
        t.text = contenido; t.font = fuente; t.fontSize = tamano; t.color = color;
        t.fontStyle = fuentePropia ? FontStyle.Normal : FontStyle.Bold; t.alignment = alineacion; t.raycastTarget = false;
        return t;
    }

    static void Colocar(RectTransform rt, Vector2 ancla, Vector2 pos, Vector2 tam)
    {
        rt.anchorMin = ancla; rt.anchorMax = ancla; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = tam;
    }

    static void Estirar(RectTransform rt, float margen)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(margen, margen); rt.offsetMax = new Vector2(-margen, -margen);
    }
}
