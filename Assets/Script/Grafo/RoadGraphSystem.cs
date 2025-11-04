using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RoadGraphSystem : MonoBehaviour
{
    public RoadGraph roadGraph = new RoadGraph();
    public bool visualizeGraph = true;
    public GameObject intersectionIndicatorPrefab;
    public GameObject nodeIndicatorPrefab;
    
    private List<GameObject> visualIndicators = new List<GameObject>();
    
    void Awake()
    {
        // Reconstruir conexiones al despertar
        if (roadGraph != null)
        {
            roadGraph.RebuildAllConnections();
        }
    }
    
    public void BuildGraphFromRoadArchitect(GameObject roadArchitectContainer)
    {
        roadGraph = new RoadGraph();
        ClearVisualIndicators();
        
        // Buscar el objeto RoadArchitectSystem2
        Transform roadArchitectSystem = roadArchitectContainer.transform.Find("RoadArchitectSystem2");
        if (roadArchitectSystem == null)
        {
            Debug.LogError("No se encontró RoadArchitectSystem2 en el contenedor");
            return;
        }
        
        // Buscar la carpeta de intersecciones
        Transform intersectionsFolder = roadArchitectSystem.Find("Intersections");
        List<Transform> intersectionTransforms = new List<Transform>();
        if (intersectionsFolder != null)
        {
            foreach (Transform intersection in intersectionsFolder)
            {
                if (intersection.name.StartsWith("Inter"))
                {
                    intersectionTransforms.Add(intersection);
                }
            }
            Debug.Log($"Encontradas {intersectionTransforms.Count} intersecciones");
        }
        
        // Buscar todas las carreteras (Road)
        List<Transform> roads = new List<Transform>();
        foreach (Transform child in roadArchitectSystem)
        {
            if (child.name.StartsWith("Road"))
            {
                roads.Add(child);
            }
        }
        
        Debug.Log($"Encontradas {roads.Count} carreteras en Road Architect");
        
        // Primero procesar todas las intersecciones
        Dictionary<Vector3, GraphNode> intersectionNodes = new Dictionary<Vector3, GraphNode>();
        foreach (Transform intersection in intersectionTransforms)
        {
            GraphNode intersectionNode = roadGraph.AddNode(intersection.position, intersection.name, "Intersection");
            intersectionNode.isIntersection = true;
            intersectionNode.nodeType = "intersection";
            CreateVisualIndicator(intersectionNode.position, intersectionIndicatorPrefab);
            
            // Almacenar para conexiones posteriores
            intersectionNodes[intersection.position] = intersectionNode;
        }
        
        // Procesar cada carretera
        Dictionary<string, List<GraphNode>> roadNodes = new Dictionary<string, List<GraphNode>>();
        foreach (Transform road in roads)
        {
            // Buscar la spline de la carretera
            Transform spline = road.Find("Spline");
            if (spline == null)
            {
                Debug.LogWarning($"No se encontró Spline en la carretera {road.name}");
                continue;
            }
            
            // Recoger todos los nodos de la spline
            List<Transform> nodes = new List<Transform>();
            foreach (Transform child in spline)
            {
                if (child.name.StartsWith("Node"))
                {
                    nodes.Add(child);
                }
            }
            
            // Ordenar los nodos por nombre
            nodes = nodes.OrderBy(n => n.name).ToList();
            
            Debug.Log($"Encontrados {nodes.Count} nodos en la spline de {road.name}");
            
            // Crear nodos en el grafo y conectarlos
            List<GraphNode> currentRoadNodes = new List<GraphNode>();
            GraphNode previousNode = null;
            
            foreach (Transform node in nodes)
            {
                GraphNode graphNode = roadGraph.AddNode(node.position, node.name, road.name);
                currentRoadNodes.Add(graphNode);
                
                // Verificar si está cerca de una intersección
                foreach (var intersection in intersectionNodes)
                {
                    if (Vector3.Distance(graphNode.position, intersection.Key) < 5f)
                    {
                        graphNode.isIntersection = true;
                        graphNode.nodeType = "intersection";
                        CreateVisualIndicator(graphNode.position, intersectionIndicatorPrefab);
                        break;
                    }
                }
                
                if (!graphNode.isIntersection)
                {
                    CreateVisualIndicator(graphNode.position, nodeIndicatorPrefab);
                }
                
                // Conectar con el nodo anterior en la misma carretera
                if (previousNode != null)
                {
                    float distance = Vector3.Distance(previousNode.position, graphNode.position);
                    roadGraph.ConnectNodes(previousNode, graphNode, distance);
                }
                
                previousNode = graphNode;
            }
            
            roadNodes[road.name] = currentRoadNodes;
        }
        
        // Conectar intersecciones con los nodos de carreteras cercanos
        foreach (var intersection in intersectionNodes)
        {
            foreach (var road in roadNodes)
            {
                foreach (GraphNode roadNode in road.Value)
                {
                    float distance = Vector3.Distance(intersection.Key, roadNode.position);
                    if (distance < 10f) // Umbral para conectar intersecciones con nodos de carretera
                    {
                        roadGraph.ConnectNodes(intersection.Value, roadNode, distance);
                        roadGraph.ConnectNodes(roadNode, intersection.Value, distance);
                        
                        // Marcar como intersección si está cerca
                        if (distance < 5f)
                        {
                            roadNode.isIntersection = true;
                            roadNode.nodeType = "intersection";
                        }
                    }
                }
            }
        }
        
        // Conectar nodos que estén muy cerca (posibles intersecciones)
        ConnectCloseNodes(5f);
        
        // Reconstruir todas las conexiones
        roadGraph.RebuildAllConnections();
        
        Debug.Log($"Grafo construido con {roadGraph.nodes.Count} nodos y {roadGraph.edges.Count} aristas. " +
                 $"{roadGraph.nodes.Count(n => n.isIntersection)} intersecciones detectadas.");
    }
    
    private void ConnectCloseNodes(float maxDistance)
    {
        for (int i = 0; i < roadGraph.nodes.Count; i++)
        {
            for (int j = i + 1; j < roadGraph.nodes.Count; j++)
            {
                GraphNode nodeA = roadGraph.nodes[i];
                GraphNode nodeB = roadGraph.nodes[j];
                
                float distance = Vector3.Distance(nodeA.position, nodeB.position);
                if (distance <= maxDistance && !nodeA.edges.Any(e => e.endNodeId == nodeB.id))
                {
                    roadGraph.ConnectNodes(nodeA, nodeB, distance);
                    
                    // Si están muy cerca, marcar como intersección
                    if (distance < 3f)
                    {
                        nodeA.isIntersection = true;
                        nodeB.isIntersection = true;
                        nodeA.nodeType = "intersection";
                        nodeB.nodeType = "intersection";
                    }
                }
            }
        }
    }
    
    private void CreateVisualIndicator(Vector3 position, GameObject prefab)
    {
        if (prefab != null)
        {
            GameObject indicator = Instantiate(prefab, position, Quaternion.identity, transform);
            visualIndicators.Add(indicator);
        }
    }
    
    private void ClearVisualIndicators()
    {
        foreach (GameObject indicator in visualIndicators)
        {
            if (indicator != null)
                DestroyImmediate(indicator);
        }
        visualIndicators.Clear();
    }
    
    void OnDrawGizmos()
    {
        if (!visualizeGraph || roadGraph == null) return;
        
        // Asegurarse de que las conexiones estén reconstruidas
        roadGraph.RebuildAllConnections();
        
        foreach (GraphNode node in roadGraph.nodes)
        {
            Gizmos.color = node.isIntersection ? Color.red : Color.blue;
            Gizmos.DrawSphere(node.position, node.isIntersection ? 0.7f : 0.5f);
            
            // Mostrar el nombre original del nodo
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(node.position + Vector3.up, $"{node.originalName}\n({node.roadName})");
            #endif
        }
        
        Gizmos.color = Color.green;
        foreach (GraphEdge edge in roadGraph.edges)
        {
            // Asegurarse de que la conexión esté reconstruida
            if (edge.startNode == null || edge.endNode == null)
                edge.RebuildConnections(roadGraph);
                
            Gizmos.DrawLine(edge.startNode.position, edge.endNode.position);
            
            Vector3 direction = (edge.endNode.position - edge.startNode.position).normalized;
            Vector3 perpendicular = Vector3.Cross(direction, Vector3.up).normalized * 0.3f;
            Vector3 arrowStart = edge.startNode.position + (edge.endNode.position - edge.startNode.position) * 0.7f;
            
            Gizmos.DrawLine(arrowStart, arrowStart + direction * 1f - perpendicular * 0.5f);
            Gizmos.DrawLine(arrowStart, arrowStart + direction * 1f + perpendicular * 0.5f);
            
            if (edge.trafficCost > 0)
            {
                Gizmos.color = Color.Lerp(Color.yellow, Color.red, Mathf.Clamp01(edge.trafficCost / 5f));
                Gizmos.DrawSphere((edge.startNode.position + edge.endNode.position) / 2, 0.3f);
            }
        }
    }
}