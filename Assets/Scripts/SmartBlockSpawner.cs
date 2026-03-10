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
    [Header("Lookahead")]
    [Tooltip("Enable multi-step lookahead when selecting a set of blocks")]
    public bool enableLookahead = true;
    [Tooltip("Maximum number of candidate shapes to consider for lookahead (keeps combinatorics bounded)")]
    public int lookaheadCandidateLimit = 16;
    [Tooltip("Beam width for lookahead search")]
    public int lookaheadBeamWidth = 8;
    [Tooltip("Max placement anchors per shape to consider during lookahead")]
    public int lookaheadPlacementsLimit = 8;
    // Fixed grid size for optimized lookahead (8x8 game)
    private const int LOOKAHEAD_GRID_SIZE = 8;
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
        // Score candidates with a weighted heuristic to prefer shapes that both clear well and have meaningful size.
        // Heuristic: score = clearPotential * 1000 + shape.Count * 10 + validPositions
        var scored = new List<(List<Vector2Int> shape, int clearPotential, int validPositions, int score)>();
        foreach (var shape in candidates)
        {
            int validPos = CountValidPositions(shape, emptySet);
            if (validPos == 0) continue;
            int clearPotential = BestClearPotential(shape, emptySet);
            int score = (clearPotential * 1000) + (shape.Count * 10) + validPos;
            scored.Add((shape, clearPotential, validPos, score));
        }

        // If lookahead enabled, perform a bounded beam-search over top candidates to select a set of shapes
        // that work well together (maximize cumulative clears and prefer perfect clear).
        var ordered = new List<List<Vector2Int>>();
        if (enableLookahead)
        {
            var topCandidates = scored
                .OrderByDescending(s => s.score)
                .Select(s => s.shape)
                .Distinct(new ShapeListComparer())
                .Take(Mathf.Max(1, lookaheadCandidateLimit))
                .ToList();

            ordered = LookaheadSelect(blockCount, topCandidates, emptySet);
        }

        // fallback to greedy selection if lookahead disabled or returned nothing
        if (ordered == null || ordered.Count == 0)
        {
            ordered = scored
                .OrderByDescending(s => s.score)
                .ThenByDescending(s => s.clearPotential)
                .Select(s => s.shape)
                .Distinct(new ShapeListComparer())
                .Take(blockCount)
                .ToList();
        }

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
        // Deterministic fallback: pick first 'count' variations that fit within the grid.
        var outList = new List<List<Vector2Int>>();
        foreach (var v in ShapeDatabase.AllShapeVariations)
        {
            if (v == null) continue;
            if (v.Count > GridSize * GridSize) continue;
            outList.Add(NormalizeShape(v));
            if (outList.Count >= count) break;
        }
        return outList;
    }

    // Return random blocks that are placeable given current empty set.
    private List<List<Vector2Int>> GetRandomPlaceableBlocks(int count, HashSet<Vector2Int> emptySet)
    {
        // Deterministic: iterate precomputed variations and collect those placeable
        var outList = new List<List<Vector2Int>>();
        foreach (var v in ShapeDatabase.AllShapeVariations)
        {
            if (v == null) continue;
            var norm = NormalizeShape(v);
            if (CountValidPositions(norm, emptySet) > 0 && !outList.Contains(norm, new ShapeListComparer()))
            {
                outList.Add(norm);
                if (outList.Count >= count) break;
            }
        }
        // If still not enough, return what we have (caller may pad)
        return outList;
    }

    // -----------------------
    // Lookahead helpers
    // -----------------------
    private List<List<Vector2Int>> LookaheadSelect(int blockCount, List<List<Vector2Int>> topCandidates, HashSet<Vector2Int> emptySet)
    {
        int gridSize = LOOKAHEAD_GRID_SIZE;

        // Beam node
        var beam = new List<(HashSet<Vector2Int> empty, List<List<Vector2Int>> picks, int score)>();
        beam.Add((new HashSet<Vector2Int>(emptySet), new List<List<Vector2Int>>(), 0));

        for (int depth = 0; depth < blockCount; depth++)
        {
            var expansions = new List<(HashSet<Vector2Int> empty, List<List<Vector2Int>> picks, int score)>();

            foreach (var node in beam)
            {
                foreach (var shape in topCandidates)
                {
                    // get valid anchors for this shape on this node's empty set
                    var anchors = GetValidAnchors(shape, node.empty);
                    if (anchors == null || anchors.Count == 0) continue;

                    // Evaluate placements but limit to best N placements per shape to bound branching
                    var placementResults = new List<(Vector2Int anchor, int clears, HashSet<Vector2Int> newEmpty)>();
                    foreach (var a in anchors)
                    {
                        var (clears, newEmpty) = ApplyPlacementAndClear(node.empty, shape, a.x, a.y);
                        placementResults.Add((a, clears, newEmpty));
                    }
                    var topPlacements = placementResults
                        .OrderByDescending(p => p.clears)
                        .ThenByDescending(p => shape.Count)
                        .Take(lookaheadPlacementsLimit)
                        .ToList();

                    foreach (var p in topPlacements)
                    {
                        // stronger weighting for grid8: emphasize clears and larger shapes
                        int reward = p.clears * 2000 + shape.Count * 100;
                        if (ShapeDatabase.IsSmallShape(shape)) reward -= 50; // slight penalty for small shapes
                        if (p.newEmpty.Count == gridSize * gridSize) reward += 500000; // big perfect-clear bonus
                        var newPicks = new List<List<Vector2Int>>(node.picks) { shape };
                        expansions.Add((new HashSet<Vector2Int>(p.newEmpty), newPicks, node.score + reward));
                    }
                }
            }

            if (expansions.Count == 0) break;

            // Keep top beamWidth expansions
            beam = expansions
                .OrderByDescending(b => b.score)
                .Take(lookaheadBeamWidth)
                .Select(b => (b.empty, b.picks, b.score))
                .ToList();
        }

        // choose best beam node
        var best = beam.OrderByDescending(b => b.score).FirstOrDefault();
        var result = best.picks;
        // pad with random placeable blocks if needed
        if (result == null) result = new List<List<Vector2Int>>();
        while (result.Count < blockCount)
        {
            var r = GetRandomPlaceableBlocks(1, emptySet);
            if (r != null && r.Count > 0) result.Add(r[0]);
            else result.Add(TetrisShapes.GetRandomShapeWithRotation());
        }
        return result.Take(blockCount).ToList();
    }

    private List<Vector2Int> GetValidAnchors(List<Vector2Int> shape, HashSet<Vector2Int> emptySet)
    {
        var anchors = new List<Vector2Int>();
        var bbox = GetBoundingBox(shape);
        int maxX = LOOKAHEAD_GRID_SIZE - bbox.width;
        int maxY = LOOKAHEAD_GRID_SIZE - bbox.height;
        for (int ax = 0; ax <= maxX; ax++)
            for (int ay = 0; ay <= maxY; ay++)
                if (CanPlaceAt(shape, ax, ay, emptySet))
                    anchors.Add(new Vector2Int(ax, ay));
        return anchors;
    }

    // Simulate placing shape at anchor (assumes CanPlaceAt is true), then clear full rows/cols and return (clearsCount, newEmptySet)
    private (int clears, HashSet<Vector2Int> newEmpty) ApplyPlacementAndClear(HashSet<Vector2Int> emptySet, List<Vector2Int> shape, int anchorX, int anchorY)
    {
        var newEmpty = new HashSet<Vector2Int>(emptySet);
        // place: remove occupied cells
        foreach (var o in shape)
        {
            int x = anchorX + o.x;
            int y = anchorY + o.y;
            newEmpty.Remove(new Vector2Int(x, y));
        }

        int gridSize = LOOKAHEAD_GRID_SIZE;
        var clearedRows = new List<int>();
        var clearedCols = new List<int>();

        // detect full rows
        for (int y = 0; y < gridSize; y++)
        {
            bool rowFull = true;
            for (int x = 0; x < gridSize; x++)
            {
                if (newEmpty.Contains(new Vector2Int(x, y))) { rowFull = false; break; }
            }
            if (rowFull) clearedRows.Add(y);
        }

        // detect full cols
        for (int x = 0; x < gridSize; x++)
        {
            bool colFull = true;
            for (int y = 0; y < gridSize; y++)
            {
                if (newEmpty.Contains(new Vector2Int(x, y))) { colFull = false; break; }
            }
            if (colFull) clearedCols.Add(x);
        }

        // Apply clears: add those cells back to empty set
        foreach (var y in clearedRows)
            for (int x = 0; x < gridSize; x++)
                newEmpty.Add(new Vector2Int(x, y));

        foreach (var x in clearedCols)
            for (int y = 0; y < gridSize; y++)
                newEmpty.Add(new Vector2Int(x, y));

        int clears = clearedRows.Count + clearedCols.Count;
        return (clears, newEmpty);
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
