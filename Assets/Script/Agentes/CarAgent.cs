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
    public LayerMask roadLayer;
    public LayerMask obstacleLayer; // Layer para obstáculos dinámicos
    public int rayCount = 9;
    public float raySpreadAngle = 120f;

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

    private float baseMotorForce;
    private float baseSteeringAngle;

    private DriverProfile currentDriverProfile;
    private List<DriverProfile> driverProfiles;

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
        rb = GetComponent<Rigidbody>();
        wheels = GetComponentsInChildren<WheelCollider>();
        rb.centerOfMass = new Vector3(0, -0.5f, 0);

        baseMotorForce = motorForce;
        baseSteeringAngle = steeringAngle;
        currentMotorForce = baseMotorForce;
        currentSteeringResponse = 1f;

        LoadDriverProfiles();
        SelectDriverProfileByLocation(initialLocation);
    }

    private void SnapCarAboveGround()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 10f, roadLayer))
        {
            float heightAboveGround = 0.2f;
            transform.position = hit.point + Vector3.up * heightAboveGround;
            transform.rotation = Quaternion.FromToRotation(transform.up, hit.normal) * transform.rotation;
        }
    }

    private void LoadDriverProfiles()
    {
        driverProfiles = new List<DriverProfile>();
        if (driverDataFile == null) return;

        var lines = driverDataFile.text.Split('\n');
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var profile = ParseDriverData(line);
            if (profile != null) driverProfiles.Add(profile);
        }
    }

    private DriverProfile ParseDriverData(string line)
    {
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
        ResetCar();
        SnapCarAboveGround();

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
        foreach (var w in wheels) SetWheelFriction(w, friction);

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
        sensor.AddObservation(transform.up.y);

        // Raycasts para pista y obstáculos
        float angleStep = rayCount > 1 ? raySpreadAngle / (rayCount - 1) : 0f;
        for (int i = 0; i < rayCount; i++)
        {
            float angle = rayCount == 1 ? 0f : (-raySpreadAngle / 2 + i * angleStep);
            Vector3 dir = Quaternion.Euler(0, angle, 0) * transform.forward;
            float adjDist = 5f * (WeatherManager.Instance?.GetVisibilityFactor() ?? 1f);

            // Detecta suelo
            sensor.AddObservation(Physics.Raycast(transform.position, dir, out RaycastHit hit, adjDist, roadLayer) ? hit.distance / adjDist : 0f);
            // Detecta obstáculos dinámicos
            sensor.AddObservation(Physics.Raycast(transform.position, dir, out RaycastHit hitObs, adjDist, obstacleLayer) ? 1f - hitObs.distance / adjDist : 0f);
        }

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
        float steer = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float throttle = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);

        ApplySteering(steer);
        ApplyMotor(throttle);
    }

    private void ApplySteering(float steerInput)
    {
        currentSteerInput = Mathf.Lerp(currentSteerInput, steerInput, 0.2f);
        foreach (var wheel in wheels)
            if (wheel.transform.localPosition.z > 0) wheel.steerAngle = currentSteerInput * steeringAngle * currentSteeringResponse;
    }

    private void ApplyMotor(float throttleInput)
    {
        currentThrottleInput = Mathf.Lerp(currentThrottleInput, throttleInput, 0.2f);
        foreach (var wheel in wheels)
            if (wheel.transform.localPosition.z < 0) wheel.motorTorque = currentThrottleInput * currentMotorForce;
    }

    private bool IsGrounded() => wheels.All(w => w.isGrounded);
    private bool IsTilted() => Vector3.Angle(transform.up, Vector3.up) > 45f;

    private void RewardDriving()
    {
        // Avanzar hacia adelante
        float forwardDot = Vector3.Dot(transform.forward, Vector3.forward);
        AddReward(0.01f * forwardDot);

        // Mantener centrado (aproximado usando posición lateral)
        float lateralOffset = Mathf.Abs(transform.localPosition.x);
        AddReward(-0.001f * lateralOffset);

        // Evitar inclinarse o saltar
        if (!IsGrounded()) AddReward(-0.01f);
        if (IsTilted()) AddReward(-0.005f);
    }

    private void FixedUpdate()
    {
        RewardDriving();
        UpdateWeatherAdaptation();

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
            Gizmos.DrawRay(transform.position, dir * 5f);
        }
    }
}