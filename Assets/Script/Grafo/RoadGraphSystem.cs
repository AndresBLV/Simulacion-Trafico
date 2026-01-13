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
    public bool buildOnAwake = true; 
    
    private List<GameObject> visualIndicators = new List<GameObject>();
    
    // Diccionario extendido de nombres especiales para TODOS los nodos importantes
    Dictionary<string, string> specialNodeNames = new Dictionary<string, string>
    {
        // Intersecciones
        { "Inter22", "Acceso Universidad Metropolitana (Terrazas del Ávila)" },
        { "Inter3", "Acceso Distribuidor Metropolitano (Autopista)" },
        
        // Nodos de SPAWN específicos que necesita TrafficManager
        // Nodos iniciales de Road1
        { "Node1(Road1)", "Spawn_Road1_Inicio1" },
        { "Node2(Road1)", "Spawn_Road1_Inicio2" },
        { "Node3(Road1)", "Spawn_Road1_Inicio3" },
        
        // Nodos iniciales de Road5
        { "Node1(Road5)", "Spawn_Road5_Inicio1" },
        { "Node2(Road5)", "Spawn_Road5_Inicio2" },
        { "Node3(Road5)", "Spawn_Road5_Inicio3" },
        
        // Nodo 22 de Road3
        { "Node22(Road3)", "Spawn_Road3_Nodo22" },
        
        // Nodos DESTINO específicos que necesita TrafficManager
        { "Node29(Road1)", "Destino_Road1_Nodo29" },
        { "Node21(Road5)", "Destino_Road5_Nodo21" }
    };
    
    // Método público para obtener nodos por nombre especial
    public List<GraphNode> GetNodesBySpecialName(string specialName)
    {
        List<GraphNode> foundNodes = new List<GraphNode>();
        
        foreach (var node in roadGraph.nodes)
        {
            if (node.specialName == specialName)
            {
                foundNodes.Add(node);
            }
        }
        
        return foundNodes;
    }
    
    // Método para obtener todos los nodos de spawn
    public List<GraphNode> GetAllSpawnNodes()
    {
        List<GraphNode> spawnNodes = new List<GraphNode>();
        
        foreach (var node in roadGraph.nodes)
        {
            if (!string.IsNullOrEmpty(node.specialName) && 
                (node.specialName.StartsWith("Spawn_") || 
                 node.specialName.StartsWith("Destino_")))
            {
                spawnNodes.Add(node);
            }
        }
        
        return spawnNodes;
    }
    
    // Método para obtener nodos de spawn específicos
    public List<GraphNode> GetSpawnNodesByType(string type)
    {
        List<GraphNode> spawnNodes = new List<GraphNode>();
        
        foreach (var node in roadGraph.nodes)
        {
            if (!string.IsNullOrEmpty(node.specialName))
            {
                if (type == "spawn" && node.specialName.StartsWith("Spawn_"))
                {
                    spawnNodes.Add(node);
                }
                else if (type == "destino" && node.specialName.StartsWith("Destino_"))
                {
                    spawnNodes.Add(node);
                }
            }
        }
        
        return spawnNodes;
    }
    
    void Awake()
    {
        if (buildOnAwake)
        {
            BuildGraphAutomatically();
            
            if (roadGraph != null && roadGraph.nodes.Count > 0)
            {
                roadGraph.CleanInvalidEdges();
                roadGraph.RebuildAllConnections();
                
                // Mostrar nodos especiales creados
                ShowSpecialNodesInfo();
                
                Debug.Log($"Grafo inicializado: {roadGraph.nodes.Count} nodos, {roadGraph.edges.Count} aristas válidas");
            }
            else
            {
                Debug.Log("Grafo vacío. Para construir el grafo, asegúrate de tener RoadArchitectSystem en la escena.");
            }
        }
        else
        {
            Debug.Log("buildOnAwake está desactivado. El grafo no se construirá automáticamente.");
        }
    }

    public void BuildGraphAutomatically()
    {
        GameObject roadArchitectContainer = FindRoadArchitectSystem();
        
        if (roadArchitectContainer != null)
        {
            BuildGraphFromRoadArchitect(roadArchitectContainer);
        }
        else
        {
            Debug.LogWarning("Road Architect no encontrado en la escena.");
        }
    }

    private GameObject FindRoadArchitectSystem()
    {
        // Buscar por nombre exacto
        GameObject roadArchitect = GameObject.Find("RoadArchitectSystem");
        
        if (roadArchitect != null)
        {
            Debug.Log($"Road Architect encontrado: {GetFullPath(roadArchitect.transform)}");
            return roadArchitect;
        }
        
        // Buscar recursivamente
        roadArchitect = FindGameObjectRecursive("RoadArchitectSystem", null);
        
        if (roadArchitect != null)
        {
            Debug.Log($"Road Architect encontrado (búsqueda recursiva): {GetFullPath(roadArchitect.transform)}");
            return roadArchitect;
        }
        
        Debug.LogWarning("No se encontró RoadArchitectSystem en la jerarquía.");
        return null;
    }
    
    public void BuildGraphFromRoadArchitect(GameObject roadArchitectContainer)
    {
        roadGraph = new RoadGraph();
        ClearVisualIndicators();
        
        if (roadArchitectContainer == null)
        {
            Debug.LogError("El contenedor de Road Architect es nulo");
            return;
        }
        
        Debug.Log($"Construyendo grafo desde: {roadArchitectContainer.name} (Ruta: {GetFullPath(roadArchitectContainer.transform)})");
        
        Transform roadArchitectSystem = roadArchitectContainer.transform;
        
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
        
        // ========== PROCESAR INTERSECCIONES ==========
        List<GraphNode> intersectionNodes = new List<GraphNode>();
        Transform intersectionsFolder = roadArchitectSystem.Find("Intersections");
        
        if (intersectionsFolder != null)
        {
            Debug.Log($"Carpeta 'Intersections' encontrada con {intersectionsFolder.childCount} hijos");
            
            foreach (Transform intersection in intersectionsFolder)
            {
                // Solo procesar objetos que comiencen con "Inter"
                if (intersection.name.StartsWith("Inter"))
                {
                    Vector3Int roundedPos = RoundPosition(intersection.position, 1f);
                    
                    if (!positionToNode.ContainsKey(roundedPos))
                    {
                        GraphNode intersectionNode = roadGraph.AddNode(
                            intersection.position, 
                            intersection.name, 
                            "Intersection"
                        );
                        intersectionNode.isIntersection = true;
                        intersectionNode.nodeType = "intersection";
                        
                        // ASIGNAR NOMBRE ESPECIAL si está en el diccionario
                        if (specialNodeNames.ContainsKey(intersection.name))
                        {
                            intersectionNode.specialName = specialNodeNames[intersection.name];
                            Debug.Log($"¡NODO ESPECIAL CREADO! {intersection.name} -> {intersectionNode.specialName}");
                        }
                        
                        intersectionNodes.Add(intersectionNode);
                        positionToNode[roundedPos] = intersectionNode;
                        
                        // Crear indicador visual
                        CreateVisualIndicator(intersectionNode.position, intersectionIndicatorPrefab);
                        
                        Debug.Log($"Intersección creada: {intersection.name} en posición {intersection.position}");
                    }
                    else
                    {
                        Debug.Log($"Intersección duplicada en posición {intersection.position}, usando nodo existente");
                    }
                }
            }
            Debug.Log($"Procesadas {intersectionNodes.Count} intersecciones");
        }
        else
        {
            Debug.Log("No se encontró la carpeta 'Intersections'");
        }
        
        // ========== PROCESAR CARRETERAS ==========
        Dictionary<string, List<GraphNode>> roadNodes = new Dictionary<string, List<GraphNode>>();
        
        // Buscar todos los hijos que empiezan con "Road" directamente bajo RoadArchitectSystem
        List<Transform> roads = new List<Transform>();
        foreach (Transform child in roadArchitectSystem)
        {
            if (child.name.StartsWith("Road"))
            {
                roads.Add(child);
            }
        }
        
        Debug.Log($"Encontradas {roads.Count} carreteras directamente bajo RoadArchitectSystem");
        
        // Procesar cada carretera
        foreach (Transform road in roads)
        {
            Debug.Log($"Procesando carretera: {road.name}");
            
            // Buscar la carpeta Spline dentro de esta carretera
            Transform spline = road.Find("Spline");
            if (spline == null)
            {
                Debug.LogWarning($"No se encontró la carpeta 'Spline' en {road.name}");
                continue;
            }
            
            Debug.Log($"Spline encontrado en {road.name} con {spline.childCount} hijos");
            
            // Recoger todos los nodos dentro de Spline que comienzan con "Node"
            List<Transform> nodes = new List<Transform>();
            foreach (Transform child in spline)
            {
                // Solo tomar los objetos que son nodos directos (no hijos de nodos)
                if (child.name.StartsWith("Node") && child.parent == spline)
                {
                    nodes.Add(child);
                }
            }
            
            // Ordenar los nodos por nombre
            nodes = nodes.OrderBy(n => n.name).ToList();
            
            if (nodes.Count == 0)
            {
                Debug.LogWarning($"No se encontraron nodos en la spline de {road.name}");
                continue;
            }
            
            Debug.Log($"Encontrados {nodes.Count} nodos en {road.name}");
            
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
                    Debug.Log($"Reutilizando nodo existente en posición {node.position} para {road.name}/{node.name}");
                }
                else
                {
                    // Crear nuevo nodo
                    graphNode = roadGraph.AddNode(node.position, node.name, road.name);
                    positionToNode[roundedPos] = graphNode;
                    
                    // Verificar si está cerca de una intersección existente
                    foreach (var intersectionNode in intersectionNodes)
                    {
                        float distance = Vector3.Distance(graphNode.position, intersectionNode.position);
                        if (distance < 2f) // Umbral para considerar que es la misma intersección
                        {
                            Debug.Log($"Nodo {node.name} está cerca de intersección {intersectionNode.originalName} (distancia: {distance})");
                            graphNode.isIntersection = true;
                            graphNode.nodeType = "intersection";
                            break;
                        }
                    }
                }
                
                // Actualizar información del nodo
                if (string.IsNullOrEmpty(graphNode.originalName))
                    graphNode.originalName = node.name;
                if (string.IsNullOrEmpty(graphNode.roadName))
                    graphNode.roadName = road.name;
                
                // IMPORTANTE: Asignar nombre especial si este nodo está en nuestro diccionario
                string nodeFullName = $"{node.name}({road.name})";
                if (specialNodeNames.ContainsKey(nodeFullName))
                {
                    graphNode.specialName = specialNodeNames[nodeFullName];
                    Debug.Log($"¡NODO ESPECIAL ASIGNADO! {nodeFullName} -> {graphNode.specialName}");
                }
                
                currentRoadNodes.Add(graphNode);
                
                // Crear indicador visual
                if (graphNode.isIntersection)
                {
                    CreateVisualIndicator(graphNode.position, intersectionIndicatorPrefab);
                }
                else
                {
                    CreateVisualIndicator(graphNode.position, nodeIndicatorPrefab);
                }
                
                // Conectar con el nodo anterior en la misma carretera
                if (previousNode != null && previousNode != graphNode)
                {
                    float distance = Vector3.Distance(previousNode.position, graphNode.position);
                    
                    // Verificar si la conexión ya existe antes de crearla
                    bool connectionExists = roadGraph.edges.Any(e => 
                        (e.startNodeId == previousNode.id && e.endNodeId == graphNode.id) ||
                        (e.startNodeId == graphNode.id && e.endNodeId == previousNode.id));
                    
                    if (!connectionExists)
                    {
                        GraphEdge edge1 = roadGraph.ConnectNodes(previousNode, graphNode, distance);
                        GraphEdge edge2 = roadGraph.ConnectNodes(graphNode, previousNode, distance);
                        
                        if (edge1 != null && edge2 != null)
                        {
                            Debug.Log($"Conectados {previousNode.originalName} ↔ {graphNode.originalName} (distancia: {distance})");
                        }
                    }
                }
                
                previousNode = graphNode;
            }
            
            roadNodes[road.name] = currentRoadNodes;
        }
        
        // ========== CONECTAR INTERSECCIONES CON CARRETERAS ==========
        int intersectionConnections = 0;
        foreach (var intersectionNode in intersectionNodes)
        {
            // Buscar nodos cercanos a esta intersección
            foreach (var roadNode in roadGraph.nodes)
            {
                if (roadNode == intersectionNode) continue;
                
                float distance = Vector3.Distance(intersectionNode.position, roadNode.position);
                
                // Conectar si están suficientemente cerca
                if (distance < 10f)
                {
                    bool alreadyConnected = roadGraph.edges.Any(e => 
                        (e.startNodeId == intersectionNode.id && e.endNodeId == roadNode.id) ||
                        (e.startNodeId == roadNode.id && e.endNodeId == intersectionNode.id));
                    
                    if (!alreadyConnected)
                    {
                        roadGraph.ConnectNodes(intersectionNode, roadNode, distance);
                        roadGraph.ConnectNodes(roadNode, intersectionNode, distance);
                        intersectionConnections++;
                        
                        Debug.Log($"Conectada intersección {intersectionNode.originalName} ↔ {roadNode.originalName} (distancia: {distance})");
                    }
                }
            }
        }
        
        // ========== CONEXIONES FINALES ==========
        roadGraph.CleanInvalidEdges();
        roadGraph.RebuildAllConnections();
        
        // Verificar consistencia
        int isolatedNodes = 0;
        foreach (var node in roadGraph.nodes)
        {
            if (node.edges.Count == 0)
            {
                isolatedNodes++;
                Debug.LogWarning($"Nodo aislado: {node.id} ({node.originalName}) en posición {node.position}");
            }
        }
        
        // Mostrar información de nodos especiales
        ShowSpecialNodesInfo();
        
        // ========== RESUMEN ==========
        Debug.Log("=== RESUMEN DE CONSTRUCCIÓN DEL GRAFO ===");
        Debug.Log($"Carreteras procesadas: {roads.Count}");
        Debug.Log($"Intersecciones procesadas: {intersectionNodes.Count}");
        Debug.Log($"Nodos totales: {roadGraph.nodes.Count}");
        Debug.Log($"Nodos de intersección: {roadGraph.nodes.Count(n => n.isIntersection)}");
        Debug.Log($"Aristas válidas: {roadGraph.edges.Count}");
        Debug.Log($"Conexiones de intersecciones: {intersectionConnections}");
        Debug.Log($"Nodos aislados: {isolatedNodes}");
        
        if (isolatedNodes > 0)
        {
            Debug.LogWarning($"Hay {isolatedNodes} nodos aislados. Considera verificar las distancias de conexión.");
        }
    }
    
    // Método para mostrar información de nodos especiales
    void ShowSpecialNodesInfo()
    {
        int specialNodesCount = 0;
        Debug.Log("=== NODOS ESPECIALES ENCONTRADOS ===");
        
        foreach (var node in roadGraph.nodes)
        {
            if (!string.IsNullOrEmpty(node.specialName))
            {
                specialNodesCount++;
                Debug.Log($"{specialNodesCount}. {node.specialName} (Original: {node.originalName}, Road: {node.roadName})");
            }
        }
        
        if (specialNodesCount == 0)
        {
            Debug.LogWarning("No se encontraron nodos con nombres especiales.");
        }
        else
        {
            Debug.Log($"Total nodos especiales: {specialNodesCount}");
        }
        
        // Mostrar separadamente nodos de spawn y destino
        var spawnNodes = GetSpawnNodesByType("spawn");
        var destinoNodes = GetSpawnNodesByType("destino");
        
        Debug.Log($"Nodos de SPAWN encontrados: {spawnNodes.Count}");
        foreach (var node in spawnNodes)
        {
            Debug.Log($"  - {node.specialName} en {node.position}");
        }
        
        Debug.Log($"Nodos de DESTINO encontrados: {destinoNodes.Count}");
        foreach (var node in destinoNodes)
        {
            Debug.Log($"  - {node.specialName} en {node.position}");
        }
    }
    
    // ========== MÉTODOS AUXILIARES ==========
    
    private GameObject FindGameObjectRecursive(string name, Transform parent)
    {
        if (parent == null)
        {
            GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (GameObject rootObject in rootObjects)
            {
                GameObject found = FindGameObjectRecursive(name, rootObject.transform);
                if (found != null) return found;
            }
            return null;
        }
        
        if (parent.name == name)
        {
            return parent.gameObject;
        }
        
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            GameObject found = FindGameObjectRecursive(name, child);
            if (found != null) return found;
        }
        
        return null;
    }
    
    private string GetFullPath(Transform tr)
    {
        if (tr.parent == null)
            return tr.name;
        return GetFullPath(tr.parent) + "/" + tr.name;
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
        
        roadGraph.RebuildAllConnections();
        
        foreach (GraphNode node in roadGraph.nodes)
        {
            // Nodos con nombre especial en AMARILLO
            if (!string.IsNullOrEmpty(node.specialName))
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(node.position, node.isIntersection ? 0.8f : 0.6f);
                
                #if UNITY_EDITOR
                // Mostrar el nombre especial
                UnityEditor.Handles.Label(node.position + Vector3.up * 1.5f, 
                    $"<color=yellow>{node.specialName}</color>\n<color=white>{node.originalName}</color>");
                #endif
            }
            // Intersecciones en ROJO
            else if (node.isIntersection)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(node.position, 0.7f);
                
                #if UNITY_EDITOR
                UnityEditor.Handles.Label(node.position + Vector3.up, 
                    $"<color=red>{node.originalName}</color>\n({node.roadName})");
                #endif
            }
            // Nodos normales en AZUL
            else
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawSphere(node.position, 0.5f);
                
                #if UNITY_EDITOR
                UnityEditor.Handles.Label(node.position + Vector3.up, 
                    $"{node.originalName}\n({node.roadName})");
                #endif
            }
        }
        
        Gizmos.color = Color.green;
        foreach (GraphEdge edge in roadGraph.edges)
        {
            if (edge.startNode == null || edge.endNode == null)
                edge.RebuildConnections(roadGraph);
                
            Gizmos.DrawLine(edge.startNode.position, edge.endNode.position);
            
            Vector3 direction = (edge.endNode.position - edge.startNode.position).normalized;
            Vector3 perpendicular = Vector3.Cross(direction, Vector3.up).normalized * 0.3f;
            Vector3 arrowStart = edge.startNode.position + (edge.endNode.position - edge.startNode.position) * 0.7f;
            
            Gizmos.DrawLine(arrowStart, arrowStart + direction * 1f - perpendicular * 0.5f);
            Gizmos.DrawLine(arrowStart, arrowStart + direction * 1f + perpendicular * 0.5f);
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
        
        int isolatedNodes = 0;
        foreach (var node in roadGraph.nodes)
        {
            if (node.edges.Count == 0)
            {
                isolatedNodes++;
                Debug.LogWarning($"Nodo aislado: {node.id} ({node.originalName}) en posición {node.position}");
            }
        }
        
        Debug.Log($"Nodos aislados: {isolatedNodes}");
        
        // Mostrar nodos especiales
        ShowSpecialNodesInfo();
    }
    
    [ContextMenu("Mostrar Nodos Especiales")]
    public void ShowSpecialNodes()
    {
        ShowSpecialNodesInfo();
    }
}