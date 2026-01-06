using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GraphNode
{
    public int id;
    public Vector3 position;
    public bool isIntersection = false;
    public string nodeType = "regular";
    public string originalName = "";
    public string roadName = "";

     // Propiedad name que combina la información disponible
    public string name 
    { 
        get 
        { 
            if (!string.IsNullOrEmpty(originalName)) 
                return originalName;
            else if (!string.IsNullOrEmpty(roadName)) 
                return roadName;
            else 
                return $"Node_{id}";
        } 
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