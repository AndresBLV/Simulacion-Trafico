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
        public bool graphReady = false;
        
        private List<GameObject> visualIndicators = new List<GameObject>();
        

        private Dictionary<string, string> spawnToDestinoMapping = new Dictionary<string, string>
        {
            { "Spawn_Road2_Node1", "Destino_Road2_Node10" },
            { "Spawn_Road1_Node1", "Destino_Road1_Node29" },
            { "Spawn_Road1_Node3", "Destino_Road1_Node29" },
            { "Spawn_Road1_Node9", "Destino_Road1_Node29" },
            { "Spawn_Road1_Node11", "Destino_Road1_Node29" },
            { "Spawn_Road1_Node18", "Destino_Road1_Node29" },
            { "Spawn_Road5_Node2", "Destino_Road5_Node21" },
            { "Spawn_Road5_Node1", "Destino_Road5_Node15" },
            { "Spawn_Road5_Node17", "Destino_Road5_Node21" },
            { "Spawn_Road5_Node20", "Destino_Road5_Node21" },
            { "Spawn_Road3_Node14", "Destino_Road3_Node28" },
            { "Spawn_Road3_Node15", "Destino_Road3_Node28" },
            { "Spawn_Road3_Node16", "Destino_Road3_Node28" },
            { "Spawn_Road3_Node17", "Destino_Road3_Node28" },
            { "Spawn_Road3_Node18", "Destino_Road3_Node28" },
            { "Spawn_Road8_Node7", "Destino_Road8_Node11" },
            { "Spawn_Road8_Node13", "Destino_Road8_Node16" },
            { "Spawn_Road10_Node3", "Destino_Road10_Node8" },
            { "Spawn_Road7_Node2", "Destino_Road7_Node4" },
            { "Spawn_Road7_Node5", "Destino_Road7_Node7" },
            { "Spawn_Road3_Node2", "Destino_Road3_Node9" },
            { "Spawn_Road4_Node2", "Destino_Road4_Node12" },
            { "Spawn_Road4_Node3", "Destino_Road4_Node12" },
            { "Spawn_Road4_Node4", "Destino_Road4_Node12" },
            { "Spawn_Road2_Node3", "Destino_Road2_Node10" },
            { "Spawn_Road8_Node3", "Destino_Road8_Node11" },
            { "Spawn_Road11_Node3", "Destino_Road11_Node10" }
            // Agrega más según necesites
        };
                
        // Diccionario extendido de nombres especiales para TODOS los nodos importantes
        Dictionary<string, string> specialNodeNames = new Dictionary<string, string>
        {
            // Intersecciones
            { "Inter22", "Acceso Universidad Metropolitana (Terrazas del Ávila)" },
            { "Inter3", "Acceso Distribuidor Metropolitano (Autopista)" },

            // ===== SPAWN OFICIALES =====
            { "Node4(Road8)", "Spawn_Road8_Node4" },
            { "Node7(Road8)", "Spawn_Road8_Node7" },
            { "Node13(Road8)", "Spawn_Road8_Node13" },
            { "Node1(Road1)", "Spawn_Road1_Node1" },
            { "Node3(Road1)", "Spawn_Road1_Node3" },
            { "Node9(Road1)", "Spawn_Road1_Node9" },
            { "Node11(Road1)", "Spawn_Road1_Node11" },
            { "Node18(Road1)", "Spawn_Road1_Node18" },
            { "Node1(Road2)", "Spawn_Road2_Node1" },
            { "Node2(Road2)", "Spawn_Road2_Node2" },
            { "Node3(Road6)", "Spawn_Road6_Node3" },
            { "Node2(Road4)", "Spawn_Road4_Node2" },
            { "Node3(Road4)", "Spawn_Road4_Node3" },
            { "Node4(Road4)", "Spawn_Road4_Node4" },
            { "Node1(Road5)", "Spawn_Road5_Node1" },
            { "Node2(Road5)", "Spawn_Road5_Node2" },
            { "Node17(Road5)", "Spawn_Road5_Node17" },
            { "Node20(Road5)", "Spawn_Road5_Node20" },
            { "Node14(Road3)", "Spawn_Road3_Node14" },
            { "Node15(Road3)", "Spawn_Road3_Node15" },
            { "Node16(Road3)", "Spawn_Road3_Node16" },
            { "Node17(Road3)", "Spawn_Road3_Node17" },
            { "Node18(Road3)", "Spawn_Road3_Node18" },
            { "Node2(Road3)", "Spawn_Road3_Node2" },
            { "Node3(Road10)", "Spawn_Road10_Node3" },
            { "Node2(Road7)", "Spawn_Road7_Node2" },
            { "Node5(Road7)", "Spawn_Road7_Node5" },
            { "Node3(Road11)", "Spawn_Road11_Node3" },

            // ===== DESTINOS (si los necesitas) =====
            { "Node29(Road1)", "Destino_Road1_Node29" },
            { "Node21(Road5)", "Destino_Road5_Node21" },
            { "Node15(Road5)", "Destino_Road5_Node15" },
            { "Node10(Road2)", "Destino_Road2_Node10" },
            { "Node11(Road8)", "Destino_Road8_Node11" },
            { "Node16(Road8)", "Destino_Road8_Node16" },
            { "Node8(Road10)", "Destino_Road10_Node8" },
            { "Node12(Road4)", "Destino_Road4_Node12" },
            { "Node22(Road3)", "Destino_Road3_Node22" },
            { "Node9(Road3)", "Destino_Road3_Node9" },
            { "Node7(Road7)", "Destino_Road7_Node7" },
            { "Node10(Road11)", "Destino_Road11_Node10" },
            { "Node14(Road6)", "Destino_Road6_Node14" },
            { "Node4(Road7)", "Destino_Road7_Node4" },
            { "Node28(Road3)", "Destino_Road3_Node28" }
            
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
                StartCoroutine(BuildGraphCoroutine());
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
                StartCoroutine(BuildGraphFromRoadArchitectCoroutine(roadArchitectContainer));
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

        IEnumerator BuildGraphCoroutine()
        {
            GameObject roadArchitect = FindRoadArchitectSystem();
            if (roadArchitect == null)
            {
                Debug.LogWarning("Road Architect no encontrado.");
                yield break;
            }

            // Construir grafo
            StartCoroutine(BuildGraphFromRoadArchitectCoroutine(roadArchitect));

            // Espera un frame para que Unity pueda renderizar
            yield return null;

            // Subdividir aristas largas gradualmente
            yield return StartCoroutine(SubdivideLongEdgesCoroutine(20f));

            // Limpiar aristas inválidas y reconstruir conexiones
            roadGraph.CleanInvalidEdges();
            roadGraph.RebuildAllConnections();

            // Mostrar nodos especiales
            ShowSpecialNodesInfo();

            Debug.Log($"Grafo inicializado: {roadGraph.nodes.Count} nodos, {roadGraph.edges.Count} aristas válidas");
        }
        
        public IEnumerator BuildGraphFromRoadArchitectCoroutine(GameObject roadArchitectContainer)
        {
            graphReady = false;
            roadGraph = new RoadGraph();

            ClearVisualIndicators();

            if (roadArchitectContainer == null)
            {
                Debug.LogError("El contenedor de Road Architect es nulo");
                yield break;
            }

            Transform roadArchitectSystem = roadArchitectContainer.transform;
            Dictionary<Vector3Int, GraphNode> positionToNode = new Dictionary<Vector3Int, GraphNode>();

            Vector3Int RoundPosition(Vector3 pos, float precision = 0.5f)
            {
                return new Vector3Int(
                    Mathf.RoundToInt(pos.x / precision),
                    Mathf.RoundToInt(pos.y / precision),
                    Mathf.RoundToInt(pos.z / precision)
                );
            }

            // =========================
            // 1️⃣ CREAR INTERSECCIONES
            // =========================

            Transform intersectionsFolder = roadArchitectSystem.Find("Intersections");
            List<GraphNode> intersectionNodes = new List<GraphNode>();

            if (intersectionsFolder != null)
            {
                foreach (Transform intersection in intersectionsFolder)
                {
                    if (!intersection.name.StartsWith("Inter")) continue;

                    Vector3Int roundedPos = RoundPosition(intersection.position, 1f);

                    if (!positionToNode.ContainsKey(roundedPos))
                    {
                        GraphNode intersectionNode =
                            roadGraph.AddNode(intersection.position, intersection.name, "Intersection");

                        intersectionNode.isIntersection = true;
                        intersectionNode.nodeType = "intersection";

                        if (specialNodeNames.ContainsKey(intersection.name))
                        {
                            intersectionNode.specialName = specialNodeNames[intersection.name];
                            intersectionNode.displayName = specialNodeNames[intersection.name];
                        }

                        intersectionNodes.Add(intersectionNode);
                        positionToNode[roundedPos] = intersectionNode;

                        CreateVisualIndicator(intersectionNode.position, intersectionIndicatorPrefab);
                    }
                }
            }

            // =========================
            // 2️⃣ CREAR CARRETERAS
            // =========================

            foreach (Transform road in roadArchitectSystem)
            {
                if (!road.name.StartsWith("Road")) continue;

                Transform spline = road.Find("Spline");
                if (spline == null) continue;

                List<Transform> splineNodes = new List<Transform>();

                List<GraphNode> createdRoadNodes = new List<GraphNode>();

                foreach (Transform child in spline)
                {
                    if (child.name.StartsWith("Node") && child.parent == spline)
                        splineNodes.Add(child);
                }

                if (splineNodes.Count == 0) continue;

                GraphNode previousNode = null;

                foreach (Transform node in splineNodes)
                {
                    Vector3Int roundedPos = RoundPosition(node.position, 0.5f);

                    if (!positionToNode.TryGetValue(roundedPos, out GraphNode graphNode))
                    {
                        graphNode = roadGraph.AddNode(node.position, node.name, road.name);
                        string nodeFullName = $"{node.name}({road.name})";

                        if (specialNodeNames.ContainsKey(nodeFullName))
                        {
                            graphNode.specialName = specialNodeNames[nodeFullName];
                            graphNode.displayName = specialNodeNames[nodeFullName];
                        }
                        positionToNode[roundedPos] = graphNode;
                    }

                    createdRoadNodes.Add(graphNode);

                    CreateVisualIndicator(graphNode.position, nodeIndicatorPrefab);

                    // 🔹 Conexión SECUENCIAL dentro de la misma carretera
                    if (previousNode != null)
                    {
                        float dist = Vector3.Distance(previousNode.position, graphNode.position);

                        roadGraph.ConnectNodes(previousNode, graphNode, dist);
                        roadGraph.ConnectNodes(graphNode, previousNode, dist);
                    }

                    previousNode = graphNode;

                    yield return null;
                    
                }
                foreach (var roadNode in createdRoadNodes)
                {
                    ConnectIfCloseToIntersection(roadNode, intersectionNodes);
                }
            }

            // =========================
            // FINALIZAR
            // =========================

            roadGraph.RebuildAllConnections();

            graphReady = true;

            DebugGraphInfo();

            AssignDestinosToSpawns();

            Debug.Log($"Grafo construido. Nodos: {roadGraph.nodes.Count}, Aristas: {roadGraph.edges.Count}");
        }

        public void AssignDestinosToSpawns()
        {
            var spawnNodes = GetSpawnNodesByType("spawn"); // todos los nodos de spawn

            foreach (var spawn in spawnNodes)
            {
                if (string.IsNullOrEmpty(spawn.specialName)) continue;

                if (spawnToDestinoMapping.TryGetValue(spawn.specialName, out string destinoName))
                {
                    var destinoNode = roadGraph.nodes.FirstOrDefault(n => n.specialName == destinoName);
                    if (destinoNode != null)
                    {
                        spawn.assignedDestino = destinoNode; // asigna el destino
                        Debug.Log($"Spawn {spawn.specialName} -> Destino {destinoNode.specialName}");
                    }
                    else
                    {
                        Debug.LogWarning($"No se encontró el nodo destino {destinoName} para el spawn {spawn.specialName}");
                    }
                }
                else
                {
                    Debug.LogWarning($"No hay destino definido para el spawn {spawn.specialName}");
                }
            }
        }

        void ConnectIfCloseToIntersection(GraphNode roadNode, List<GraphNode> intersections)
        {
            float connectionThreshold = 5f; // ajusta si es necesario

            foreach (var inter in intersections)
            {
                float dist = Vector3.Distance(roadNode.position, inter.position);

                if (dist <= connectionThreshold)
                {
                    roadGraph.ConnectNodes(roadNode, inter, dist);
                    roadGraph.ConnectNodes(inter, roadNode, dist);
                }
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
        public void StartSubdivideLongEdges(float maxSegmentLength = 5f)
        {
            StartCoroutine(SubdivideLongEdgesCoroutine(maxSegmentLength));
        }

        // Subdivide aristas largas en nodos intermedios
        public IEnumerator SubdivideLongEdgesCoroutine(float maxSegmentLength = 5f)
        {
            List<(GraphNode start, GraphNode end)> edgesToSplit = new List<(GraphNode, GraphNode)>();

            // 1️⃣ Identificar aristas largas
            foreach (var edge in roadGraph.edges)
            {
                if (edge.startNode == null || edge.endNode == null) continue;

                float distance = Vector3.Distance(edge.startNode.position, edge.endNode.position);
                if (distance > maxSegmentLength)
                    edgesToSplit.Add((edge.startNode, edge.endNode));
            }

            // 2️⃣ Subdividir cada arista
            foreach (var (start, end) in edgesToSplit)
            {
                float distance = Vector3.Distance(start.position, end.position);
                int segments = Mathf.CeilToInt(distance / maxSegmentLength);
                if (segments <= 1) continue;

                // Eliminar la arista original
                roadGraph.RemoveEdge(start, end);

                GraphNode prevNode = start;

                for (int i = 1; i < segments; i++) // excluimos el nodo final
                {
                    float t = (float)i / segments;

                    // Interpolación lineal
                    Vector3 newPos = Vector3.Lerp(start.position, end.position, t);

                    // 🔹 Ajustar nodo sobre la carretera usando raycast hacia abajo
                    RaycastHit hit;
                    if (Physics.Raycast(newPos + Vector3.up * 10f, Vector3.down, out hit, 20f))
                    {
                        newPos = hit.point + Vector3.up * 0.1f; // pequeño offset para evitar colisiones
                    }

                    GraphNode newNode = roadGraph.AddNode(newPos, $"SubNode_{start.id}_{end.id}_{i}", start.roadName);

                    // Conectar con el nodo anterior
                    roadGraph.ConnectNodes(prevNode, newNode, Vector3.Distance(prevNode.position, newNode.position));
                    roadGraph.ConnectNodes(newNode, prevNode, Vector3.Distance(prevNode.position, newNode.position));

                    prevNode = newNode;

                    if (i % 5 == 0) // dar un frame cada 5 nodos para no bloquear Unity
                        yield return null;
                }

                // Conectar con el nodo final
                roadGraph.ConnectNodes(prevNode, end, Vector3.Distance(prevNode.position, end.position));
                roadGraph.ConnectNodes(end, prevNode, Vector3.Distance(prevNode.position, end.position));
            }

            roadGraph.RebuildAllConnections();
            Debug.Log("Subdivisión de aristas largas completada (nodos ajustados sobre carretera).");
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

            foreach (GraphNode node in roadGraph.nodes)
            {
                // Nodos con nombre especial en AMARILLO
                if (!string.IsNullOrEmpty(node.specialName))
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawSphere(node.position, node.isIntersection ? 0.8f : 0.6f);

        #if UNITY_EDITOR
                    // Mostrar el nombre especial
                    UnityEditor.Handles.Label(
                        node.position + Vector3.up * 1.5f,
                        $"<color=yellow>{node.specialName}</color>\n<color=white>{node.originalName}</color>"
                    );
        #endif
                }
                // Intersecciones en ROJO
                else if (node.isIntersection)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawSphere(node.position, 0.7f);

        #if UNITY_EDITOR
                    UnityEditor.Handles.Label(
                        node.position + Vector3.up,
                        $"<color=red>{node.originalName}</color>\n({node.roadName})"
                    );
        #endif
                }
                // Nodos normales en AZUL
                else
                {
                    Gizmos.color = Color.blue;
                    Gizmos.DrawSphere(node.position, 0.5f);

        #if UNITY_EDITOR
                    UnityEditor.Handles.Label(
                        node.position + Vector3.up,
                        $"{node.originalName}\n({node.roadName})"
                    );
        #endif
                }
            }

            // Dibujar aristas en VERDE
            Gizmos.color = Color.green;
            foreach (GraphEdge edge in roadGraph.edges)
            {
                if (edge.startNode == null || edge.endNode == null)
                {
                    // Intentar reconstruir conexiones si están rotas
                    edge.RebuildConnections(roadGraph);
                    if (edge.startNode == null || edge.endNode == null) continue;
                }

                Gizmos.DrawLine(edge.startNode.position, edge.endNode.position);

                // Flecha de dirección
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