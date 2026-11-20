// HU-05: animaciones del personaje de gel, hechas por código.
// Va en la raíz del prefab Player_Gel (junto a ControladorJugador) y lee de él
// la velocidad, si corre, si está en el suelo, si saltó y la velocidad vertical.
//
//   Reposo   -> las piezas flotan con desfase y el cuerpo "respira"
//   Caminar  -> pies alternados, manos en arcos grandes, leve rebote e inclinación
//   Correr   -> estilo ninja tierno: manos hacia atrás aleteando, cuerpo muy inclinado
//   Saltar   -> se estira y sube las manitos por encima de la cabeza
//   Caer     -> agita las manos arriba, los pies cuelgan y los ojos se abren grandes
//   Aterrizar-> se aplasta y rebota como gelatina
//   Ojos     -> parpadean, miran a los lados en reposo y se entrecierran al correr
//   Dormir   -> tras unos segundos quieto se duerme: ojos cerrados, cabeza ladeada
//               y una nube "Zzz" sobre la cabeza
//   Despertar-> ojos grandes, saltito y una burbuja "!"
//
// Cada pieza sigue su posición con un pequeño retraso (como un resorte):
// eso le da el efecto de gelatina.
using UnityEngine;

[DefaultExecutionOrder(100)]   // después del movimiento y antes de la cámara
[RequireComponent(typeof(ControladorJugador))]
public class AnimacionGel : MonoBehaviour
{
    [Header("Reposo")]
    public float flotacion = 0.02f;
    public float ciclosReposo = 0.7f;            // ciclos por segundo

    [Header("Caminar")]
    public float ciclosPorMetro = 0.55f;
    public float pasoCaminar = 0.12f;
    public float alturaPasoCaminar = 0.07f;
    public float braceoCaminar = 0.18f;
    public float inclinacionCaminar = 5f;

    [Header("Correr (estilo ninja tierno)")]
    public float pasoCorrer = 0.15f;
    public float alturaPasoCorrer = 0.09f;
    public float manosAtras = 0.3f;              // cuánto van las manos hacia atrás
    public float aleteoManos = 0.035f;
    public float inclinacionCorrer = 18f;

    [Header("Salto y caída")]
    public float manosArribaAlSaltar = 0.6f;
    public float aplastamientoAlCaer = 0.22f;

    [Header("Ojos")]
    public float parpadeoMin = 2f, parpadeoMax = 5f;

    [Header("Dormir")]
    public float segundosParaAdormecerse = 15f;
    public float segundosParaDormir = 18f;

    [Header("Personalización (HU-02)")]
    public float duracionAlegria = 0.9f;          // salto de alegría al elegir un color
    public float alturaSaltoAlegria = 0.22f;

    [Header("Burbujas sobre la cabeza")]
    public Texture2D texturaNube;                // nube vacía
    public Texture2D texturaLetraZ;              // una "Z": se anima Z -> Zz -> Zzz
    public Texture2D texturaExclamacion;
    public float cicloZzz = 3f;                  // segundos que dura cada ciclo de las Z
    public float tamanoBurbuja = 0.45f;          // alto en metros
    public Vector3 posicionBurbuja = new Vector3(0.28f, 0.42f, 0f);   // respecto a la cabeza

    public bool Durmiendo => sueno > 0.5f;

    class Pieza
    {
        public Transform t;
        public Vector3 basePos;        // posición en el espacio del jugador
        public Quaternion baseRot;     // rotación relativa al jugador
        public float lado;             // -1 izquierda, +1 derecha
        public float retraso;          // segundos de "gelatina"
        public Vector3 actual, vel;
        public Vector3 ejeLargo;       // de hombro a mano, en el espacio del jugador (solo manos)
        public Quaternion giroActual = Quaternion.identity;
    }

    class Ojo
    {
        public Transform t;
        public Vector3 escala, posLocal;
        public int ejeVertical;
    }

    class Burbuja
    {
        public SpriteRenderer sr;
        public float visible;          // 0..1
        public float tiempo;           // desde que apareció
        public SpriteRenderer[] letras = new SpriteRenderer[0];
    }

    // Posición (x, y) y alto de cada Z, en píxeles de la imagen de la nube (512 x 512)
    static readonly Vector3[] LetrasZ = { new Vector3(190, 280, 190), new Vector3(300, 235, 140), new Vector3(385, 188, 100) };

    ControladorJugador ctrl;
    CamaraJugador camara;
    Transform modelo;
    Vector3 escalaModelo;
    Pieza cabeza, cuerpo, manoI, manoD, pieI, pieD;
    Ojo[] ojos = new Ojo[0];
    Burbuja burbujaZzz, burbujaExcl;

    float fase, tiempo, pesoMov, pesoCorrer, pesoAire, pesoSubida, pesoCaida;
    bool estabaEnSuelo = true;
    float tiempoEnAire;
    float rebote, tiempoRebote = 10f;
    float parpadeo, siguienteParpadeo = 2f; bool parpadeoDoble;
    float cierreAterrizaje, mirada, objetivoMirada, siguienteMirada = 4f;
    float quieto, sueno, sorpresa, tiempoExcl = 10f;

    // Personalización: voltear a la cámara y saltar de alegría con ojos "><"
    bool mirandoCamara;
    float alegria = 10f, pesoAlegria, elevacion;
    bool aterrizoAlegria = true;
    Transform[] ojosFelices = new Transform[0];

    void Awake()
    {
        ctrl = GetComponent<ControladorJugador>();
        camara = GetComponent<CamaraJugador>();
        cabeza = Crear("Cabeza", 0.09f);
        cuerpo = Crear("Cuerpo", 0.05f);
        manoI = Crear("Brazo_L", 0.1f); manoD = Crear("Brazo_R", 0.1f);
        pieI = Crear("Pierna_L", 0.04f); pieD = Crear("Pierna_R", 0.04f);
        modelo = cuerpo != null ? cuerpo.t.parent : null;
        while (modelo != null && modelo.parent != transform) modelo = modelo.parent;
        if (modelo != null) escalaModelo = modelo.localScale;

        var lista = new System.Collections.Generic.List<Ojo>();
        foreach (var nombre in new[] { "Ojo_L", "Ojo_R" })
        {
            var t = Buscar(nombre);
            if (t == null) continue;
            var o = new Ojo { t = t, escala = t.localScale, posLocal = t.localPosition };
            // El eje local del ojo que apunta hacia arriba es el que se aplasta al parpadear
            float mejor = -1f;
            for (int i = 0; i < 3; i++)
            {
                Vector3 eje = Vector3.zero; eje[i] = 1f;
                float d = Mathf.Abs(Vector3.Dot(t.TransformDirection(eje), transform.up));
                if (d > mejor) { mejor = d; o.ejeVertical = i; }
            }
            lista.Add(o);
        }
        ojos = lista.ToArray();
        CrearOjosFelices();

        burbujaZzz = CrearBurbuja("Burbuja_Zzz", texturaNube);
        burbujaExcl = CrearBurbuja("Burbuja_Exclamacion", texturaExclamacion);
        if (burbujaZzz != null && texturaLetraZ != null)
        {
            float u = tamanoBurbuja / 512f;   // metros por píxel de la nube
            burbujaZzz.letras = new SpriteRenderer[LetrasZ.Length];
            for (int i = 0; i < LetrasZ.Length; i++)
            {
                var go = new GameObject("Z" + (i + 1));
                go.transform.SetParent(burbujaZzz.sr.transform, false);
                go.transform.localPosition = new Vector3((LetrasZ[i].x - 256f) * u, (256f - LetrasZ[i].y) * u, -0.01f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = Sprite.Create(texturaLetraZ, new Rect(0, 0, texturaLetraZ.width, texturaLetraZ.height),
                                          new Vector2(0.5f, 0.5f), texturaLetraZ.height / (LetrasZ[i].z * u));
                sr.sortingOrder = 1;          // por delante de la nube
                burbujaZzz.letras[i] = sr;
            }
        }
    }

    Pieza Crear(string nombre, float retraso)
    {
        var t = Buscar(nombre);
        if (t == null) { Debug.LogWarning("AnimacionGel: no encontré la pieza " + nombre); return null; }
        var p = new Pieza
        {
            t = t,
            basePos = transform.InverseTransformPoint(t.position),
            baseRot = Quaternion.Inverse(transform.rotation) * t.rotation,
            retraso = retraso
        };
        p.lado = Mathf.Sign(p.basePos.x);
        p.ejeLargo = EjeLargo(t);
        return p;
    }

    float profundidadCuerpoGuardada = -1f;
    float MitadProfundidadCuerpo()
    {
        if (profundidadCuerpoGuardada < 0f)
        {
            var r = cuerpo != null ? cuerpo.t.GetComponent<Renderer>() : null;
            profundidadCuerpoGuardada = r != null ? r.bounds.extents.z : 0.2f;
        }
        return profundidadCuerpoGuardada;
    }

    float radioCabezaGuardado = -1f;
    float MitadAncho(Transform t)
    {
        if (radioCabezaGuardado < 0f)
        {
            var r = t.GetComponent<Renderer>();
            radioCabezaGuardado = r != null ? Mathf.Max(r.bounds.extents.x, r.bounds.extents.z) : 0.24f;
        }
        return radioCabezaGuardado;
    }

    // Dirección más larga de la pieza (análisis de sus vértices), apuntando hacia abajo:
    // en las manos va del hombro a la punta de la mano.
    Vector3 EjeLargo(Transform t)
    {
        var mf = t.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return Vector3.down;
        var v = mf.sharedMesh.vertices;
        if (v.Length < 3) return Vector3.down;
        var puntos = new Vector3[v.Length];
        Vector3 centro = Vector3.zero;
        for (int i = 0; i < v.Length; i++)
        {
            puntos[i] = transform.InverseTransformPoint(t.TransformPoint(v[i]));
            centro += puntos[i];
        }
        centro /= v.Length;
        // Iteración de potencia sobre la covarianza para hallar el eje principal
        Vector3 eje = new Vector3(0.3f, -1f, 0.2f).normalized;
        for (int k = 0; k < 16; k++)
        {
            Vector3 suma = Vector3.zero;
            foreach (var p in puntos)
            {
                Vector3 d = p - centro;
                suma += d * Vector3.Dot(d, eje);
            }
            if (suma.sqrMagnitude < 1e-12f) break;
            eje = suma.normalized;
        }
        return eje.y > 0f ? -eje : eje;
    }

    Burbuja CrearBurbuja(string nombre, Texture2D textura)
    {
        if (textura == null) return null;
        var go = new GameObject(nombre);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(textura, new Rect(0, 0, textura.width, textura.height),
                                  new Vector2(0.5f, 0.5f), textura.height / tamanoBurbuja);
        sr.color = new Color(1, 1, 1, 0);
        go.SetActive(false);
        return new Burbuja { sr = sr };
    }

    Transform Buscar(string nombre)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.name == nombre) return t;
        return null;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // ---------- Leer el movimiento ----------
        bool controlActivo = ctrl.enabled;
        float velocidad = controlActivo ? ctrl.VelocidadHorizontal.magnitude : 0f;
        bool enSuelo = !controlActivo || ctrl.EnSuelo;
        bool saltoAhora = controlActivo && ctrl.SaltoEsteFrame;
        bool enAire = !enSuelo && tiempoEnAire > 0.05f;
        pesoMov = Suavizar(pesoMov, velocidad > 0.1f ? 1f : 0f, 8f, dt);
        pesoCorrer = Suavizar(pesoCorrer, controlActivo && ctrl.Corriendo ? 1f : 0f, 6f, dt);
        pesoAire = Suavizar(pesoAire, enAire ? 1f : 0f, 10f, dt);
        pesoSubida = Suavizar(pesoSubida, enAire && ctrl.VelocidadVertical > 0f ? 1f : 0f, 10f, dt);
        pesoCaida = Suavizar(pesoCaida, enAire && ctrl.VelocidadVertical <= 0f ? 1f : 0f, 8f, dt);
        fase += velocidad * ciclosPorMetro * 2f * Mathf.PI * dt;

        // ---------- Salto y aterrizaje ----------
        if (saltoAhora) Rebotar(-0.12f);                       // se estira al despegar
        if (!enSuelo) tiempoEnAire += dt;
        if (enSuelo && !estabaEnSuelo && tiempoEnAire > 0.15f)
        {
            Rebotar(aplastamientoAlCaer);                                // se aplasta al caer
            cierreAterrizaje = 0.15f;
        }
        if (enSuelo) tiempoEnAire = 0f;
        estabaEnSuelo = enSuelo;

        // ---------- Dormir y despertar ----------
        bool activo = velocidad > 0.05f || !enSuelo || saltoAhora || mirandoCamara;
        if (activo)
        {
            if (Durmiendo) Despertar();
            quieto = 0f;
        }
        else quieto += dt;
        float objetivoSueno = Mathf.InverseLerp(segundosParaAdormecerse, segundosParaDormir, quieto);
        sueno = activo ? Suavizar(sueno, 0f, 12f, dt) : Suavizar(sueno, objetivoSueno, 1.5f, dt);
        sorpresa = Mathf.Max(0f, sorpresa - dt * 1.6f);

        // ---------- Personalización: mirar a la cámara y saltar de alegría ----------
        if (mirandoCamara)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 haciaCamara = cam.transform.position - transform.position;
                haciaCamara.y = 0f;
                if (haciaCamara.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.Slerp(transform.rotation,
                        Quaternion.LookRotation(haciaCamara), 1f - Mathf.Exp(-6f * dt));
            }
        }
        alegria += dt;
        elevacion = 0f;
        if (alegria < duracionAlegria)
        {
            float u = Mathf.Clamp01(alegria / (duracionAlegria * 0.65f));      // sube y baja en el 65% del tiempo
            elevacion = Mathf.Sin(u * Mathf.PI) * alturaSaltoAlegria;
            if (u >= 1f && !aterrizoAlegria) { Rebotar(aplastamientoAlCaer * 0.8f); aterrizoAlegria = true; }
        }
        pesoAlegria = Suavizar(pesoAlegria, alegria < duracionAlegria * 0.8f ? 1f : 0f, 14f, dt);

        // ---------- Valores de la animación ----------
        tiempo += dt * Mathf.Lerp(ciclosReposo, 0.35f, sueno);
        float suelo = 1f - pesoAire;
        float correr = pesoCorrer * pesoMov;
        float paso = Mathf.Lerp(pasoCaminar, pasoCorrer, pesoCorrer) * pesoMov * suelo;
        float alturaPaso = Mathf.Lerp(alturaPasoCaminar, alturaPasoCorrer, pesoCorrer) * pesoMov * suelo;
        float inclinacion = Mathf.Lerp(inclinacionCaminar, inclinacionCorrer, pesoCorrer) * pesoMov * suelo
                          - 6f * pesoSubida;                       // al saltar se echa un poco atrás
        float reposo = 1f - pesoMov;
        float flota = Mathf.Sin(tiempo * 2f * Mathf.PI) * flotacion * reposo * (1f + 0.5f * sueno);
        float flotaRetrasada = Mathf.Sin((tiempo - 0.15f) * 2f * Mathf.PI) * flotacion * reposo * (1f + 0.5f * sueno);
        float rebotePaso = -Mathf.Abs(Mathf.Sin(fase)) * Mathf.Lerp(0.025f, 0.04f, pesoCorrer) * pesoMov * suelo;
        float balanceo = Mathf.Sin(fase) * Mathf.Lerp(3f, 1.5f, pesoCorrer) * pesoMov;

        // Aplastar/estirar (gelatina)
        tiempoRebote += dt;
        float s = rebote * Mathf.Exp(-tiempoRebote * 6f) * Mathf.Cos(tiempoRebote * 18f);
        float respira = 1f + 0.015f * Mathf.Sin(tiempo * 2f * Mathf.PI) * reposo;
        float estiraAire = 0.05f * pesoSubida;
        Vector3 aplastar = new Vector3(1f + s * 0.6f - estiraAire * 0.5f, (1f - s + estiraAire) * respira, 1f + s * 0.6f - estiraAire * 0.5f);
        if (modelo != null) modelo.localScale = Vector3.Scale(escalaModelo, aplastar);

        // ---------- Cuerpo ----------
        Quaternion inclina = Quaternion.AngleAxis(inclinacion, Vector3.right) * Quaternion.AngleAxis(balanceo, Vector3.forward);
        Mover(cuerpo, new Vector3(0f, flota + rebotePaso, 0f), inclina, aplastar, dt);

        // ---------- Cabeza ----------
        float alturaCabeza = cabeza != null && cuerpo != null ? cabeza.basePos.y - cuerpo.basePos.y : 0.5f;
        Vector3 offCabeza = new Vector3(0f, flotaRetrasada + rebotePaso - 0.04f * sueno,
                                        Mathf.Sin(inclinacion * Mathf.Deg2Rad) * alturaCabeza);
        Quaternion giroCabeza = Quaternion.AngleAxis(inclinacion * 0.5f, Vector3.right)
                              * Quaternion.AngleAxis(balanceo * 0.6f + 12f * sueno, Vector3.forward);
        Mover(cabeza, offCabeza, giroCabeza, aplastar, dt);

        // ---------- Manos ----------
        // Además de moverse, cada mano GIRA para apuntar hacia donde corresponde:
        //   caminar -> se balancea adelante/atrás
        //   correr  -> acostada, apuntando hacia atrás (ninja)
        //   saltar  -> de pie, al lado de la cabeza (sin tocarla)
        //   caer    -> abierta en diagonal hacia arriba, agitándose
        float tiempoReal = Time.time;
        float radioCabeza = cabeza != null ? MitadAncho(cabeza.t) : 0.24f;
        foreach (var m in new[] { manoI, manoD })
        {
            if (m == null) continue;
            float f = fase + (m.lado > 0 ? 0f : Mathf.PI);
            float hombroY = (cuerpo != null ? cuerpo.basePos.y + 0.18f : 0.95f) - m.basePos.y;
            float ladoX = Mathf.Abs(m.basePos.x);

            // Posiciones (respecto a la posición de reposo de la mano)
            Vector3 caminar = new Vector3(0f, Mathf.Cos(f) * braceoCaminar * 0.45f, Mathf.Sin(f) * braceoCaminar);
            Vector3 ninja = new Vector3(m.lado * (0.3f - ladoX), hombroY + Mathf.Sin(fase * 2f + m.lado) * aleteoManos, -manosAtras);
            Vector3 poseSuelo = Vector3.Lerp(caminar, ninja, pesoCorrer) * pesoMov + new Vector3(0f, flotaRetrasada * 1.2f, 0f);
            float xCabeza = radioCabeza + 0.16f;   // separadas de la cabeza
            Vector3 subir = new Vector3(m.lado * (xCabeza - ladoX), hombroY + manosArribaAlSaltar * 0.45f, 0f);
            float agita = Mathf.Sin(tiempoReal * 18f + m.lado * 1.5f) * 0.05f;
            Vector3 caer = new Vector3(m.lado * (xCabeza + 0.08f - ladoX), hombroY + 0.12f + agita, 0f);
            float manosArriba = Mathf.Max(pesoSubida, pesoAlegria);
            Vector3 off = poseSuelo * suelo + subir * manosArriba + caer * pesoCaida;

            // Dormir: junta las manitos delante de la barriga (bien tierno)
            float alturaManitos = (cuerpo != null ? cuerpo.basePos.y - 0.04f : 0.72f) - m.basePos.y;
            float frente = MitadProfundidadCuerpo() + 0.09f - m.basePos.z;
            Vector3 dormir = new Vector3(m.lado * (0.07f - ladoX), alturaManitos + flotaRetrasada, frente);
            off = Vector3.Lerp(off, dormir, sueno);

            // Giros: hacia dónde apunta la mano (del hombro a la punta)
            Vector3 eje = m.ejeLargo;
            Quaternion gCaminar = Quaternion.AngleAxis(-Mathf.Sin(f) * 25f, Vector3.right);
            Quaternion gNinja = Quaternion.FromToRotation(eje, new Vector3(m.lado * 0.15f, 0.12f, -1f).normalized)
                              * Quaternion.identity;
            Quaternion gSubir = Quaternion.FromToRotation(eje, new Vector3(m.lado * 0.12f, 1f, 0f).normalized);
            Quaternion gCaer = Quaternion.FromToRotation(eje, new Vector3(m.lado * 0.6f, 0.8f, 0f).normalized)
                             * Quaternion.AngleAxis(agita * 200f, Vector3.forward);
            Quaternion objetivo = Quaternion.Slerp(Quaternion.identity, gCaminar, pesoMov * (1f - pesoCorrer));
            objetivo = Quaternion.Slerp(objetivo, gNinja, pesoMov * pesoCorrer * suelo);
            objetivo = Quaternion.Slerp(objetivo, gSubir, manosArriba);
            objetivo = Quaternion.Slerp(objetivo, gCaer, pesoCaida);
            // Dormida: las manitos se inclinan hacia adentro y se tocan de las puntas
            Quaternion gDormir = Quaternion.FromToRotation(eje, new Vector3(-m.lado * 0.45f, -1f, 0.2f).normalized);
            objetivo = Quaternion.Slerp(objetivo, gDormir, sueno);
            m.giroActual = Quaternion.Slerp(m.giroActual, objetivo, 1f - Mathf.Exp(-12f * dt));

            Mover(m, off, m.giroActual, aplastar, dt);
        }

        // ---------- Pies ----------
        foreach (var p in new[] { pieI, pieD })
        {
            if (p == null) continue;
            float f = fase + (p.lado > 0 ? Mathf.PI : 0f);
            Vector3 off = new Vector3(0f, Mathf.Max(0f, Mathf.Cos(f)) * alturaPaso, Mathf.Sin(f) * paso)
                        + new Vector3(0f, 0.06f, 0f) * pesoSubida                       // al subir los recoge
                        + new Vector3(p.lado * 0.05f, -0.07f, 0f) * pesoCaida;          // al caer cuelgan
            Mover(p, off, Quaternion.identity, aplastar, dt);
        }

        AnimarOjos(dt, reposo);
        AnimarBurbujas(dt);
    }

    // ---------- API para la personalización (la usa PanelColoresHUD) ----------
    public void MirarACamara(bool activar)
    {
        mirandoCamara = activar;
        if (activar) quieto = 0f;
    }

    public void SaltoDeAlegria()
    {
        alegria = 0f;
        aterrizoAlegria = false;
        Rebotar(-0.15f);
    }

    void OnEnable() { ColorPersonaje.AlCambiarColor += AlCambiarColor; }
    void OnDisable() { ColorPersonaje.AlCambiarColor -= AlCambiarColor; }
    void AlCambiarColor(int indice) { SaltoDeAlegria(); }

    // Ojos "><": cada ojo se duplica dos veces, más delgado, y las copias se giran
    // para formar ">" y "<". Se muestran solo durante el salto de alegría.
    void CrearOjosFelices()
    {
        var lista = new System.Collections.Generic.List<Transform>();
        foreach (var o in ojos)
        {
            Vector3 eje = Vector3.zero; eje[o.ejeVertical] = 1f;
            Vector3 ejeMundo = o.t.TransformDirection(eje).normalized;
            var r = o.t.GetComponent<Renderer>();
            float alto = r != null ? r.bounds.size.y : 0.1f;
            Vector3 centro = o.t.position;
            // +X del jugador se ve a la izquierda de quien lo mira: ahí va ">", y "<" en el otro ojo
            float ladoVista = Mathf.Sign(transform.InverseTransformPoint(centro).x);
            Vector3 punta = centro - transform.right * (ladoVista * 0.3f * alto);
            Vector3 normal = cabeza != null ? (punta - cabeza.t.position).normalized : transform.forward;
            float ang = 32f * Mathf.Deg2Rad, largo = 0.3f * alto;
            for (int k = -1; k <= 1; k += 2)
            {
                Vector3 trazo = (transform.right * (ladoVista * Mathf.Cos(ang)) + transform.up * (k * Mathf.Sin(ang))).normalized;
                var copia = Instantiate(o.t.gameObject, o.t.parent);
                copia.name = o.t.name + (k < 0 ? "_FelizAbajo" : "_FelizArriba");
                copia.transform.position = punta + trazo * largo + normal * 0.006f;
                copia.transform.rotation = Quaternion.FromToRotation(ejeMundo, trazo) * o.t.rotation;
                Vector3 escala = o.escala * 0.55f;
                escala[o.ejeVertical] = o.escala[o.ejeVertical] * 0.62f;
                copia.transform.localScale = escala;
                copia.SetActive(false);
                lista.Add(copia.transform);
            }
        }
        ojosFelices = lista.ToArray();
    }

    void Despertar()
    {
        sorpresa = 1f;
        Rebotar(-0.18f);
        sueno = 0f;
        tiempoExcl = 0f;
    }

    void AnimarOjos(float dt, float reposo)
    {
        // Parpadeo (a veces doble); dormido no parpadea
        siguienteParpadeo -= dt;
        if (siguienteParpadeo <= 0f && parpadeo <= 0f && sueno < 0.3f && pesoCaida < 0.5f)
        {
            parpadeo = 0.18f;
            parpadeoDoble = Random.value < 0.2f;
            siguienteParpadeo = Random.Range(parpadeoMin, parpadeoMax) * (1f + sueno * 2f);
        }
        if (parpadeo > 0f)
        {
            parpadeo -= dt;
            if (parpadeo <= 0f && parpadeoDoble) { parpadeoDoble = false; parpadeo = 0.18f; }
        }
        float abrir = parpadeo > 0f ? Mathf.Max(0.08f, Mathf.Abs((parpadeo - 0.09f) / 0.09f)) : 1f;
        if (cierreAterrizaje > 0f) { cierreAterrizaje -= dt; abrir = Mathf.Min(abrir, 0.15f); }

        abrir *= Mathf.Lerp(1f, 0.75f, pesoCorrer * pesoMov);   // entrecierra al correr
        abrir *= Mathf.Lerp(1f, 0.12f, sueno);                   // dormido: rayitas
        float grandes = 1f + 0.3f * sorpresa + 0.15f * pesoCaida; // sorpresa o caída: ojos grandes

        // Mirar a los lados de vez en cuando (solo en reposo y despierto)
        siguienteMirada -= dt;
        if (siguienteMirada <= 0f)
        {
            objetivoMirada = (reposo > 0.9f && sueno < 0.1f && Random.value < 0.6f) ? (Random.value < 0.5f ? -1f : 1f) : 0f;
            siguienteMirada = objetivoMirada != 0f ? 0.9f : Random.Range(3f, 6f);
        }
        if (reposo < 0.9f || sueno > 0.1f) objetivoMirada = 0f;
        mirada = Suavizar(mirada, objetivoMirada, 8f, dt);

        bool primeraPersona = camara != null && camara.PrimeraPersona;
        bool feliz = pesoAlegria > 0.5f && !primeraPersona;
        foreach (var f in ojosFelices)
            if (f.gameObject.activeSelf != feliz) f.gameObject.SetActive(feliz);

        foreach (var o in ojos)
        {
            var rOjo = o.t.GetComponent<Renderer>();
            if (rOjo != null && !primeraPersona) rOjo.enabled = !feliz;
            Vector3 e = o.escala * grandes;
            e[o.ejeVertical] = o.escala[o.ejeVertical] * abrir * grandes;
            o.t.localScale = e;
            Vector3 desplazamiento = transform.TransformVector(new Vector3(mirada * 0.012f, -0.006f * sueno, 0f));
            o.t.localPosition = o.posLocal + (o.t.parent != null ? o.t.parent.InverseTransformVector(desplazamiento) : desplazamiento);
        }
    }

    void AnimarBurbujas(float dt)
    {
        bool primeraPersona = camara != null && camara.PrimeraPersona;
        tiempoExcl += dt;
        bool mostrarExcl = tiempoExcl < 1.1f && !primeraPersona;
        bool mostrarZzz = sueno > 0.6f && !mostrarExcl && !primeraPersona;
        Actualizar(burbujaZzz, mostrarZzz, dt, true);
        Actualizar(burbujaExcl, mostrarExcl, dt, false);
    }

    void Actualizar(Burbuja b, bool mostrar, float dt, bool flotar)
    {
        if (b == null) return;
        if (mostrar && b.visible <= 0f) b.tiempo = 0f;
        b.visible = Mathf.MoveTowards(b.visible, mostrar ? 1f : 0f, dt * (mostrar ? 4f : 3f));
        bool activa = b.visible > 0f;
        if (b.sr.gameObject.activeSelf != activa) b.sr.gameObject.SetActive(activa);
        if (!activa) return;
        b.tiempo += dt;

        // Aparece con un rebote elástico y luego flota suave
        float t = b.tiempo;
        float pop = 1f + 0.35f * Mathf.Exp(-t * 7f) * Mathf.Sin(t * 22f);
        float escala = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t * 5f)) * pop;
        if (flotar) escala *= 1f + 0.04f * Mathf.Sin(t * 2.2f);
        float sube = flotar ? 0.03f * Mathf.Sin(t * 1.6f) : 0.08f * (1f - Mathf.Exp(-t * 6f));

        Vector3 baseCabeza = cabeza != null ? cabeza.t.position : transform.position + Vector3.up * 1.35f;
        Camera cam = Camera.main;
        Vector3 derecha = cam != null ? cam.transform.right : transform.right;
        b.sr.transform.position = baseCabeza + derecha * posicionBurbuja.x + Vector3.up * (posicionBurbuja.y + sube);
        if (cam != null) b.sr.transform.rotation = cam.transform.rotation;   // siempre mira a la cámara
        b.sr.transform.localScale = Vector3.one * escala;
        b.sr.color = new Color(1f, 1f, 1f, b.visible);

        // Letras Z: aparecen una por una (Z -> Zz -> Zzz), luego se desvanecen y se repite
        float c = (t - 0.4f) % cicloZzz;
        for (int i = 0; i < b.letras.Length; i++)
        {
            float inicio = i * 0.5f;
            float dentro = t < 0.4f ? -1f : c - inicio;
            float alfa = dentro < 0f ? 0f : Mathf.Clamp01(dentro * 6f) * Mathf.Clamp01((cicloZzz - 0.5f - c) * 3f);
            float popLetra = dentro < 0f ? 0f
                : Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, dentro * 6f)) * (1f + 0.4f * Mathf.Exp(-dentro * 8f) * Mathf.Sin(dentro * 25f));
            b.letras[i].transform.localScale = Vector3.one * popLetra;
            b.letras[i].color = new Color(1f, 1f, 1f, alfa * b.visible);
        }
    }

    void Mover(Pieza p, Vector3 offset, Quaternion giro, Vector3 aplastar, float dt)
    {
        if (p == null) return;
        p.actual = Vector3.SmoothDamp(p.actual, offset, ref p.vel, p.retraso, Mathf.Infinity, dt);
        Vector3 local = Vector3.Scale(p.basePos + p.actual, aplastar);
        p.t.position = transform.TransformPoint(local + Vector3.up * elevacion);
        p.t.rotation = transform.rotation * giro * p.baseRot;
    }

    void Rebotar(float cantidad)
    {
        rebote = cantidad;
        tiempoRebote = 0f;
    }

    static float Suavizar(float actual, float objetivo, float rapidez, float dt)
    {
        return Mathf.Lerp(actual, objetivo, 1f - Mathf.Exp(-rapidez * dt));
    }
}
