// HU-06: emote Gelatina. El personaje se derrite en una gota de gel brillante que SE QUEDA:
//   - se aplasta hasta el piso y en su lugar aparece una gota redondeada del mismo color,
//     lisa y brillante (estilo Frutiger Aero), con los dos ojitos acostados encima
//     (misma forma, tamaño y distancia entre ellos que en la cara)
//   - tiembla como gelatina (resorte) al derretirse, al arrancar y al frenar
//   - de vez en cuando suben burbujitas de vidrio desde la gota
//   - se puede mover despacio (sin correr ni saltar): la gota se alarga hacia adelante,
//     se inclina un poco y le recorre una ola suave
//   - con la tecla G vuelve a la normalidad con un rebote de gelatina
// La gota es una malla que se recalcula cada frame (64 x 14 vértices, muy liviana).
using UnityEngine;

public partial class AnimacionGel
{
    [Header("Charco (emote Gelatina)")]
    public float radioCharco = 0.5f;
    public float altoCharco = 0.26f;
    public Texture2D texturaBurbuja;           // burbujitas que suben de la gota

    public bool EnCharco => enCharco;
    bool ModeloOculto => modeloOculto;

    bool enCharco, modeloOculto;
    float progresoCharco;              // 0 = normal, 1 = charco
    float abrirCharco = 1f;            // parpadeo (lo calcula AnimarOjos)
    float faseOla, movAnterior, siguienteBurbuja = 1f;
    float bambole, velBambole;         // resorte de gelatina (arriba / abajo)
    float bamboleLado, velBamboleLado, giroAnterior;
    Renderer[] piezasModelo = new Renderer[0];
    GameObject charco;
    Mesh mallaCharco;
    MeshRenderer rendCharco;
    Vector3[] vertsCharco;
    Ojo[] ojosCharco = new Ojo[0];
    Sprite spBurbuja;

    const int SegCharco = 64, AnillosCharco = 14;

    public void EntrarCharco()
    {
        if (enCharco) return;
        enCharco = true;
        emote = -1;
        quieto = 0f;
        Rebotar(-0.15f);
    }

    public void SalirDeCharco()
    {
        enCharco = false;
    }

    // El cuerpo se aplasta mientras se derrite (y se estira al volver)
    Vector3 AplastarCharco()
    {
        float m = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progresoCharco / 0.7f));
        return new Vector3(1f + 0.7f * m, 1f - 0.8f * m, 1f + 0.7f * m);
    }

    void CrearCharco()
    {
        if (modelo != null) piezasModelo = modelo.GetComponentsInChildren<Renderer>(true);
        var texBurbuja = texturaBurbuja != null ? texturaBurbuja : BurbujaDeRespaldo();
        spBurbuja = Sprite.Create(texBurbuja, new Rect(0, 0, texBurbuja.width, texBurbuja.height),
                                  new Vector2(0.5f, 0.5f), texBurbuja.height / tamanoParticula);

        charco = new GameObject("Charco");
        charco.transform.SetParent(transform, false);
        rendCharco = charco.AddComponent<MeshRenderer>();
        mallaCharco = new Mesh { name = "Charco" };
        mallaCharco.MarkDynamic();
        vertsCharco = new Vector3[1 + SegCharco * AnillosCharco];
        mallaCharco.vertices = vertsCharco;

        // Triángulos (en sentido horario visto desde arriba, para que miren hacia arriba)
        var tri = new System.Collections.Generic.List<int>();
        for (int i = 0; i < SegCharco; i++)
        {
            int sig = (i + 1) % SegCharco;
            tri.Add(0); tri.Add(IdCharco(1, sig)); tri.Add(IdCharco(1, i));
            for (int j = 1; j < AnillosCharco; j++)
            {
                int a = IdCharco(j, i), b = IdCharco(j, sig), c = IdCharco(j + 1, i), d = IdCharco(j + 1, sig);
                tri.Add(a); tri.Add(d); tri.Add(c);
                tri.Add(a); tri.Add(b); tri.Add(d);
            }
        }
        mallaCharco.SetTriangles(tri, 0);
        charco.AddComponent<MeshFilter>().sharedMesh = mallaCharco;

        // Ojitos del charco: copias de los ojos, ACOSTADAS sobre la gota y alargadas hacia adelante "||"
        var lista = new System.Collections.Generic.List<Ojo>();
        foreach (var o in ojos)
        {
            var copia = Instantiate(o.t.gameObject);
            copia.name = o.t.name + "_Charco";
            copia.transform.SetParent(charco.transform, true);       // conserva el tamaño
            // Ejes del ojo (en su espacio local): "alto" (apunta arriba) y "frente" (apunta hacia afuera de la cara).
            // Se acuesta el ojo: su frente mira hacia arriba y su alto queda a lo largo de la gota (hacia adelante),
            // así desde arriba se ven como "||" acostados sobre la superficie.
            Vector3 ejeAlto = Vector3.zero; ejeAlto[o.ejeVertical] = 1f;
            if (Vector3.Dot(o.t.TransformDirection(ejeAlto), transform.up) < 0f) ejeAlto = -ejeAlto;
            Vector3 ejeFrente = Vector3.forward; float mejor = -1f;
            for (int k = 0; k < 3; k++)
            {
                if (k == o.ejeVertical) continue;
                Vector3 eje = Vector3.zero; eje[k] = 1f;
                float d = Vector3.Dot(o.t.TransformDirection(eje), transform.forward);
                if (Mathf.Abs(d) > mejor) { mejor = Mathf.Abs(d); ejeFrente = d >= 0f ? eje : -eje; }
            }
            Quaternion ejesOjo = Quaternion.LookRotation(ejeFrente, ejeAlto);          // local: Z = frente, Y = alto
            Quaternion acostado = Quaternion.LookRotation(Vector3.up, Vector3.forward); // Z -> arriba, Y -> adelante
            copia.transform.localRotation = acostado * Quaternion.Inverse(ejesOjo);
            var escala = copia.transform.localScale;                  // misma forma y tamaño que en la cara
            lista.Add(new Ojo
            {
                t = copia.transform,
                escala = escala,
                posLocal = new Vector3(transform.InverseTransformPoint(o.t.position).x, 0f, 0f),   // misma distancia entre ojos
                ejeVertical = o.ejeVertical
            });
        }
        ojosCharco = lista.ToArray();
        charco.SetActive(false);
    }

    int IdCharco(int anillo, int seg) => 1 + (anillo - 1) * SegCharco + seg;

    // Si el prefab no tiene la textura de la burbuja, se dibuja una sencilla por código
    static Texture2D BurbujaDeRespaldo()
    {
        const int N = 64;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x - N / 2f + 0.5f) / (N / 2f), dy = (y - N / 2f + 0.5f) / (N / 2f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float aro = Mathf.Exp(-Mathf.Pow((d - 0.88f) / 0.07f, 2f)) * 0.9f;
                float brillo = Mathf.Exp(-((dx + 0.35f) * (dx + 0.35f) + (dy - 0.4f) * (dy - 0.4f)) * 40f);
                float a = d < 1f ? Mathf.Clamp01(aro + brillo + 0.08f) : 0f;
                tex.SetPixel(x, y, new Color(0.85f, 0.97f, 1f, a));
            }
        tex.Apply();
        return tex;
    }

    // Alto de la gota en un punto (u = 0 centro, 1 borde): cúpula lisa y redondeada
    float AltoGota(float u, float alto) => alto * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(u, 2.4f)), 0.5f);

    void ActualizarCharco(float dt, float velocidad)
    {
        if (charco == null) return;
        float antes = progresoCharco;
        progresoCharco = Mathf.MoveTowards(progresoCharco, enCharco ? 1f : 0f, dt / (enCharco ? 0.9f : 0.6f));
        if (antes < 0.7f && progresoCharco >= 0.7f) velBambole -= 1.6f;    // al terminar de derretirse, tiembla

        // El cuerpo normal se esconde cuando ya está casi aplastado
        bool ocultar = progresoCharco > 0.7f;
        if (ocultar != modeloOculto)
        {
            modeloOculto = ocultar;
            foreach (var r in piezasModelo)
                if (r != null && !r.name.Contains("Anillo")) r.enabled = !ocultar;
            if (!ocultar) Rebotar(-0.3f);                      // al volver, rebote de gelatina
        }
        ctrl.ModoCharco = enCharco || progresoCharco > 0.3f;    // lento, sin correr ni saltar

        // Tamaño de la gota: crece con un pequeño rebote al derretirse
        float s = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, progresoCharco));
        s *= 1f + 0.12f * Mathf.Sin(s * Mathf.PI);
        bool ver = s > 0.001f;
        if (charco.activeSelf != ver) charco.SetActive(ver);
        if (!ver) return;

        var rCuerpo = cuerpo != null ? cuerpo.t.GetComponent<Renderer>() : null;
        if (rCuerpo != null) rendCharco.sharedMaterial = rCuerpo.sharedMaterial;   // mismo gel (y mismo color)

        // Resorte de gelatina: se excita al arrancar y al frenar
        float mov = Mathf.Clamp01(velocidad / Mathf.Max(0.01f, ctrl.velocidadCharco));
        velBambole += (movAnterior - mov) * 3f;
        movAnterior = mov;
        velBambole += (-140f * bambole - 7f * velBambole) * dt;
        bambole += velBambole * dt;
        // segundo resorte, de lado a lado (al girar)
        float giroY = transform.eulerAngles.y;
        velBamboleLado += Mathf.DeltaAngle(giroAnterior, giroY) * 0.01f;
        giroAnterior = giroY;
        velBamboleLado += (-90f * bamboleLado - 5f * velBamboleLado) * dt;
        bamboleLado += velBamboleLado * dt;
        float sy = 1f + bambole + 0.04f * Mathf.Sin(Time.time * 3.1f), sxz = 1f - bambole * 0.5f;

        float t = Time.time;
        faseOla += dt * (1.5f + 5f * mov);
        float respira = 1f + 0.035f * Mathf.Sin(t * 2.2f);
        float alto = altoCharco * s * respira * sy;

        // Bamboleo: la cima se mece en círculos suaves y, al avanzar, se queda un poco atrás (inercia)
        float meceX = radioCharco * s * (0.07f * Mathf.Sin(t * 2.6f) + 0.5f * bamboleLado);
        float meceZ = radioCharco * s * (0.05f * Mathf.Sin(t * 3.3f + 1f) - 0.16f * mov - 0.6f * bambole);
        vertsCharco[0] = new Vector3(meceX, alto, meceZ);
        for (int i = 0; i < SegCharco; i++)
        {
            float a = i / (float)SegCharco * Mathf.PI * 2f;
            float cx = Mathf.Cos(a), cz = Mathf.Sin(a);
            // borde casi redondo que ondula suave (gota de gel, no estrella)
            float forma = 1f + 0.035f * Mathf.Sin(2f * a + t * 0.9f) + 0.025f * Mathf.Sin(3f * a - t * 1.3f);
            float rx = radioCharco * s * sxz * forma * (1f - 0.1f * mov);
            float rz = radioCharco * s * sxz * forma * (1f + 0.22f * mov + 0.08f * mov * cz);   // se alarga hacia adelante
            for (int j = 1; j <= AnillosCharco; j++)
            {
                float u = j / (float)AnillosCharco;
                float x = cx * rx * u, z = cz * rz * u;
                float inclina = 1f + 0.15f * mov * cz * u;                      // el frente más alto al avanzar
                float ola = 1f + 0.03f * Mathf.Sin(u * 8f - t * 3f) + 0.06f * mov * Mathf.Sin(faseOla - z * 8f);
                float h = AltoGota(u, alto) * inclina * ola + 0.004f;
                float k = alto > 0.0001f ? h / alto : 0f;                         // cuánto se mece (más arriba, más)
                vertsCharco[IdCharco(j, i)] = new Vector3(x + meceX * k, h, z + meceZ * k);
            }
        }
        mallaCharco.vertices = vertsCharco;
        mallaCharco.RecalculateNormals();
        mallaCharco.RecalculateBounds();

        // Ojitos acostados arriba, un poco hacia el frente, con parpadeo
        float zOjos = radioCharco * s * (1f + 0.22f * mov) * 0.2f;
        float yOjos = AltoGota(0.22f, alto) * (1f + 0.15f * mov * 0.22f) + 0.003f;
        foreach (var o in ojosCharco)
        {
            float kOjos = alto > 0.0001f ? yOjos / alto : 0f;                      // los ojos se mecen con la cima
            o.t.localPosition = new Vector3(o.posLocal.x * Mathf.Min(s, 1f) + meceX * kOjos, yOjos, zOjos + meceZ * kOjos);
            Vector3 e = o.escala * Mathf.Clamp01(s * 1.2f);                        // crecen al formarse la gota
            e[o.ejeVertical] *= abrirCharco;
            o.t.localScale = e;
        }

        // Burbujitas de vidrio que suben de la gota (toque Aqua)
        siguienteBurbuja -= dt;
        if (enCharco && progresoCharco >= 1f && siguienteBurbuja <= 0f && spBurbuja != null)
        {
            float ang = Random.value * Mathf.PI * 2f, rad = Random.value * 0.6f;
            Vector3 local = new Vector3(Mathf.Cos(ang) * radioCharco * rad, AltoGota(rad, alto), Mathf.Sin(ang) * radioCharco * rad);
            Emitir(spBurbuja, transform.TransformPoint(local), new Vector3(Random.Range(-0.04f, 0.04f), Random.Range(0.25f, 0.4f), 0f),
                   Random.Range(1.4f, 2f), Random.Range(0.3f, 0.6f), 0f);
            siguienteBurbuja = Random.Range(0.3f, 0.7f);
        }
    }
}
