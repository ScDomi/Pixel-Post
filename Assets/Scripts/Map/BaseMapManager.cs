using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Assertions;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Map;

public class BaseMapManager : MonoBehaviour
{
    [HideInInspector] public Vector2Int origin;
    [HideInInspector] public Vector2Int biomeSize;

    [Header("Required References")]
    public ObjectManager objectManager;   // Reference to ObjectManager

    [Header("Noise Settings")]
    public float noiseScale = 10f;
    private float xOffset;
    private float yOffset;
    public bool useRandomSeed = true;
    public int seed = 0;

    [Header("Terrain Layers")]
    public Tilemap baseLayer;    // Base earth layer
    public Tilemap grassLayer;   // Grass overlay
    public Tilemap waterLayer;   // Water overlay
    public Tilemap roadLayer;    // Road overlay

    [Header("Basic Terrain Tiles")]
    public Tile earthTile;       // Base terrain
    public Tile grassTile;       // Grass overlay
    public Tile waterTile;       // Water overlay
    public RuleTile roadTile;    // Road tile with automatic connections

    [Header("Generation Thresholds")]
    [Range(0f, 1f)]
    public float grassThreshold = 0.4f;     // Above this value will be grass
    [Range(0f, 1f)]
    public float waterThreshold = 0.3f;     // Below this value will be water

    [Header("Region Detection")]
    public int minRegionSize = 4;           // Minimum size for a valid region

    // Public access to detected regions
    public List<Map.EarthRegion> EarthRegions { get; private set; } = new List<Map.EarthRegion>();
    public List<Map.GrassRegion> GrassRegions { get; private set; } = new List<Map.GrassRegion>();
    public List<Map.LakeRegion> LakeRegions { get; private set; } = new List<Map.LakeRegion>();
    public List<Map.HouseRegion> HouseRegions { get; private set; } = new List<Map.HouseRegion>();

    // Methods are now public so they can be called by MapGenerationManager
    public void ValidateDependencies()
    {
        Assert.IsNotNull(baseLayer, "Base layer is required!");
        Assert.IsNotNull(grassLayer, "Grass layer is required!");
        Assert.IsNotNull(waterLayer, "Water layer is required!");
        Assert.IsNotNull(roadLayer, "Road layer is required!");
        
        Assert.IsNotNull(earthTile, "Earth tile is required!");
        Assert.IsNotNull(grassTile, "Grass tile is required!");
        Assert.IsNotNull(waterTile, "Water tile is required!");
        Assert.IsNotNull(roadTile, "Road tile is required!");
    }

    public void InitializeRandomization()
    {
        if (useRandomSeed)
            seed = Random.Range(0, 99999);
        
        Random.InitState(seed);
        xOffset = Random.Range(0f, 99999f);
        yOffset = Random.Range(0f, 99999f);
    }

    public void GenerateBaseMap()
    {
        // 1. Fill entire base layer with earth
        FillBaseLayer();
        
        // 2. Generate and overlay grass
        GenerateGrassLayer();
        
        // 3. Generate and overlay water
        GenerateWaterLayer();
    }

    private void FillBaseLayer()
    {
        for (int x = 0; x < biomeSize.x; x++)
        for (int y = 0; y < biomeSize.y; y++)
        {
            var cell = new Vector3Int(origin.x + x, origin.y + y, 0);
            baseLayer.SetTile(cell, earthTile);
        }
    }

    private void GenerateGrassLayer()
    {
        for (int x = 0; x < biomeSize.x; x++)
        for (int y = 0; y < biomeSize.y; y++)
        {
            float nx = (x + origin.x) / (float)biomeSize.x * noiseScale;
            float ny = (y + origin.y) / (float)biomeSize.y * noiseScale;
            if (Mathf.PerlinNoise(nx, ny) > grassThreshold)
                grassLayer.SetTile(new Vector3Int(origin.x + x, origin.y + y, 0), grassTile);
        }
    }

    private void GenerateWaterLayer()
    {
        for (int x = 0; x < biomeSize.x; x++)
        for (int y = 0; y < biomeSize.y; y++)
        {
            float nx = (x + origin.x + 1000f) / biomeSize.x * noiseScale;
            float ny = (y + origin.y + 1000f) / biomeSize.y * noiseScale;
            if (Mathf.PerlinNoise(nx, ny) < waterThreshold)
                waterLayer.SetTile(new Vector3Int(origin.x + x, origin.y + y, 0), waterTile);
        }
    }

    public void DetectRegions()
    {
        Debug.Log("BaseMapManager: Starting region detection...");
        var bounds = baseLayer.cellBounds;
        var visited = new HashSet<Vector3Int>();

        // Find Earth Regions (tiles with earth but no grass)
        FindRegions<Map.EarthRegion>(
            bounds, visited,
            pos => baseLayer.GetTile(pos) != null && grassLayer.GetTile(pos) == null && waterLayer.GetTile(pos) == null,
            (name, tiles) => new Map.EarthRegion(name, tiles, baseLayer),
            EarthRegions
        );
        Debug.Log($"BaseMapManager: Found {EarthRegions.Count} earth regions");
        for (int i = 0; i < EarthRegions.Count; i++)
        {
            var region = EarthRegions[i];
            Debug.Log($"EarthRegion {i}: Name={region.Name}, Size={region.Size}, Center={region.Center}, Bounds={region.Bounds}");
        }

        // Find Grass Regions
        visited.Clear();
        FindRegions<Map.GrassRegion>(
            bounds, visited,
            pos => grassLayer.GetTile(pos) != null && waterLayer.GetTile(pos) == null,
            (name, tiles) => new Map.GrassRegion(name, tiles, baseLayer),
            GrassRegions
        );
        Debug.Log($"BaseMapManager: Found {GrassRegions.Count} grass regions");

        // Find Lake Regions
        visited.Clear();
        FindRegions<Map.LakeRegion>(
            bounds, visited,
            pos => waterLayer.GetTile(pos) != null,
            (name, tiles) => new Map.LakeRegion(name, tiles, baseLayer),
            LakeRegions
        );
        Debug.Log($"BaseMapManager: Found {LakeRegions.Count} lake regions");

        // Detect House Regions
        DetectHouseRegions();
    }

    private void DetectHouseRegions()
    {
        Debug.Log("BaseMapManager: Starting house region detection...");
        // Start the coroutine to detect house regions after placement
        StartCoroutine(DetectHouseRegionsAfterPlacement());
    }

    private IEnumerator DetectHouseRegionsAfterPlacement()
    {
        Debug.Log("BaseMapManager: Waiting for house placement...");
        yield return new WaitForSeconds(0.5f);

        Debug.Log($"BaseMapManager: ObjectManager reference {(objectManager != null ? "found" : "not found")}");
        
        if (objectManager != null)
        {
            var houseFronts = objectManager.GetHouseFrontPositions();
            Debug.Log($"BaseMapManager: Found {houseFronts?.Count ?? 0} house positions");

            HouseRegions.Clear();

            // Create house regions centered on the house front
            if (houseFronts != null)
            {
                foreach (var front in houseFronts)
                {
                    Debug.Log($"BaseMapManager: Processing house at position {front.position}");
                    var regionTiles = new List<Vector3Int>();
                    var radius = 3; // Radius for house region influence
                    
                    // Add tiles in a circular area around the house front
                    for (int x = -radius; x <= radius; x++)
                    {
                        for (int y = -radius; y <= radius; y++)
                        {
                            var checkPos = front.position + new Vector3Int(x, y, 0);
                            if (Vector2.Distance(new Vector2(x, y), Vector2.zero) <= radius &&
                                grassLayer.GetTile(checkPos) != null)
                            {
                                regionTiles.Add(checkPos);
                            }
                        }
                    }

                    if (regionTiles.Count > 0)
                    {
                        var region = new Map.HouseRegion($"House_{HouseRegions.Count}", regionTiles, baseLayer);
                        HouseRegions.Add(region);
                    }
                }
            }
        }
    }

    private void FindRegions<T>(
        BoundsInt bounds,
        HashSet<Vector3Int> visited,
        System.Func<Vector3Int, bool> isValidTile,
        System.Func<string, List<Vector3Int>, T> createRegion,
        List<T> regionList) where T : Map.BaseRegion
    {
        int regionCount = 0;

        foreach (var pos in bounds.allPositionsWithin)
        {
            if (visited.Contains(pos)) continue;
            visited.Add(pos);
            
            if (!isValidTile(pos)) continue;

            var tiles = new List<Vector3Int>();
            var queue = new Queue<Vector3Int>();
            queue.Enqueue(pos);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                tiles.Add(current);

                foreach (var dir in new[] { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right })
                {
                    var next = current + dir;
                    if (!bounds.Contains(next) || visited.Contains(next)) continue;
                    visited.Add(next);
                    if (isValidTile(next))
                        queue.Enqueue(next);
                }
            }

            if (tiles.Count >= minRegionSize)
            {
                var region = createRegion($"{typeof(T).Name}_{regionCount++}", tiles);
                regionList.Add(region);
            }
        }
    }
}
