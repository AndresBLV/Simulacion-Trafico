using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPCAgent : MonoBehaviour
{
    public RoadGraphSystem roadGraphSystem;
    public GraphNode startNode;
    public GraphNode targetNode;
    public float speed = 10f;
    public float rotationSpeed = 2f;
    public float minDistanceToNode = 2f;
    
    [HideInInspector]
    public float baseSpeed;
    [HideInInspector]
    public float baseRotationSpeed;
    
    [HideInInspector]
    public GraphNode currentNode;
    [HideInInspector]
    public GraphEdge currentEdge;
    
    private List<GraphNode> currentPath;
    private int currentPathIndex = 0;
    private Vector3 currentTargetPosition;
    private bool isWaitingAtIntersection = false;
    private float intersectionWaitTimer = 0f;
    private float maxWaitTime = 3f;
    
    public void Initialize()
    {
        baseSpeed = speed;
        baseRotationSpeed = rotationSpeed;
        
        if (startNode != null)
        {
            if (roadGraphSystem != null)
            {
                roadGraphSystem.roadGraph.RebuildAllConnections();
            }
            
            transform.position = startNode.position;
            currentNode = startNode;
            
            CalculatePathToTarget();
            
            if (currentPath != null && currentPath.Count > 1)
            {
                currentPathIndex = 1;
                currentTargetPosition = currentPath[currentPathIndex].position;
                UpdateCurrentEdge();
            }
        }
    }
    
    void Update()
    {
        if (isWaitingAtIntersection)
        {
            intersectionWaitTimer += Time.deltaTime;
            if (intersectionWaitTimer >= maxWaitTime)
            {
                isWaitingAtIntersection = false;
                intersectionWaitTimer = 0f;
            }
            return;
        }
        
        if (currentPath == null || currentPathIndex >= currentPath.Count)
        {
            FindNewDestination();
            return;
        }
        
        MoveTowardsTarget();
        
        if (Vector3.Distance(transform.position, currentTargetPosition) < minDistanceToNode)
        {
            ReachNextNode();
        }
    }
    
    void MoveTowardsTarget()
    {
        Vector3 direction = (currentTargetPosition - transform.position).normalized;
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        transform.position += transform.forward * speed * Time.deltaTime;
        
        UpdateCurrentEdge();
    }
    
    void ReachNextNode()
    {
        currentNode = currentPath[currentPathIndex];
        
        if (currentNode.isIntersection)
        {
            isWaitingAtIntersection = true;
        }
        
        currentPathIndex++;
        
        if (currentPathIndex < currentPath.Count)
        {
            currentTargetPosition = currentPath[currentPathIndex].position;
        }
        else
        {
            FindNewDestination();
        }
        
        UpdateCurrentEdge();
    }
    
    void FindNewDestination()
    {
        if (roadGraphSystem == null || roadGraphSystem.roadGraph.nodes.Count < 2) return;
        
        roadGraphSystem.roadGraph.RebuildAllConnections();
        
        GraphNode newTarget;
        do {
            newTarget = roadGraphSystem.roadGraph.nodes[Random.Range(0, roadGraphSystem.roadGraph.nodes.Count)];
        } while (newTarget == currentNode || newTarget.isIntersection);
        
        targetNode = newTarget;
        CalculatePathToTarget();
        
        if (currentPath != null && currentPath.Count > 1)
        {
            currentPathIndex = 1;
            currentTargetPosition = currentPath[currentPathIndex].position;
            UpdateCurrentEdge();
        }
    }
    
    void CalculatePathToTarget()
    {
        if (currentNode != null && targetNode != null)
        {
            roadGraphSystem.roadGraph.RebuildAllConnections();
            
            currentPath = roadGraphSystem.roadGraph.FindPath(currentNode, targetNode);
            currentPathIndex = 0;
        }
    }
    
    void UpdateCurrentEdge()
    {
        if (currentPath != null && currentPathIndex > 0 && currentPathIndex < currentPath.Count)
        {
            GraphNode previousNode = currentPath[currentPathIndex - 1];
            GraphNode nextNode = currentPath[currentPathIndex];
            
            foreach (GraphEdge edge in previousNode.edges)
            {
                if (edge.endNodeId == nextNode.id)
                {
                    if (edge.endNode == null)
                        edge.RebuildConnections(roadGraphSystem.roadGraph);
                        
                    currentEdge = edge;
                    break;
                }
            }
        }
    }
    
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.CompareTag("NPC"))
        {
            FindNewDestination();
        }
    }
    
    void OnDrawGizmosSelected()
    {
        if (currentPath != null && currentPath.Count > 0)
        {
            Gizmos.color = Color.yellow;
            for (int i = currentPathIndex; i < currentPath.Count - 1; i++)
            {
                Gizmos.DrawLine(currentPath[i].position, currentPath[i+1].position);
                Gizmos.DrawSphere(currentPath[i].position, 0.5f);
            }
            
            if (currentPathIndex < currentPath.Count)
            {
                Gizmos.DrawSphere(currentPath[currentPathIndex].position, 0.7f);
            }
        }
    }
}