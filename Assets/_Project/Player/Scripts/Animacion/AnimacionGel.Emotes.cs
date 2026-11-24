// HU-06: emotes del personaje de gel (parte de AnimacionGel, en un archivo aparte).
// 9 emotes hechos por código, igual que el resto de las animaciones:
//   1 Saludar   2 Bailar   3 Aplaudir
//   4 Pensar    5 Amor     6 Mareado
//   7 Enojado   8 Sorpresa 9 Gelatina
// Cada emote es una "pose" que cambia con el tiempo; se mezcla suave con la animación normal.
// Solo se puede hacer en el suelo y quieto; si el jugador se mueve o salta, el emote se corta.
// Bailar tiene una estela de pentagrama con notas (AnimacionGel.Estela.cs); otros sueltan partículas (corazones, vapor, "?"); Mareado gira y los ojos se deforman en espiral.
// Gelatina es especial: se derrite en un charco que se queda (ver AnimacionGel.Charco.cs).
// Todos mantienen el carácter tierno del personaje.
using System.Collections.Generic;
using UnityEngine;

public partial class AnimacionGel
{
    public static readonly string[] NombresEmotes =
        { "Saludar", "Bailar", "Aplaudir", "Pensar", "Amor", "Mareado", "Enojado", "Sorpresa", "Gelatina" };
    static readonly float[] DuracionEmote = { 2.4f, 4.6f, 2.2f, 3.2f, 2.8f, 3.4f, 2.6f, 1.8f, 2.6f };

    [Header("Emotes (HU-06)")]
    public Texture2D texturaNota;
    public Texture2D texturaCorazon;
    public Texture2D texturaVapor;
    public Texture2D texturaPregunta;      // burbuja "?" (Pensar)
    public Texture2D texturaOndas;         // ondas ")))" junto a la mano (Saludar)
    public float tamanoParticula = 0.2f;

    public bool EnEmote => emote >= 0;
    public int EmoteActual => emote;

    int emote = -1, emoteMostrado = -1;
    float tEmote, pesoEmote, siguienteParticula;
    PoseEmote pose;

    struct PoseEmote
    {
        public Vector3 cuerpo, cabeza;
        public Quaternion giroCuerpo, giroCabeza;
        public Vector3 manoPosI, manoPosD;      // posición de cada mano en el espacio del jugador
        public Vector3 manoDirI, manoDirD;      // hacia dónde apunta (cero = sin girar)
        public Vector3 pieI, pieD;
        public Vector3 escala;
        public float elevar, abrirOjos, ojosGrandes;
        public float giro;                      // vueltas del cuerpo entero, en grados (Mareado)
        public bool ojosFelices, ojosEspiral;
    }

    // ---------- API (la usa PanelEmotesHUD) ----------
    /// Empieza un emote (0..8). Devuelve false si no se puede (en el aire o moviéndose).
    public bool HacerEmote(int indice)
    {
        if (indice < 0 || indice >= NombresEmotes.Length) return false;
        if (EnCharco) return false;                                         // en charco solo se sale con G
        if (ctrl == null || !ctrl.Activo || !ctrl.EnSuelo || ctrl.VelocidadHorizontal.magnitude > 0.15f) return false;
        if (indice == 8) { EntrarCharco(); return true; }                   // Gelatina: se queda derretido
        emote = emoteMostrado = indice;
        tEmote = 0f;
        siguienteParticula = 0f;
        quieto = 0f;
        if (Durmiendo) Despertar();
        Rebotar(-0.1f);
        if (indice == 7) { sorpresa = 1f; tiempoExcl = 0f; }                       // Sorpresa: burbuja "!"
        return true;
    }

    public void CancelarEmote() { emote = -1; }

    // Se llama en Update después de leer el movimiento
    void ActualizarEmote(float dt, float velocidad, bool enSuelo, bool saltoAhora)
    {
        if (emote >= 0)
        {
            if (velocidad > 0.15f || !enSuelo || saltoAhora) emote = -1;       // moverse corta el emote
            else
            {
                tEmote += dt;
                if (tEmote >= DuracionEmote[emote]) emote = -1;
                else EmitirParticulas(dt);
            }
        }
        pesoEmote = Suavizar(pesoEmote, emote >= 0 ? 1f : 0f, emote >= 0 ? 10f : 6f, dt);
        if (pesoEmote < 0.001f) emoteMostrado = -1;
        pose = emoteMostrado >= 0 ? CalcularPose(emoteMostrado, tEmote) : PoseNeutra();
        ActualizarParticulas(dt);
        ActualizarEstela(dt);                       // Bailar: pentagrama en espiral con notas
        ActualizarEnojo();                          // Enojado: la cabeza se pone roja arriba
        ActualizarOndasMano();                      // Saludar: ondas junto a la mano
    }

    PoseEmote PoseNeutra()
    {
        return new PoseEmote
        {
            giroCuerpo = Quaternion.identity, giroCabeza = Quaternion.identity,
            manoPosI = manoI != null ? manoI.basePos : Vector3.zero,
            manoPosD = manoD != null ? manoD.basePos : Vector3.zero,
            escala = Vector3.one, abrirOjos = 1f, ojosGrandes = 1f
        };
    }

    // ---------- Las 9 poses ----------
    PoseEmote CalcularPose(int e, float t)
    {
        var p = PoseNeutra();
        float yHombro = (cuerpo != null ? cuerpo.basePos.y : 0.75f) + 0.18f;
        float yPanza = (cuerpo != null ? cuerpo.basePos.y : 0.75f) - 0.04f;
        float yCabeza = cabeza != null ? cabeza.basePos.y : 1.3f;
        float rCab = cabeza != null ? MitadAncho(cabeza.t) : 0.24f;
        float frente = MitadProfundidadCuerpo() + 0.09f;
        const float PI2 = Mathf.PI * 2f;

        switch (e)
        {
            case 0: // Saludar: una mano arriba moviéndose de lado a lado
            {
                float w = Mathf.Sin(t * 12f);
                Mano(ref p, +1, new Vector3(rCab + 0.12f + w * 0.05f, yCabeza + 0.08f, 0.05f), new Vector3(0.25f + w * 0.35f, 1f, 0.1f));
                p.giroCabeza = Quaternion.AngleAxis(-7f, Vector3.forward);
                p.giroCuerpo = Quaternion.AngleAxis(4f, Vector3.forward);
                p.ojosFelices = true;
                break;
            }
            case 1: // Bailar: rebota, se balancea, brazos arriba y abajo, pies alternados
            {
                float b = Mathf.Abs(Mathf.Sin(PI2 * 1.8f * t));
                float sw = Mathf.Sin(Mathf.PI * 1.8f * t);
                p.cuerpo = new Vector3(sw * 0.04f, -b * 0.04f, 0f);
                p.giroCuerpo = Quaternion.AngleAxis(sw * 10f, Vector3.forward);
                p.cabeza = new Vector3(sw * 0.06f, -b * 0.03f, 0f);
                p.giroCabeza = Quaternion.AngleAxis(-sw * 8f, Vector3.forward);
                foreach (int lado in new[] { -1, 1 })
                {
                    float arriba = 0.5f + 0.5f * Mathf.Sin(PI2 * 0.9f * t + (lado > 0 ? 0f : Mathf.PI));
                    Mano(ref p, lado, new Vector3(0.33f, yHombro - 0.05f + arriba * 0.3f, 0.04f), new Vector3(0.8f, -0.3f + arriba * 1.2f, 0f));
                    Vector3 pie = new Vector3(0f, Mathf.Max(0f, Mathf.Sin(PI2 * 0.9f * t + (lado > 0 ? 0f : Mathf.PI))) * 0.07f, 0f);
                    if (lado < 0) p.pieI = pie; else p.pieD = pie;
                }
                p.ojosFelices = true;
                break;
            }
            case 2: // Aplaudir: manos que se juntan frente al pecho y saltitos
            {
                float abierto = 0.5f + 0.5f * Mathf.Cos(PI2 * 3f * t);
                foreach (int lado in new[] { -1, 1 })
                    Mano(ref p, lado, new Vector3(0.035f + 0.07f * abierto, yHombro - 0.06f, frente), new Vector3(-0.6f, 0.35f, 0.7f));
                p.elevar = Mathf.Abs(Mathf.Sin(PI2 * 1.5f * t)) * 0.05f;
                p.ojosFelices = true;
                break;
            }
            case 3: // Pensar: una mano en la barbilla, el otro brazo cruzado debajo, cabeza ladeada y "?"
            {
                Vector3 dirBarbilla = new Vector3(-0.25f, 1f, 0.35f).normalized;
                Vector3 barbilla = new Vector3(0.04f, yCabeza - rCab * 0.75f, rCab * 0.55f);
                Mano(ref p, +1, barbilla - dirBarbilla * 0.11f, dirBarbilla);
                Mano(ref p, -1, new Vector3(-0.03f, yPanza + 0.03f, frente), new Vector3(-0.95f, 0.15f, 0.25f));
                float hmm = Mathf.Sin(t * 1.6f);
                p.giroCabeza = Quaternion.AngleAxis(-8f, Vector3.right) * Quaternion.AngleAxis(-10f + hmm * 4f, Vector3.forward);
                p.cabeza = new Vector3(0.015f, 0f, 0f);
                p.giroCuerpo = Quaternion.AngleAxis(-3f + hmm * 1.5f, Vector3.forward);
                break;
            }
            case 4: // Amor: manos al pecho, se mece y suben corazones
            {
                float sw = Mathf.Sin(PI2 * 0.7f * t);
                p.giroCuerpo = Quaternion.AngleAxis(sw * 7f, Vector3.forward);
                p.giroCabeza = Quaternion.AngleAxis(sw * 10f, Vector3.forward);
                foreach (int lado in new[] { -1, 1 })
                    Mano(ref p, lado, new Vector3(0.06f, yHombro - 0.08f, frente), new Vector3(-0.8f, -0.1f, 0.6f));
                p.abrirOjos = 0.15f;
                break;
            }
            case 5: // Mareado: da dos vueltas con los brazos abiertos y queda tambaleándose con ojos en espiral
            {
                if (t < 1f)
                {
                    float u = t;
                    p.giro = 720f * (1f - (1f - u) * (1f - u));
                    foreach (int lado in new[] { -1, 1 })
                        Mano(ref p, lado, new Vector3(0.38f, yHombro, 0f), new Vector3(1f, 0.15f, 0f));
                    p.elevar = Mathf.Sin(u * Mathf.PI) * 0.03f;
                    p.abrirOjos = 0.6f;
                }
                else
                {
                    p.giro = 720f;
                    float c = (t - 1f) * 5f;
                    p.cuerpo = new Vector3(Mathf.Cos(c) * 0.04f, 0f, Mathf.Sin(c) * 0.04f);
                    p.giroCuerpo = Quaternion.AngleAxis(Mathf.Sin(c) * 9f, Vector3.forward) * Quaternion.AngleAxis(Mathf.Cos(c) * 6f, Vector3.right);
                    p.cabeza = new Vector3(Mathf.Cos(c - 0.6f) * 0.07f, -0.02f, Mathf.Sin(c - 0.6f) * 0.07f);
                    p.giroCabeza = Quaternion.AngleAxis(Mathf.Sin(c - 0.6f) * 12f, Vector3.forward);
                    foreach (int lado in new[] { -1, 1 })
                    {
                        Mano(ref p, lado, new Vector3(0.27f, yHombro - 0.12f + Mathf.Sin(c + lado) * 0.03f, 0.02f), new Vector3(0.45f, -1f, 0.1f));
                        Vector3 pie = new Vector3(0f, Mathf.Max(0f, Mathf.Sin(c * 0.5f + (lado > 0 ? 0f : Mathf.PI))) * 0.04f, 0f);
                        if (lado < 0) p.pieI = pie; else p.pieD = pie;
                    }
                }
                p.ojosEspiral = t > 0.5f;
                break;
            }
            case 6: // Enojado: patalea y sacude los puños arriba
            {
                p.cuerpo = new Vector3(Mathf.Sin(t * 35f) * 0.01f, -Mathf.Abs(Mathf.Sin(PI2 * 1.5f * t)) * 0.02f, 0f);
                p.giroCuerpo = Quaternion.AngleAxis(8f, Vector3.right);
                foreach (int lado in new[] { -1, 1 })
                {
                    Mano(ref p, lado, new Vector3(0.32f, yHombro + 0.04f + Mathf.Sin(t * 30f + lado) * 0.025f, 0.08f), new Vector3(0.45f, 0.9f, 0.15f));
                    Vector3 pie = new Vector3(0f, Mathf.Max(0f, Mathf.Sin(PI2 * 1.5f * t + (lado > 0 ? 0f : Mathf.PI))) * 0.09f, 0f);
                    if (lado < 0) p.pieI = pie; else p.pieD = pie;
                }
                p.abrirOjos = 0.55f;
                break;
            }
            case 7: // Sorpresa: brinca hacia atrás con los brazos arriba y ojos grandes
            {
                float u = Mathf.Clamp01(t / 0.45f);
                p.elevar = Mathf.Sin(u * Mathf.PI) * 0.14f;
                p.cuerpo = new Vector3(0f, 0f, -0.08f * Mathf.SmoothStep(0f, 1f, u));
                p.cabeza = p.cuerpo;
                p.giroCuerpo = Quaternion.AngleAxis(-8f, Vector3.right);
                foreach (int lado in new[] { -1, 1 })
                    Mano(ref p, lado, new Vector3(rCab + 0.22f, yCabeza + 0.02f, -0.04f), new Vector3(0.75f, 0.65f, 0f));
                p.ojosGrandes = 1.35f;
                break;
            }
        }
        return p;
    }

    // Pone una mano: "pos" y "dir" se dan para el lado derecho (x positiva) y se reflejan para el izquierdo
    void Mano(ref PoseEmote p, int lado, Vector3 pos, Vector3 dir)
    {
        Vector3 posLado = new Vector3(pos.x * lado, pos.y, pos.z);
        Vector3 dirLado = new Vector3(dir.x * lado, dir.y, dir.z);
        bool esDerecha = manoD != null && Mathf.Sign(manoD.lado) == Mathf.Sign(lado);
        if (esDerecha) { p.manoPosD = posLado; p.manoDirD = dirLado; }
        else { p.manoPosI = posLado; p.manoDirI = dirLado; }
    }

    // ---------- Saludar: ondas ")))" a los lados de la mano que saluda ----------
    // Dos ondas que siguen a la mano; la del lado hacia donde va la mano se agranda y brilla.
    SpriteRenderer[] ondasMano = new SpriteRenderer[0];

    void CrearOndasMano()
    {
        if (spOndas == null) return;
        ondasMano = new SpriteRenderer[2];
        for (int i = 0; i < 2; i++)
        {
            var go = new GameObject(i == 0 ? "OndasMano_Izq" : "OndasMano_Der");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = spOndas;
            sr.flipX = i == 0;
            sr.sortingOrder = 2;
            go.SetActive(false);
            ondasMano[i] = sr;
        }
    }

    void ActualizarOndasMano()
    {
        if (ondasMano.Length == 0) return;
        bool primeraPersona = camara != null && camara.PrimeraPersona;
        float peso = emoteMostrado == 0 && !primeraPersona ? pesoEmote : 0f;
        var mano = manoD != null && manoD.lado > 0f ? manoD : manoI;
        var cam = Camera.main;
        for (int i = 0; i < 2; i++)
        {
            var sr = ondasMano[i];
            bool ver = peso > 0.01f && mano != null;
            if (sr.gameObject.activeSelf != ver) sr.gameObject.SetActive(ver);
            if (!ver) continue;
            float lado = i == 0 ? -1f : 1f;
            // la mano se mueve con sin(t*12): su velocidad es cos(t*12); la onda de ese lado "empuja"
            float empuje = Mathf.Max(0f, Mathf.Cos(tEmote * 12f) * lado);
            Vector3 der = cam != null ? cam.transform.right : transform.right;
            sr.transform.position = mano.t.position + Vector3.up * 0.16f + der * (lado * (0.15f + 0.04f * empuje));
            if (cam != null) sr.transform.rotation = cam.transform.rotation;
            sr.transform.localScale = Vector3.one * (1.1f + 0.45f * empuje);
            sr.color = new Color(1f, 1f, 1f, peso * (0.35f + 0.65f * empuje));
        }
    }

    // ---------- Enojado: degradado rojo en la parte de arriba de la cabeza ----------
    MaterialPropertyBlock bloqueEnojo;
    Renderer rendCabeza;
    float enojoAplicado;
    static readonly int IdEnojo = Shader.PropertyToID("_Enojo");
    static readonly int IdEnojoDesde = Shader.PropertyToID("_EnojoDesde");
    static readonly int IdEnojoHasta = Shader.PropertyToID("_EnojoHasta");

    void ActualizarEnojo()
    {
        if (cabeza == null) return;
        if (rendCabeza == null) rendCabeza = cabeza.t.GetComponent<Renderer>();
        if (rendCabeza == null) return;
        float objetivo = emoteMostrado == 6 ? pesoEmote * (0.85f + 0.15f * Mathf.Sin(Time.time * 9f)) : 0f;   // late, como "echando humo"
        if (objetivo < 0.001f && enojoAplicado < 0.001f) return;
        enojoAplicado = objetivo;
        if (bloqueEnojo == null) bloqueEnojo = new MaterialPropertyBlock();
        var b = rendCabeza.bounds;
        rendCabeza.GetPropertyBlock(bloqueEnojo);
        bloqueEnojo.SetFloat(IdEnojo, objetivo);
        bloqueEnojo.SetFloat(IdEnojoDesde, b.center.y - b.extents.y * 0.15f);   // empieza un poco abajo de la mitad
        bloqueEnojo.SetFloat(IdEnojoHasta, b.max.y - b.extents.y * 0.1f);       // casi arriba, rojo fuerte
        rendCabeza.SetPropertyBlock(bloqueEnojo);
    }

    // ---------- Partículas ----------
    class Particula
    {
        public SpriteRenderer sr;
        public Vector3 vel;
        public float vida, total, escala, gravedad;
    }

    readonly List<Particula> particulas = new List<Particula>();
    Sprite spNota, spCorazon, spVapor, spPregunta, spOndas;

    void CrearSpritesEmotes()
    {
        spNota = SpriteDe(texturaNota);
        spCorazon = SpriteDe(texturaCorazon);
        spVapor = SpriteDe(texturaVapor);
        spPregunta = SpriteDe(texturaPregunta);
        spOndas = SpriteDe(texturaOndas);
        CrearOndasMano();
        CrearOjosEspiral();
        CrearCharco();
        CrearEstela();
    }

    // ---------- Ojos en espiral (Mareado) ----------
    // Cada ojo se "deforma" en una espiral 3D hecha con su mismo material (azul marino brillante):
    // el ojo normal se aplasta mientras la espiral crece con un rebote, y luego gira.
    Transform[] ojosEspiral = new Transform[0];
    float pesoEspiral;

    void CrearOjosEspiral()
    {
        if (cabeza == null) return;
        var lista = new List<Transform>();
        foreach (var o in ojos)
        {
            var r = o.t.GetComponent<Renderer>();
            if (r == null) continue;
            float alto = r.bounds.size.y;
            Vector3 normal = (o.t.position - cabeza.t.position).normalized;
            var go = new GameObject(o.t.name + "_Espiral");
            go.transform.SetParent(o.t.parent, true);
            go.transform.position = o.t.position + normal * 0.004f;
            go.transform.rotation = Quaternion.LookRotation(normal, transform.up);
            go.AddComponent<MeshFilter>().sharedMesh = MallaEspiral(alto * 0.62f, alto * 0.085f);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = r.sharedMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.localScale = Vector3.zero;
            go.SetActive(false);
            lista.Add(go.transform);
        }
        ojosEspiral = lista.ToArray();
    }

    // Tubo en forma de espiral (2 vueltas) en el plano XY, un poco abombado hacia +Z
    static Mesh MallaEspiral(float radio, float grosor)
    {
        const int Pasos = 90, Lados = 8;
        const float Vueltas = 2f;
        var v = new List<Vector3>(); var tri = new List<int>();
        for (int i = 0; i < Pasos; i++)
        {
            float u = i / (Pasos - 1f);
            float ang = u * Vueltas * Mathf.PI * 2f;
            float r = radio * (0.08f + 0.92f * u);
            Vector3 c = new Vector3(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r, (1f - u * u) * radio * 0.12f);
            Vector3 tang = new Vector3(-Mathf.Sin(ang), Mathf.Cos(ang), 0f);
            Vector3 n1 = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f), n2 = Vector3.forward;
            float g = grosor * Mathf.Lerp(0.8f, 1f, u);
            for (int k = 0; k < Lados; k++)
            {
                float a = k / (float)Lados * Mathf.PI * 2f;
                v.Add(c + (n1 * Mathf.Cos(a) + n2 * Mathf.Sin(a) * 0.7f) * g);
            }
        }
        for (int i = 0; i < Pasos - 1; i++)
            for (int k = 0; k < Lados; k++)
            {
                int a0 = i * Lados + k, a1 = i * Lados + (k + 1) % Lados, b0 = a0 + Lados, b1 = a1 + Lados;
                tri.Add(a0); tri.Add(b0); tri.Add(a1);
                tri.Add(a1); tri.Add(b0); tri.Add(b1);
            }
        var m = new Mesh { name = "OjoEspiral" };
        m.SetVertices(v); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    // La llama AnimarOjos: transforma los ojos en espiral y las hace girar.
    // Devuelve cuánto se ve la espiral (0..1) para aplastar el ojo normal.
    float AnimarOjosEspiral(float dt, bool primeraPersona)
    {
        bool ver = pose.ojosEspiral && pesoEmote > 0.5f && !primeraPersona && ojosEspiral.Length > 0;
        pesoEspiral = Mathf.MoveTowards(pesoEspiral, ver ? 1f : 0f, dt * 5f);
        float pop = pesoEspiral <= 0f ? 0f : Mathf.SmoothStep(0f, 1f, pesoEspiral) * (1f + 0.25f * Mathf.Sin(pesoEspiral * Mathf.PI));
        for (int i = 0; i < ojosEspiral.Length; i++)
        {
            var e = ojosEspiral[i];
            bool activa = pesoEspiral > 0f;
            if (e.gameObject.activeSelf != activa) e.gameObject.SetActive(activa);
            if (!activa) continue;
            e.localScale = Vector3.one * pop;
            e.Rotate(0f, 0f, (i % 2 == 0 ? 1f : -1f) * 420f * dt, Space.Self);
        }
        return pesoEspiral;
    }

    Sprite SpriteDe(Texture2D tex)
    {
        if (tex == null) return null;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.height / tamanoParticula);
    }

    // Punto junto a la cabeza: "derecha" según la cámara (para que siempre se vea) y "arriba"
    Vector3 PuntoCabeza(float derecha, float arriba)
    {
        Vector3 c = cabeza != null ? cabeza.t.position : transform.position + Vector3.up * 1.3f;
        var cam = Camera.main;
        Vector3 der = cam != null ? cam.transform.right : transform.right;
        return c + der * derecha + Vector3.up * arriba;
    }

    void EmitirParticulas(float dt)
    {
        siguienteParticula -= dt;
        if (siguienteParticula > 0f) return;
        float rCab = cabeza != null ? MitadAncho(cabeza.t) : 0.24f;
        switch (emote)
        {
            case 2: // Aplaudir: en cada palmada salen ondas de sonido a los dos lados de las manos
            {
                const float Periodo = 1f / 3f;                       // 3 palmadas por segundo (igual que la pose)
                float fase = Mathf.Repeat(tEmote - Periodo * 0.5f, Periodo);   // las manos se juntan en t = 1/6, 1/2, ...
                if (tEmote < Periodo * 0.5f) { siguienteParticula = Periodo * 0.5f - tEmote; break; }
                if (manoI != null && manoD != null)
                {
                    var cam = Camera.main;
                    Vector3 der = cam != null ? cam.transform.right : transform.right;
                    Vector3 centro = (manoI.t.position + manoD.t.position) * 0.5f + Vector3.up * 0.04f;
                    foreach (float lado in new[] { -1f, 1f })
                    {
                        var onda = Emitir(spOndas, centro + der * (lado * 0.12f), der * (lado * 0.35f) + Vector3.up * 0.05f, 0.55f, 1.15f, 0f);
                        if (onda != null) onda.sr.flipX = lado < 0f;
                    }
                }
                siguienteParticula = Periodo - fase;
                break;
            }
            case 4: // Amor: corazones que suben
                Emitir(spCorazon, PuntoCabeza(Random.Range(-0.2f, 0.2f), rCab + 0.05f),
                       new Vector3(Random.Range(-0.1f, 0.1f), 0.4f, 0f), 1.4f, Random.Range(0.7f, 1.1f), 0f);
                siguienteParticula = 0.5f; break;
            case 3: // Pensar: aparece un "?" junto a la cabeza (dos veces)
                Emitir(spPregunta, PuntoCabeza(rCab + 0.12f, rCab * 0.6f), new Vector3(0f, 0.06f, 0f), 1.4f, 1.3f, 0f);
                siguienteParticula = 1.5f; break;
            case 6: // Enojado: vapor sobre la cabeza
                Emitir(spVapor, PuntoCabeza(Random.Range(-0.12f, 0.12f), rCab + 0.05f),
                       new Vector3(Random.Range(-0.15f, 0.15f), 0.35f, 0f), 0.9f, Random.Range(0.7f, 1f), 0f);
                siguienteParticula = 0.3f; break;
            default:
                siguienteParticula = 10f; break;
        }
    }

    Particula Emitir(Sprite sp, Vector3 pos, Vector3 vel, float vida, float escala, float gravedad)
    {
        if (sp == null || (camara != null && camara.PrimeraPersona)) return null;
        Particula p = null;
        foreach (var q in particulas) if (!q.sr.gameObject.activeSelf) { p = q; break; }
        if (p == null)
        {
            var go = new GameObject("ParticulaEmote");
            go.transform.SetParent(transform, false);
            p = new Particula { sr = go.AddComponent<SpriteRenderer>() };
            p.sr.sortingOrder = 2;
            particulas.Add(p);
        }
        p.sr.sprite = sp;
        p.sr.flipX = false;
        p.sr.transform.position = pos;
        p.vel = vel; p.vida = 0f; p.total = vida; p.escala = escala; p.gravedad = gravedad;
        p.sr.gameObject.SetActive(true);
        return p;
    }

    void ActualizarParticulas(float dt)
    {
        var cam = Camera.main;
        foreach (var p in particulas)
        {
            if (!p.sr.gameObject.activeSelf) continue;
            p.vida += dt;
            if (p.vida >= p.total) { p.sr.gameObject.SetActive(false); continue; }
            p.vel.y -= p.gravedad * dt;
            p.sr.transform.position += p.vel * dt;
            float pop = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, p.vida * 6f)) * (1f + 0.3f * Mathf.Exp(-p.vida * 8f) * Mathf.Sin(p.vida * 22f));
            p.sr.transform.localScale = Vector3.one * p.escala * pop;
            if (cam != null) p.sr.transform.rotation = cam.transform.rotation;
            float alfa = Mathf.Clamp01((p.total - p.vida) / 0.35f);
            p.sr.color = new Color(1f, 1f, 1f, alfa);
        }
    }
}
