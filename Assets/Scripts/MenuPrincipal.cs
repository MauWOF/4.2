using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MenuPrincipal : MonoBehaviour
{
    public GameObject instrucciones;
    void Start()
    {
        Transform area = instrucciones.transform.parent;
        var jugar = area.Find("JUGAR").GetComponent<Button>();
        for (int nivel = 1; nivel <= 3; nivel++)
        {
            int elegido = nivel;
            var boton = nivel == 1 ? jugar : Instantiate(jugar, area);
            boton.name = "Nivel " + nivel;
            var rect = (RectTransform)boton.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.33f - (nivel - 1) * 0.085f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(410, 65);
            bool disponible = nivel <= DatosPartida.Desbloqueado;
            var texto = boton.GetComponentInChildren<Text>();
            texto.rectTransform.sizeDelta = rect.sizeDelta;
            texto.fontSize = 22;
            texto.text = "NIVEL " + nivel + (disponible ? " · JUGAR" : " · BLOQUEADO");
            boton.interactable = disponible;
            boton.onClick = new Button.ButtonClickedEvent();
            boton.onClick.AddListener(() => ElegirNivel(elegido));
        }
        var ayuda = area.Find("CÓMO JUGAR").GetComponent<RectTransform>();
        ayuda.anchorMin = ayuda.anchorMax = new Vector2(0.5f, 0.075f);
        ayuda.anchoredPosition = Vector2.zero;
        ayuda.sizeDelta = new Vector2(410, 55);
        var botonAyuda = ayuda.GetComponent<Button>();
        botonAyuda.onClick = new Button.ButtonClickedEvent();
        botonAyuda.onClick.AddListener(Ayuda);
        var cerrarAyuda = instrucciones.GetComponentInChildren<Button>(true);
        cerrarAyuda.onClick = new Button.ButtonClickedEvent();
        cerrarAyuda.onClick.AddListener(Ayuda);
        foreach (var texto in area.GetComponentsInChildren<Text>(true))
        {
            if (texto.text.StartsWith("PROYECTO FINAL"))
            {
                texto.text = "PROYECTO FINAL · FASE 2";
                texto.fontSize = 14;
                texto.rectTransform.anchorMin = texto.rectTransform.anchorMax = new Vector2(0.5f, 0.02f);
                texto.rectTransform.anchoredPosition = Vector2.zero;
            }
            if (texto.text.StartsWith("Desliza el dedo"))
            {
                texto.fontSize = 22;
                texto.text = "Desliza para girar y busca los huecos.\nEvita el rojo y llega a la base.\n\nCada piso superado suma 10 puntos.\nCompleta la torre para desbloquear\nel siguiente nivel y seguir sumando.\n\nTu récord y niveles se guardan.\n\nPC: arrastra el ratón o usa las flechas.";
            }
        }
        var record = Instantiate(jugar.GetComponentInChildren<Text>(), area);
        record.name = "Record del menu";
        record.fontSize = 22;
        record.color = Color.white;
        record.text = "RÉCORD " + DatosPartida.Record.ToString("000");
        record.rectTransform.anchorMin = record.rectTransform.anchorMax = new Vector2(0.5f, 0.61f);
        record.rectTransform.anchoredPosition = Vector2.zero;
        record.rectTransform.sizeDelta = new Vector2(450, 45);
        // La ventana de instrucciones siempre queda delante de los botones nuevos.
        instrucciones.transform.SetAsLastSibling();
    }
    public void ElegirNivel(int nivel)
    {
        if (nivel < 1 || nivel > DatosPartida.Desbloqueado) return;
        DatosPartida.Nueva(nivel);
        Time.timeScale = 1;
        SceneManager.LoadScene("Juego");
    }
    public void Jugar() { ElegirNivel(1); }
    public void Ayuda() { instrucciones.SetActive(!instrucciones.activeSelf); }
}
