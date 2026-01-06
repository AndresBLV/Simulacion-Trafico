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
            roadGraph.CleanInvalidEdges();
            roadGraph.RebuildAllConnections();
            
            Debug.Log($"Grafo inicializado: {roadGraph.nodes.Count} nodos, {roadGraph.edges.Count} aristas válidas");
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
        
        // Diccionario para evitar nodos duplicados en la misma posición
        Dictionary<Vector3Int, GraphNode> positionToNode = new Dictionary<Vector3Int, GraphNode>();
        
        // Función para redondear posición a una precisión determinada
        Vector3Int RoundPosition(Vector3 position, float precision = 0.5f)
        {
            return new Vector3Int(
                Mathf.RoundToInt(position.x / precision),
                Mathf.RoundToInt(position.y / precision),
                Mathf.RoundToInt(position.z / precision)
            );
        }
        
        // Primero procesar todas las intersecciones
        foreach (Transform intersection in intersectionTransforms)
        {
            Vector3Int roundedPos = RoundPosition(intersection.position, 1f); // Mayor precisión para intersecciones
            
            if (!positionToNode.ContainsKey(roundedPos))
            {
                GraphNode intersectionNode = roadGraph.AddNode(intersection.position, intersection.name, "Intersection");
                intersectionNode.isIntersection = true;
                intersectionNode.nodeType = "intersection";
                CreateVisualIndicator(intersectionNode.position, intersectionIndicatorPrefab);
                
                positionToNode[roundedPos] = intersectionNode;
            }
            else
            {
                Debug.Log($"Intersección duplicada en posición {intersection.position}, usando nodo existente");
            }
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
                Vector3Int roundedPos = RoundPosition(node.position, 0.5f);
                GraphNode graphNode;
                
                // Verificar si ya existe un nodo en esta posición
                if (positionToNode.TryGetValue(roundedPos, out GraphNode existingNode))
                {
                    graphNode = existingNode;
                    
                    // Actualizar información del nodo existente si es necesario
                    if (string.IsNullOrEmpty(graphNode.originalName))
                        graphNode.originalName = node.name;
                    if (string.IsNullOrEmpty(graphNode.roadName))
                        graphNode.roadName = road.name;
                        
                    Debug.Log($"Reutilizando nodo existente en posición {node.position} para {road.name}");
                }
                else
                {
                    // Crear nuevo nodo
                    graphNode = roadGraph.AddNode(node.position, node.name, road.name);
                    positionToNode[roundedPos] = graphNode;
                }
                
                currentRoadNodes.Add(graphNode);
                
                // Verificar si está cerca de una intersección existente
                foreach (var kvp in positionToNode)
                {
                    if (kvp.Value.isIntersection && 
                        Vector3.Distance(graphNode.position, kvp.Value.position) < 3f)
                    {
                        // Convertir este nodo en intersección también
                        graphNode.isIntersection = true;
                        graphNode.nodeType = "intersection";
                        break;
                    }
                }
                
                if (graphNode.isIntersection)
                {
                    CreateVisualIndicator(graphNode.position, intersectionIndicatorPrefab);
                }
                else
                {
                    CreateVisualIndicator(graphNode.position, nodeIndicatorPrefab);
                }
                
                // Conectar con el nodo anterior en la misma carretera
                if (previousNode != null && previousNode != graphNode) // Evitar conectar consigo mismo
                {
                    float distance = Vector3.Distance(previousNode.position, graphNode.position);
                    roadGraph.ConnectNodes(previousNode, graphNode, distance);
                    
                    // También crear conexión inversa para navegación bidireccional
                    roadGraph.ConnectNodes(graphNode, previousNode, distance);
                }
                
                previousNode = graphNode;
            }
            
            roadNodes[road.name] = currentRoadNodes;
        }
        
        // Conectar intersecciones con los nodos de carreteras cercanos
        int intersectionConnections = 0;
        foreach (var kvp in positionToNode)
        {
            GraphNode node = kvp.Value;
            
            if (node.isIntersection)
            {
                // Buscar todos los nodos cercanos (no solo de roadNodes)
                foreach (var otherNode in roadGraph.nodes)
                {
                    if (otherNode != node && !node.edges.Any(e => e.endNodeId == otherNode.id))
                    {
                        float distance = Vector3.Distance(node.position, otherNode.position);
                        
                        // Conexión más generosa para intersecciones
                        if (distance < 15f)
                        {
                            roadGraph.ConnectNodes(node, otherNode, distance);
                            roadGraph.ConnectNodes(otherNode, node, distance);
                            intersectionConnections++;
                            
                            // Si está muy cerca, marcar el otro nodo como intersección también
                            if (distance < 5f)
                            {
                                otherNode.isIntersection = true;
                                otherNode.nodeType = "intersection";
                            }
                        }
                    }
                }
            }
        }
        
        Debug.Log($"Creadas {intersectionConnections} conexiones de intersecciones");
        
        // Conectar nodos que estén muy cerca (posibles intersecciones)
        ConnectCloseNodes(8f); // Aumentar umbral para mejor conectividad
        
        // Limpiar aristas inválidas antes de reconstruir conexiones
        roadGraph.CleanInvalidEdges();
        
        // Reconstruir todas las conexiones
        roadGraph.RebuildAllConnections();
        
        // Verificar consistencia del grafo
        int isolatedNodes = 0;
        foreach (var node in roadGraph.nodes)
        {
            if (node.edges.Count == 0)
            {
                isolatedNodes++;
                Debug.LogWarning($"Nodo aislado detectado: {node.id} en posición {node.position}");
            }
        }
        
        Debug.Log($"Grafo construido exitosamente:");
        Debug.Log($"- Nodos totales: {roadGraph.nodes.Count}");
        Debug.Log($"- Nodos de intersección: {roadGraph.nodes.Count(n => n.isIntersection)}");
        Debug.Log($"- Aristas válidas: {roadGraph.edges.Count}");
        Debug.Log($"- Nodos aislados: {isolatedNodes}");
        
        if (isolatedNodes > 0)
        {
            Debug.LogWarning($"Hay {isolatedNodes} nodos aislados. Considera aumentar el umbral de conexión.");
        }
    }
    
    private void ConnectCloseNodes(float maxDistance)
    {
        // Usar una cuadrícula espacial para mejorar el rendimiento
        Dictionary<Vector3Int, List<GraphNode>> spatialGrid = new Dictionary<Vector3Int, List<GraphNode>>();
        float gridSize = maxDistance;
        
        // Colocar nodos en la cuadrícula
        foreach (var node in roadGraph.nodes)
        {
            Vector3Int gridKey = new Vector3Int(
                Mathf.FloorToInt(node.position.x / gridSize),
                Mathf.FloorToInt(node.position.y / gridSize),
                Mathf.FloorToInt(node.position.z / gridSize)
            );
            
            if (!spatialGrid.ContainsKey(gridKey))
                spatialGrid[gridKey] = new List<GraphNode>();
            
            spatialGrid[gridKey].Add(node);
        }
        
        // Conectar nodos cercanos
        int connectionsMade = 0;
        foreach (var gridCell in spatialGrid)
        {
            foreach (var nodeA in gridCell.Value)
            {
                // Verificar nodos en esta celda y celdas adyacentes
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            Vector3Int neighborKey = new Vector3Int(
                                gridCell.Key.x + dx,
                                gridCell.Key.y + dy,
                                gridCell.Key.z + dz
                            );
                            
                            if (spatialGrid.ContainsKey(neighborKey))
                            {
                                foreach (var nodeB in spatialGrid[neighborKey])
                                {
                                    if (nodeA != nodeB && nodeA.id < nodeB.id) // Evitar duplicados y auto-conexiones
                                    {
                                        float distance = Vector3.Distance(nodeA.position, nodeB.position);
                                        if (distance <= maxDistance && 
                                            !roadGraph.edges.Any(e => 
                                                (e.startNodeId == nodeA.id && e.endNodeId == nodeB.id) ||
                                                (e.startNodeId == nodeB.id && e.endNodeId == nodeA.id)))
                                        {
                                            roadGraph.ConnectNodes(nodeA, nodeB, distance);
                                            roadGraph.ConnectNodes(nodeB, nodeA, distance);
                                            connectionsMade++;
                                            
                                            // Marcar como intersección si están muy cerca
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
                        }
                    }
                }
            }
        }
        
        Debug.Log($"Creadas {connectionsMade} conexiones entre nodos cercanos");
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

    public void DebugGraphInfo()
    {
        Debug.Log("=== INFORMACIÓN DEL GRAFO ===");
        Debug.Log($"Nodos: {roadGraph.nodes.Count}");
        Debug.Log($"Aristas: {roadGraph.edges.Count}");
        
        int invalidEdges = 0;
        foreach (var edge in roadGraph.edges)
        {
            var startNode = roadGraph.GetNodeById(edge.startNodeId);
            var endNode = roadGraph.GetNodeById(edge.endNodeId);
            
            if (startNode == null || endNode == null)
            {
                invalidEdges++;
                Debug.LogError($"Arista inválida: {edge.startNodeId}->{edge.endNodeId}");
            }
        }
        
        Debug.Log($"Aristas inválidas: {invalidEdges}");
        
        // Verificar nodos sin conexiones
        int isolatedNodes = 0;
        foreach (var node in roadGraph.nodes)
        {
            if (node.edges.Count == 0)
            {
                isolatedNodes++;
                Debug.LogWarning($"Nodo aislado: {node.id} en posición {node.position}");
            }
        }
        
        Debug.Log($"Nodos aislados: {isolatedNodes}");
    }
}