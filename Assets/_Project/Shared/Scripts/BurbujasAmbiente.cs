// Burbujas de vidrio que flotan por el escenario (toque Frutiger Aero / Aqua).
// Suben despacio meciéndose, crecen al aparecer y desaparecen arriba; luego vuelven a empezar.
// Se crean al jugar (no se guardan en la escena).
using UnityEngine;

public class BurbujasAmbiente : MonoBehaviour
{
    public Material material;                  // vidrio con borde brillante
    public int cantidad = 45;
    public float radio = 70f;                  // alrededor de este objeto
    public float radioLibre = 6f;              // no aparecen pegadas al centro
    public Vector2 tamano = new Vector2(0.35f, 1.4f);
    public Vector2 alturas = new Vector2(0.5f, 14f);
    public float velocidad = 0.6f;

    Transform[] burbujas; Vector3[] base_; float[] fase, tam;

    void Start()
    {
        if (material == null) return;
        burbujas = new Transform[cantidad]; base_ = new Vector3[cantidad]; fase = new float[cantidad]; tam = new float[cantidad];
        var malla = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
        for (int i = 0; i < cantidad; i++)
        {
            var go = new GameObject("Burbuja");
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = malla;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            burbujas[i] = go.transform;
            Reiniciar(i, Random.Range(0f, 1f));
        }
    }

    void Reiniciar(int i, float progreso)
    {
        float ang = Random.Range(0f, Mathf.PI * 2f), r = Mathf.Lerp(radioLibre, radio, Mathf.Sqrt(Random.value));
        base_[i] = transform.position + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
        fase[i] = progreso;
        tam[i] = Random.Range(tamano.x, tamano.y);
    }

    void Update()
    {
        if (burbujas == null) return;
        float alto = alturas.y - alturas.x;
        for (int i = 0; i < burbujas.Length; i++)
        {
            fase[i] += Time.deltaTime * velocidad / alto * (1.4f - tam[i] * 0.4f);
            if (fase[i] >= 1f) Reiniciar(i, 0f);
            float f = fase[i];
            float vaiven = Mathf.Sin(Time.time * 0.8f + i * 1.7f) * 0.6f;
            burbujas[i].position = base_[i] + new Vector3(vaiven, alturas.x + f * alto, Mathf.Cos(Time.time * 0.6f + i) * 0.4f);
            float aparece = Mathf.SmoothStep(0f, 1f, Mathf.Min(f * 8f, (1f - f) * 6f));
            float tiembla = 1f + 0.04f * Mathf.Sin(Time.time * 5f + i);
            burbujas[i].localScale = new Vector3(tiembla, 2f - tiembla, tiembla) * tam[i] * aparece;
        }
    }
}
