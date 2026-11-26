// Marca dónde aparece el jugador al entrar a una escena (una por escena de zona).
using UnityEngine;

namespace GlossyHorizons.Core
{
    public class PuntoAparicion : MonoBehaviour
    {
        void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.5f);
            Gizmos.DrawRay(transform.position + Vector3.up, transform.forward * 1.5f);
        }
    }
}
