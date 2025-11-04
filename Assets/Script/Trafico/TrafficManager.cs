using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TrafficManager : MonoBehaviour
{
    public RoadGraphSystem roadGraphSystem;
    public GameObject npcVehiclePrefab;
    public int maxNPCVehicles = 20;
    public float spawnInterval = 3f;
    public float minNPCSpeed = 5f;
    public float maxNPCSpeed = 15f;
    public float trafficUpdateInterval = 2f;
    
    private List<NPCAgent> activeNPCs = new List<NPCAgent>();
    private float spawnTimer = 0f;
    private float trafficUpdateTimer = 0f;
    
    void Start()
    {
        if (roadGraphSystem == null)
            roadGraphSystem = FindFirstObjectByType<RoadGraphSystem>();
            
        // Asegurarse de que el grafo esté reconstruido
        if (roadGraphSystem != null)
        {
            roadGraphSystem.roadGraph.RebuildAllConnections();
        }
    }
    
    void Update()
    {
        spawnTimer += Time.deltaTime;
        if (spawnTimer >= spawnInterval && activeNPCs.Count < maxNPCVehicles)
        {
            SpawnNPCVehicle();
            spawnTimer = 0f;
        }
        
        trafficUpdateTimer += Time.deltaTime;
        if (trafficUpdateTimer >= trafficUpdateInterval)
        {
            UpdateEdgeCostsBasedOnTraffic();
            trafficUpdateTimer = 0f;
        }
    }
    
    void SpawnNPCVehicle()
    {
        if (roadGraphSystem == null || roadGraphSystem.roadGraph.nodes.Count < 2) return;
        
        // Asegurarse de que el grafo esté reconstruido
        roadGraphSystem.roadGraph.RebuildAllConnections();
        
        // Buscar nodos que no sean intersecciones para spawnear
        List<GraphNode> spawnNodes = roadGraphSystem.roadGraph.nodes
            .Where(n => !n.isIntersection)
            .ToList();
            
        if (spawnNodes.Count == 0) return;
        
        GraphNode startNode = spawnNodes[Random.Range(0, spawnNodes.Count)];
        GraphNode targetNode;
        
        do {
            targetNode = roadGraphSystem.roadGraph.nodes[Random.Range(0, roadGraphSystem.roadGraph.nodes.Count)];
        } while (targetNode == startNode || targetNode.isIntersection);
        
        GameObject npcObj = Instantiate(npcVehiclePrefab, startNode.position, Quaternion.identity);
        NPCAgent npcAgent = npcObj.GetComponent<NPCAgent>();
        
        if (npcAgent != null)
        {
            npcAgent.roadGraphSystem = roadGraphSystem;
            npcAgent.startNode = startNode;
            npcAgent.targetNode = targetNode;
            npcAgent.speed = Random.Range(minNPCSpeed, maxNPCSpeed);
            npcAgent.Initialize();
            
            activeNPCs.Add(npcAgent);
        }
    }
    
    void UpdateEdgeCostsBasedOnTraffic()
    {
        if (roadGraphSystem == null) return;
        
        // Asegurarse de que el grafo esté reconstruido
        roadGraphSystem.roadGraph.RebuildAllConnections();
        
        foreach (GraphEdge edge in roadGraphSystem.roadGraph.edges)
        {
            edge.trafficCost = 0f;
        }
        
        foreach (NPCAgent npc in activeNPCs)
        {
            if (npc.currentEdge != null)
            {
                npc.currentEdge.trafficCost += 0.1f;
            }
        }
        
        CarAgent playerAgent = FindFirstObjectByType<CarAgent>();
        if (playerAgent != null && playerAgent.currentEdge != null)
        {
            playerAgent.currentEdge.trafficCost += 0.2f;
        }
    }
    
    public void RemoveNPC(NPCAgent npc)
    {
        activeNPCs.Remove(npc);
        Destroy(npc.gameObject);
    }
    
    public List<NPCAgent> GetNPCsNearPosition(Vector3 position, float radius)
    {
        List<NPCAgent> nearbyNPCs = new List<NPCAgent>();
        
        foreach (NPCAgent npc in activeNPCs)
        {
            if (Vector3.Distance(position, npc.transform.position) <= radius)
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