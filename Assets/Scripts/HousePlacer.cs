using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering;
using UnityEngine.Assertions;

/// <summary>
/// Platziert Häuser auf Gras-Regionen und versieht sie beim Spawn mit SortingGroup auf "Props".
/// Bietet zudem eine Methode zur Ermittlung der Haus-Frontzellen für RoadConnector.
/// </summary>
public class HousePlacer : MonoBehaviour
{
    [Header("Required References")]
    public ProceduralMapGenerator mapGen;      // Reference to map generator for layers

    [Header("House Settings")]
    public GameObject[] housePrefabs;          // FBX-House Prefabs
    public int numberOfHouses = 20;            // gewünschte Hausanzahl
    public int minRegionSize = 2;              // Mindestgröße einer Gras-Region
    public float minDistance = 5f;             // Mindestabstand zwischen Häusern

    [Header("Sorting Settings")]
    public string sortingLayerName = "Props";  // Sorting Layer für Häuser
    public int sortingOrder = 0;               // Order in Layer

    [Header("Parenting & Debug")]
    public Transform housesContainer;          // Parent-Transform für Häuser
    public bool debugLogs = true;              // Debug-Logs ein/aus

    private Tilemap grassLayer;                // Reference to grass layer
    private Tilemap waterLayer;                // Reference to water layer to avoid water areas

    private void Awake()
    {
        Assert.IsNotNull(mapGen, "MapGenerator reference is required!");
        Assert.IsNotNull(housePrefabs, "House prefabs array is required!");
        Assert.IsTrue(housePrefabs.Length > 0, "At least one house prefab is required!");

        // Get layer references from map generator
        grassLayer = mapGen.grassLayer;
        waterLayer = mapGen.waterLayer;

        Assert.IsNotNull(grassLayer, "Grass layer reference not found in MapGenerator!");
        Assert.IsNotNull(waterLayer, "Water layer reference not found in MapGenerator!");

        // Create container if not assigned
        if (housesContainer == null)
        {
            housesContainer = new GameObject("HouseContainer").transform;
            housesContainer.parent = this.transform;
            if (debugLogs)
                Debug.Log("HousePlacer: Created HouseContainer", housesContainer);
        }
    }

    private IEnumerator Start()
    {
        // Wait a frame for terrain generation to complete
        yield return null;

        // Find regions and candidates
        var regions = FindGrassRegions();
        if (debugLogs) Debug.Log($"Found {regions.Count} grass regions (>= {minRegionSize})");

        var candidates = regions.SelectMany(r => r).Shuffle().ToList();
        if (debugLogs) Debug.Log($"Total house candidate cells: {candidates.Count}");

        PlaceHouses(candidates);
    }

    private List<List<Vector3Int>> FindGrassRegions()
    {
        var bounds = grassLayer.cellBounds;
        var visited = new HashSet<Vector3Int>();
        var regions = new List<List<Vector3Int>>();

        foreach (var pos in bounds.allPositionsWithin)
        {
            if (visited.Contains(pos)) continue;
            visited.Add(pos);
            if (!IsValidHousePosition(pos)) continue;

            var queue = new Queue<Vector3Int>();
            var region = new List<Vector3Int>();
            queue.Enqueue(pos);

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                region.Add(cur);
                foreach (var d in new[] { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right })
                {
                    var np = cur + d;
                    if (!bounds.Contains(np) || visited.Contains(np)) continue;
                    visited.Add(np);
                    if (IsValidHousePosition(np)) queue.Enqueue(np);
                }
            }

            if (region.Count >= minRegionSize)
                regions.Add(region);
        }
        return regions;
    }

    private bool IsValidHousePosition(Vector3Int pos)
    {
        // Position must have grass and no water
        return grassLayer.GetTile(pos) != null && waterLayer.GetTile(pos) == null;
    }

    private void PlaceHouses(List<Vector3Int> candidates)
    {
        var placed = new List<Vector3>();
        int count = 0;

        foreach (var cell in candidates)
        {
            if (count >= numberOfHouses) break;
            var worldPos = grassLayer.CellToWorld(cell) + new Vector3(0.5f, 0.5f, 0f);
            if (placed.Any(p => Vector2.Distance(p, worldPos) < minDistance))
                continue;

            // Haus instanziieren
            var prefab = housePrefabs[Random.Range(0, housePrefabs.Length)];
            var inst = Instantiate(prefab, worldPos, Quaternion.Euler(0, Random.Range(0, 4) * 90, 0), housesContainer);
            if (debugLogs)
                Debug.Log($"HousePlacer: Instantiated {prefab.name} at {worldPos}", inst);

            // Füge immer SortingGroup hinzu
            var sg = inst.gameObject.AddComponent<SortingGroup>();
            sg.sortingLayerName = sortingLayerName;
            sg.sortingOrder = sortingOrder;

            placed.Add(worldPos);
            count++;
        }
        if (debugLogs)
            Debug.Log($"HousePlacer: {count}/{numberOfHouses} houses placed.");
    }

    /// <summary>
    /// Liefert alle Haus-Frontzellen (Startpunkte für Straßen) und deren Richtungen basierend auf der Hausrotation.
    /// </summary>
    public List<(Vector3Int pos, Vector3Int dir)> GetHouseFrontCells()
    {
        var fronts = new List<(Vector3Int pos, Vector3Int dir)>();
        foreach (Transform house in housesContainer)
        {
            Vector3 wp = house.position;
            float ry = house.eulerAngles.y;
            Vector3Int dir = Vector3Int.down; // 0 degrees = facing down/south
            
            if (Mathf.Approximately(ry, 0f)) dir = Vector3Int.down;
            else if (Mathf.Approximately(ry, 180f)) dir = Vector3Int.up;
            else if (Mathf.Approximately(ry, 90f)) dir = Vector3Int.left;
            else if (Mathf.Approximately(ry, 270f)) dir = Vector3Int.right;

            Vector3 sample = wp + (Vector3)dir * 0.5f;
            Vector3Int cell = grassLayer.WorldToCell(sample);
            fronts.Add((cell, dir));
        }
        return fronts;
    }
}

/// <summary>
/// Extension für Shuffle (Fisher–Yates).
/// </summary>
public static class ListExtensions
{
    public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source)
    {
        var list = source.ToList();
        for (int i = 0; i < list.Count; i++)
        {
            int j = Random.Range(i, list.Count);
            var tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
        return list;
    }
}
