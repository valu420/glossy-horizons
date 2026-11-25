// Lo que AnimacionGel necesita saber del movimiento de un personaje de gel.
// Lo implementan ControladorJugador (el jugador, con teclado o mando) y HabitanteGel (los NPC de la isla, HU-38),
// así los dos se animan con exactamente la misma animación procedural (HU-05).
using UnityEngine;

public interface IMovimientoGel
{
    bool Activo { get; }                    // false = está en pausa (paneles, vista cercana...): se anima en reposo
    Vector3 VelocidadHorizontal { get; }
    bool EnSuelo { get; }
    bool Corriendo { get; }
    bool SaltoEsteFrame { get; }
    float VelocidadVertical { get; }        // > 0 subiendo, < 0 cayendo
    bool ModoCharco { get; set; }           // emote Gelatina: derretido, lento y sin saltar
    float VelocidadCharco { get; }
}
