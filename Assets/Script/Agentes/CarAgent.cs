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
    private float lastDebugTime = 0f;
    [Header("Car Settings")]
    public float motorForce = 1500f;
    public float steeringAngle = 25f;
    public Transform resetPosition;
    public float raycastDistance = 5f;
    public LayerMask roadLayer;
    public int rayCount = 5;
    public float raySpreadAngle = 45f;

    [Header("Spawn Manager")]
    public SpawnManager spawnManager;
    
    [Header("Graph Navigation")]
    public RoadGraphSystem roadGraphSystem;
    public float pathRecalculationInterval = 5f;
    public float nodeReachedDistance = 3f;
    
    [Header("Weather Adaptation")]
    public float rainSpeedAdaptationRate = 0.1f;
    public float rainSteeringAdaptationRate = 0.05f;
    
    [Header("Driver Data Configuration")]
    public TextAsset driverDataFile;
    public string initialLocation = "La Guaira";
    
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

    private float currentMotorForce;
    private float currentSteeringResponse;
    private float weatherAdaptationLevel = 0f;

    private DriverProfile currentDriverProfile;
    private List<DriverProfile> driverProfiles;
    private float baseMotorForce;
    private float baseSteeringAngle;
    
    [System.Serializable]
    public class DriverProfile
    {
        public string year;
        public int peopleCount;

        public float speed;
        public float initialSpeed;
        public float maxSpeed;
        public float averageSpeed;

        public string location;
        public string roadType;
        public string livingZone;

        public string preferredEntrance;
        public string drivingStyle;

        public float externalReactionPercent;
        public float deviationPercent;
        public float speedChangePercent;

        public float estimatedDelay;

        public string carType;
        public string vehicleType;
        public string transmission;
        public string condition;

        public string delayReported;
        public string accompanied;

        public string leaveHomeTime;
        public string arriveUnimetTime;

        public string school;
        public string trafficEstimate;

        // Behaviour factors
        public float speedMultiplier;
        public float steeringMultiplier;
        public float riskFactor;
        public float patienceFactor;
    }
    
    protected override void Awake()
    {
        Debug.Log("CarAgent.Awake() - Iniciando");
        
        rb = GetComponent<Rigidbody>();
        wheels = GetComponentsInChildren<WheelCollider>();
        rb.centerOfMass = new Vector3(0, -0.5f, 0);
        
        currentMotorForce = motorForce;
        currentSteeringResponse = 1f;
        
        baseMotorForce = motorForce;
        baseSteeringAngle = steeringAngle;
        
        if (roadGraphSystem == null)
            roadGraphSystem = FindFirstObjectByType<RoadGraphSystem>();
            
        LoadDriverProfiles();
        
        Debug.Log($"CarAgent.Awake() - Completado. Ruedas encontradas: {wheels.Length}");
    }

    private void LoadDriverProfiles()
    {
        Debug.Log("CarAgent.LoadDriverProfiles() - Cargando perfiles");
        
        driverProfiles = new List<DriverProfile>();
        
        if (driverDataFile == null)
        {
            Debug.LogError("No se ha asignado el archivo de datos de conductores!");
            return;
        }
        
        string[] lines = driverDataFile.text.Split('\n');
        Debug.Log($"CarAgent.LoadDriverProfiles() - Líneas en archivo: {lines.Length}");
        
        int loadedCount = 0;
        foreach (string line in lines)
        {
            if (string.IsNullOrEmpty(line.Trim())) continue;
            
            DriverProfile profile = ParseDriverData(line);
            if (profile != null) 
            {
                driverProfiles.Add(profile);
                loadedCount++;
            }
        }
        
        Debug.Log($"CarAgent.LoadDriverProfiles() - Se cargaron {loadedCount} perfiles de conductores");
    }

    private DriverProfile ParseDriverData(string line)
    {
        try
        {
            DriverProfile p = new DriverProfile();
            string[] parts = line.Split('|');

            foreach (string raw in parts)
            {
                string[] kv = raw.Split(':');
                if (kv.Length < 2) continue;

                string key = kv[0].Trim();
                string value = kv[1].Trim();

                switch (key)
                {
                    case "Año":
                        p.year = value;
                        break;

                    case "Número de personas":
                        int.TryParse(value, out p.peopleCount);
                        break;

                    case "Velocidad":
                        float.TryParse(value, out p.speed);
                        break;

                    case "Velocidad inicial":
                        float.TryParse(value, out p.initialSpeed);
                        break;

                    case "Velocidad máximo":
                        float.TryParse(value, out p.maxSpeed);
                        break;

                    case "Velocidad promedio":
                        float.TryParse(value, out p.averageSpeed);
                        break;

                    case "Ubicación":
                        p.location = value;
                        break;

                    case "Tipo de vía":
                        p.roadType = value;
                        break;

                    case "Zona donde vive":
                        p.livingZone = value;
                        break;

                    case "Entrada preferida":
                        p.preferredEntrance = value;
                        break;

                    case "Estilo de manejo":
                        p.drivingStyle = value;
                        break;

                    case "reacción a situación externa (%)":
                        float.TryParse(value, out p.externalReactionPercent);
                        break;

                    case "desvío (%)":
                        float.TryParse(value, out p.deviationPercent);
                        break;

                    case "cambio de velocidad (%)":
                        float.TryParse(value, out p.speedChangePercent);
                        break;

                    case "Demora estimada (min)":
                        float.TryParse(value, out p.estimatedDelay);
                        break;

                    case "Tipo de Carro":
                        p.carType = value;
                        break;

                    case "carro/moto/wawa/taxi":
                        p.vehicleType = value;
                        if (p.vehicleType.ToLower() == "a pie")
                            p.vehicleType = "wawa";
                        break;

                    case "Sincrónico/automático":
                        p.transmission = value;
                        break;

                    case "buenas/malas condiciones":
                        p.condition = value;
                        break;

                    case "Retraso":
                        p.delayReported = value;
                        break;

                    case "Viene acompañado":
                        p.accompanied = value;
                        break;

                    case "Hora de salir de la casa":
                        p.leaveHomeTime = value;
                        break;

                    case "Hora de llegar a la Unimet":
                        p.arriveUnimetTime = value;
                        break;

                    case "Colegio Integral el Ávila":
                        p.school = value;
                        break;

                    case "tráfico estimado por los encuestados":
                        p.trafficEstimate = value;
                        break;
                }
            }

            CalculateBehaviorFactors(p);
            return p;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error parseando línea: {line}\nError: {e.Message}");
            return null;
        }
    }

    private void CalculateBehaviorFactors(DriverProfile profile)
    {
        Debug.Log($"CarAgent.CalculateBehaviorFactors() - Calculando factores para perfil");
        
        // 1. Estilo de manejo básico
        if (profile.drivingStyle.Contains("Prudente"))
        {
            profile.speedMultiplier = 0.7f;
            profile.riskFactor = 0.3f;
            profile.patienceFactor = 0.8f;
            Debug.Log("CarAgent.CalculateBehaviorFactors() - Estilo: Prudente");
        }
        else if (profile.drivingStyle.Contains("Agresivo"))
        {
            profile.speedMultiplier = 1.2f;
            profile.riskFactor = 0.8f;
            profile.patienceFactor = 0.3f;
            Debug.Log("CarAgent.CalculateBehaviorFactors() - Estilo: Agresivo");
        }
        else
        {
            // Neutral / no definido
            profile.speedMultiplier = 1.0f;
            profile.riskFactor = 0.5f;
            profile.patienceFactor = 0.6f;
            Debug.Log("CarAgent.CalculateBehaviorFactors() - Estilo: Neutral");
        }

        // 2. Ajuste de velocidad por velocidad real promedio
        if (profile.averageSpeed > 0)
            profile.speedMultiplier *= Mathf.Clamp(profile.averageSpeed / 30f, 0.5f, 1.5f);

        // 3. Nueva lógica basada en porcentajes (0–100)
        float reaction = profile.externalReactionPercent;   // reacción a situación externa (%)
        float deviation = profile.deviationPercent; // desvío (%)
        float speedChange = profile.speedChangePercent;    // cambio de velocidad (%)

        // Mientras más baja la reacción => más lenta la respuesta
        profile.steeringMultiplier = Mathf.Lerp(1.2f, 0.6f, reaction / 100f);

        // Mientras más desvío, más inestable el conductor
        profile.riskFactor += deviation / 200f; // suma entre 0 y 0.5

        // Mientras más cambio de velocidad, menos paciencia
        profile.patienceFactor -= speedChange / 300f;  // resta entre ~0 y 0.33

        // Clamp valores finales
        profile.riskFactor = Mathf.Clamp01(profile.riskFactor);
        profile.patienceFactor = Mathf.Clamp01(profile.patienceFactor);
        
        Debug.Log($"CarAgent.CalculateBehaviorFactors() - Factores calculados: " +
                 $"SpeedMultiplier={profile.speedMultiplier}, " +
                 $"SteeringMultiplier={profile.steeringMultiplier}, " +
                 $"RiskFactor={profile.riskFactor}, " +
                 $"PatienceFactor={profile.patienceFactor}");
    }

    private void SelectDriverProfileByLocation(string location)
    {
        Debug.Log($"CarAgent.SelectDriverProfileByLocation() - Buscando perfiles para ubicación: {location}");
        
        var matches = driverProfiles
            .Where(p => p.location != null && p.location.ToLower().Contains(location.ToLower()))
            .ToList();

        Debug.Log($"CarAgent.SelectDriverProfileByLocation() - Coincidencias encontradas: {matches.Count}");

        if (matches.Count == 0)
        {
            Debug.LogWarning($"No hay perfiles para ubicación '{location}'. Se escogerá uno aleatorio.");
            currentDriverProfile = driverProfiles[Random.Range(0, driverProfiles.Count)];
        }
        else
        {
            currentDriverProfile = matches[Random.Range(0, matches.Count)];
        }

        Debug.Log($"CarAgent.SelectDriverProfileByLocation() - Perfil seleccionado: " +
                 $"Location={currentDriverProfile.location}, " +
                 $"DrivingStyle={currentDriverProfile.drivingStyle}");

        // Aplica factores de comportamiento
        motorForce = baseMotorForce * currentDriverProfile.speedMultiplier;
        steeringAngle = baseSteeringAngle * currentDriverProfile.steeringMultiplier;
        
        Debug.Log($"CarAgent.SelectDriverProfileByLocation() - Configuraciones aplicadas: " +
                 $"MotorForce={motorForce}, SteeringAngle={steeringAngle}");
    }

    public override void OnEpisodeBegin()
    {
        Debug.Log("CarAgent.OnEpisodeBegin() - Iniciando nuevo episodio");
        
        currentSteerInput = 0f;
        currentThrottleInput = 0f;

        // 1. Seleccionar el perfil basado en ubicación (DEL TXT)
        Debug.Log("CarAgent.OnEpisodeBegin() - Paso 1: Seleccionando perfil del conductor");
        SelectDriverProfileByLocation(initialLocation);

        // 2. Inicializar navegación si depende del perfil
        Debug.Log("CarAgent.OnEpisodeBegin() - Paso 2: Inicializando navegación gráfica");
        InitializeGraphNavigation();

        // 3. Ahora sí: respawn basado en la zona del perfil
        Debug.Log("CarAgent.OnEpisodeBegin() - Paso 3: Reiniciando coche");
        ResetCar();

        // 4. Reset de otras variables
        Debug.Log("CarAgent.OnEpisodeBegin() - Paso 4: Reiniciando otras variables");
        weatherAdaptationLevel = 0f;
        currentMotorForce = motorForce;
        currentSteeringResponse = 1f;
        
        Debug.Log("CarAgent.OnEpisodeBegin() - Episodio inicializado correctamente");
    }

    private void InitializeGraphNavigation()
    {
        Debug.Log("CarAgent.InitializeGraphNavigation() - Inicializando navegación");
        
        if (roadGraphSystem != null)
        {
            Debug.Log($"CarAgent.InitializeGraphNavigation() - RoadGraphSystem encontrado, nodos: {roadGraphSystem.roadGraph.nodes.Count}");
            
            if (roadGraphSystem.roadGraph.nodes.Count > 0)
            {
                roadGraphSystem.roadGraph.RebuildAllConnections();
                currentNode = roadGraphSystem.roadGraph.GetNearestNode(transform.position);
                Debug.Log($"CarAgent.InitializeGraphNavigation() - Nodo actual: {currentNode?.name}");

                if (currentDriverProfile != null && !string.IsNullOrEmpty(currentDriverProfile.preferredEntrance))
                {
                    Debug.Log($"CarAgent.InitializeGraphNavigation() - Configurando entrada preferida: {currentDriverProfile.preferredEntrance}");
                    SetTargetByPreferredEntrance();
                }
                else
                {
                    Debug.Log("CarAgent.InitializeGraphNavigation() - Configurando objetivo aleatorio");
                    SetRandomTarget();
                }
                
                CalculatePathToTarget();
            }
            else
            {
                Debug.LogError("CarAgent.InitializeGraphNavigation() - RoadGraphSystem no tiene nodos!");
            }
        }
        else
        {
            Debug.LogError("CarAgent.InitializeGraphNavigation() - RoadGraphSystem no asignado!");
        }
    }

    private void SetTargetByPreferredEntrance()
    {
        Debug.Log($"CarAgent.SetTargetByPreferredEntrance() - Buscando entrada: {currentDriverProfile.preferredEntrance}");
        
        if (roadGraphSystem.roadGraph.nodes.Count > 1)
        {
            // Usar el nuevo método de búsqueda que incluye nombres especiales
            var possibleTargets = roadGraphSystem.roadGraph.FindNodesByName(
                currentDriverProfile.preferredEntrance, 
                exactMatch: false  // Buscar con "contiene" no exacto
            )
            .Where(n => n != currentNode)
            .ToList();
            
            Debug.Log($"CarAgent.SetTargetByPreferredEntrance() - Objetivos posibles: {possibleTargets.Count}");
            
            if (possibleTargets.Count > 0)
            {
                targetNode = possibleTargets[Random.Range(0, possibleTargets.Count)];
                Debug.Log($"CarAgent.SetTargetByPreferredEntrance() - Objetivo establecido: {targetNode.searchName} " +
                        $"(ID: {targetNode.id}, Original: {targetNode.originalName})");
                return;
            }
            else
            {
                Debug.LogWarning($"CarAgent.SetTargetByPreferredEntrance() - No se encontró '{currentDriverProfile.preferredEntrance}'. " +
                            "Buscando nodos especiales...");
                
                // Intentar buscar nodos especiales como fallback
                var specialNodes = roadGraphSystem.roadGraph.FindSpecialNodes();
                if (specialNodes.Count > 0)
                {
                    targetNode = specialNodes[Random.Range(0, specialNodes.Count)];
                    Debug.Log($"CarAgent.SetTargetByPreferredEntrance() - Usando nodo especial: {targetNode.searchName}");
                    return;
                }
            }
        }

        Debug.Log("CarAgent.SetTargetByPreferredEntrance() - No se encontró entrada preferida, usando objetivo aleatorio");
        SetRandomTarget();
    }

    private void SetRandomTarget()
    {
        Debug.Log("CarAgent.SetRandomTarget() - Estableciendo objetivo aleatorio");
        
        if (roadGraphSystem.roadGraph.nodes.Count > 1)
        {
            var possibleTargets = roadGraphSystem.roadGraph.nodes
                .Where(n => !n.isIntersection && n != currentNode)
                .ToList();
                
            if (possibleTargets.Count > 0)
            {
                targetNode = possibleTargets[Random.Range(0, possibleTargets.Count)];
                Debug.Log($"CarAgent.SetRandomTarget() - Objetivo aleatorio (no intersección): {targetNode.name}");
                return;
            }

            do
            {
                targetNode = roadGraphSystem.roadGraph.nodes[Random.Range(0, roadGraphSystem.roadGraph.nodes.Count)];
            }
            while (targetNode == currentNode);
            
            Debug.Log($"CarAgent.SetRandomTarget() - Objetivo aleatorio: {targetNode.name}");
        }
        else
        {
            Debug.LogError("CarAgent.SetRandomTarget() - No hay suficientes nodos para establecer objetivo!");
        }
    }

    private void CalculatePathToTarget()
    {
        Debug.Log($"CarAgent.CalculatePathToTarget() - Calculando ruta de {currentNode?.name} a {targetNode?.name}");
        
        if (currentNode != null && targetNode != null)
        {
            roadGraphSystem.roadGraph.RebuildAllConnections();
            currentPath = roadGraphSystem.roadGraph.FindPath(currentNode, targetNode);
            currentPathIndex = 0;
            lastPathRecalculationTime = Time.time;

            if (currentPath != null)
            {
                Debug.Log($"CarAgent.CalculatePathToTarget() - Ruta encontrada con {currentPath.Count} nodos");
            }
            else
            {
                Debug.LogWarning("CarAgent.CalculatePathToTarget() - No se encontró ruta, estableciendo nuevo objetivo aleatorio");
                SetRandomTarget();
                CalculatePathToTarget();
            }
        }
        else
        {
            Debug.LogError($"CarAgent.CalculatePathToTarget() - Nodo actual o objetivo nulo! " +
                          $"CurrentNode={currentNode}, TargetNode={targetNode}");
        }
    }

    private void UpdateGraphNavigation()
    {
        // DEBUG: Verificar estado del grafo periódicamente
        if (Time.time - lastDebugTime > 10f) // Cada 10 segundos
        {
            Debug.Log("CarAgent.UpdateGraphNavigation() - Debug del grafo");
            if (roadGraphSystem != null)
            {
                roadGraphSystem.DebugGraphInfo(); // Llamar al método de debugging
            }
            else
            {
                Debug.LogWarning("CarAgent.UpdateGraphNavigation() - RoadGraphSystem es nulo!");
            }
            lastDebugTime = Time.time;
        }
    
        if (Time.time - lastPathRecalculationTime > pathRecalculationInterval)
        {
            Debug.Log("CarAgent.UpdateGraphNavigation() - Recalculando ruta por intervalo");
            
            if (roadGraphSystem == null)
            {
                Debug.LogError("CarAgent.UpdateGraphNavigation() - RoadGraphSystem es nulo!");
                return;
            }
            
            roadGraphSystem.roadGraph.RebuildAllConnections();
            List<GraphNode> alt = roadGraphSystem.roadGraph.FindPath(currentNode, targetNode);

            if (alt != null && alt.Count > 0)
            {
                float currentCost = CalculatePathCost(currentPath);
                float altCost = CalculatePathCost(alt);

                Debug.Log($"CarAgent.UpdateGraphNavigation() - Comparando rutas: " +
                         $"Coste actual={currentCost}, Coste alternativo={altCost}");

                if (altCost < currentCost * 0.8f)
                {
                    Debug.Log("CarAgent.UpdateGraphNavigation() - Cambiando a ruta alternativa");
                    currentPath = alt;
                    currentPathIndex = 0;
                }
            }

            lastPathRecalculationTime = Time.time;
        }

        if (currentPath != null && currentPathIndex < currentPath.Count)
        {
            Vector3 nextNodePos = currentPath[currentPathIndex].position;
            float dist = Vector3.Distance(transform.position, nextNodePos);

            Debug.Log($"CarAgent.UpdateGraphNavigation() - Distancia al siguiente nodo ({currentPathIndex}): {dist}");

            if (dist < nodeReachedDistance)
            {
                Debug.Log($"CarAgent.UpdateGraphNavigation() - Nodo {currentPath[currentPathIndex].name} alcanzado");
                currentNode = currentPath[currentPathIndex];
                currentPathIndex++;
                AddReward(0.1f);

                if (currentPathIndex >= currentPath.Count && currentNode == targetNode)
                {
                    Debug.Log("CarAgent.UpdateGraphNavigation() - ¡Objetivo alcanzado!");
                    AddReward(1f);

                    if (!string.IsNullOrEmpty(currentDriverProfile.preferredEntrance))
                    {
                        Debug.Log("CarAgent.UpdateGraphNavigation() - Estableciendo nuevo objetivo por entrada preferida");
                        SetTargetByPreferredEntrance();
                    }
                    else
                    {
                        Debug.Log("CarAgent.UpdateGraphNavigation() - Estableciendo nuevo objetivo aleatorio");
                        SetRandomTarget();
                    }

                    CalculatePathToTarget();
                }
            }

            UpdateCurrentEdge();
        }
        else
        {
            Debug.LogWarning($"CarAgent.UpdateGraphNavigation() - Sin ruta válida. " +
                           $"CurrentPath={currentPath}, CurrentPathIndex={currentPathIndex}, PathCount={currentPath?.Count}");
        }
    }

    private float CalculatePathCost(List<GraphNode> path)
    {
        if (path == null || path.Count < 2) 
        {
            Debug.LogWarning("CarAgent.CalculatePathCost() - Ruta inválida");
            return Mathf.Infinity;
        }

        float cost = 0f;

        for (int i = 0; i < path.Count - 1; i++)
        {
            GraphNode A = path[i];
            GraphNode B = path[i + 1];

            foreach (GraphEdge e in A.edges)
            {
                if (e.endNodeId == B.id)
                {
                    cost += e.TotalCost;
                    break;
                }
            }
        }

        Debug.Log($"CarAgent.CalculatePathCost() - Coste calculado: {cost}");
        return cost;
    }

    private void UpdateCurrentEdge()
    {
        if (currentPath != null && currentPathIndex > 0 && currentPathIndex < currentPath.Count)
        {
            GraphNode prev = currentPath[currentPathIndex - 1];
            GraphNode next = currentPath[currentPathIndex];

            Debug.Log($"CarAgent.UpdateCurrentEdge() - Buscando arista entre {prev.name} y {next.name}");

            foreach (GraphEdge e in prev.edges)
            {
                if (e.endNodeId == next.id)
                {
                    if (e.endNode == null)
                    {
                        Debug.Log("CarAgent.UpdateCurrentEdge() - Reconstruyendo conexiones de arista");
                        e.RebuildConnections(roadGraphSystem.roadGraph);
                    }

                    currentEdge = e;
                    Debug.Log($"CarAgent.UpdateCurrentEdge() - Arista actual actualizada: {currentEdge.startNode.name} -> {currentEdge.endNode.name}");
                    break;
                }
            }
        }
        else
        {
            Debug.LogWarning("CarAgent.UpdateCurrentEdge() - No se puede actualizar arista actual");
        }
    }

    private void UpdateWeatherAdaptation()
    {
        if (WeatherManager.Instance == null) 
        {
            Debug.LogWarning("CarAgent.UpdateWeatherAdaptation() - WeatherManager no encontrado");
            return;
        }

        Debug.Log($"CarAgent.UpdateWeatherAdaptation() - Lloviendo: {WeatherManager.Instance.isRaining}, " +
                 $"Intensidad: {WeatherManager.Instance.rainIntensity}");

        if (WeatherManager.Instance.isRaining)
        {
            weatherAdaptationLevel = Mathf.Clamp01(weatherAdaptationLevel + Time.deltaTime * rainSpeedAdaptationRate);

            float targetMF = motorForce * WeatherManager.Instance.GetMotorForceMultiplier();
            float targetST = WeatherManager.Instance.GetSteeringMultiplier();

            currentMotorForce = Mathf.Lerp(motorForce, targetMF, weatherAdaptationLevel);
            currentSteeringResponse = Mathf.Lerp(1f, targetST, weatherAdaptationLevel);

            Debug.Log($"CarAgent.UpdateWeatherAdaptation() - Adaptación a lluvia: {weatherAdaptationLevel}, " +
                     $"MotorForce: {currentMotorForce}, SteeringResponse: {currentSteeringResponse}");

            foreach (WheelCollider w in wheels)
            {
                WheelFrictionCurve f = w.forwardFriction;
                f.stiffness = WeatherManager.Instance.GetFrictionFactor();
                w.forwardFriction = f;

                WheelFrictionCurve s = w.sidewaysFriction;
                s.stiffness = WeatherManager.Instance.GetFrictionFactor();
                w.sidewaysFriction = s;
            }

            AddReward(0.001f * weatherAdaptationLevel);
        }
        else
        {
            weatherAdaptationLevel = Mathf.Clamp01(weatherAdaptationLevel - Time.deltaTime * rainSpeedAdaptationRate);

            currentMotorForce = Mathf.Lerp(
                motorForce * WeatherManager.Instance.GetMotorForceMultiplier(),
                motorForce,
                weatherAdaptationLevel
            );

            currentSteeringResponse = Mathf.Lerp(
                WeatherManager.Instance.GetSteeringMultiplier(),
                1f,
                weatherAdaptationLevel
            );

            Debug.Log($"CarAgent.UpdateWeatherAdaptation() - Adaptación reduciendo: {weatherAdaptationLevel}");

            foreach (WheelCollider w in wheels)
            {
                WheelFrictionCurve f = w.forwardFriction;
                f.stiffness = 1f;
                w.forwardFriction = f;

                WheelFrictionCurve s = w.sidewaysFriction;
                s.stiffness = 1f;
                w.sidewaysFriction = s;
            }
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Debug.Log("CarAgent.CollectObservations() - Recolectando observaciones");
        
        float velocity = rb.linearVelocity.magnitude / 20f;
        float forwardDot = Vector3.Dot(transform.forward, Vector3.forward);
        
        sensor.AddObservation(velocity);
        sensor.AddObservation(forwardDot);
        
        Debug.Log($"CarAgent.CollectObservations() - Observaciones básicas: Velocidad={velocity}, ForwardDot={forwardDot}");

        float angleStep = rayCount > 1 ? raySpreadAngle / (rayCount - 1) : 0f;

        for (int i = 0; i < rayCount; i++)
        {
            float angle = (rayCount == 1)
                ? 0f
                : (-raySpreadAngle / 2 + i * angleStep);

            Vector3 dir = Quaternion.Euler(0, angle, 0) * transform.forward;

            float adjDist = raycastDistance;
            if (WeatherManager.Instance != null)
                adjDist *= WeatherManager.Instance.GetVisibilityFactor();

            if (Physics.Raycast(transform.position, dir, out RaycastHit hit, adjDist, roadLayer))
            {
                sensor.AddObservation(hit.distance / adjDist);
                Debug.DrawRay(transform.position, dir * hit.distance, Color.green);
                Debug.Log($"CarAgent.CollectObservations() - Rayo {i}: Impacto a distancia {hit.distance}");
            }
            else
            {
                sensor.AddObservation(0f);
                Debug.DrawRay(transform.position, dir * adjDist, Color.red);
                Debug.Log($"CarAgent.CollectObservations() - Rayo {i}: Sin impacto");
            }
        }

        sensor.AddObservation(transform.up.y);

        foreach (var wheel in wheels) 
        {
            sensor.AddObservation(wheel.isGrounded);
            Debug.Log($"CarAgent.CollectObservations() - Rueda {wheel.name}: Grounded={wheel.isGrounded}");
        }

        if (currentPath != null && currentPathIndex < currentPath.Count)
        {
            Vector3 nextDir = (currentPath[currentPathIndex].position - transform.position).normalized;
            float dotToNext = Vector3.Dot(transform.forward, nextDir);
            float distToNext = Vector3.Distance(transform.position, currentPath[currentPathIndex].position) / 50f;
            
            sensor.AddObservation(dotToNext);
            sensor.AddObservation(distToNext);
            
            Debug.Log($"CarAgent.CollectObservations() - Navegación: DotToNext={dotToNext}, DistToNext={distToNext}");
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            Debug.LogWarning("CarAgent.CollectObservations() - Sin ruta válida para navegación");
        }

        if (currentEdge != null)
        {
            float trafficCost = currentEdge.trafficCost / 5f;
            sensor.AddObservation(trafficCost);
            Debug.Log($"CarAgent.CollectObservations() - Coste de tráfico: {trafficCost}");
        }
        else
        {
            sensor.AddObservation(0f);
            Debug.Log("CarAgent.CollectObservations() - Sin arista actual");
        }

        if (WeatherManager.Instance != null)
        {
            sensor.AddObservation(WeatherManager.Instance.isRaining ? 1f : 0f);
            sensor.AddObservation(WeatherManager.Instance.rainIntensity);
            sensor.AddObservation(weatherAdaptationLevel);
            
            Debug.Log($"CarAgent.CollectObservations() - Clima: Raining={WeatherManager.Instance.isRaining}, " +
                     $"Intensity={WeatherManager.Instance.rainIntensity}, " +
                     $"Adaptation={weatherAdaptationLevel}");
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            Debug.Log("CarAgent.CollectObservations() - Sin WeatherManager");
        }

        if (currentDriverProfile != null)
        {
            sensor.AddObservation(currentDriverProfile.speedMultiplier);
            sensor.AddObservation(currentDriverProfile.riskFactor);
            sensor.AddObservation(currentDriverProfile.patienceFactor);
            
            Debug.Log($"CarAgent.CollectObservations() - Perfil: SpeedMultiplier={currentDriverProfile.speedMultiplier}, " +
                     $"RiskFactor={currentDriverProfile.riskFactor}, " +
                     $"PatienceFactor={currentDriverProfile.patienceFactor}");
        }
        else
        {
            sensor.AddObservation(1f);
            sensor.AddObservation(0.5f);
            sensor.AddObservation(0.5f);
            Debug.LogError("CarAgent.CollectObservations() - Sin perfil de conductor!");
        }
        
        Debug.Log($"CarAgent.CollectObservations() - Total observaciones: {sensor.ObservationSize()}");
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        Debug.Log("CarAgent.OnActionReceived() - Recibiendo acciones del modelo");
        
        float steerInput = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float throttleInput = Mathf.Clamp(actions.ContinuousActions[1], 0f, 1f);
        
        Debug.Log($"CarAgent.OnActionReceived() - Acciones crudas: Steer={steerInput}, Throttle={throttleInput}");
        
        currentSteerInput = Mathf.Lerp(currentSteerInput, steerInput, 0.1f);
        currentThrottleInput = Mathf.Lerp(currentThrottleInput, throttleInput, 0.15f);
        
        Debug.Log($"CarAgent.OnActionReceived() - Acciones suavizadas: Steer={currentSteerInput}, Throttle={currentThrottleInput}");

        //UpdateWeatherAdaptation();
        
        ApplySteering(currentSteerInput);
        ApplyMotor(currentThrottleInput);
        
        //CalculateRewards();
        //UpdateGraphNavigation();
    }

    private void CalculateRewards()
    {
        float forwardVelocity = Vector3.Dot(transform.forward, rb.linearVelocity);
        float velocityReward = 0.001f + forwardVelocity * 0.01f;
        AddReward(velocityReward);
        
        Debug.Log($"CarAgent.CalculateRewards() - Recompensa por velocidad: {velocityReward}, ForwardVelocity={forwardVelocity}");

        float steerChangePenalty = -Mathf.Abs(currentSteerInput - previousSteerInput) * 0.005f;
        AddReward(steerChangePenalty);
        Debug.Log($"CarAgent.CalculateRewards() - Penalización por cambio de dirección: {steerChangePenalty}");
        previousSteerInput = currentSteerInput;

        if (!IsGrounded()) 
        {
            AddReward(-0.05f);
            Debug.Log("CarAgent.CalculateRewards() - Penalización por no estar en suelo: -0.05");
        }
        
        if (IsTilted()) 
        {
            AddReward(-0.01f);
            Debug.Log("CarAgent.CalculateRewards() - Penalización por inclinación: -0.01");
        }

        if (currentPath != null && currentPathIndex < currentPath.Count)
        {
            Vector3 toNode = (currentPath[currentPathIndex].position - transform.position).normalized;
            float directionReward = Vector3.Dot(transform.forward, toNode) * 0.01f;
            AddReward(directionReward);
            Debug.Log($"CarAgent.CalculateRewards() - Recompensa por dirección: {directionReward}");
        }

        if (WeatherManager.Instance != null && WeatherManager.Instance.isRaining)
        {
            float lateral = Mathf.Abs(Vector3.Dot(transform.right, rb.linearVelocity));
            Debug.Log($"CarAgent.CalculateRewards() - Velocidad lateral en lluvia: {lateral}");

            if (lateral > 2f)
            {
                float lateralPenalty = -0.02f * lateral * WeatherManager.Instance.rainIntensity;
                AddReward(lateralPenalty);
                Debug.Log($"CarAgent.CalculateRewards() - Penalización por derrape en lluvia: {lateralPenalty}");
            }

            if (Mathf.Abs(currentSteerInput) < 0.2f && forwardVelocity > 5f)
            {
                float steadyReward = 0.005f * WeatherManager.Instance.rainIntensity;
                AddReward(steadyReward);
                Debug.Log($"CarAgent.CalculateRewards() - Recompensa por conducción estable en lluvia: {steadyReward}");
            }
        }

        if (currentDriverProfile != null)
        {
            if (currentDriverProfile.drivingStyle.Contains("Prudente") ||
                currentDriverProfile.drivingStyle.Contains("Lento"))
            {
                if (forwardVelocity < 15f && Mathf.Abs(currentSteerInput) < 0.3f)
                {
                    float patientReward = 0.002f * currentDriverProfile.patienceFactor;
                    AddReward(patientReward);
                    Debug.Log($"CarAgent.CalculateRewards() - Recompensa por conducción prudente: {patientReward}");
                }
            }
            else if (currentDriverProfile.drivingStyle.Contains("Agresivo"))
            {
                if (forwardVelocity > 20f)
                {
                    float aggressiveReward = 0.001f * currentDriverProfile.riskFactor;
                    AddReward(aggressiveReward);
                    Debug.Log($"CarAgent.CalculateRewards() - Recompensa por conducción agresiva: {aggressiveReward}");
                }
            }
        }
        
        Debug.Log($"CarAgent.CalculateRewards() - Recompensa acumulada actual: {GetCumulativeReward()}");
    }

    private void ApplySteering(float steerInput)
    {
        Debug.Log($"CarAgent.ApplySteering() - Aplicando dirección: {steerInput}");
        
        foreach (var wheel in wheels)
        {
            if (wheel.transform.localPosition.z > 0)
            {
                float angle = steerInput * steeringAngle * currentSteeringResponse;
                wheel.steerAngle = angle;
                Debug.Log($"CarAgent.ApplySteering() - Rueda {wheel.name}: Ángulo={angle}");
            }
        }
    }

    private void ApplyMotor(float throttleInput)
    {
        Debug.Log($"CarAgent.ApplyMotor() - Aplicando aceleración: {throttleInput}");
        
        foreach (var wheel in wheels)
        {
            if (wheel.transform.localPosition.z < 0)
            {
                float torque = throttleInput * currentMotorForce;
                wheel.motorTorque = torque;
                Debug.Log($"CarAgent.ApplyMotor() - Rueda {wheel.name}: Torque={torque}");
            }
        }
    }

    private bool IsGrounded()
    {
        bool allGrounded = true;
        foreach (var wheel in wheels)
        {
            if (!wheel.isGrounded)
            {
                allGrounded = false;
                Debug.Log($"CarAgent.IsGrounded() - Rueda {wheel.name} NO está en suelo");
            }
        }
        
        Debug.Log($"CarAgent.IsGrounded() - Todas las ruedas en suelo: {allGrounded}");
        return allGrounded;
    }

    private bool IsTilted()
    {
        float angle = Vector3.Angle(transform.up, Vector3.up);
        bool tilted = angle > 45f;
        
        Debug.Log($"CarAgent.IsTilted() - Ángulo de inclinación: {angle}, ¿Inclinado?: {tilted}");
        return tilted;
    }

    private void FixedUpdate()
    {
        if (StepCount > MaxStep)
        {
            Debug.Log($"CarAgent.FixedUpdate() - Máximo de pasos alcanzado ({StepCount}/{MaxStep}). Finalizando episodio.");
            AddReward(-1f);
            EndEpisode();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"CarAgent.OnCollisionEnter() - Colisión con: {collision.gameObject.name}, Tag: {collision.gameObject.tag}");
        
        if (collision.gameObject.CompareTag("Obstacle") || collision.gameObject.CompareTag("NPC"))
        {
            Debug.Log($"CarAgent.OnCollisionEnter() - Colisión con obstáculo/NPC. Penalizando.");
            AddReward(-2f);
            EndEpisode();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"CarAgent.OnTriggerEnter() - Trigger con: {other.gameObject.name}, Tag: {other.gameObject.tag}");
        
        if (other.CompareTag("Boundary"))
        {
            Debug.Log("CarAgent.OnTriggerEnter() - Salida de límites. Penalizando.");
            AddReward(-2f);
            EndEpisode();
        }
        else if (other.CompareTag("FinishLine") && !hasFinished)
        {
            Debug.Log("CarAgent.OnTriggerEnter() - ¡Línea de meta alcanzada! Recompensando.");
            AddReward(5f);
            hasFinished = true;
            EndEpisode();
        }
    }

    private void ResetCar()
    {
        Debug.Log("CarAgent.ResetCar() - Reiniciando coche");
        
        Transform spawn = resetPosition; // fallback por si no hay SpawnManager

        if (spawnManager != null && currentDriverProfile != null)
        {
            // Primero intenta usar la zona donde vive del TXT
            string zone = currentDriverProfile.livingZone;

            // Si no existe, usa ubicación
            if (string.IsNullOrEmpty(zone))
                zone = currentDriverProfile.location;

            Debug.Log($"CarAgent.ResetCar() - Buscando punto de spawn para zona: {zone}");

            // Si también está vacío, usa fallback
            if (!string.IsNullOrEmpty(zone))
                spawn = spawnManager.GetSpawnPointForLocation(zone);
        }

        Debug.Log($"CarAgent.ResetCar() - Spawn point seleccionado: {spawn?.name}");

        // Reposicionar coche
        transform.position = spawn.position;
        transform.rotation = spawn.rotation;
        Debug.Log($"CarAgent.ResetCar() - Posición reiniciada a: {transform.position}");

        // Reset de física
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        hasFinished = false;

        // Reiniciar ruedas
        foreach (var wheel in wheels)
        {
            wheel.motorTorque = 0f;
            wheel.steerAngle = 0f;
            wheel.brakeTorque = 0f;
        }
        
        Debug.Log("CarAgent.ResetCar() - Coche reiniciado correctamente");
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;

        for (int i = 0; i < rayCount; i++)
        {
            float angle = (rayCount == 1)
                ? 0f
                : (-raySpreadAngle / 2 + i * (raySpreadAngle / (rayCount - 1)));

            Vector3 dir = Quaternion.Euler(0, angle, 0) * transform.forward;

            float adjDist = raycastDistance;
            if (WeatherManager.Instance != null)
                adjDist *= WeatherManager.Instance.GetVisibilityFactor();

            Gizmos.DrawRay(transform.position, dir * adjDist);
        }

        if (currentPath != null && currentPath.Count > 0)
        {
            Gizmos.color = Color.magenta;

            for (int i = currentPathIndex; i < currentPath.Count - 1; i++)
            {
                Gizmos.DrawLine(currentPath[i].position, currentPath[i + 1].position);
                Gizmos.DrawSphere(currentPath[i].position, 0.7f);
            }

            if (currentPathIndex < currentPath.Count)
                Gizmos.DrawSphere(currentPath[currentPathIndex].position, 1f);
        }
    }

    public void SetInitialLocation(string newLocation)
    {
        Debug.Log($"CarAgent.SetInitialLocation() - Cambiando ubicación inicial de {initialLocation} a {newLocation}");
        initialLocation = newLocation;
    }

    public DriverProfile GetCurrentDriverProfile()
    {
        return currentDriverProfile;
    }
}