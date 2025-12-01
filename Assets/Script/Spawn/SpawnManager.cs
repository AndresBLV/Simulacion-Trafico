using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    [Header("Agrupaciones por zona")]
    public Transform spawnZonesParent;

    private Dictionary<string, List<Transform>> spawnPointsByZone;

    void Awake()
    {
        LoadSpawnPoints();
    }

    private void LoadSpawnPoints()
    {
        spawnPointsByZone = new Dictionary<string, List<Transform>>();

        foreach (Transform zone in spawnZonesParent)
        {
            string zoneName = zone.name.ToLower().Trim();

            List<Transform> points = new List<Transform>();
            foreach (Transform p in zone)
            {
                points.Add(p);
            }

            spawnPointsByZone.Add(zoneName, points);
        }

        Debug.Log($"SpawnManager listo. Zonas cargadas: {spawnPointsByZone.Count}");
    }

    /// <summary>
    /// Devuelve un spawn point adecuado según la ubicación del conductor.
    /// </summary>
    public Transform GetSpawnPointForLocation(string location)
    {
        if (string.IsNullOrEmpty(location))
            return GetAnySpawnPoint();

        location = location.ToLower().Trim();

        // Busca coincidencias de texto
        foreach (var zone in spawnPointsByZone)
        {
            if (location.Contains(zone.Key))
            {
                var points = zone.Value;
                return points[Random.Range(0, points.Count)];
            }
        }

        // Si no encontró nada, usa fallback
        return GetAnySpawnPoint();
    }

    private Transform GetAnySpawnPoint()
    {
        var firstZone = spawnPointsByZone.First().Value;
        return firstZone[Random.Range(0, firstZone.Count)];
    }

    /// Gizmos para ver los spawn points en el editor
    private void OnDrawGizmos()
    {
        if (spawnZonesParent == null) return;

        Gizmos.color = Color.green;

        foreach (Transform zone in spawnZonesParent)
        {
            foreach (Transform p in zone)
            {
                Gizmos.DrawSphere(p.position, 0.6f);
            }
        }
    }
}
