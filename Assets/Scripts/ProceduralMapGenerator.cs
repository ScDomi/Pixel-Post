using UnityEngine;
using UnityEngine.Tilemaps;



public class ProceduralMapGenerator : MonoBehaviour
{
    public Tile tileType1; // Assign in the Inspector
    public Tile tileType2;
    public Tile tileType3;
    public Tilemap tilemap;

    public int mapWidth = 100;
    public int mapHeight = 100;

    void Start()
    {
        GenerateMap();
    }

    void GenerateMap()
    {
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                Tile selectedTile = null;

                if (x < mapWidth / 3)
                {
                    selectedTile = tileType1;
                }
                else if (x < 2 * mapWidth / 3)
                {
                    selectedTile = tileType2;
                }
                else
                {
                    selectedTile = tileType3;
                }

                tilemap.SetTile(new Vector3Int(x, y, 0), selectedTile);
            }
        }
    }
}