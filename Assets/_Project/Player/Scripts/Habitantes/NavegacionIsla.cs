// HU-39: navegación de los habitantes por la isla.
// Va en la raíz "Habitantes_Isla" (la pone LookFrutiger.Habitantes). Al empezar:
//   - carga la malla de navegación horneada en el editor (NavMesh_Hub.asset), sin paquetes extra;
//   - hace que los caminos, plazas y baldosas (área 3) "cuesten" menos que el pasto (área 0), así los
//     habitantes prefieren ir por los caminos;
//   - le pone al jugador un NavMeshObstacle para que los habitantes lo esquiven.
// Los habitantes le piden destinos con DestinoAleatorio: un punto cerca de uno de los puntos de interés.
using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(-50)]
public class NavegacionIsla : MonoBehaviour
{
    public const int AreaCamino = 3;

    public NavMeshData datos;
    public float alturaSuelo;                       // altura de la superficie de la isla
    [Tooltip("Centros de los puntos de interés (lobby, juegos, piscina, jardín, mirador)")]
    public Vector3[] puntosInteres = new Vector3[0];
    public float[] radiosInteres = new float[0];
    public float costoPasto = 3f;                   // relativo al camino (1)

    public static NavegacionIsla Instancia { get; private set; }

    NavMeshDataInstance instancia;

    void Awake()
    {
        Instancia = this;
        if (datos != null) instancia = NavMesh.AddNavMeshData(datos);
        NavMesh.SetAreaCost(0, costoPasto);
        NavMesh.SetAreaCost(AreaCamino, 1f);
    }

    void Start()
    {
        var jugador = FindAnyObjectByType<ControladorJugador>();
        if (jugador != null && jugador.GetComponent<NavMeshObstacle>() == null)
        {
            var o = jugador.gameObject.AddComponent<NavMeshObstacle>();
            o.shape = NavMeshObstacleShape.Capsule;
            o.radius = 0.4f; o.height = 1.6f; o.center = new Vector3(0f, 0.8f, 0f);
            o.carving = false;                      // solo para que lo esquiven, sin rehacer la malla
        }
    }

    void OnDestroy()
    {
        if (instancia.valid) instancia.Remove();
        if (Instancia == this) Instancia = null;
    }

    /// <summary>Un punto caminable cerca de un punto de interés (a veces, cerca de donde ya está).</summary>
    public bool DestinoAleatorio(Vector3 desde, out Vector3 destino)
    {
        for (int intento = 0; intento < 12; intento++)
        {
            Vector3 centro; float radio;
            if (puntosInteres.Length == 0 || Random.value < 0.25f) { centro = desde; radio = 14f; }   // paseo corto
            else
            {
                int i = Random.Range(0, puntosInteres.Length);
                centro = puntosInteres[i];
                radio = i < radiosInteres.Length ? radiosInteres[i] : 15f;
            }
            var c = Random.insideUnitCircle * radio;
            var p = new Vector3(centro.x + c.x, alturaSuelo, centro.z + c.y);
            if (!NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas)) continue;
            if (Mathf.Abs(hit.position.y - alturaSuelo) > 0.6f) continue;       // ni techos ni el fondo de la piscina
            if ((hit.position - desde).sqrMagnitude < 4f * 4f) continue;        // que valga la pena caminar
            destino = hit.position;
            return true;
        }
        destino = desde;
        return false;
    }
}
