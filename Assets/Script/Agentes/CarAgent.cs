using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Linq;

public class CarAgent : Agent
{
    [Header("Car Settings")]
    public float motorForce = 1500f;
    public float steeringAngle = 25f;
    public Transform resetPosition;
    public float raycastDistance = 5f;
    public LayerMask roadLayer;
    public int rayCount = 5;
    public float raySpreadAngle = 45f;
    
    [Header("Graph Navigation")]
    public RoadGraphSystem roadGraphSystem;
    public float pathRecalculationInterval = 5f;
    public float nodeReachedDistance = 3f;
    
    [Header("Weather Adaptation")]
    public float rainSpeedAdaptationRate = 0.1f;
    public float rainSteeringAdaptationRate = 0.05f;
    
    private GraphNode currentNode;
    private GraphNode targetNode;
    private List<GraphNode> currentPath;
    private int currentPathIndex = 0;
    public GraphEdge currentEdge;
    private float lastPathRecalculationTime;
    
    private Rigidbody rb;
    private WheelCollider[] wheels;
    private bool hasFinished = false;
    private float currentSteerInput;
    private float currentThrottleInput;
    private float previousSteerInput;
    
    // Variables para adaptación a la lluvia
    private float currentMotorForce;
    private float currentSteeringResponse;
    private float weatherAdaptationLevel = 0f;
    
    protected override void Awake()
    {
        rb = GetComponent<Rigidbody>();
        wheels = GetComponentsInChildren<WheelCollider>();
        rb.centerOfMass = new Vector3(0, -0.5f, 0);
        
        currentMotorForce = motorForce;
        currentSteeringResponse = 1f;
        
        if (roadGraphSystem == null)
            roadGraphSystem = FindFirstObjectByType<RoadGraphSystem>();
    }

    public override void OnEpisodeBegin()
    {
        currentSteerInput = 0f;
        currentThrottleInput = 0f;
        ResetCar();
        InitializeGraphNavigation();
        
        // Reiniciar adaptación al clima
        weatherAdaptationLevel = 0f;
        currentMotorForce = motorForce;
        currentSteeringResponse = 1f;
    }

    private void InitializeGraphNavigation()
    {
        if (roadGraphSystem != null && roadGraphSystem.roadGraph.nodes.Count > 0)
        {
            roadGraphSystem.roadGraph.RebuildAllConnections();
            
            currentNode = roadGraphSystem.roadGraph.GetNearestNode(transform.position);
            SetRandomTarget();
            CalculatePathToTarget();
        }
    }
    
    private void SetRandomTarget()
    {
        if (roadGraphSystem.roadGraph.nodes.Count > 1)
        {
            roadGraphSystem.roadGraph.RebuildAllConnections();
            
            List<GraphNode> possibleTargets = roadGraphSystem.roadGraph.nodes
                .Where(n => !n.isIntersection && n != currentNode)
                .ToList();
                
            if (possibleTargets.Count > 0)
            {
                targetNode = possibleTargets[Random.Range(0, possibleTargets.Count)];
            }
            else
            {
                do
                {
                    targetNode = roadGraphSystem.roadGraph.nodes[Random.Range(0, roadGraphSystem.roadGraph.nodes.Count)];
                } while (targetNode == currentNode);
            }
        }
    }
    
    private void CalculatePathToTarget()
    {
        if (currentNode != null && targetNode != null)
        {
            roadGraphSystem.roadGraph.RebuildAllConnections();
            
            currentPath = roadGraphSystem.roadGraph.FindPath(currentNode, targetNode);
            currentPathIndex = 0;
            lastPathRecalculationTime = Time.time;
            
            if (currentPath == null)
            {
                Debug.LogWarning("No se pudo encontrar una ruta al objetivo. Eligiendo nuevo objetivo.");
                SetRandomTarget();
                CalculatePathToTarget();
            }
        }
    }
    
    private void UpdateGraphNavigation()
    {
        if (Time.time - lastPathRecalculationTime > pathRecalculationInterval)
        {
            roadGraphSystem.roadGraph.RebuildAllConnections();
            
            List<GraphNode> alternativePath = roadGraphSystem.roadGraph.FindPath(currentNode, targetNode);
            if (alternativePath != null && alternativePath.Count > 0)
            {
                float currentPathCost = CalculatePathCost(currentPath);
                float alternativePathCost = CalculatePathCost(alternativePath);
                
                if (alternativePathCost < currentPathCost * 0.8f)
                {
                    currentPath = alternativePath;
                    currentPathIndex = 0;
                    Debug.Log("Cambiando a ruta alternativa debido al tráfico");
                }
            }
            
            lastPathRecalculationTime = Time.time;
        }
        
        if (currentPath != null && currentPathIndex < currentPath.Count)
        {
            Vector3 nextNodePos = currentPath[currentPathIndex].position;
            float distanceToNode = Vector3.Distance(transform.position, nextNodePos);
            
            if (distanceToNode < nodeReachedDistance)
            {
                currentNode = currentPath[currentPathIndex];
                currentPathIndex++;
                AddReward(0.1f);
                
                if (currentPathIndex >= currentPath.Count && currentNode == targetNode)
                {
                    AddReward(1f);
                    SetRandomTarget();
                    CalculatePathToTarget();
                }
            }
            
            UpdateCurrentEdge();
        }
    }
    
    private float CalculatePathCost(List<GraphNode> path)
    {
        if (path == null || path.Count < 2) return Mathf.Infinity;
        
        float totalCost = 0f;
        for (int i = 0; i < path.Count - 1; i++)
        {
            GraphNode nodeA = path[i];
            GraphNode nodeB = path[i + 1];
            
            foreach (GraphEdge edge in nodeA.edges)
            {
                if (edge.endNodeId == nodeB.id)
                {
                    totalCost += edge.TotalCost;
                    break;
                }
            }
        }
        
        return totalCost;
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
                    if (edge.endNode == null)
                        edge.RebuildConnections(roadGraphSystem.roadGraph);
                        
                    currentEdge = edge;
                    break;
                }
            }
        }
    }
    
    private void UpdateWeatherAdaptation()
    {
        if (WeatherManager.Instance == null) return;
        
        if (WeatherManager.Instance.isRaining)
        {
            // Aumentar adaptación con el tiempo
            weatherAdaptationLevel = Mathf.Clamp01(weatherAdaptationLevel + Time.deltaTime * rainSpeedAdaptationRate);
            
            // Ajustar parámetros de conducción según la lluvia y nivel de adaptación
            float targetMotorForce = motorForce * WeatherManager.Instance.GetMotorForceMultiplier();
            float targetSteeringResponse = WeatherManager.Instance.GetSteeringMultiplier();
            
            currentMotorForce = Mathf.Lerp(motorForce, targetMotorForce, weatherAdaptationLevel);
            currentSteeringResponse = Mathf.Lerp(1f, targetSteeringResponse, weatherAdaptationLevel);
            
            // Ajustar fricción de las ruedas
            foreach (WheelCollider wheel in wheels)
            {
                WheelFrictionCurve forwardFriction = wheel.forwardFriction;
                forwardFriction.stiffness = WeatherManager.Instance.GetFrictionFactor();
                wheel.forwardFriction = forwardFriction;
                
                WheelFrictionCurve sidewaysFriction = wheel.sidewaysFriction;
                sidewaysFriction.stiffness = WeatherManager.Instance.GetFrictionFactor();
                wheel.sidewaysFriction = sidewaysFriction;
            }
            
            // Recompensa por adaptarse a la lluvia
            AddReward(0.001f * weatherAdaptationLevel);
        }
        else
        {
            // Reducir adaptación cuando no llueve
            weatherAdaptationLevel = Mathf.Clamp01(weatherAdaptationLevel - Time.deltaTime * rainSpeedAdaptationRate);
            
            // Volver a valores normales
            currentMotorForce = Mathf.Lerp(motorForce * WeatherManager.Instance.GetMotorForceMultiplier(), 
                motorForce, weatherAdaptationLevel);
            currentSteeringResponse = Mathf.Lerp(WeatherManager.Instance.GetSteeringMultiplier(), 
                1f, weatherAdaptationLevel);
            
            // Restaurar fricción normal
            foreach (WheelCollider wheel in wheels)
            {
                WheelFrictionCurve forwardFriction = wheel.forwardFriction;
                forwardFriction.stiffness = 1.0f;
                wheel.forwardFriction = forwardFriction;
                
                WheelFrictionCurve sidewaysFriction = wheel.sidewaysFriction;
                sidewaysFriction.stiffness = 1.0f;
                wheel.sidewaysFriction = sidewaysFriction;
            }
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(rb.linearVelocity.magnitude / 20f);
        sensor.AddObservation(Vector3.Dot(transform.forward, Vector3.forward));

        float angleStep = rayCount > 1 ? raySpreadAngle / (rayCount - 1) : 0f;

        for (int i = 0; i < rayCount; i++)
        {
            float angle = (rayCount == 1) ? 0f : (-raySpreadAngle / 2 + i * angleStep);
            Vector3 dir = Quaternion.Euler(0, angle, 0) * transform.forward;

            // Ajustar distancia de rayos según visibilidad por lluvia
            float adjustedRaycastDistance = raycastDistance;
            if (WeatherManager.Instance != null)
            {
                adjustedRaycastDistance *= WeatherManager.Instance.GetVisibilityFactor();
            }

            if (Physics.Raycast(transform.position, dir, out RaycastHit hit, adjustedRaycastDistance, roadLayer))
            {
                sensor.AddObservation(hit.distance / adjustedRaycastDistance);
                Debug.DrawRay(transform.position, dir * hit.distance, Color.green);
            }
            else
            {
                sensor.AddObservation(0f);
                Debug.DrawRay(transform.position, dir * adjustedRaycastDistance, Color.red);
            }
        }

        sensor.AddObservation(transform.up.y);
        
        foreach (var wheel in wheels)
        {
            sensor.AddObservation(wheel.isGrounded); 
        }
        
        if (currentPath != null && currentPathIndex < currentPath.Count)
        {
            Vector3 nextNodeDir = (currentPath[currentPathIndex].position - transform.position).normalized;
            sensor.AddObservation(Vector3.Dot(transform.forward, nextNodeDir));
            sensor.AddObservation(Vector3.Distance(transform.position, currentPath[currentPathIndex].position) / 50f);
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }
        
        if (currentEdge != null)
        {
            sensor.AddObservation(currentEdge.trafficCost / 5f);
        }
        else
        {
            sensor.AddObservation(0f);
        }
        
        // Observaciones meteorológicas
        if (WeatherManager.Instance != null)
        {
            sensor.AddObservation(WeatherManager.Instance.isRaining ? 1f : 0f);
            sensor.AddObservation(WeatherManager.Instance.rainIntensity);
            sensor.AddObservation(weatherAdaptationLevel);
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float steerInput = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float throttleInput = Mathf.Clamp(actions.ContinuousActions[1], 0f, 1f);
        
        currentSteerInput = Mathf.Lerp(currentSteerInput, steerInput, 0.1f);
        currentThrottleInput = Mathf.Lerp(currentThrottleInput, throttleInput, 0.15f);

        // Aplicar adaptación al clima
        UpdateWeatherAdaptation();
        
        ApplySteering(currentSteerInput);
        ApplyMotor(currentThrottleInput);
        
        CalculateRewards();
        UpdateGraphNavigation();
    }

    private void CalculateRewards()
    {
        float forwardVelocity = Vector3.Dot(transform.forward, rb.linearVelocity);
        float velocityReward = forwardVelocity * 0.01f;
        float baseReward = 0.001f;
        
        AddReward(baseReward + velocityReward);
        
        float steeringChangePenalty = Mathf.Abs(currentSteerInput - previousSteerInput) * -0.005f;
        AddReward(steeringChangePenalty);
        previousSteerInput = currentSteerInput;
        
        if (!IsGrounded())
            AddReward(-0.05f);
            
        if (IsTilted())
            AddReward(-0.01f);
            
        if (currentPath != null && currentPathIndex < currentPath.Count)
        {
            Vector3 toNextNode = (currentPath[currentPathIndex].position - transform.position).normalized;
            float alignment = Vector3.Dot(transform.forward, toNextNode);
            AddReward(alignment * 0.01f);
        }
        
        // Recompensas/penalizaciones específicas por clima
        if (WeatherManager.Instance != null && WeatherManager.Instance.isRaining)
        {
            // Penalizar derrapes en lluvia
            float lateralVelocity = Mathf.Abs(Vector3.Dot(transform.right, rb.linearVelocity));
            if (lateralVelocity > 2f)
            {
                AddReward(-0.02f * lateralVelocity * WeatherManager.Instance.rainIntensity);
            }
            
            // Recompensar conducción suave en lluvia
            if (Mathf.Abs(currentSteerInput) < 0.2f && forwardVelocity > 5f)
            {
                AddReward(0.005f * WeatherManager.Instance.rainIntensity);
            }
        }
    }

    private void ApplySteering(float steerInput)
    {
        foreach (var wheel in wheels)
        {
            if (wheel.transform.localPosition.z > 0)
            {
                wheel.steerAngle = steerInput * steeringAngle * currentSteeringResponse;
            }
        }
    }

    private void ApplyMotor(float throttleInput)
    {
        foreach (var wheel in wheels)
        {
            if (wheel.transform.localPosition.z < 0)
            {
                wheel.motorTorque = throttleInput * currentMotorForce;
            }
        }
    }

    private bool IsGrounded()
    {
        foreach (var wheel in wheels)
        {
            if (!wheel.isGrounded)
                return false;
        }
        return true;
    }

    private bool IsTilted()
    {
        return Vector3.Angle(transform.up, Vector3.up) > 45f;
    }

    private void FixedUpdate()
    {
        if (StepCount > MaxStep)
        {
            AddReward(-1f);
            EndEpisode();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Obstacle") || collision.gameObject.CompareTag("NPC"))
        {
            AddReward(-2f);
            EndEpisode();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Boundary"))
        {
            AddReward(-2f);
            EndEpisode();
        }
        else if (other.CompareTag("FinishLine") && !hasFinished)
        {
            AddReward(5f);
            hasFinished = true;
            EndEpisode();
        }
    }

    private void ResetCar()
    {
        transform.position = resetPosition.position;
        transform.rotation = resetPosition.rotation;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        hasFinished = false;
        
        foreach (var wheel in wheels)
        {
            wheel.motorTorque = 0f;
            wheel.steerAngle = 0f;
            wheel.brakeTorque = 0f;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        for (int i = 0; i < rayCount; i++)
        {
            float angle = (rayCount == 1) ? 0f : (-raySpreadAngle / 2 + i * (raySpreadAngle / (rayCount - 1)));
            Vector3 dir = Quaternion.Euler(0, angle, 0) * transform.forward;
            
            // Ajustar visualización de rayos según visibilidad
            float adjustedRaycastDistance = raycastDistance;
            if (WeatherManager.Instance != null)
            {
                adjustedRaycastDistance *= WeatherManager.Instance.GetVisibilityFactor();
            }
            
            Gizmos.DrawRay(transform.position, dir * adjustedRaycastDistance);
        }
        
        if (currentPath != null && currentPath.Count > 0)
        {
            Gizmos.color = Color.magenta;
            for (int i = currentPathIndex; i < currentPath.Count - 1; i++)
            {
                Gizmos.DrawLine(currentPath[i].position, currentPath[i+1].position);
                Gizmos.DrawSphere(currentPath[i].position, 0.7f);
            }
            
            if (currentPathIndex < currentPath.Count)
            {
                Gizmos.DrawSphere(currentPath[currentPathIndex].position, 1f);
            }
        }
    }
}