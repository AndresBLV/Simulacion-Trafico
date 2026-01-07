using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GraphNode
{
    public int id;
    
    public Vector3 position;
    public bool isIntersection = false;
    public string nodeType = "regular";
    public string originalName = "";  // Nombre original del GameObject
    public string roadName = "";
    
    // NUEVO: Nombre para display y búsqueda (puede ser diferente del original)
    public string displayName = "";
    
    // NUEVO: Nombre especial (para las entradas importantes)
    public string specialName = "";
    
    // Propiedad name que combina la información disponible
    // PRIORIDAD: displayName -> originalName -> roadName -> Node_id
    public string name 
    { 
        get 
        { 
            if (!string.IsNullOrEmpty(displayName)) 
                return displayName;
            if (!string.IsNullOrEmpty(originalName)) 
                return originalName;
            if (!string.IsNullOrEmpty(roadName)) 
                return roadName;
            return $"Node_{id}";
        } 
    }
    
    // Propiedad para búsqueda (incluye todos los nombres posibles)
    public string searchName
    {
        get
        {
            // Si tiene un nombre especial, usarlo primero
            if (!string.IsNullOrEmpty(specialName))
                return specialName;
            return name;
        }
    }
    
    // Verificar si tiene nombre especial
    public bool HasSpecialName()
    {
        return !string.IsNullOrEmpty(specialName);
    }
    
    // No serializar las edges para evitar circularidad
    [System.NonSerialized]
    public List<GraphEdge> edges = new List<GraphEdge>();
    
    public GraphNode(int id, Vector3 position, string originalName = "", string roadName = "")
    {
        this.id = id;
        this.position = position;
        this.originalName = originalName;
        this.roadName = roadName;
        this.displayName = originalName; // Por defecto, displayName = originalName
        this.specialName = ""; // Inicialmente vacío
    }
    
    // Método para establecer un nombre especial
    public void SetSpecialName(string specialName)
    {
        if (!string.IsNullOrEmpty(specialName))
        {
            this.specialName = specialName;
            this.displayName = specialName; // También actualizar displayName
        }
    }
    
    // Método para reconstruir conexiones después de la deserialización
    public void RebuildConnections(RoadGraph graph)
    {
        edges.Clear();
        
        // Buscar aristas donde este nodo sea el inicio
        foreach (var edge in graph.edges)
        {
            // Asegurarse de reconstruir la conexión primero
            edge.RebuildConnections(graph);
            
            if (edge.startNodeId == this.id)
            {
                // Verificar que endNode existe antes de agregar
                if (edge.endNode != null)
                {
                    edges.Add(edge);
                }
                else
                {
                    Debug.LogWarning($"Nodo {id}: Arista con endNodeId={edge.endNodeId} tiene endNode null. Se omite.");
                }
            }
        }
    }
}