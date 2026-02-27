using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TrafficManager : MonoBehaviour
{
    [Header("Configuración de NPCs")]
    public RoadGraphSystem roadGraphSystem;
    public GameObject npcVehiclePrefab;
    public int maxNPCVehicles = 20;
    public float spawnInterval = 3f;
    public float minNPCSpeed = 5f;
    public float maxNPCSpeed = 15f;
    public float trafficUpdateInterval = 2f;
    
    [Header("Configuración de Despawn")]
    public float despawnDelay = 2f;
    public bool respawnAfterDespawn = true; // Si se debe respawnear después de llegar al destino
    
    [Header("Referencias")]
    public Transform playerTransform;
    
    [Header("Debug")]
    public bool debugMode = true;
    public bool showSpawnGizmos = true;
    public bool showTargetGizmos = true;
    
    // Listas de nodos
    private List<GraphNode> spawnNodes = new List<GraphNode>();
    [System.Serializable]
    public class WeightedSpawn
    {
        public GraphNode node;
        public float weight;
    }

    private List<WeightedSpawn> weightedSpawns = new List<WeightedSpawn>();
    private List<GraphNode> targetNodes = new List<GraphNode>();
    
    private List<NPCAgent> activeNPCs = new List<NPCAgent>();
    private float spawnTimer = 0f;
    private float trafficUpdateTimer = 0f;
    private bool isInitialized = false;
    
    IEnumerator Start()
    {
        Debug.Log("TrafficManager - Esperando inicialización del grafo...");

        if (roadGraphSystem == null)
            roadGraphSystem = FindFirstObjectByType<RoadGraphSystem>();

        if (roadGraphSystem == null)
        {
            Debug.LogError("No se encontró RoadGraphSystem");
            yield break;
        }

        // Si el grafo aún no está listo, construirlo
        if (!roadGraphSystem.graphReady)
        {
            roadGraphSystem.BuildGraphAutomatically();
        }

        // 🔴 ESPERAR HASTA QUE EL GRAFO ESTÉ LISTO
        while (!roadGraphSystem.graphReady)
        {
            yield return null;
        }

        Debug.Log("TrafficManager - Grafo listo.");

        roadGraphSystem.roadGraph.RebuildAllConnections();

        GetNodesBySpecialNames();

        ConfigureSpawnWeights();

        if (spawnNodes.Count == 0)
        {
            Debug.LogError("❌ No se encontraron nodos de spawn.");
            yield break;
        }

        if (targetNodes.Count == 0)
        {
            Debug.LogError("❌ No se encontraron nodos destino.");
            yield break;
        }

        isInitialized = true;

        Debug.Log($"TrafficManager inicializado correctamente. Spawn: {spawnNodes.Count} Destino: {targetNodes.Count}");

        StartCoroutine(InitialSpawn());
    }

    void ConfigureSpawnWeights()
    {
        weightedSpawns.Clear();

        foreach (var node in spawnNodes)
        {
            float weight = 1f;

            // 🔥 MÁS TRÁFICO EN ROAD3
            if (node.specialName.Contains("Road3"))
            {
                weight = 0.5f; // puedes cambiar a 2f si quieres menos
            }

            weightedSpawns.Add(new WeightedSpawn
            {
                node = node,
                weight = weight
            });

            Debug.Log($"Spawn configurado: {node.specialName} - Peso: {weight}");
        }
    }

    GraphNode GetWeightedRandomSpawn()
    {
        float totalWeight = weightedSpawns.Sum(s => s.weight);
        float randomPoint = Random.Range(0f, totalWeight);

        float cumulative = 0f;

        foreach (var spawn in weightedSpawns)
        {
            cumulative += spawn.weight;
            if (randomPoint <= cumulative)
                return spawn.node;
        }

        return weightedSpawns[0].node; // fallback
    }
    
    IEnumerator InitialSpawn()
    {
        yield return new WaitForSeconds(1f);
        
        int initialCount = Mathf.Min(3, maxNPCVehicles);
        Debug.Log($"Iniciando spawn de {initialCount} NPCs iniciales...");
        
        for (int i = 0; i < initialCount; i++)
        {
            SpawnNPCVehicle();
            yield return new WaitForSeconds(0.5f);
        }
    }
    
    void GetNodesBySpecialNames()
    {
        spawnNodes.Clear();
        targetNodes.Clear();
        
        Debug.Log("Obteniendo nodos por nombres especiales...");
        
        // Recorrer todos los nodos del grafo
        foreach (var node in roadGraphSystem.roadGraph.nodes)
        {
            if (!string.IsNullOrEmpty(node.specialName))
            {
                // Nodos de spawn (que empiezan con "Spawn_")
                if (node.specialName.StartsWith("Spawn_"))
                {
                    spawnNodes.Add(node);
                    Debug.Log($"Nodo de spawn encontrado: {node.specialName} (Original: {node.originalName})");
                }
                // Nodos destino (que empiezan con "Destino_")
                else if (node.specialName.StartsWith("Destino_"))
                {
                    targetNodes.Add(node);
                    Debug.Log($"Nodo destino encontrado: {node.specialName} (Original: {node.originalName})");
                }
            }
        }
        
        Debug.Log($"Encontrados {spawnNodes.Count} nodos de spawn y {targetNodes.Count} nodos destino por nombres especiales");
    }
    
    void FindSpawnNodesAlternative()
    {
        Debug.Log("Buscando nodos de spawn alternativos...");
        
        // Buscar nodos que probablemente sean de spawn (primeros nodos de cada road)
        Dictionary<string, List<GraphNode>> nodesByRoad = new Dictionary<string, List<GraphNode>>();
        
        // Agrupar nodos por carretera
        foreach (var node in roadGraphSystem.roadGraph.nodes)
        {
            if (!string.IsNullOrEmpty(node.roadName))
            {
                if (!nodesByRoad.ContainsKey(node.roadName))
                {
                    nodesByRoad[node.roadName] = new List<GraphNode>();
                }
                nodesByRoad[node.roadName].Add(node);
            }
        }
        
        // Tomar los primeros 3 nodos de cada carretera como spawn
        foreach (var kvp in nodesByRoad)
        {
            var roadName = kvp.Key;
            var nodes = kvp.Value;
            
            // Ordenar nodos por nombre (asumiendo que Node1, Node2, etc.)
            var sortedNodes = nodes.OrderBy(n => {
                // Extraer número del nombre
                var match = System.Text.RegularExpressions.Regex.Match(n.originalName, @"\d+");
                if (match.Success && int.TryParse(match.Value, out int num))
                {
                    return num;
                }
                return int.MaxValue;
            }).ToList();
            
            // Añadir primeros 3 nodos como spawn
            int countToAdd = Mathf.Min(3, sortedNodes.Count);
            for (int i = 0; i < countToAdd; i++)
            {
                if (!spawnNodes.Contains(sortedNodes[i]))
                {
                    spawnNodes.Add(sortedNodes[i]);
                    Debug.Log($"Nodo de spawn alternativo: {sortedNodes[i].originalName} en {roadName}");
                }
            }
        }
    }
    
    void FindTargetNodesAlternative()
    {
        Debug.Log("Buscando nodos destino alternativos...");
        
        // Buscar nodos con números altos (posiblemente finales de carretera)
        List<GraphNode> potentialTargets = new List<GraphNode>();
        
        foreach (var node in roadGraphSystem.roadGraph.nodes)
        {
            // Buscar nodos que contengan "29" o "21" en el nombre
            if (node.originalName.Contains("29") || node.originalName.Contains("21"))
            {
                potentialTargets.Add(node);
            }
        }
        
        // Si no encontramos por números específicos, buscar los últimos nodos de cada carretera
        if (potentialTargets.Count == 0)
        {
            Dictionary<string, List<GraphNode>> nodesByRoad = new Dictionary<string, List<GraphNode>>();
            
            foreach (var node in roadGraphSystem.roadGraph.nodes)
            {
                if (!string.IsNullOrEmpty(node.roadName))
                {
                    if (!nodesByRoad.ContainsKey(node.roadName))
                    {
                        nodesByRoad[node.roadName] = new List<GraphNode>();
                    }
                    nodesByRoad[node.roadName].Add(node);
                }
            }
            
            foreach (var kvp in nodesByRoad)
            {
                var nodes = kvp.Value;
                
                // Ordenar por nombre y tomar el último
                var sortedNodes = nodes.OrderBy(n => {
                    var match = System.Text.RegularExpressions.Regex.Match(n.originalName, @"\d+");
                    if (match.Success && int.TryParse(match.Value, out int num))
                    {
                        return num;
                    }
                    return int.MaxValue;
                }).ToList();
                
                if (sortedNodes.Count > 0)
                {
                    GraphNode lastNode = sortedNodes[sortedNodes.Count - 1];
                    if (!targetNodes.Contains(lastNode))
                    {
                        targetNodes.Add(lastNode);
                        Debug.Log($"Nodo destino alternativo (último nodo): {lastNode.originalName} en {kvp.Key}");
                    }
                }
            }
        }
        else
        {
            targetNodes.AddRange(potentialTargets);
        }
    }
    
    void CreateEmergencyPrefab()
    {
        Debug.LogWarning("TrafficManager: Creando prefab de emergencia...");
        
        GameObject emergencyPrefab = new GameObject("Emergency_NPC");
        
        // Componentes esenciales
        NPCAgent agent = emergencyPrefab.AddComponent<NPCAgent>();
        Rigidbody rb = emergencyPrefab.AddComponent<Rigidbody>();
        rb.mass = 1500f;
        rb.linearDamping = 0.1f;
        rb.angularDamping = 0.5f;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        
        BoxCollider collider = emergencyPrefab.AddComponent<BoxCollider>();
        collider.size = new Vector3(2f, 1f, 4f);
        
        // Modelo visual
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Vehicle_Visual";
        visual.transform.SetParent(emergencyPrefab.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = new Vector3(1.8f, 0.9f, 3.6f);
        
        // Color aleatorio
        Renderer renderer = visual.GetComponent<Renderer>();
        renderer.material = new Material(Shader.Find("Standard"));
        renderer.material.color = new Color(
            Random.Range(0.3f, 0.8f),
            Random.Range(0.3f, 0.8f),
            Random.Range(0.3f, 0.8f)
        );
        
        Destroy(visual.GetComponent<BoxCollider>());
        
        npcVehiclePrefab = emergencyPrefab;
        Debug.Log("Prefab de emergencia creado");
    }
    
    void Update()
    {
        if (!isInitialized) return;
        
        // DEBUG: Mostrar estado ocasionalmente
        if (debugMode && Time.frameCount % 600 == 0) // Cada 10 segundos (60 fps * 10)
        {
            Debug.Log($"TrafficManager Update - NPCs activos: {activeNPCs.Count}/{maxNPCVehicles}, " +
                     $"Timer spawn: {spawnTimer:F1}/{spawnInterval}");
        }
        
        // Temporizador para spawn de NPCs
        spawnTimer += Time.deltaTime;
        
        // Spawn por intervalo
        if (spawnTimer >= spawnInterval && activeNPCs.Count < maxNPCVehicles)
        {
            SpawnNPCVehicle();
            spawnTimer = 0f;
        }
        
        // Temporizador para actualizar costos de tráfico
        trafficUpdateTimer += Time.deltaTime;
        if (trafficUpdateTimer >= trafficUpdateInterval)
        {
            //UpdateEdgeCostsBasedOnTraffic();
            trafficUpdateTimer = 0f;
        }
        
        // Verificar NPCs para despawn
        CheckForDespawn();
        
        // Limpiar NPCs lejanos o eliminados
        CleanupNPCs();
    }
    
    void SpawnNPCVehicle()
    {
        if (roadGraphSystem == null || roadGraphSystem.roadGraph == null || roadGraphSystem.roadGraph.nodes.Count == 0) 
        {
            Debug.LogWarning("TrafficManager: No hay nodos suficientes para generar NPC");
            return;
        }
        
        if (spawnNodes.Count == 0)
        {
            Debug.LogWarning("TrafficManager: No hay nodos de spawn configurados");
            return;
        }
        
        if (targetNodes.Count == 0)
        {
            Debug.LogWarning("TrafficManager: No hay nodos destino configurados");
            return;
        }
        
        // Seleccionar nodo de spawn aleatorio
        GraphNode spawnNode = GetWeightedRandomSpawn();
        GraphNode targetNode = GetValidConnectedTarget(spawnNode);

        if (targetNode == null)
        {
            Debug.LogWarning("No hay destino válido para este spawn. Cancelando spawn.");
            return;
        }

        // Evitar que sea el mismo nodo
        int attempts = 0;
        while (spawnNode == targetNode && attempts < 5)
        {
            targetNode = targetNodes[Random.Range(0, targetNodes.Count)];
            targetNode = roadGraphSystem.roadGraph.FindClosestConnectedNode(targetNode) ?? targetNode;
            attempts++;
        }
        
        // Calcular posición de spawn
        Vector3 spawnPos = spawnNode.position;
        
        // Ajustar altura para asegurar que esté sobre el suelo
        RaycastHit hit;
        if (Physics.Raycast(spawnPos + Vector3.up * 50, Vector3.down, out hit, 100))
        {
            spawnPos = hit.point + Vector3.up * 0.5f;
        }
        else
        {
            spawnPos.y = 1f; // Altura por defecto
        }
        
        // Instanciar NPC
        GameObject npcObj = Instantiate(npcVehiclePrefab, spawnPos, Quaternion.identity, transform);
        npcObj.name = $"NPC_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
        
        // Configurar NPCAgent
        NPCAgent npcAgent = npcObj.GetComponent<NPCAgent>();
        if (npcAgent != null)
        {
            // Configurar NPC
            npcAgent.roadGraphSystem = roadGraphSystem;
            npcAgent.startNode = spawnNode;
            npcAgent.targetNode = targetNode;
            float fixedSpeed = 30f; // velocidad fija para todos los NPCs
            npcAgent.speed = fixedSpeed;
            npcAgent.showPath = true;
            npcAgent.minDistanceToNode = 1.5f;
            
            // Añadir componente de tracker de destino
            NPCDestinationTracker tracker = npcObj.AddComponent<NPCDestinationTracker>();
            tracker.Initialize(this, npcAgent, targetNode, despawnDelay);
            
            activeNPCs.Add(npcAgent);
            
            // Inicializar NPC después de un frame
            StartCoroutine(InitializeNPCDelayed(npcAgent));
            
            Debug.Log($"TrafficManager: NPC generado - " +
                     $"Desde: {(spawnNode.specialName ?? spawnNode.originalName)} " +
                     $"Hacia: {(targetNode.specialName ?? targetNode.originalName)} " +
                     $"Velocidad: {npcAgent.speed:F1}");
        }
        else
        {
            Debug.LogError("TrafficManager: Error al obtener componente NPCAgent");
            Destroy(npcObj);
        }
    }

    GraphNode GetValidConnectedTarget(GraphNode spawnNode)
    {
        foreach (var candidate in targetNodes.OrderBy(x => Random.value))
        {
            var path = roadGraphSystem.roadGraph.FindPath(spawnNode, candidate);
            if (path != null && path.Count > 0)
            {
                return candidate;
            }
        }

        Debug.LogWarning($"No se encontró destino conectado para {spawnNode.originalName}");
        return null;
    }

    
   IEnumerator InitializeNPCDelayed(NPCAgent agent)
    {
        // Esperar un frame para que Unity inicialice el GameObject
        yield return null;

        // ⚡ Protección: si el NPC ya fue destruido, salir
        if (agent == null) yield break;

        // 🔹 CAMBIO: Asegurar conexiones antes de calcular ruta


        agent.CalculatePathToTarget();

        // ⚡ Protección: validar de nuevo después del cálculo
        if (agent == null) yield break;

        // Si no hay ruta válida, intentar reintento
        if (agent.currentPath == null || agent.currentPath.Count == 0)
        {
            Debug.LogWarning($"NPC {agent.name} no pudo calcular ruta inicial. Reintentando...");
            yield return new WaitForSeconds(0.5f);

            if (agent == null) yield break; // ⚡ Validación antes de reintento
            agent.CalculatePathToTarget();

            // 🔹 CAMBIO: Si sigue sin ruta, intentar nodo destino alternativo
            if (agent == null || agent.currentPath == null || agent.currentPath.Count == 0)
            {
                Debug.LogWarning($"NPC {agent?.name ?? "null"} sigue sin ruta. Buscando nodo destino conectado...");
                GraphNode fallbackTarget = roadGraphSystem.roadGraph.FindClosestConnectedNode(agent.targetNode);
                if (fallbackTarget != null)
                {
                    agent.targetNode = fallbackTarget;
                    agent.CalculatePathToTarget();
                }

                if (agent.currentPath == null || agent.currentPath.Count == 0)
                {
                    Debug.LogError($"NPC {agent?.name ?? "null"} no tiene ruta válida. Será eliminado.");
                    RemoveNPC(agent); // Llamar al TrafficManager para eliminarlo
                }
            }
        }
    }
    
    // void UpdateEdgeCostsBasedOnTraffic()
    // {
    //     if (roadGraphSystem == null) return;
        
    //     // Reiniciar costos
    //     foreach (GraphEdge edge in roadGraphSystem.roadGraph.edges)
    //     {
    //         edge.trafficCost = 0f;
    //     }
        
    //     // Actualizar costos basados en NPCs activos
    //     foreach (NPCAgent npc in activeNPCs)
    //     {
    //         if (npc != null && npc.currentEdge != null)
    //         {
    //             npc.currentEdge.trafficCost += 0.1f;
    //             if (npc.currentEdge.trafficCost > 3f)
    //                 npc.currentEdge.trafficCost = 3f;
    //         }
    //     }
        
    //     // // Agregar costo por jugador
    //     // if (playerTransform != null)
    //     // {
    //     //     CarAgent playerAgent = playerTransform.GetComponent<CarAgent>();
    //     //     if (playerAgent != null && playerAgent.currentEdge != null)
    //     //     {
    //     //         playerAgent.currentEdge.trafficCost += 0.2f;
    //     //         if (playerAgent.currentEdge.trafficCost > 3f)
    //     //             playerAgent.currentEdge.trafficCost = 3f;
    //     //     }
    //     // }
        
    //     // Debug: mostrar costos altos
    //     if (debugMode)
    //     {
    //         int highTrafficEdges = roadGraphSystem.roadGraph.edges.Count(e => e.trafficCost > 1f);
    //         if (highTrafficEdges > 0)
    //         {
    //             Debug.Log($"TrafficManager: {highTrafficEdges} aristas con tráfico alto (>1.0)");
    //         }
    //     }
    // }
    
    void CheckForDespawn()
    {
        for (int i = activeNPCs.Count - 1; i >= 0; i--)
        {
            NPCAgent npc = activeNPCs[i];
            if (npc == null) continue;
            
            // Verificar si llegó al destino usando el tracker
            NPCDestinationTracker tracker = npc.GetComponent<NPCDestinationTracker>();
            if (tracker != null && tracker.HasReachedDestination() && !tracker.IsDespawning())
            {
                Debug.Log($"NPC {npc.name} llegó al destino. Iniciando despawn...");
                StartCoroutine(DespawnNPC(npc, tracker));
            }
        }
    }
    
    IEnumerator DespawnNPC(NPCAgent npc, NPCDestinationTracker tracker)
    {
        if (npc == null) yield break;
        
        // Marcar como en proceso de despawn
        tracker.SetDespawning(true);
        
        // Desactivar movimiento
        npc.enabled = false;
        
        // Opcional: efectos visuales antes de desaparecer
        Renderer renderer = npc.GetComponentInChildren<Renderer>();
        Color originalColor = Color.white;
        if (renderer != null)
        {
            originalColor = renderer.material.color;
            renderer.material.color = Color.gray;
        }
        
        // Esperar el delay de despawn
        yield return new WaitForSeconds(despawnDelay);
        
        // Destruir el NPC
        RemoveNPC(npc);
        
        // Respawnear nuevo NPC si está configurado
        if (respawnAfterDespawn && activeNPCs.Count < maxNPCVehicles)
        {
            yield return new WaitForSeconds(1f);
            SpawnNPCVehicle();
        }
    }
    
    void CleanupNPCs()
    {
        List<NPCAgent> toRemove = new List<NPCAgent>();

        for (int i = activeNPCs.Count - 1; i >= 0; i--)
        {
            NPCAgent npc = activeNPCs[i];

            // Si ya fue destruido
            if (npc == null)
            {
                activeNPCs.RemoveAt(i);
                continue;
            }

            // Si está muy lejos
            if (playerTransform != null)
            {
                float distanceToPlayer = Vector3.Distance(playerTransform.position, npc.transform.position);
                if (distanceToPlayer > 500f)
                {
                    toRemove.Add(npc);
                }
            }
        }

        // Eliminar después del loop
        foreach (var npc in toRemove)
        {
            RemoveNPC(npc);
        }
    }
    
    public void RemoveNPC(NPCAgent npc)
    {   
        if (npc == null) return;
        if (npc != null && activeNPCs.Contains(npc))
        {
            Debug.Log($"Removiendo NPC: {npc.name}");
            activeNPCs.Remove(npc);
            
            // Destruir el tracker si existe
            NPCDestinationTracker tracker = npc.GetComponent<NPCDestinationTracker>();
            if (tracker != null)
            {
                Destroy(tracker);
            }
            
            Destroy(npc.gameObject);
        }
    }
    
    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        
        // Dibujar nodos de spawn (verde)
        if (showSpawnGizmos)
        {
            Gizmos.color = Color.green;
            foreach (var node in spawnNodes)
            {
                if (node != null)
                {
                    Gizmos.DrawWireSphere(node.position, 1f);
                    Gizmos.DrawSphere(node.position, 0.5f);
                }
            }
        }
        
        // Dibujar nodos destino (rojo)
        if (showTargetGizmos)
        {
            Gizmos.color = Color.red;
            foreach (var node in targetNodes)
            {
                if (node != null)
                {
                    Gizmos.DrawSphere(node.position, 1f);
                    Gizmos.DrawWireSphere(node.position, 2f);
                }
            }
        }
        
        // Dibujar NPCs activos (azul)
        Gizmos.color = Color.blue;
        foreach (var npc in activeNPCs)
        {
            if (npc != null)
            {
                Gizmos.DrawCube(npc.transform.position, Vector3.one * 0.8f);
                
                // Dibujar línea hacia su destino
                if (npc.targetNode != null)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(npc.transform.position, npc.targetNode.position);
                    Gizmos.color = Color.blue;
                }
            }
        }
    }
    
    [ContextMenu("Forzar Spawn de NPC")]
    public void ForceSpawnNPC()
    {
        if (isInitialized)
        {
            Debug.Log("Forzando spawn de NPC...");
            SpawnNPCVehicle();
        }
        else
        {
            Debug.LogWarning("TrafficManager no está inicializado");
        }
    }
    
    [ContextMenu("Reencontrar Nodos")]
    public void RefindNodes()
    {
        GetNodesBySpecialNames();
        Debug.Log($"Nodos encontrados: Spawn={spawnNodes.Count}, Destino={targetNodes.Count}");
    }
    
    [ContextMenu("Debug Estado de NPCs")]
    public void DebugNPCs()
    {
        Debug.Log("=== ESTADO DE NPCs ===");
        Debug.Log($"Total NPCs activos: {activeNPCs.Count}");
        Debug.Log($"Nodos de spawn disponibles: {spawnNodes.Count}");
        Debug.Log($"Nodos destino disponibles: {targetNodes.Count}");
        
        int movingCount = 0;
        int stuckCount = 0;
        int waitingCount = 0;
        
        foreach (var npc in activeNPCs)
        {
            if (npc != null)
            {
                if (npc.currentPath != null && npc.currentPath.Count > 0)
                    movingCount++;
                else
                    stuckCount++;
                
                if (npc.isWaitingAtIntersection)
                    waitingCount++;
                
                Debug.Log($"NPC {npc.name}: " +
                         $"Pos: {npc.transform.position}, " +
                         $"Desde: {npc.startNode?.originalName}, " +
                         $"Hacia: {npc.targetNode?.originalName}, " +
                         $"Ruta: {npc.currentPath?.Count ?? 0} nodos, " +
                         $"Esperando: {npc.isWaitingAtIntersection}");
            }
        }
        
        Debug.Log($"En movimiento: {movingCount}, Atascados: {stuckCount}, Esperando: {waitingCount}");
        Debug.Log("=====================");
    }
    
    [ContextMenu("Limpiar Todos los NPCs")]
    public void ClearAllNPCs()
    {
        Debug.Log("Limpiando todos los NPCs...");
        
        foreach (var npc in activeNPCs.ToArray())
        {
            if (npc != null)
            {
                Destroy(npc.gameObject);
            }
        }
        
        activeNPCs.Clear();
        Debug.Log("Todos los NPCs eliminados");
    }
    
    [ContextMenu("Reiniciar Sistema")]
    public void ResetSystem()
    {
        ClearAllNPCs();
        spawnTimer = 0f;
        trafficUpdateTimer = 0f;
        
        // Reencontrar nodos
        GetNodesBySpecialNames();
        
        // Spawn inicial
        if (isInitialized)
        {
            StartCoroutine(InitialSpawn());
        }
        
        Debug.Log("Sistema de tráfico reiniciado");
    }
    
    public List<NPCAgent> GetNPCsNearPosition(Vector3 position, float radius)
    {
        List<NPCAgent> nearbyNPCs = new List<NPCAgent>();
        
        foreach (NPCAgent npc in activeNPCs)
        {
            if (npc != null && Vector3.Distance(position, npc.transform.position) <= radius)
            {
                nearbyNPCs.Add(npc);
            }
        }
        
        return nearbyNPCs;
    }
    
    public int GetActiveVehicleCount()
    {
        return activeNPCs.Count;
    }
}

// Componente para trackear llegada a destino
public class NPCDestinationTracker : MonoBehaviour
{
    private TrafficManager trafficManager;
    private NPCAgent npcAgent;
    private GraphNode targetNode;
    private float despawnDelay;
    private bool hasReachedDestination = false;
    private bool isDespawning = false;
    private float arrivalTime;
    
    public void Initialize(TrafficManager manager, NPCAgent agent, GraphNode target, float delay)
    {
        trafficManager = manager;
        npcAgent = agent;
        targetNode = target;
        despawnDelay = delay;
    }
    
    void Update()
    {
        if (hasReachedDestination || isDespawning || npcAgent == null || targetNode == null) return;
        
        // Verificar si llegó al destino
        float distance = Vector3.Distance(transform.position, targetNode.position);
        if (distance < 3f) // Umbral de llegada
        {
            hasReachedDestination = true;
            arrivalTime = Time.time;
        }
    }
    
    public bool HasReachedDestination()
    {
        return hasReachedDestination;
    }
    
    public bool IsDespawning()
    {
        return isDespawning;
    }
    
    public void SetDespawning(bool despawning)
    {
        isDespawning = despawning;
    }
    
    public float GetTimeSinceArrival()
    {
        if (!hasReachedDestination) return 0f;
        return Time.time - arrivalTime;
    }
}