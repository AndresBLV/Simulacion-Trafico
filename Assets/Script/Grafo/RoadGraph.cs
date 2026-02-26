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
        // 🔒 REGLA DE CONECTIVIDAD
        bool sameRoad = start.roadName == end.roadName;

        bool startIsIntersection = start.roadName == "Intersection";
        bool endIsIntersection = end.roadName == "Intersection";

        // ❌ Bloquear conexiones inválidas
        if (!sameRoad && !startIsIntersection && !endIsIntersection)
        {
            return null;
        }

        // Verificar si ya existe
        if (edges.Any(e => e.startNodeId == start.id && e.endNodeId == end.id))
            return edges.First(e => e.startNodeId == start.id && e.endNodeId == end.id);

        GraphEdge newEdge = new GraphEdge(start.id, end.id, cost);
        edges.Add(newEdge);

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
    
    public GraphNode GetNearestNode(Vector3 position, float maxDistance = 5f)
    {
        var nearest = nodes
            .Where(n => Vector3.Distance(n.position, position) <= maxDistance)
            .OrderBy(n => Vector3.Distance(n.position, position))
            .FirstOrDefault();

        return nearest;
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
    
    public void CleanInvalidEdges(float maxEdgeDistance = 8f)
    {
        int removedEdges = 0;
        
        // Crear lista de aristas para eliminar
        List<GraphEdge> edgesToRemove = new List<GraphEdge>();
        
        foreach (var edge in edges)
        {
            var startNode = GetNodeById(edge.startNodeId);
            var endNode = GetNodeById(edge.endNodeId);
            
            bool remove = false;

            // Eliminar aristas con nodos nulos
            if (startNode == null || endNode == null)
            {
                remove = true;
            }
            else
            {
                // Eliminar aristas demasiado largas
                float distance = Vector3.Distance(startNode.position, endNode.position);
                if (distance > maxEdgeDistance)
                {
                    remove = true;
                    Debug.LogWarning($"ARISTA DEMASIADO LARGA ELIMINADA: {startNode.originalName} -> {endNode.originalName} | Dist: {distance}");
                }
            }
            
            if (remove)
            {
                edgesToRemove.Add(edge);
                removedEdges++;
            }
        }
        
        // Remover aristas inválidas
        foreach (var edge in edgesToRemove)
        {
            edges.Remove(edge);
        }
        
        if (removedEdges > 0)
        {
            Debug.Log($"Removidas {removedEdges} aristas inválidas o demasiado largas");
            RebuildAllConnections();
        }
    }

    public void RemoveEdge(GraphNode start, GraphNode end)
    {
        // Eliminar la arista desde start hacia end
        edges.RemoveAll(e => e.startNodeId == start.id && e.endNodeId == end.id);
        
        // También eliminar la arista desde end hacia start (si el grafo es bidireccional)
        edges.RemoveAll(e => e.startNodeId == end.id && e.endNodeId == start.id);

        // Limpiar las listas de edges de los nodos
        start.edges.RemoveAll(e => e.endNodeId == end.id);
        end.edges.RemoveAll(e => e.endNodeId == start.id);
    }

    public GraphNode FindClosestConnectedNode(GraphNode node)
    {
        if (node == null) return null;

        // Si el nodo ya tiene conexiones, devolverlo
        if (node.edges != null && node.edges.Count > 0)
            return node;

        // Buscar nodo cercano en el grafo que tenga conexiones
        GraphNode closest = null;
        float minDistance = float.MaxValue;

        foreach (var n in nodes)
        {
            if (n == null || n.edges == null || n.edges.Count == 0) continue;

            float dist = Vector3.Distance(node.position, n.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                closest = n;
            }
        }

        return closest;
    }

        public List<GraphNode> FindNodesByName(string name, bool exactMatch = false)
    {
        string searchName = name.ToLower();
        var results = new List<GraphNode>();
        
        foreach (var node in nodes)
        {
            bool found = false;
            
            // 1. Buscar en searchName (incluye specialName)
            string nodeSearchName = node.searchName.ToLower();
            if (exactMatch)
            {
                if (nodeSearchName == searchName)
                    found = true;
            }
            else
            {
                if (nodeSearchName.Contains(searchName))
                    found = true;
            }
            
            // 2. Buscar en originalName (por si acaso)
            if (!found && !string.IsNullOrEmpty(node.originalName))
            {
                string originalLower = node.originalName.ToLower();
                if (exactMatch)
                {
                    if (originalLower == searchName)
                        found = true;
                }
                else
                {
                    if (originalLower.Contains(searchName))
                        found = true;
                }
            }
            
            if (found)
                results.Add(node);
        }
        
        return results;
    }
    
    // Método para encontrar nodos especiales específicos
    public List<GraphNode> FindSpecialNodes(string specialNameKeyword = "")
    {
        var specialNodes = new List<GraphNode>();
        
        foreach (var node in nodes)
        {
            if (node.HasSpecialName())
            {
                if (string.IsNullOrEmpty(specialNameKeyword))
                {
                    specialNodes.Add(node);
                }
                else
                {
                    string nodeSpecialName = node.specialName.ToLower();
                    if (nodeSpecialName.Contains(specialNameKeyword.ToLower()))
                    {
                        specialNodes.Add(node);
                    }
                }
            }
        }
        
        return specialNodes;
    }
}