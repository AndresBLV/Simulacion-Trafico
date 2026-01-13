using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NPCAgent : MonoBehaviour
{
    [Header("Configuración")]
    public RoadGraphSystem roadGraphSystem;
    public GraphNode startNode;
    public GraphNode targetNode;
    public float speed = 10f;
    public float rotationSpeed = 2f;
    public float minDistanceToNode = 2f;
    public float waitTimeAtIntersection = 3f;
    
    [Header("Debug")]
    public bool showPath = true;
    public Color pathColor = Color.yellow;
    
    // Variables privadas
    public List<GraphNode> currentPath;
    public int currentPathIndex = 0;
    public Vector3 currentTargetPosition;
    public bool isWaitingAtIntersection = false;
    public float intersectionWaitTimer = 0f;
    public bool isInitialized = false;
    public Rigidbody rb;
    
    // Propiedades públicas (para acceso externo)
    [HideInInspector] public GraphNode currentNode;
    [HideInInspector] public GraphEdge currentEdge;
    
    void Start()
    {
        Debug.Log($"NPCAgent.Start() - Inicializando NPC: {gameObject.name}");
        
        // Obtener Rigidbody
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 1500f;
            rb.linearDamping = 0.1f;
            rb.angularDamping = 0.5f;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }
        
        // Si no hay roadGraphSystem asignado, buscarlo
        if (roadGraphSystem == null)
        {
            roadGraphSystem = FindFirstObjectByType<RoadGraphSystem>();
            if (roadGraphSystem == null)
            {
                Debug.LogError($"NPCAgent {gameObject.name}: No se encontró RoadGraphSystem en la escena!");
                return;
            }
        }
        
        // Asegurar que el grafo esté construido
        if (roadGraphSystem.roadGraph == null || roadGraphSystem.roadGraph.nodes.Count == 0)
        {
            Debug.LogWarning($"NPCAgent {gameObject.name}: El grafo está vacío. Intentando construir...");
            roadGraphSystem.BuildGraphAutomatically();
        }
        
        // Reconstruir conexiones
        roadGraphSystem.roadGraph.RebuildAllConnections();
        
        // Si no hay startNode asignado, obtener uno aleatorio
        if (startNode == null)
        {
            Debug.LogWarning($"NPCAgent {gameObject.name}: No hay startNode asignado. Obteniendo uno aleatorio...");
            startNode = GetRandomNode();
            if (startNode == null)
            {
                Debug.LogError($"NPCAgent {gameObject.name}: No se pudo obtener un nodo inicial!");
                return;
            }
        }
        
        // Posicionar en el nodo inicial
        Vector3 spawnPosition = startNode.position;
        // Asegurar que esté sobre el suelo
        spawnPosition.y += 1f;
        transform.position = spawnPosition;
        currentNode = startNode;
        
        Debug.Log($"NPCAgent {gameObject.name} - Nodo inicial: {currentNode.name}, Posición: {currentNode.position}");
        
        // Encontrar un destino inicial
        FindNewDestination();
        
        isInitialized = true;
        Debug.Log($"NPCAgent {gameObject.name} - Inicializado correctamente");
    }
    
    void Update()
    {
        if (!isInitialized) return;
        
        // Si está esperando en una intersección
        if (isWaitingAtIntersection)
        {
            intersectionWaitTimer += Time.deltaTime;
            if (intersectionWaitTimer >= waitTimeAtIntersection)
            {
                isWaitingAtIntersection = false;
                intersectionWaitTimer = 0f;
                Debug.Log($"NPCAgent {gameObject.name} - Continuando desde intersección");
            }
            return;
        }
        
        // Si no hay ruta actual, buscar nueva
        if (currentPath == null || currentPathIndex >= currentPath.Count)
        {
            Debug.LogWarning($"NPCAgent {gameObject.name} - Sin ruta válida. Buscando nuevo destino...");
            FindNewDestination();
            return;
        }
        
        // Moverse hacia el objetivo
        MoveTowardsTarget();
        
        // Verificar si llegó al nodo objetivo
        if (Vector3.Distance(transform.position, currentTargetPosition) <= minDistanceToNode)
        {
            ReachNextNode();
        }
    }
    
    private void FixedUpdate()
    {
        // Asegurar que no se vuelque
        if (rb != null)
        {
            Vector3 euler = transform.rotation.eulerAngles;
            euler.x = 0;
            euler.z = 0;
            transform.rotation = Quaternion.Euler(euler);
        }
    }
    
    private void MoveTowardsTarget()
    {
        if (currentPath == null || currentPathIndex >= currentPath.Count) return;
        
        // Calcular dirección
        Vector3 direction = (currentTargetPosition - transform.position).normalized;
        
        // Si está muy cerca, ir al siguiente nodo
        if (Vector3.Distance(transform.position, currentTargetPosition) < 1f)
        {
            ReachNextNode();
            return;
        }
        
        // Rotación suave
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
        
        // Movimiento hacia adelante
        transform.Translate(Vector3.forward * speed * Time.deltaTime, Space.Self);
        
        // Mantener altura
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out hit, 10f))
        {
            Vector3 pos = transform.position;
            pos.y = hit.point.y + 0.5f;
            transform.position = pos;
        }
    }

    private void ReachNextNode()
    {
        if (currentPath == null || currentPathIndex >= currentPath.Count) return;
        
        // Actualizar nodo actual
        currentNode = currentPath[currentPathIndex];
        Debug.Log($"{gameObject.name} llegó a {currentNode.name}");
        
        // Avanzar índice
        currentPathIndex++;
        
        // Si hay más nodos, establecer nuevo objetivo
        if (currentPathIndex < currentPath.Count)
        {
            currentTargetPosition = currentPath[currentPathIndex].position;
            UpdateCurrentEdge();
        }
        else
        {
            // Llegó al final del camino
            Debug.Log($"{gameObject.name} completó la ruta");
            // El TrafficManager se encargará del despawn
        }
    }
        
    public void CalculatePathToTarget()
    {
        if (currentNode == null || targetNode == null)
        {
            Debug.LogError($"NPCAgent {gameObject.name}: No se puede calcular ruta - nodo actual o destino nulo");
            return;
        }
        
        if (currentNode == targetNode)
        {
            Debug.LogWarning($"NPCAgent {gameObject.name}: El destino es el mismo nodo actual");
            return;
        }
        
        if (roadGraphSystem == null || roadGraphSystem.roadGraph == null)
        {
            Debug.LogError($"NPCAgent {gameObject.name}: RoadGraphSystem no disponible");
            return;
        }
        
        roadGraphSystem.roadGraph.RebuildAllConnections();
        currentPath = roadGraphSystem.roadGraph.FindPath(currentNode, targetNode);
        currentPathIndex = 0;
        
        if (currentPath != null && currentPath.Count > 0)
        {
            Debug.Log($"NPCAgent {gameObject.name} - Ruta calculada: {currentPath.Count} nodos");
            currentTargetPosition = currentPath[0].position;
            UpdateCurrentEdge();
        }
        else
        {
            Debug.LogWarning($"NPCAgent {gameObject.name}: No se encontró ruta de {currentNode?.name} a {targetNode?.name}");
            
            // Intentar con un nodo diferente
            Invoke("FindNewDestination", 1f);
        }
    }

   public void FindNewDestination()
    {
        if (roadGraphSystem == null || roadGraphSystem.roadGraph == null || roadGraphSystem.roadGraph.nodes.Count < 2)
        {
            Debug.LogError($"NPCAgent {gameObject.name}: Grafo no disponible o muy pequeño");
            return;
        }
        
        // Buscar un nodo aleatorio diferente al actual
        GraphNode newTarget = null;
        int attempts = 0;
        int maxAttempts = 10;
        
        while (attempts < maxAttempts && (newTarget == null || newTarget == currentNode))
        {
            int randomIndex = Random.Range(0, roadGraphSystem.roadGraph.nodes.Count);
            newTarget = roadGraphSystem.roadGraph.nodes[randomIndex];
            attempts++;
        }
        
        if (newTarget != null && newTarget != currentNode)
        {
            targetNode = newTarget;
            CalculatePathToTarget();
            Debug.Log($"NPCAgent {gameObject.name}: Nuevo destino: {targetNode.name}");
        }
    }
        
    private void UpdateCurrentEdge()
    {
        if (currentPath != null && currentPathIndex > 0 && currentPathIndex < currentPath.Count)
        {
            GraphNode previousNode = currentPath[currentPathIndex - 1];
            GraphNode nextNode = currentPath[currentPathIndex];
            
            foreach (GraphEdge edge in previousNode.edges)
            {
                if (edge.endNodeId == nextNode.id)
                {
                    currentEdge = edge;
                    break;
                }
            }
        }
    }
    
    private GraphNode GetRandomNode()
    {
        if (roadGraphSystem == null || roadGraphSystem.roadGraph.nodes.Count == 0) return null;
        
        // Preferir nodos que no sean intersecciones
        var nonIntersectionNodes = roadGraphSystem.roadGraph.nodes
            .Where(n => !n.isIntersection)
            .ToList();
        
        if (nonIntersectionNodes.Count > 0)
        {
            return nonIntersectionNodes[Random.Range(0, nonIntersectionNodes.Count)];
        }
        
        // Si todos son intersecciones, usar cualquier nodo
        return roadGraphSystem.roadGraph.nodes[Random.Range(0, roadGraphSystem.roadGraph.nodes.Count)];
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"NPCAgent {gameObject.name} - Colisión con: {collision.gameObject.name}");
        
        if (collision.gameObject.CompareTag("Player") || 
            collision.gameObject.CompareTag("NPC") ||
            collision.gameObject.CompareTag("Obstacle"))
        {
            Debug.Log($"NPCAgent {gameObject.name} - Colisión con objeto. Buscando nuevo destino...");
            
            // Pequeño retroceso
            if (rb != null)
            {
                rb.AddForce(-transform.forward * 5f, ForceMode.Impulse);
            }
            
            // Buscar nuevo destino
            FindNewDestination();
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("TrafficLight"))
        {
            Debug.Log($"NPCAgent {gameObject.name} - Encontró semáforo. Esperando...");
            isWaitingAtIntersection = true;
            intersectionWaitTimer = 0f;
        }
        
        if (other.CompareTag("Boundary"))
        {
            Debug.Log($"NPCAgent {gameObject.name} - Salió de los límites. Reiniciando...");
            // Reposicionar en un nodo aleatorio
            startNode = GetRandomNode();
            if (startNode != null)
            {
                transform.position = startNode.position;
                currentNode = startNode;
                FindNewDestination();
            }
        }
    }
    
    private void OnDrawGizmos()
    {
        if (!showPath || currentPath == null || currentPath.Count == 0) return;
        
        Gizmos.color = pathColor;
        
        // Dibujar línea desde el NPC hasta el próximo nodo
        if (currentPathIndex < currentPath.Count)
        {
            Gizmos.DrawLine(transform.position, currentTargetPosition);
            Gizmos.DrawSphere(currentTargetPosition, 0.3f);
        }
        
        // Dibujar el resto del camino
        for (int i = Mathf.Max(0, currentPathIndex - 1); i < currentPath.Count - 1; i++)
        {
            if (i >= 0 && i + 1 < currentPath.Count)
            {
                Gizmos.DrawLine(currentPath[i].position, currentPath[i + 1].position);
                Gizmos.DrawSphere(currentPath[i].position, 0.2f);
            }
        }
        
        // Dibujar esfera en el destino final
        if (currentPath.Count > 0)
        {
            Gizmos.DrawSphere(currentPath[currentPath.Count - 1].position, 0.5f);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        // Dibujar información adicional cuando está seleccionado
        if (currentNode != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(currentNode.position, 1f);
        }
        
        if (currentEdge != null && currentEdge.startNode != null && currentEdge.endNode != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(currentEdge.startNode.position, currentEdge.endNode.position);
        }
    }
    
    // Método público para reiniciar el NPC
    public void ResetNPC(GraphNode newStartNode = null)
    {
        if (newStartNode != null)
        {
            startNode = newStartNode;
        }
        else
        {
            startNode = GetRandomNode();
        }
        
        isWaitingAtIntersection = false;
        intersectionWaitTimer = 0f;
        
        // Reposicionar
        if (startNode != null)
        {
            Vector3 spawnPosition = startNode.position;
            spawnPosition.y += 1f; // Asegurar que esté sobre el suelo
            transform.position = spawnPosition;
            currentNode = startNode;
        }
        
        FindNewDestination();
        
        Debug.Log($"NPCAgent {gameObject.name} - Reiniciado");
    }
    
    // Método para configurar velocidad
    public void SetSpeed(float newSpeed)
    {
        speed = newSpeed;
    }
    
    // Método para obtener información del NPC
    public string GetNPCInfo()
    {
        return $"NPC: {gameObject.name}\n" +
               $"Nodo Actual: {currentNode?.name}\n" +
               $"Destino: {targetNode?.name}\n" +
               $"Velocidad: {speed:F1}\n" +
               $"Ruta: {currentPath?.Count ?? 0} nodos\n" +
               $"Esperando: {isWaitingAtIntersection}";
    }
}