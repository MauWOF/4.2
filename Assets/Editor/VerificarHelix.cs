using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

// Comprobaciones manuales desde Helix. Solo escriben en la consola de Unity.
[InitializeOnLoad]
public static class VerificarHelix
{
    static int fase, comprobaciones;
    static double inicio;
    static bool reboto;
    static VerificarHelix()
    {
        EditorApplication.update += Actualizar;
        EditorApplication.playModeStateChanged += estado => {
            if (estado != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool("HelixPruebaActiva", false)) return;
            RestaurarPreferencias();
            SessionState.SetBool("HelixPruebaActiva", false);
            DatosPartida.Nueva(1);
            EditorSceneManager.OpenScene("Assets/Scenes/Menu.unity");
        };
    }
    [MenuItem("Helix/Verificar funcionamiento")]
    public static void Preparar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool("HelixPruebaActiva", true);
        foreach (string clave in new[] { DatosPartida.ClaveRecord, DatosPartida.ClaveNivel })
        {
            SessionState.SetBool("PruebaExiste." + clave, PlayerPrefs.HasKey(clave));
            SessionState.SetInt("PruebaValor." + clave, PlayerPrefs.GetInt(clave));
        }
        PlayerPrefs.SetInt(DatosPartida.ClaveRecord, 0);
        PlayerPrefs.SetInt(DatosPartida.ClaveNivel, 1);
        DatosPartida.Nueva(1);
        EditorSceneManager.OpenScene("Assets/Scenes/Juego.unity");
        EditorApplication.isPlaying = true;
    }
    static void RestaurarPreferencias()
    {
        foreach (string clave in new[] { DatosPartida.ClaveRecord, DatosPartida.ClaveNivel })
        {
            if (SessionState.GetBool("PruebaExiste." + clave, false)) PlayerPrefs.SetInt(clave, SessionState.GetInt("PruebaValor." + clave, 0));
            else PlayerPrefs.DeleteKey(clave);
        }
        PlayerPrefs.Save();
    }
    static void Comprobar(bool correcto, string detalle)
    {
        if (!correcto) throw new System.Exception("Verificacion Helix: " + detalle);
        comprobaciones++;
        Debug.Log("Helix OK: " + detalle);
    }
    static void Esperar(int siguiente) { fase = siguiente; inicio = EditorApplication.timeSinceStartup; }
    static bool Paso(double segundos) => EditorApplication.timeSinceStartup - inicio > segundos;
    static void CaerEnMeta(Partida p)
    {
        p.pelota.isKinematic = false;
        p.pelota.position = new Vector3(0.75f, -36.9f, -2.3f);
        p.pelota.linearVelocity = Vector3.down * 2;
    }
    static void RevisarTorre(Partida p)
    {
        int piezas = 0;
        foreach (var s in Object.FindAnyObjectByType<GirarTorre>().GetComponentsInChildren<Superficie>())
            if (!s.meta && Mathf.Abs(s.transform.localPosition.y) < 0.1f) piezas++;
        Comprobar(piezas == 8 + p.Nivel, "Nivel " + p.Nivel + ": " + piezas + " sectores y hueco correcto");
    }
    static void Actualizar()
    {
        if (!SessionState.GetBool("HelixPruebaActiva", false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try { EjecutarPaso(); }
        catch (System.Exception error) { Debug.LogException(error); EditorApplication.isPlaying = false; }
    }
    static void EjecutarPaso()
    {
        if (fase == 12)
        {
            if (SceneManager.GetActiveScene().name != "Menu" || !Paso(0.5)) return;
            Comprobar(DatosPartida.Record == 360 && PlayerPrefs.GetInt(DatosPartida.ClaveRecord) == 360, "Record persistente al cargar Menu");
            Comprobar(PlayerPrefs.GetInt(DatosPartida.ClaveNivel) == 3, "Desbloqueo persistente de los tres niveles");
            var menu = Object.FindAnyObjectByType<MenuPrincipal>();
            var ayuda = menu.instrucciones.transform.parent.Find("CÓMO JUGAR").GetComponent<Button>();
            var puntero = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, ayuda.transform.position) };
            var impactos = new List<RaycastResult>();
            EventSystem.current.RaycastAll(puntero, impactos);
            Comprobar(impactos.Count > 0 && impactos[0].gameObject.GetComponentInParent<Button>() == ayuda,
                "Ayuda recibe el toque; primer elemento: " + (impactos.Count > 0 ? impactos[0].gameObject.name : "ninguno"));
            ayuda.onClick.Invoke();
            Comprobar(menu.instrucciones.activeSelf, "El boton de ayuda abre las instrucciones");
            menu.instrucciones.GetComponentInChildren<Button>().onClick.Invoke();
            Comprobar(!menu.instrucciones.activeSelf, "Entendido cierra las instrucciones");
            Debug.Log("HELIX VERIFICACION COMPLETA: " + comprobaciones + " comprobaciones correctas. Se restauran los datos anteriores.");
            fase = 13; EditorApplication.isPlaying = false; return;
        }
        var p = Object.FindAnyObjectByType<Partida>();
        if (p == null || !p.Jugando && fase == 0) return;
        if (fase == 0) { fase = 1; inicio = EditorApplication.timeSinceStartup; RevisarTorre(p); }
        else if (fase == 1)
        {
            if (p.pelota.linearVelocity.y > 5) reboto = true;
            if (!Paso(3)) return;
            Comprobar(reboto, "Rebote fisico");
            Comprobar(p.progreso.value == 0 && p.progreso.fillRect.rect.width < 1, "Barra vacia al iniciar");
            p.Pausar(); Comprobar(!p.Jugando && Time.timeScale == 0, "Pausa"); p.Pausar();
            p.pelota.isKinematic = true; p.pelota.position = new Vector3(0.75f, -0.8f, -2.3f); Esperar(2);
        }
        else if (fase == 2 && Paso(0.3))
        {
            Comprobar(p.Puntos == 10 && p.textoPuntos.text.Contains("010"), "UI suma 10 puntos por piso");
            p.pelota.position = new Vector3(0.75f, 1, -2.3f); Esperar(3);
        }
        else if (fase == 3 && Paso(0.3))
        {
            Comprobar(p.Puntos == 10, "El rebote no resta puntos");
            p.pelota.position = new Vector3(0.75f, -0.8f, -2.3f); Esperar(4);
        }
        else if (fase == 4 && Paso(0.3))
        {
            Comprobar(p.Puntos == 10, "El mismo piso no suma dos veces");
            var rojo = System.Array.Find(Object.FindObjectsByType<Superficie>(), s => s.peligrosa);
            Vector3 posicion = rojo.transform.rotation * new Vector3(1.65f, 0, 1.65f);
            posicion.y = rojo.transform.position.y + 1;
            p.pelota.isKinematic = false; p.pelota.constraints = RigidbodyConstraints.FreezeRotation;
            p.pelota.position = posicion; p.pelota.linearVelocity = Vector3.down * 2; Esperar(5);
        }
        else if (fase == 5 && Paso(1.5))
        {
            Comprobar(!p.Jugando && p.panelResultado.activeSelf, "Colision roja causa derrota");
            p.panelResultado.GetComponentInChildren<Button>().onClick.Invoke(); Esperar(6);
        }
        else if (fase == 6 && Paso(0.8))
        {
            Comprobar(p.Nivel == 1 && p.Puntos == 0, "Reintentar reinicia la puntuacion");
            CaerEnMeta(p); Esperar(7);
        }
        else if (fase == 7 && Paso(1.5))
        {
            Comprobar(!p.Jugando && p.Puntos == 120 && DatosPartida.Desbloqueado == 2, "Meta del nivel 1: 120 puntos y nivel 2 desbloqueado");
            p.panelResultado.GetComponentInChildren<Button>().onClick.Invoke(); Esperar(8);
        }
        else if (fase == 8 && Paso(0.8))
        {
            Comprobar(p.Nivel == 2 && p.Puntos == 120, "Continuar conserva puntos en nivel 2"); RevisarTorre(p);
            CaerEnMeta(p); Esperar(9);
        }
        else if (fase == 9 && Paso(1.5))
        {
            Comprobar(!p.Jugando && p.Puntos == 240 && DatosPartida.Desbloqueado == 3, "Nivel 2 completado y nivel 3 desbloqueado");
            p.panelResultado.GetComponentInChildren<Button>().onClick.Invoke(); Esperar(10);
        }
        else if (fase == 10 && Paso(0.8))
        {
            Comprobar(p.Nivel == 3 && p.Puntos == 240, "Continuar conserva puntos en nivel 3"); RevisarTorre(p);
            CaerEnMeta(p); Esperar(11);
        }
        else if (fase == 11 && Paso(1.5))
        {
            Comprobar(!p.Jugando && p.Puntos == 360 && p.textoResultado.text.Contains("JUEGO COMPLETADO"), "Victoria final con 360 puntos");
            p.Menu(); Esperar(12);
        }
    }
}
