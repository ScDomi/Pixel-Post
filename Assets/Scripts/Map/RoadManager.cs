using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Map;

public class RoadManager : MonoBehaviour
{
    [Header("Required References")]
    public BaseMapManager baseMapManager;
    public ObjectManager objectManager;

    [Header("Road Settings")]
    public int maxEarthSteps = 25;        // Maximum consecutive steps on earth tiles
    public int earthPenalty = 5;          // Pathfinding penalty for earth tiles

    private Tilemap roadLayer;
    private RuleTile roadTile;

    private void Start()
    {
        // Wait for houses to be placed
        StartCoroutine(WaitForHousesAndBuildRoads());
    }

    private IEnumerator WaitForHousesAndBuildRoads()
    {
        // Wait for object manager to place houses
        yield return new WaitForSeconds(0.5f);

        roadLayer = baseMapManager.roadLayer;
        roadTile = baseMapManager.roadTile;

        var houseFronts = objectManager.GetHouseFrontPositions();
        if (houseFronts.Count < 2)
        {
            Debug.LogWarning("RoadManager: Not enough houses to build roads (minimum 2 required).");
            yield break;
        }

        var edges = BuildMST(houseFronts);
        foreach (var (start, end) in edges)
        {
            ConnectPoints(start.position, start.direction, end.position, end.direction);
        }
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
        return pos.x >= 0 && pos.x < baseMapManager.mapWidth &&
               pos.y >= 0 && pos.y < baseMapManager.mapHeight &&
               baseMapManager.waterLayer.GetTile(pos) == null;
    }

    private bool IsEarthTile(Vector3Int pos)
    {
        return baseMapManager.baseLayer.GetTile(pos) != null && 
               baseMapManager.grassLayer.GetTile(pos) == null;
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

    private List<(HouseFront a, HouseFront b)> BuildMST(List<HouseFront> nodes)
    {
        var result = new List<(HouseFront, HouseFront)>();
        var visited = new HashSet<Vector3Int> { nodes[0].position };
        var edges = new List<(HouseFront, HouseFront, int)>();

        // Initialize edges from first node to all others
        foreach (var n in nodes.Skip(1))
            edges.Add((nodes[0], n, Cost(nodes[0].position, n.position)));

        while (visited.Count < nodes.Count)
        {
            // Find the cheapest edge that connects to an unvisited node
            var best = edges
                .Where(e => visited.Contains(e.Item1.position) ^ visited.Contains(e.Item2.position))
                .OrderBy(e => e.Item3)
                .First();

            var newNode = visited.Contains(best.Item1.position) ? best.Item2 : best.Item1;
            result.Add((best.Item1, best.Item2));
            visited.Add(newNode.position);

            // Add new edges from the newly visited node
            foreach (var other in nodes.Where(n => !visited.Contains(n.position)))
                edges.Add((newNode, other, Cost(newNode.position, other.position)));
        }

        return result;
    }

    private int Cost(Vector3Int a, Vector3Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    public struct HouseFront
    {
        public Vector3Int position;
        public Vector3Int direction;

        public HouseFront(Vector3Int pos, Vector3Int dir)
        {
            position = pos;
            direction = dir;
        }
    }

    private class PriorityQueue<T>
    {
        private List<(T item, int priority)> elements = new List<(T, int)>();

        public int Count => elements.Count;

        public void Enqueue(T item, int priority)
        {
            elements.Add((item, priority));
            var currentIndex = elements.Count - 1;
            while (currentIndex > 0)
            {
                var parentIndex = (currentIndex - 1) / 2;
                if (elements[parentIndex].priority <= elements[currentIndex].priority)
                    break;

                var temp = elements[currentIndex];
                elements[currentIndex] = elements[parentIndex];
                elements[parentIndex] = temp;
                currentIndex = parentIndex;
            }
        }

        public T Dequeue()
        {
            var result = elements[0].item;
            elements[0] = elements[elements.Count - 1];
            elements.RemoveAt(elements.Count - 1);

            var currentIndex = 0;
            while (true)
            {
                var smallestIndex = currentIndex;
                var leftChild = 2 * currentIndex + 1;
                var rightChild = 2 * currentIndex + 2;

                if (leftChild < elements.Count && elements[leftChild].priority < elements[smallestIndex].priority)
                    smallestIndex = leftChild;

                if (rightChild < elements.Count && elements[rightChild].priority < elements[smallestIndex].priority)
                    smallestIndex = rightChild;

                if (smallestIndex == currentIndex)
                    break;

                var temp = elements[currentIndex];
                elements[currentIndex] = elements[smallestIndex];
                elements[smallestIndex] = temp;
                currentIndex = smallestIndex;
            }

            return result;
        }
    }
}
