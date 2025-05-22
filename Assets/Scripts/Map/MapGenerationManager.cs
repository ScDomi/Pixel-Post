using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Tilemaps;

public class MapGenerationManager : MonoBehaviour
{
    [Header("Biome ScriptableObjects")]
    public List<MapBiomeConfig> biomeConfigs;

    [Header("Globale Biomeinstellungen")]
    public Vector2Int biomeSize = new Vector2Int(100, 50); // Feste Größe für alle Biome

    private List<BaseMapManager> allBMM = new List<BaseMapManager>();
    private List<ObjectManager> allOM  = new List<ObjectManager>();
    private List<TileManager> allTM    = new List<TileManager>();
    private RoadManager roadManager;

    private Tilemap FindTilemap(string name)
    {
        var go = GameObject.Find(name);
        if (go == null) Debug.LogError($"Tilemap GameObject '{name}' not found!");
        return go ? go.GetComponent<Tilemap>() : null;
    }

    private void Start()
    {
        // Tilemaps aus der Szene suchen (einmalig, da für alle Biome gleich)
        var baseLayer = FindTilemap("BaseLayer");
        var grassLayer = FindTilemap("EarthLayer"); // oder "GrassLayer" falls vorhanden
        var waterLayer = FindTilemap("WaterLayer");
        var roadLayer = FindTilemap("RoadLayer");

        for (int i = 0; i < biomeConfigs.Count; i++)
        {
            var cfg = biomeConfigs[i];
            // Überschreibe die Größe im SO, falls nötig
            cfg.biomeSize = biomeSize;
            Vector2Int origin = new Vector2Int(0, i * biomeSize.y);

            // BaseMapManager erzeugen
            var goB = new GameObject($"BMM_{i}"); goB.transform.parent = transform;
            var b = goB.AddComponent<BaseMapManager>();
            b.origin = origin; b.biomeSize = biomeSize;
            b.baseLayer = baseLayer;
            b.grassLayer = grassLayer;
            b.waterLayer = waterLayer;
            b.roadLayer = roadLayer;
            b.earthTile = cfg.earthTile;
            b.grassTile = cfg.grassTile;
            b.waterTile = cfg.waterTile;
            b.roadTile = cfg.roadTile;
            b.noiseScale = cfg.noiseScale;
            b.useRandomSeed = cfg.useRandomSeed;
            b.seed = cfg.seed;
            b.grassThreshold = cfg.grassThreshold;
            b.waterThreshold = cfg.waterThreshold;
            b.minRegionSize = cfg.minRegionSize;
            allBMM.Add(b);

            // ObjectManager erzeugen
            var goO = new GameObject($"OM_{i}"); goO.transform.parent = transform;
            var o = goO.AddComponent<ObjectManager>();
            o.origin = origin; o.biomeSize = biomeSize; o.baseMapManager = b;
            o.treePrefabs = cfg.treePrefabs;
            o.highGrassPrefab = cfg.highGrassPrefab;
            o.lowGrassPrefab = cfg.lowGrassPrefab;
            o.housePrefabs = cfg.housePrefabs;
            o.minHouseDistance = cfg.minHouseDistance;
            o.highGrassRadius = cfg.highGrassRadius;
            o.lowGrassRadius = cfg.lowGrassRadius;
            o.treeDensity = cfg.treeDensity;
            o.lakeTreeChance = cfg.lakeTreeChance;
            o.lakeHighGrassChance = cfg.lakeHighGrassChance;
            o.lakeLowGrassChance = cfg.lakeLowGrassChance;
            o.roadGrassChance = cfg.roadGrassChance;
            o.roadHighGrassRatio = cfg.roadHighGrassRatio;
            o.sortingLayerName = cfg.sortingLayerName;
            o.treeSortingOrder = cfg.treeSortingOrder;
            o.houseSortingOrder = cfg.houseSortingOrder;
            o.highGrassSortingOrder = cfg.highGrassSortingOrder;
            o.lowGrassSortingOrder = cfg.lowGrassSortingOrder;
            allOM.Add(o);

            // TileManager erzeugen
            var goT = new GameObject($"TM_{i}"); goT.transform.parent = transform;
            var t = goT.AddComponent<TileManager>();
            t.origin = origin; t.biomeSize = biomeSize; t.baseMapManager = b;
            t.darkEarthTiles = cfg.darkEarthTiles;
            t.mediumEarthTiles = cfg.mediumEarthTiles;
            t.lightEarthTiles = cfg.lightEarthTiles;
            t.lightGrassTiles = cfg.lightGrassTiles;
            t.mediumGrassTiles = cfg.mediumGrassTiles;
            t.darkGrassTiles = cfg.darkGrassTiles;
            t.waterTileVariants = cfg.waterTileVariants;
            t.roadTile = cfg.roadTile;
            allTM.Add(t);
        }
        StartCoroutine(GenerateAll());
    }

    private IEnumerator GenerateAll()
    {
        foreach(var b in allBMM){ b.ValidateDependencies(); b.InitializeRandomization(); b.GenerateBaseMap(); }
        yield return null;
        foreach(var o in allOM) o.PlaceHouses(); yield return new WaitForSeconds(0.5f);
        foreach(var b in allBMM) b.DetectRegions(); yield return new WaitForSeconds(0.5f);
        foreach(var t in allTM) t.EnhanceTerrain(); yield return new WaitForSeconds(0.3f);
        foreach (var x in allOM) x.PlaceRemainingObjects(); yield return new WaitForSeconds(0.3f);

        // Logging: Anzahl Häuser pro Biome
        int totalHouses = 0;
        for (int i = 0; i < allOM.Count; i++)
        {
            var houseFronts = allOM[i].GetHouseFrontPositions();
            Debug.Log($"Biome #{i}: Houses found: {houseFronts.Count}");
            foreach (var hf in houseFronts)
                Debug.Log($"Biome #{i} House at {hf.position} dir {hf.direction}");
            totalHouses += houseFronts.Count;
        }

        // RoadManager erzeugen und alle HouseFronts übergeben
        var goR = new GameObject("RoadManager"); goR.transform.parent = transform;
        roadManager = goR.AddComponent<RoadManager>();
        roadManager.baseMapManager = allBMM[0];
        // Setze globalen Bereich für das Pathfinding
        roadManager.globalOrigin = new Vector2Int(0, 0);
        roadManager.globalSize = new Vector2Int(biomeSize.x, biomeSize.y * biomeConfigs.Count);
        var allHouseFronts = new List<RoadManager.HouseFront>();
        foreach (var om in allOM)
            allHouseFronts.AddRange(om.GetHouseFrontPositions());
        Debug.Log($"Total houses for road connection: {allHouseFronts.Count}");
        foreach (var hf in allHouseFronts)
            Debug.Log($"RoadManager House at {hf.position} dir {hf.direction}");
        roadManager.SetHouseFronts(allHouseFronts);
        roadManager.enabled = true;

        Debug.Log("All biomes generated.");
    }
}
