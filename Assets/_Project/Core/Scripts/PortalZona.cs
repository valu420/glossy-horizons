// Portal entre escenas. Va en el panel del portal (con un Collider en modo Trigger).
// Cuando el jugador lo atraviesa carga la escena destino con TransicionEscenas.
using UnityEngine;

namespace GlossyHorizons.Core
{
    [DisallowMultipleComponent]
    public class PortalZona : MonoBehaviour
    {
        [Tooltip("Nombre de la escena (debe estar en Build Settings). Ej: Zone_DarkAero, Hub")]
        public string escenaDestino;
        [Tooltip("Identificador del portal. Al volver al Hub, el jugador aparece frente al portal con este mismo id.")]
        public string idPortal;
        [Tooltip("Bloqueado: no hace nada al cruzarlo (por ejemplo la zona Evil hasta desbloquearla).")]
        public bool bloqueado;

        void OnTriggerEnter(Collider otro)
        {
            if (bloqueado || TransicionEscenas.Ocupado) return;
            if (otro.GetComponentInParent<ControladorJugador>() == null) return;
            TransicionEscenas.Ir(escenaDestino, idPortal);
        }
    }
}
