using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Linq;
using System.IO;

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
    
    [Header("Driver Data Configuration")]
    public TextAsset driverDataFile;
    public string initialLocation = "La Guaira"; // Configura la ubicación inicial aquí
    
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
    
    // Variables para datos del conductor
    private DriverProfile currentDriverProfile;
    private List<DriverProfile> driverProfiles;
    private float baseMotorForce;
    private float baseSteeringAngle;
    
    [System.Serializable]
    public class DriverProfile
    {
        public string location;
        public float speed;
        public float initialSpeed;
        public float maxSpeed;
        public float averageSpeed;
        public string preferredEntrance;
        public string drivingStyle;
        public string externalReaction;
        public string deviation;
        public float speedChangePercent;
        public float estimatedDelay;
        public string carType;
        public string vehicleType;
        public string leaveHomeTime;
        public string arriveUnimetTime;
        
        // Factores de comportamiento calculados
        public float speedMultiplier;
        public float steeringMultiplier;
        public float riskFactor;
        public float patienceFactor;
    }

    protected override void Awake()
    {
        rb = GetComponent<Rigidbody>();
        wheels = GetComponentsInChildren<WheelCollider>();
        rb.centerOfMass = new Vector3(0, -0.5f, 0);
        
        currentMotorForce = motorForce;
        currentSteeringResponse = 1f;
        
        baseMotorForce = motorForce;
        baseSteeringAngle = steeringAngle;
        
        if (roadGraphSystem == null)
            roadGraphSystem = FindFirstObjectByType<RoadGraphSystem>();
            
        // Cargar perfiles de conductores
        LoadDriverProfiles();
    }

    private void LoadDriverProfiles()
    {
        driverProfiles = new List<DriverProfile>();
        
        if (driverDataFile == null)
        {
            Debug.LogError("No se ha asignado el archivo de datos de conductores!");
            return;
        }
        
        string[] lines = driverDataFile.text.Split('\n');
        
        foreach (string line in lines)
        {
            if (string.IsNullOrEmpty(line.Trim())) continue;
            
            DriverProfile profile = ParseDriverData(line);
            if (profile != null)
            {
                driverProfiles.Add(profile);
            }
        }
        
        Debug.Log($"Se cargaron {driverProfiles.Count} perfiles de conductores");
    }
    
    private DriverProfile ParseDriverData(string line)
    {
        try
        {
            DriverProfile profile = new DriverProfile();
            
            // Parsear los datos usando el formato del archivo
            string[] parts = line.Split('|');
            
            foreach (string part in parts)
            {
                string[] keyValue = part.Split(':');
                if (keyValue.Length < 2) continue;
                
                string key = keyValue[0].Trim();
                string value = keyValue[1].Trim();
                
                switch (key)
                {
                    case "Ubicación":
                        profile.location = value;
                        break;
                    case "Velocidad":
                        float.TryParse(value, out profile.speed);
                        break;
                    case "Velocidad inicial":
                        float.TryParse(value, out profile.initialSpeed);
                        break;
                    case "Velocidad máximo":
                        float.TryParse(value, out profile.maxSpeed);
                        break;
                    case "Velocidad promedio":
                        float.TryParse(value, out profile.averageSpeed);
                        break;
                    case "Entrada preferida":
                        profile.preferredEntrance = value;
                        break;
                    case "Estilo de manejo":
                        profile.drivingStyle = value;
                        break;
                    case "reacción a situación externa":
                        profile.externalReaction = value;
                        break;
                    case "desvío":
                        profile.deviation = value;
                        break;
                    case "cambio de velocidad (%)":
                        float.TryParse(value, out profile.speedChangePercent);
                        break;
                    case "Demora estimada (min)":
                        float.TryParse(value, out profile.estimatedDelay);
                        break;
                    case "Tipo de Carro":
                        profile.carType = value;
                        break;
                    case "carro/moto/wawa/taxi":
                        profile.vehicleType = value;
                        break;
                    case "Hora de salir de la casa":
                        profile.leaveHomeTime = value;
                        break;
                    case "Hora de llegar a la Unimet":
                        profile.arriveUnimetTime = value;
                        break;
                }
            }
            
            // Calcular factores de comportamiento
            CalculateBehaviorFactors(profile);
            
            return profile;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error parseando línea: {line}\nError: {e.Message}");
            return null;
        }
    }
    
    private void CalculateBehaviorFactors(DriverProfile profile)
    {
        // Factor de velocidad basado en el estilo de manejo y velocidad promedio
        if (profile.drivingStyle.Contains("Prudente"))
        {
            profile.speedMultiplier = 0.7f;
            profile.riskFactor = 0.3f;
            profile.patienceFactor = 0.8f;
        }
        else if (profile.drivingStyle.Contains("Lento"))
        {
            profile.speedMultiplier = 0.5f;
            profile.riskFactor = 0.1f;
            profile.patienceFactor = 0.9f;
        }
        else if (profile.drivingStyle.Contains("Agresivo"))
        {
            profile.speedMultiplier = 1.2f;
            profile.riskFactor = 0.8f;
            profile.patienceFactor = 0.3f;
        }
        else
        {
            profile.speedMultiplier = 1.0f;
            profile.riskFactor = 0.5f;
            profile.patienceFactor = 0.6f;
        }
        
        // Ajustar según velocidad promedio
        if (profile.averageSpeed > 0)
        {
            profile.speedMultiplier *= Mathf.Clamp(profile.averageSpeed / 30f, 0.5f, 1.5f);
        }
        
        // Factor de steering basado en reacción externa
        if (profile.externalReaction.Contains("Lenta"))
        {
            profile.steeringMultiplier = 0.6f;
        }
        else if (profile.externalReaction.Contains("Moderada"))
        {
            profile.steeringMultiplier = 0.8f;
        }
        else if (profile.externalReaction.Contains("Rápida"))
        {
            profile.steeringMultiplier = 1.2f;
        }
        else
        {
            profile.steeringMultiplier = 1.0f;
        }
    }
    
    private void SelectDriverProfileByLocation(string location)
    {
        // Buscar perfiles que coincidan con la ubicación
        var matchingProfiles = driverProfiles.Where(p => p.location.Contains(location)).ToList();
        
        if (matchingProfiles.Count == 0)
        {
            Debug.LogWarning($"No se encontraron perfiles para ubicación: {location}. Usando perfil por defecto.");
            currentDriverProfile = new DriverProfile()
            {
                speedMultiplier = 1.0f,
                steeringMultiplier = 1.0f,
                riskFactor = 0.5f,
                patienceFactor = 0.5f
            };
            return;
        }
        
        // Seleccionar un perfil aleatorio de los que coinciden
        currentDriverProfile = matchingProfiles[Random.Range(0, matchingProfiles.Count)];
        
        // Aplicar ajustes basados en el perfil
        motorForce = baseMotorForce * currentDriverProfile.speedMultiplier;
        steeringAngle = baseSteeringAngle * currentDriverProfile.steeringMultiplier;
        
        Debug.Log($"Perfil seleccionado: {currentDriverProfile.location} - " +
                 $"Estilo: {currentDriverProfile.drivingStyle} - " +
                 $"Multiplicador velocidad: {currentDriverProfile.speedMultiplier}");
    }

    public override void OnEpisodeBegin()
    {
        currentSteerInput = 0f;
        currentThrottleInput = 0f;
        ResetCar();
        
        // Seleccionar perfil basado en ubicación inicial
        SelectDriverProfileByLocation(initialLocation);
        
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
            
            // Usar la entrada preferida del perfil si está disponible
            if (currentDriverProfile != null && !string.IsNullOrEmpty(currentDriverProfile.preferredEntrance))
            {
                SetTargetByPreferredEntrance();
            }
            else
            {
                SetRandomTarget();
            }
            
            CalculatePathToTarget();
        }
    }
    
    private void SetTargetByPreferredEntrance()
    {
        if (roadGraphSystem.roadGraph.nodes.Count > 1)
        {
            roadGraphSystem.roadGraph.RebuildAllConnections();
            
            // Buscar nodos que coincidan con la entrada preferida
            List<GraphNode> possibleTargets = roadGraphSystem.roadGraph.nodes
                .Where(n => n.name.Contains(currentDriverProfile.preferredEntrance) && n != currentNode)
                .ToList();
                
            if (possibleTargets.Count > 0)
            {
                targetNode = possibleTargets[Random.Range(0, possibleTargets.Count)];
                Debug.Log($"Objetivo establecido por entrada preferida: {currentDriverProfile.preferredEntrance}");
                return;
            }
        }
        
        // Fallback a objetivo aleatorio
        SetRandomTarget();
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
                    
                    // Usar entrada preferida para nuevo objetivo si está disponible
                    if (currentDriverProfile != null && !string.IsNullOrEmpty(currentDriverProfile.preferredEntrance))
                    {
                        SetTargetByPreferredEntrance();
                    }
                    else
                    {
                        SetRandomTarget();
                    }
                    
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
        
        // Observaciones del perfil del conductor
        if (currentDriverProfile != null)
        {
            sensor.AddObservation(currentDriverProfile.speedMultiplier);
            sensor.AddObservation(currentDriverProfile.riskFactor);
            sensor.AddObservation(currentDriverProfile.patienceFactor);
        }
        else
        {
            sensor.AddObservation(1f); // Multiplicador velocidad por defecto
            sensor.AddObservation(0.5f); // Factor de riesgo por defecto
            sensor.AddObservation(0.5f); // Factor de paciencia por defecto
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
        
        // Recompensas basadas en el perfil del conductor
        if (currentDriverProfile != null)
        {
            // Recompensar comportamiento consistente con el estilo de manejo
            if (currentDriverProfile.drivingStyle.Contains("Prudente") || currentDriverProfile.drivingStyle.Contains("Lento"))
            {
                // Recompensar velocidades moderadas y steering suave
                if (forwardVelocity < 15f && Mathf.Abs(currentSteerInput) < 0.3f)
                {
                    AddReward(0.002f * currentDriverProfile.patienceFactor);
                }
            }
            else if (currentDriverProfile.drivingStyle.Contains("Agresivo"))
            {
                // Recompensar progresión más rápida (pero con cuidado)
                if (forwardVelocity > 20f)
                {
                    AddReward(0.001f * currentDriverProfile.riskFactor);
                }
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
    
    // Método público para cambiar la ubicación inicial
    public void SetInitialLocation(string newLocation)
    {
        initialLocation = newLocation;
    }
    
    // Método para obtener el perfil actual
    public DriverProfile GetCurrentDriverProfile()
    {
        return currentDriverProfile;
    }
}