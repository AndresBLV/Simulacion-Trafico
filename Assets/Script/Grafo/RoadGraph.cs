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
        
        return newEdge;
    }
    
    public GraphNode GetNodeById(int id)
    {
        return nodes.FirstOrDefault(n => n.id == id);
    }
    
    public List<GraphNode> FindPath(GraphNode start, GraphNode goal)
    {
        // Validación de parámetros
        if (start == null || goal == null)
        {
            Debug.LogError("Start o Goal node es null en FindPath");
            return null;
        }
        
        // Verificar que los nodos existan en el grafo
        if (!nodes.Contains(start) || !nodes.Contains(goal))
        {
            Debug.LogError($"Start o Goal node no existe en el grafo. Start: {start?.id}, Goal: {goal?.id}");
            return null;
        }
        
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
            
            foreach (GraphEdge edge in current.edges.ToList()) // Usar ToList para evitar modificaciones durante iteración
            {
                // Asegurarse de que la conexión esté reconstruida
                if (edge.endNode == null)
                {
                    edge.RebuildConnections(this);
                    
                    // Si después de reconstruir sigue siendo null, omitir esta arista
                    if (edge.endNode == null)
                    {
                        Debug.LogWarning($"Arista {edge.startNodeId}->{edge.endNodeId} no pudo reconstruir endNode. Se omite.");
                        continue;
                    }
                }
                    
                GraphNode neighbor = edge.endNode;
                
                // Verificar que el vecino exista y no sea null
                if (neighbor == null)
                {
                    Debug.LogWarning($"Vecino es null en arista {edge.startNodeId}->{edge.endNodeId}");
                    continue;
                }
                
                // Verificar que el vecino exista en la lista de nodos
                if (!nodes.Contains(neighbor))
                {
                    Debug.LogWarning($"Nodo vecino {neighbor.id} no encontrado en lista de nodos del grafo");
                    continue;
                }
                
                float currentGScore = gScore.ContainsKey(current) ? gScore[current] : float.MaxValue;
                float tentativeGScore = currentGScore + edge.TotalCost;
                
                float neighborGScore = gScore.ContainsKey(neighbor) ? gScore[neighbor] : float.MaxValue;
                
                if (tentativeGScore < neighborGScore)
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;
                    fScore[neighbor] = tentativeGScore + Heuristic(neighbor, goal);
                    
                    if (!openSet.Contains(neighbor))
                        openSet.Add(neighbor);
                }
            }
        }
        
        Debug.LogWarning($"No se encontró camino de {start.id} a {goal.id}");
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
    
    public void CleanInvalidEdges()
    {
        int removedEdges = 0;
        
        // Crear lista de aristas para eliminar
        List<GraphEdge> edgesToRemove = new List<GraphEdge>();
        
        foreach (var edge in edges)
        {
            var startNode = GetNodeById(edge.startNodeId);
            var endNode = GetNodeById(edge.endNodeId);
            
            if (startNode == null || endNode == null)
            {
                edgesToRemove.Add(edge);
                removedEdges++;
                Debug.LogWarning($"Removiendo arista inválida: {edge.startNodeId}->{edge.endNodeId}");
            }
        }
        
        // Remover aristas inválidas
        foreach (var edge in edgesToRemove)
        {
            edges.Remove(edge);
        }
        
        if (removedEdges > 0)
        {
            Debug.Log($"Removidas {removedEdges} aristas inválidas");
            RebuildAllConnections();
        }
    }
}