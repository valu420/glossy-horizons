// HU-02: hace girar lento al personaje de la vista previa.
// También se puede girar arrastrando con el mouse.
using UnityEngine;
using UnityEngine.InputSystem;

public class GirarVistaPrevia : MonoBehaviour
{
    public float gradosPorSegundo = 30f;
    public float sensibilidadArrastre = 0.4f;
    public float flotacion = 0.03f;   // sube y baja suave

    float tiempo;
    float alturaBase;

    void Start() { alturaBase = transform.localPosition.y; }

    void Update()
    {
        float giro = gradosPorSegundo * Time.deltaTime;
        var mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.isPressed)
            giro = -mouse.delta.ReadValue().x * sensibilidadArrastre;   // arrastrar con clic derecho
        transform.Rotate(0f, giro, 0f, Space.World);

        tiempo += Time.deltaTime;
        var p = transform.localPosition;
        p.y = alturaBase + Mathf.Sin(tiempo * 2f) * flotacion;
        transform.localPosition = p;
    }
}
