// Pantalla del kiosco Aeropedia: muestra una galería de imágenes de la estética Frutiger.
//   - En el mapa la imagen cubre toda la pantalla del kiosco (sin borde) y va pasando sola cada
//     'segundosPorImagen' con un fundido suave.
//   - Si el jugador se acerca por delante, sobre el kiosco flota el aviso "E  Ver" (mirando a la cámara).
//   - Con E la cámara vuela hasta quedar de frente al kiosco (se ve la pantalla con su marco). A los lados
//     hay dos botones de vidrio ‹ › (también flechas o A / D) y abajo el contador "1 / 4".
//   - E, Esc o el botón rojo vuelven al juego (la cámara regresa y el jugador recupera el control).
// La interfaz se arma sola al empezar, con los sprites del HUD (los asigna LookFrutiger.Pantallas).
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PantallaGaleria : MonoBehaviour
{
    [Header("Imágenes")]
    public Texture2D[] imagenes = new Texture2D[0];
    public Renderer lienzo;                                  // quad que muestra la imagen (hijo de este objeto)
    public Vector2 tamanoPantalla = new Vector2(1.6f, 1f);   // tamaño de la cara de la pantalla (m)
    public float segundosPorImagen = 7f;

    [Header("Interacción")]
    public float distanciaAviso = 3.5f;
    public float duracionVuelo = 0.6f;

    [Header("Estilo (sprites del HUD)")]
    public Sprite orbe;          // esfera de vidrio (se tiñe de aqua)
    public Sprite brillo;        // aro de luz de la esfera
    public Sprite cerrar;        // botón rojo de cerrar

    enum Estado { Libre, Entrando, Viendo, Saliendo }
    Estado estado = Estado.Libre;

    int indice;
    float reloj, fundido = 1f, fundidoObjetivo = 1f, vuelo, avisoAlfa;
    int pendiente; bool hayPendiente;
    MaterialPropertyBlock bloque;

    ControladorJugador jugador;
    CamaraJugador camaraJugador;
    Camera cam;
    Canvas hudJugador;
    Vector3 camDesdePos; Quaternion camDesdeRot;
    Vector3 vistaPos; Quaternion vistaRot;

    CanvasGroup grupoVisor, grupoAviso;
    Transform aviso;             // "E  Ver" flotando sobre el kiosco
    Text contador;
    Font fuente;

    static readonly Color Aqua = new Color(0.55f, 0.9f, 1f);
    static readonly Color AzulMarino = new Color(0.07f, 0.25f, 0.53f);

    void Start()
    {
        bloque = new MaterialPropertyBlock();
        jugador = FindAnyObjectByType<ControladorJugador>();
        if (jugador != null) camaraJugador = jugador.GetComponent<CamaraJugador>();
        fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        CrearVisor();
        CrearAviso();
        Mostrar(0);
    }

    // ------------------------------------------------------------------ imágenes
    void Mostrar(int i)
    {
        if (imagenes == null || imagenes.Length == 0) return;
        indice = (i % imagenes.Length + imagenes.Length) % imagenes.Length;
        var tex = imagenes[indice];
        float asp = tex != null ? (float)tex.width / tex.height : 16f / 9f;
        if (lienzo != null)
        {
            // la imagen cubre toda la cara de la pantalla: se recorta lo que sobre (centrado), sin bordes
            float caraAsp = tamanoPantalla.x / Mathf.Max(tamanoPantalla.y, 1e-3f);
            var st = asp > caraAsp
                ? new Vector4(caraAsp / asp, 1f, (1f - caraAsp / asp) * 0.5f, 0f)
                : new Vector4(1f, asp / caraAsp, 0f, (1f - asp / caraAsp) * 0.5f);
            lienzo.transform.localScale = new Vector3(tamanoPantalla.x, tamanoPantalla.y, 1f);
            lienzo.GetPropertyBlock(bloque);
            if (tex != null) bloque.SetTexture("_BaseMap", tex);
            bloque.SetVector("_BaseMap_ST", st);
            lienzo.SetPropertyBlock(bloque);
        }
        if (contador != null) contador.text = $"{indice + 1} / {imagenes.Length}";
        reloj = 0f;
    }

    public void Cambiar(int paso)
    {
        if (imagenes == null || imagenes.Length < 2) return;
        pendiente = indice + paso; hayPendiente = true;
        fundidoObjetivo = 0f;                       // se apaga, cambia y vuelve a encender
    }

    void ActualizarFundido(float dt)
    {
        fundido = Mathf.MoveTowards(fundido, fundidoObjetivo, dt * 6f);
        if (fundido <= 0.001f && hayPendiente) { Mostrar(pendiente); hayPendiente = false; fundidoObjetivo = 1f; }
        float b = Mathf.Lerp(0.25f, 1f, fundido);
        if (lienzo != null)
        {
            lienzo.GetPropertyBlock(bloque);
            bloque.SetColor("_BaseColor", new Color(b, b, b, 1f));
            lienzo.SetPropertyBlock(bloque);
        }
    }

    // ------------------------------------------------------------------ ciclo
    void Update()
    {
        float dt = Time.deltaTime;
        ActualizarFundido(dt);
        var teclado = Keyboard.current;

        switch (estado)
        {
            case Estado.Libre:
                reloj += dt;
                if (reloj >= segundosPorImagen) { reloj = 0f; Cambiar(1); }
                bool cerca = JugadorCerca();
                avisoAlfa = Mathf.MoveTowards(avisoAlfa, cerca ? 1f : 0f, dt * 5f);
                if (cerca && teclado != null && teclado.eKey.wasPressedThisFrame) Entrar();
                break;

            case Estado.Entrando:
            case Estado.Saliendo:
                vuelo = Mathf.Min(1f, vuelo + dt / Mathf.Max(0.05f, duracionVuelo));
                float k = Mathf.SmoothStep(0f, 1f, vuelo);
                bool entrando = estado == Estado.Entrando;
                if (cam != null)
                {
                    cam.transform.position = entrando ? Vector3.Lerp(camDesdePos, vistaPos, k) : Vector3.Lerp(vistaPos, camDesdePos, k);
                    cam.transform.rotation = entrando ? Quaternion.Slerp(camDesdeRot, vistaRot, k) : Quaternion.Slerp(vistaRot, camDesdeRot, k);
                }
                grupoVisor.alpha = entrando ? k : 1f - k;
                if (vuelo >= 1f)
                {
                    if (entrando) estado = Estado.Viendo;
                    else Terminar();
                }
                break;

            case Estado.Viendo:
                if (teclado == null) break;
                if (teclado.leftArrowKey.wasPressedThisFrame || teclado.aKey.wasPressedThisFrame) Cambiar(-1);
                else if (teclado.rightArrowKey.wasPressedThisFrame || teclado.dKey.wasPressedThisFrame) Cambiar(1);
                else if (teclado.eKey.wasPressedThisFrame || teclado.escapeKey.wasPressedThisFrame) Salir();
                break;
        }
    }

    // el aviso flota sobre el kiosco y siempre mira a la cámara
    void LateUpdate()
    {
        if (aviso == null) return;
        if (estado != Estado.Libre) avisoAlfa = 0f;
        grupoAviso.alpha = avisoAlfa;
        aviso.gameObject.SetActive(avisoAlfa > 0.001f);
        var c = Camera.main;
        if (c == null || avisoAlfa <= 0.001f) return;
        aviso.position = transform.position + Vector3.up * (tamanoPantalla.y * 0.5f + 0.45f) + Vector3.up * 0.04f * Mathf.Sin(Time.time * 2.2f);
        aviso.rotation = Quaternion.LookRotation(aviso.position - c.transform.position, Vector3.up);
    }

    bool JugadorCerca()
    {
        if (jugador == null) { jugador = FindAnyObjectByType<ControladorJugador>(); if (jugador == null) return false; camaraJugador = jugador.GetComponent<CamaraJugador>(); }
        var rel = jugador.transform.position - transform.position;
        var frente = -transform.forward;                     // la imagen mira hacia -forward del lienzo
        float delante = Vector3.Dot(new Vector3(rel.x, 0f, rel.z), new Vector3(frente.x, 0f, frente.z).normalized);
        return delante > 0.2f && new Vector2(rel.x, rel.z).magnitude < distanciaAviso;
    }

    void Entrar()
    {
        cam = Camera.main;
        if (cam == null) return;
        camDesdePos = cam.transform.position; camDesdeRot = cam.transform.rotation;
        // distancia para ver la pantalla entera con su marco (deja espacio a los botones y al contador)
        float tanV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float d = Mathf.Max(tamanoPantalla.y * 0.5f / tanV, tamanoPantalla.x * 0.5f / (tanV * cam.aspect)) / 0.8f;
        var frente = -transform.forward;
        vistaPos = transform.position + frente * Mathf.Max(d, 0.25f);
        vistaRot = Quaternion.LookRotation(transform.forward, transform.up);

        if (jugador != null) jugador.enabled = false;
        if (camaraJugador != null) camaraJugador.enabled = false;   // también suelta el cursor
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        var hud = FindAnyObjectByType<PanelColoresHUD>();
        hudJugador = hud != null ? hud.GetComponent<Canvas>() : null;
        if (hudJugador != null) hudJugador.enabled = false;
        grupoVisor.blocksRaycasts = true;
        estado = Estado.Entrando; vuelo = 0f;
    }

    public void Salir()
    {
        if (estado != Estado.Viendo && estado != Estado.Entrando) return;
        grupoVisor.blocksRaycasts = false;
        estado = Estado.Saliendo; vuelo = 0f;
    }

    void Terminar()
    {
        estado = Estado.Libre;
        if (jugador != null) jugador.enabled = true;
        if (camaraJugador != null) camaraJugador.enabled = true;
        if (hudJugador != null) hudJugador.enabled = true;
        grupoVisor.alpha = 0f;
    }

    // ------------------------------------------------------------------ interfaz
    // Visor: ‹ › a los lados, contador abajo de la imagen y el botón rojo de cerrar.
    void CrearVisor()
    {
        var raiz = new GameObject("UI_PantallaAeropedia", typeof(RectTransform));
        raiz.transform.SetParent(transform, false);
        var canvas = raiz.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        var esc = raiz.AddComponent<CanvasScaler>();
        esc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        esc.referenceResolution = new Vector2(1920f, 1080f);
        esc.matchWidthOrHeight = 0.5f;
        raiz.AddComponent<GraphicRaycaster>();
        grupoVisor = raiz.AddComponent<CanvasGroup>();
        grupoVisor.alpha = 0f; grupoVisor.blocksRaycasts = false;


        var izq = Orbe("Anterior", raiz.transform, new Vector2(0f, 0.5f), new Vector2(110f, 0f), 120f, "‹", 86);
        var der = Orbe("Siguiente", raiz.transform, new Vector2(1f, 0.5f), new Vector2(-110f, 0f), 120f, "›", 86);
        Boton(izq.gameObject).onClick.AddListener(() => Cambiar(-1));
        Boton(der.gameObject).onClick.AddListener(() => Cambiar(1));

        contador = Txt("Contador", raiz.transform, "", 42, Color.white, TextAnchor.MiddleCenter);
        Colocar(contador.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(300f, 70f));
        Contorno(contador);

        var cerrarImg = Img("Cerrar", raiz.transform, cerrar, Color.white);
        Colocar(cerrarImg.rectTransform, new Vector2(1f, 1f), new Vector2(-110f, -70f), new Vector2(144f, 66f));
        Boton(cerrarImg.gameObject).onClick.AddListener(Salir);
    }

    // Aviso en el mundo: esfera aqua con la E y la palabra "Ver", sobre el kiosco
    void CrearAviso()
    {
        var go = new GameObject("Aviso_Ver", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(280f, 110f);
        go.transform.localScale = Vector3.one * 0.0032f;     // ~0.9 m de ancho
        grupoAviso = go.AddComponent<CanvasGroup>();
        grupoAviso.alpha = 0f; grupoAviso.blocksRaycasts = false; grupoAviso.interactable = false;
        var tecla = Orbe("Tecla", go.transform, new Vector2(0.5f, 0.5f), new Vector2(-62f, 0f), 96f, "E", 50);
        tecla.raycastTarget = false;
        var ver = Txt("Ver", go.transform, "Ver", 50, Color.white, TextAnchor.MiddleLeft);
        Colocar(ver.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(72f, 0f), new Vector2(150f, 80f));
        Contorno(ver);
        aviso = go.transform;
        go.SetActive(false);
    }

    // esfera de vidrio aqua con su aro de luz y un símbolo encima
    Image Orbe(string nombre, Transform padre, Vector2 ancla, Vector2 pos, float tam, string simbolo, int tamTexto)
    {
        var img = Img(nombre, padre, orbe, Aqua);
        Colocar(img.rectTransform, ancla, pos, new Vector2(tam, tam));
        if (brillo != null)
        {
            var b = Img("Brillo", img.transform, brillo, new Color(1f, 1f, 1f, 0.9f));
            Colocar(b.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(tam * 1.08f, tam * 1.08f));
            b.raycastTarget = false;
        }
        var t = Txt("Simbolo", img.transform, simbolo, tamTexto, Color.white, TextAnchor.MiddleCenter);
        Colocar(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, char.IsLetter(simbolo[0]) ? 0f : 6f), new Vector2(tam, tam));
        Contorno(t);
        return img;
    }

    static Button Boton(GameObject go)
    {
        var b = go.AddComponent<Button>();
        var c = b.colors;
        c.highlightedColor = Color.white; c.pressedColor = new Color(0.8f, 0.9f, 1f, 1f);
        c.normalColor = new Color(0.92f, 0.97f, 1f, 1f); c.fadeDuration = 0.08f;
        b.colors = c;
        return b;
    }

    Image Img(string nombre, Transform padre, Sprite sp, Color color)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var img = go.AddComponent<Image>();
        img.sprite = sp; img.color = color; img.preserveAspect = true;
        return img;
    }

    Text Txt(string nombre, Transform padre, string contenido, int tamano, Color color, TextAnchor alineacion)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var t = go.AddComponent<Text>();
        t.text = contenido; t.font = fuente; t.fontSize = tamano; t.color = color;
        t.fontStyle = FontStyle.Bold; t.alignment = alineacion; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        return t;
    }

    static void Contorno(Text t)
    {
        var o = t.gameObject.AddComponent<Outline>();
        o.effectColor = AzulMarino; o.effectDistance = new Vector2(2f, -2f);
    }

    static void Colocar(RectTransform rt, Vector2 ancla, Vector2 pos, Vector2 tam)
    {
        rt.anchorMin = rt.anchorMax = ancla; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = tam;
    }
}
