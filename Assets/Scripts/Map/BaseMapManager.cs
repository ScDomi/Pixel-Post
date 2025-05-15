using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Assertions;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Map;

public class BaseMapManager : MonoBehaviour
{
    [Header("Map Size")]
    public int mapWidth = 100;
    public int mapHeight = 100;

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

    void Start()
    {
        ValidateDependencies();
        InitializeRandomization();
        GenerateBaseMap();
        DetectRegions();
    }

    private void ValidateDependencies()
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

    private void InitializeRandomization()
    {
        if (useRandomSeed)
            seed = Random.Range(0, 99999);
        
        Random.InitState(seed);
        xOffset = Random.Range(0f, 99999f);
        yOffset = Random.Range(0f, 99999f);
    }

    private void GenerateBaseMap()
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
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                baseLayer.SetTile(new Vector3Int(x, y, 0), earthTile);
            }
        }
    }

    private void GenerateGrassLayer()
    {
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                float nx = (x + xOffset) / (float)mapWidth * noiseScale;
                float ny = (y + yOffset) / (float)mapHeight * noiseScale;
                float noise = Mathf.PerlinNoise(nx, ny);

                if (noise > grassThreshold)
                {
                    grassLayer.SetTile(new Vector3Int(x, y, 0), grassTile);
                }
            }
        }
    }

    private void GenerateWaterLayer()
    {
        float waterXOffset = xOffset + 1000f;
        float waterYOffset = yOffset + 1000f;

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                float nx = (x + waterXOffset) / (float)mapWidth * noiseScale;
                float ny = (y + waterYOffset) / (float)mapHeight * noiseScale;
                float noise = Mathf.PerlinNoise(nx, ny);

                if (noise < waterThreshold)
                {
                    waterLayer.SetTile(new Vector3Int(x, y, 0), waterTile);
                }
            }
        }
    }

    private void DetectRegions()
    {
        var bounds = baseLayer.cellBounds;
        var visited = new HashSet<Vector3Int>();

        // Find Earth Regions (tiles with earth but no grass)
        FindRegions<Map.EarthRegion>(
            bounds, visited,
            pos => baseLayer.GetTile(pos) != null && grassLayer.GetTile(pos) == null && waterLayer.GetTile(pos) == null,
            (name, tiles) => new Map.EarthRegion(name, tiles, baseLayer),
            EarthRegions
        );

        // Find Grass Regions
        visited.Clear();
        FindRegions<Map.GrassRegion>(
            bounds, visited,
            pos => grassLayer.GetTile(pos) != null && waterLayer.GetTile(pos) == null,
            (name, tiles) => new Map.GrassRegion(name, tiles, baseLayer),
            GrassRegions
        );

        // Find Lake Regions
        visited.Clear();
        FindRegions<Map.LakeRegion>(
            bounds, visited,
            pos => waterLayer.GetTile(pos) != null,
            (name, tiles) => new Map.LakeRegion(name, tiles, baseLayer),
            LakeRegions
        );

        // Wait a frame to let the ObjectManager place houses
        StartCoroutine(DetectHouseRegionsAfterPlacement());
    }

    private IEnumerator DetectHouseRegionsAfterPlacement()
    {
        yield return new WaitForSeconds(0.5f); // Wait for houses to be placed

        var objectManager = GetComponent<ObjectManager>();
        if (objectManager != null)
        {
            var houseFronts = objectManager.GetHouseFrontPositions();
            HouseRegions.Clear();

            // Create house regions around each house
            foreach (var front in houseFronts)
            {
                var housePos = front.position - front.direction; // Get actual house position
                var regionTiles = new List<Vector3Int>();
                
                // Add tiles in a 3x3 area around the house
                for (int x = -1; x <= 1; x++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        var checkPos = housePos + new Vector3Int(x, y, 0);
                        if (grassLayer.GetTile(checkPos) != null)
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
