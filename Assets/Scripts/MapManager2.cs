using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Assertions;

public class ProceduralMapGenerator : MonoBehaviour
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
    public Tilemap propsLayer;   // Props overlay (for misc decorations)

    [Header("Terrain Tiles")]
    public Tile earthTile;       // Base terrain
    public Tile grassTile;       // Grass overlay
    public Tile waterTile;       // Water overlay
    public RuleTile roadTile;    // Road tile with automatic connections

    [Header("Generation Thresholds")]
    [Range(0f, 1f)]
    public float grassThreshold = 0.4f;     // Above this value will be grass
    [Range(0f, 1f)]
    public float waterThreshold = 0.3f;     // Below this value will be water

    void Start()
    {
        ValidateLayers();
        InitializeRandomization();
        GenerateMap();
    }

    void ValidateLayers()
    {
        Assert.IsNotNull(baseLayer, "Base layer is required!");
        Assert.IsNotNull(grassLayer, "Grass layer is required!");
        Assert.IsNotNull(waterLayer, "Water layer is required!");
        Assert.IsNotNull(roadLayer, "Road layer is required!");
        Assert.IsNotNull(propsLayer, "Props layer is required!");
        
        Assert.IsNotNull(earthTile, "Earth tile is required!");
        Assert.IsNotNull(grassTile, "Grass tile is required!");
        Assert.IsNotNull(waterTile, "Water tile is required!");
        Assert.IsNotNull(roadTile, "Road tile is required!");
    }

    void InitializeRandomization()
    {
        if (useRandomSeed)
        {
            seed = Random.Range(0, 99999);
        }
        Random.InitState(seed);
        
        xOffset = Random.Range(0f, 99999f);
        yOffset = Random.Range(0f, 99999f);
    }

    void GenerateMap()
    {
        // 1. Fill entire base layer with earth
        FillBaseLayer();
        
        // 2. Generate and overlay grass
        GenerateGrassLayer();
        
        // 3. Generate and overlay water
        GenerateWaterLayer();
    }

    void FillBaseLayer()
    {
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                baseLayer.SetTile(new Vector3Int(x, y, 0), earthTile);
            }
        }
    }

    void GenerateGrassLayer()
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

    void GenerateWaterLayer()
    {
        // Use different offset for water to create unique pattern
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
}