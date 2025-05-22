using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using Map;
using System.Linq;

public class TileManager : MonoBehaviour
{
    [HideInInspector] public Vector2Int origin;
    [HideInInspector] public Vector2Int biomeSize;

    [Header("Required References")]
    public BaseMapManager baseMapManager;

    [Header("Earth Tiles")]
    public Tile[] darkEarthTiles;      // Darker earth/dirt variants for region centers
    public Tile[] mediumEarthTiles;    // Medium earth/dirt variants
    public Tile[] lightEarthTiles;     // Lighter earth/dirt variants for edges
    
    [Header("Grass Tiles")]
    public Tile[] lightGrassTiles;     // Lighter grass variants
    public Tile[] mediumGrassTiles;    // Medium grass variants
    public Tile[] darkGrassTiles;      // Darker grass variants
    
    [Header("Water Tiles")]
    public Tile[] waterTileVariants;   // Different water tile variants

    [Header("Road Settings")]
    public RuleTile roadTile;          // Road tile with automatic connections

    // All initialization is now controlled by MapGenerationManager
    public void EnhanceTerrain()
    {
        Debug.Log($"TileManager ({this.name}): Starting terrain enhancement for assigned biome...");

        // Nur die Regionen des eigenen Bioms bearbeiten
        if (darkEarthTiles != null && (darkEarthTiles.Length > 0 || mediumEarthTiles?.Length > 0 || lightEarthTiles?.Length > 0))
        {
            foreach (var region in baseMapManager.EarthRegions)
            {
                if (IsRegionInMyBiome(region.Center))
                    EnhanceEarthRegion(region);
            }
        }

        if (lightGrassTiles != null || mediumGrassTiles != null || darkGrassTiles != null)
        {
            foreach (var region in baseMapManager.GrassRegions)
            {
                if (IsRegionInMyBiome(region.Center))
                    EnhanceGrassRegion(region);
            }
        }

        if (waterTileVariants != null && waterTileVariants.Length > 0)
        {
            foreach (var region in baseMapManager.LakeRegions)
            {
                if (IsRegionInMyBiome(region.Center))
                    EnhanceWaterRegion(region);
            }
        }

        // Hausregionen nur bearbeiten, wenn darkGrassTiles gesetzt sind (z.B. für das Gras-Biom)
        if (darkGrassTiles != null && darkGrassTiles.Length > 0)
        {
            foreach (var region in baseMapManager.HouseRegions)
            {
                if (IsRegionInMyBiome(region.Center))
                    EnhanceHouseRegion(region);
            }
        }

        Debug.Log($"TileManager ({this.name}): Terrain enhancement for assigned biome completed");
    }

    // Prüft, ob eine Region im Bereich dieses TileManagers liegt
    private bool IsRegionInMyBiome(Vector3 regionCenter)
    {
        var cell = baseMapManager.baseLayer.WorldToCell(regionCenter);
        return cell.x >= origin.x && cell.x < origin.x + biomeSize.x &&
               cell.y >= origin.y && cell.y < origin.y + biomeSize.y;
    }

    private void EnhanceEarthRegion(Map.EarthRegion region)
    {
        // Check if we have any earth tile variants
        if ((darkEarthTiles == null || darkEarthTiles.Length == 0) &&
            (mediumEarthTiles == null || mediumEarthTiles.Length == 0) &&
            (lightEarthTiles == null || lightEarthTiles.Length == 0)) 
        {
            Debug.LogWarning($"TileManager: No earth tile variants available for region {region.Name}");
            return;
        }

        // Get maximum distance from center for normalization
        float maxDistance = 0f;
        foreach (var pos in region.Tiles)
        {
            var worldPos = baseMapManager.baseLayer.CellToWorld(pos) + new Vector3(0.5f, 0.5f, 0f);
            float distance = Vector3.Distance(worldPos, region.Center);
            maxDistance = Mathf.Max(maxDistance, distance);
        }

        int darkTiles = 0, mediumTiles = 0, lightTiles = 0;
        foreach (var pos in region.Tiles)
        {
            var worldPos = baseMapManager.baseLayer.CellToWorld(pos) + new Vector3(0.5f, 0.5f, 0f);
            float distance = Vector3.Distance(worldPos, region.Center);
            float normalizedDistance = distance / maxDistance;

            // Add some noise to make the transitions less circular
            float noise = Mathf.PerlinNoise(pos.x * 0.2f, pos.y * 0.2f) * 0.3f;
            normalizedDistance = Mathf.Clamp01(normalizedDistance + noise);

            Tile[] selectedSet;
            if (normalizedDistance < 0.33f && darkEarthTiles != null && darkEarthTiles.Length > 0)
            {
                selectedSet = darkEarthTiles;
                darkTiles++;
            }
            else if (normalizedDistance < 0.66f && mediumEarthTiles != null && mediumEarthTiles.Length > 0)
            {
                selectedSet = mediumEarthTiles;
                mediumTiles++;
            }
            else if (lightEarthTiles != null && lightEarthTiles.Length > 0)
            {
                selectedSet = lightEarthTiles;
                lightTiles++;
            }
            else
            {
                selectedSet = (darkEarthTiles?.Length > 0 ? darkEarthTiles : 
                             mediumEarthTiles?.Length > 0 ? mediumEarthTiles : 
                             lightEarthTiles)!;
            }

            var tileIndex = Mathf.Abs((pos.x * 48271 + pos.y * 16807) % selectedSet.Length);
            baseMapManager.baseLayer.SetTile(pos, selectedSet[tileIndex]);
        }

        Debug.Log($"TileManager: Enhanced earth region {region.Name} with {darkTiles} dark, {mediumTiles} medium, {lightTiles} light tiles");
    }

    private void EnhanceGrassRegion(Map.GrassRegion region)
    {
        if (lightGrassTiles == null && mediumGrassTiles == null && darkGrassTiles == null)
        {
            Debug.LogWarning($"TileManager: No grass tile variants available for region {region.Name}");
            return;
        }

        int tileCount = 0;
        foreach (var pos in region.Tiles)
        {
            // Use Perlin noise for organic variation
            float baseNoise = Mathf.PerlinNoise(pos.x * 0.1f, pos.y * 0.1f);
            float detailNoise = Mathf.PerlinNoise(pos.x * 0.3f, pos.y * 0.3f) * 0.3f;
            float noiseVal = baseNoise + detailNoise;

            Tile[] selectedSet;
            if (noiseVal < 0.33f && lightGrassTiles != null && lightGrassTiles.Length > 0)
                selectedSet = lightGrassTiles;
            else if (noiseVal < 0.66f && mediumGrassTiles != null && mediumGrassTiles.Length > 0)
                selectedSet = mediumGrassTiles;
            else if (darkGrassTiles != null && darkGrassTiles.Length > 0)
                selectedSet = darkGrassTiles;
            else
            {
                selectedSet = (darkGrassTiles?.Length > 0 ? darkGrassTiles : 
                             mediumGrassTiles?.Length > 0 ? mediumGrassTiles : 
                             lightGrassTiles)!;
            }

            var tileIndex = Mathf.Abs((pos.x * 48271 + pos.y * 16807) % selectedSet.Length);
            baseMapManager.grassLayer.SetTile(pos, selectedSet[tileIndex]);
            tileCount++;
        }

        Debug.Log($"TileManager: Enhanced grass region {region.Name} with {tileCount} variant tiles");
    }

    private void EnhanceWaterRegion(Map.LakeRegion region)
    {
        if (waterTileVariants == null || waterTileVariants.Length == 0)
        {
            Debug.LogWarning($"TileManager: No water tile variants available for region {region.Name}");
            return;
        }

        int tileCount = 0;
        foreach (var pos in region.Tiles)
        {
            var tileIndex = Mathf.Abs((pos.x * 48271 + pos.y * 16807) % waterTileVariants.Length);
            baseMapManager.waterLayer.SetTile(pos, waterTileVariants[tileIndex]);
            tileCount++;
        }

        Debug.Log($"TileManager: Enhanced water region {region.Name} with {tileCount} variant tiles");
    }

    private void EnhanceHouseRegion(Map.HouseRegion region)
    {
        Debug.Log($"TileManager: Starting to enhance house region {region.Name}");
        
        if (darkGrassTiles == null || darkGrassTiles.Length == 0)
        {
            Debug.LogWarning($"TileManager: No dark grass tiles available for house region {region.Name}");
            return;
        }

        float radius = 5f;
        var center = region.Center;
        Debug.Log($"TileManager: Processing house region at {center} with radius {radius}");

        var centerCell = baseMapManager.grassLayer.WorldToCell(center);
        int radiusCells = Mathf.CeilToInt(radius);
        int tilesChanged = 0;
        int tilesChecked = 0;

        for (int x = -radiusCells; x <= radiusCells; x++)
        {
            for (int y = -radiusCells; y <= radiusCells; y++)
            {
                var checkPos = centerCell + new Vector3Int(x, y, 0);
                tilesChecked++;
                
                if (baseMapManager.grassLayer.GetTile(checkPos) == null)
                {
                    Debug.Log($"TileManager: No grass tile at position {checkPos}");
                    continue;
                }

                float dist = Vector2.Distance(
                    new Vector2(checkPos.x, checkPos.y),
                    new Vector2(centerCell.x, centerCell.y)
                );

                if (dist <= radius)
                {
                    var tileIndex = Mathf.Abs((checkPos.x * 48271 + checkPos.y * 16807) % darkGrassTiles.Length);
                    baseMapManager.grassLayer.SetTile(checkPos, darkGrassTiles[tileIndex]);
                    tilesChanged++;
                }
            }
        }

        Debug.Log($"TileManager: House region {region.Name} enhancement complete. Checked {tilesChecked} tiles, changed {tilesChanged} to dark grass");
    }
}
