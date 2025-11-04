using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("Paneles de UI")]
    public GameObject panelInicio;
    public GameObject panelSimulacion;
    
    [Header("Referencias de Botones")]
    public Button botonPlay;
    public Button botonSalir;
    public Button botonPausa;
    public Button botonReiniciar;
    public Button botonAgregarAgente;
    public Button botonEliminarAgente;
    public Button botonMenuPrincipal;
    
    [Header("Controles de UI")]
    public Slider sliderVelocidad;
    public TMP_Dropdown dropdownComportamiento;
    public TMP_Text contadorAgentes;
    
    [Header("Referencias del Sistema")]
    public SimulationManager simulationManager;
    
    private bool simulacionPausada = false;
    
    void Start()
    {
        // Configurar estado inicial
        MostrarPantallaInicio();
        
        // Configurar listeners de botones
        botonPlay.onClick.AddListener(OnPlayClick);
        botonSalir.onClick.AddListener(OnSalirClick);
        botonPausa.onClick.AddListener(OnPausaClick);
        botonReiniciar.onClick.AddListener(OnReiniciarClick);
        botonAgregarAgente.onClick.AddListener(OnAgregarAgenteClick);
        botonEliminarAgente.onClick.AddListener(OnEliminarAgenteClick);
        botonMenuPrincipal.onClick.AddListener(OnMenuPrincipalClick);
        
        // Configurar otros controles
        sliderVelocidad.onValueChanged.AddListener(OnVelocidadCambiada);
        dropdownComportamiento.onValueChanged.AddListener(OnComportamientoCambiado);
    }
    
    void Update()
    {
        // Actualizar contador de agentes en tiempo real
        if (panelSimulacion.activeInHierarchy && simulationManager != null)
        {
            contadorAgentes.text = $"Agentes: {simulationManager.ObtenerCantidadAgentes()}";
        }
    }
    
    public void MostrarPantallaInicio()
    {
        panelInicio.SetActive(true);
        panelSimulacion.SetActive(false);
        Time.timeScale = 0f; // Pausar el tiempo
    }
    
    public void MostrarPantallaSimulacion()
    {
        panelInicio.SetActive(false);
        panelSimulacion.SetActive(true);
        Time.timeScale = 1f; // Reanudar el tiempo
    }
    
    private void OnPlayClick()
    {
        MostrarPantallaSimulacion();
        simulationManager.IniciarSimulacion();
    }
    
    private void OnSalirClick()
    {
        Application.Quit();
        
        // Para el editor de Unity
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
    
    private void OnPausaClick()
    {
        simulacionPausada = !simulacionPausada;
        
        if (simulacionPausada)
        {
            Time.timeScale = 0f;
            botonPausa.GetComponentInChildren<TMP_Text>().text = "Reanudar";
        }
        else
        {
            Time.timeScale = sliderVelocidad.value;
            botonPausa.GetComponentInChildren<TMP_Text>().text = "Pausa";
        }
    }
    
    private void OnReiniciarClick()
    {
        simulationManager.ReiniciarSimulacion();
    }
    
    private void OnAgregarAgenteClick()
    {
        simulationManager.AgregarAgente();
    }
    
    private void OnEliminarAgenteClick()
    {
        simulationManager.EliminarAgente();
    }
    
    private void OnMenuPrincipalClick()
    {
        simulationManager.DetenerSimulacion();
        MostrarPantallaInicio();
    }
    
    private void OnVelocidadCambiada(float valor)
    {
        if (!simulacionPausada)
        {
            Time.timeScale = valor;
        }
    }
    
    private void OnComportamientoCambiado(int indice)
    {
        string comportamiento = dropdownComportamiento.options[indice].text;
        simulationManager.CambiarComportamiento(comportamiento);
    }
}