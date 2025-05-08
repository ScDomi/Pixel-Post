using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering;

/// <summary>
/// Platziert Häuser auf Gras-Regionen und versieht sie beim Spawn mit SortingGroup auf "Props".
/// Bietet zudem eine Methode zur Ermittlung der Haus-Frontzellen für RoadConnector.
/// </summary>
public class HousePlacer : MonoBehaviour
{
    [Header("Map Settings")]
    public Tilemap baseLayer;                  // Tilemap für das Terrain

    [Header("Grass Tile Variants")]
    public TileBase[] grassTiles;              // Alle Gras-Tiles

    [Header("House Settings")]
    public GameObject[] housePrefabs;          // FBX-House Prefabs
    public int numberOfHouses = 20;            // gewünschte Hausanzahl
    public int minRegionSize = 2;              // Mindestgröße einer Gras-Region
    public float minDistance = 5f;             // Mindestabstand zwischen Häusern

    [Header("Sorting Settings")]
    public string sortingLayerName = "Props"; // Sorting Layer für Häuser
    public int sortingOrder = 0;               // Order in Layer

    [Header("Parenting & Debug")]
    public Transform housesContainer;          // Parent-Transform für Häuser
    public bool debugLogs = true;              // Debug-Logs ein/aus

    private void Awake()
    {
        // Erstelle Container, falls nicht zugewiesen
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
        // Warte einen Frame, damit Terrain-Generation abgeschlossen ist
        yield return null;

        if (grassTiles == null || grassTiles.Length == 0)
        {
            Debug.LogError("HousePlacer: Keine grassTiles gesetzt!");
            yield break;
        }

        // Regionen finden und Kandidaten ermitteln
        var regions = FindGrassRegions();
        if (debugLogs) Debug.Log($"Found {regions.Count} grass regions (>= {minRegionSize})");

        var candidates = regions.SelectMany(r => r).Shuffle().ToList();
        if (debugLogs) Debug.Log($"Total house candidate cells: {candidates.Count}");

        PlaceHouses(candidates);
    }

    private List<List<Vector3Int>> FindGrassRegions()
    {
        var bounds = baseLayer.cellBounds;
        var visited = new HashSet<Vector3Int>();
        var regions = new List<List<Vector3Int>>();

        foreach (var pos in bounds.allPositionsWithin)
        {
            if (visited.Contains(pos)) continue;
            visited.Add(pos);
            if (!IsGrassCell(pos)) continue;

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
                    if (IsGrassCell(np)) queue.Enqueue(np);
                }
            }

            if (region.Count >= minRegionSize)
                regions.Add(region);
        }
        return regions;
    }

    private bool IsGrassCell(Vector3Int pos)
    {
        var tile = baseLayer.GetTile(pos);
        if (tile == null) return false;
        if (grassTiles.Contains(tile)) return true;
        var name = tile.name.ToLower();
        return grassTiles.Any(gt => name.Contains(gt.name.ToLower()));
    }

    private void PlaceHouses(List<Vector3Int> candidates)
    {
        var placed = new List<Vector3>();
        int count = 0;

        foreach (var cell in candidates)
        {
            if (count >= numberOfHouses) break;
            var worldPos = baseLayer.CellToWorld(cell) + new Vector3(0.5f, 0.5f, 0f);
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
            Vector3Int cell = baseLayer.WorldToCell(sample);
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
