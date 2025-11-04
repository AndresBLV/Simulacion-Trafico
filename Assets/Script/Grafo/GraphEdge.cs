using UnityEngine;

[System.Serializable]
public class GraphEdge
{
    // Almacenar IDs en lugar de referencias directas para la serialización
    public int startNodeId;
    public int endNodeId;
    
    public float baseCost = 1f;
    public float trafficCost = 0f;
    public int laneCount = 1;
    public float speedLimit = 50f;
    public string roadType = "street";
    
    // No serializar las referencias a nodos para evitar circularidad
    [System.NonSerialized]
    public GraphNode startNode;
    
    [System.NonSerialized]
    public GraphNode endNode;
    
    public GraphEdge(int startId, int endId, float cost = 1f)
    {
        startNodeId = startId;
        endNodeId = endId;
        baseCost = cost;
    }
    
    public float TotalCost {
        get { return baseCost + trafficCost; }
    }
    
    // Método para reconstruir conexiones después de la deserialización
    public void RebuildConnections(RoadGraph graph)
    {
        startNode = graph.GetNodeById(startNodeId);
        endNode = graph.GetNodeById(endNodeId);
    }
}