using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using Map;
using System.Linq;

public class TileManager : MonoBehaviour
{
    [Header("Required References")]
    public BaseMapManager baseMapManager;

    [Header("Earth Tiles")]
    public Tile[] earthTileVariants;      // Different earth/dirt tile variants
    
    [Header("Grass Tiles")]
    public Tile[] lightGrassTiles;        // Lighter grass variants
    public Tile[] mediumGrassTiles;       // Medium grass variants
    public Tile[] darkGrassTiles;         // Darker grass variants
    
    [Header("Water Tiles")]
    public Tile[] waterTileVariants;      // Different water tile variants

    [Header("Road Settings")]
    public RuleTile roadTile;             // Road tile with automatic connections

    void Start()
    {
        // Wait for base map generation to complete
        Invoke(nameof(EnhanceTerrain), 0.2f);
    }

    private void EnhanceTerrain()
    {
        // Replace basic earth tiles with variants
        foreach (var region in baseMapManager.EarthRegions)
        {
            EnhanceEarthRegion(region);
        }

        // Replace basic grass tiles with variants
        foreach (var region in baseMapManager.GrassRegions)
        {
            EnhanceGrassRegion(region);
        }

        // Replace basic water tiles with variants
        foreach (var region in baseMapManager.LakeRegions)
        {
            EnhanceWaterRegion(region);
        }
    }

    private void EnhanceEarthRegion(Map.EarthRegion region)
    {
        if (earthTileVariants == null || earthTileVariants.Length == 0) return;

        foreach (var pos in region.Tiles)
        {
            // Use position for consistent random selection
            var tileIndex = Mathf.Abs((pos.x * 48271 + pos.y * 16807) % earthTileVariants.Length);
            baseMapManager.baseLayer.SetTile(pos, earthTileVariants[tileIndex]);
        }
    }

    private void EnhanceGrassRegion(Map.GrassRegion region)
    {
        if (lightGrassTiles == null && mediumGrassTiles == null && darkGrassTiles == null) return;

        // Combine all available grass tiles
        var allGrassTiles = new List<Tile>();
        if (lightGrassTiles != null) allGrassTiles.AddRange(lightGrassTiles);
        if (mediumGrassTiles != null) allGrassTiles.AddRange(mediumGrassTiles);
        if (darkGrassTiles != null) allGrassTiles.AddRange(darkGrassTiles);

        if (allGrassTiles.Count == 0) return;

        foreach (var pos in region.Tiles)
        {
            // Use noise for more natural variation
            float noiseVal = Mathf.PerlinNoise(pos.x * 0.1f, pos.y * 0.1f);
            
            Tile[] selectedSet;
            if (noiseVal < 0.33f && lightGrassTiles != null && lightGrassTiles.Length > 0)
                selectedSet = lightGrassTiles;
            else if (noiseVal < 0.66f && mediumGrassTiles != null && mediumGrassTiles.Length > 0)
                selectedSet = mediumGrassTiles;
            else if (darkGrassTiles != null && darkGrassTiles.Length > 0)
                selectedSet = darkGrassTiles;
            else
                selectedSet = allGrassTiles.ToArray();

            var tileIndex = Mathf.Abs((pos.x * 48271 + pos.y * 16807) % selectedSet.Length);
            baseMapManager.grassLayer.SetTile(pos, selectedSet[tileIndex]);
        }
    }

    private void EnhanceWaterRegion(Map.LakeRegion region)
    {
        if (waterTileVariants == null || waterTileVariants.Length == 0) return;

        foreach (var pos in region.Tiles)
        {
            var tileIndex = Mathf.Abs((pos.x * 48271 + pos.y * 16807) % waterTileVariants.Length);
            baseMapManager.waterLayer.SetTile(pos, waterTileVariants[tileIndex]);
        }
    }
}
