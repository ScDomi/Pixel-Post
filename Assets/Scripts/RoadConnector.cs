using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Assertions;

/// <summary>
/// Verbindet Haus-Frontzellen per MST und zeichnet Straßen mit A* Pathfinding,
/// basierend auf der Ausrichtung der Häuser und Terrain-Beschränkungen.
/// Wartet auf Haus-Platzierung.
/// </summary>
public class RoadConnector : MonoBehaviour
{
    [Header("Required References")]
    public HousePlacer housePlacer;       // HousePlacer component
    public ProceduralMapGenerator mapGen;  // Reference to map generator for layer access

    [Header("Road Settings")]
    public RuleTile roadTile;             // Road RuleTile for automatic connections

    [Header("Path Constraints")]
    public int maxEarthSteps = 25;        // Maximum consecutive steps on earth tiles
    public int earthPenalty = 5;          // Pathfinding penalty for earth tiles

    private Tilemap roadLayer;            // Reference to road layer
    private Tilemap grassLayer;           // Reference to grass layer
    private Tilemap waterLayer;           // Reference to water layer
    private Tilemap baseLayer;            // Reference to base (earth) layer

    private void Awake()
    {
        Assert.IsNotNull(housePlacer, "HousePlacer reference is required!");
        Assert.IsNotNull(mapGen, "ProceduralMapGenerator reference is required!");
        Assert.IsNotNull(roadTile, "Road tile is required!");
        
        // Get layer references from map generator
        roadLayer = mapGen.roadLayer;
        grassLayer = mapGen.grassLayer;
        waterLayer = mapGen.waterLayer;
        baseLayer = mapGen.baseLayer;
        
        Assert.IsNotNull(roadLayer, "Road layer reference not found in MapGenerator!");
        Assert.IsNotNull(grassLayer, "Grass layer reference not found in MapGenerator!");
        Assert.IsNotNull(waterLayer, "Water layer reference not found in MapGenerator!");
        Assert.IsNotNull(baseLayer, "Base layer reference not found in MapGenerator!");
    }

    private IEnumerator Start()
    {
        yield return new WaitUntil(() => housePlacer.housesContainer != null
                                        && housePlacer.housesContainer.childCount >= 2);
        yield return null;

        var nodes = housePlacer.GetHouseFrontCells();
        if (nodes == null || nodes.Count < 2)
        {
            Debug.LogWarning("RoadConnector: Nicht genug Haus-Frontpunkte (mindestens 2 benötigt).");
            yield break;
        }

        var edges = BuildMST(nodes);
        foreach (var (start, end) in edges)
            ConnectPoints(start.pos, start.dir, end.pos, end.dir);
    }

    private void ConnectPoints(Vector3Int start, Vector3Int startDir, Vector3Int end, Vector3Int endDir)
    {
        var path = FindPath(start, end);
        if (path == null || path.Count == 0)
        {
            Debug.LogWarning($"Could not find path between {start} and {end}");
            return;
        }

        foreach (var pos in path)
        {
            roadLayer.SetTile(pos, roadTile);
        }
    }

    private List<Vector3Int> FindPath(Vector3Int start, Vector3Int end)
    {
        var openSet = new PriorityQueue<Vector3Int>();
        var closedSet = new HashSet<Vector3Int>();
        var cameFrom = new Dictionary<Vector3Int, Vector3Int>();
        var gScore = new Dictionary<Vector3Int, int>();
        var earthSteps = new Dictionary<Vector3Int, int>();

        openSet.Enqueue(start, 0);
        gScore[start] = 0;
        earthSteps[start] = IsEarthTile(start) ? 1 : 0;

        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();
            if (current == end)
                return ReconstructPath(cameFrom, current);

            closedSet.Add(current);

            foreach (var dir in new[] { Vector3Int.up, Vector3Int.right, Vector3Int.down, Vector3Int.left })
            {
                var neighbor = current + dir;

                if (closedSet.Contains(neighbor) || !IsValidPosition(neighbor))
                    continue;

                var isEarth = IsEarthTile(neighbor);
                var newEarthSteps = isEarth ? earthSteps[current] + 1 : 0;

                if (isEarth && newEarthSteps > maxEarthSteps)
                    continue;

                var tentativeGScore = gScore[current] + GetMovementCost(neighbor);

                if (!gScore.ContainsKey(neighbor) || tentativeGScore < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;
                    earthSteps[neighbor] = newEarthSteps;

                    var priority = tentativeGScore + ManhattanDistance(neighbor, end);
                    openSet.Enqueue(neighbor, priority);
                }
            }
        }

        return null;
    }

    private bool IsValidPosition(Vector3Int pos)
    {
        return pos.x >= 0 && pos.x < mapGen.mapWidth &&
               pos.y >= 0 && pos.y < mapGen.mapHeight &&
               waterLayer.GetTile(pos) == null;
    }

    private bool IsEarthTile(Vector3Int pos)
    {
        return baseLayer.GetTile(pos) != null && grassLayer.GetTile(pos) == null;
    }

    private int GetMovementCost(Vector3Int pos)
    {
        return IsEarthTile(pos) ? earthPenalty : 1;
    }

    private int ManhattanDistance(Vector3Int a, Vector3Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private List<Vector3Int> ReconstructPath(Dictionary<Vector3Int, Vector3Int> cameFrom, Vector3Int current)
    {
        var path = new List<Vector3Int> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Add(current);
        }
        path.Reverse();
        return path;
    }

    private List<((Vector3Int pos, Vector3Int dir) a, (Vector3Int pos, Vector3Int dir) b)> BuildMST(List<(Vector3Int pos, Vector3Int dir)> nodes)
    {
        var result = new List<((Vector3Int pos, Vector3Int dir), (Vector3Int pos, Vector3Int dir))>();
        var visited = new HashSet<Vector3Int> { nodes[0].pos };
        var edges = new List<((Vector3Int pos, Vector3Int dir), (Vector3Int pos, Vector3Int dir), int)>();

        foreach (var n in nodes.Skip(1))
            edges.Add((nodes[0], n, Cost(nodes[0].pos, n.pos)));

        while (visited.Count < nodes.Count)
        {
            var best = edges
                .Where(e => visited.Contains(e.Item1.pos) ^ visited.Contains(e.Item2.pos))
                .OrderBy(e => e.Item3)
                .First();

            var newNode = visited.Contains(best.Item1.pos) ? best.Item2 : best.Item1;
            result.Add((best.Item1, best.Item2));
            visited.Add(newNode.pos);

            foreach (var other in nodes.Where(n => !visited.Contains(n.pos)))
                edges.Add((newNode, other, Cost(newNode.pos, other.pos)));
        }
        return result;
    }

    private int Cost(Vector3Int a, Vector3Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private class PriorityQueue<T>
    {
        private List<(T item, int priority)> elements = new List<(T, int)>();

        public int Count => elements.Count;

        public void Enqueue(T item, int priority)
        {
            elements.Add((item, priority));
            elements.Sort((a, b) => a.priority.CompareTo(b.priority));
        }

        public T Dequeue()
        {
            var item = elements[0].item;
            elements.RemoveAt(0);
            return item;
        }
    }
}
