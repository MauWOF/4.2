using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System.Collections.Generic;

// Solo la partida actual vive en memoria. Record y desbloqueos usan PlayerPrefs.
public static class DatosPartida
{
    public const string ClaveRecord = "HelixJump.Record";
    public const string ClaveNivel = "HelixJump.NivelDesbloqueado";
    public static int nivel = 1;
    public static int puntosAcumulados;
    public static int Record => Mathf.Max(0, PlayerPrefs.GetInt(ClaveRecord, 0));
    public static int Desbloqueado => Mathf.Clamp(PlayerPrefs.GetInt(ClaveNivel, 1), 1, 3);
    public static void Nueva(int elegido)
    {
        nivel = Mathf.Clamp(elegido, 1, Desbloqueado);
        puntosAcumulados = 0;
    }
    public static void GuardarRecord(int puntos)
    {
        if (puntos <= Record) return;
        PlayerPrefs.SetInt(ClaveRecord, puntos);
        PlayerPrefs.Save();
    }
    public static void Desbloquear(int siguiente)
    {
        PlayerPrefs.SetInt(ClaveNivel, Mathf.Max(Desbloqueado, Mathf.Clamp(siguiente, 1, 3)));
        PlayerPrefs.Save();
    }
}

public class Partida : MonoBehaviour
{
    public Rigidbody pelota;
    public GameObject panelPausa, panelResultado;
    public Text textoPuntos, textoResultado, textoAyuda;
    public Slider progreso;
    public AudioClip rebote, victoria, derrota;
    public float separacion = 3.2f;
    public int pisos = 12;
    public bool Jugando { get; private set; }
    public int Nivel { get; private set; }
    public int Puntos => DatosPartida.puntosAcumulados + superados * 10;
    public int Superados => superados;
    AudioSource sonido;
    Text textoRecord;
    Button botonResultado;
    int superados;
    bool terminada, ganada;

    void Start()
    {
        Time.timeScale = 1;
        Application.targetFrameRate = 60;
        Nivel = Mathf.Clamp(DatosPartida.nivel, 1, DatosPartida.Desbloqueado);
        sonido = GetComponent<AudioSource>();
        ConfigurarTorre();
        ConfigurarInterfaz();
        Jugando = true;
        panelPausa.SetActive(false);
        panelResultado.SetActive(false);
        ActualizarInterfaz();
    }
    void ConfigurarInterfaz()
    {
        Transform area = textoPuntos.transform.parent;
        foreach (var texto in area.GetComponentsInChildren<Text>(true))
            if (texto.text.StartsWith("NIVEL")) texto.text = "NIVEL " + Nivel.ToString("00");
        textoPuntos.fontSize = 40;
        textoPuntos.rectTransform.sizeDelta = new Vector2(400, 80);
        textoRecord = Instantiate(textoPuntos, area);
        textoRecord.name = "Record";
        textoRecord.fontSize = 22;
        textoRecord.rectTransform.anchorMin = textoRecord.rectTransform.anchorMax = new Vector2(0.5f, 0.79f);
        textoRecord.rectTransform.anchoredPosition = Vector2.zero;
        textoRecord.rectTransform.sizeDelta = new Vector2(420, 45);
        progreso.fillRect.anchorMin = Vector2.zero;
        progreso.fillRect.anchorMax = Vector2.one;
        progreso.fillRect.offsetMin = progreso.fillRect.offsetMax = Vector2.zero;
        progreso.value = 1;
        progreso.value = 0;
        botonResultado = panelResultado.GetComponentInChildren<Button>(true);
        // Sustituye el evento guardado en la escena para continuar o reintentar.
        botonResultado.onClick = new Button.ButtonClickedEvent();
        botonResultado.onClick.AddListener(ContinuarOReintentar);
        textoResultado.fontSize = 28;
    }
    void ConfigurarTorre()
    {
        var giro = FindAnyObjectByType<GirarTorre>();
        var sectores = giro.GetComponentsInChildren<Superficie>();
        var porPiso = new List<Superficie>[pisos];
        for (int i = 0; i < pisos; i++) porPiso[i] = new List<Superficie>();
        Material seguro = null, rojo = null;
        foreach (var sector in sectores)
        {
            if (sector.meta) continue;
            var material = sector.GetComponent<Renderer>().sharedMaterial;
            if (sector.peligrosa) rojo = material; else seguro = material;
            int piso = Mathf.RoundToInt(-sector.transform.localPosition.y / separacion);
            if (piso >= 0 && piso < pisos) porPiso[piso].Add(sector);
        }
        int anchoHueco = 4 - Nivel; // 90, 60 y 30 grados.
        for (int piso = 0; piso < pisos; piso++)
        {
            var piezas = porPiso[piso];
            if (piezas.Count == 0) continue;
            int necesarias = 12 - anchoHueco;
            while (piezas.Count < necesarias)
                piezas.Add(Instantiate(piezas[0], giro.transform));
            int hueco = (piso * 3) % 12;
            for (int i = 0; i < piezas.Count; i++)
            {
                var pieza = piezas[i];
                pieza.gameObject.SetActive(i < necesarias);
                if (i >= necesarias) continue;
                int distancia = anchoHueco + i;
                int sector = (hueco + distancia) % 12;
                pieza.transform.localPosition = Vector3.down * piso * separacion;
                pieza.transform.localRotation = Quaternion.Euler(0, sector * 30 + piso * 23, 0);
                // Al subir de nivel el rojo se acerca a ambos bordes del hueco.
                // Los dos primeros pisos siempre permiten aprender sin peligro.
                bool peligro = piso > 1 && (Nivel == 1 ? distancia == 6 || distancia == 7
                    : Nivel == 2 ? distancia == 3 || distancia == 4 || distancia == 10
                    : distancia == 1 || distancia == 2 || distancia == 10 || distancia == 11);
                pieza.peligrosa = peligro;
                pieza.name = peligro ? "Sector peligroso" : "Sector seguro";
                pieza.GetComponent<Renderer>().sharedMaterial = peligro ? rojo : seguro;
            }
        }
        Physics.SyncTransforms();
    }
    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && !terminada) Pausar();
        if (!Jugando) return;
        int alcanzados = Mathf.Clamp(Mathf.FloorToInt((-pelota.position.y - 0.45f) / separacion) + 1, 0, pisos);
        // Guarda el maximo: un rebote nunca resta puntos ni permite contar otro piso dos veces.
        if (alcanzados > superados)
        {
            superados = alcanzados;
            DatosPartida.GuardarRecord(Puntos);
            ActualizarInterfaz();
        }
        if (pelota.position.y < -pisos * separacion - 5) Terminar(false);
    }
    void ActualizarInterfaz()
    {
        textoPuntos.text = "PUNTOS " + Puntos.ToString("000");
        textoRecord.text = "RECORD " + DatosPartida.Record.ToString("000");
        progreso.value = (float)superados / pisos;
        textoAyuda.gameObject.SetActive(superados == 0);
    }
    public void SonarRebote() { if (rebote != null) sonido.PlayOneShot(rebote, 0.35f); }
    public void Pausar()
    {
        if (terminada) return;
        Jugando = !Jugando;
        panelPausa.SetActive(!Jugando);
        Time.timeScale = Jugando ? 1 : 0;
    }
    public void Terminar(bool victoriaDelNivel)
    {
        if (terminada) return;
        terminada = true;
        ganada = victoriaDelNivel;
        Jugando = false;
        pelota.linearVelocity = Vector3.zero;
        pelota.isKinematic = true;
        if (ganada)
        {
            superados = pisos;
            DatosPartida.Desbloquear(Nivel + 1);
        }
        DatosPartida.GuardarRecord(Puntos);
        ActualizarInterfaz();
        textoResultado.text = ganada ? (Nivel < 3 ? "NIVEL " + Nivel + " COMPLETADO" : "¡JUEGO COMPLETADO!") : "¡INTENTALO OTRA VEZ!";
        textoResultado.text += "\n" + Puntos + " PUNTOS";
        botonResultado.GetComponentInChildren<Text>().text = ganada && Nivel < 3 ? "SIGUIENTE NIVEL" : "VOLVER A JUGAR";
        panelResultado.SetActive(true);
        var clip = ganada ? victoria : derrota;
        if (clip != null) sonido.PlayOneShot(clip, 0.6f);
    }
    public void ContinuarOReintentar()
    {
        if (ganada && Nivel < 3)
        {
            DatosPartida.puntosAcumulados = Puntos;
            DatosPartida.nivel = Nivel + 1;
            Time.timeScale = 1;
            SceneManager.LoadScene("Juego");
        }
        else if (ganada) { DatosPartida.Nueva(1); CargarJuego(); }
        else Reiniciar();
    }
    void CargarJuego() { Time.timeScale = 1; SceneManager.LoadScene("Juego"); }
    public void Reiniciar() { DatosPartida.Nueva(Nivel); CargarJuego(); }
    public void Menu() { Time.timeScale = 1; DatosPartida.Nueva(1); SceneManager.LoadScene("Menu"); }
    void OnApplicationPause(bool pausa) { if (pausa && Jugando) Pausar(); }
    void OnDestroy() { Time.timeScale = 1; }
}
