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
                if (profile != null) driverProfiles.Add(profile);
            }
            
            Debug.Log($"Se cargaron {driverProfiles.Count} perfiles de conductores");
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
            // 1. Estilo de manejo básico
            if (profile.drivingStyle.Contains("Prudente"))
            {
                profile.speedMultiplier = 0.7f;
                profile.riskFactor = 0.3f;
                profile.patienceFactor = 0.8f;
            }
            else if (profile.drivingStyle.Contains("Agresivo"))
            {
                profile.speedMultiplier = 1.2f;
                profile.riskFactor = 0.8f;
                profile.patienceFactor = 0.3f;
            }
            else
            {
                // Neutral / no definido
                profile.speedMultiplier = 1.0f;
                profile.riskFactor = 0.5f;
                profile.patienceFactor = 0.6f;
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
        }

        private void SelectDriverProfileByLocation(string location)
        {
            var matches = driverProfiles
                .Where(p => p.location != null && p.location.ToLower().Contains(location.ToLower()))
                .ToList();

            if (matches.Count == 0)
            {
                Debug.LogWarning($"No hay perfiles para ubicación '{location}'. Se escogerá uno aleatorio.");
                currentDriverProfile = driverProfiles[Random.Range(0, driverProfiles.Count)];
            }
            else
            {
                currentDriverProfile = matches[Random.Range(0, matches.Count)];
            }

            // Aplica factores de comportamiento
            motorForce = baseMotorForce * currentDriverProfile.speedMultiplier;
            steeringAngle = baseSteeringAngle * currentDriverProfile.steeringMultiplier;
        }


        public override void OnEpisodeBegin()
        {
            currentSteerInput = 0f;
            currentThrottleInput = 0f;

            // 1. Seleccionar el perfil basado en ubicación (DEL TXT)
            SelectDriverProfileByLocation(initialLocation);

            // 2. Inicializar navegación si depende del perfil
            InitializeGraphNavigation();

            // 3. Ahora sí: respawn basado en la zona del perfil
            ResetCar();

            // 4. Reset de otras variables
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

                if (currentDriverProfile != null && !string.IsNullOrEmpty(currentDriverProfile.preferredEntrance))
                    SetTargetByPreferredEntrance();
                else
                    SetRandomTarget();
                
                CalculatePathToTarget();
            }
        }

        private void SetTargetByPreferredEntrance()
        {
            if (roadGraphSystem.roadGraph.nodes.Count > 1)
            {
                var possibleTargets = roadGraphSystem.roadGraph.nodes
                    .Where(n => n.name.Contains(currentDriverProfile.preferredEntrance) && n != currentNode)
                    .ToList();
                
                if (possibleTargets.Count > 0)
                {
                    targetNode = possibleTargets[Random.Range(0, possibleTargets.Count)];
                    return;
                }
            }

            SetRandomTarget();
        }

        private void SetRandomTarget()
        {
            if (roadGraphSystem.roadGraph.nodes.Count > 1)
            {
                var possibleTargets = roadGraphSystem.roadGraph.nodes
                    .Where(n => !n.isIntersection && n != currentNode)
                    .ToList();
                    
                if (possibleTargets.Count > 0)
                {
                    targetNode = possibleTargets[Random.Range(0, possibleTargets.Count)];
                    return;
                }

                do
                {
                    targetNode = roadGraphSystem.roadGraph.nodes[Random.Range(0, roadGraphSystem.roadGraph.nodes.Count)];
                }
                while (targetNode == currentNode);
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
                List<GraphNode> alt = roadGraphSystem.roadGraph.FindPath(currentNode, targetNode);

                if (alt != null && alt.Count > 0)
                {
                    float currentCost = CalculatePathCost(currentPath);
                    float altCost = CalculatePathCost(alt);

                    if (altCost < currentCost * 0.8f)
                    {
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

                if (dist < nodeReachedDistance)
                {
                    currentNode = currentPath[currentPathIndex];
                    currentPathIndex++;
                    AddReward(0.1f);

                    if (currentPathIndex >= currentPath.Count && currentNode == targetNode)
                    {
                        AddReward(1f);

                        if (!string.IsNullOrEmpty(currentDriverProfile.preferredEntrance))
                            SetTargetByPreferredEntrance();
                        else
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

            return cost;
        }

        private void UpdateCurrentEdge()
        {
            if (currentPath != null && currentPathIndex > 0 && currentPathIndex < currentPath.Count)
            {
                GraphNode prev = currentPath[currentPathIndex - 1];
                GraphNode next = currentPath[currentPathIndex];

                foreach (GraphEdge e in prev.edges)
                {
                    if (e.endNodeId == next.id)
                    {
                        if (e.endNode == null)
                            e.RebuildConnections(roadGraphSystem.roadGraph);

                        currentEdge = e;
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
                weatherAdaptationLevel = Mathf.Clamp01(weatherAdaptationLevel + Time.deltaTime * rainSpeedAdaptationRate);

                float targetMF = motorForce * WeatherManager.Instance.GetMotorForceMultiplier();
                float targetST = WeatherManager.Instance.GetSteeringMultiplier();

                currentMotorForce = Mathf.Lerp(motorForce, targetMF, weatherAdaptationLevel);
                currentSteeringResponse = Mathf.Lerp(1f, targetST, weatherAdaptationLevel);

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
            sensor.AddObservation(rb.linearVelocity.magnitude / 20f);
            sensor.AddObservation(Vector3.Dot(transform.forward, Vector3.forward));

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
                }
                else
                {
                    sensor.AddObservation(0f);
                    Debug.DrawRay(transform.position, dir * adjDist, Color.red);
                }
            }

            sensor.AddObservation(transform.up.y);

            foreach (var wheel in wheels) sensor.AddObservation(wheel.isGrounded);

            if (currentPath != null && currentPathIndex < currentPath.Count)
            {
                Vector3 nextDir = (currentPath[currentPathIndex].position - transform.position).normalized;
                sensor.AddObservation(Vector3.Dot(transform.forward, nextDir));
                sensor.AddObservation(Vector3.Distance(transform.position, currentPath[currentPathIndex].position) / 50f);
            }
            else
            {
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
            }

            if (currentEdge != null)
                sensor.AddObservation(currentEdge.trafficCost / 5f);
            else
                sensor.AddObservation(0f);

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

            if (currentDriverProfile != null)
            {
                sensor.AddObservation(currentDriverProfile.speedMultiplier);
                sensor.AddObservation(currentDriverProfile.riskFactor);
                sensor.AddObservation(currentDriverProfile.patienceFactor);
            }
            else
            {
                sensor.AddObservation(1f);
                sensor.AddObservation(0.5f);
                sensor.AddObservation(0.5f);
            }
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            float steerInput = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
            float throttleInput = Mathf.Clamp(actions.ContinuousActions[1], 0f, 1f);
            
            currentSteerInput = Mathf.Lerp(currentSteerInput, steerInput, 0.1f);
            currentThrottleInput = Mathf.Lerp(currentThrottleInput, throttleInput, 0.15f);

            UpdateWeatherAdaptation();
            
            ApplySteering(currentSteerInput);
            ApplyMotor(currentThrottleInput);
            
            CalculateRewards();
            UpdateGraphNavigation();
        }

        private void CalculateRewards()
        {
            float forwardVelocity = Vector3.Dot(transform.forward, rb.linearVelocity);
            AddReward(0.001f + forwardVelocity * 0.01f);

            AddReward(-Mathf.Abs(currentSteerInput - previousSteerInput) * 0.005f);
            previousSteerInput = currentSteerInput;

            if (!IsGrounded()) AddReward(-0.05f);
            if (IsTilted()) AddReward(-0.01f);

            if (currentPath != null && currentPathIndex < currentPath.Count)
            {
                Vector3 toNode = (currentPath[currentPathIndex].position - transform.position).normalized;
                AddReward(Vector3.Dot(transform.forward, toNode) * 0.01f);
            }

            if (WeatherManager.Instance != null && WeatherManager.Instance.isRaining)
            {
                float lateral = Mathf.Abs(Vector3.Dot(transform.right, rb.linearVelocity));

                if (lateral > 2f)
                    AddReward(-0.02f * lateral * WeatherManager.Instance.rainIntensity);

                if (Mathf.Abs(currentSteerInput) < 0.2f && forwardVelocity > 5f)
                    AddReward(0.005f * WeatherManager.Instance.rainIntensity);
            }

            if (currentDriverProfile != null)
            {
                if (currentDriverProfile.drivingStyle.Contains("Prudente") ||
                    currentDriverProfile.drivingStyle.Contains("Lento"))
                {
                    if (forwardVelocity < 15f && Mathf.Abs(currentSteerInput) < 0.3f)
                        AddReward(0.002f * currentDriverProfile.patienceFactor);
                }
                else if (currentDriverProfile.drivingStyle.Contains("Agresivo"))
                {
                    if (forwardVelocity > 20f)
                        AddReward(0.001f * currentDriverProfile.riskFactor);
                }
            }
        }

        private void ApplySteering(float steerInput)
        {
            foreach (var wheel in wheels)
            {
                if (wheel.transform.localPosition.z > 0)
                    wheel.steerAngle = steerInput * steeringAngle * currentSteeringResponse;
            }
        }

        private void ApplyMotor(float throttleInput)
        {
            foreach (var wheel in wheels)
            {
                if (wheel.transform.localPosition.z < 0)
                    wheel.motorTorque = throttleInput * currentMotorForce;
            }
        }

        private bool IsGrounded()
        {
            foreach (var wheel in wheels)
            {
                if (!wheel.isGrounded) return false;
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
            Transform spawn = resetPosition; // fallback por si no hay SpawnManager

            if (spawnManager != null && currentDriverProfile != null)
            {
                // Primero intenta usar la zona donde vive del TXT
                string zone = currentDriverProfile.livingZone;

                // Si no existe, usa ubicación
                if (string.IsNullOrEmpty(zone))
                    zone = currentDriverProfile.location;

                // Si también está vacío, usa fallback
                if (!string.IsNullOrEmpty(zone))
                    spawn = spawnManager.GetSpawnPointForZone(zone);
            }

            // Reposicionar coche
            transform.position = spawn.position;
            transform.rotation = spawn.rotation;

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
            initialLocation = newLocation;
        }

        public DriverProfile GetCurrentDriverProfile()
        {
            return currentDriverProfile;
        }
    }
