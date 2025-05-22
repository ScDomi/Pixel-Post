using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "MapBiomeConfig", menuName = "Map/MapBiomeConfig", order = 1)]
public class MapBiomeConfig : ScriptableObject
{
    public Vector2Int biomeSize = new Vector2Int(100, 50);

    [Header("Terrain Layers")]
    // Removed Tilemap references as per the change request

    [Header("Basic Terrain Tiles")]
    public Tile earthTile;
    public Tile grassTile;
    public Tile waterTile;
    public RuleTile roadTile;

    [Header("Noise & Thresholds")]
    public float noiseScale = 10f;
    public bool useRandomSeed = true;
    public int seed = 0;
    [Range(0f, 1f)] public float grassThreshold = 0.4f;
    [Range(0f, 1f)] public float waterThreshold = 0.3f;
    public int minRegionSize = 4;

    [Header("Tile Variants")]
    public Tile[] darkEarthTiles;
    public Tile[] mediumEarthTiles;
    public Tile[] lightEarthTiles;
    public Tile[] lightGrassTiles;
    public Tile[] mediumGrassTiles;
    public Tile[] darkGrassTiles;
    public Tile[] waterTileVariants;

    [Header("Object Manager Prefabs")]
    public GameObject[] treePrefabs;
    public GameObject highGrassPrefab;
    public GameObject lowGrassPrefab;
    public GameObject[] housePrefabs;

    [Header("Object Manager Settings")]
    [Range(0f, 1f)] public float treeDensity = 0.3f;
    public float minTreeDistance = 2f;
    public float minTreeRoadDistance = 3f;
    public float highGrassRadius = 5f;
    public float lowGrassRadius = 8f;
    [Range(0f, 1f)] public float highGrassDensity = 0.4f;
    [Range(0f, 1f)] public float lowGrassDensity = 0.3f;
    [Range(0f, 1f)] public float lakeTreeChance = 0.2f;
    public float lakeEffectRadius = 6f;
    [Range(0f, 1f)] public float lakeHighGrassChance = 0.4f;
    [Range(0f, 1f)] public float lakeLowGrassChance = 0.5f;
    public float roadGrassRadius = 3f;
    [Range(0f, 1f)] public float roadGrassChance = 0.3f;
    [Range(0f, 1f)] public float roadHighGrassRatio = 0.4f;
    public float minHouseDistance = 5f;

    [Header("Sorting Layers")]
    public string sortingLayerName = "Props";
    public int treeSortingOrder = 3;
    public int houseSortingOrder = 2;
    public int highGrassSortingOrder = 1;
    public int lowGrassSortingOrder = 0;
}