using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System.Linq;

namespace Map
{
    public abstract class BaseRegion
    {
        public string Name { get; protected set; }
        public int Size { get; protected set; }
        public List<Vector3Int> Tiles { get; protected set; }
        public Vector3 Center { get; protected set; }
        public BoundsInt Bounds { get; protected set; }

        protected BaseRegion(string name, List<Vector3Int> tiles, Tilemap baseLayer)
        {
            Name = name;
            Tiles = tiles;
            Size = tiles.Count;
            
            // Calculate center in world space
            var sum = Vector3.zero;
            foreach (var tile in tiles)
            {
                sum += baseLayer.CellToWorld(tile) + new Vector3(0.5f, 0.5f, 0f);
            }
            Center = sum / Size;

            // Calculate region bounds
            var minX = tiles.Min(t => t.x);
            var maxX = tiles.Max(t => t.x);
            var minY = tiles.Min(t => t.y);
            var maxY = tiles.Max(t => t.y);
            
            Bounds = new BoundsInt(
                new Vector3Int(minX, minY, 0),
                new Vector3Int(maxX - minX + 1, maxY - minY + 1, 1)
            );
        }
    }

    public class EarthRegion : BaseRegion
    {
        public EarthRegion(string name, List<Vector3Int> tiles, Tilemap baseLayer)
            : base(name, tiles, baseLayer) { }
    }

    public class GrassRegion : BaseRegion
    {
        public GrassRegion(string name, List<Vector3Int> tiles, Tilemap baseLayer)
            : base(name, tiles, baseLayer) { }
    }

    public class LakeRegion : BaseRegion
    {
        public LakeRegion(string name, List<Vector3Int> tiles, Tilemap baseLayer)
            : base(name, tiles, baseLayer) { }
    }

    public class HouseRegion : BaseRegion
    {
        public int MaxHouses { get; private set; }

        public HouseRegion(string name, List<Vector3Int> tiles, Tilemap baseLayer)
            : base(name, tiles, baseLayer)
        {
            // Calculate max houses based on region size
            MaxHouses = Mathf.Max(1, Mathf.FloorToInt(Mathf.Sqrt(Size) / 2));
        }
    }
}
