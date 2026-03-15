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

        [Header("Construcción del Grafo")]
        [Tooltip("Capa de la geometría de carretera para el raycast de subdivisión. Si es 'Nothing', se usa interpolación directa.")]
        public LayerMask roadLayerMask = 0;
        [Tooltip("Radio máximo (XZ) para conectar nodos de carretera a intersecciones")]
        public float intersectionConnectionRadius = 10f;
        [Tooltip("Diferencia vertical máxima permitida para conectar un nodo a una intersección (evita unir niveles distintos)")]
        public float maxVerticalConnectionDiff = 3f;
        [Tooltip("Roads que se excluyen completamente del grafo (ej: Road13 si tiene nodos fuera de la pista)")]
        public List<string> excludedRoads = new List<string>();
        [Tooltip("Roads que se excluyen solo de la subdivisión de aristas (permanecen en el grafo pero sin SubNodes intermedios)")]
        public List<string> excludedRoadsFromSubdivision = new List<string>();
        [Tooltip("Nodos específicos a excluir del grafo. Formato: 'RoadName/NodeName'  ej: 'Road13/Node0'")]
        public List<string> excludedSpecificNodes = new List<string>();
        [Tooltip("Multiplicadores de costo por road. Valores < 1 hacen que A* prefiera esa ruta (ej: 0.3 = muy preferida). Valores > 1 la evitan.")]
        public List<RoadCostMultiplier> roadCostMultipliers = new List<RoadCostMultiplier>();

        [Tooltip("Nodos de paso obligatorio para spawns específicos. Los vehículos se distribuyen entre la ruta normal y la ruta vía el nodo intermedio.")]
        public List<ViaNodeConfig> viaNodeConfigs = new List<ViaNodeConfig>();

        [System.Serializable]
        public class ViaNodeConfig
        {
            [Tooltip("Nombre especial del spawn, ej: Spawn_Road3_Node14")]
            public string spawnSpecialName;
            [Tooltip("Nombre especial del nodo intermedio obligatorio, ej: Destino_Road13_Node6")]
            public string viaNodeSpecialName;
            [Tooltip("Probabilidad de que este vehículo tome la ruta con via-nodo (0=nunca, 1=siempre, 0.5=mitad)")]
            [Range(0f, 1f)]
            public float chance = 0.5f;
        }

        // Devuelve el via-node para un spawn dado, o null si no aplica (por probabilidad o configuración)
        public GraphNode GetViaNode(string spawnSpecialName)
        {
            if (viaNodeConfigs == null) return null;
            foreach (var cfg in viaNodeConfigs)
            {
                if (cfg.spawnSpecialName != spawnSpecialName) continue;
                if (Random.value > cfg.chance) return null;   // no le tocó esta vez
                // Buscar el nodo en el grafo por specialName o originalName
                var via = roadGraph.nodes.FirstOrDefault(n =>
                    n.specialName == cfg.viaNodeSpecialName ||
                    n.originalName == cfg.viaNodeSpecialName);
                if (via == null)
                    Debug.LogWarning($"[ViaNode] No se encontró el nodo '{cfg.viaNodeSpecialName}' en el grafo.");
                return via;
            }
            return null;
        }

        [System.Serializable]
        public class RoadCostMultiplier
        {
            [Tooltip("Nombre del road, ej: Road13")]
            public string roadName;
            [Tooltip("Multiplicador de costo. 0.3 = A* prefiere esta ruta. 2.0 = A* la evita.")]
            [Range(0.05f, 5f)]
            public float costMultiplier = 0.3f;
        }
        [Tooltip("Activa en Play Mode para ver todos los roadNames exactos en la consola")]
        public bool debugPrintRoadNames = false;

        [Tooltip("Conexiones manuales forzadas para cuando la detección automática falla")]
        public List<ManualIntersectionConnection> manualConnections = new List<ManualIntersectionConnection>();

        [System.Serializable]
        public class ManualIntersectionConnection
        {
            [Tooltip("Nombre del road, ej: Road13")]
            public string roadName;
            [Tooltip("Nombre original del nodo del road, ej: Node2")]
            public string nodeName;
            [Tooltip("Nombre de la intersección, ej: Inter21")]
            public string intersectionName;
        }
        
        private List<GameObject> visualIndicators = new List<GameObject>();
        

        private Dictionary<string, string> spawnToDestinoMapping = new Dictionary<string, string>
        {   
            { "Spawn_Road2_Node1",  "Destino_Road2_Node10" },
            { "Spawn_Road1_Node1",  "Destino_Road1_Node29" },
            { "Spawn_Road1_Node3",  "Destino_Road1_Node29" },
            { "Spawn_Road1_Node9",  "Destino_Road1_Node29" },
            { "Spawn_Road1_Node11", "Destino_Road1_Node29" },
            { "Spawn_Road1_Node18", "Destino_Road1_Node29" },
            { "Spawn_Road5_Node2",  "Destino_Road5_Node21" },
            { "Spawn_Road5_Node1",  "Destino_Road5_Node15" },
            { "Spawn_Road5_Node17", "Destino_Road5_Node21" },
            { "Spawn_Road5_Node20", "Destino_Road5_Node21" },
            { "Spawn_Road3_Node14", "Destino_Road3_Node22" },
            { "Spawn_Road3_Node15", "Destino_Road3_Node28" },
            { "Spawn_Road3_Node16", "Destino_Road3_Node28" },
            { "Spawn_Road3_Node17", "Destino_Road3_Node28" },
            { "Spawn_Road3_Node18", "Destino_Road3_Node28" },
            { "Spawn_Road8_Node5",  "Destino_Road8_Node11" },
            { "Spawn_Road8_Node7",  "Destino_Road8_Node11" },
            { "Spawn_Road8_Node13", "Destino_Road8_Node16" },
            { "Spawn_Road10_Node3", "Destino_Road10_Node8" },
            { "Spawn_Road7_Node2",  "Destino_Road7_Node4" },
            { "Spawn_Road7_Node5",  "Destino_Road7_Node7" },
            { "Spawn_Road3_Node2",  "Destino_Road3_Node9" },
           // { "Spawn_Road4_Node2",  "Destino_Road4_Node12" },
           // { "Spawn_Road4_Node3",  "Destino_Road4_Node12" },
           // { "Spawn_Road4_Node4",  "Destino_Road4_Node12" },
            { "Spawn_Road2_Node3",  "Destino_Road2_Node10" },
            { "Spawn_Road8_Node3",  "Destino_Road8_Node11" },
            {"Spawn_Road3_Node19" , "Destino_Road14_Node9" },
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
            { "Node5(Road8)", "Spawn_Road8_Node5" },
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
            { "Node19(Road3)", "Spawn_Road3_Node19" },
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
            { "Node6(Road13)", "Destino_Road13_Node6" },
            { "Node9(Road14)", "Destino_Road14_Node9" },
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

                // Saltar roads excluidas por el usuario
                if (excludedRoads.Contains(road.name))
                {
                    Debug.Log($"[GraphBuilder] Road excluida del grafo: {road.name}");
                    continue;
                }

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
                    // Saltar nodos específicos excluidos (formato "RoadName/NodeName")
                    string nodeKey = $"{road.name}/{node.name}";
                    if (excludedSpecificNodes.Contains(nodeKey))
                    {
                        Debug.Log($"[GraphBuilder] Nodo excluido: {nodeKey}");
                        continue;
                    }

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
                        float cost = dist * GetRoadCostMultiplier(road.name);

                        roadGraph.ConnectNodes(previousNode, graphNode, cost);
                        roadGraph.ConnectNodes(graphNode, previousNode, cost);
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

            // Conectar intersecciones que quedaron sin conexión a alguna carretera cercana
            EnsureIntersectionsConnected();

            // Aplicar conexiones manuales forzadas configuradas en el Inspector
            ApplyManualConnections();

            roadGraph.RebuildAllConnections();

            graphReady = true;

            DebugGraphInfo();

            AssignDestinosToSpawns();

            Debug.Log($"Grafo construido. Nodos: {roadGraph.nodes.Count}, Aristas: {roadGraph.edges.Count}");

            if (debugPrintRoadNames)
            {
                var groups = roadGraph.nodes.GroupBy(n => n.roadName).OrderBy(g => g.Key);
                Debug.Log("=== ROADNAMES EN EL GRAFO ===");
                foreach (var g in groups)
                {
                    var originals = g.Where(n => !n.originalName.StartsWith("SubNode_")).ToList();
                    var subNodes  = g.Where(n =>  n.originalName.StartsWith("SubNode_")).ToList();
                    string ejemplos = string.Join(", ", originals.Select(n => n.originalName).Take(5));
                    Debug.Log($"  roadName='{g.Key}'  originales={originals.Count}  SubNodes={subNodes.Count}  ej: [{ejemplos}]");
                }
                Debug.Log("=== FIN ROADNAMES ===");
            }
        }

        public void AssignDestinosToSpawns()
        {
            var spawnNodes = GetSpawnNodesByType("spawn");

            foreach (var spawn in spawnNodes)
            {
                if (string.IsNullOrEmpty(spawn.specialName)) continue;

                if (spawnToDestinoMapping.TryGetValue(spawn.specialName, out string destinoName))
                {
                    var destinoNode = roadGraph.nodes.FirstOrDefault(n => n.specialName == destinoName);
                    if (destinoNode != null)
                    {
                        spawn.assignedDestino = destinoNode;
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
            foreach (var inter in intersections)
            {
                // Filtrar por diferencia vertical: no conectar nodos en niveles distintos
                float verticalDiff = Mathf.Abs(roadNode.position.y - inter.position.y);
                if (verticalDiff > maxVerticalConnectionDiff) continue;

                float dist = Vector3.Distance(roadNode.position, inter.position);
                if (dist <= intersectionConnectionRadius)
                {
                    float cost = dist * GetRoadCostMultiplier(roadNode.roadName);
                    roadGraph.ConnectNodes(roadNode, inter, cost);
                    roadGraph.ConnectNodes(inter, roadNode, cost);
                }
            }
        }

        // Paso post-construcción: para cada intersección sin conexiones a alguna carretera,
        // conecta el nodo de carretera más cercano dentro del radio.
        // Usa distancia XZ (horizontal) para el radio de búsqueda, permitiendo nodos
        // ligeramente elevados (ej: Node0 de un road cuyo spline empieza un poco arriba).
        // El filtro vertical solo se aplica cuando la diferencia es muy grande (paso elevado real).
        void EnsureIntersectionsConnected()
        {
            var allRoadNodes = roadGraph.nodes
                .Where(n => n.roadName != "Intersection" && !n.originalName.StartsWith("SubNode_"))
                .ToList();

            var intersectionNodes = roadGraph.nodes
                .Where(n => n.isIntersection)
                .ToList();

            float searchRadius = intersectionConnectionRadius * 2f;
            // Para el paso de corrección se permite hasta el doble de la diferencia vertical,
            // para no bloquear roads cuyos nodos están ligeramente sobre la superficie.
            float verticalLimit = maxVerticalConnectionDiff * 2f;

            int extraConnections = 0;
            foreach (var inter in intersectionNodes)
            {
                // Recopilar qué roads ya están conectadas a esta intersección
                var connectedRoads = new HashSet<string>(
                    inter.edges.Select(e => e.endNode?.roadName).Where(r => r != null));

                // Buscar candidatos usando distancia horizontal (XZ) para el radio
                var candidates = new List<(GraphNode node, float dist)>();
                foreach (var n in allRoadNodes)
                {
                    if (connectedRoads.Contains(n.roadName)) continue;
                    float vertDiff = Mathf.Abs(n.position.y - inter.position.y);
                    if (vertDiff > verticalLimit) continue;                    // paso elevado real → ignorar
                    Vector2 interXZ = new Vector2(inter.position.x, inter.position.z);
                    Vector2 nodeXZ  = new Vector2(n.position.x,     n.position.z);
                    float horizDist = Vector2.Distance(interXZ, nodeXZ);
                    if (horizDist <= searchRadius)
                        candidates.Add((n, Vector3.Distance(n.position, inter.position)));
                }

                candidates.Sort((a, b) => a.dist.CompareTo(b.dist));

                // Conectar el nodo más cercano de cada road no conectada
                var roadsAdded = new HashSet<string>();
                foreach (var (node, dist) in candidates)
                {
                    if (roadsAdded.Contains(node.roadName)) continue;
                    roadGraph.ConnectNodes(node, inter, dist);
                    roadGraph.ConnectNodes(inter, node, dist);
                    roadsAdded.Add(node.roadName);
                    extraConnections++;
                    Debug.Log($"[IntersectionFix] {inter.originalName} ↔ {node.originalName}({node.roadName}) dist={dist:F1}");
                }
            }

            if (extraConnections > 0)
                Debug.Log($"[IntersectionFix] {extraConnections} conexiones adicionales creadas.");
        }

        // Aplica las conexiones manuales definidas en el Inspector
        void ApplyManualConnections()
        {
            if (manualConnections == null || manualConnections.Count == 0) return;

            foreach (var mc in manualConnections)
            {
                // Buscar el nodo del road
                var roadNode = roadGraph.nodes.FirstOrDefault(n =>
                    n.roadName == mc.roadName &&
                    (n.originalName == mc.nodeName || n.originalName == $"{mc.nodeName}({mc.roadName})"));

                // Buscar la intersección
                var interNode = roadGraph.nodes.FirstOrDefault(n =>
                    n.isIntersection &&
                    (n.originalName == mc.intersectionName || n.specialName == mc.intersectionName));

                if (roadNode == null)
                {
                    Debug.LogWarning($"[ManualConnection] No se encontró nodo '{mc.nodeName}' en road '{mc.roadName}'");
                    continue;
                }
                if (interNode == null)
                {
                    Debug.LogWarning($"[ManualConnection] No se encontró intersección '{mc.intersectionName}'");
                    continue;
                }

                float dist = Vector3.Distance(roadNode.position, interNode.position);
                roadGraph.ConnectNodes(roadNode, interNode, dist);
                roadGraph.ConnectNodes(interNode, roadNode, dist);
                Debug.Log($"[ManualConnection] ✓ {mc.nodeName}({mc.roadName}) ↔ {mc.intersectionName} dist={dist:F1}m");
            }
        }

        // Método para mostrar información de nodos especiales
        float GetRoadCostMultiplier(string roadName)
        {
            if (roadCostMultipliers == null) return 1f;
            foreach (var entry in roadCostMultipliers)
                if (entry.roadName == roadName) return entry.costMultiplier;
            return 1f;
        }

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

                // Saltar aristas donde cualquiera de los dos extremos pertenece a una road excluida
                string startRoad = edge.startNode.roadName;
                string endRoad   = edge.endNode.roadName;

                bool startExcluded = excludedRoads.Contains(startRoad) || excludedRoadsFromSubdivision.Contains(startRoad);
                bool endExcluded   = excludedRoads.Contains(endRoad)   || excludedRoadsFromSubdivision.Contains(endRoad);
                if (startExcluded || endExcluded)
                {
                    Debug.Log($"[Subdivision] Saltando arista excluida: {edge.startNode.originalName}({startRoad}) → {edge.endNode.originalName}({endRoad})");
                    continue;
                }

                float distance = Vector3.Distance(edge.startNode.position, edge.endNode.position);
                if (distance > maxSegmentLength)
                    edgesToSplit.Add((edge.startNode, edge.endNode));
            }

            Debug.Log($"[Subdivision] Aristas a subdividir: {edgesToSplit.Count}  |  Roads excluidas subdivision: [{string.Join(", ", excludedRoadsFromSubdivision)}]");

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

                    // Interpolación lineal entre los dos nodos de carretera
                    Vector3 newPos = Vector3.Lerp(start.position, end.position, t);

                    // Ajustar altura solo si hay una capa de carretera configurada
                    // Si roadLayerMask == 0 se usa la interpolación directa (evita que el
                    // raycast golpee terreno y coloque nodos fuera de la pista)
                    if (roadLayerMask != 0)
                    {
                        RaycastHit hit;
                        if (Physics.Raycast(newPos + Vector3.up * 10f, Vector3.down, out hit, 20f, roadLayerMask))
                            newPos = hit.point + Vector3.up * 0.1f;
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

        [ContextMenu("Debug: Listar RoadNames del Grafo")]
        public void DebugRoadNames()
        {
            var groups = roadGraph.nodes
                .GroupBy(n => n.roadName)
                .OrderBy(g => g.Key);

            Debug.Log("=== ROADNAMES EN EL GRAFO ===");
            foreach (var g in groups)
                Debug.Log($"  roadName='{g.Key}'  ({g.Count()} nodos)  ej: {g.First().originalName}");
            Debug.Log("=== FIN ===");
        }

        [ContextMenu("Debug: Conexiones de Intersecciones")]
        public void DebugIntersectionConnections()
        {
            var intersections = roadGraph.nodes.Where(n => n.isIntersection).ToList();
            Debug.Log($"=== CONEXIONES DE INTERSECCIONES ({intersections.Count}) ===");
            foreach (var inter in intersections)
            {
                var connectedRoads = inter.edges
                    .Where(e => e.endNode != null)
                    .Select(e => $"{e.endNode.originalName}({e.endNode.roadName})")
                    .Distinct()
                    .ToList();
                Debug.Log($"{inter.originalName} @ Y={inter.position.y:F1} → [{string.Join(", ", connectedRoads)}]");
            }
            Debug.Log("=== FIN ===");
        }
    }