// HUD: cursor del mouse estilo Frutiger Aero.
// Va en el HUD. Usa una flecha de vidrio blanca y, al pasar sobre un botón
// (cualquier Button, Toggle, Slider...), cambia a la versión cian iluminada.
// Las texturas deben importarse como "Cursor" (lo hace la herramienta del menú).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class CursorFrutiger : MonoBehaviour
{
    public Texture2D flecha;
    public Texture2D activo;
    public Vector2 puntaDeLaFlecha = new Vector2(4f, 4f);   // píxel de la punta (hotspot)

    static readonly List<RaycastResult> resultados = new List<RaycastResult>();
    bool sobreBoton;
    bool iniciado;

    void OnEnable() { Poner(false); }

    void OnDisable()
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);   // vuelve al cursor de Windows
        iniciado = false;
    }

    void Update()
    {
        bool encima = HayBotonDebajo();
        if (!iniciado || encima != sobreBoton) Poner(encima);
    }

    void Poner(bool sobreUnBoton)
    {
        sobreBoton = sobreUnBoton;
        iniciado = true;
        var tex = sobreUnBoton && activo != null ? activo : flecha;
        if (tex != null) Cursor.SetCursor(tex, puntaDeLaFlecha, CursorMode.Auto);
    }

    static bool HayBotonDebajo()
    {
        var es = EventSystem.current;
        var mouse = Mouse.current;
        if (es == null || mouse == null || Cursor.lockState == CursorLockMode.Locked) return false;
        var datos = new PointerEventData(es) { position = mouse.position.ReadValue() };
        resultados.Clear();
        es.RaycastAll(datos, resultados);
        foreach (var r in resultados)
        {
            var s = r.gameObject.GetComponentInParent<Selectable>();
            if (s != null && s.IsInteractable()) return true;
        }
        return false;
    }
}
