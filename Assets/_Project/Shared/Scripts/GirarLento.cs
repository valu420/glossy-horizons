// Hace girar este objeto muy despacio alrededor del eje vertical (por ejemplo, las nubes del cielo).
using UnityEngine;

public class GirarLento : MonoBehaviour
{
    public float gradosPorSegundo = 0.4f;
    void Update() => transform.Rotate(0f, gradosPorSegundo * Time.deltaTime, 0f, Space.World);
}
