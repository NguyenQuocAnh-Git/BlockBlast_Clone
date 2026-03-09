using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Simplified SmartBlockSpawner
/// Goals:
/// - Provide shapes that can fit into the current grid
/// - Prefer shapes that maximize immediate row/column clears
/// - Keep implementation small and readable
/// </summary>
public class SmartBlockSpawner : MonoBehaviour
{
    [Header("Smart Spawning")]
    public bool enableSmartSpawning = true;
    // Number of grid cells per side (assumes square grid, default 8)
    // Use MapGenerator's grid size when available.
    private int GridSize => mapGenerator != null ? mapGenerator.GridSize : 8;

    // Reference to map for getting empty cells
    private MapGenerator mapGenerator;

    void Awake()
    {
        mapGenerator = FindObjectOfType<MapGenerator>();
    }

    /// <summary>
    /// Return up to blockCount shapes that fit the current grid.
    /// Prioritize shapes by how many rows/cols they can clear at best placement,
    /// then by number of valid placements (flexibility).
    /// </summary>
    public List<List<Vector2Int>> GetSmartBlockList(int blockCount)
    {
        if (!enableSmartSpawning || mapGenerator == null)
            return GetRandomBlocks(blockCount);

        var emptyCells = mapGenerator.GetEmptyCells();
        if (emptyCells == null || emptyCells.Count == 0)
            return GetRandomBlocks(blockCount);

        var emptySet = new HashSet<Vector2Int>(emptyCells);

        // Gather candidate shapes (use prebuilt shape variations)
        var candidates = new List<List<Vector2Int>>();
        foreach (var shape in ShapeDatabase.AllShapeVariations)
        {
            if (shape == null) continue;
            if (shape.Count > emptyCells.Count) continue;
            candidates.Add(NormalizeShape(shape));
        }

        // Score candidates: (clearPotential, validPositions)
        var scored = new List<(List<Vector2Int> shape, int clearPotential, int validPositions)>();
        foreach (var shape in candidates)
        {
            int validPos = CountValidPositions(shape, emptySet);
            if (validPos == 0) continue;
            int clearPotential = BestClearPotential(shape, emptySet);
            scored.Add((shape, clearPotential, validPos));
        }

        var ordered = scored
            .OrderByDescending(s => s.clearPotential)
            .ThenByDescending(s => s.validPositions)
            .ThenByDescending(s => s.shape.Count)
            .Select(s => s.shape)
            .Distinct(new ShapeListComparer())
            .Take(blockCount)
            .ToList();

        if (ordered.Count < blockCount)
        {
            // Fill remaining slots with candidate shapes that have at least one valid placement,
            // preferring larger shapes so player has meaningful pieces.
            var remaining = new List<List<Vector2Int>>();
            foreach (var c in candidates.OrderByDescending(s => s.Count))
            {
                if (remaining.Count + ordered.Count >= blockCount) break;
                if (ordered.Contains(c, new ShapeListComparer())) continue;
                if (CountValidPositions(c, emptySet) > 0) remaining.Add(c);
            }
            // If still not enough, fallback to random placeable shapes
            int need = blockCount - (ordered.Count + remaining.Count);
            if (need > 0)
            {
                var rndExtras = GetRandomPlaceableBlocks(need, emptySet);
                remaining.AddRange(rndExtras);
            }
            ordered.AddRange(remaining.Take(blockCount - ordered.Count));
        }

        return ordered;
    }

    private static List<Vector2Int> NormalizeShape(List<Vector2Int> shape)
    {
        if (shape == null || shape.Count == 0) return shape;
        int minX = shape.Min(p => p.x);
        int minY = shape.Min(p => p.y);
        var outShape = new List<Vector2Int>(shape.Count);
        foreach (var p in shape) outShape.Add(new Vector2Int(p.x - minX, p.y - minY));
        return outShape;
    }

    private int CountValidPositions(List<Vector2Int> shape, HashSet<Vector2Int> emptySet)
    {
        int count = 0;
        var bbox = GetBoundingBox(shape);
        int maxX = GridSize - bbox.width;
        int maxY = GridSize - bbox.height;
        for (int ax = 0; ax <= maxX; ax++)
            for (int ay = 0; ay <= maxY; ay++)
                if (CanPlaceAt(shape, ax, ay, emptySet)) count++;
        return count;
    }

    private int BestClearPotential(List<Vector2Int> shape, HashSet<Vector2Int> emptySet)
    {
        int best = 0;
        var bbox = GetBoundingBox(shape);
        int maxX = GridSize - bbox.width;
        int maxY = GridSize - bbox.height;
        for (int ax = 0; ax <= maxX; ax++)
        {
            for (int ay = 0; ay <= maxY; ay++)
            {
                if (!CanPlaceAt(shape, ax, ay, emptySet)) continue;
                int clears = SimulateClears(shape, ax, ay, emptySet);
                if (clears > best) best = clears;
            }
        }
        return best;
    }

    private bool CanPlaceAt(List<Vector2Int> shape, int anchorX, int anchorY, HashSet<Vector2Int> emptySet)
    {
        foreach (var o in shape)
        {
            int x = anchorX + o.x;
            int y = anchorY + o.y;
            if (x < 0 || x >= GridSize || y < 0 || y >= GridSize) return false;
            if (!emptySet.Contains(new Vector2Int(x, y))) return false;
        }
        return true;
    }

    private int SimulateClears(List<Vector2Int> shape, int anchorX, int anchorY, HashSet<Vector2Int> emptySet)
    {
        bool[,] grid = new bool[GridSize, GridSize];
        for (int x = 0; x < GridSize; x++)
            for (int y = 0; y < GridSize; y++)
                grid[x, y] = !emptySet.Contains(new Vector2Int(x, y));
        foreach (var o in shape)
        {
            int x = anchorX + o.x;
            int y = anchorY + o.y;
            grid[x, y] = true;
        }
        int clears = 0;
        for (int y = 0; y < GridSize; y++)
        {
            bool rowFull = true;
            for (int x = 0; x < GridSize; x++) if (!grid[x, y]) { rowFull = false; break; }
            if (rowFull) clears++;
        }
        for (int x = 0; x < GridSize; x++)
        {
            bool colFull = true;
            for (int y = 0; y < GridSize; y++) if (!grid[x, y]) { colFull = false; break; }
            if (colFull) clears++;
        }
        return clears;
    }

    private (int minX, int minY, int width, int height) GetBoundingBox(List<Vector2Int> shape)
    {
        int minX = shape.Min(p => p.x);
        int minY = shape.Min(p => p.y);
        int maxX = shape.Max(p => p.x);
        int maxY = shape.Max(p => p.y);
        return (minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private List<List<Vector2Int>> GetRandomBlocks(int count)
    {
        var outList = new List<List<Vector2Int>>();
        for (int i = 0; i < count; i++) outList.Add(TetrisShapes.GetRandomShapeWithRotation());
        return outList;
    }

    // Return random blocks that are placeable given current empty set.
    private List<List<Vector2Int>> GetRandomPlaceableBlocks(int count, HashSet<Vector2Int> emptySet)
    {
        var outList = new List<List<Vector2Int>>();
        int attempts = 0;
        int maxAttempts = count * 20;
        while (outList.Count < count && attempts < maxAttempts)
        {
            attempts++;
            var s = TetrisShapes.GetRandomShapeWithRotation();
            if (s == null) continue;
            var norm = NormalizeShape(s);
            if (CountValidPositions(norm, emptySet) > 0 && !outList.Contains(norm, new ShapeListComparer()))
                outList.Add(norm);
        }
        // As a last resort, fall back to any random shapes (can happen rarely)
        while (outList.Count < count) outList.Add(TetrisShapes.GetRandomShapeWithRotation());
        return outList;
    }
}

// Simple comparer to allow Distinct on List<Vector2Int> by content
internal class ShapeListComparer : IEqualityComparer<List<Vector2Int>>
{
    public bool Equals(List<Vector2Int> a, List<Vector2Int> b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null) return false;
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
        return true;
    }
    public int GetHashCode(List<Vector2Int> shape)
    {
        if (shape == null) return 0;
        int h = 17;
        foreach (var p in shape) h = h * 31 + (p.x * 397) ^ p.y;
        return h;
    }
}
