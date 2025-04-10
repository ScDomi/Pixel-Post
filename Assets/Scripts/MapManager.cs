using UnityEngine;
using UnityEngine.Tilemaps;

public class MapManager : MonoBehaviour
{
    // Tile references for each biome
    public RuleTile biomeATile1;
    public RuleTile biomeATile2;
    public RuleTile biomeATile3;
    public RuleTile biomeBTile1;
    public RuleTile biomeBTile2;
    public RuleTile biomeBTile3;

    // Reference to the Tilemap
    public Tilemap tilemap;

    // Map dimensions
    public int mapWidth = 100;
    public int mapHeight = 100;

    // Scale for Perlin Noise
    public float noiseScale = 10f;

    // Called when the game starts
    void Start()
    {
        GenerateMap();
    }

    // Generate a procedural map based on Perlin Noise
    void GenerateMap()
    {
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                float xCoord = (float)x / mapWidth * noiseScale;
                float yCoord = (float)y / mapHeight * noiseScale;
                float noiseValue = Mathf.PerlinNoise(xCoord, yCoord);

                RuleTile selectedTile = SelectTileForBiome(noiseValue, y);
                tilemap.SetTile(new Vector3Int(x, y, 0), selectedTile);
            }
        }
    }

    // Determines which tile to use based on the noise value and y position
    RuleTile SelectTileForBiome(float noiseValue, int yPos)
    {
        // Create a smooth transition zone
        float transitionPoint = mapHeight / 2f;
        float transitionZone = 5f; // Size of the transition zone

        // If we're in the transition zone, add some noise to create a more natural border
        if (Mathf.Abs(yPos - transitionPoint) < transitionZone)
        {
            // Add some randomness to the transition point
            float randomOffset = Mathf.PerlinNoise(yPos * 0.1f, 0) * transitionZone * 2;
            transitionPoint += randomOffset;
        }

        // Determine which biome set to use
        if (yPos < transitionPoint)
        {
            // Biome A (bottom half)
            if (noiseValue < 0.33f)
                return biomeATile1;
            else if (noiseValue < 0.66f)
                return biomeATile2;
            else
                return biomeATile3;
        }
        else
        {
            // Biome B (top half)
            if (noiseValue < 0.33f)
                return biomeBTile1;
            else if (noiseValue < 0.66f)
                return biomeBTile3;
            else
                return biomeBTile2;
        }
    }
}
