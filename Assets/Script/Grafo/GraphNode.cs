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
        foreach (var edge in graph.edges)
        {
            if (edge.startNodeId == this.id)
            {
                edges.Add(edge);
            }
        }
    }
}