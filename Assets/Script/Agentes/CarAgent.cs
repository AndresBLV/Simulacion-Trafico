using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

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

    [Header("Weather Adaptation")]
    public float rainSpeedAdaptationRate = 0.1f;
    public float rainSteeringAdaptationRate = 0.05f;

    [Header("Driver Data Configuration")]
    public TextAsset driverDataFile;
    public string initialLocation = "La Guaira";

    private Rigidbody rb;
    private WheelCollider[] wheels;
    private bool hasFinished;
    private float currentSteerInput;
    private float currentThrottleInput;

    private float currentMotorForce;
    private float currentSteeringResponse;
    private float weatherAdaptationLevel;

    private DriverProfile currentDriverProfile;
    private List<DriverProfile> driverProfiles;
    private float baseMotorForce;
    private float baseSteeringAngle;

    [System.Serializable]
    public class DriverProfile
    {
        public string year;
        public int peopleCount;
        public float speed, initialSpeed, maxSpeed, averageSpeed;
        public string location, roadType, livingZone;
        public string preferredEntrance, drivingStyle;
        public float externalReactionPercent, deviationPercent, speedChangePercent;
        public float estimatedDelay;
        public string carType, vehicleType, transmission, condition;
        public string delayReported, accompanied, leaveHomeTime, arriveUnimetTime;
        public string school, trafficEstimate;
        public float speedMultiplier, steeringMultiplier, riskFactor, patienceFactor;
    }

    protected override void Awake()
    {
        Debug.Log("Awake CarAgent ejecutado");
        
        rb = GetComponent<Rigidbody>();
        if(rb == null) Debug.LogWarning("No hay Rigidbody asignado!");
        
        wheels = GetComponentsInChildren<WheelCollider>();
        Debug.Log($"Ruedas encontradas: {wheels.Length}");
        
        rb.centerOfMass = new Vector3(0, -0.5f, 0);
        
        currentMotorForce = motorForce;
        currentSteeringResponse = 1f;
        baseMotorForce = motorForce;
        baseSteeringAngle = steeringAngle;
    }

    private void SnapCarAboveGround()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 10f, roadLayer))
        {
            // Altura deseada desde el suelo hasta el centro del carro
            float heightAboveGround = 0.2f; 

            Vector3 targetPos = hit.point + Vector3.up * heightAboveGround;
            transform.position = targetPos;

            // Opcional: alinear el carro con la normal del terreno
            transform.rotation = Quaternion.FromToRotation(transform.up, hit.normal) * transform.rotation;
        }
    }

    private void LoadDriverProfiles()
    {
        driverProfiles = new List<DriverProfile>();
        if (driverDataFile == null)
        {
            Debug.LogError("Archivo de datos de conductores no asignado");
            return;
        }

        var lines = driverDataFile.text.Split('\n');
        int loadedCount = 0;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var profile = ParseDriverData(line);
            if (profile != null)
            {
                driverProfiles.Add(profile);
                loadedCount++;
            }
        }

        Debug.Log($"Se cargaron {loadedCount} perfiles de conductores");
    }

    private DriverProfile ParseDriverData(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        var profile = new DriverProfile();
        foreach (var raw in line.Split('|'))
        {
            var kv = raw.Split(':');
            if (kv.Length < 2) continue;
            var key = kv[0].Trim(); 
            var value = kv[1].Trim();
            switch (key)
            {
                case "Año": profile.year = value; break;
                case "Número de personas": int.TryParse(value, out profile.peopleCount); break;
                case "Velocidad": float.TryParse(value, out profile.speed); break;
                case "Velocidad inicial": float.TryParse(value, out profile.initialSpeed); break;
                case "Velocidad máximo": float.TryParse(value, out profile.maxSpeed); break;
                case "Velocidad promedio": float.TryParse(value, out profile.averageSpeed); break;
                case "Ubicación": profile.location = value; break;
                case "Tipo de vía": profile.roadType = value; break;
                case "Zona donde vive": profile.livingZone = value; break;
                case "Entrada preferida": profile.preferredEntrance = value; break;
                case "Estilo de manejo": profile.drivingStyle = value; break;
                case "reacción a situación externa (%)": float.TryParse(value, out profile.externalReactionPercent); break;
                case "desvío (%)": float.TryParse(value, out profile.deviationPercent); break;
                case "cambio de velocidad (%)": float.TryParse(value, out profile.speedChangePercent); break;
                case "Demora estimada (min)": float.TryParse(value, out profile.estimatedDelay); break;
                case "Tipo de Carro": profile.carType = value; break;
                case "carro/moto/wawa/taxi": profile.vehicleType = value.ToLower() == "a pie" ? "wawa" : value; break;
                case "Sincrónico/automático": profile.transmission = value; break;
                case "buenas/malas condiciones": profile.condition = value; break;
                case "Retraso": profile.delayReported = value; break;
                case "Viene acompañado": profile.accompanied = value; break;
                case "Hora de salir de la casa": profile.leaveHomeTime = value; break;
                case "Hora de llegar a la Unimet": profile.arriveUnimetTime = value; break;
                case "Colegio Integral el Ávila": profile.school = value; break;
                case "tráfico estimado por los encuestados": profile.trafficEstimate = value; break;
            }
        }
        CalculateBehaviorFactors(profile);
        return profile;
    }

    private void CalculateBehaviorFactors(DriverProfile p)
    {
        if (p.drivingStyle.Contains("Prudente")) { p.speedMultiplier = 0.7f; p.riskFactor = 0.3f; p.patienceFactor = 0.8f; }
        else if (p.drivingStyle.Contains("Agresivo")) { p.speedMultiplier = 1.2f; p.riskFactor = 0.8f; p.patienceFactor = 0.3f; }
        else { p.speedMultiplier = 1f; p.riskFactor = 0.5f; p.patienceFactor = 0.6f; }

        if (p.averageSpeed > 0) p.speedMultiplier *= Mathf.Clamp(p.averageSpeed / 30f, 0.5f, 1.5f);
        p.steeringMultiplier = Mathf.Lerp(1.2f, 0.6f, p.externalReactionPercent / 100f);
        p.riskFactor = Mathf.Clamp01(p.riskFactor + p.deviationPercent / 200f);
        p.patienceFactor = Mathf.Clamp01(p.patienceFactor - p.speedChangePercent / 300f);
    }

    private void SelectDriverProfileByLocation(string location)
    {
        var matches = driverProfiles.Where(p => !string.IsNullOrEmpty(p.location) && p.location.ToLower().Contains(location.ToLower())).ToList();
        currentDriverProfile = matches.Count > 0 ? matches[Random.Range(0, matches.Count)] : driverProfiles[Random.Range(0, driverProfiles.Count)];

        currentMotorForce = baseMotorForce * currentDriverProfile.speedMultiplier;
        steeringAngle = baseSteeringAngle * currentDriverProfile.steeringMultiplier;
    }

public override void OnEpisodeBegin()
{

    currentSteerInput = 0f;
    currentThrottleInput = 0f;

    // Reset de carro y Rigidbody
    ResetCar();

    SnapCarAboveGround();

    // Log WeatherManager
    if (WeatherManager.Instance != null)
    {
        currentMotorForce = motorForce * WeatherManager.Instance.GetMotorForceMultiplier();
        currentSteeringResponse = WeatherManager.Instance.GetSteeringMultiplier();
    }
    else
    {
        currentMotorForce = motorForce;
        currentSteeringResponse = 1f;
    }

    // Impulso de prueba para debug
    rb.AddForce(transform.forward * 5f, ForceMode.VelocityChange);
}

    private void UpdateWeatherAdaptation()
    {
        if (WeatherManager.Instance == null) return;

        bool raining = WeatherManager.Instance.isRaining;
        weatherAdaptationLevel = Mathf.Clamp01(weatherAdaptationLevel + Time.deltaTime * (raining ? rainSpeedAdaptationRate : -rainSpeedAdaptationRate));

        float targetMotor = raining ? motorForce * WeatherManager.Instance.GetMotorForceMultiplier() : motorForce;
        float targetSteer = raining ? WeatherManager.Instance.GetSteeringMultiplier() : 1f;

        currentMotorForce = Mathf.Lerp(motorForce, targetMotor, weatherAdaptationLevel);
        currentSteeringResponse = Mathf.Lerp(1f, targetSteer, weatherAdaptationLevel);

        float friction = raining ? WeatherManager.Instance.GetFrictionFactor() : 1f;
        foreach (var w in wheels) { SetWheelFriction(w, friction); }

        if (raining) AddReward(0.001f * weatherAdaptationLevel);
    }

    private void SetWheelFriction(WheelCollider w, float stiffness)
    {
        var f = w.forwardFriction; f.stiffness = stiffness; w.forwardFriction = f;
        var s = w.sidewaysFriction; s.stiffness = stiffness; w.sidewaysFriction = s;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        float velocity = rb.linearVelocity.magnitude / 20f;
        float forwardDot = Vector3.Dot(transform.forward, Vector3.forward);

        sensor.AddObservation(velocity);
        sensor.AddObservation(forwardDot);

        float angleStep = rayCount > 1 ? raySpreadAngle / (rayCount - 1) : 0f;

        for (int i = 0; i < rayCount; i++)
        {
            float angle = rayCount == 1 ? 0f : (-raySpreadAngle / 2 + i * angleStep);
            Vector3 dir = Quaternion.Euler(0, angle, 0) * transform.forward;
            float adjDist = raycastDistance * (WeatherManager.Instance?.GetVisibilityFactor() ?? 1f);

            sensor.AddObservation(Physics.Raycast(transform.position, dir, out RaycastHit hit, adjDist, roadLayer) ? hit.distance / adjDist : 0f);
        }

        sensor.AddObservation(transform.up.y);

        foreach (var wheel in wheels) sensor.AddObservation(wheel.isGrounded);

        sensor.AddObservation(WeatherManager.Instance?.isRaining == true ? 1f : 0f);
        sensor.AddObservation(WeatherManager.Instance?.rainIntensity ?? 0f);
        sensor.AddObservation(weatherAdaptationLevel);

        if (currentDriverProfile != null)
        {
            sensor.AddObservation(currentDriverProfile.speedMultiplier);
            sensor.AddObservation(currentDriverProfile.riskFactor);
            sensor.AddObservation(currentDriverProfile.patienceFactor);
        }
        else
        {
            sensor.AddObservation(1f); sensor.AddObservation(0.5f); sensor.AddObservation(0.5f);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        currentSteerInput = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);

        currentThrottleInput = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);

        ApplySteering(currentSteerInput);
        ApplyMotor(currentThrottleInput);

        Debug.Log($"RawActions -> Steer: {currentSteerInput:F2}, Throttle: {currentThrottleInput:F2}");
    }

    private void ApplySteering(float steerInput)
    {
        foreach (var wheel in wheels)
        {
            if (wheel.transform.localPosition.z > 0) // delanteras
                wheel.steerAngle = steerInput * steeringAngle * currentSteeringResponse;
        }
    }

    private void ApplyMotor(float throttleInput)
    {   
        Debug.Log($"=== ApplyMotor === ThrottleInput={throttleInput}, currentMotorForce={currentMotorForce}");

        foreach (var wheel in wheels)
        {
            if (wheel.transform.localPosition.z < 0)
            {
                float torque = throttleInput * currentMotorForce;
                wheel.motorTorque = torque;
                Debug.Log($"Wheel {wheel.name}: motorTorque={torque}");
            }
        }     
        foreach (var wheel in wheels) { Debug.Log($"Wheel {wheel.name}: motorTorque={wheel.motorTorque}, steerAngle={wheel.steerAngle}, WheelGrounded {wheel.isGrounded}"); } 
    }

    private bool IsGrounded() => wheels.All(w => w.isGrounded);
    private bool IsTilted() => Vector3.Angle(transform.up, Vector3.up) > 45f;

    private void FixedUpdate()
    {
        Debug.Log($"FixedUpdate - Velocity: {rb.linearVelocity.magnitude}, AngularVelocity: {rb.angularVelocity.magnitude}");

        if (StepCount > MaxStep)
        {
            Debug.Log($"MaxStep reached ({StepCount}/{MaxStep}). Ending episode.");
            AddReward(-1f);
            EndEpisode();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Obstacle") || collision.gameObject.CompareTag("NPC")) { AddReward(-2f); EndEpisode(); }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Boundary")) { AddReward(-2f); EndEpisode(); }
        else if (other.CompareTag("FinishLine") && !hasFinished) { AddReward(5f); hasFinished = true; EndEpisode(); }
    }

    private void ResetCar()
    {
        transform.position = resetPosition.position;
        transform.rotation = resetPosition.rotation;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        hasFinished = false;

        foreach (var wheel in wheels) { wheel.motorTorque = 0f; wheel.steerAngle = 0f; wheel.brakeTorque = 0f; }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        float angleStep = rayCount > 1 ? raySpreadAngle / (rayCount - 1) : 0f;
        for (int i = 0; i < rayCount; i++)
        {
            float angle = rayCount == 1 ? 0f : (-raySpreadAngle / 2 + i * angleStep);
            Vector3 dir = Quaternion.Euler(0, angle, 0) * transform.forward;
            float adjDist = raycastDistance * (WeatherManager.Instance?.GetVisibilityFactor() ?? 1f);
            Gizmos.DrawRay(transform.position, dir * adjDist);
        }
    }

    public void SetInitialLocation(string newLocation) => initialLocation = newLocation;
    public DriverProfile GetCurrentDriverProfile() => currentDriverProfile;

    private void Log(string msg) => Debug.Log($"CarAgent: {msg}");
}