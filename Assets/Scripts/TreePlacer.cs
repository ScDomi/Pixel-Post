using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Linq;

public abstract class BaseRegion
{
    public string Name { get; protected set; }
    public int Size { get; protected set; }
    public List<Vector3Int> Tiles { get; protected set; }
    public Vector3 Center { get; protected set; }
    public float SpilloverRadius { get; protected set; }

    protected BaseRegion(string name, List<Vector3Int> tiles, Tilemap baseLayer)
    {
        Name = name;
        Tiles = tiles;
        Size = tiles.Count;
        
        // Calculate center in world space
        var sum = Vector3.zero;
        foreach (var tile in tiles)
        {
            sum += baseLayer.CellToWorld(tile) + new Vector3(0.5f, 0.5f, 0f);
        }
        Center = sum / Size;

        // Calculate actual region dimensions
        var minX = tiles.Min(t => t.x);
        var maxX = tiles.Max(t => t.x);
        var minY = tiles.Min(t => t.y);
        var maxY = tiles.Max(t => t.y);
        
        // Calculate width and height
        var width = maxX - minX + 1;  // +1 because both bounds are inclusive
        var height = maxY - minY + 1;
        var area = width * height;
        
        // Spillover radius based on region dimensions
        SpilloverRadius = Mathf.Sqrt(area) * 1.5f;
    }
}

public class ForestRegion : BaseRegion
{
    public int MaxTrees { get; private set; }

    public ForestRegion(string name, List<Vector3Int> tiles, Tilemap baseLayer)
        : base(name, tiles, baseLayer)
    {
        // Max trees based on square root of actual rectangular area
        var minX = tiles.Min(t => t.x);
        var maxX = tiles.Max(t => t.x);
        var minY = tiles.Min(t => t.y);
        var maxY = tiles.Max(t => t.y);
        var width = maxX - minX + 1;
        var height = maxY - minY + 1;
        var area = width * height;
        MaxTrees = Mathf.RoundToInt(Mathf.Sqrt(area) * 2);
    }
}

public class LakeRegion : BaseRegion
{
    public LakeRegion(string name, List<Vector3Int> tiles, Tilemap baseLayer)
        : base(name, tiles, baseLayer)
    {
    }
}

/// <summary>
/// Places trees primarily on earth tiles with some spillover onto grass.
/// Uses noise for varying density across the map.
/// </summary>
public class TreePlacer : MonoBehaviour
{
    [Header("Required References")]
    public ProceduralMapGenerator mapGen;      // Reference to map generator for layers
    public GameObject[] treePrefabs;           // Tree prefabs to place
    public GameObject highGrassPrefab;         // High grass prefab
    public GameObject lowGrassPrefab;          // Low grass prefab

    [Header("Forest Settings")]
    [Range(0f, 1f)]
    public float baseDensity = 0.3f;          // Base chance to place a tree
    public float minDistance = 2f;             // Minimum distance between trees
    
    [Header("Forest Grass Settings")]
    public float highGrassRadius = 5f;         // Radius for high grass around forests
    public float lowGrassRadius = 8f;          // Radius for low grass around forests
    [Range(0f, 1f)]
    public float highGrassDensity = 0.4f;      // Density of high grass
    [Range(0f, 1f)]
    public float lowGrassDensity = 0.3f;       // Density of low grass
    
    [Header("Lake Settings")]
    [Range(0f, 1f)]
    public float lakeTreeChance = 0.2f;        // Chance for trees near lakes
    public float lakeEffectRadius = 6f;        // How far from lake edge effects apply
    [Range(0f, 1f)]
    public float lakeHighGrassChance = 0.4f;   // Chance for high grass near lakes
    [Range(0f, 1f)]
    public float lakeLowGrassChance = 0.5f;    // Chance for low grass near lakes
    
    [Header("Road Settings")]
    public float roadGrassRadius = 3f;         // How far from roads grass grows
    [Range(0f, 1f)]
    public float roadGrassChance = 0.3f;       // Chance to place grass near roads
    [Range(0f, 1f)]
    public float roadHighGrassRatio = 0.4f;    // Ratio of high grass vs low grass
    
    [Header("Spillover Settings")]
    [Range(0f, 1f)]
    public float spilloverChance = 0.3f;      // Chance for trees to spill outside forest
    [Range(0f, 1f)]
    public float maxSpilloverFraction = 0.3f;  // Max fraction of trees that can spill
    
    [Header("Density Variation")]
    public float noiseScale = 15f;            // Scale of the density variation
    [Range(0f, 1f)]
    public float noiseMagnitude = 0.4f;       // How much the density varies

    [Header("Sorting Settings")]
    public string sortingLayerName = "Props";  // Sorting layer for vegetation
    public int treeSortingOrder = 2;          // Trees appear above grass
    public int highGrassSortingOrder = 1;     // High grass appears above low grass
    public int lowGrassSortingOrder = 0;      // Low grass appears at bottom

    [Header("Debug")]
    public bool debugLogs = true;

    private Tilemap grassLayer;
    private Tilemap baseLayer;
    private Tilemap roadLayer;
    private Transform vegetationContainer;
    private List<Vector3> allPlacedObjects = new List<Vector3>();
    
    private void Awake()
    {
        Assert.IsNotNull(mapGen, "MapGenerator reference is required!");
        Assert.IsNotNull(treePrefabs, "Tree prefabs array is required!");
        Assert.IsTrue(treePrefabs.Length > 0, "At least one tree prefab is required!");

        grassLayer = mapGen.grassLayer;
        baseLayer = mapGen.baseLayer;
        roadLayer = mapGen.roadLayer;

        Assert.IsNotNull(grassLayer, "Grass layer reference not found!");
        Assert.IsNotNull(baseLayer, "Base layer reference not found!");
        Assert.IsNotNull(roadLayer, "Road layer reference not found!");

        // Create container for vegetation
        vegetationContainer = new GameObject("VegetationContainer").transform;
        vegetationContainer.parent = transform;
    }

    private void Start()
    {
        // Wait a frame for terrain generation
        Invoke(nameof(GenerateVegetation), 0.1f);
    }

    private void GenerateVegetation()
    {
        var forestRegions = FindForestRegions();
        var lakeRegions = FindLakeRegions();
        
        if (debugLogs)
        {
            Debug.Log($"Found {forestRegions.Count} forest regions");
            Debug.Log($"Found {lakeRegions.Count} lake regions");
        }

        // Process forests first
        foreach (var region in forestRegions)
        {
            ProcessForestRegion(region);
        }

        // Then lakes
        foreach (var region in lakeRegions)
        {
            ProcessLakeRegion(region);
        }

        // Finally, add grass along roads
        ProcessRoadEdges();
    }

    private void ProcessForestRegion(ForestRegion region)
    {
        // Place trees first
        var (inForestTrees, spilloverTrees) = PlaceTreesInRegion(region);
        allPlacedObjects.AddRange(inForestTrees);
        allPlacedObjects.AddRange(spilloverTrees);

        // Place high grass in radius
        PlaceGrassAroundPoint(region.Center, highGrassRadius, highGrassDensity, true);
        
        // Place low grass in larger radius
        PlaceGrassAroundPoint(region.Center, lowGrassRadius, lowGrassDensity, false);
    }

    private void ProcessLakeRegion(LakeRegion region)
    {
        foreach (var tile in region.Tiles)
        {
            var worldPos = baseLayer.CellToWorld(tile) + new Vector3(0.5f, 0.5f, 0f);
            
            // Try to place vegetation around lake edges
            for (float angle = 0; angle < 360; angle += 45)
            {
                float rad = angle * Mathf.Deg2Rad;
                for (float dist = 1; dist <= lakeEffectRadius; dist += 1f)
                {
                    Vector3 checkPos = worldPos + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * dist;
                    Vector3Int checkTile = baseLayer.WorldToCell(checkPos);
                    
                    if (!IsValidVegetationPosition(checkTile, checkPos)) continue;

                    // Place vegetation with decreasing probability based on distance
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

    private void ProcessRoadEdges()
    {
        var bounds = roadLayer.cellBounds;
        foreach (var pos in bounds.allPositionsWithin)
        {
            if (roadLayer.GetTile(pos) == null) continue;

            var worldPos = roadLayer.CellToWorld(pos) + new Vector3(0.5f, 0.5f, 0f);
            
            // Place grass around road tiles
            for (float angle = 0; angle < 360; angle += 30)
            {
                float rad = angle * Mathf.Deg2Rad;
                for (float dist = 1; dist <= roadGrassRadius; dist += 0.5f)
                {
                    if (Random.value > roadGrassChance) continue;

                    Vector3 checkPos = worldPos + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * dist;
                    Vector3Int checkTile = baseLayer.WorldToCell(checkPos);
                    
                    if (!IsValidVegetationPosition(checkTile, checkPos)) continue;

                    // Mix high and low grass
                    PlaceGrass(checkPos, Random.value < roadHighGrassRatio);
                }
            }
        }
    }

    private void PlaceGrassAroundPoint(Vector3 center, float radius, float density, bool isHighGrass)
    {
        for (float angle = 0; angle < 360; angle += 15)
        {
            float rad = angle * Mathf.Deg2Rad;
            for (float dist = 1; dist <= radius; dist += 0.5f)
            {
                if (Random.value > density) continue;

                Vector3 checkPos = center + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * dist;
                Vector3Int checkTile = baseLayer.WorldToCell(checkPos);
                
                if (!IsValidVegetationPosition(checkTile, checkPos)) continue;
                
                PlaceGrass(checkPos, isHighGrass);
            }
        }
    }

    private void PlaceGrass(Vector3 position, bool isHighGrass)
    {
        if (allPlacedObjects.Any(p => Vector2.Distance(p, position) < minDistance))
            return;

        var prefab = isHighGrass ? highGrassPrefab : lowGrassPrefab;
        var rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
        var grass = Instantiate(prefab, position, rotation, vegetationContainer);

        // Add sorting group
        var sg = grass.AddComponent<SortingGroup>();
        sg.sortingLayerName = sortingLayerName;
        sg.sortingOrder = isHighGrass ? highGrassSortingOrder : lowGrassSortingOrder;

        // Random scale variation
        float scale = Random.Range(0.8f, 1.2f);
        grass.transform.localScale *= scale;

        allPlacedObjects.Add(position);
    }

    private List<ForestRegion> FindForestRegions()
    {
        var bounds = baseLayer.cellBounds;
        var visited = new HashSet<Vector3Int>();
        var regions = new List<ForestRegion>();
        int regionCount = 0;

        foreach (var pos in bounds.allPositionsWithin)
        {
            if (visited.Contains(pos)) continue;
            visited.Add(pos);
            
            // Check if this is an earth tile (has earth but no grass)
            if (!IsEarthTile(pos)) continue;

            // Find all connected earth tiles
            var earthTiles = new List<Vector3Int>();
            var queue = new Queue<Vector3Int>();
            queue.Enqueue(pos);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                earthTiles.Add(current);

                foreach (var dir in new[] { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right })
                {
                    var next = current + dir;
                    if (!bounds.Contains(next) || visited.Contains(next)) continue;
                    visited.Add(next);
                    if (IsEarthTile(next))
                        queue.Enqueue(next);
                }
            }

            if (earthTiles.Count > 0)
            {
                var region = new ForestRegion($"Forest_{regionCount++}", earthTiles, baseLayer);
                regions.Add(region);
            }
        }

        return regions;
    }

    private List<LakeRegion> FindLakeRegions()
    {
        var bounds = mapGen.waterLayer.cellBounds;
        var visited = new HashSet<Vector3Int>();
        var regions = new List<LakeRegion>();
        int regionCount = 0;

        foreach (var pos in bounds.allPositionsWithin)
        {
            if (visited.Contains(pos)) continue;
            visited.Add(pos);
            
            if (mapGen.waterLayer.GetTile(pos) == null) continue;

            var waterTiles = new List<Vector3Int>();
            var queue = new Queue<Vector3Int>();
            queue.Enqueue(pos);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                waterTiles.Add(current);

                foreach (var dir in new[] { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right })
                {
                    var next = current + dir;
                    if (!bounds.Contains(next) || visited.Contains(next)) continue;
                    visited.Add(next);
                    if (mapGen.waterLayer.GetTile(next) != null)
                        queue.Enqueue(next);
                }
            }

            if (waterTiles.Count > 0)
            {
                var region = new LakeRegion($"Lake_{regionCount++}", waterTiles, baseLayer);
                regions.Add(region);
            }
        }

        return regions;
    }

    private bool IsEarthTile(Vector3Int pos)
    {
        return baseLayer.GetTile(pos) != null && 
               grassLayer.GetTile(pos) == null && 
               mapGen.waterLayer.GetTile(pos) == null;
    }

    private bool IsValidVegetationPosition(Vector3Int cellPos, Vector3 worldPos)
    {
        // Check if position is within map bounds
        if (!baseLayer.cellBounds.Contains(cellPos))
            return false;

        // Check if position is not on water or road
        if (mapGen.waterLayer.GetTile(cellPos) != null || roadLayer.GetTile(cellPos) != null)
            return false;

        return true;
    }

    private (List<Vector3> inForest, List<Vector3> spillover) PlaceTreesInRegion(ForestRegion region)
    {
        var inForestTrees = new List<Vector3>();
        var spilloverTrees = new List<Vector3>();
        var randomOffset = Random.Range(0f, 1000f);

        // First, place trees inside the forest region
        foreach (var tile in region.Tiles)
        {
            var worldPos = baseLayer.CellToWorld(tile) + new Vector3(0.5f, 0.5f, 0f);
            
            if (allPlacedObjects.Any(p => Vector2.Distance(p, worldPos) < minDistance) ||
                inForestTrees.Any(p => Vector2.Distance(p, worldPos) < minDistance))
                continue;

            // Use noise for local density variation
            float nx = (tile.x + randomOffset) / (float)mapGen.mapWidth * noiseScale;
            float ny = (tile.y + randomOffset) / (float)mapGen.mapHeight * noiseScale;
            float localDensity = baseDensity + (Mathf.PerlinNoise(nx, ny) * 2 - 1) * noiseMagnitude;

            if (Random.value < localDensity && inForestTrees.Count < region.MaxTrees)
            {
                PlaceTree(worldPos);
                inForestTrees.Add(worldPos);
            }
        }

        // Then handle spillover
        if (Random.value < spilloverChance && inForestTrees.Count > 0)
        {
            int maxSpillover = Mathf.RoundToInt(inForestTrees.Count * maxSpilloverFraction);
            int spilloverCount = Random.Range(1, maxSpillover + 1);

            for (int i = 0; i < spilloverCount; i++)
            {
                // Try to place spillover trees
                for (int attempts = 0; attempts < 20; attempts++) // Limit attempts to prevent infinite loops
                {
                    float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                    float distance = Random.Range(minDistance, region.SpilloverRadius);
                    Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * distance;
                    Vector3 spillPos = region.Center + offset;
                    Vector3Int cellPos = baseLayer.WorldToCell(spillPos);

                    // Check if position is valid
                    if (IsValidVegetationPosition(cellPos, spillPos))
                    {
                        PlaceTree(spillPos);
                        spilloverTrees.Add(spillPos);
                        break;
                    }
                }
            }
        }

        return (inForestTrees, spilloverTrees);
    }

    private void PlaceTree(Vector3 position)
    {
        var prefab = treePrefabs[Random.Range(0, treePrefabs.Length)];
        var rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
        var tree = Instantiate(prefab, position, rotation, vegetationContainer);

        // Add sorting group for proper layering
        var sg = tree.AddComponent<SortingGroup>();
        sg.sortingLayerName = sortingLayerName;
        sg.sortingOrder = treeSortingOrder;

        // Add slight random scale variation
        float scale = Random.Range(0.9f, 1.1f);
        tree.transform.localScale *= scale;

        allPlacedObjects.Add(position);
    }
}
