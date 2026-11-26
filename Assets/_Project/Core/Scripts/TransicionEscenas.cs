// Cambio de escena con fundido a negro. Carga la escena destino en modo Single, así el Hub (o la zona
// anterior) se descarga por completo y no gasta memoria ni rendimiento mientras estás en otra zona.
// Después coloca al jugador: junto al portal por el que se salió (al volver al Hub) o en el
// PuntoAparicion de la escena (al entrar a una zona).
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GlossyHorizons.Core
{
    public class TransicionEscenas : MonoBehaviour
    {
        public const string EscenaHub = "Hub";

        static TransicionEscenas instancia;
        /// <summary>Id del portal que se cruzó por última vez (para reaparecer frente a él al volver al Hub).</summary>
        public static string PortalOrigen { get; private set; }

        float alfa;
        bool ocupado;
        Texture2D negro;
        string mensaje = "Cargando siguiente mundo";
        float progreso;
        GUIStyle estiloTexto;

        public static bool Ocupado => instancia != null && instancia.ocupado;

        public static void Ir(string escena, string portalOrigen = null)
        {
            if (string.IsNullOrEmpty(escena)) return;
            if (!Application.CanStreamedLevelBeLoaded(escena))
            {
                Debug.LogWarning($"[Portales] La escena '{escena}' no está en Build Settings.");
                return;
            }
            if (instancia == null)
            {
                var go = new GameObject("TransicionEscenas");
                DontDestroyOnLoad(go);
                instancia = go.AddComponent<TransicionEscenas>();
            }
            if (instancia.ocupado) return;
            PortalOrigen = portalOrigen;
            instancia.mensaje = escena == EscenaHub ? "Volviendo al lobby" : "Cargando siguiente mundo";
            instancia.StartCoroutine(instancia.Rutina(escena));
        }

        void Awake()
        {
            negro = new Texture2D(1, 1);
            negro.SetPixel(0, 0, Color.black);
            negro.Apply();
        }

        IEnumerator Rutina(string escena)
        {
            ocupado = true;
            progreso = 0f;
            yield return Fundido(1f, 0.45f);
            float inicio = Time.unscaledTime;

            var op = SceneManager.LoadSceneAsync(escena, LoadSceneMode.Single);   // descarga la escena actual
            op.allowSceneActivation = false;
            // la pantalla de carga se ve al menos un momento, aunque la escena cargue rápido
            while (op.progress < 0.9f || Time.unscaledTime - inicio < 1.2f)
            {
                progreso = Mathf.Clamp01(op.progress / 0.9f);
                yield return null;
            }
            progreso = 1f;
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;
            yield return Resources.UnloadUnusedAssets();
            yield return null;   // deja correr los Start() de la escena nueva

            ColocarJugador(escena);
            yield return null;
            yield return Fundido(0f, 0.55f);
            ocupado = false;
        }

        IEnumerator Fundido(float destino, float duracion)
        {
            float inicio = alfa, t = 0f;
            while (t < duracion)
            {
                t += Time.unscaledDeltaTime;
                alfa = Mathf.Lerp(inicio, destino, Mathf.Clamp01(t / duracion));
                yield return null;
            }
            alfa = destino;
        }

        void OnGUI()
        {
            if (alfa <= 0.001f) return;
            GUI.depth = -1000;
            var c = GUI.color; GUI.color = new Color(1, 1, 1, alfa);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), negro);

            if (estiloTexto == null)
                estiloTexto = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            estiloTexto.fontSize = Mathf.Max(18, Screen.height / 20);
            estiloTexto.normal.textColor = Color.white;
            int puntos = (int)(Time.unscaledTime * 2.5f) % 4;
            GUI.Label(new Rect(0, Screen.height * 0.42f, Screen.width, Screen.height * 0.12f), mensaje + new string('.', puntos), estiloTexto);

            // barra de progreso
            float ancho = Screen.width * 0.28f, alto = Mathf.Max(4f, Screen.height * 0.008f);
            float x = (Screen.width - ancho) * 0.5f, y = Screen.height * 0.58f;
            GUI.color = new Color(1, 1, 1, alfa * 0.2f);
            GUI.DrawTexture(new Rect(x, y, ancho, alto), Texture2D.whiteTexture);
            GUI.color = new Color(0.45f, 0.9f, 1f, alfa);
            GUI.DrawTexture(new Rect(x, y, ancho * progreso, alto), Texture2D.whiteTexture);
            GUI.color = c;
        }

        static void ColocarJugador(string escena)
        {
            var jugador = FindFirstObjectByType<ControladorJugador>();
            if (jugador == null) return;

            Vector3 pos = jugador.transform.position;
            Quaternion rot = jugador.transform.rotation;
            bool hay = false;

            if (escena == EscenaHub && !string.IsNullOrEmpty(PortalOrigen))
            {
                foreach (var p in FindObjectsByType<PortalZona>(FindObjectsSortMode.None))
                {
                    if (p.idPortal != PortalOrigen) continue;
                    var c = p.transform.position;
                    var haciaCentro = new Vector3(-c.x, 0f, -c.z);   // el lobby está centrado en el origen
                    if (haciaCentro.sqrMagnitude < 0.01f) haciaCentro = Vector3.back;
                    haciaCentro.Normalize();
                    pos = c + haciaCentro * 3.5f;
                    rot = Quaternion.LookRotation(haciaCentro, Vector3.up);
                    pos.y = SueloEn(pos, c.y);
                    hay = true;
                    break;
                }
            }
            if (!hay)
            {
                var punto = FindFirstObjectByType<PuntoAparicion>();
                if (punto != null) { pos = punto.transform.position; rot = punto.transform.rotation; hay = true; }
            }
            if (!hay) return;

            var cc = jugador.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            jugador.transform.SetPositionAndRotation(pos, rot);
            if (cc != null) cc.enabled = true;
        }

        static float SueloEn(Vector3 pos, float yPortal)
        {
            if (Physics.Raycast(new Vector3(pos.x, yPortal + 3f, pos.z), Vector3.down, out var hit, 10f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point.y + 0.05f;
            return yPortal + 0.05f;
        }
    }
}
