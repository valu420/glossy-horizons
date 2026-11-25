using UnityEngine;

namespace GlossyHorizons.Core
{
    /// <summary>
    /// Delfín decorativo: nada en círculos bajo el agua y salta en arco cada cierto tiempo.
    /// Solo se mueve en Play; en el editor queda en la pose que dejó el constructor de la escena.
    /// </summary>
    public class DolphinSwim : MonoBehaviour
    {
        public Vector3 center;            // centro del círculo (solo XZ)
        public float radius = 12f;
        public float speed = 7f;          // m/s a lo largo del círculo
        public float seaY = -6.2f;        // nivel del mar
        public float jumpHeight = 3.2f;
        public float dive = 3f;           // profundidad del buceo entre saltos
        public float period = 7f;         // segundos entre saltos
        [Range(0.1f, 0.6f)] public float jumpFraction = 0.32f;
        public float phase;               // desfase (0..1) para que no salten a la vez
        public bool clockwise;

        Vector3 last;
        bool started;

        void Update()
        {
            if (!Application.isPlaying) return;
            float t = Time.time + phase * period;
            float ang = (clockwise ? -1f : 1f) * t * speed / Mathf.Max(0.1f, radius);
            float ph = Mathf.Repeat(t / period, 1f);
            float y = ph < jumpFraction
                ? Mathf.Sin(ph / jumpFraction * Mathf.PI) * jumpHeight
                : -Mathf.Sin((ph - jumpFraction) / (1f - jumpFraction) * Mathf.PI) * dive;
            var p = new Vector3(center.x + Mathf.Cos(ang) * radius, seaY + y, center.z + Mathf.Sin(ang) * radius);
            if (!started) { transform.position = p; last = p; started = true; return; }
            var d = p - last;
            if (d.sqrMagnitude > 1e-6f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d.normalized), 8f * Time.deltaTime);
            transform.position = p;
            last = p;
        }
    }
}
