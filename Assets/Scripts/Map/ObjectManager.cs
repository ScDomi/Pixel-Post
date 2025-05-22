using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Linq;
using Map;

public class ObjectManager : MonoBehaviour
{
    [HideInInspector] public Vector2Int origin;
    [HideInInspector] public Vector2Int biomeSize;

    [Header("Required References")]
    public BaseMapManager baseMapManager;
    
    [Header("Vegetation Prefabs")]
    public GameObject[] treePrefabs;
    public GameObject highGrassPrefab;
    public GameObject lowGrassPrefab;
    public GameObject[] housePrefabs;

    [Header("Forest Settings")]
    [Range(0f, 1f)]
    public float treeDensity = 0.3f;
    public float minTreeDistance = 2f;
    public float minTreeRoadDistance = 3f;  // Mindestabstand zwischen Bäumen und Straßen
    
    [Header("Grass Settings")]
    public float highGrassRadius = 5f;
    public float lowGrassRadius = 8f;
    [Range(0f, 1f)]
    public float highGrassDensity = 0.4f;
    [Range(0f, 1f)]
    public float lowGrassDensity = 0.3f;
    
    [Header("Lake Vegetation")]
    [Range(0f, 1f)]
    public float lakeTreeChance = 0.2f;
    public float lakeEffectRadius = 6f;
    [Range(0f, 1f)]
    public float lakeHighGrassChance = 0.4f;
    [Range(0f, 1f)]
    public float lakeLowGrassChance = 0.5f;
    
    [Header("Road Vegetation")]
    public float roadGrassRadius = 3f;
    [Range(0f, 1f)]
    public float roadGrassChance = 0.3f;
    [Range(0f, 1f)]
    public float roadHighGrassRatio = 0.4f;

    [Header("House Settings")]
    public float minHouseDistance = 5f;
    
    [Header("Sorting Settings")]
    public string sortingLayerName = "Props";
    public int treeSortingOrder = 3;
    public int houseSortingOrder = 2;
    public int highGrassSortingOrder = 1;
    public int lowGrassSortingOrder = 0;

    private Transform objectContainer;
    private List<Vector3> placedObjects = new List<Vector3>();
    private Dictionary<GameObject, Vector3Int> houseDirections = new Dictionary<GameObject, Vector3Int>();

    private void Awake()
    {
        objectContainer = new GameObject("ObjectContainer").transform;
        objectContainer.parent = transform;
    }

    // Public methods for MapGenerationManager to call
    public void PlaceHouses()
    {
        // Find all grass tiles first
        var bounds = baseMapManager.grassLayer.cellBounds;
        var candidates = new List<Vector3Int>();
        
        foreach (var pos in bounds.allPositionsWithin)
        {
            if (pos.x < origin.x || pos.y < origin.y) continue;
            if (pos.x >= origin.x + biomeSize.x || pos.y >= origin.y + biomeSize.y) continue;
            if (IsValidHousePosition(baseMapManager.grassLayer.CellToWorld(pos)))
                candidates.Add(pos);
        }
        
        // Shuffle candidates for random placement
        candidates = candidates.OrderBy(x => Random.value).ToList();
        
        // Try to place houses
        int totalHouses = Mathf.Min(5, candidates.Count / 4); // Maximum 20 houses, or fewer if not enough space
        int housesPlaced = 0;
        
        foreach (var cell in candidates)
        {
            if (housesPlaced >= totalHouses) break;
            
            var worldPos = baseMapManager.grassLayer.CellToWorld(cell) + new Vector3(0.5f, 0.5f, 0f);
            if (!placedObjects.Any(p => Vector2.Distance(p, worldPos) < minHouseDistance))
            {
                PlaceHouse(worldPos);
                housesPlaced++;
            }
        }
    }

    public void PlaceRemainingObjects()
    {
        // Stelle sicher, dass alle Platzierungen nur im eigenen Biome-Bereich erfolgen
        PlaceForestVegetation();
        PlaceLakeVegetation();
        PlaceRoadVegetation();
    }

    private bool IsInBiomeBounds(Vector3 worldPos)
    {
        var cell = baseMapManager.baseLayer.WorldToCell(worldPos);
        return cell.x >= origin.x && cell.x < origin.x + biomeSize.x &&
               cell.y >= origin.y && cell.y < origin.y + biomeSize.y;
    }

    private bool IsValidHousePosition(Vector3 worldPos)
    {
        var cellPos = baseMapManager.baseLayer.WorldToCell(worldPos);
        
        // Position must have grass and no water
        if (baseMapManager.grassLayer.GetTile(cellPos) == null || 
            baseMapManager.waterLayer.GetTile(cellPos) != null)
            return false;

        // Check map bounds
        if (!baseMapManager.baseLayer.cellBounds.Contains(cellPos))
            return false;

        return true;
    }



    private void PlaceHouse(Vector3 position)
    {
        var prefab = housePrefabs[Random.Range(0, housePrefabs.Length)];
        // Erst -90° um Y, dann 90° um Z, dann random um X
        float randomX = Random.Range(0, 360);
        var rotation = Quaternion.Euler(randomX, -90f, 90f);
        var house = Instantiate(prefab, position, rotation, objectContainer);
        
        // Add sorting group
        var sg = house.AddComponent<SortingGroup>();
        sg.sortingLayerName = sortingLayerName;
        sg.sortingOrder = houseSortingOrder;
        
        // Store house direction based on rotation
        Vector3Int direction;
        // Die Richtung bleibt wie gehabt, da sie von der ursprünglichen Logik abhängt
        if (Mathf.Approximately(rotation.eulerAngles.y, 0f)) direction = Vector3Int.down;
        else if (Mathf.Approximately(rotation.eulerAngles.y, 90f)) direction = Vector3Int.left;
        else if (Mathf.Approximately(rotation.eulerAngles.y, 180f)) direction = Vector3Int.up;
        else direction = Vector3Int.right;
        
        houseDirections[house] = direction;
        placedObjects.Add(position);
    }

    public List<RoadManager.HouseFront> GetHouseFrontPositions()
    {
        var fronts = new List<RoadManager.HouseFront>();
        
        foreach (var houseEntry in houseDirections)
        {
            var house = houseEntry.Key;
            var dir = houseEntry.Value;
            var worldPos = house.transform.position;
            
            // Get tile position in front of house
            var samplePos = worldPos + (Vector3)dir * 0.5f;
            var tilePos = baseMapManager.grassLayer.WorldToCell(samplePos);
            
            fronts.Add(new RoadManager.HouseFront(tilePos, dir));
        }
        
        return fronts;
    }

    private void PlaceForestVegetation()
    {
        foreach (var region in baseMapManager.EarthRegions)
        {
            foreach (var tile in region.Tiles)
            {
                var worldPos = baseMapManager.baseLayer.CellToWorld(tile) + new Vector3(0.5f, 0.5f, 0f);
                
                if (Random.value < treeDensity && 
                    !placedObjects.Any(p => Vector2.Distance(p, worldPos) < minTreeDistance) &&
                    IsValidPosition(worldPos, true))
                {
                    PlaceTree(worldPos);
                }
            }

            // Place grass around the forest
            PlaceGrassAroundRegion(region);
        }
    }

    private void PlaceLakeVegetation()
    {
        foreach (var region in baseMapManager.LakeRegions)
        {
            foreach (var tile in region.Tiles)
            {
                var worldPos = baseMapManager.waterLayer.CellToWorld(tile) + new Vector3(0.5f, 0.5f, 0f);
                
                for (float angle = 0; angle < 360; angle += 45)
                {
                    float rad = angle * Mathf.Deg2Rad;
                    for (float dist = 1; dist <= lakeEffectRadius; dist += 1f)
                    {
                        Vector3 checkPos = worldPos + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * dist;
                                 if (!IsValidPosition(checkPos, true)) continue;

                float distFactor = 1 - (dist / lakeEffectRadius);
                
                if (Random.value < lakeTreeChance * distFactor)
                    PlaceTree(checkPos);
                        if (Random.value < lakeHighGrassChance * distFactor)
                            PlaceGrass(checkPos, true);
                        if (Random.value < lakeLowGrassChance * distFactor)
                            PlaceGrass(checkPos, false);
                    }
                }
            }
        }
    }

    private void PlaceRoadVegetation()
    {
        var roadBounds = baseMapManager.roadLayer.cellBounds;
        foreach (var pos in roadBounds.allPositionsWithin)
        {
            if (baseMapManager.roadLayer.GetTile(pos) == null) continue;

            var worldPos = baseMapManager.roadLayer.CellToWorld(pos) + new Vector3(0.5f, 0.5f, 0f);
            
            for (float angle = 0; angle < 360; angle += 30)
            {
                float rad = angle * Mathf.Deg2Rad;
                for (float dist = 1; dist <= roadGrassRadius; dist += 0.5f)
                {
                    if (Random.value > roadGrassChance) continue;

                    Vector3 checkPos = worldPos + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * dist;
                    if (!IsValidPosition(checkPos)) continue;

                    PlaceGrass(checkPos, Random.value < roadHighGrassRatio);
                }
            }
        }
    }

    private void PlaceGrassAroundRegion(Map.BaseRegion region)
    {
        // Place high grass closer to the region
        foreach (var tile in region.Tiles)
        {
            var worldPos = baseMapManager.baseLayer.CellToWorld(tile) + new Vector3(0.5f, 0.5f, 0f);
            PlaceGrassInRadius(worldPos, highGrassRadius, highGrassDensity, true);
        }

        // Place low grass in a wider radius
        foreach (var tile in region.Tiles)
        {
            var worldPos = baseMapManager.baseLayer.CellToWorld(tile) + new Vector3(0.5f, 0.5f, 0f);
            PlaceGrassInRadius(worldPos, lowGrassRadius, lowGrassDensity, false);
        }
    }

    private void PlaceGrassInRadius(Vector3 center, float radius, float density, bool isHighGrass)
    {
        if (!IsInBiomeBounds(center)) return;

        for (float angle = 0; angle < 360; angle += 15)
        {
            float rad = angle * Mathf.Deg2Rad;
            for (float dist = 1; dist <= radius; dist += 0.5f)
            {
                if (Random.value > density) continue;

                Vector3 checkPos = center + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * dist;
                if (!IsValidPosition(checkPos)) continue;

                PlaceGrass(checkPos, isHighGrass);
            }
        }
    }

    private void PlaceTree(Vector3 worldPos)
    {
        if (!IsInBiomeBounds(worldPos)) return;

        var prefab = treePrefabs[Random.Range(0, treePrefabs.Length)];
        // Erst -90° um Y, dann 90° um Z, dann random um X
        float randomX = Random.Range(0, 360);
        var rotation = Quaternion.Euler(randomX, -90f, 90f);
        var tree = Instantiate(prefab, worldPos, rotation, objectContainer);

        var sg = tree.AddComponent<SortingGroup>();
        sg.sortingLayerName = sortingLayerName;
        sg.sortingOrder = treeSortingOrder;

        float scale = Random.Range(0.9f, 1.1f);
        tree.transform.localScale *= scale;

        placedObjects.Add(worldPos);
    }

    private void PlaceGrass(Vector3 worldPos, bool isHighGrass)
    {
        if (!IsInBiomeBounds(worldPos)) return;

        if (placedObjects.Any(p => Vector2.Distance(p, worldPos) < minTreeDistance * 0.5f))
            return;
        var prefab = isHighGrass ? highGrassPrefab : lowGrassPrefab;
        // Erst -90° um Y, dann 90° um Z, dann random um X
        float randomX = Random.Range(0, 360);
        var rotation = Quaternion.Euler(randomX, -90f, 90f);
        var grass = Instantiate(prefab, worldPos, rotation, objectContainer);

        var sg = grass.AddComponent<SortingGroup>();
        sg.sortingLayerName = sortingLayerName;
        sg.sortingOrder = isHighGrass ? highGrassSortingOrder : lowGrassSortingOrder;

        float scale = Random.Range(0.8f, 1.2f);
        grass.transform.localScale *= scale;

        placedObjects.Add(worldPos);
    }
private bool IsValidPosition(Vector3 worldPos, bool isTree = false)
{
    var cellPos = baseMapManager.baseLayer.WorldToCell(worldPos);

    // Check if position is within map bounds
    if (!baseMapManager.baseLayer.cellBounds.Contains(cellPos))
        return false;

    // Check if position is in any earth region (no grass objects in earth regions)
    if (!isTree) // <--- NUR für Gras, NICHT für Bäume!
    {
        foreach (var earthRegion in baseMapManager.EarthRegions)
        {
            if (earthRegion.Tiles.Contains(cellPos))
                return false;
        }
    }

    // Check if position is not on water or road
    if (baseMapManager.waterLayer.GetTile(cellPos) != null || 
        baseMapManager.roadLayer.GetTile(cellPos) != null)
        return false;

    // For trees, check minimum distance to roads
    if (isTree)
    {
        var bounds = baseMapManager.roadLayer.cellBounds;
        for (int x = -Mathf.CeilToInt(minTreeRoadDistance); x <= Mathf.CeilToInt(minTreeRoadDistance); x++)
        {
            for (int y = -Mathf.CeilToInt(minTreeRoadDistance); y <= Mathf.CeilToInt(minTreeRoadDistance); y++)
            {
                var checkPos = cellPos + new Vector3Int(x, y, 0);
                if (bounds.Contains(checkPos) && baseMapManager.roadLayer.GetTile(checkPos) != null)
                {
                    var roadWorldPos = baseMapManager.roadLayer.CellToWorld(checkPos) + new Vector3(0.5f, 0.5f, 0f);
                    if (Vector2.Distance(worldPos, roadWorldPos) < minTreeRoadDistance)
                        return false;
                }
            }
        }
    }

    return true;
}
}
