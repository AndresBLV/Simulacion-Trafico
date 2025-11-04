using UnityEngine;
using System.Collections.Generic;

public class SimulationManager : MonoBehaviour
{
    [Header("Prefabs y Configuración")]
    public GameObject agentePrefab;
    public int agentesIniciales = 10;
    public Transform areaSpawning;
    
    private List<GameObject> agentes = new List<GameObject>();
    
    void Start()
    {
        // Opcional: Iniciar con algunos agentes
        // for (int i = 0; i < agentesIniciales; i++)
        // {
        //     AgregarAgente();
        // }
    }
    
    public void IniciarSimulacion()
    {
        // Lógica para iniciar la simulación
        Debug.Log("Simulación iniciada");
    }
    
    public void DetenerSimulacion()
    {
        // Lógica para detener la simulación
        Debug.Log("Simulación detenida");
    }
    
    public void ReiniciarSimulacion()
    {
        // Eliminar todos los agentes existentes
        foreach (GameObject agente in agentes)
        {
            Destroy(agente);
        }
        agentes.Clear();
        
        // Crear nuevos agentes
        for (int i = 0; i < agentesIniciales; i++)
        {
            AgregarAgente();
        }
        
        Debug.Log("Simulación reiniciada");
    }
    
    public void AgregarAgente()
    {
        if (agentePrefab != null && areaSpawning != null)
        {
            Vector3 posicion = new Vector3(
                Random.Range(areaSpawning.position.x - areaSpawning.localScale.x / 2, 
                            areaSpawning.position.x + areaSpawning.localScale.x / 2),
                areaSpawning.position.y,
                Random.Range(areaSpawning.position.z - areaSpawning.localScale.z / 2, 
                            areaSpawning.position.z + areaSpawning.localScale.z / 2)
            );
            
            GameObject nuevoAgente = Instantiate(agentePrefab, posicion, Quaternion.identity);
            agentes.Add(nuevoAgente);
            
            Debug.Log("Agente agregado. Total: " + agentes.Count);
        }
    }
    
    public void EliminarAgente()
    {
        if (agentes.Count > 0)
        {
            GameObject agenteAEliminar = agentes[agentes.Count - 1];
            agentes.RemoveAt(agentes.Count - 1);
            Destroy(agenteAEliminar);
            
            Debug.Log("Agente eliminado. Total: " + agentes.Count);
        }
    }
    
    public int ObtenerCantidadAgentes()
    {
        return agentes.Count;
    }
    
    public void CambiarComportamiento(string comportamiento)
    {
        // Lógica para cambiar el comportamiento de los agentes
        Debug.Log("Comportamiento cambiado a: " + comportamiento);
        
        foreach (GameObject agente in agentes)
        {
            // Aquí implementarías la lógica para cambiar el comportamiento
            // agente.GetComponent<Agente>().EstablecerComportamiento(comportamiento);
        }
    }
}