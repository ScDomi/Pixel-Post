using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Verbindet Haus-Frontzellen per MST und zeichnet organische Straßen,
/// basierend auf der Ausrichtung der Häuser. Wartet auf Haus-Platzierung.
/// </summary>
public class RoadConnector : MonoBehaviour
{
    [Header("Referenzen")]
    public HousePlacer housePlacer;   // HousePlacer-Komponente mit housesContainer
    public Tilemap roadLayer;         // Tilemap für die Straßen
    public Tilemap baseLayer;         // Tilemap für das Terrain
    public Tile roadTile;             // Tile-Asset der Straße
    public TileBase grassTile;        // Referenz zum Grass-Tile

    [Header("Straßenbreiten")]
    public int widthThreshold = 10;   // ab dieser Länge → breite Straße
    public int defaultWidth = 2;      // normale Breite
    public int wideWidth = 4;         // breite Straße

    [Header("Organische Straßen")]
    public float noiseMagnitude = 0.5f;   // Stärke der zufälligen Abweichung
    public float noiseFrequency = 0.1f;   // Häufigkeit der Abweichungen
    public int smoothingPasses = 2;       // Wie oft geglättet wird

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
            DrawOrganicRoad(start.pos, start.dir, end.pos, end.dir);
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

    private void DrawOrganicRoad(Vector3Int start, Vector3Int startDir, Vector3Int end, Vector3Int endDir)
    {
        // Generiere Kontrollpunkte für die Straße
        var points = new List<Vector3>();
        points.Add(start);
        
        // Füge Zwischenpunkte basierend auf Start- und Endrichtung hinzu
        var midPoint = (Vector3)(start + end) * 0.5f;
        var startControl = (Vector3)start + (Vector3)startDir * Vector3.Distance(start, end) * 0.3f;
        var endControl = (Vector3)end - (Vector3)endDir * Vector3.Distance(start, end) * 0.3f;
        
        // Füge organische Variationen hinzu
        for (float t = 0.2f; t <= 0.8f; t += 0.2f)
        {
            var basePoint = BezierPoint(start, startControl, endControl, end, t);
            var noise = Mathf.PerlinNoise(basePoint.x * noiseFrequency, basePoint.y * noiseFrequency) * 2 - 1;
            var perpendicular = new Vector3(-startDir.y, startDir.x, 0) * noise * noiseMagnitude;
            points.Add(Vector3Int.RoundToInt(basePoint + perpendicular));
        }
        points.Add(end);

        // Glätte die Punkte
        for (int i = 0; i < smoothingPasses; i++)
        {
            var smoothed = new List<Vector3> { points[0] };
            for (int j = 1; j < points.Count - 1; j++)
            {
                var avg = (points[j - 1] + points[j] * 2 + points[j + 1]) * 0.25f;
                smoothed.Add(avg);
            }
            smoothed.Add(points[points.Count - 1]);
            points = smoothed;
        }

        // Zeichne die Straße zwischen den Punkten
        for (int i = 0; i < points.Count - 1; i++)
        {
            DrawRoadSegment(Vector3Int.RoundToInt(points[i]), Vector3Int.RoundToInt(points[i + 1]));
        }
    }

    private Vector3 BezierPoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        Vector3 p = uuu * p0;
        p += 3 * uu * t * p1;
        p += 3 * u * tt * p2;
        p += ttt * p3;

        return p;
    }

    private void DrawRoadSegment(Vector3Int start, Vector3Int end)
    {
        var path = GetBresenhamLine(start, end);
        foreach (var pos in path)
        {
            if (!IsValidRoadPosition(pos)) continue;
            
            int width = Vector3Int.Distance(start, end) >= widthThreshold ? wideWidth : defaultWidth;
            int halfWidth = width / 2;

            for (int w = -halfWidth; w < halfWidth; w++)
            {
                var dirVector = ((Vector3)end - (Vector3)start).normalized;
                var perpendicular = new Vector3Int(-Mathf.RoundToInt(dirVector.y), Mathf.RoundToInt(dirVector.x), 0);
                var roadPos = pos + perpendicular * w;
                
                if (IsValidRoadPosition(roadPos))
                    roadLayer.SetTile(roadPos, roadTile);
            }
        }
    }

    private bool IsValidRoadPosition(Vector3Int pos)
    {
        var baseTile = baseLayer.GetTile(pos);
        return baseTile != null && baseTile == grassTile;
    }

    private List<Vector3Int> GetBresenhamLine(Vector3Int start, Vector3Int end)
    {
        var points = new List<Vector3Int>();
        int x = start.x;
        int y = start.y;
        int dx = Mathf.Abs(end.x - start.x);
        int dy = Mathf.Abs(end.y - start.y);
        int sx = start.x < end.x ? 1 : -1;
        int sy = start.y < end.y ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            points.Add(new Vector3Int(x, y, 0));
            if (x == end.x && y == end.y) break;
            
            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y += sy;
            }
        }
        return points;
    }
}
