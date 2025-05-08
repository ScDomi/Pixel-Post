using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

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

    [Header("Terrain Tiles")]
    public Tilemap baseLayer;
    public Tile waterTile;
    public Tile earthTile;
    public Tile grassTile;
    public Tile desertTile;

    [Header("Road Generation")]
    public Tilemap roadLayer;
    public Tile roadTile;
    public int targetRoadTiles = 400;       // Wie viele *einzigartige* Erd-Kacheln zur Straße werden sollen
    [Range(0f, 1f)]
    public float turnChance = 0.05f;        // 5% Chance, bei jedem Schritt die Richtung neu zu wählen

    void Start()
    {
        InitializeRandomization();
        GenerateTerrain();
        //GenerateRoadsOnEarth();
    }

    void InitializeRandomization()
    {
        if (useRandomSeed)
        {
            seed = Random.Range(0, 99999);
        }
        Random.InitState(seed);
        
        // Generiere zufällige Offsets für die Noise-Map
        xOffset = Random.Range(0f, 99999f);
        yOffset = Random.Range(0f, 99999f);
    }

    /// <summary>
    /// Malt das Terrain (Wasser, Erde, Gras, Wüste) wie gehabt.
    /// </summary>
    void GenerateTerrain()
    {
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                float nx = (x + xOffset) / (float)mapWidth * noiseScale;
                float ny = (y + yOffset) / (float)mapHeight * noiseScale;
                float h  = Mathf.PerlinNoise(nx, ny);

                var pos = new Vector3Int(x, y, 0);
                if (h < 0.2f)       baseLayer.SetTile(pos, waterTile);
                else if (h < 0.35f)  baseLayer.SetTile(pos, earthTile);
                else if (h < 0.8f)  baseLayer.SetTile(pos, grassTile);
                else                baseLayer.SetTile(pos, desertTile);
            }
        }
    }

    /// <summary>
    /// Führt einen Random-Walk *nur* auf Erd-Kacheln durch und legt dort Straßen-Tiles.
    /// </summary>
    void GenerateRoadsOnEarth()
    {
        // 1) Alle Erd-Koordinaten sammeln
        List<Vector2Int> earthCells = new List<Vector2Int>();
        for (int x = 0; x < mapWidth; x++)
            for (int y = 0; y < mapHeight; y++)
                if (baseLayer.GetTile(new Vector3Int(x, y, 0)) == earthTile)
                    earthCells.Add(new Vector2Int(x, y));

        if (earthCells.Count == 0) return;

        // 2) Start-Punkt zufällig wählen
        Vector2Int pos = earthCells[Random.Range(0, earthCells.Count)];

        // 3) Initiale Laufrichtung
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        Vector2Int dir = dirs[Random.Range(0, dirs.Length)];

        // 4) Random-Walk, bis wir genug *einzigartige* Tiles haben
        var roadCells = new HashSet<Vector2Int>();
        while (roadCells.Count < targetRoadTiles)
        {
            // a) Aktuelle Position als Straße merken
            roadCells.Add(pos);

            // b) Alle möglichen nächsten Richtungen sammeln,
            //    die noch Erd-Tile sind
            var options = new List<Vector2Int>();
            foreach (var d in dirs)
            {
                var np = pos + d;
                if (np.x >= 0 && np.x < mapWidth && np.y >= 0 && np.y < mapHeight)
                {
                    if (baseLayer.GetTile(new Vector3Int(np.x, np.y, 0)) == earthTile)
                        options.Add(d);
                }
            }
            if (options.Count == 0)
                break; // kein Erd-Nachbar mehr => Abbruch

            // c) Mit 'turnChance' neue Richtung wählen, sonst geradeaus,
            //    aber nur, wenn geradeaus noch Erd-Tile ist
            if (Random.value < turnChance || !options.Contains(dir))
                dir = options[Random.Range(0, options.Count)];

            // d) Schritt ausführen
            pos += dir;
        }

        // 5) Alle gesammelten Koordinaten als RoadTile setzen
        foreach (var cell in roadCells)
        {
            roadLayer.SetTile(new Vector3Int(cell.x, cell.y, 0), roadTile);
        }
    }
}