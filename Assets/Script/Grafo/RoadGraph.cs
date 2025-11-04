using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class RoadGraph
{
    public List<GraphNode> nodes = new List<GraphNode>();
    public List<GraphEdge> edges = new List<GraphEdge>();
    
    public GraphNode AddNode(Vector3 position, string originalName = "", string roadName = "")
    {
        int newId = nodes.Count > 0 ? nodes.Max(n => n.id) + 1 : 0;
        GraphNode newNode = new GraphNode(newId, position, originalName, roadName);
        nodes.Add(newNode);
        return newNode;
    }
    
    public GraphEdge ConnectNodes(GraphNode start, GraphNode end, float cost = 1f)
    {
        // Verificar si la conexión ya existe
        if (edges.Any(e => e.startNodeId == start.id && e.endNodeId == end.id))
            return edges.First(e => e.startNodeId == start.id && e.endNodeId == end.id);
            
        GraphEdge newEdge = new GraphEdge(start.id, end.id, cost);
        edges.Add(newEdge);
        
        // Reconstruir conexiones inmediatamente
        newEdge.RebuildConnections(this);
        start.edges.Add(newEdge);
        
        return newEdge;
    }
    
    public GraphNode GetNodeById(int id)
    {
        return nodes.FirstOrDefault(n => n.id == id);
    }
    
    public List<GraphNode> FindPath(GraphNode start, GraphNode goal)
    {
        // Asegurarse de que todas las conexiones estén reconstruidas
        RebuildAllConnections();
        
        Dictionary<GraphNode, GraphNode> cameFrom = new Dictionary<GraphNode, GraphNode>();
        Dictionary<GraphNode, float> gScore = new Dictionary<GraphNode, float>();
        Dictionary<GraphNode, float> fScore = new Dictionary<GraphNode, float>();
        
        List<GraphNode> openSet = new List<GraphNode> { start };
        gScore[start] = 0;
        fScore[start] = Heuristic(start, goal);
        
        while (openSet.Count > 0)
        {
            GraphNode current = openSet.OrderBy(n => fScore.ContainsKey(n) ? fScore[n] : float.MaxValue).First();
            
            if (current == goal)
                return ReconstructPath(cameFrom, current);
                
            openSet.Remove(current);
            
            foreach (GraphEdge edge in current.edges)
            {
                // Asegurarse de que la conexión esté reconstruida
                if (edge.endNode == null)
                    edge.RebuildConnections(this);
                    
                GraphNode neighbor = edge.endNode;
                float tentativeGScore = (gScore.ContainsKey(current) ? gScore[current] : float.MaxValue) + edge.TotalCost;
                
                if (!gScore.ContainsKey(neighbor) || tentativeGScore < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;
                    fScore[neighbor] = gScore[neighbor] + Heuristic(neighbor, goal);
                    
                    if (!openSet.Contains(neighbor))
                        openSet.Add(neighbor);
                }
            }
        }
        
        return null;
    }
    
    private float Heuristic(GraphNode a, GraphNode b)
    {
        return Vector3.Distance(a.position, b.position);
    }
    
    private List<GraphNode> ReconstructPath(Dictionary<GraphNode, GraphNode> cameFrom, GraphNode current)
    {
        List<GraphNode> path = new List<GraphNode> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Insert(0, current);
        }
        return path;
    }
    
    public GraphNode GetNearestNode(Vector3 position)
    {
        if (nodes.Count == 0) return null;
        return nodes.OrderBy(n => Vector3.Distance(n.position, position)).First();
    }
    
    // Método para reconstruir todas las conexiones después de la deserialización
    public void RebuildAllConnections()
    {
        foreach (var node in nodes)
        {
            node.RebuildConnections(this);
        }
        
        foreach (var edge in edges)
        {
            edge.RebuildConnections(this);
        }
    }
}